using System.Collections.Immutable;
using AncientOrder.Rules.Combat;
using AncientOrder.Rules.Common;

namespace AncientOrder.Rules.Session;

public sealed partial class GameSession
{
    /// <summary>Starts a fight with a fresh seed from the session's seed source; clears pending orders and the last outcome.</summary>
    public Result<StartEncounterResult> StartEncounter(string? encounterId) =>
        Run((c, s) => CombatOperations.StartEncounter(c, s, encounterId));

    /// <summary>Declares this round's player orders, replacing the pending ones. An empty list clears them.</summary>
    public Result<DeclarationResult> DeclareOrders(IReadOnlyList<CombatAction?>? orders) =>
        Run((c, s) => CombatOperations.DeclareOrders(c, s, orders));

    /// <summary>Plays one round with the pending orders. Ends the fight on victory or defeat.</summary>
    public Result<CombatView> AdvanceRound() => Run(CombatOperations.AdvanceRound);

    public Result<CombatView> GetCombatView() => Run(CombatOperations.GetCombatView);

    public Result<ImmutableArray<RoundResult>> GetRoundHistory() => Run(CombatOperations.GetRoundHistory);

    public Result<CombatResultView> GetCombatResult() => Run(CombatOperations.GetCombatResult);
}
