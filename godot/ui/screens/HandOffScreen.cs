using AncientOrder.Rules.HandOff;
using Godot;

namespace AncientOrder.Ui;

/// <summary>
/// The stand-in for the Sprint 8 fight, shown while the snapshot carries a hand-off. It reads only HandOffView: the fight's name and the lineup going in, by name and in order.
/// Return to Start goes back to the start screen. It refreshes whenever it becomes visible (a loaded hand-off save is rendered without a SnapshotRendered) and on every snapshot.
/// </summary>
public partial class HandOffScreen : Control, IFlowScreen
{
    private ScreenFlow? _flow;
    private Label _fightName = null!;
    private VBoxContainer _lineup = null!;
    private Button _return = null!;

    public void Attach(ScreenFlow flow)
    {
        _flow = flow;
        flow.SnapshotRendered += snapshot =>
        {
            if (Visible)
            {
                Show(snapshot.HandOff);
            }
        };
    }

    public override void _Ready()
    {
        _fightName = GetNode<Label>("Center/Column/Panel/Content/FightName");
        _lineup = GetNode<VBoxContainer>("Center/Column/Panel/Content/LineupList");
        _return = GetNode<Button>("Center/Column/ReturnToStart");
        _return.Pressed += () => _flow?.ReturnToStart();
        VisibilityChanged += () =>
        {
            if (Visible && _flow?.Session is { } session)
            {
                Show(session.Snapshot.HandOff);
            }
        };
    }

    private void Show(HandOffView? view)
    {
        _fightName.Text = view?.FightName ?? string.Empty;
        foreach (var child in _lineup.GetChildren())
        {
            _lineup.RemoveChild(child);
            child.QueueFree();
        }

        if (view is null || view.Lineup.IsDefaultOrEmpty)
        {
            _lineup.AddChild(new Label { Text = "No one is named", ThemeTypeVariation = "MutedLabel", HorizontalAlignment = HorizontalAlignment.Center });
            return;
        }

        foreach (var member in view.Lineup)
        {
            _lineup.AddChild(new Label { Text = member.Name, HorizontalAlignment = HorizontalAlignment.Center });
        }
    }
}
