using System.Collections.Immutable;
using System.Text.Json.Serialization;
using AncientOrder.Rules.Conditions;
using AncientOrder.Rules.Personality;

namespace AncientOrder.Rules.Content.Packs.Format;

/// <summary>
/// A condition, told apart by its "type". Thresholds are inclusive, and a trait or relationship condition needs at least one of
/// atLeast and atMost. Every when and requires field in a content file is a list of these, all of which must hold.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(FlagConditionFormat), "flag")]
[JsonDerivedType(typeof(ChoseConditionFormat), "chose")]
[JsonDerivedType(typeof(SeenConditionFormat), "seen")]
[JsonDerivedType(typeof(TraitConditionFormat), "trait")]
[JsonDerivedType(typeof(RelationshipConditionFormat), "relationship")]
[JsonDerivedType(typeof(InPartyConditionFormat), "inParty")]
[JsonDerivedType(typeof(LineupReadyConditionFormat), "lineupReady")]
[JsonDerivedType(typeof(AllConditionFormat), "all")]
[JsonDerivedType(typeof(AnyConditionFormat), "any")]
[JsonDerivedType(typeof(NotConditionFormat), "not")]
public abstract record ConditionFormat;

public sealed record FlagConditionFormat(string Flag) : ConditionFormat;

public sealed record ChoseConditionFormat(string Conversation, string Choice) : ConditionFormat;

public sealed record SeenConditionFormat(string Conversation) : ConditionFormat;

public sealed record TraitConditionFormat(Trait Trait, double? AtLeast = null, double? AtMost = null) : ConditionFormat;

public sealed record RelationshipConditionFormat(string Character, RelationshipMeasure Measure, double? AtLeast = null, double? AtMost = null) : ConditionFormat;

public sealed record InPartyConditionFormat(string Character) : ConditionFormat;

public sealed record LineupReadyConditionFormat(string? Fight = null) : ConditionFormat;

public sealed record AllConditionFormat(ImmutableArray<ConditionFormat> Conditions) : ConditionFormat;

public sealed record AnyConditionFormat(ImmutableArray<ConditionFormat> Conditions) : ConditionFormat;

public sealed record NotConditionFormat(ConditionFormat Condition) : ConditionFormat;
