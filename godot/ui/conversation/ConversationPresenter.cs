using System.Collections.Immutable;
using AncientOrder.Rules.Common;
using AncientOrder.Rules.Conversations;
using AncientOrder.Rules.Session;
using Godot;

namespace AncientOrder.Ui;

/// <summary>
/// Binds the conversation panel (T17) to the session. It reads every rendered snapshot: a running conversation opens the panel at its current step
/// (a conversation started in the town, or a mid-conversation save that was loaded), and LastChoice becomes an outcome handed to the panel before the new step.
/// Reactions also make the person lean in or pull back in the town. Personality shifts and relationship numbers are never shown here (ADR-075).
/// The only thing it remembers is the step view last shown, to find the text of the choice taken.
/// </summary>
public sealed class ConversationPresenter
{
    private readonly ScreenFlow _flow;
    private readonly IConversationPanel _panel;
    private ConversationView? _view;

    public ConversationPresenter(ScreenFlow flow, IConversationPanel panel)
    {
        _flow = flow;
        _panel = panel;
        _panel.ChoiceTaken += OnChoice;
        _panel.ContinueTaken += OnContinue;
        _panel.CloseTaken += Close;
        _flow.SnapshotRendered += Handle;
        _flow.ConversationHoldDropped += Resync;
        Resync();
    }

    private void Resync()
    {
        if (_flow.Session is { } session)
        {
            Handle(session.Snapshot with { LastChoice = null });
        }
    }

    /// <summary>The panel is waiting on the player's Close after a conversation-ending choice.</summary>
    public bool IsClosing => _flow.ConversationHold;

    private void Handle(SessionSnapshot snapshot)
    {
        ConversationOutcome? outcome = null;
        if (snapshot.LastChoice is { } report)
        {
            outcome = BuildOutcome(snapshot, report);
            PlayTells(snapshot, report);
        }

        if (snapshot.Conversation is { } view)
        {
            _flow.ConversationHold = false;
            if (outcome is { IsEmpty: false })
            {
                _panel.ShowOutcome(outcome, false);
            }

            _view = view;
            _panel.ShowStep(view);
            return;
        }

        _view = null;
        if (outcome is { IsEmpty: false })
        {
            _panel.ShowOutcome(outcome, true);
            _flow.ConversationHold = true;
        }
        else if (!_flow.ConversationHold)
        {
            _panel.Close();
        }
    }

    private ConversationOutcome BuildOutcome(SessionSnapshot snapshot, ChoiceReport report)
    {
        string? playerLine = null;
        if (report.ChoiceId is { } choiceId && _view?.Choices.FirstOrDefault(c => c.Id == choiceId) is { } choice)
        {
            playerLine = choice.Text;
        }

        var body = report.Reactions.Where(r => !string.IsNullOrWhiteSpace(r.Text)).Select(r => r.Text).ToImmutableArray();
        var notices = ImmutableArray.CreateBuilder<string>();
        foreach (var id in report.Joined)
        {
            notices.Add($"{NameOf(id)} joins the party.");
        }

        foreach (var id in report.Left)
        {
            notices.Add($"{NameOf(id)} leaves the party.");
        }

        return new ConversationOutcome(snapshot.Game?.Player.Name, playerLine, body, notices.ToImmutable());
    }

    private string NameOf(string characterId) =>
        _flow.Content is { } content && content.Characters.TryGet(characterId, out var definition) ? definition.Name : characterId;

    private void PlayTells(SessionSnapshot snapshot, ChoiceReport report)
    {
        if (snapshot.Town is not { } town)
        {
            return;
        }

        foreach (var reaction in report.Reactions)
        {
            if (town.People.Any(p => p.CharacterId == reaction.CharacterId))
            {
                _flow.Stage.PlayTell(reaction.CharacterId, reaction.Direction, reaction.Strength);
            }
        }
    }

    private void OnChoice(string choiceId)
    {
        if (_flow.ConversationHold || _flow.Session is not { } session)
        {
            return;
        }

        var result = session.Choose(choiceId);
        if (!result.IsOk)
        {
            _flow.ShowError(result.Error);
        }
    }

    private void OnContinue()
    {
        if (_flow.ConversationHold || _flow.Session is not { } session)
        {
            return;
        }

        var result = session.Continue();
        if (!result.IsOk)
        {
            _flow.ShowError(result.Error);
        }
    }

    private void Close()
    {
        if (!_flow.ConversationHold)
        {
            return;
        }

        _panel.Close();
        _flow.ConversationHold = false;

        if (_flow.IsAtHandOff)
        {
            FirstButton(_flow.HandOffScreen)?.GrabFocus();
        }
    }

    private static Button? FirstButton(Node? node)
    {
        if (node is null)
        {
            return null;
        }

        foreach (var child in node.GetChildren())
        {
            if (child is Button { Disabled: false, Visible: true } button)
            {
                return button;
            }

            if (FirstButton(child) is { } found)
            {
                return found;
            }
        }

        return null;
    }
}
