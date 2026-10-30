using System.Globalization;
using AncientOrder.Rules.Combat;
using AncientOrder.Rules.Party;
using AncientOrder.Rules.Personality;
using AncientOrder.Rules.State;
using Godot;

namespace AncientOrder.Ui;

/// <summary>
/// The party screen (T19): the player's readout, the companions with their lineup toggles, the fighting lineup against its limits, and how the lineup feels together.
/// Render draws a PartyView in full each call and draws nothing the view does not carry. Member readouts and synergy numbers appear only when the view says so.
/// A toggle raises LineupToggled and does not keep its own state: the presenter draws again, so a refused pick snaps back.
/// </summary>
public partial class PartyScreen : Control, IPartyScreen, IFlowScreen
{
    private const string MemberIdKey = "member_id";
    private static readonly Color HeaderColor = new(0.6588f, 0.5961f, 0.502f);
    private static readonly Color BodyColor = new(0.9098f, 0.8627f, 0.7843f);

    private readonly PackedScene _card = GD.Load<PackedScene>("res://ui/party/MemberCard.tscn");
    private ScreenFlow? _flow;
    private PartyView? _pending;
    private bool _ready;
    private Label _playerName = null!;
    private VBoxContainer _playerTraits = null!;
    private ColorRect _combatRule = null!;
    private GridContainer _playerCombat = null!;
    private Label _partyEmpty = null!;
    private VBoxContainer _cards = null!;
    private Label _lineupCount = null!;
    private VBoxContainer _lineupNames = null!;
    private Label _lineupEmpty = null!;
    private Label _reason = null!;
    private VBoxContainer _together = null!;
    private VBoxContainer _togetherList = null!;

    public event Action<string, bool>? LineupToggled;

    public void Attach(ScreenFlow flow) => _flow = flow;

    public override void _Ready()
    {
        _playerName = GetNode<Label>("%PlayerName");
        _playerTraits = GetNode<VBoxContainer>("%PlayerTraits");
        _combatRule = GetNode<ColorRect>("%CombatRule");
        _playerCombat = GetNode<GridContainer>("%PlayerCombat");
        _partyEmpty = GetNode<Label>("%PartyEmpty");
        _cards = GetNode<VBoxContainer>("%Cards");
        _lineupCount = GetNode<Label>("%LineupCount");
        _lineupNames = GetNode<VBoxContainer>("%LineupNames");
        _lineupEmpty = GetNode<Label>("%LineupEmpty");
        _reason = GetNode<Label>("%Reason");
        _together = GetNode<VBoxContainer>("%Together");
        _togetherList = GetNode<VBoxContainer>("%TogetherList");
        GetNode<Button>("%Close").Pressed += () => _flow?.Back();
        _ready = true;
        if (_pending is { } view)
        {
            _pending = null;
            Render(view);
        }
    }

    public void Render(PartyView view)
    {
        if (!_ready)
        {
            _pending = view;
            return;
        }

        var focusedId = FocusedMemberId();
        RenderPlayer(view.Player);
        RenderMembers(view);
        RenderLineup(view);
        RenderTogether(view);
        if (focusedId is not null && IsVisibleInTree())
        {
            foreach (var toggle in _cards.FindChildren("Toggle", "CheckButton", true, false))
            {
                if (toggle.HasMeta(MemberIdKey) && (string)toggle.GetMeta(MemberIdKey) == focusedId)
                {
                    ((CheckButton)toggle).GrabFocus();
                }
            }
        }
    }

    private string? FocusedMemberId()
    {
        var owner = GetViewport()?.GuiGetFocusOwner();
        return owner is not null && IsAncestorOf(owner) && owner.HasMeta(MemberIdKey) ? (string)owner.GetMeta(MemberIdKey) : null;
    }

    private static void Clear(Node container)
    {
        foreach (var child in container.GetChildren())
        {
            container.RemoveChild(child);
            child.QueueFree();
        }
    }

    private void RenderPlayer(PlayerReadout player)
    {
        _playerName.Text = player.Name;
        Clear(_playerTraits);
        foreach (var trait in Enum.GetValues<Trait>())
        {
            _playerTraits.AddChild(TraitRow(trait, player.Personality[trait]));
        }

        Clear(_playerCombat);
        var hasCombat = player.Combat is not null;
        _combatRule.Visible = hasCombat;
        _playerCombat.Visible = hasCombat;
        if (player.Combat is { } combat)
        {
            foreach (var (label, value) in CombatStats(combat))
            {
                _playerCombat.AddChild(TextLabel(label, HeaderColor));
                _playerCombat.AddChild(TextLabel(value, BodyColor));
            }
        }
    }

