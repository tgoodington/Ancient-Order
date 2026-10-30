using System.Collections.Immutable;
using AncientOrder.Rules.Combat;
using AncientOrder.Rules.Conversations;
using AncientOrder.Rules.HandOff;
using AncientOrder.Rules.Party;
using AncientOrder.Rules.State;
using AncientOrder.Rules.Town;

namespace AncientOrder.Rules.Session;

/// <summary>How the last finished fight ended: Victory or Defeat, and the round it ended on.</summary>
public sealed record CombatOutcome(CombatStatus Status, int Round);

/// <summary>
/// Everything a screen reads from the session. Version starts at 0 and goes up by one per committed change.
/// Synergy is for the player plus the current lineup and is empty without a game.
/// Party is the party screen's view, ready made (null without a game).
/// Conversation is the current step ready to show (null when none runs). LastChoice is what the choice that produced this
/// snapshot did; it is null after every other change.
/// Town is the town the player is in with who and what is present, derived from state on every commit (null without a town).
/// HandOff is the fight the player left for, with its name and the lineup names, derived on commit (null until the hand-off is recorded).
/// </summary>
public sealed record SessionSnapshot(
    long Version,
    GameState? Game,
    ImmutableArray<CombatAction> PendingOrders,
    CombatOutcome? LastCombatOutcome,
    ImmutableArray<SynergyResult> Synergy,
    ConversationView? Conversation = null,
    ChoiceReport? LastChoice = null,
    PartyView? Party = null,
    TownView? Town = null,
    HandOffView? HandOff = null)
{
    public static SessionSnapshot Initial { get; } = new(
        0,
        null,
        ImmutableArray<CombatAction>.Empty,
        null,
        ImmutableArray<SynergyResult>.Empty);
}
