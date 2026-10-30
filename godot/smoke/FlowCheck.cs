using AncientOrder.Rules.Content.Check;
using AncientOrder.Rules.Saves;
using AncientOrder.Rules.Session;
using AncientOrder.Ui;
using Godot;

namespace AncientOrder;

/// <summary>
/// Headless check of the screen flow (T13): boots main.tscn on the stand-in pack and on a missing pack, then walks start, new game, menu, party, saves, esc, save, return to start, load.
/// Saves go under user://smoke/flow/saves, which it empties before and after. Exits 0 on success, or 1 after printing the failing step.
/// </summary>
public partial class FlowCheck : Node
{
    private const string SaveFolder = "user://smoke/flow/saves";

    public override async void _Ready()
    {
        var step = "start";
        var folder = ProjectSettings.GlobalizePath(SaveFolder);
        try
        {
            Empty(folder);
            await Run(folder, s =>
            {
                step = s;
                GD.Print("flow: " + s);
            });
            Empty(folder);
            GD.Print("FLOW PASSED");
            GetTree().Quit(0);
        }
        catch (Exception ex)
        {
            Empty(folder);
            GD.PrintErr($"FLOW FAILED at step '{step}': {ex.Message}");
            GetTree().Quit(1);
        }
    }

    private static void Need(bool ok, string what)
    {
        if (!ok)
        {
            throw new InvalidOperationException(what);
        }
    }

    private static void Empty(string folder)
    {
        if (Directory.Exists(folder))
        {
            Directory.Delete(folder, true);
        }
    }

    private async Task Frame() => await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

    private ScreenFlow Make()
    {
        var flow = GD.Load<PackedScene>("res://main.tscn").Instantiate<ScreenFlow>();
        flow.AutoStart = false;
        AddChild(flow);
        return flow;
    }

    private static InputEventAction Esc() => new() { Action = "ui_menu", Pressed = true };

    private async Task Run(string folder, Action<string> step)
    {
        step("broken pack shows the content error screen");
        var broken = Make();
        var badReport = ContentCheck.Run(new GodotContentSource("res://smoke/no_such_pack"));
        Need(badReport.Content is null, "missing pack passed");
        broken.Start(null, null, badReport);
        await Frame();
        Need(broken.IsContentErrorShown, "content error screen not shown");
        broken.QueueFree();
        await Frame();

        step("stand-in pack boots to the start screen");
        var report = ContentCheck.Run(new GodotContentSource("res://content/standin"));
        var content = report.Content ?? throw new InvalidOperationException("stand-in pack failed");
        var session = new GameSession(content, new GameSessionOptions { Saves = new SaveStore(new FileSaveBackend(folder), content) });
        var flow = Make();
        flow.Start(content, session, report);
        await Frame();
        Need(flow.IsAtStart && flow.StartScreen!.Visible, "not at start");
        Need(flow.GetNode<Label>("TopLayer/StandInMarker").Visible, "stand-in marker hidden");
        flow._UnhandledInput(Esc());
        Need(!flow.IsMenuOpen, "menu opened at the start screen");

        step("new game goes to the town");
        flow.StartNewGame();
        await Frame();
        Need(flow.IsAtTown && !flow.StartScreen!.Visible && flow.Stage.Visible, "not at town");

        step("esc opens the menu and pauses; party; back; back");
        flow._UnhandledInput(Esc());
        Need(flow.IsMenuOpen, "menu not open");
        flow.OpenParty();
        Need(flow.IsPartyOpen && flow.PartyScreen!.Visible, "party not open");
        flow._UnhandledInput(Esc());
        Need(!flow.IsPartyOpen && flow.IsMenuOpen, "esc did not return to the menu");
        flow.OpenSaves(SaveScreenMode.SaveAndLoad);
        Need(flow.IsSavesOpen, "saves not open");
        flow._UnhandledInput(Esc());
        flow._UnhandledInput(Esc());
        Need(!flow.IsMenuOpen && flow.IsAtTown, "menu not closed");

        step("save, return to start, load");
        var saved = await session.SaveToSlot(1);
        Need(saved.IsOk, "save refused");
        flow.ReturnToStart();
        await Frame();
        Need(flow.IsAtStart && !flow.Stage.Visible, "not back at start");
        var loaded = await session.LoadFromSlot(1);
        Need(loaded.IsOk, "load refused");
        flow.NotifyLoaded(loaded.Value);
        await Frame();
        Need(flow.IsAtTown && flow.Stage.Visible, "load did not return to the town");

        flow.QueueFree();
    }
}
