using System.Collections.Immutable;
using AncientOrder.Rules.Personality;

namespace AncientOrder.Rules.Conditions;

/// <summary>
/// An explained condition list: a tree that mirrors the conditions, with Met on every node and what the check looked at.
/// The root of an explained list is an All node. For the dev tools and the panel; players are shown an in-world line, never this (ADR-075).
/// </summary>
public abstract record RequirementView
{
    private protected RequirementView(bool met) => Met = met;

    public bool Met { get; }

    public sealed record Flag(string FlagId, bool Met) : RequirementView(Met);

    public sealed record Chose(string ConversationId, string ChoiceId, bool Met) : RequirementView(Met);

    public sealed record Seen(string ConversationId, bool Met) : RequirementView(Met);

    /// <summary>Current is the player's value of the trait now.</summary>
    public sealed record TraitRange(Trait Trait, double? AtLeast, double? AtMost, double Current, bool Met) : RequirementView(Met);

    /// <summary>CharacterName is the character's name (the id when the character is unknown). Current is the measure's value now.</summary>
    public sealed record RelationshipRange(
        string CharacterId, string CharacterName, RelationshipMeasure Measure, double? AtLeast, double? AtMost, double Current, bool Met) : RequirementView(Met);

    public sealed record InParty(string CharacterId, string CharacterName, bool Met) : RequirementView(Met);

    /// <summary>Reason is the plain reason the lineup is not ready, or null when it is.</summary>
    public sealed record LineupReady(string? FightId, string? Reason, bool Met) : RequirementView(Met);

    public sealed record All(ImmutableArray<RequirementView> Children, bool Met) : RequirementView(Met);

    public sealed record Any(ImmutableArray<RequirementView> Children, bool Met) : RequirementView(Met);

    public sealed record Negation(RequirementView Child, bool Met) : RequirementView(Met);
}
