using AncientOrder.Rules.Conversations;
using Godot;

namespace AncientOrder.Ui;

/// <summary>
/// The conversation panel (T17): a bottom-anchored panel over the town that shows the previous choice's outcome (player line, body-language lines, party notices), the current step's lines,
/// and choices (locked ones greyed with their in-world line) or Continue. All content text is plain Label text, never markup, and no numbers are shown.
/// The panel's height follows its content, from a small minimum up to MaxHeightShare of the viewport; past that the scroll container scrolls and shows its scrollbar.
/// Mouse, choice_1..choice_9 (the Nth shown row, locked ones counted), arrows and ui_accept all take choices. Up and down move the highlight between the selectable buttons and wrap at the ends;
/// locked choices are never focusable. Hovering a choice moves the highlight but never takes it. Visibility is owned by ScreenFlow.
/// </summary>
public partial class ConversationPanel : Control, IConversationPanel
{
    private const float MaxWidth = 900f;
    private const float Gutter = 16f;
    private const float BottomGap = 12f;
    private const float MinHeight = 160f;
    private const float MaxHeightShare = 0.66f;
    private const int MaxChoiceKeys = 9;

    private static readonly Color AccentColor = new(0.5451f, 0.4510f, 0.3333f);

    private PanelContainer _panel = null!;
    private Label _header = null!;
    private ScrollContainer _scroll = null!;
    private VBoxContainer _content = null!;
    private VBoxContainer _outcome = null!;
    private VBoxContainer _transcript = null!;
    private VBoxContainer _choices = null!;
    private ConversationOutcome? _staged;
    private readonly List<(Button Button, string Id, bool Available)> _choiceButtons = [];
    private Tween? _fade;
    private bool _taken;
    private float _contentHeight;

    public event Action<string>? ChoiceTaken;

    public event Action? ContinueTaken;

    public event Action? CloseTaken;

    public override void _Ready()
    {
        _panel = GetNode<PanelContainer>("Panel");
        _header = GetNode<Label>("Panel/Column/Header");
        _scroll = GetNode<ScrollContainer>("Panel/Column/Scroll");
        _content = GetNode<VBoxContainer>("Panel/Column/Scroll/Content");
        _outcome = GetNode<VBoxContainer>("Panel/Column/Scroll/Content/Outcome");
        _transcript = GetNode<VBoxContainer>("Panel/Column/Scroll/Content/Transcript");
        _choices = GetNode<VBoxContainer>("Panel/Column/Scroll/Content/Choices");
        Resized += OnResized;
        Layout();
    }

    public void ShowOutcome(ConversationOutcome outcome, bool final)
    {
        _taken = false;
        if (!final)
        {
            _staged = outcome;
            return;
        }

        Clear();
        _staged = null;
        FillOutcome(outcome, false);
        var close = MakeButton("Close");
        close.Pressed += () => Raise(() => CloseTaken?.Invoke());
        _choices.AddChild(close);
        Presented(close);
    }

    public void ShowStep(ConversationView view)
    {
        Clear();
        _header.Text = view.Title ?? "";
        _header.Visible = !string.IsNullOrEmpty(view.Title);

        if (_staged is { } staged)
        {
            FillOutcome(staged, true);
            _staged = null;
        }

        foreach (var line in view.Lines)
        {
            _transcript.AddChild(LineBlock(line));
        }

        Button? first = null;
        if (view.Choices.IsDefaultOrEmpty)
        {
            var next = MakeButton("Continue");
            next.Pressed += () => Raise(() => ContinueTaken?.Invoke());
            _choices.AddChild(next);
            first = next;
        }
        else
        {
            foreach (var choice in view.Choices)
            {
                var text = choice.Text;
                var button = MakeButton(text);
                if (choice.Available)
                {
                    var id = choice.Id;
                    button.Pressed += () => Raise(() => ChoiceTaken?.Invoke(id));
                    button.MouseEntered += () =>
                    {
                        if (IsVisibleInTree() && ProcessMode != ProcessModeEnum.Disabled)
                        {
                            button.GrabFocus();
                        }
                    };
                    first ??= button;
                }
                else
                {
                    button.Disabled = true;
                    button.FocusMode = FocusModeEnum.None;
                    button.ThemeTypeVariation = "LockedChoice";
                    if (!string.IsNullOrEmpty(choice.LockedText))
                    {
                        button.Text = text + "\n" + choice.LockedText;
                    }
                }

                _choices.AddChild(button);
                _choiceButtons.Add((button, choice.Id, choice.Available));
            }
        }

        Presented(first);
    }

    public void Close()
    {
        _taken = false;
        Clear();
        _staged = null;
        _header.Text = "";
        _header.Visible = false;
    }

    public override void _Input(InputEvent @event)
    {
        if (IsVisibleInTree() && @event.IsPressed() && (@event.IsAction("ui_up") || @event.IsAction("ui_down")))
        {
            if (MoveHighlight(@event.IsAction("ui_down") ? 1 : -1))
            {
                GetViewport().SetInputAsHandled();
            }

            return;
        }

        if (!IsVisibleInTree() || _choiceButtons.Count == 0 || !@event.IsPressed() || @event.IsEcho())
        {
            return;
        }

        for (var i = 0; i < Math.Min(MaxChoiceKeys, _choiceButtons.Count); i++)
        {
            if (@event.IsAction($"choice_{i + 1}"))
            {
                GetViewport().SetInputAsHandled();
                if (_choiceButtons[i].Available)
                {
                    var id = _choiceButtons[i].Id;
                    Raise(() => ChoiceTaken?.Invoke(id));
                }

                return;
            }
        }
    }

