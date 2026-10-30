using System.Collections.Immutable;
using AncientOrder.Rules.Combat;
using AncientOrder.Rules.Conversations;
using AncientOrder.Rules.HandOff;
using AncientOrder.Rules.Story;
using AncientOrder.Rules.Town;

namespace AncientOrder.Rules.State;

public sealed record PlayerCharacter(string Id, string Name, AncientOrder.Rules.Personality.Personality Personality);

/// <summary>
/// One playthrough. Party is who has joined, in join order (never sorted; the player is not in it); Lineup is picked only from it. Characters and ReactionProgress are lookup only, never iterated: walk the catalog and look up by id.
/// Timestamp is Unix ms. Conversation is where the player is in a running conversation, else null.
/// </summary>
public sealed record GameState(
    PlayerCharacter Player,
    ImmutableDictionary<string, CharacterState> Characters,
    ImmutableArray<string> Lineup,
    CombatState? Combat,
    ImmutableDictionary<string, ReactionProgress> ReactionProgress,
    long Timestamp,
    ActiveConversation? Conversation = null)
{
    public ImmutableArray<string> Party { get; init; } = [];

    /// <summary>Flags set, choices taken, and conversations seen so far. Lookup only.</summary>
    public StoryMemory Memory { get; init; } = StoryMemory.Empty;

    /// <summary>Where the player stands: the town and a point on its ground. Null when there is no town. Who and what is present is worked out from state and never stored.</summary>
    public TownPresence? Town { get; init; }

    /// <summary>Set once the player has left the town for a fight: the fight, the lineup, and the town. The town is complete from then on. Null otherwise.</summary>
    public FightHandOff? HandOff { get; init; }
}
