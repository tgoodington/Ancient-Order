using AncientOrder.Rules.Content.Check;
using AncientOrder.Rules.Saves;
using AncientOrder.Rules.Session;
using AncientOrder.Ui;
using Godot;

namespace AncientOrder;

/// <summary>
/// Headless check of the real save and load screen (T20) inside the real flow on the stand-in pack: ten rows, load-only hides Save, busy disables, save to a slot with
/// the overwrite confirmation, Cancel and Esc, load with its confirmation, a damaged slot shown and deleted, and the inline error line. Saves go under user://smoke/savescreen/saves.
/// Exits 0 on success, or 1 after printing the failing step. Pass "--shot=path" after "--" to write a screenshot of the open screen (needs a window).
/// </summary>
public partial class SaveScreenCheck : Node
{
    private const string SaveFolder = "user://smoke/savescreen/saves";

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
                GD.Print("savescreen: " + s);
            });
            Empty(folder);
            GD.Print("SAVESCREEN PASSED");
            GetTree().Quit(0);
        }
        catch (Exception ex)
        {
            Empty(folder);
            GD.PrintErr($"SAVESCREEN FAILED at step '{step}': {ex.Message}");
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

    private static void Press(BaseButton button) => button.EmitSignal(BaseButton.SignalName.Pressed);

    private static HashSet<string> Labels(Node node)
    {
        var found = new HashSet<string>();
        void Walk(Node n)
        {
            if (n is Label { Visible: true } label && label.IsVisibleInTree())
            {
                found.Add(label.Text);
            }

            foreach (var child in n.GetChildren())
            {
                Walk(child);
            }
        }

        Walk(node);
        return found;
    }

    private async Task Frame() => await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

    private async Task Until(Func<bool> done, string what)
    {
        for (var i = 0; i < 600 && !done(); i++)
        {
            await Frame();
        }

        Need(done(), "timed out waiting for " + what);
    }

    private async Task Run(string folder, Action<string> step)
    {
        var report = ContentCheck.Run(new GodotContentSource("res://content/standin"));
        var content = report.Content ?? throw new InvalidOperationException("stand-in pack failed");
        var session = new GameSession(content, new GameSessionOptions { Saves = new SaveStore(new FileSaveBackend(folder), content) });
        var flow = GD.Load<PackedScene>("res://main.tscn").Instantiate<ScreenFlow>();
        flow.AutoStart = false;
        AddChild(flow);
        flow.Start(content, session, report);
        await Frame();
        var screen = flow.SaveScreen as SaveScreen ?? throw new InvalidOperationException("save screen is not the real scene");
        bool Idle() => !screen.BusyShown && screen.Rows.All(r => !r.Disabled);

        step("load-only from the start screen: ten empty rows, no Save, busy disables while listing");
        flow.OpenSaves(SaveScreenMode.LoadOnly);
        Need(screen.BusyShown && screen.Rows.All(r => r.Disabled) && screen.LoadButton.Disabled, "not disabled while busy");
        await Until(Idle, "first list");
        Need(screen.Rows.Count == 10 && screen.Rows.All(r => r.Status == SlotStatus.Empty), "not ten empty rows");
        Need(!screen.SaveButton.Visible, "Save visible in load-only");
        Need(screen.LoadButton.Disabled && screen.DeleteButton.Disabled, "actions enabled with only empty slots");
        Need(Labels(screen).Contains("Empty") && Labels(screen).Contains("Load Game"), "empty marker or title missing");
        flow.Back();

        step("a new game; save to an empty slot needs no confirmation");
        flow.StartNewGame();
        await Frame();
        flow.OpenMenu();
        flow.OpenSaves(SaveScreenMode.SaveAndLoad);
        await Until(Idle, "list");
        Need(screen.SaveButton.Visible && !screen.SaveButton.Disabled, "Save missing in menu mode");
        screen.SelectSlot(3);
        Press(screen.SaveButton);
        Need(!screen.IsConfirming, "empty slot asked to confirm");
        await Until(() => screen.Rows[2].Status == SlotStatus.Saved && Idle(), "save");
        var first = (await session.ListSlots()).Value[2].Metadata!;
        var text = Labels(screen);
        Need(text.Any(t => t.Contains("In town") || t.Contains("In a conversation")), "situation words missing");
        Need(!string.IsNullOrEmpty(first.PlayerName) && text.Contains(first.PlayerName), "player name missing");
        if (!string.IsNullOrEmpty(first.TownName))
        {
            Need(text.Any(t => t.Contains(first.TownName)), "town missing");
        }

        Need(text.Contains(SlotRow.SavedWhen(first.SavedAt)), "saved time missing");

        step("overwrite asks first; Cancel and Esc do nothing");
        await Task.Delay(30);
        Press(screen.SaveButton);
        Need(screen.IsConfirming, "overwrite not confirmed");
        Need(screen.CancelButton.HasFocus(), "Cancel not focused first");
        Press(screen.CancelButton);
        Need(!screen.IsConfirming, "Cancel left the panel open");
        Press(screen.SaveButton);
        screen._UnhandledInput(new InputEventAction { Action = "ui_menu", Pressed = true });
        Need(!screen.IsConfirming && flow.IsSavesOpen, "Esc did not just cancel the confirmation");
        Need((await session.ListSlots()).Value[2].Metadata!.SavedAt == first.SavedAt, "cancelled overwrite saved anyway");

        step("a re-render while busy closes an open confirmation and raises no request");
        var raised = 0;
        screen.SaveRequested += _ => raised++;
        Press(screen.SaveButton);
        Need(screen.IsConfirming, "overwrite not confirmed before re-render");
        screen.Render((await session.ListSlots()).Value, SaveScreenMode.SaveAndLoad, true, null);
        Need(!screen.IsConfirming, "busy re-render left the confirmation open");
        Press(screen.AcceptButton);
        Need(raised == 0, "a stale accept raised a request");
        screen.Render((await session.ListSlots()).Value, SaveScreenMode.SaveAndLoad, false, null);

        step("overwrite accepted saves again");
        Press(screen.SaveButton);
        Press(screen.AcceptButton);
        Need(screen.BusyShown, "not busy after accepting");
        await Until(Idle, "overwrite");
        Need((await session.ListSlots()).Value[2].Metadata!.SavedAt > first.SavedAt, "overwrite did not save");

        step("a refusal reason shows inline");
        screen.Render((await session.ListSlots()).Value, SaveScreenMode.SaveAndLoad, false, "Nothing happened because of a test.");
        Need(screen.ErrorShown.Contains("test"), "error not shown");
        screen.Render((await session.ListSlots()).Value, SaveScreenMode.SaveAndLoad, false, null);
        Need(screen.ErrorShown.Length == 0, "error not cleared");

        step("a damaged slot is shown as damaged and can only be deleted");
        File.WriteAllText(System.IO.Path.Combine(folder, SaveStore.KeyFor(4) + ".json"), "{ not a save");
        flow.Back();
        flow.OpenSaves(SaveScreenMode.SaveAndLoad);
        await Until(Idle, "relist");
        Need(screen.Rows[3].Status == SlotStatus.Corrupted && Labels(screen).Contains("Damaged save"), "damaged slot not shown");
        screen.SelectSlot(4);
        Need(screen.LoadButton.Disabled && !screen.DeleteButton.Disabled, "damaged slot actions wrong");
        Press(screen.DeleteButton);
        Need(screen.IsConfirming, "delete not confirmed");
        Press(screen.AcceptButton);
        await Until(() => screen.Rows[3].Status == SlotStatus.Empty && Idle(), "delete");

        step("load in the menu asks first, then closes the screens");
        screen.SelectSlot(3);
        await Shot(flow);
        Press(screen.LoadButton);
        Need(screen.IsConfirming, "load over progress not confirmed");
        Press(screen.AcceptButton);
        await Until(() => !flow.IsSavesOpen, "load");
        Need(!flow.IsMenuOpen, "menu still open after load");

        step("load-only loads straight away");
        flow.ReturnToStart();
        await Frame();
        flow.OpenSaves(SaveScreenMode.LoadOnly);
        await Until(Idle, "list");
        Need(screen.SelectedSlot == 3 && !screen.SaveButton.Visible, "default selection or Save visibility wrong");
        Press(screen.LoadButton);
        Need(!screen.IsConfirming, "load-only asked to confirm");
        await Until(() => !flow.IsSavesOpen, "load-only load");
        Need(flow.IsAtTown, "load did not go to the town");

        flow.QueueFree();
    }

    private async Task Shot(ScreenFlow flow)
    {
        var path = OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--shot="))?["--shot=".Length..];
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathRooted(path) || DisplayServer.GetName() == "headless")
        {
            return;
        }

        await Frame();
        await Frame();
        flow.GetViewport().GetTexture().GetImage().SavePng(path);
    }
}
