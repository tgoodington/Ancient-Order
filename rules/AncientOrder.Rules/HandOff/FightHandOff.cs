using System.Collections.Immutable;

namespace AncientOrder.Rules.HandOff;

/// <summary>
/// The recorded hand-off from the town to a fight: which fight (encounter) is next, who is in the lineup (a copy, in lineup order),
/// and the town that is complete. TownId is null when the hand-off happened with no town. Sprint 8 starts the fight from this.
/// </summary>
public sealed record FightHandOff(string FightId, ImmutableArray<string> Lineup, string? TownId);
