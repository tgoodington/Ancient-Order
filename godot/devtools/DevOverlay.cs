using System.Text;
using AncientOrder.Rules.Conversations;
using AncientOrder.Rules.Personality;
using AncientOrder.Rules.Session;
using AncientOrder.Ui;
using Godot;

namespace AncientOrder.DevTools;

/// <summary>
/// F3 overlay (action dev_overlay) with the numbers players never see: exact traits, what the last choice changed, the people present, the open conversation's choices
/// with their requirements, story memory, lineup readiness and synergy. It redraws once per snapshot (and when opened), only reads, and ignores the mouse except for its own scroll.
/// </summary>
public partial class DevOverlay : PanelContainer
{
    private const int FontSize = 12;

    private ScreenFlow _flow = null!;
    private RichTextLabel _text = null!;
    private SessionSnapshot? _snapshot;
    private ChoiceReport? _lastChoice;

    public string PlainText => _text.GetParsedText();

    public override void _Ready()
    {
        _text = GetNode<RichTextLabel>("Scroll/Text");
        var mono = new SystemFont { FontNames = ["Consolas", "Courier New", "monospace"] };
        _text.AddThemeFontOverride("normal_font", mono);
        _text.AddThemeFontOverride("bold_font", mono);
        _text.AddThemeFontSizeOverride("normal_font_size", FontSize);
        _text.AddThemeFontSizeOverride("bold_font_size", FontSize);
    }

    public void Bind(ScreenFlow flow)
    {
        _flow = flow;
        flow.SnapshotRendered += snapshot =>
        {
            _snapshot = snapshot;
            _lastChoice = snapshot.LastChoice ?? _lastChoice;
            if (Visible)
            {
                Redraw();
            }
        };
    }

    public override void _Input(InputEvent @event)
    {
        if (!@event.IsActionPressed("dev_overlay") || @event.IsEcho())
        {
            return;
        }

        Toggle();
        GetViewport().SetInputAsHandled();
    }

    public void Toggle()
    {
        Visible = !Visible;
        if (Visible)
        {
            _snapshot ??= _flow.Session?.Snapshot;
            Redraw();
        }
    }

    private void Redraw()
    {
        var text = new StringBuilder();
        Pack(text);
        if (_snapshot?.Game is { } game)
        {
            Player(text, game);
            LastChoice(text);
            People(text, _snapshot, game);
            Conversation(text, _snapshot);
            Memory(text, game);
            Lineup(text, _snapshot);
            Synergy(text, _snapshot);
        }
        else
        {
            text.Append("\n(no game running)\n");
        }

        _text.Text = text.ToString();
    }

    private static string Esc(string? value) => (value ?? string.Empty).Replace("[", "[lb]");

    private static string Num(double value) => RequirementText.Num(value);

    private static string Signed(double value) => (value > 0 ? "+" : string.Empty) + Num(value);

    private static void Header(StringBuilder text, string title) => text.Append("\n[b]").Append(title).Append("[/b]\n");

    private string TraitName(Trait trait)
    {
        var color = GetThemeColor(trait.ToString().ToLowerInvariant(), "Traits");
        return $"[color=#{color.ToHtml(false)}]{trait}[/color]";
    }

    private void Pack(StringBuilder text)
    {
        text.Append("[b]DEV OVERLAY[/b] (F3)\n");
        if (_flow.Content is { } content)
        {
            text.Append("pack: ").Append(Esc(content.Pack.Id)).Append(content.Pack.StandIn ? " (stand-in)" : string.Empty).Append('\n');
        }
    }

    private void Player(StringBuilder text, Rules.State.GameState game)
    {
        Header(text, "Player " + Esc(game.Player.Name));
        foreach (var trait in Enum.GetValues<Trait>())
        {
            text.Append(TraitName(trait)).Append(' ').Append(Num(game.Player.Personality[trait])).Append("%\n");
        }
    }

