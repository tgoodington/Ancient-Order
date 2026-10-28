using System.Collections.Immutable;
using AncientOrder.Rules.Combat;

namespace AncientOrder.Rules.State;

public sealed record PlayerCharacter(string Id, string Name, AncientOrder.Rules.Personality.Personality Personality);

/// <summary>
/// One playthrough. Characters and ReactionProgress are lookup only, never iterated: walk the catalog and look up by id.
/// Timestamp is Unix ms.
/// </summary>
public sealed record GameState(
    PlayerCharacter Player,
    ImmutableDictionary<string, CharacterState> Characters,
    ImmutableArray<string> Lineup,
    CombatState? Combat,
    ImmutableDictionary<string, ReactionProgress> ReactionProgress,
    long Timestamp);
