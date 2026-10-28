using System.Collections.Immutable;
using System.Globalization;
using AncientOrder.Rules.Combat;
using AncientOrder.Rules.Common;

namespace AncientOrder.Rules.Session;

internal static class CombatOperations
{
    public static Outcome<StartEncounterResult> StartEncounter(SessionContext context, SessionSnapshot s, string? encounterId)
    {
        var invalid = SessionValidation.IdString("encounterId", encounterId);
        if (invalid is not null)
        {
            return Outcome.Refuse<StartEncounterResult>(s, invalid);
        }

        if (s.Game is null)
        {
            return Outcome.NoActiveGame<StartEncounterResult>(s);
        }

        var started = FightSetup.Start(context.Content, s.Game, encounterId!, context.Seeds.NextSeed(), context.Now());
        if (!started.IsOk)
        {
            return Outcome.Refuse<StartEncounterResult>(s, started.Error);
        }

        var game = started.Value;
        var combat = game.Combat!;
        return Outcome.Change(
            s with { Game = game, PendingOrders = ImmutableArray<CombatAction>.Empty, LastCombatOutcome = null },
            new StartEncounterResult(encounterId!, combat.Round, combat.Status));
    }

    public static Outcome<DeclarationResult> DeclareOrders(SessionContext context, SessionSnapshot s, IReadOnlyList<CombatAction?>? orders)
    {
        // One copy, validated and used; the caller's list is never read again.
        ImmutableArray<CombatAction?>? copy = orders is null ? null : [.. orders];
        var invalid = SessionValidation.CombatOrders(copy);
        if (invalid is not null)
        {
            return Outcome.Refuse<DeclarationResult>(s, invalid);
        }

        if (s.Game is null)
        {
            return Outcome.NoActiveGame<DeclarationResult>(s);
        }

        if (ActiveFight(s) is not { } combat)
        {
            return NoActiveCombat<DeclarationResult>(s);
        }

        var input = copy!.Value.Select(o => o!).ToList();
        var rejected = new List<(int Index, DeclarationEntry Entry)>();

        // Player-side checks first (designer rules 2026-10-24); then one order per fighter and the engine's checks.
        var eligible = new List<CombatAction>();
        var originalIndex = new List<int>();
        for (var i = 0; i < input.Count; i++)
        {
            var problem = CheckPlayerOrder(combat, input[i]);
            if (problem is not null)
            {
                rejected.Add((i, Reject(input[i], problem)));
                continue;
            }

            eligible.Add(input[i]);
            originalIndex.Add(i);
        }

        var batch = Declarations.ValidateAll(combat, eligible);
        var refusedHere = new HashSet<int>();
        foreach (var r in batch.Rejected)
        {
            refusedHere.Add(r.Index);
            rejected.Add((originalIndex[r.Index], Reject(eligible[r.Index], r.Reason)));
        }

        // An order refused but replaced by its fallback (GROUP to ATTACK) is reported as rejected too, with its reason, as today.
        for (var j = 0; j < eligible.Count; j++)
        {
            if (refusedHere.Contains(j))
            {
                continue;
            }

            var check = Declarations.Validate(combat, eligible[j]);
            if (!check.Valid && check.Fallback is not null)
            {
                rejected.Add((originalIndex[j], Reject(eligible[j], check.Error ?? string.Empty)));
            }
        }

        var rejectedInOrder = rejected.OrderBy(r => r.Index).ToList();
        if (input.Count > 0 && batch.Accepted.IsEmpty)
        {
            var details = rejectedInOrder
                .Select(r => new RuleErrorDetail($"actions/{r.Index.ToString(CultureInfo.InvariantCulture)}", r.Entry.Error ?? string.Empty))
                .ToImmutableArray();
            return Outcome.Refuse<DeclarationResult>(s, new RuleError(ErrorCode.COMBAT_INVALID_DECLARATION, "All declarations were invalid", details));
        }

        var accepted = batch.Accepted
            .Select(a => new DeclarationEntry(a.CombatantId, a.Type, a.TargetId, true, null))
            .ToImmutableArray();
        var result = new DeclarationResult(accepted, [.. rejectedInOrder.Select(r => r.Entry)]);
        return Outcome.Change(s with { PendingOrders = batch.Accepted }, result);
    }

