using System.Collections.Immutable;
using AncientOrder.Rules.Conditions;
using AncientOrder.Rules.Reactions;
using AncientOrder.Rules.State;

namespace AncientOrder.Rules.Conversations;

public enum SpeakerKind
{
    Narrator,
    Player,
    Character,
}

public sealed record ParticipantView(string Id, string Name, Demeanor Demeanor);

/// <summary>A line as shown. The narrator has no id and no name; the player shows the player's name.</summary>
public sealed record LineView(SpeakerKind SpeakerKind, string? SpeakerId, string? SpeakerName, string Text);

/// <summary>
/// A choice as shown. LockedText is the in-world line shown when it is not Available. Requirement is the explained requirement of
/// a locked choice, for the dev tools only (ADR-075); it is null when the choice is available.
/// </summary>
public sealed record ChoiceView(string Id, string Text, bool Available, RequirementView? Requirement, string? LockedText);

/// <summary>The current step, ready to show. A step with nothing to pick has no Choices and IsContinue set, so the screen offers a single Continue.</summary>
public sealed record ConversationView(
    string Id,
    string? Title,
    ImmutableArray<ParticipantView> Participants,
    string StepId,
    ImmutableArray<LineView> Lines,
    ImmutableArray<ChoiceView> Choices,
    bool IsContinue)
{
    /// <summary>Derives the view of the running conversation, or null when none runs or the content no longer has it.</summary>
    public static ConversationView? From(GameContent content, GameState game)
    {
        if (game.Conversation is not { } active
            || !content.Conversations.TryGet(active.ConversationId, out var conversation)
            || conversation.Steps.FirstOrDefault(s => s.Id == active.StepId) is not { } step)
        {
            return null;
        }

        var participants = ImmutableArray.CreateBuilder<ParticipantView>();
        if (!conversation.Participants.IsDefault)
        {
            foreach (var id in conversation.Participants)
            {
                if (content.Characters.TryGet(id, out var character))
                {
                    participants.Add(new ParticipantView(id, character.Name, game.Characters.TryGetValue(id, out var state) ? DemeanorRules.Of(content.Settings, state) : Demeanor.Neutral));
                }
            }
        }

        var lines = ImmutableArray.CreateBuilder<LineView>();
        if (!step.Lines.IsDefault)
        {
            foreach (var line in step.Lines)
            {
                if (ConversationGates.LineShows(content, game, line))
                {
                    lines.Add(ToView(content, game, line));
                }
            }
        }

        var choices = ImmutableArray.CreateBuilder<ChoiceView>();
        if (!step.Choices.IsDefault)
        {
            foreach (var choice in step.Choices)
            {
                if (ConversationGates.ChoiceShows(content, game, choice))
                {
                    var available = ConversationGates.ChoiceAvailable(content, game, choice);
                    choices.Add(new ChoiceView(
                        choice.Id,
                        choice.Text,
                        available,
                        available ? null : ConversationGates.ExplainRequirements(content, game, choice),
                        available ? choice.LockedText : choice.LockedText ?? content.Settings.Conversations.LockedText));
                }
            }
        }

        return new ConversationView(conversation.Id, conversation.Title, participants.ToImmutable(), step.Id, lines.ToImmutable(), choices.ToImmutable(), choices.Count == 0);
    }

    private static LineView ToView(GameContent content, GameState game, LineDefinition line) =>
        line.Speaker switch
        {
            Speaker.Player => new LineView(SpeakerKind.Player, game.Player.Id, game.Player.Name, line.Text),
            Speaker.Character c => new LineView(SpeakerKind.Character, c.Id, content.Characters.TryGet(c.Id, out var d) ? d.Name : c.Id, line.Text),
            _ => new LineView(SpeakerKind.Narrator, null, null, line.Text),
        };
}
