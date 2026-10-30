using AncientOrder.Rules.Saves;
using AncientOrder.Ui;
using Godot;

namespace AncientOrder.Tests;

/// <summary>The Esc menu's Return to Start and Quit (ES-10): both ask first, Cancel and Esc keep the game where it was, and accepting works over the town, a conversation and the hand-off.</summary>
public class GameMenuTests
{
    private static GameMenu MenuOf(StartedGame game) =>
        game.Flow.GetNode("OverlayLayer/OverlayHost").GetChildren().OfType<GameMenu>().Single();

    private static StartScreen StartOf(StartedGame game) => (StartScreen)Assert.NotNull(game.Flow.StartScreen, "no start screen");

    private static async Task<StartedGame> InTown(TestContext t, Dictionary<string, string> pack, ISaveBackend? saves = null)
    {
        var game = await t.StartGame(pack, saves);
        // The previous test's town is still leaving the navigation map; starting before the server settles leaves navigation never ready (see TownTriggerTests).
        for (var i = 0; i < 5; i++)
        {
            await t.Root.ToSignal(t.Root.GetTree(), SceneTree.SignalName.PhysicsFrame);
        }

        game.Flow.StartNewGame();
        await t.WaitUntil(() => game.Flow.IsAtTown, "the town");
        return game;
    }

    private static async Task<GameMenu> OpenMenu(TestContext t, StartedGame game)
    {
        await t.Press("ui_menu");
        await t.WaitUntil(() => game.Flow.IsMenuOpen, "the menu to open");
        return MenuOf(game);
    }

    private static async Task ReachHandOff(TestContext t, StartedGame game)
    {
        var session = Assert.NotNull(game.Session, "no session");
        Assert.True(session.SetLineup(["ann"]).IsOk, "lineup refused");
        Assert.True(session.MovePlayer(HandOffPacks.ExitX, HandOffPacks.ExitZ - 1).IsOk, "move refused");
        Assert.True(session.TriggerSpot("exit").IsOk, "hand-off refused");
        await t.WaitUntil(() => game.Flow.IsAtHandOff, "the hand-off screen");
    }

    private static async Task AssertReturnedToStart(TestContext t, StartedGame game)
    {
        var start = StartOf(game);
        Assert.True(game.Flow.IsAtStart, "not at the start screen");
        Assert.False(game.Flow.IsMenuOpen, "menu still open");
        Assert.True(start.Visible, "start screen hidden");
        Assert.False(MenuOf(game).Visible, "menu still shown");
        Assert.False(game.Flow.ConversationPanel!.Visible, "conversation panel still shown");
        Assert.False(game.Flow.ConversationHold, "conversation hold kept");
        Assert.False(game.Flow.HandOffScreen!.Visible, "hand-off screen still shown");
        await t.WaitUntil(() => start.LoadEnabled, "Load to be enabled with a save on disk");
    }

    [GodotTest]
    public async Task The_menu_lists_Return_to_Start_and_Quit_after_the_existing_buttons(TestContext t)
    {
        var game = await InTown(t, TestPacks.Town());

        var menu = await OpenMenu(t, game);

        var texts = menu.GetNode("Center/Panel/Buttons").GetChildren().OfType<Button>().Select(b => b.Text);
        Assert.Equal("Resume|Party|Save and Load|Return to Start|Quit", string.Join("|", texts), "menu buttons");
        Assert.False(menu.IsConfirming, "a confirm shown before any press");
    }

    [GodotTest]
    public async Task Cancel_and_Esc_dismiss_the_confirm_and_leave_the_menu_open(TestContext t)
    {
        var game = await InTown(t, TestPacks.Town());
        var quits = 0;
        game.Flow.QuitRequested += () => quits++;
        var menu = await OpenMenu(t, game);

        await t.Click(menu.ReturnToStartButton);
        Assert.True(menu.IsConfirming, "no confirm for Return to Start");
        Assert.Equal("Return to the start screen? Unsaved progress will be lost.", menu.ConfirmText, "Return to Start wording");
        Assert.Equal("Return to Start", menu.AcceptButton.Text, "accept button");
        await t.WaitUntil(() => menu.CancelButton.HasFocus(), "Cancel to take focus");
        await t.Click(menu.CancelButton);
        Assert.False(menu.IsConfirming, "confirm still shown after Cancel");
        Assert.True(game.Flow.IsMenuOpen && game.Flow.IsAtTown, "Cancel did not leave the game where it was");

        await t.Click(menu.QuitButton);
        Assert.True(menu.IsConfirming, "no confirm for Quit");
        Assert.Equal("Quit the game? Unsaved progress will be lost.", menu.ConfirmText, "Quit wording");
        Assert.Equal("Quit", menu.AcceptButton.Text, "accept button");
        await t.Press("ui_menu");
        await t.WaitUntil(() => !menu.IsConfirming, "Esc to dismiss the confirm");
        Assert.False(menu.IsConfirming, "confirm still shown after Esc");
        Assert.True(game.Flow.IsMenuOpen && game.Flow.IsAtTown, "Esc closed the menu as well");
        Assert.Equal(0, quits, "quit fired without being accepted");

        await t.Press("ui_menu");
        await t.WaitUntil(() => !game.Flow.IsMenuOpen, "a second Esc to close the menu");
    }

