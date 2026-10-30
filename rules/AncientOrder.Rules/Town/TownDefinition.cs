using System.Collections.Immutable;
using AncientOrder.Rules.Conditions;

namespace AncientOrder.Rules.Town;

/// <summary>How a spot fires: Interact when the player uses it within reach, WalkOver when the player steps into its area.</summary>
public enum SpotTrigger
{
    Interact,
    WalkOver,
}

/// <summary>A fixed obstacle: nothing can be walked into its footprint. Height and Label are for display.</summary>
public sealed record ObstacleDefinition(string Id, Polygon Footprint, double Height, string? Label = null);

/// <summary>Where a person may stand and when. Facing is degrees (0 faces -Z, clockwise seen from above), display only.</summary>
public sealed record PersonPlacement(Point At, double? Facing = null)
{
    public ImmutableArray<Condition> When { get; init; } = [];
}

/// <summary>
/// One thing a person or spot can open: a conversation (ConversationId) or a fight hand-off (FightId), exactly one of the two.
/// It counts when its When holds.
/// </summary>
public sealed record OpensEntry(string? ConversationId = null, string? FightId = null)
{
    public ImmutableArray<Condition> When { get; init; } = [];

    public bool IsHandOff => FightId is not null;
}

/// <summary>A person in the town. They are present when some placement's When holds; the first such placement is where they stand. Opens is in content order.</summary>
public sealed record TownPerson(string Character, ImmutableArray<PersonPlacement> Placements)
{
    public ImmutableArray<OpensEntry> Opens { get; init; } = [];
}

/// <summary>A spot. Shown defaults to true for Interact and false for WalkOver (see IsShown). It is present while its When holds. Opens is in content order.</summary>
public sealed record SpotDefinition(string Id, Area Area, SpotTrigger Trigger, string? Name = null, bool? Shown = null)
{
    public ImmutableArray<Condition> When { get; init; } = [];

    public ImmutableArray<OpensEntry> Opens { get; init; } = [];

    public bool IsShown => Shown ?? Trigger == SpotTrigger.Interact;
}

/// <summary>
/// A town. Ground is the union of its walkable polygons; Obstacles are cut out of it. Start is where a new game stands.
/// Reach overrides the settings' reach for this town. People and Spots are in content order.
/// </summary>
public sealed record TownDefinition(
    string Id,
    string Name,
    ImmutableArray<Polygon> Ground,
    ImmutableArray<ObstacleDefinition> Obstacles,
    Point Start,
    ImmutableArray<TownPerson> People,
    ImmutableArray<SpotDefinition> Spots,
    double? Reach = null);
