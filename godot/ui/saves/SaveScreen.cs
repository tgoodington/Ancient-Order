using System.Collections.Immutable;
using AncientOrder.Rules.Saves;
using Godot;

namespace AncientOrder.Ui;

/// <summary>
/// The save and load screen (T20). Ten slot rows, Save / Load / Delete for the selected row, an inline status line (busy or the refusal reason) and one modal
/// confirmation panel (Cancel is focused first). Render is the only way state comes in; the three events are the only way requests go out, and each is raised
/// only after any confirmation is accepted. Names in slots come from save files, so they only ever go into plain Labels.
/// Keyboard: up and down move between rows (selection follows focus), Enter on a row moves to its main action, Esc cancels a confirmation (otherwise it is the flow's).
/// </summary>
public partial class SaveScreen : Control, IFlowScreen, ISaveScreen
{
    private const int SlotCount = 10;

    private enum Pending
    {
        None,
        Overwrite,
        Delete,
        Load,
    }

    private readonly List<SlotRow> _rows = new();
    private ScreenFlow? _flow;
    private ImmutableArray<SlotInfo> _slots = ImmutableArray<SlotInfo>.Empty;
    private SaveScreenMode _mode = SaveScreenMode.LoadOnly;
    private bool _busy;
    private int _selected;
    private bool _wantFocus;
    private Pending _pending;
    private Control? _returnFocus;
    private Label _title = null!;
    private Label _busyLine = null!;
    private PanelContainer _errorBox = null!;
    private Label _errorText = null!;
    private Button _save = null!;
    private Button _load = null!;
    private Button _delete = null!;
    private Button _back = null!;
    private Control _confirm = null!;
    private Label _confirmHeading = null!;
    private Label _confirmMessage = null!;
    private Button _cancel = null!;
    private Button _accept = null!;

    [Export]
    public PackedScene? SlotRowScene { get; set; }

    public event Action<int>? SaveRequested;

    public event Action<int>? LoadRequested;

    public event Action<int>? DeleteRequested;

    public bool IsConfirming => _confirm.Visible;

    public int SelectedSlot => _selected;

    public IReadOnlyList<SlotRow> Rows => _rows;

    public Button SaveButton => _save;

    public Button LoadButton => _load;

    public Button DeleteButton => _delete;

    public Button AcceptButton => _accept;

    public Button CancelButton => _cancel;

    public string ErrorShown => _errorBox.Visible ? _errorText.Text : string.Empty;

    public bool BusyShown => _busyLine.Visible;

    public void Attach(ScreenFlow flow) => _flow = flow;

    /// <summary>Selects a slot row (what a click or arrow focus does). For tests and the dev tools.</summary>
    public void SelectSlot(int slot)
    {
        if (slot is >= 1 and <= SlotCount)
        {
            Select(slot);
        }
    }

    public override void _Ready()
    {
        const string column = "Center/Panel/Column/";
        _title = GetNode<Label>(column + "Title");
        _busyLine = GetNode<Label>(column + "Status/Busy");
        _errorBox = GetNode<PanelContainer>(column + "Status/Error");
        _errorText = GetNode<Label>(column + "Status/Error/Message");
        _save = GetNode<Button>(column + "Actions/Save");
        _load = GetNode<Button>(column + "Actions/Load");
        _delete = GetNode<Button>(column + "Actions/Delete");
        _back = GetNode<Button>(column + "Actions/Back");
        _confirm = GetNode<Control>("Confirm");
        _confirmHeading = GetNode<Label>("Confirm/Center/Panel/Box/Heading");
        _confirmMessage = GetNode<Label>("Confirm/Center/Panel/Box/Message");
        _cancel = GetNode<Button>("Confirm/Center/Panel/Box/Buttons/Cancel");
        _accept = GetNode<Button>("Confirm/Center/Panel/Box/Buttons/Accept");

        var rows = GetNode<VBoxContainer>(column + "Scroll/Rows");
        var group = new ButtonGroup();
        for (var i = 1; i <= SlotCount; i++)
        {
            var row = SlotRowScene!.Instantiate<SlotRow>();
            row.ButtonGroup = group;
            rows.AddChild(row);
            row.Show(i, null);
            var slot = i;
            row.Pressed += () =>
            {
                Select(slot);
                FocusPrimary();
            };
            row.FocusEntered += () => Select(slot);
            _rows.Add(row);
        }

        _save.Pressed += OnSave;
        _load.Pressed += OnLoad;
        _delete.Pressed += OnDelete;
        _back.Pressed += () => _flow?.Back();
        _cancel.Pressed += CancelConfirm;
        _accept.Pressed += AcceptConfirm;
        _cancel.FocusNeighborRight = _accept.GetPath();
        _accept.FocusNeighborLeft = _cancel.GetPath();
        _cancel.FocusNeighborLeft = _cancel.GetPath();
        _accept.FocusNeighborRight = _accept.GetPath();
        _cancel.FocusNeighborTop = _cancel.GetPath();
        _accept.FocusNeighborTop = _accept.GetPath();
        _cancel.FocusNeighborBottom = _cancel.GetPath();
        _accept.FocusNeighborBottom = _accept.GetPath();
        VisibilityChanged += OnVisibilityChanged;
        Apply();
    }

