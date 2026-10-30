using AncientOrder.Rules.Saves;
using AncientOrder.Rules.Session;
using AncientOrder.Rules.State;
using AncientOrder.Rules.Town;
using AncientOrder.Ui;
using Godot;

namespace AncientOrder.Tests;

/// <summary>Saving and loading (ES-10) through the real save and load screen: slot states, the three confirmations, the two modes, the busy state, a mid-conversation load, and ScreenFlow.LoadCompleted. The rules tests carry the save format and reconcile logic.</summary>
public class SaveScreenTests
{
    private const long SeedTime = 1_700_000_000_000;
    private const double SavedX = 12;
    private const double SavedZ = 8;
    private const double MovedX = 15;
    private const double MovedZ = 15;
    private const double Near = 0.05;

    private const string Talk = """
        "conversations": [
          { "id": "talk", "participants": ["ann"], "start": [{ "step": "hello" }],
            "steps": [
              { "id": "hello", "lines": [ { "speaker": "ann", "text": "Hello there" } ], "choices": [ { "id": "go", "text": "Go on", "next": [ { "step": "second" } ] } ] },
              { "id": "second", "lines": [ { "speaker": "ann", "text": "Tell me more" } ], "choices": [ { "id": "leave", "text": "Say goodbye", "next": [ { "step": "end" } ] } ] },
              { "id": "end", "lines": [ { "speaker": "ann", "text": "Farewell" } ] }
            ] }
        ],
        """;

    private static Dictionary<string, string> TalkPack()
    {
        var pack = TestPacks.Town();
        pack["all.json"] = pack["all.json"].Replace("\"towns\": [", Talk + "\n\"towns\": [", StringComparison.Ordinal);
        return pack;
    }

    private static SaveScreen ScreenOf(StartedGame game) => (SaveScreen)Assert.NotNull(game.Flow.SaveScreen, "no save screen");

    private static bool Idle(SaveScreen screen) => !screen.BusyShown && screen.Rows.All(r => !r.Disabled);

    private static List<string> Visible<T>(Node node, Func<T, string> text)
        where T : Control
    {
        var found = new List<string>();
        void Walk(Node n)
        {
            if (n is T { Visible: true } control && control.IsVisibleInTree())
            {
                found.Add(text(control));
            }

            foreach (var child in n.GetChildren())
            {
                Walk(child);
            }
        }

        Walk(node);
        return found;
    }

    private static List<string> Labels(Node node) => Visible<Label>(node, l => l.Text);

    private static GameSession SessionFor(Dictionary<string, string> pack, ISaveBackend saves)
    {
        var report = TestContext.Check(pack);
        return TestContext.NewSession(Assert.NotNull(report.Content, "test pack failed its check"), saves);
    }

    /// <summary>Saves a new game with the player at the saved point into slot 1, and leaves a damaged entry in slot 2.</summary>
    private static async Task Seed(ISaveBackend saves, Action<GameSession>? more = null)
    {
        var session = SessionFor(TalkPack(), saves);
        Assert.True(session.NewGame().IsOk, "seed: new game refused");
        Assert.True(session.MovePlayer(SavedX, SavedZ).IsOk, "seed: move refused");
        more?.Invoke(session);
        Assert.True((await session.SaveToSlot(1)).IsOk, "seed: save refused");
    }

    private static async Task<SaveScreen> OpenSaves(TestContext t, StartedGame game, SaveScreenMode mode)
    {
        var screen = ScreenOf(game);
        game.Flow.OpenSaves(mode);
        await t.WaitUntil(() => game.Flow.IsSavesOpen && Idle(screen), "the slot list");
        await t.Frames(3);
        return screen;
    }

    private static async Task InGame(TestContext t, StartedGame game)
    {
        game.Flow.StartNewGame();
        await t.WaitUntil(() => game.Flow.IsAtTown, "the town");
        game.Flow.OpenMenu();
    }

    private static TownPresence Presence(StartedGame game) => Assert.NotNull(game.Session!.Snapshot.Game!.Town, "no town position");

