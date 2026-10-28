using System.Collections.Immutable;
using AncientOrder.Rules.Common;

namespace AncientOrder.Rules.Combat;

/// <summary>The new state after an action and the record of what happened.</summary>
public sealed record ActionResolution(CombatState State, ActionResult Result);

/// <summary>Position-preserving edits shared by the resolution code. Every edit returns a new state.</summary>
internal static class CombatStateEdits
{
    public static Combatant? Find(CombatState state, string id) =>
        state.PlayerParty.FirstOrDefault(c => c.Id == id) ?? state.EnemyParty.FirstOrDefault(c => c.Id == id);

    public static bool IsPlayer(CombatState state, string id) => state.PlayerParty.Any(c => c.Id == id);

    public static ImmutableArray<Combatant> OwnParty(CombatState state, string combatantId) =>
        IsPlayer(state, combatantId) ? state.PlayerParty : state.EnemyParty;

    public static CombatState Replace(CombatState state, Combatant updated) =>
        IsPlayer(state, updated.Id)
            ? state with { PlayerParty = [.. state.PlayerParty.Select(c => c.Id == updated.Id ? updated : c)] }
            : state with { EnemyParty = [.. state.EnemyParty.Select(c => c.Id == updated.Id ? updated : c)] };

    /// <summary>Changes stamina by delta, clamped to [0, MaxStamina]; KO when it reaches 0.</summary>
    public static CombatState ApplyStaminaDelta(CombatState state, string targetId, double delta)
    {
        Combatant Update(Combatant c)
        {
            if (c.Id != targetId)
            {
                return c;
            }

            var stamina = Math.Min(c.MaxStamina, Math.Max(0, c.Stamina + delta));
            return c with { Stamina = stamina, IsKO = stamina <= 0 };
        }

        return IsPlayer(state, targetId)
            ? state with { PlayerParty = [.. state.PlayerParty.Select(Update)] }
            : state with { EnemyParty = [.. state.EnemyParty.Select(Update)] };
    }
}

public static class Pipeline
{
    /// <summary>
    /// Puts actions in resolution order: priority (GROUP, DEFEND, ATTACK/SPECIAL, EVADE), then effective speed (a GROUP uses its
    /// team's average non-KO speed), highest first. Every action sharing its priority and speed with another draws one roll,
    /// in the given order, before anything resolves; higher rolls go first and equal rolls keep the given order.
    /// </summary>
    public static ImmutableArray<CombatAction> Order(IReadOnlyList<CombatAction> actions, CombatState state, IRollSource rolls)
    {
        var priorities = new int[actions.Count];
        var speeds = new double[actions.Count];
        for (var i = 0; i < actions.Count; i++)
        {
            priorities[i] = CombatConstants.ActionPriority(actions[i].Type);
            speeds[i] = EffectiveSpeed(actions[i], state);
        }

        var tieRolls = new double[actions.Count];
        for (var i = 0; i < actions.Count; i++)
        {
            for (var j = 0; j < actions.Count; j++)
            {
                if (j != i && priorities[j] == priorities[i] && speeds[j] == speeds[i])
                {
                    tieRolls[i] = rolls.Roll();
                    break;
                }
            }
        }

        return [.. Enumerable.Range(0, actions.Count)
            .OrderBy(i => priorities[i])
            .ThenByDescending(i => speeds[i])
            .ThenByDescending(i => tieRolls[i])
            .ThenBy(i => i)
            .Select(i => actions[i])];
    }

    private static double EffectiveSpeed(CombatAction action, CombatState state)
    {
        if (action.Type == ActionType.Group)
        {
            var active = CombatStateEdits.OwnParty(state, action.CombatantId).Where(c => !c.IsKO).ToList();
            if (active.Count == 0)
            {
                return 0;
            }

            var total = 0.0;
            foreach (var c in active)
            {
                total += c.Speed;
            }

            return total / active.Count;
        }

        return CombatStateEdits.Find(state, action.CombatantId)?.Speed ?? 0;
    }

    /// <summary>Resolves one action against the state. The state's ActionQueue is read for DEFEND intercepts.</summary>
    public static ActionResolution ResolveAction(CombatState state, CombatAction action, IRollSource rolls)
    {
        switch (action.Type)
        {
            case ActionType.Group:
                return GroupAction.Resolve(state, new GroupActionDeclaration(action.CombatantId, action.TargetId ?? string.Empty), GroupAction.DefaultConfig, rolls);
            case ActionType.Defend:
                return new ActionResolution(state, new ActionResult(action.CombatantId, ActionType.Defend, null));
            case ActionType.Evade:
                return ResolveEvade(state, action);
            default:
                return ResolveAttack(state, action, rolls);
        }
    }