    public void Render(ImmutableArray<SlotInfo> slots, SaveScreenMode mode, bool busy, string? error)
    {
        var wasBusy = _busy;
        var before = _confirm.Visible && _selected > 0 ? InfoFor(_selected) : null;
        var modeBefore = _mode;
        _slots = slots;
        _mode = mode;
        _busy = busy;
        if (_selected == 0)
        {
            _selected = DefaultSelection();
        }

        _title.Text = mode == SaveScreenMode.LoadOnly ? "Load Game" : "Save and Load";
        _busyLine.Visible = busy;
        _errorBox.Visible = !busy && !string.IsNullOrEmpty(error);
        _errorText.Text = error ?? string.Empty;
        Apply();
        if (_confirm.Visible && (busy || mode != modeBefore || InfoFor(_selected) != before))
        {
            CancelConfirm();
        }

        if (wasBusy && !busy && _wantFocus)
        {
            _wantFocus = false;
            FocusSelected();
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!_confirm.Visible || !IsVisibleInTree() || @event.IsEcho())
        {
            return;
        }

        if (@event.IsActionPressed("ui_menu") || @event.IsActionPressed("ui_cancel"))
        {
            CancelConfirm();
            GetViewport().SetInputAsHandled();
        }
    }

    private void OnVisibilityChanged()
    {
        _pending = Pending.None;
        _confirm.Visible = false;
        SetBehindFocus(true);
        if (Visible)
        {
            _selected = 0;
            _wantFocus = true;
            _slots = ImmutableArray<SlotInfo>.Empty;
            _errorBox.Visible = false;
            Apply();
        }
    }

    private int DefaultSelection()
    {
        if (_mode == SaveScreenMode.LoadOnly)
        {
            var first = _slots.FirstOrDefault(s => s.Status != SlotStatus.Empty);
            if (first is not null)
            {
                return first.Slot;
            }
        }

        return 1;
    }

    private SlotInfo? InfoFor(int slot) => _slots.IsDefaultOrEmpty ? null : _slots.FirstOrDefault(s => s.Slot == slot);

    private void Apply()
    {
        var listed = !_slots.IsDefaultOrEmpty;
        for (var i = 0; i < _rows.Count; i++)
        {
            var row = _rows[i];
            row.Show(i + 1, listed ? InfoFor(i + 1) ?? new SlotInfo(i + 1, false, SlotStatus.Empty, null) : null);
            row.Disabled = _busy || !listed;
            row.SetPressedNoSignal(_selected == i + 1);
        }

        var info = _selected > 0 ? InfoFor(_selected) : null;
        var status = info?.Status ?? SlotStatus.Empty;
        var canSave = _mode == SaveScreenMode.SaveAndLoad;
        _save.Visible = canSave;
        _save.Disabled = _busy || !canSave || info is null;
        _load.Disabled = _busy || status != SlotStatus.Saved;
        _delete.Disabled = _busy || info is null || status == SlotStatus.Empty;
        FixFocusChain();
    }

    private void FixFocusChain()
    {
        if (_rows.Count == 0)
        {
            return;
        }

        var first = _save.Visible ? _save : _load;
        _rows[^1].FocusNeighborBottom = first.GetPath();
        var above = _rows[Math.Max(0, _selected - 1)].GetPath();
        foreach (var button in new[] { _save, _load, _delete, _back })
        {
            button.FocusNeighborTop = above;
        }
    }