    [GodotTest]
    public async Task Slot_rows_show_empty_filled_and_damaged_states(TestContext t)
    {
        var saves = new MemorySaveBackend();
        await Seed(saves);
        await saves.WriteAsync(SaveStore.KeyFor(2), [1, 2, 3, 4]);
        var game = await t.StartGame(TalkPack(), saves);

        var screen = await OpenSaves(t, game, SaveScreenMode.LoadOnly);

        Assert.Equal(10, screen.Rows.Count, "row count");
        Assert.Equal(SlotStatus.Saved, screen.Rows[0].Status, "slot 1 status");
        Assert.Equal(SlotStatus.Corrupted, screen.Rows[1].Status, "slot 2 status");
        for (var i = 2; i < 10; i++)
        {
            Assert.Equal(SlotStatus.Empty, screen.Rows[i].Status, $"slot {i + 1} status");
        }

        var filled = Labels(screen.Rows[0]);
        Assert.True(filled.Contains(TestPacks.PlayerName), "filled row lacks the player name");
        Assert.True(filled.Any(l => l.Contains(TestPacks.TownName, StringComparison.Ordinal) && l.Contains("In town", StringComparison.Ordinal)), "filled row lacks the town and situation");
        Assert.True(filled.Contains(SlotRow.SavedWhen(SeedTime)), "filled row lacks the saved time");
        var damaged = Labels(screen.Rows[1]);
        Assert.True(damaged.Contains("Damaged save"), "damaged row not marked");
        Assert.False(damaged.Contains(TestPacks.PlayerName), "damaged row shows a name");
        Assert.True(Labels(screen.Rows[2]).Contains("Empty"), "empty row not marked");
    }

    [GodotTest]
    public async Task A_save_made_in_a_conversation_shows_that_situation(TestContext t)
    {
        var saves = new MemorySaveBackend();
        await Seed(saves, s => Assert.True(s.StartConversation("talk").IsOk, "seed: conversation refused"));
        var game = await t.StartGame(TalkPack(), saves);

        var screen = await OpenSaves(t, game, SaveScreenMode.LoadOnly);

        Assert.True(Labels(screen.Rows[0]).Any(l => l.Contains("In a conversation", StringComparison.Ordinal)), "situation words missing");
    }

    [GodotTest]
    public async Task Load_only_has_no_Save_and_a_damaged_slot_can_only_be_deleted(TestContext t)
    {
        var saves = new MemorySaveBackend();
        await Seed(saves);
        await saves.WriteAsync(SaveStore.KeyFor(2), [9, 9]);
        var game = await t.StartGame(TalkPack(), saves);

        var screen = await OpenSaves(t, game, SaveScreenMode.LoadOnly);

        Assert.False(screen.SaveButton.Visible, "Save offered in load-only mode");
        screen.SelectSlot(2);
        await t.Frames(1);
        Assert.True(screen.LoadButton.Disabled, "Load enabled on a damaged slot");
        Assert.False(screen.DeleteButton.Disabled, "Delete disabled on a damaged slot");
        screen.SelectSlot(5);
        await t.Frames(1);
        Assert.True(screen.LoadButton.Disabled && screen.DeleteButton.Disabled, "actions enabled on an empty slot");
    }

    [GodotTest]
    public async Task Load_only_loads_without_asking(TestContext t)
    {
        var saves = new MemorySaveBackend();
        await Seed(saves);
        var game = await t.StartGame(TalkPack(), saves);
        var screen = await OpenSaves(t, game, SaveScreenMode.LoadOnly);
        screen.SelectSlot(1);
        await t.Frames(1);

        await t.Click(screen.LoadButton);

        Assert.False(screen.IsConfirming, "load-only asked before loading");
        await t.WaitUntil(() => game.Flow.IsAtTown, "the town after loading");
        Assert.False(game.Flow.IsSavesOpen, "save screen still open");
    }

    [GodotTest]
    public async Task Saving_to_an_empty_slot_does_not_ask_and_overwriting_does(TestContext t)
    {
        var saves = new GatedSaveBackend();
        var game = await t.StartGame(TalkPack(), saves);
        await InGame(t, game);
        var screen = await OpenSaves(t, game, SaveScreenMode.SaveAndLoad);
        Assert.True(screen.SaveButton.Visible, "Save missing in the menu's mode");
        screen.SelectSlot(3);
        await t.Frames(1);

        await t.Click(screen.SaveButton);

        Assert.False(screen.IsConfirming, "an empty slot asked to confirm");
        await t.WaitUntil(() => screen.Rows[2].Status == SlotStatus.Saved && Idle(screen), "the save");
        Assert.Equal(1, saves.Writes, "writes after the first save");

        await t.Click(screen.SaveButton);
        Assert.True(screen.IsConfirming, "overwrite did not ask");
        Assert.True(screen.CancelButton.HasFocus(), "Cancel not focused first");
        await t.Click(screen.CancelButton);
        Assert.False(screen.IsConfirming, "Cancel left the panel open");
        await t.Frames(3);
        Assert.Equal(1, saves.Writes, "a cancelled overwrite wrote");

        await t.Click(screen.SaveButton);
        await t.Click(screen.AcceptButton);
        await t.WaitUntil(() => saves.Writes == 2 && Idle(screen), "the overwrite");
        Assert.False(screen.IsConfirming, "panel still open after accepting");
    }