    private static ActionResolution ResolveEvade(CombatState state, CombatAction action)
    {
        var minimal = new ActionResult(action.CombatantId, ActionType.Evade, null);
        var combatant = CombatStateEdits.Find(state, action.CombatantId);
        if (combatant is null || combatant.IsKO)
        {
            return new ActionResolution(state, minimal);
        }

        var regen = Formulas.CalculateEvadeRegen(combatant.MaxStamina);
        var current = CombatStateEdits.ApplyStaminaDelta(state, combatant.Id, regen);
        var fresh = CombatStateEdits.Find(current, combatant.Id)!;
        var recovered = fresh.Stamina - combatant.Stamina;
        var withEnergy = Energy.AddEnergySegments(fresh, EnergyEvent.Action, true);
        current = CombatStateEdits.Replace(current, Energy.CheckAscensionAdvance(withEnergy));
        return new ActionResolution(current, minimal with { EvadeRecovery = recovered });
    }

    /// <summary>The first DEFEND in queue order aimed at this attack's target that is still standing takes the hit; otherwise the target does.</summary>
    private static string ResolveDefendIntercept(CombatState state, CombatAction action)
    {
        if (action.TargetId is null)
        {
            return string.Empty;
        }

        foreach (var queued in state.ActionQueue)
        {
            if (queued.Type == ActionType.Defend && queued.TargetId == action.TargetId)
            {
                var interceptor = CombatStateEdits.Find(state, queued.CombatantId);
                if (interceptor is not null && !interceptor.IsKO)
                {
                    return queued.CombatantId;
                }
            }
        }

        return action.TargetId;
    }

