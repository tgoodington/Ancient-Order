using AncientOrder.Rules.Saves;
using AncientOrder.Ui;
using Godot;

namespace AncientOrder.Tests;

/// <summary>Starting a game (ES-1): the start screen, New Game, the Load button's states, loading a save, and the content error screen. Wiring and presentation only; the rules tests carry the logic.</summary>
public class StartScreenTests
{
    private const double SavedX = 12;
    private const double SavedZ = 8;
    private const double Near = 0.05;

    private static Button NewGameButton(StartScreen start) => start.GetNode<Button>("Center/Column/NewGame");

    private static Button LoadButton(StartScreen start) => start.GetNode<Button>("Center/Column/LoadRow/Load");

    private static StartScreen StartOf(StartedGame game) => (StartScreen)Assert.NotNull(game.Flow.StartScreen, "no start screen");

    /// <summary>Saves a game with the player standing at the saved point into slot 1 of the backend.</summary>
    private static async Task SeedSlot(ISaveBackend backend)
    {
        var report = TestContext.Check(TestPacks.Town());
        var session = TestContext.NewSession(Assert.NotNull(report.Content, "test pack failed its check"), backend);
        Assert.True(session.NewGame().IsOk, "seed: new game refused");
        Assert.True(session.MovePlayer(SavedX, SavedZ).IsOk, "seed: move refused");
        Assert.True((await session.SaveToSlot(1)).IsOk, "seed: save refused");
    }

    [GodotTest]
    public async Task The_start_screen_shows_at_launch(TestContext t)
    {
        var game = await t.StartGame(TestPacks.Town());

        Assert.True(game.Flow.IsAtStart, "flow is not at the start");
        Assert.True(StartOf(game).Visible, "start screen hidden");
        Assert.False(game.Flow.IsContentErrorShown, "content error shown for a passing pack");
        Assert.False(game.Flow.Stage.Visible, "town shown behind the start screen");
        var title = StartOf(game).GetNode<Label>("Center/Column/Title").Text;
        Assert.Equal(ProjectSettings.GetSetting("application/config/name").AsString(), title, "title is the project name");
        Assert.NoDigits(title, "title");
    }

    [GodotTest]
    public async Task Escape_at_the_start_screen_opens_no_menu(TestContext t)
    {
        var game = await t.StartGame(TestPacks.Town());

        await t.Press("ui_menu");

        Assert.False(game.Flow.IsMenuOpen, "menu opened at the start screen");
        Assert.True(game.Flow.IsAtStart, "left the start screen");
    }

    [GodotTest]
    public async Task New_Game_shows_the_town_with_the_avatar_at_the_start_point(TestContext t)
    {
        var game = await t.StartGame(TestPacks.Town());

        await t.Click(NewGameButton(StartOf(game)));

        Assert.True(game.Flow.IsAtTown, "flow is not at the town");
        Assert.False(StartOf(game).Visible, "start screen still shown");
        Assert.True(game.Flow.Stage.Visible, "town hidden");
        var state = Assert.NotNull(game.Session!.Snapshot.Game, "no game started");
        Assert.Equal(TestPacks.PlayerName, state.Player.Name, "player name");
        var avatar = game.Flow.Stage.PlayerPosition;
        Assert.True(Math.Abs(avatar.X - TestPacks.StartX) < Near && Math.Abs(avatar.Z - TestPacks.StartZ) < Near, $"avatar at {avatar}, not at the start point");
    }

    [GodotTest]
    public async Task With_no_saves_Load_is_disabled_and_says_so(TestContext t)
    {
        var game = await t.StartGame(TestPacks.Town());
        var start = StartOf(game);

        await t.WaitUntil(() => start.NoSavesShown, "the No saves yet note");

        Assert.False(start.LoadEnabled, "Load enabled with no saves");
        Assert.False(LoadButton(start).IsVisibleInTree() && !LoadButton(start).Disabled, "Load button can be pressed");
        var note = start.GetNode<Label>("Center/Column/LoadRow/NoSaves");
        Assert.Equal("No saves yet", note.Text, "note text");
    }

