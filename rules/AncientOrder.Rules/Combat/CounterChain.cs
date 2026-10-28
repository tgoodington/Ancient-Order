using System.Collections.Immutable;
using AncientOrder.Rules.Common;

namespace AncientOrder.Rules.Combat;

public sealed record CounterChainResult(CombatState State, int ChainLength, ImmutableArray<CounterExchange> Exchanges);

public static class CounterChain
{
    /// <summary>
    /// Resolves a Parry counter chain after a successful initial Parry: the parrier counter-attacks the original attacker, and the
    /// chain continues (roles swap) only while each reaction is a successful Parry. Stops at KO or at MaxChainDepth (silently).
    /// </summary>
    public static CounterChainResult Resolve(CombatState state, Combatant originalAttacker, Combatant parrier, IRollSource rolls)
    {
        var current = state;
        var exchanges = ImmutableArray.CreateBuilder<CounterExchange>();
        var chainLength = 0;

        var attackerId = parrier.Id;
        var targetId = originalAttacker.Id;

        while (chainLength < CombatConstants.MaxChainDepth)
        {
            var attacker = CombatStateEdits.Find(current, attackerId);
            var target = CombatStateEdits.Find(current, targetId);

            if (attacker is null || target is null || attacker.IsKO || target.IsKO)
            {
                break;
            }

            if (target.Stamina <= 0)
            {
                break;
            }

            var rawDamage = Formulas.CalculateBaseDamage(attacker.Power);
            var selectedDefense = ElementalPaths.GetPreferredDefense(target.ElementalPath);
            var defenseRoll = rolls.Roll();
            var outcome = Defense.ResolveDefense(selectedDefense, rawDamage, Defense.EffectiveReactionSkills(target), defenseRoll);
            var finalDamage = rawDamage * outcome.DamageMultiplier;

            chainLength += 1;
            exchanges.Add(new CounterExchange(attackerId, targetId, selectedDefense, outcome, finalDamage));

            current = ApplyDamage(current, targetId, finalDamage);

            if (selectedDefense != DefenseType.Defenseless)
            {
                var reactor = CombatStateEdits.Find(current, targetId);
                if (reactor is not null)
                {
                    current = CombatStateEdits.Replace(current, ReactionProgression.AwardReactionXp(reactor, ToReaction(selectedDefense), outcome.Success));
                }
            }

            if (selectedDefense == DefenseType.Parry && outcome.Success)
            {
                var updatedTarget = CombatStateEdits.Find(current, targetId);
                if (updatedTarget is null || updatedTarget.IsKO)
                {
                    break;
                }

                (attackerId, targetId) = (targetId, attackerId);
            }
            else
            {
                break;
            }
        }

        return new CounterChainResult(current, chainLength, exchanges.ToImmutable());
    }

    // Unlike an attack, counter damage is not capped at max stamina (it only subtracts).
    private static CombatState ApplyDamage(CombatState state, string targetId, double damage)
    {
        var inPlayer = state.PlayerParty.Any(c => c.Id == targetId);
        var inEnemy = state.EnemyParty.Any(c => c.Id == targetId);
        if (!inPlayer && !inEnemy)
        {
            return state;
        }

        Combatant Update(Combatant c)
        {
            if (c.Id != targetId)
            {
                return c;
            }

            var stamina = Math.Max(0, c.Stamina - damage);
            return c with { Stamina = stamina, IsKO = stamina <= 0 };
        }

        return inPlayer
            ? state with { PlayerParty = [.. state.PlayerParty.Select(Update)] }
            : state with { EnemyParty = [.. state.EnemyParty.Select(Update)] };
    }

    private static ReactionKind ToReaction(DefenseType defense) => defense switch
    {
        DefenseType.Block => ReactionKind.Block,
        DefenseType.Dodge => ReactionKind.Dodge,
        DefenseType.Parry => ReactionKind.Parry,
        _ => throw new ArgumentOutOfRangeException(nameof(defense)),
    };
}
