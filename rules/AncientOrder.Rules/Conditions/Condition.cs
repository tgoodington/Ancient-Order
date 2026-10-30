using System.Collections.Immutable;
using AncientOrder.Rules.Personality;

namespace AncientOrder.Rules.Conditions;

/// <summary>Which of a character's two feelings toward the player a relationship condition reads.</summary>
public enum RelationshipMeasure
{
    Affection,
    Trust,
}

/// <summary>
/// A test over the game so far. Thresholds are inclusive; a trait or relationship condition needs at least one of AtLeast and AtMost.
/// Every when and requires field holds a list of conditions that must all hold; an empty or missing list always holds.
/// </summary>
public abstract record Condition
{
    private protected Condition()
    {
    }

    /// <summary>The flag is set.</summary>
    public sealed record Flag(string FlagId) : Condition;

    /// <summary>The player has taken this choice of this conversation.</summary>
    public sealed record Chose(string ConversationId, string ChoiceId) : Condition;

    /// <summary>The conversation has started at least once.</summary>
    public sealed record Seen(string ConversationId) : Condition;

    /// <summary>The player's trait value is within the range.</summary>
    public sealed record TraitRange(Trait Trait, double? AtLeast, double? AtMost) : Condition;

    /// <summary>The character's affection or trust toward the player is within the range.</summary>
    public sealed record RelationshipRange(string CharacterId, RelationshipMeasure Measure, double? AtLeast, double? AtMost) : Condition;

    /// <summary>The character is in the party.</summary>
    public sealed record InParty(string CharacterId) : Condition;

    /// <summary>The lineup can go into a fight. FightId, when given, names the fight whose lineup limits apply.</summary>
    public sealed record LineupReady(string? FightId) : Condition;

    /// <summary>Every condition holds.</summary>
    public sealed record All(ImmutableArray<Condition> Conditions) : Condition;

    /// <summary>At least one condition holds.</summary>
    public sealed record Any(ImmutableArray<Condition> Conditions) : Condition;

    /// <summary>The condition does not hold.</summary>
    public sealed record Negation(Condition Inner) : Condition;
}