    [GodotTest]
    public async Task Delete_asks_first_and_empties_the_slot_only_when_accepted(TestContext t)
    {
        var saves = new GatedSaveBackend();
        await Seed(saves);
        var game = await t.StartGame(TalkPack(), saves);
        var screen = await OpenSaves(t, game, SaveScreenMode.LoadOnly);
        screen.SelectSlot(1);
        await t.Frames(1);

        await t.Click(screen.DeleteButton);
        Assert.True(screen.IsConfirming, "delete did not ask");
        await t.Click(screen.CancelButton);
        await t.Frames(3);
        Assert.Equal(0, saves.Removes, "a cancelled delete removed");
        Assert.Equal(SlotStatus.Saved, screen.Rows[0].Status, "slot after cancelled delete");

        await t.Click(screen.DeleteButton);
        await t.Click(screen.AcceptButton);
        await t.WaitUntil(() => screen.Rows[0].Status == SlotStatus.Empty && Idle(screen), "the delete");
        Assert.Equal(1, saves.Removes, "removes");
    }

    [GodotTest]
    public async Task Loading_from_the_menu_asks_and_only_the_accepted_load_changes_the_game(TestContext t)
    {
        var saves = new MemorySaveBackend();
        await Seed(saves);
        var game = await t.StartGame(TalkPack(), saves);
        await InGame(t, game);
        Assert.True(game.Session!.MovePlayer(MovedX, MovedZ).IsOk, "move refused");
        var completed = new List<LoadReport>();
        game.Flow.LoadCompleted += completed.Add;
        var screen = await OpenSaves(t, game, SaveScreenMode.SaveAndLoad);
        screen.SelectSlot(1);
        await t.Frames(1);

        await t.Click(screen.LoadButton);
        Assert.True(screen.IsConfirming, "load over progress did not ask");
        await t.Click(screen.CancelButton);
        await t.Frames(3);
        Assert.Equal(0, completed.Count, "a cancelled load completed");
        Assert.Equal(MovedX, Presence(game).Position.X, "position after a cancelled load");

        await t.Click(screen.LoadButton);
        await t.Click(screen.AcceptButton);
        await t.WaitUntil(() => completed.Count == 1, "LoadCompleted");

        Assert.True(Math.Abs(Presence(game).Position.X - SavedX) < Near && Math.Abs(Presence(game).Position.Z - SavedZ) < Near, "position after the load");
        Assert.False(game.Flow.IsSavesOpen, "save screen still open");
        Assert.False(game.Flow.IsMenuOpen, "menu still open");
        Assert.True(completed[0].IsEmpty, "an unchanged load reported adjustments");
    }

    [GodotTest]
    public async Task Buttons_are_disabled_and_a_busy_line_shows_while_the_slot_list_loads(TestContext t)
    {
        var saves = new GatedSaveBackend();
        await Seed(saves);
        var game = await t.StartGame(TalkPack(), saves);
        var screen = ScreenOf(game);
        await t.Frames(3);
        saves.Hold();

        game.Flow.OpenSaves(SaveScreenMode.SaveAndLoad);
        await t.WaitUntil(() => saves.Waiting > 0, "the list to reach the backend");

        Assert.True(screen.BusyShown, "no busy line while listing");
        Assert.True(screen.Rows.All(r => r.Disabled), "rows enabled while busy");
        Assert.True(screen.SaveButton.Disabled && screen.LoadButton.Disabled && screen.DeleteButton.Disabled, "action buttons enabled while busy");

        saves.Release();
        await t.WaitUntil(() => Idle(screen), "the list to finish");
        Assert.Equal(SlotStatus.Saved, screen.Rows[0].Status, "slot 1 after the list");
        Assert.False(screen.LoadButton.Disabled, "Load still disabled after the list");
    }

