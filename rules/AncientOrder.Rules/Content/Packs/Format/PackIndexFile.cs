using System.Collections.Immutable;

namespace AncientOrder.Rules.Content.Packs.Format;

/// <summary>pack.json: names the pack and lists its content files in load order, as paths relative to the pack folder.</summary>
public sealed record PackIndexFile(
    string Format,
    int Version,
    string Id,
    string Name,
    bool StandIn,
    ImmutableArray<string> Files);