    public static Outcome<CombatView> AdvanceRound(SessionContext context, SessionSnapshot s)
    {
        if (s.Game is null)
        {
            return Outcome.NoActiveGame<CombatView>(s);
        }

        if (ActiveFight(s) is not { } combat)
        {
            return NoActiveCombat<CombatView>(s);
        }

        var resolved = RoundManager.RunRound(combat, s.PendingOrders, context.Content.Archetypes);
        var game = FightSetup.Sync(s.Game, resolved, context.Now());
        var outcome = s.LastCombatOutcome;
        if (resolved.Status != CombatStatus.Active)
        {
            game = FightSetup.End(game, context.Now());
            outcome = new CombatOutcome(resolved.Status, resolved.Round);
        }

        return Outcome.Change(
            s with { Game = game, PendingOrders = ImmutableArray<CombatAction>.Empty, LastCombatOutcome = outcome },
            BuildView(resolved, ImmutableArray<CombatAction>.Empty));
    }

    public static Outcome<CombatView> GetCombatView(SessionContext context, SessionSnapshot s)
    {
        if (s.Game is null)
        {
            return Outcome.NoActiveGame<CombatView>(s);
        }

        return s.Game.Combat is null
            ? NoActiveCombat<CombatView>(s)
            : Outcome.Read(s, BuildView(s.Game.Combat, s.PendingOrders));
    }

    public static Outcome<ImmutableArray<RoundResult>> GetRoundHistory(SessionContext context, SessionSnapshot s)
    {
        if (s.Game is null)
        {
            return Outcome.NoActiveGame<ImmutableArray<RoundResult>>(s);
        }

        return s.Game.Combat is null
            ? NoActiveCombat<ImmutableArray<RoundResult>>(s)
            : Outcome.Read(s, s.Game.Combat.RoundHistory);
    }

    public static Outcome<CombatResultView> GetCombatResult(SessionContext context, SessionSnapshot s)
    {
        if (s.Game is null)
        {
            return Outcome.NoActiveGame<CombatResultView>(s);
        }

        if (s.Game.Combat is { } combat)
        {
            return Outcome.Read(s, new CombatResultView(CombatResultStatus.Active, combat.Round));
        }

        if (s.LastCombatOutcome is { } last)
        {
            var status = last.Status == CombatStatus.Victory ? CombatResultStatus.Victory : CombatResultStatus.Defeat;
            return Outcome.Read(s, new CombatResultView(status, last.Round));
        }

        return NoActiveCombat<CombatResultView>(s);
    }

    // A fight whose status is not Active never runs another round.
    private static CombatState? ActiveFight(SessionSnapshot s) =>
        s.Game?.Combat is { Status: CombatStatus.Active } combat ? combat : null;

    private static Outcome<T> NoActiveCombat<T>(SessionSnapshot s) =>
        Outcome.Refuse<T>(s, ErrorCode.NO_ACTIVE_COMBAT, "No active combat encounter");

    private static DeclarationEntry Reject(CombatAction order, string reason) =>
        new(order.CombatantId, order.Type, order.TargetId, false, reason);

    private static string? CheckPlayerOrder(CombatState combat, CombatAction action)
    {
        var member = combat.PlayerParty.FirstOrDefault(c => c.Id == action.CombatantId);
        if (member is null)
        {
            return $"Combatant '{action.CombatantId}' is not in the player's party";
        }

        if (action.EnergySegments is { } n)
        {
            if (action.Type != ActionType.Special)
            {
                return "energySegments is only allowed for SPECIAL";
            }

            if (!double.IsInteger(n) || n < 1 || n > member.Energy)
            {
                return $"energySegments must be an integer between 1 and {member.Energy.ToString("R", CultureInfo.InvariantCulture)}";
            }
        }

        return null;
    }

    private static CombatView BuildView(CombatState combat, ImmutableArray<CombatAction> pending)
    {
        var targets = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var order in pending)
        {
            targets[order.CombatantId] = order.TargetId;
        }

        var fighters = combat.PlayerParty.Concat(combat.EnemyParty)
            .Select(c => new FighterView(
                c.Id,
                c.Stamina,
                c.MaxStamina > 0 ? c.Stamina / c.MaxStamina : 0,
                c.IsKO ? FighterStance.KO : FighterStance.Active,
                targets.TryGetValue(c.Id, out var target) ? target : null))
            .ToImmutableArray();
        return new CombatView(combat.Round, combat.Phase, combat.Status, fighters, pending);
    }
}