    private static ActionResolution ResolveAttack(CombatState state, CombatAction action, IRollSource rolls)
    {
        var minimal = new ActionResult(action.CombatantId, action.Type, null);
        var minimalResolution = new ActionResolution(state, minimal);

        if (action.TargetId is null)
        {
            return minimalResolution;
        }

        var attacker = CombatStateEdits.Find(state, action.CombatantId);
        if (attacker is null || attacker.IsKO)
        {
            return minimalResolution;
        }

        var trueTargetId = ResolveDefendIntercept(state, action);
        var target = CombatStateEdits.Find(state, trueTargetId);
        if (target is null || target.IsKO)
        {
            return minimalResolution;
        }

        var rankKO = false;
        if (attacker.Rank - target.Rank >= 0.5)
        {
            var threshold = Formulas.CalculateRankKOThreshold(attacker.Rank, target.Rank);
            rankKO = Formulas.CheckRankKO(threshold, rolls.Roll());
        }

        var blindside = false;
        if (attacker.Speed > target.Speed)
        {
            var threshold = Formulas.CalculateBlindsideThreshold(attacker.Speed, target.Speed);
            blindside = Formulas.CheckBlindside(threshold, rolls.Roll());
        }

        DefenseType selectedDefense;
        if (blindside)
        {
            selectedDefense = DefenseType.Defenseless;
        }
        else if (action.Type == ActionType.Special)
        {
            selectedDefense = ElementalPaths.GetSpecialForceDefense(attacker.ElementalPath);
        }
        else
        {
            selectedDefense = ElementalPaths.GetPreferredDefense(target.ElementalPath);
        }

        // Action power (spreadsheet Math!O): one rounding step after any Special boost.
        var actionPower = attacker.Power;
        if (action.Type == ActionType.Special)
        {
            var segments = action.EnergySegments ?? attacker.Energy;
            actionPower = Formulas.CalculateSpecialDamageBonus(actionPower, segments);
        }

        var rawDamage = Formulas.CalculateBaseDamage(actionPower);

        var effectiveSkills = Defense.EffectiveReactionSkills(target);
        var defenseRoll = rolls.Roll();
        var defenseResult = Defense.ResolveDefense(selectedDefense, rawDamage, effectiveSkills, defenseRoll);

        // Crushing Blow compares the attack's action power (rawDamage), not the attacker's base Power.
        var crushingBlow = false;
        if (selectedDefense == DefenseType.Block && rawDamage > target.Power)
        {
            var cbThreshold = Formulas.CalculateCrushingBlowThreshold(rawDamage, target.Power);
            crushingBlow = Formulas.CheckCrushingBlow(cbThreshold, rolls.Roll());
        }

        var finalDamage = rawDamage * defenseResult.DamageMultiplier;
        var current = CombatStateEdits.ApplyStaminaDelta(state, trueTargetId, -finalDamage);

        var counterChainTriggered = false;
        var exchanges = ImmutableArray<CounterExchange>.Empty;
        if (selectedDefense == DefenseType.Parry && defenseResult.Success)
        {
            var freshAttacker = CombatStateEdits.Find(current, attacker.Id)!;
            var freshTarget = CombatStateEdits.Find(current, trueTargetId)!;
            var chain = CounterChain.Resolve(current, freshAttacker, freshTarget, rolls);
            current = chain.State;
            counterChainTriggered = chain.ChainLength > 0;
            exchanges = chain.Exchanges;
        }

        if (selectedDefense != DefenseType.Defenseless)
        {
            var targetBeforeXp = CombatStateEdits.Find(current, trueTargetId)!;
            current = CombatStateEdits.Replace(current, ReactionProgression.AwardReactionXp(targetBeforeXp, ToReaction(selectedDefense), defenseResult.Success));
        }

        var attackerAfterDamage = CombatStateEdits.Find(current, attacker.Id)!;
        var attackerWithEnergy = Energy.AddEnergySegments(attackerAfterDamage, EnergyEvent.Action, finalDamage > 0);
        current = CombatStateEdits.Replace(current, Energy.CheckAscensionAdvance(attackerWithEnergy));

        var targetAfterDamage = CombatStateEdits.Find(current, trueTargetId)!;
        if (!targetAfterDamage.IsKO)
        {
            var targetWithEnergy = Energy.AddEnergySegments(targetAfterDamage, EnergyEvent.Reaction, defenseResult.Success);
            current = CombatStateEdits.Replace(current, Energy.CheckAscensionAdvance(targetWithEnergy));
        }

        var finalAttacker = CombatStateEdits.Find(current, attacker.Id)!;
        var finalTarget = CombatStateEdits.Find(current, trueTargetId)!;

        if (finalDamage > 0 && ElementalPaths.Config(finalAttacker.ElementalPath).Type == PathType.Action)
        {
            current = CombatStateEdits.Replace(current, ElementalPaths.ApplyPathDebuff(finalTarget, finalAttacker.ElementalPath));
        }

        if (defenseResult.Success)
        {
            var targetForBuff = CombatStateEdits.Find(current, trueTargetId)!;
            if (ElementalPaths.Config(targetForBuff.ElementalPath).Type == PathType.Reaction)
            {
                current = CombatStateEdits.Replace(current, ElementalPaths.ApplyPathBuff(targetForBuff, targetForBuff.ElementalPath));
            }
        }

        if (action.Type == ActionType.Special)
        {
            var attackerForSpecial = CombatStateEdits.Find(current, attacker.Id)!;
            var segmentsUsed = action.EnergySegments ?? attackerForSpecial.Energy;
            current = CombatStateEdits.Replace(current, Energy.SpendEnergy(attackerForSpecial, segmentsUsed));
        }

        if (crushingBlow)
        {
            current = ApplyCrushingBlowDebuff(current, trueTargetId);
        }

        if (rankKO)
        {
            current = CombatStateEdits.ApplyStaminaDelta(current, trueTargetId, double.NegativeInfinity);
        }

        var attackResult = new AttackResult(
            action.CombatantId,
            trueTargetId,
            finalDamage,
            selectedDefense,
            defenseResult,
            rankKO,
            blindside,
            crushingBlow,
            counterChainTriggered);

        return new ActionResolution(current, new ActionResult(action.CombatantId, action.Type, attackResult) { CounterExchanges = exchanges });
    }

    private static ReactionKind ToReaction(DefenseType defense) => defense switch
    {
        DefenseType.Block => ReactionKind.Block,
        DefenseType.Dodge => ReactionKind.Dodge,
        DefenseType.Parry => ReactionKind.Parry,
        _ => throw new ArgumentOutOfRangeException(nameof(defense)),
    };

    private static CombatState ApplyCrushingBlowDebuff(CombatState state, string targetId)
    {
        static double Reduce(double v) => Math.Max(0, v - CombatConstants.CrushingBlowDebuff);

        var target = CombatStateEdits.Find(state, targetId);
        if (target is null)
        {
            return state;
        }

        var block = target.ReactionSkills.Block;
        var debuffed = target with
        {
            ReactionSkills = target.ReactionSkills with
            {
                Block = new DefenseSkill(Reduce(block.SR), Reduce(block.SMR), Reduce(block.FMR)),
            },
        };
        return CombatStateEdits.Replace(state, debuffed);
    }
}