    private void Select(int slot)
    {
        if (_selected == slot)
        {
            return;
        }

        _selected = slot;
        Apply();
    }

    private void FocusSelected()
    {
        Callable.From(() =>
        {
            if (!IsInstanceValid(this) || !IsVisibleInTree() || _confirm.Visible || _selected == 0)
            {
                return;
            }

            var row = _rows[_selected - 1];
            if (!row.Disabled)
            {
                row.GrabFocus();
            }
        }).CallDeferred();
    }

    private void FocusPrimary()
    {
        var target = (_selected > 0 ? InfoFor(_selected)?.Status : null) switch
        {
            SlotStatus.Saved => _load,
            SlotStatus.Corrupted => _delete,
            _ => _save,
        };
        if (target.Visible && !target.Disabled)
        {
            target.GrabFocus();
        }
    }

    private void OnSave()
    {
        var info = InfoFor(_selected);
        if (info is null || _busy)
        {
            return;
        }

        if (info.Status == SlotStatus.Empty)
        {
            Request(SaveRequested, info.Slot);
            return;
        }

        var who = info.Status == SlotStatus.Saved && !string.IsNullOrWhiteSpace(info.Metadata?.PlayerName) ? $" ({info.Metadata!.PlayerName})" : string.Empty;
        Ask(Pending.Overwrite, _save, "Overwrite this save?", $"Slot {info.Slot}{who} already holds a save. Saving here replaces it.", "Overwrite");
    }

    private void OnLoad()
    {
        var info = InfoFor(_selected);
        if (info is null || _busy || info.Status != SlotStatus.Saved)
        {
            return;
        }

        if (_mode == SaveScreenMode.LoadOnly)
        {
            Request(LoadRequested, info.Slot);
            return;
        }

        Ask(Pending.Load, _load, "Load this save?", $"Anything you have done since your last save will be lost. Slot {info.Slot} replaces the game in progress.", "Load");
    }

    private void OnDelete()
    {
        var info = InfoFor(_selected);
        if (info is null || _busy || info.Status == SlotStatus.Empty)
        {
            return;
        }

        var text = info.Status == SlotStatus.Corrupted
            ? $"Slot {info.Slot} holds a damaged save. Deleting clears the slot."
            : $"Slot {info.Slot} will be emptied. This can't be undone.";
        Ask(Pending.Delete, _delete, "Delete this save?", text, "Delete");
    }

    private void Ask(Pending pending, Control from, string heading, string message, string accept)
    {
        _pending = pending;
        _returnFocus = from;
        _confirmHeading.Text = heading;
        _confirmMessage.Text = message;
        _accept.Text = accept;
        SetBehindFocus(false);
        _confirm.Visible = true;
        _cancel.GrabFocus();
    }

    private void CancelConfirm()
    {
        if (!_confirm.Visible)
        {
            return;
        }

        var back = _returnFocus;
        Close();
        if (back is Button { Disabled: false } button && IsInstanceValid(button) && button.IsVisibleInTree())
        {
            button.GrabFocus();
        }
        else
        {
            FocusSelected();
        }
    }

    private void AcceptConfirm()
    {
        var pending = _pending;
        var slot = _selected;
        Close();
        if (_busy)
        {
            return;
        }

        switch (pending)
        {
            case Pending.Overwrite:
                Request(SaveRequested, slot);
                break;
            case Pending.Load:
                Request(LoadRequested, slot);
                break;
            case Pending.Delete:
                Request(DeleteRequested, slot);
                break;
        }
    }

    private void Close()
    {
        _pending = Pending.None;
        _confirm.Visible = false;
        SetBehindFocus(true);
    }

    private void Request(Action<int>? request, int slot)
    {
        _wantFocus = true;
        request?.Invoke(slot);
    }

    private void SetBehindFocus(bool on)
    {
        var mode = on ? FocusModeEnum.All : FocusModeEnum.None;
        foreach (var row in _rows)
        {
            row.FocusMode = mode;
        }

        _save.FocusMode = mode;
        _load.FocusMode = mode;
        _delete.FocusMode = mode;
        _back.FocusMode = mode;
    }
}