    private bool MoveHighlight(int step)
    {
        var targets = _choices.GetChildren().OfType<Button>().Where(b => !b.Disabled && b.FocusMode != FocusModeEnum.None).ToList();
        if (targets.Count == 0)
        {
            return false;
        }

        var at = targets.FindIndex(b => b.HasFocus());
        var next = at < 0 ? (step > 0 ? 0 : targets.Count - 1) : (at + step + targets.Count) % targets.Count;
        targets[next].GrabFocus();
        return true;
    }

    private void Layout()
    {
        var width = Math.Min(MaxWidth, Size.X - Gutter * 2);
        var cap = Math.Max(MinHeight, Size.Y * MaxHeightShare - BottomGap);
        var height = Math.Clamp(_contentHeight, MinHeight, cap);
        _panel.OffsetLeft = -width / 2;
        _panel.OffsetRight = width / 2;
        _panel.OffsetBottom = -BottomGap;
        _panel.OffsetTop = -BottomGap - height;
    }

    private void Fit()
    {
        _contentHeight = _panel.GetCombinedMinimumSize().Y + _content.GetCombinedMinimumSize().Y;
        Layout();
    }

    private async void OnResized()
    {
        Layout();
        if (!IsInsideTree() || !_panel.IsVisibleInTree())
        {
            return;
        }

        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        if (GodotObject.IsInstanceValid(this) && IsInsideTree())
        {
            Fit();
        }
    }

    private void Raise(Action raise)
    {
        if (_taken)
        {
            return;
        }

        _taken = true;
        raise();
    }

    private void Clear()
    {
        _taken = false;
        _fade?.Kill();
        _choiceButtons.Clear();
        foreach (var box in new[] { _outcome, _transcript, _choices })
        {
            foreach (var child in box.GetChildren())
            {
                box.RemoveChild(child);
                child.QueueFree();
            }
        }
    }

    private void FillOutcome(ConversationOutcome outcome, bool separate)
    {
        if (!string.IsNullOrEmpty(outcome.PlayerLine))
        {
            _outcome.AddChild(SpokenBlock(outcome.PlayerName, outcome.PlayerLine, true));
        }

        if (!outcome.BodyLines.IsDefaultOrEmpty)
        {
            foreach (var tell in outcome.BodyLines)
            {
                _outcome.AddChild(MakeLabel(tell, "TellLine"));
            }
        }

        if (!outcome.Notices.IsDefaultOrEmpty)
        {
            foreach (var notice in outcome.Notices)
            {
                _outcome.AddChild(MakeLabel(notice, "MutedLabel"));
            }
        }

        if (separate && _outcome.GetChildCount() > 0)
        {
            _outcome.AddChild(new HSeparator());
        }
    }

    private static Control LineBlock(LineView line) => line.SpeakerKind switch
    {
        SpeakerKind.Narrator => MakeLabel(line.Text, "NarrationLine"),
        SpeakerKind.Player => SpokenBlock(line.SpeakerName, line.Text, true),
        _ => SpokenBlock(line.SpeakerName, line.Text, false),
    };

    private static Control SpokenBlock(string? name, string text, bool player)
    {
        var column = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        column.AddThemeConstantOverride("separation", 2);
        if (!string.IsNullOrEmpty(name))
        {
            var label = MakeLabel(name, "TitleLabel");
            label.AddThemeFontSizeOverride("font_size", 20);
            column.AddChild(label);
        }

        column.AddChild(MakeLabel(text, null));
        if (!player)
        {
            return column;
        }

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 10);
        row.AddChild(new ColorRect { Color = AccentColor, CustomMinimumSize = new Vector2(3, 0), MouseFilter = MouseFilterEnum.Ignore });
        row.AddChild(column);
        return row;
    }

    private static Label MakeLabel(string text, string? variation)
    {
        var label = new Label
        {
            Text = text,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        if (variation is not null)
        {
            label.ThemeTypeVariation = variation;
        }

        return label;
    }

    private static Button MakeButton(string text) => new()
    {
        Text = text,
        Alignment = HorizontalAlignment.Left,
        AutowrapMode = TextServer.AutowrapMode.WordSmart,
        SizeFlagsHorizontal = SizeFlags.ExpandFill,
        FocusMode = FocusModeEnum.All,
    };

    private void Presented(Button? focus)
    {
        if (!IsInsideTree())
        {
            return;
        }

        _panel.Modulate = new Color(1, 1, 1, 0);
        if (focus is not null)
        {
            Callable.From(() =>
            {
                if (GodotObject.IsInstanceValid(focus) && focus.IsVisibleInTree())
                {
                    focus.GrabFocus();
                }
            }).CallDeferred();
        }

        CallDeferred(nameof(Reveal));
    }

    private async void Reveal()
    {
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        if (!GodotObject.IsInstanceValid(this) || !IsInsideTree())
        {
            return;
        }

        _fade?.Kill();
        Fit();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        if (!GodotObject.IsInstanceValid(this) || !IsInsideTree())
        {
            return;
        }

        Fit();
        _panel.Modulate = Colors.White;
        _content.Modulate = new Color(1, 1, 1, 0);
        _fade = CreateTween();
        _fade.TweenProperty(_content, "modulate:a", 1.0f, 0.18);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        if (!GodotObject.IsInstanceValid(this) || !IsInsideTree())
        {
            return;
        }

        var firstNew = new[] { _outcome, _transcript, _choices }.SelectMany(box => box.GetChildren()).OfType<Control>().FirstOrDefault();
        var top = firstNew is null ? 0 : (int)(firstNew.GlobalPosition.Y - _content.GlobalPosition.Y);
        _scroll.ScrollVertical = Math.Clamp(top, 0, (int)_scroll.GetVScrollBar().MaxValue);
    }
}
