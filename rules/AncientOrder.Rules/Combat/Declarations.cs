using System.Collections.Immutable;
using System.Globalization;

namespace AncientOrder.Rules.Combat;

/// <summary>Outcome of checking one order. Fallback is a safe replacement order when one exists (GROUP to ATTACK).</summary>
public sealed record DeclarationCheck(bool Valid, string? Error, CombatAction? Fallback)
{
    public static DeclarationCheck Ok { get; } = new(true, null, null);

    public static DeclarationCheck Fail(string error, CombatAction? fallback = null) => new(false, error, fallback);
}

public sealed record RejectedOrder(int Index, string Reason);

/// <summary>The orders kept (in declaration order, fallbacks substituted, GROUP overrides applied) and the ones refused.</summary>
public sealed record DeclarationBatch(ImmutableArray<CombatAction> Accepted, ImmutableArray<RejectedOrder> Rejected);

public static class Declarations
{
    public static string DuplicateOrderError(string combatantId) =>
        $"Combatant '{combatantId}' already has an order this round";

    /// <summary>Checks one order against the state, in today's order: fighter active, known type, target rules, stamina, Special energy.</summary>
    public static DeclarationCheck Validate(CombatState state, CombatAction action)
    {
        var combatant = Find(state, action.CombatantId);
        if (combatant is null)
        {
            return DeclarationCheck.Fail($"Combatant '{action.CombatantId}' not found in combat");
        }

        if (combatant.IsKO)
        {
            return DeclarationCheck.Fail($"Combatant '{action.CombatantId}' is KO'd and cannot act");
        }

        if (!Enum.IsDefined(action.Type))
        {
            return DeclarationCheck.Fail($"'{((int)action.Type).ToString(CultureInfo.InvariantCulture)}' is not a valid action type");
        }

        var targetCheck = action.Type switch
        {
            ActionType.Attack => ValidateAttackTarget(state, action),
            ActionType.Special => ValidateAttackTarget(state, action),
            ActionType.Defend => ValidateDefendTarget(state, action),
            ActionType.Evade => ValidateEvadeTarget(action),
            _ => ValidateGroupTarget(state, action),
        };
        if (!targetCheck.Valid)
        {
            return targetCheck;
        }

        if (combatant.Stamina <= 0)
        {
            return DeclarationCheck.Fail($"Combatant '{action.CombatantId}' has no stamina and cannot act");
        }

        if (action.Type == ActionType.Special && combatant.Energy <= 0)
        {
            return DeclarationCheck.Fail($"Combatant '{action.CombatantId}' has no energy segments for SPECIAL");
        }

        return DeclarationCheck.Ok;
    }

    /// <summary>
    /// Validates a whole declaration: each order in turn against the state, a fighter holding an accepted order refuses a second one,
    /// an invalid order is replaced by its fallback when it has one and dropped otherwise, and an accepted GROUP removes the
    /// living allies' orders.
    /// </summary>
    public static DeclarationBatch ValidateAll(CombatState state, IReadOnlyList<CombatAction> orders)
    {
        var accepted = new List<CombatAction>();
        var rejected = ImmutableArray.CreateBuilder<RejectedOrder>();
        var holders = new HashSet<string>(StringComparer.Ordinal);
        string? groupLeaderId = null;

        for (var i = 0; i < orders.Count; i++)
        {
            var order = orders[i];
            if (holders.Contains(order.CombatantId))
            {
                rejected.Add(new RejectedOrder(i, DuplicateOrderError(order.CombatantId)));
                continue;
            }

            var check = Validate(state, order);
            if (check.Valid)
            {
                accepted.Add(order);
                holders.Add(order.CombatantId);
                if (order.Type == ActionType.Group)
                {
                    groupLeaderId = order.CombatantId;
                }
            }
            else if (check.Fallback is not null)
            {
                accepted.Add(check.Fallback);
                holders.Add(check.Fallback.CombatantId);
            }
            else
            {
                rejected.Add(new RejectedOrder(i, check.Error ?? string.Empty));
            }
        }

        if (groupLeaderId is not null)
        {
            var leaderId = groupLeaderId;
            var leaderParty = state.PlayerParty.Any(c => c.Id == leaderId) ? state.PlayerParty : state.EnemyParty;
            var overridden = new HashSet<string>(StringComparer.Ordinal);
            foreach (var ally in leaderParty)
            {
                if (!ally.IsKO && ally.Id != leaderId)
                {
                    overridden.Add(ally.Id);
                }
            }

            accepted = [.. accepted.Where(a => !overridden.Contains(a.CombatantId))];
        }

        return new DeclarationBatch([.. accepted], rejected.ToImmutable());
    }

