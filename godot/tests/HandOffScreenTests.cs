using AncientOrder.Rules.Saves;
using AncientOrder.Ui;
using Godot;

namespace AncientOrder.Tests;

/// <summary>The hand-off placeholder (ES-9): the screen that replaces the town, its contents, the Esc menu and Return to Start there, a save at the hand-off, and a final outcome over it. The rules tests carry the hand-off logic.</summary>
public class HandOffScreenTests
{
    private static async Task<StartedGame> NewGame(TestContext t, ISaveBackend? saves = null)
    {
        var game = await t.StartGame(HandOffPacks.Town(), saves);
        Assert.True(game.Report.Passed, string.Join("; ", game.Report.Problems.Select(p => p.Path + " " + p.Message)));
        game.Flow.StartNewGame();
        await t.Frames(1);
        Assert.True(game.Flow.IsAtTown, "not in the town");
        return game;
    }

    private static async Task ReachHandOff(TestContext t, StartedGame game, params string[] lineup)
    {
        var session = Assert.NotNull(game.Session, "no session");
        Assert.True(session.SetLineup([.. lineup]).IsOk, "lineup refused");
        Assert.True(session.MovePlayer(HandOffPacks.ExitX, HandOffPacks.ExitZ - 1).IsOk, "move refused");
        var result = session.TriggerSpot("exit");
        Assert.True(result.IsOk, "hand-off refused");
        await t.WaitUntil(() => game.Flow.IsAtHandOff, "the hand-off screen");
    }

    private static List<string> Labels(Node root)
    {
        var found = new List<string>();
        void Walk(Node n)
        {
            if (n is Label label)
            {
                found.Add(label.Text);
            }

            foreach (var child in n.GetChildren())
            {
                Walk(child);
            }
        }

        Walk(root);
        return found;
    }

    private static HandOffScreen ScreenOf(StartedGame game) => (HandOffScreen)Assert.NotNull(game.Flow.HandOffScreen, "no hand-off screen");

    private static Button ReturnButton(HandOffScreen screen) => screen.GetNode<Button>("Center/Column/ReturnToStart");

    private static string FightNameOf(HandOffScreen screen) => screen.GetNode<Label>("Center/Column/Panel/Content/FightName").Text;

    private static string LineupOf(HandOffScreen screen) =>
        string.Join(",", screen.GetNode("Center/Column/Panel/Content/LineupList").GetChildren().OfType<Label>().Select(l => l.Text));

    [GodotTest]
    public async Task The_town_stays_up_and_the_screen_hidden_until_a_hand_off(TestContext t)
    {
        var game = await NewGame(t);

        Assert.False(ScreenOf(game).Visible, "hand-off screen shown in the town");
        Assert.True(game.Flow.Stage.Visible, "town hidden");
        Assert.False(game.Flow.IsAtHandOff, "flow at the hand-off in the town");
    }

    [GodotTest]
    public async Task The_hand_off_replaces_the_town_with_the_fight_name_and_the_lineup_in_order(TestContext t)
    {
        var game = await NewGame(t);

        await ReachHandOff(t, game, "bo", "ann");

        var screen = ScreenOf(game);
        Assert.True(screen.Visible, "hand-off screen hidden");
        Assert.False(game.Flow.Stage.Visible, "town still shown");
        Assert.False(game.Flow.IsAtTown, "flow still at the town");
        Assert.Equal(HandOffPacks.FightName, FightNameOf(screen), "fight name");
        Assert.Equal("Bo,Ann", LineupOf(screen), "lineup in order");
        Assert.True(Labels(screen).Any(l => l.Contains("PLACEHOLDER", StringComparison.Ordinal)), "placeholder heading missing");
        Assert.True(game.Flow.Stage.IsPaused, "town running behind the hand-off");
    }

