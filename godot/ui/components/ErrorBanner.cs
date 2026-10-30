using AncientOrder.Rules.Common;
using Godot;

namespace AncientOrder.Ui;

/// <summary>
/// A top-anchored banner that shows why an action was refused.
/// It hides after a few seconds or on click, and a newer refusal replaces the one shown.
/// </summary>
public partial class ErrorBanner : PanelContainer
{
    private const double ShowSeconds = 5.0;

    private Label _message = null!;
    private Godot.Timer _timer = null!;

    public override void _Ready()
    {
        _message = GetNode<Label>("Message");
        _timer = GetNode<Godot.Timer>("HideTimer");
        _timer.OneShot = true;
        _timer.Timeout += Dismiss;
        GuiInput += OnGuiInput;
        Visible = false;
    }

    public void Show(RuleError error) => Show(error.Message);

    public void Show(string message)
    {
        _message.Text = message;
        Visible = true;
        _timer.Start(ShowSeconds);
    }

    private void Dismiss()
    {
        _timer.Stop();
        Visible = false;
    }

    private void OnGuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton { Pressed: true })
        {
            Dismiss();
            AcceptEvent();
        }
    }
}