    [GodotTest]
    public async Task Accepting_Quit_fires_the_quit_hook_from_the_town_a_conversation_and_the_hand_off(TestContext t)
    {
        var town = await InTown(t, ConversationPacks.Talks());
        var quits = 0;
        town.Flow.QuitRequested += () => quits++;
        var menu = await OpenMenu(t, town);
        await t.Click(menu.QuitButton);
        await t.Click(menu.AcceptButton);
        Assert.Equal(1, quits, "quit from the town");

        await t.Press("ui_menu");
        await t.WaitUntil(() => !town.Flow.IsMenuOpen, "the menu to close");
        Assert.True(town.Session!.StartConversation("chat").IsOk, "conversation refused");
        await t.WaitUntil(() => town.Flow.ConversationPanel!.Visible, "the conversation panel");
        await OpenMenu(t, town);
        await t.Click(menu.QuitButton);
        await t.Click(menu.AcceptButton);
        Assert.Equal(2, quits, "quit from a conversation");

        var fight = await InTown(t, HandOffPacks.Town());
        fight.Flow.QuitRequested += () => quits++;
        await ReachHandOff(t, fight);
        var fightMenu = await OpenMenu(t, fight);
        await t.Click(fightMenu.QuitButton);
        await t.Click(fightMenu.AcceptButton);
        Assert.Equal(3, quits, "quit from the hand-off");
    }

    [GodotTest]
    public async Task Return_to_Start_from_the_town_closes_the_menu_and_a_new_game_can_walk(TestContext t)
    {
        var game = await InTown(t, TestPacks.Town(), new MemorySaveBackend());
        Assert.True((await game.Session!.SaveToSlot(1)).IsOk, "save refused");
        var menu = await OpenMenu(t, game);

        await t.Click(menu.ReturnToStartButton);
        await t.Click(menu.AcceptButton);

        await AssertReturnedToStart(t, game);
        await t.Click(StartOf(game).GetNode<Button>("Center/Column/NewGame"));
        Assert.True(game.Flow.IsAtTown && !game.Flow.IsMenuOpen, "new game did not show the town");
        await t.WaitUntil(() => game.Flow.Stage.IsNavigationReady, "navigation to become ready");
        Assert.True(game.Flow.Stage.WalkTo(new Vector3(5, 0, 4)), "a click-to-walk was refused after the fresh town");
        Assert.True(game.Flow.Stage.IsWalking, "no walk started");
    }

    [GodotTest]
    public async Task Return_to_Start_during_a_conversation_drops_it_from_the_screen(TestContext t)
    {
        var game = await InTown(t, ConversationPacks.Talks(), new MemorySaveBackend());
        Assert.True((await game.Session!.SaveToSlot(1)).IsOk, "save refused");
        Assert.True(game.Session.StartConversation("chat").IsOk, "conversation refused");
        await t.WaitUntil(() => game.Flow.ConversationPanel!.Visible, "the conversation panel");
        var menu = await OpenMenu(t, game);

        await t.Click(menu.ReturnToStartButton);
        await t.Click(menu.AcceptButton);

        await AssertReturnedToStart(t, game);
    }

    [GodotTest]
    public async Task Return_to_Start_over_the_hand_off_shows_the_start_screen(TestContext t)
    {
        var game = await InTown(t, HandOffPacks.Town(), new MemorySaveBackend());
        Assert.True((await game.Session!.SaveToSlot(1)).IsOk, "save refused");
        await ReachHandOff(t, game);
        var menu = await OpenMenu(t, game);

        await t.Click(menu.ReturnToStartButton);
        await t.Click(menu.AcceptButton);

        await AssertReturnedToStart(t, game);
    }
}