    private void LastChoice(StringBuilder text)
    {
        Header(text, "Last choice");
        if (_lastChoice is not { } report)
        {
            text.Append("(none yet)\n");
            return;
        }

        text.Append(Esc(report.ConversationId)).Append('/').Append(Esc(report.ChoiceId ?? "(continue)"));
        text.Append(report.Ended ? " ended" : string.Empty).Append(report.NextConversationId is null ? string.Empty : " next " + Esc(report.NextConversationId)).Append('\n');
        if (report.WrittenShift.Length > 0)
        {
            text.Append("written shift: ").AppendJoin(", ", report.WrittenShift.Select(s => $"{s.Trait} {Signed(s.Amount)}")).Append('\n');
        }

        foreach (var change in report.TraitChanges)
        {
            text.Append("trait ").Append(TraitName(change.Trait)).Append(' ').Append(Num(change.Before)).Append(" -> ").Append(Num(change.After)).Append(" (").Append(Signed(change.Change)).Append(")\n");
        }

        foreach (var reaction in report.Reactions)
        {
            text.Append("react ").Append(Esc(reaction.Name)).Append(' ').Append(reaction.Kind).Append(' ').Append(reaction.Direction).Append(' ').Append(reaction.Strength)
                .Append("\n  aff ").Append(Signed(reaction.IntendedAffection)).Append(" asked, ").Append(Signed(reaction.AffectionChange))
                .Append(" applied; trust ").Append(Signed(reaction.IntendedTrust)).Append(" asked, ").Append(Signed(reaction.TrustChange)).Append(" applied\n");
        }

        foreach (var change in report.RelationshipChanges)
        {
            text.Append("relationship ").Append(Esc(change.CharacterId)).Append(" aff ").Append(Signed(change.Affection)).Append(" trust ").Append(Signed(change.Trust)).Append('\n');
        }

        Names(text, "flags set", report.FlagsSet);
        Names(text, "flags cleared", report.FlagsCleared);
        Names(text, "joined", report.Joined);
        Names(text, "left", report.Left);
        if (report.HandOff is not null)
        {
            text.Append("hand-off: ").Append(Esc(report.HandOff)).Append('\n');
        }
    }

    private static void Names(StringBuilder text, string label, IReadOnlyCollection<string> names)
    {
        if (names.Count > 0)
        {
            text.Append(label).Append(": ").AppendJoin(", ", names.Select(Esc)).Append('\n');
        }
    }

    private static void People(StringBuilder text, SessionSnapshot snapshot, Rules.State.GameState game)
    {
        Header(text, "People");
        var shown = new List<(string Id, string Name, Rules.Reactions.Demeanor Demeanor)>();
        foreach (var person in snapshot.Town?.People ?? [])
        {
            shown.Add((person.CharacterId, person.Name, person.Demeanor));
        }

        foreach (var participant in snapshot.Conversation?.Participants ?? [])
        {
            if (!shown.Any(p => p.Id == participant.Id))
            {
                shown.Add((participant.Id, participant.Name, participant.Demeanor));
            }
        }

        if (shown.Count == 0)
        {
            text.Append("(nobody present)\n");
        }

        foreach (var (id, name, demeanor) in shown)
        {
            var state = game.Characters.TryGetValue(id, out var s) ? s : null;
            text.Append(Esc(name)).Append(": aff ").Append(state is null ? "?" : Num(state.Affection)).Append(" trust ").Append(state is null ? "?" : Num(state.Trust)).Append(' ').Append(demeanor).Append('\n');
        }
    }

    private static void Conversation(StringBuilder text, SessionSnapshot snapshot)
    {
        Header(text, "Conversation");
        if (snapshot.Conversation is not { } conversation)
        {
            text.Append("(none)\n");
            return;
        }

        text.Append(Esc(conversation.Id)).Append(" step ").Append(Esc(conversation.StepId)).Append(conversation.IsContinue ? " (continue)" : string.Empty).Append('\n');
        foreach (var choice in conversation.Choices)
        {
            text.Append(choice.Available ? "open   " : "LOCKED ").Append(Esc(choice.Id)).Append(": ").Append(Esc(choice.Text)).Append('\n');
            if (!choice.Available)
            {
                if (choice.LockedText is not null)
                {
                    text.Append("  line: ").Append(Esc(choice.LockedText)).Append('\n');
                }

                if (choice.Requirement is { } requirement)
                {
                    foreach (var line in RequirementText.Format(requirement).Split('\n'))
                    {
                        text.Append("  ").Append(Esc(line)).Append('\n');
                    }
                }
            }
        }
    }

    private static void Memory(StringBuilder text, Rules.State.GameState game)
    {
        Header(text, "Memory");
        var flags = game.Memory.Flags.OrderBy(f => f, StringComparer.Ordinal).ToList();
        text.Append("flags (").Append(flags.Count).Append("): ").Append(flags.Count == 0 ? "-" : string.Join(", ", flags.Select(Esc))).Append('\n');
        text.Append("chosen ").Append(game.Memory.Chosen.Count).Append("  seen ").Append(game.Memory.Seen.Count).Append('\n');
    }

    private static void Lineup(StringBuilder text, SessionSnapshot snapshot)
    {
        Header(text, "Lineup");
        if (snapshot.Party is not { } party)
        {
            text.Append("(no party view)\n");
            return;
        }

        text.Append(party.Readiness.Ready ? "ready" : "not ready: " + Esc(party.Readiness.Reason)).Append('\n');
    }

    private static void Synergy(StringBuilder text, SessionSnapshot snapshot)
    {
        Header(text, "Synergy");
        if (snapshot.Synergy.IsDefaultOrEmpty)
        {
            text.Append("(none)\n");
            return;
        }

        foreach (var result in snapshot.Synergy)
        {
            text.Append(Esc(result.Name)).Append(": ").Append(result.Stat).Append(' ').Append(result.Step).Append(' ').Append(Num(result.Value)).Append(" match ").Append(Num(result.MatchQuality)).Append('\n');
        }
    }
}