    private HBoxContainer TraitRow(Trait trait, double value)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 10);
        var name = TextLabel(trait.ToString(), BodyColor);
        name.CustomMinimumSize = new Vector2(84, 0);
        row.AddChild(name);
        var bar = TraitBar(trait, value, 16);
        bar.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        row.AddChild(bar);
        var number = TextLabel(Percent(value), BodyColor);
        number.CustomMinimumSize = new Vector2(48, 0);
        number.HorizontalAlignment = HorizontalAlignment.Right;
        row.AddChild(number);
        return row;
    }

    private ProgressBar TraitBar(Trait trait, double value, float height)
    {
        var bar = new ProgressBar
        {
            ThemeTypeVariation = "ReadoutBar",
            MinValue = PersonalityRules.MinTraitValue,
            MaxValue = PersonalityRules.MaxTraitValue,
            Value = value,
            ShowPercentage = false,
            CustomMinimumSize = new Vector2(0, height),
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
            FocusMode = FocusModeEnum.None,
        };
        var fill = GetThemeStylebox("fill", "ReadoutBar") is StyleBoxFlat themed ? (StyleBoxFlat)themed.Duplicate() : new StyleBoxFlat();
        fill.BgColor = TraitColor(trait);
        bar.AddThemeStyleboxOverride("fill", fill);
        return bar;
    }

    private Color TraitColor(Trait trait) => GetThemeColor(trait.ToString().ToLowerInvariant(), "Traits");

    private void RenderMembers(PartyView view)
    {
        Clear(_cards);
        _partyEmpty.Visible = view.Members.IsEmpty;
        foreach (var member in view.Members)
        {
            var card = _card.Instantiate<PanelContainer>();
            _cards.AddChild(card);
            card.GetNode<Label>("%Name").Text = member.Name;
            var toggle = card.GetNode<CheckButton>("%Toggle");
            toggle.SetMeta(MemberIdKey, member.Id);
            toggle.SetPressedNoSignal(member.InLineup);
            toggle.Disabled = !member.CanFight;
            card.GetNode<Label>("%Status").Visible = !member.CanFight;
            var id = member.Id;
            toggle.Toggled += on => LineupToggled?.Invoke(id, on);
            if (view.ShowMemberReadouts && member.Readout is { } readout)
            {
                RenderMemberReadout(card, readout);
            }
        }
    }

    private void RenderMemberReadout(Control card, MemberReadout readout)
    {
        card.GetNode<VBoxContainer>("%Readout").Visible = true;
        var grid = card.GetNode<GridContainer>("%Traits");
        foreach (var trait in Enum.GetValues<Trait>())
        {
            var value = readout.Personality[trait];
            var cell = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            cell.AddThemeConstantOverride("separation", 2);
            var line = new HBoxContainer();
            var name = TextLabel(trait.ToString(), TraitColor(trait));
            name.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            line.AddChild(name);
            line.AddChild(TextLabel(Percent(value), BodyColor));
            cell.AddChild(line);
            cell.AddChild(TraitBar(trait, value, 5));
            grid.AddChild(cell);
        }

        if (readout.Combat is { } combat)
        {
            var label = card.GetNode<Label>("%Combat");
            label.Visible = true;
            label.Text = string.Join("   ", CombatStats(combat).Select(s => $"{s.Label} {s.Value}"));
        }
    }

    private void RenderLineup(PartyView view)
    {
        var limits = view.Limits;
        var count = view.Lineup.Length;
        _lineupCount.Text = limits.Min == limits.Max ? $"{count} of {limits.Max}" : $"{count} of {limits.Min} to {limits.Max}";
        Clear(_lineupNames);
        _lineupEmpty.Visible = view.Lineup.IsEmpty;
        for (var i = 0; i < view.Lineup.Length; i++)
        {
            _lineupNames.AddChild(TextLabel($"{i + 1}.  {view.Lineup[i].Name}", BodyColor));
        }

        var showReason = !view.Readiness.Ready && !string.IsNullOrEmpty(view.Readiness.Reason);
        _reason.Visible = showReason;
        _reason.Text = showReason ? view.Readiness.Reason : string.Empty;
    }

    private void RenderTogether(PartyView view)
    {
        Clear(_togetherList);
        foreach (var bonus in view.Synergy)
        {
            if (!string.IsNullOrEmpty(bonus.Feel))
            {
                var feel = new Label { Text = bonus.Feel, ThemeTypeVariation = "TellLine", AutowrapMode = TextServer.AutowrapMode.WordSmart };
                _togetherList.AddChild(feel);
            }

            if (view.ShowSynergyNumbers && bonus.Name is not null && bonus.Stat is { } stat && bonus.Step is { } step && bonus.Value is { } value)
            {
                _togetherList.AddChild(TextLabel($"{bonus.Name}   {Effect(step, value)} {StatName(stat)}", BodyColor));
            }
        }

        _together.Visible = _togetherList.GetChildCount() > 0;
    }

    private static Label TextLabel(string text, Color color)
    {
        var label = new Label { Text = text };
        label.AddThemeColorOverride("font_color", color);
        return label;
    }

    private static string Percent(double value) => Math.Round(value, MidpointRounding.AwayFromZero).ToString("0", CultureInfo.InvariantCulture) + "%";

    private static string Num(double value)
    {
        var whole = Math.Round(value);
        return Math.Abs(value - whole) < 1e-9 ? whole.ToString("0", CultureInfo.InvariantCulture) : value.ToString("0.##", CultureInfo.InvariantCulture);
    }

    private static IEnumerable<(string Label, string Value)> CombatStats(CombatProfile combat)
    {
        yield return ("Rank", Num(combat.Rank));
        yield return ("Stamina", Num(combat.Stamina));
        yield return ("Power", Num(combat.Power));
        yield return ("Speed", Num(combat.Speed));
        yield return ("Path", combat.ElementalPath.ToString());
    }

    private static string StatName(CombatStat stat) => stat == CombatStat.MaxStamina ? "Stamina" : stat.ToString();

    private static string Effect(StatStep step, double value) => step switch
    {
        StatStep.Multiply => "x" + value.ToString("0.00", CultureInfo.InvariantCulture),
        StatStep.AddPercent => Signed(value * 100) + "%",
        _ => Signed(value),
    };

    private static string Signed(double value) => (value < 0 ? "-" : "+") + Num(Math.Abs(value));
}
