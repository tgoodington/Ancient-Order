using System.Collections.Immutable;

namespace AncientOrder.Rules.Content.Packs.Format;

/// <summary>
/// A town. Coordinates are meters on the ground plane as [x, z] pairs; a polygon is a list of 3 or more pairs.
/// Ground is the walkable polygons (their union). When is a list of conditions that must all hold.
/// </summary>
public sealed record TownFormat(
    string Id,
    string Name,
    ImmutableArray<ImmutableArray<ImmutableArray<double>>> Ground,
    ImmutableArray<double> Start,
    ImmutableArray<ObstacleFormat> Obstacles = default,
    double? Reach = null,
    ImmutableArray<PersonFormat> People = default,
    ImmutableArray<SpotFormat> Spots = default);

public sealed record ObstacleFormat(string Id, ImmutableArray<ImmutableArray<double>> Footprint, double Height, string? Label = null);

/// <summary>Opens lists what the person opens, first entry whose When holds wins.</summary>
public sealed record PersonFormat(string Character, ImmutableArray<PlacementFormat> Placements, ImmutableArray<OpensFormat> Opens = default);

/// <summary>Exactly one of Conversation (a conversation id) and HandOff (a fight id) must be set; the pack check refuses anything else.</summary>
public sealed record OpensFormat(string? Conversation = null, string? HandOff = null, ImmutableArray<ConditionFormat> When = default);

/// <summary>Facing is degrees, 0 faces -Z, clockwise seen from above.</summary>
public sealed record PlacementFormat(ImmutableArray<double> At, double? Facing = null, ImmutableArray<ConditionFormat> When = default);

/// <summary>Trigger is "interact" or "walkOver". Shown defaults to true for interact and false for walkOver.</summary>
public sealed record SpotFormat(
    string Id,
    AreaFormat Area,
    Rules.Town.SpotTrigger Trigger,
    string? Name = null,
    bool? Shown = null,
    ImmutableArray<ConditionFormat> When = default,
    ImmutableArray<OpensFormat> Opens = default);

/// <summary>Exactly one of Circle and Polygon must be set; the pack check refuses anything else.</summary>
public sealed record AreaFormat(CircleFormat? Circle = null, ImmutableArray<ImmutableArray<double>> Polygon = default);

public sealed record CircleFormat(ImmutableArray<double> Center, double Radius);
