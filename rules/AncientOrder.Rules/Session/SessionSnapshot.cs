using System.Collections.Immutable;
using AncientOrder.Rules.Combat;
using AncientOrder.Rules.Party;
using AncientOrder.Rules.State;

namespace AncientOrder.Rules.Session;

/// <summary>How the last finished fight ended: Victory or Defeat, and the round it ended on.</summary>
public sealed record CombatOutcome(CombatStatus Status, int Round);

/// <summary>
/// Everything a screen reads from the session. Version starts at 0 and goes up by one per committed change.
/// Synergy is for the player plus the current lineup and is empty without a game.
/// </summary>
public sealed record SessionSnapshot(
    long Version,
    GameState? Game,
    ImmutableArray<CombatAction> PendingOrders,
    CombatOutcome? LastCombatOutcome,
    ImmutableArray<SynergyResult> Synergy)
{
    public static SessionSnapshot Initial { get; } = new(
        0,
        null,
        ImmutableArray<CombatAction>.Empty,
        null,
        ImmutableArray<SynergyResult>.Empty);
}
