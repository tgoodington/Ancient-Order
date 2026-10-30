using System.Text.Json.Serialization;

namespace AncientOrder.Rules.Content.Packs.Format;

/// <summary>An effect, told apart by its "type". Later tasks add more types.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(RelationshipEffectFormat), "relationship")]
[JsonDerivedType(typeof(JoinEffectFormat), "join")]
[JsonDerivedType(typeof(LeaveEffectFormat), "leave")]
[JsonDerivedType(typeof(ShiftEffectFormat), "shift")]
[JsonDerivedType(typeof(SetFlagEffectFormat), "setFlag")]
[JsonDerivedType(typeof(ClearFlagEffectFormat), "clearFlag")]
[JsonDerivedType(typeof(HandOffEffectFormat), "handOff")]
public abstract record EffectFormat;

public sealed record RelationshipEffectFormat(string Character, double? Affection = null, double? Trust = null) : EffectFormat;

public sealed record JoinEffectFormat(string Character) : EffectFormat;

public sealed record LeaveEffectFormat(string Character) : EffectFormat;

public sealed record ShiftEffectFormat(
    double? Patience = null,
    double? Empathy = null,
    double? Cunning = null,
    double? Logic = null,
    double? Kindness = null,
    double? Charisma = null) : EffectFormat;

public sealed record SetFlagEffectFormat(string Flag) : EffectFormat;

public sealed record ClearFlagEffectFormat(string Flag) : EffectFormat;

public sealed record HandOffEffectFormat(string Fight) : EffectFormat;