    [GodotTest]
    public async Task Escape_opens_and_closes_the_menu_over_the_hand_off(TestContext t)
    {
        var game = await NewGame(t);
        await ReachHandOff(t, game, "ann");

        await t.Press("ui_menu");
        Assert.True(game.Flow.IsMenuOpen, "menu not open at the hand-off");
        Assert.True(game.Flow.IsAtHandOff, "left the hand-off");

        await t.Press("ui_menu");
        Assert.False(game.Flow.IsMenuOpen, "menu not closed");
        Assert.True(ScreenOf(game).Visible, "hand-off screen hidden after the menu");
        Assert.False(game.Flow.Stage.Visible, "town shown after the menu");
    }

    [GodotTest]
    public async Task Return_to_Start_shows_the_start_screen_and_hides_the_hand_off(TestContext t)
    {
        var game = await NewGame(t);
        await ReachHandOff(t, game, "ann");

        await t.Click(ReturnButton(ScreenOf(game)));

        Assert.True(game.Flow.IsAtStart, "not at the start screen");
        Assert.False(ScreenOf(game).Visible, "hand-off screen still shown");
        Assert.True(game.Flow.StartScreen!.Visible, "start screen hidden");
        Assert.False(game.Flow.Stage.Visible, "town shown");
    }

    [GodotTest]
    public async Task A_save_at_the_hand_off_loads_back_onto_the_hand_off_screen(TestContext t)
    {
        var game = await NewGame(t, new MemorySaveBackend());
        await ReachHandOff(t, game, "bo", "ann");
        var session = game.Session!;
        Assert.True((await session.SaveToSlot(1)).IsOk, "save refused");
        await t.Click(ReturnButton(ScreenOf(game)));
        Assert.True(game.Flow.IsAtStart, "not at the start screen");

        var loaded = await session.LoadFromSlot(1);
        Assert.True(loaded.IsOk, "load refused");
        game.Flow.NotifyLoaded(loaded.Value);
        await t.Frames(2);

        var screen = ScreenOf(game);
        Assert.True(game.Flow.IsAtHandOff && screen.Visible, "load did not show the hand-off");
        Assert.False(game.Flow.Stage.Visible, "town shown after the load");
        Assert.Equal(HandOffPacks.FightName, FightNameOf(screen), "fight name after load");
        Assert.Equal("Bo,Ann", LineupOf(screen), "lineup after load");
    }

    [GodotTest]
    public async Task A_choice_that_hands_off_shows_its_outcome_over_the_hand_off_and_Close_ends_it_cleanly(TestContext t)
    {
        var game = await NewGame(t);
        var session = game.Session!;
        Assert.True(session.SetLineup(["ann"]).IsOk, "lineup refused");
        Assert.True(session.MovePlayer(HandOffPacks.AnnX, HandOffPacks.AnnZ - 1).IsOk, "move refused");
        Assert.True(session.TriggerPerson("ann").IsOk, "conversation refused");
        var panel = Assert.NotNull(game.Flow.ConversationPanel, "no conversation panel");
        await t.WaitUntil(() => panel.Visible, "the conversation panel");

        var go = panel.FindChildren("*", "Button", true, false).OfType<Button>().First(b => b.Text.Contains("Lead the way", StringComparison.Ordinal));
        await t.Click(go);
        await t.WaitUntil(() => game.Flow.IsAtHandOff, "the hand-off screen");

        Assert.True(game.Flow.ConversationHold, "final outcome not held");
        Assert.True(panel.Visible, "outcome panel hidden over the hand-off");
        Assert.True(ScreenOf(game).Visible, "hand-off screen hidden");
        var close = panel.FindChildren("*", "Button", true, false).OfType<Button>().First(b => b.Text == "Close");

        await t.Click(close);

        Assert.False(game.Flow.ConversationHold, "hold not dropped by Close");
        Assert.False(panel.Visible, "panel still shown after Close");
        Assert.True(game.Flow.IsAtHandOff && ScreenOf(game).Visible, "hand-off screen lost after Close");
        Assert.False(game.Flow.Stage.Visible, "town shown after Close");
        Assert.Equal(HandOffPacks.FightName, FightNameOf(ScreenOf(game)), "fight name");
    }
}
