using System.Collections.Immutable;
using AncientOrder.Rules.Party;
using AncientOrder.Rules.Session;
using Godot;

namespace AncientOrder.Ui;

/// <summary>
/// What PartyPresenter needs from the party screen (T19): the screen's root script implements this.
/// Render draws the view in full (every call replaces what was shown). LineupToggled(characterId, on) is raised when the player flips a member's lineup toggle.
/// </summary>
public interface IPartyScreen
{
    void Render(PartyView view);

    event Action<string, bool>? LineupToggled;
}

/// <summary>
/// Renders snapshot.Party into the party screen while it is open and sends lineup picks back through the session.
/// A toggle on appends the member to the current lineup and a toggle off removes them; a refusal goes to the error banner and the screen is drawn again from the unchanged snapshot.
/// Inert when the screen does not implement IPartyScreen (the flow's placeholder).
/// </summary>
public sealed class PartyPresenter
{
    private readonly ScreenFlow _flow;
    private readonly IPartyScreen? _screen;

    public PartyPresenter(ScreenFlow flow, IPartyScreen? screen)
    {
        _flow = flow;
        _screen = screen;
        if (screen is null)
        {
            return;
        }

        flow.SnapshotRendered += OnSnapshot;
        flow.PartyOpened += Refresh;
        screen.LineupToggled += OnToggled;
    }

    private void OnSnapshot(SessionSnapshot snapshot)
    {
        if (_flow.IsPartyOpen)
        {
            Draw(snapshot);
        }
    }

    private void Refresh()
    {
        if (_flow.Session is { } session)
        {
            Draw(session.Snapshot);
        }
    }

    private void Draw(SessionSnapshot snapshot)
    {
        if (snapshot.Party is { } view)
        {
            _screen!.Render(view);
        }
    }

    private void OnToggled(string characterId, bool on)
    {
        if (_flow.Session is not { } session)
        {
            return;
        }

        var party = session.Snapshot.Party;
        var current = party?.Lineup ?? ImmutableArray<LineupMemberView>.Empty;
        if (on && party is not null && current.Length >= party.Limits.Max && !current.Any(m => string.Equals(m.Id, characterId, StringComparison.Ordinal)))
        {
            _flow.ShowMessage("Lineup full");
            Refresh();
            return;
        }

        var ids = current.Select(m => m.Id).Where(id => !string.Equals(id, characterId, StringComparison.Ordinal)).ToList();
        if (on)
        {
            ids.Add(characterId);
        }

        var result = session.SetLineup(ids.Select(id => (string?)id).ToImmutableArray());
        if (!result.IsOk)
        {
            _flow.ShowError(result.Error);
        }

        Refresh();
    }
}
