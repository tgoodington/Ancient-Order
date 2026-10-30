using System.Collections.Immutable;
using AncientOrder.Rules.Conditions;
using AncientOrder.Rules.Effects;
using AncientOrder.Rules.Reactions;

namespace AncientOrder.Rules.Conversations;

/// <summary>Who speaks a line: the narrator, the player, or a character from the catalog. Content writes them as "narrator", "player", or the character's id.</summary>
public abstract record Speaker
{
    public const string NarratorId = "narrator";
    public const string PlayerId = "player";

    private protected Speaker()
    {
    }

    public sealed record Narrator : Speaker;

    public sealed record Player : Speaker;

    public sealed record Character(string Id) : Speaker;

    public static bool IsReserved(string id) => id is NarratorId or PlayerId;

    public static Speaker FromId(string id) => id switch
    {
        NarratorId => new Narrator(),
        PlayerId => new Player(),
        _ => new Character(id),
    };
}

/// <summary>Where a choice or a step leads: another step of the same conversation, or the opening of another conversation (which ends this one). When makes it conditional.</summary>
public abstract record Destination
{
    private protected Destination()
    {
    }

    /// <summary>All must hold for this destination to be taken; empty always holds.</summary>
    public ImmutableArray<Condition> When { get; init; } = [];

    public sealed record ToStep(string StepId) : Destination;

    public sealed record ToConversation(string ConversationId) : Destination;
}

/// <summary>One possible opening step. The first whose When holds opens the conversation.</summary>
public sealed record StartEntry(string StepId)
{
    public ImmutableArray<Condition> When { get; init; } = [];
}

/// <summary>A line. It is shown only while its When holds.</summary>
public sealed record LineDefinition(Speaker Speaker, string Text)
{
    public ImmutableArray<Condition> When { get; init; } = [];
}

/// <summary>
/// A choice. It is hidden unless its When holds, and shown locked (with LockedText) unless its Requires holds.
/// Next is tried in order and the first destination whose condition holds is taken; none ends the conversation.
/// </summary>
public sealed record ChoiceDefinition(
    string Id,
    string Text,
    string? LockedText,
    ImmutableArray<Effect> Effects,
    ImmutableArray<Destination> Next)
{
    public ImmutableArray<Condition> When { get; init; } = [];

    public ImmutableArray<Condition> Requires { get; init; } = [];

    /// <summary>Overrides of participant reactions to this choice; at most one per participant.</summary>
    public ImmutableArray<ReactionOverride> Reactions { get; init; } = [];
}

/// <summary>A step: lines to show, choices to pick from, and where Continue goes when there are no (visible) choices.</summary>
public sealed record StepDefinition(
    string Id,
    ImmutableArray<LineDefinition> Lines,
    ImmutableArray<ChoiceDefinition> Choices,
    ImmutableArray<Destination> Next);

public sealed record ConversationDefinition(
    string Id,
    string? Title,
    ImmutableArray<string> Participants,
    ImmutableArray<StartEntry> Start,
    ImmutableArray<StepDefinition> Steps);

/// <summary>Where the player is: set in the game state while a conversation runs.</summary>
public sealed record ActiveConversation(string ConversationId, string StepId);
