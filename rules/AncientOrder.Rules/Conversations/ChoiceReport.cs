using System.Collections.Immutable;
using AncientOrder.Rules.Common;
using AncientOrder.Rules.Personality;
using AncientOrder.Rules.Reactions;

namespace AncientOrder.Rules.Conversations;

/// <summary>A trait before and after a choice. Change is After minus Before.</summary>
public sealed record TraitChange(Trait Trait, double Before, double After)
{
    public double Change => JsMath.Round((After - Before) * 100) / 100;
}

/// <summary>What a choice wrote for a trait: the sum of its shift effects' amounts for that trait, before any limits.</summary>
public sealed record TraitShift(Trait Trait, double Amount);

/// <summary>
/// How one character reacted to a choice. Intended values are what the reaction asked for; the changes are what was applied after limits.
/// Text is the body-language line, with the character's name filled in.
/// </summary>
public sealed record ReactionReport(
    string CharacterId,
    string Name,
    ReactionKind Kind,
    TellDirection Direction,
    TellStrength Strength,
    string Text,
    double IntendedAffection,
    double IntendedTrust,
    double AffectionChange,
    double TrustChange);

/// <summary>The affection and trust a character actually gained or lost (after limits) from a choice.</summary>
public sealed record RelationshipChange(string CharacterId, double Affection, double Trust);

/// <summary>
/// What a choice (or Continue, which has no ChoiceId) did. Ended says the conversation it was in is over; NextConversationId
/// names the conversation it led straight into, if any. The lists are empty until the effect or mechanic that fills them exists
/// (T3 TraitChanges and WrittenShift, T4 Reactions, T5 FlagsSet and FlagsCleared, T6 Joined and Left, in the order they happened; a join or leave that changed nothing is not listed).
/// </summary>
public sealed record ChoiceReport(string ConversationId, string? ChoiceId, bool Ended, string? NextConversationId)
{
    /// <summary>The personality change the choice actually caused, in trait order, only traits whose value changed.</summary>
    public ImmutableArray<TraitChange> TraitChanges { get; init; } = [];

    /// <summary>The choice's written shift totals per trait, in trait order, only traits it wrote. T4 reactions follow these, not TraitChanges.</summary>
    public ImmutableArray<TraitShift> WrittenShift { get; init; } = [];

    public ImmutableArray<ReactionReport> Reactions { get; init; } = [];

    public ImmutableArray<RelationshipChange> RelationshipChanges { get; init; } = [];

    public ImmutableArray<string> Joined { get; init; } = [];

    public ImmutableArray<string> Left { get; init; } = [];

    public ImmutableArray<string> FlagsSet { get; init; } = [];

    public ImmutableArray<string> FlagsCleared { get; init; } = [];

    /// <summary>The fight (encounter id) the choice handed off to, or null. The conversation has ended and the town is complete.</summary>
    public string? HandOff { get; init; }
}