    [GodotTest]
    public async Task With_a_saved_slot_Load_is_enabled_without_the_note(TestContext t)
    {
        var saves = new MemorySaveBackend();
        await SeedSlot(saves);
        var game = await t.StartGame(TestPacks.Town(), saves);
        var start = StartOf(game);

        await t.WaitUntil(() => start.LoadEnabled, "Load to be enabled");

        Assert.False(start.NoSavesShown, "No saves yet shown with a saved slot");
    }

    [GodotTest]
    public async Task With_only_a_damaged_slot_Load_is_enabled_without_the_note(TestContext t)
    {
        var saves = new MemorySaveBackend();
        await saves.WriteAsync(SaveStore.KeyFor(2), [1, 2, 3, 4]);
        var game = await t.StartGame(TestPacks.Town(), saves);
        var start = StartOf(game);

        await t.WaitUntil(() => start.LoadEnabled, "Load to be enabled");

        Assert.False(start.NoSavesShown, "No saves yet shown with a damaged slot");
    }

    [GodotTest]
    public async Task When_the_slot_list_cannot_be_read_Load_stays_enabled_without_the_note(TestContext t)
    {
        var game = await t.StartGame(TestPacks.Town(), new FailingSaveBackend());
        var start = StartOf(game);

        await t.WaitUntil(() => start.LoadEnabled, "Load to be enabled");
        await t.Frames(3);

        Assert.True(start.LoadEnabled, "Load disabled after a failed list");
        Assert.False(start.NoSavesShown, "No saves yet shown after a failed list");
    }

    [GodotTest]
    public async Task Load_opens_the_save_screen_load_only_and_loading_shows_the_town_at_the_saved_position(TestContext t)
    {
        var saves = new MemorySaveBackend();
        await SeedSlot(saves);
        var game = await t.StartGame(TestPacks.Town(), saves);
        var start = StartOf(game);
        await t.WaitUntil(() => start.LoadEnabled, "Load to be enabled");

        await t.Click(LoadButton(start));

        Assert.True(game.Flow.IsSavesOpen, "save screen not open");
        Assert.Equal(SaveScreenMode.LoadOnly, game.Flow.SaveMode, "save screen mode");
        var screen = (SaveScreen)Assert.NotNull(game.Flow.SaveScreen, "no save screen");
        Assert.True(screen.Visible, "save screen hidden");
        Assert.False(screen.SaveButton.Visible, "Save offered in load-only mode");
        await t.WaitUntil(() => screen.Rows[0].Status == SlotStatus.Saved, "slot 1 to show as saved");
        screen.SelectSlot(1);
        await t.Frames(1);

        await t.Click(screen.LoadButton);
        await t.WaitUntil(() => game.Flow.IsAtTown, "the town after loading");
        await t.WaitUntil(
            () => Math.Abs(game.Flow.Stage.PlayerPosition.X - SavedX) < Near && Math.Abs(game.Flow.Stage.PlayerPosition.Z - SavedZ) < Near,
            "the avatar at the saved position");

        Assert.False(StartOf(game).Visible, "start screen still shown");
        Assert.False(game.Flow.IsSavesOpen, "save screen still open");
        Assert.True(game.Flow.Stage.Visible, "town hidden");
    }

    [GodotTest]
    public async Task A_pack_with_problems_shows_the_content_error_screen_instead_of_the_start_screen(TestContext t)
    {
        var game = await t.StartGame(TestPacks.UnknownStartingTown());

        Assert.False(game.Report.Passed, "pack passed its check");
        Assert.Null(game.Report.Content, "a failed pack gave content");
        Assert.True(game.Flow.IsContentErrorShown, "content error screen not shown");
        Assert.Null(game.Flow.StartScreen, "start screen built for a failed pack");
        Assert.True(game.Report.Problems.Any(p => p.Path.Contains("startingTown", StringComparison.Ordinal)), "problem does not name startingTown");
    }
}
