namespace AncientOrder.Ui;

/// <summary>
/// Implemented by a screen scene's root script to get the flow it lives in. ScreenFlow calls Attach once, before the screen is added to the tree,
/// so the screen can call StartNewGame, ReturnToStart, OpenSaves, ShowError and subscribe to the flow's events.
/// </summary>
public interface IFlowScreen
{
    void Attach(ScreenFlow flow);
}