    // The TypeScript message says ATTACK for SPECIAL and GROUP too (one shared check); kept as is.
    private static DeclarationCheck ValidateAttackTarget(CombatState state, CombatAction action)
    {
        if (action.TargetId is null)
        {
            return DeclarationCheck.Fail("ATTACK requires a target");
        }

        var opposing = FindOpposingParty(state, action.CombatantId);
        if (opposing is null)
        {
            return DeclarationCheck.Fail($"Combatant '{action.CombatantId}' is not in a valid party");
        }

        var target = opposing.Value.FirstOrDefault(c => c.Id == action.TargetId);
        if (target is null)
        {
            return DeclarationCheck.Fail($"Target '{action.TargetId}' is not a valid enemy target");
        }

        if (target.IsKO)
        {
            return DeclarationCheck.Fail($"Target '{action.TargetId}' is already KO'd");
        }

        return DeclarationCheck.Ok;
    }

    private static DeclarationCheck ValidateDefendTarget(CombatState state, CombatAction action)
    {
        if (action.TargetId is null)
        {
            return DeclarationCheck.Fail("DEFEND requires a target ally");
        }

        var ownParty = FindParty(state, action.CombatantId);
        if (ownParty is null)
        {
            return DeclarationCheck.Fail($"Combatant '{action.CombatantId}' is not in a valid party");
        }

        var target = ownParty.Value.FirstOrDefault(c => c.Id == action.TargetId);
        if (target is null)
        {
            return DeclarationCheck.Fail($"Target '{action.TargetId}' is not a valid ally target");
        }

        if (target.IsKO)
        {
            return DeclarationCheck.Fail($"Target '{action.TargetId}' is already KO'd and cannot be defended");
        }

        return DeclarationCheck.Ok;
    }

    private static DeclarationCheck ValidateEvadeTarget(CombatAction action) =>
        action.TargetId is not null
            ? DeclarationCheck.Fail("EVADE does not take a target; targetId must be null")
            : DeclarationCheck.Ok;

    private static DeclarationCheck ValidateGroupTarget(CombatState state, CombatAction action)
    {
        var targetCheck = ValidateAttackTarget(state, action);
        if (!targetCheck.Valid)
        {
            return targetCheck;
        }

        var ownParty = FindParty(state, action.CombatantId);
        if (ownParty is null)
        {
            return DeclarationCheck.Fail($"Combatant '{action.CombatantId}' is not in a valid party");
        }

        var fallback = new CombatAction(action.CombatantId, ActionType.Attack, action.TargetId, null);
        foreach (var ally in ownParty.Value)
        {
            if (ally.IsKO)
            {
                continue;
            }

            if (ally.Energy < ally.MaxEnergy)
            {
                return DeclarationCheck.Fail($"GROUP rejected: '{ally.Id}' does not have full energy ({Num(ally.Energy)}/{Num(ally.MaxEnergy)})", fallback);
            }
        }

        return DeclarationCheck.Ok;
    }

    private static string Num(double value) => value.ToString("R", CultureInfo.InvariantCulture);

    private static Combatant? Find(CombatState state, string id) =>
        state.PlayerParty.FirstOrDefault(c => c.Id == id) ?? state.EnemyParty.FirstOrDefault(c => c.Id == id);

    private static ImmutableArray<Combatant>? FindParty(CombatState state, string id)
    {
        if (state.PlayerParty.Any(c => c.Id == id))
        {
            return state.PlayerParty;
        }

        return state.EnemyParty.Any(c => c.Id == id) ? state.EnemyParty : null;
    }

    private static ImmutableArray<Combatant>? FindOpposingParty(CombatState state, string id)
    {
        if (state.PlayerParty.Any(c => c.Id == id))
        {
            return state.EnemyParty;
        }

        return state.EnemyParty.Any(c => c.Id == id) ? state.PlayerParty : null;
    }
}