    [GodotTest]
    public async Task A_running_save_disables_the_screen_until_it_finishes(TestContext t)
    {
        var saves = new GatedSaveBackend();
        var game = await t.StartGame(TalkPack(), saves);
        await InGame(t, game);
        var screen = await OpenSaves(t, game, SaveScreenMode.SaveAndLoad);
        screen.SelectSlot(4);
        await t.Frames(1);
        saves.Hold();

        await t.Click(screen.SaveButton);
        await t.WaitUntil(() => saves.Waiting > 0, "the save to reach the backend");

        Assert.True(screen.BusyShown, "no busy line while saving");
        Assert.True(screen.Rows.All(r => r.Disabled), "rows enabled while saving");
        Assert.True(screen.SaveButton.Disabled && screen.LoadButton.Disabled && screen.DeleteButton.Disabled, "action buttons enabled while saving");
        Assert.Equal(0, saves.Writes, "the write finished while held");

        saves.Release();
        await t.WaitUntil(() => screen.Rows[3].Status == SlotStatus.Saved && Idle(screen), "the save to finish");
        Assert.Equal(1, saves.Writes, "writes");
        Assert.False(screen.SaveButton.Disabled, "Save still disabled after the save");
    }

    [GodotTest]
    public async Task A_running_load_disables_the_screen_and_then_completes(TestContext t)
    {
        var saves = new GatedSaveBackend();
        await Seed(saves);
        var game = await t.StartGame(TalkPack(), saves);
        var screen = await OpenSaves(t, game, SaveScreenMode.LoadOnly);
        screen.SelectSlot(1);
        await t.Frames(1);
        var completed = 0;
        game.Flow.LoadCompleted += _ => completed++;
        saves.Hold();

        await t.Click(screen.LoadButton);
        await t.WaitUntil(() => saves.Waiting > 0, "the load to reach the backend");

        Assert.True(screen.BusyShown && screen.LoadButton.Disabled && screen.Rows.All(r => r.Disabled), "screen enabled while loading");
        Assert.Equal(0, completed, "completed while held");

        saves.Release();
        await t.WaitUntil(() => completed == 1, "LoadCompleted");
        Assert.True(game.Flow.IsAtTown, "not in the town after the load");
    }

    [GodotTest]
    public async Task Loading_a_mid_conversation_save_reopens_the_panel_at_its_step(TestContext t)
    {
        var saves = new MemorySaveBackend();
        await Seed(saves, s =>
        {
            Assert.True(s.StartConversation("talk").IsOk, "seed: conversation refused");
            Assert.True(s.Choose("go").IsOk, "seed: choice refused");
        });
        var game = await t.StartGame(TalkPack(), saves);
        var completed = new List<LoadReport>();
        game.Flow.LoadCompleted += completed.Add;
        var screen = await OpenSaves(t, game, SaveScreenMode.LoadOnly);
        screen.SelectSlot(1);
        await t.Frames(1);

        await t.Click(screen.LoadButton);
        await t.WaitUntil(() => completed.Count == 1, "LoadCompleted");

        var panel = Assert.NotNull(game.Flow.ConversationPanel, "no conversation panel");
        await t.WaitUntil(() => panel.Visible && Visible<Button>(panel, b => b.Text).Any(b => b.Contains("Say goodbye", StringComparison.Ordinal)), "the panel at the saved step");
        Assert.Equal("second", game.Session!.Snapshot.Conversation!.StepId, "session step");
        Assert.True(Labels(panel).Contains("Tell me more"), "the saved step's line is not shown");
        Assert.False(Labels(panel).Contains("Hello there"), "the opening step's line is shown");
        Assert.False(Visible<Button>(panel, b => b.Text).Any(b => b.Contains("Go on", StringComparison.Ordinal)), "the opening step's choice is shown");
        Assert.True(completed[0].IsEmpty, "an unchanged load reported adjustments");
    }

    [GodotTest]
    public async Task Loading_against_changed_content_raises_LoadCompleted_with_the_report_and_does_not_crash(TestContext t)
    {
        var saves = new MemorySaveBackend();
        await Seed(saves, s => Assert.True(s.StartConversation("talk").IsOk, "seed: conversation refused"));
        var game = await t.StartGame(TestPacks.Town(), saves);
        LoadReport? report = null;
        game.Flow.LoadCompleted += r => report = r;
        var screen = await OpenSaves(t, game, SaveScreenMode.LoadOnly);
        screen.SelectSlot(1);
        await t.Frames(1);

        await t.Click(screen.LoadButton);
        await t.WaitUntil(() => report is not null, "LoadCompleted");

        Assert.True(game.Flow.IsAtTown, "not in the town after the load");
        Assert.False(report!.IsEmpty, "no adjustments reported for a removed conversation");
        Assert.True(report.Adjustments.Any(a => a.Kind == LoadAdjustmentKind.ConversationEnded && a.Id == "talk"), "ConversationEnded not reported");
        Assert.Null(game.Session!.Snapshot.Conversation, "the removed conversation is still running");
        Assert.False(game.Flow.ConversationPanel!.Visible, "conversation panel shown for an ended conversation");
    }
}
