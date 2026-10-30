using System.Collections.Immutable;

namespace AncientOrder.Rules.Content.Packs.Format;

/// <summary>A conversation. A file may hold many. When, requires, and conditions are lists that must all hold (T5). Later tasks add reactions to the parts below.</summary>
public sealed record ConversationFormat(
    string Id,
    ImmutableArray<string> Participants,
    ImmutableArray<StepFormat> Steps,
    string? Title = null,
    ImmutableArray<StartFormat> Start = default);

public sealed record StartFormat(string Step, ImmutableArray<ConditionFormat> When = default);

public sealed record StepFormat(
    string Id,
    ImmutableArray<LineFormat> Lines,
    ImmutableArray<ChoiceFormat> Choices = default,
    ImmutableArray<DestinationFormat> Next = default);

/// <summary>Speaker is "narrator", "player", or a character id. The line is shown only while every condition in When holds.</summary>
public sealed record LineFormat(string Speaker, string Text, ImmutableArray<ConditionFormat> When = default);

public sealed record ChoiceFormat(
    string Id,
    string Text,
    string? LockedText = null,
    ImmutableArray<EffectFormat> Effects = default,
    ImmutableArray<DestinationFormat> Next = default,
    ImmutableArray<ConditionFormat> When = default,
    ImmutableArray<ConditionFormat> Requires = default,
    ImmutableArray<ReactionOverrideFormat> Reactions = default);

/// <summary>Either silence (true) or text with optional affection and trust, for one participant.</summary>
public sealed record ReactionOverrideFormat(string Character, bool? Silence = null, string? Text = null, double? Affection = null, double? Trust = null);

/// <summary>Exactly one of Step and Conversation must be set; the pack check refuses anything else.</summary>
public sealed record DestinationFormat(string? Step = null, string? Conversation = null, ImmutableArray<ConditionFormat> When = default);
