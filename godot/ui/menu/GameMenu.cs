using Godot;

namespace AncientOrder.Ui;

/// <summary>
/// The Esc menu: Resume, Party, Save and Load, Return to Start and Quit. The last two ask first (Cancel is focused; Esc cancels), since unsaved progress is lost.
/// It only raises events; ScreenFlow opens the screens and acts on the confirmed requests.
/// </summary>
public partial class GameMenu : Control
{
    private enum Pending
    {
        None,
        ReturnToStart,
        Quit,
    }

    private Button _resume = null!;
    private Button _party = null!;
    private Button _saves = null!;
    private Button _returnToStart = null!;
    private Button _quit = null!;
    private Control _confirm = null!;
    private Label _confirmMessage = null!;
    private Button _cancel = null!;
    private Button _accept = null!;
    private Pending _pending;
    private Button? _returnFocus;

    public event Action? ResumeRequested;

    public event Action? PartyRequested;

    public event Action? SavesRequested;

    public event Action? ReturnToStartRequested;

    public event Action? QuitRequested;

    public bool IsConfirming => _confirm.Visible;

    public Button ReturnToStartButton => _returnToStart;

    public Button QuitButton => _quit;

    public Button AcceptButton => _accept;

    public Button CancelButton => _cancel;

    public string ConfirmText => _confirmMessage.Text;

    public override void _Ready()
    {
        const string buttons = "Center/Panel/Buttons/";
        _resume = GetNode<Button>(buttons + "Resume");
        _party = GetNode<Button>(buttons + "Party");
        _saves = GetNode<Button>(buttons + "Saves");
        _returnToStart = GetNode<Button>(buttons + "ReturnToStart");
        _quit = GetNode<Button>(buttons + "Quit");
        _confirm = GetNode<Control>("Confirm");
        _confirmMessage = GetNode<Label>("Confirm/Center/Panel/Box/Message");
        _cancel = GetNode<Button>("Confirm/Center/Panel/Box/Buttons/Cancel");
        _accept = GetNode<Button>("Confirm/Center/Panel/Box/Buttons/Accept");
        _resume.Pressed += () => ResumeRequested?.Invoke();
        _party.Pressed += () => PartyRequested?.Invoke();
        _saves.Pressed += () => SavesRequested?.Invoke();
        _returnToStart.Pressed += () => Ask(Pending.ReturnToStart, _returnToStart, "Return to the start screen? Unsaved progress will be lost.", "Return to Start");
        _quit.Pressed += () => Ask(Pending.Quit, _quit, "Quit the game? Unsaved progress will be lost.", "Quit");
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
        VisibilityChanged += () =>
        {
            _pending = Pending.None;
            _confirm.Visible = false;
            SetBehindFocus(true);
        };
    }

    /// <summary>Moves keyboard focus to Resume.</summary>
    public void FocusFirst() => _resume.GrabFocus();

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

    private void Ask(Pending pending, Button from, string message, string accept)
    {
        _pending = pending;
        _returnFocus = from;
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
        if (back is not null && IsInstanceValid(back) && back.IsVisibleInTree())
        {
            back.GrabFocus();
        }
    }

    private void AcceptConfirm()
    {
        var pending = _pending;
        Close();
        switch (pending)
        {
            case Pending.ReturnToStart:
                ReturnToStartRequested?.Invoke();
                break;
            case Pending.Quit:
                QuitRequested?.Invoke();
                break;
        }
    }

    private void Close()
    {
        _pending = Pending.None;
        _confirm.Visible = false;
        SetBehindFocus(true);
    }

    private void SetBehindFocus(bool on)
    {
        var mode = on ? FocusModeEnum.All : FocusModeEnum.None;
        foreach (var button in new[] { _resume, _party, _saves, _returnToStart, _quit })
        {
            button.FocusMode = mode;
        }
    }
}
