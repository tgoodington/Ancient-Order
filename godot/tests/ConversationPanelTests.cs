using AncientOrder.Ui;
using Godot;

namespace AncientOrder.Tests;

/// <summary>Conversations on screen (ES-4): the real panel driven through the real flow and presenter. Speakers, unnumbered choices, Continue, number keys, arrows with accept, mouse presses, the closing outcome, and literal text.</summary>
public class ConversationPanelTests
{
    private static Control PanelOf(StartedGame game) => Assert.NotNull(game.Flow.ConversationPanel, "no conversation panel");

    private static IEnumerable<Node> Walk(Node n)
    {
        yield return n;
        foreach (var child in n.GetChildren())
        {
            foreach (var d in Walk(child))
            {
                yield return d;
            }
        }
    }

    private static List<Button> Buttons(Control panel) => Walk(panel).OfType<Button>().ToList();

    private static List<string> Texts(Control panel) =>
        Walk(panel).Select(n => n switch { Label l => l.Text, Button b => b.Text, _ => null }).OfType<string>().ToList();

    private static List<string> SpeakerNames(Control panel) =>
        Walk(panel.GetNode("Panel/Column/Scroll/Content/Transcript")).OfType<Label>().Where(l => l.ThemeTypeVariation == "TitleLabel").Select(l => l.Text).ToList();

    /// <summary>Starts a new game, starts the conversation through the session, and waits until the panel shows its first step.</summary>
    private static async Task<StartedGame> Open(TestContext t, string conversationId)
    {
        var game = await t.StartGame(ConversationPacks.Talks());
        game.Flow.StartNewGame();
        await t.NextFrame();
        Assert.True(game.Session!.StartConversation(conversationId).IsOk, "conversation start refused");
        await t.WaitUntil(() => PanelOf(game).Visible && Buttons(PanelOf(game)).Count > 0, "the panel to show the first step");
        return game;
    }

    private static string StepId(StartedGame game) => game.Session!.Snapshot.Conversation?.StepId ?? "(none)";

    [GodotTest]
    public async Task A_one_on_one_talk_shows_the_speakers_and_leaves_the_narrator_unnamed(TestContext t)
    {
        var game = await Open(t, "chat");
        var panel = PanelOf(game);

        Assert.Equal("Ann|" + TestPacks.PlayerName, string.Join("|", SpeakerNames(panel)), "named speakers in the transcript");
        var texts = Texts(panel);
        Assert.True(texts.Contains("Welcome, traveler."), "Ann's line");
        Assert.True(texts.Contains("Thank you."), "the player's line");
        Assert.True(texts.Contains(ConversationPacks.MarkupText), "the narrator's line");
        Assert.True(!texts.Contains("Narrator") && !texts.Contains("narrator"), "the narrator got a name");
        Assert.True(texts.Contains("A Quiet Word"), "the title");
    }

    [GodotTest]
    public async Task A_narrated_scene_names_only_its_characters(TestContext t)
    {
        var game = await Open(t, "scene");
        var panel = PanelOf(game);

        Assert.Equal("Ann|Bo", string.Join("|", SpeakerNames(panel)), "named speakers in the scene");
        var texts = Texts(panel);
        Assert.True(texts.Contains("Night falls on the square."), "the narration");
        Assert.True(texts.Contains("It is cold.") && texts.Contains("Very."), "the spoken lines");
    }

    [GodotTest]
    public async Task Choices_show_only_their_text_in_order_and_the_first_is_focused(TestContext t)
    {
        var game = await Open(t, "chat");
        var buttons = Buttons(PanelOf(game));

        Assert.Equal("Be kind|Ask a question|Say goodbye", string.Join("|", buttons.Select(b => b.Text)), "choice texts");
        await t.WaitUntil(() => buttons[0].HasFocus(), "the first choice to take focus");
    }

    [GodotTest]
    public async Task No_number_shows_anywhere_and_no_choice_starts_with_a_digit_prefix(TestContext t)
    {
        var game = await Open(t, "chat");

        foreach (var text in Texts(PanelOf(game)))
        {
            Assert.NoDigits(text, "panel text");
        }

        foreach (var button in Buttons(PanelOf(game)))
        {
            Assert.False(button.Text.Length > 0 && char.IsDigit(button.Text[0]), $"choice \"{button.Text}\" starts with a digit");
        }
    }

    [GodotTest]
    public async Task A_step_with_no_choices_shows_a_single_Continue_and_it_plays_the_scene_to_its_end(TestContext t)
    {
        var game = await Open(t, "scene");
        var panel = PanelOf(game);

        var only = Assert.NotNull(Buttons(panel).SingleOrDefault(), "exactly one button");
        Assert.Equal("Continue", only.Text, "the button");
        await t.WaitUntil(() => only.HasFocus(), "Continue to take focus");

        await t.Click(only);
        await t.WaitUntil(() => StepId(game) == "s2", "the second step");
        await t.WaitUntil(() => Texts(panel).Contains("Dawn comes."), "the second step on screen");
        Assert.Equal("Continue", string.Join("|", Buttons(panel).Select(b => b.Text)), "the second step offers Continue too");

        await t.Click(Buttons(panel).Single());
        await t.WaitUntil(() => game.Session!.Snapshot.Conversation is null, "the scene to end");
        await t.WaitUntil(() => !panel.Visible, "the panel to hide");
        Assert.False(game.Flow.ConversationHold, "nothing to hold after a plain Continue");
        Assert.True(game.Flow.IsAtTown, "back at the town");
    }

    [GodotTest]
    public async Task Pressing_a_number_key_takes_that_choice(TestContext t)
    {
        var game = await Open(t, "chat");

        await t.Press("choice_2");

        await t.WaitUntil(() => StepId(game) == "more", "the step after the second choice");
        Assert.True(game.Session!.Snapshot.Game!.Memory.HasChosen("chat", "ask"), "the second choice was recorded");
        Assert.False(game.Session.Snapshot.Game.Memory.HasChosen("chat", "kind"), "the first choice was not taken");
    }

    [GodotTest]
    public async Task A_number_key_with_no_such_choice_does_nothing(TestContext t)
    {
        var game = await Open(t, "chat");
        var version = game.Session!.Snapshot.Version;

        await t.Press("choice_9");
        await t.Frames(3);

        Assert.Equal(version, game.Session.Snapshot.Version, "session version after an out of range key");
        Assert.Equal("hello", StepId(game), "the step");
    }

    [GodotTest]
    public async Task Arrow_keys_move_focus_and_accept_takes_the_focused_choice(TestContext t)
    {
        var game = await Open(t, "chat");
        var buttons = Buttons(PanelOf(game));
        await t.WaitUntil(() => buttons[0].HasFocus(), "the first choice to take focus");

        await t.Press("ui_down");
        await t.WaitUntil(() => buttons[1].HasFocus(), "focus to move to the second choice");
        await t.Press("ui_accept");

        await t.WaitUntil(() => StepId(game) == "more", "the step after the accepted choice");
        Assert.True(game.Session!.Snapshot.Game!.Memory.HasChosen("chat", "ask"), "the focused (second) choice was taken");
    }

    [GodotTest]
    public async Task Up_and_down_wrap_around_the_choices(TestContext t)
    {
        var game = await Open(t, "chat");
        var buttons = Buttons(PanelOf(game));
        await t.WaitUntil(() => buttons[0].HasFocus(), "the first choice to take focus");

        await t.Press("ui_up");
        await t.WaitUntil(() => buttons[2].HasFocus(), "Up from the first choice to wrap to the last");
        await t.Press("ui_down");
        await t.WaitUntil(() => buttons[0].HasFocus(), "Down from the last choice to wrap to the first");
        await t.Press("ui_up");
        await t.Press("ui_up");
        await t.WaitUntil(() => buttons[1].HasFocus(), "two Ups to reach the middle choice");
        await t.Press("ui_accept");

        await t.WaitUntil(() => StepId(game) == "more", "the step after the accepted choice");
        Assert.True(game.Session!.Snapshot.Game!.Memory.HasChosen("chat", "ask"), "the highlighted (second) choice was taken");
    }

    [GodotTest]
    public async Task Hovering_a_choice_moves_the_highlight_but_does_not_take_it(TestContext t)
    {
        var game = await Open(t, "chat");
        var buttons = Buttons(PanelOf(game));
        await t.WaitUntil(() => buttons[0].HasFocus(), "the first choice to take focus");
        var version = game.Session!.Snapshot.Version;

        buttons[2].EmitSignal(Control.SignalName.MouseEntered);
        await t.Frames(3);

        Assert.True(buttons[2].HasFocus(), "hover did not move the highlight");
        Assert.Equal(version, game.Session.Snapshot.Version, "hover changed the game");
        Assert.Equal("hello", StepId(game), "the step");
    }

    [GodotTest]
    public async Task Accept_alone_takes_the_first_choice(TestContext t)
    {
        var game = await Open(t, "chat");
        await t.WaitUntil(() => Buttons(PanelOf(game))[0].HasFocus(), "the first choice to take focus");

        await t.Press("ui_accept");

        await t.WaitUntil(() => StepId(game) == "more", "the step after the accepted choice");
        Assert.True(game.Session!.Snapshot.Game!.Memory.HasChosen("chat", "kind"), "the first choice was taken");
    }

    [GodotTest]
    public async Task A_mouse_press_takes_a_choice(TestContext t)
    {
        var game = await Open(t, "chat");

        await t.Click(Buttons(PanelOf(game))[1]);

        await t.WaitUntil(() => StepId(game) == "more", "the step after the clicked choice");
        Assert.True(game.Session!.Snapshot.Game!.Memory.HasChosen("chat", "ask"), "the clicked choice was recorded");
    }

    [GodotTest]
    public async Task A_conversation_ending_choice_shows_its_outcome_and_closes_on_Close(TestContext t)
    {
        var game = await Open(t, "chat");
        var panel = PanelOf(game);

        await t.Press("choice_3");

        await t.WaitUntil(() => Buttons(panel).Count == 1 && Buttons(panel)[0].Text == "Close", "the Close button");
        Assert.True(game.Session!.Snapshot.Conversation is null, "the conversation has ended in the session");
        Assert.True(game.Flow.ConversationHold, "the outcome is held on screen");
        Assert.True(panel.Visible, "panel hidden while the outcome shows");
        var texts = Texts(panel);
        Assert.True(texts.Contains("Say goodbye"), "the player's choice shown as their line");
        Assert.True(!texts.Contains("Welcome, traveler."), "the finished step is still shown");
        var close = Buttons(panel).Single();
        await t.WaitUntil(() => close.HasFocus(), "Close to take focus");

        await t.Click(close);

        await t.WaitUntil(() => !panel.Visible, "the panel to hide");
        Assert.False(game.Flow.ConversationHold, "the hold is lifted");
        Assert.True(game.Flow.IsAtTown, "back at the town");
    }

    [GodotTest]
    public async Task A_step_taller_than_the_panel_opens_with_what_is_new_at_the_top_not_scrolled_to_the_bottom(TestContext t)
    {
        var game = await Open(t, "long");
        var panel = PanelOf(game);
        await t.Click(Buttons(panel)[0]);
        await t.WaitUntil(() => StepId(game) == "top", "the tall step");
        await t.WaitUntil(() => Texts(panel).Contains("Where do I begin."), "the tall step on screen");
        var scroll = panel.GetNode<ScrollContainer>("Panel/Column/Scroll");
        var echo = Assert.NotNull(Walk(panel).OfType<Label>().FirstOrDefault(l => l.Text == "Go on"), "the echoed choice");
        var buttons = Buttons(panel);
        await t.WaitUntil(() => buttons[0].HasFocus(), "the first choice to take focus");
        await t.Frames(30);

        var visible = scroll.GetGlobalRect();
        Assert.True(scroll.GetVScrollBar().Visible, $"the scrollbar is hidden while the step overflows: max {scroll.GetVScrollBar().MaxValue} page {scroll.GetVScrollBar().Page} size {scroll.GetVScrollBar().Size} scroll {scroll.Size} mode {scroll.VerticalScrollMode}");
        Assert.True(scroll.GetVScrollBar().MaxValue > 100, "the step must overflow the panel for this test");
        Assert.True(echo.GlobalPosition.Y >= visible.Position.Y - 0.5f && echo.GlobalPosition.Y < visible.End.Y, $"the echoed choice is not at the top of the visible area (label {echo.GlobalPosition.Y}, visible {visible}, scroll {scroll.ScrollVertical}/{scroll.GetVScrollBar().MaxValue})");
    }

    [GodotTest]
    public async Task A_line_with_five_choices_fits_without_scrolling(TestContext t)
    {
        var game = await Open(t, "five");
        var panel = PanelOf(game);
        var scroll = panel.GetNode<ScrollContainer>("Panel/Column/Scroll");
        await t.Frames(30);

        var visible = scroll.GetGlobalRect();
        foreach (var button in Buttons(panel))
        {
            var rect = button.GetGlobalRect();
            Assert.True(rect.Position.Y >= visible.Position.Y - 0.5f && rect.End.Y <= visible.End.Y + 0.5f, $"choice \"{button.Text}\" is not fully visible ({rect} in {visible})");
        }

        Assert.Equal(5, Buttons(panel).Count, "choices shown");
        Assert.True(scroll.GetVScrollBar().MaxValue <= scroll.GetVScrollBar().Page + 0.5, "the step needed scrolling");
        Assert.Equal(0, scroll.ScrollVertical, "scroll position");
    }

    [GodotTest]
    public async Task The_panel_never_grows_past_the_cap_and_shows_a_scrollbar_when_over_it(TestContext t)
    {
        var game = await Open(t, "long");
        var panel = PanelOf(game);
        var inner = panel.GetNode<Control>("Panel");
        var scroll = panel.GetNode<ScrollContainer>("Panel/Column/Scroll");
        await t.Click(Buttons(panel)[0]);
        await t.WaitUntil(() => StepId(game) == "top", "the tall step");
        await t.WaitUntil(() => Texts(panel).Contains("Where do I begin."), "the tall step on screen");
        await t.Frames(30);

        Assert.True(inner.Size.Y <= panel.Size.Y * 0.66f + 0.5f, $"panel height {inner.Size.Y} is over the cap of {panel.Size.Y * 0.66f}");
        Assert.True(scroll.GetVScrollBar().Visible, $"no scrollbar while over the cap: vis {scroll.GetVScrollBar().Visible} mode {scroll.VerticalScrollMode} max {scroll.GetVScrollBar().MaxValue} page {scroll.GetVScrollBar().Page} size {scroll.GetVScrollBar().Size} scroll {scroll.Size} inner {inner.Size} content {scroll.GetChild<Control>(0).Size}");
        Assert.True(scroll.GetVScrollBar().Size.X > 1, "the scrollbar has no width");
    }

    [GodotTest]
    public async Task A_one_line_step_gets_a_panel_shorter_than_the_old_fixed_share(TestContext t)
    {
        var game = await Open(t, "scene");
        var panel = PanelOf(game);
        var inner = panel.GetNode<Control>("Panel");
        await t.Frames(30);

        Assert.True(inner.Size.Y < panel.Size.Y * 0.4f - 12f - 1f, $"panel height {inner.Size.Y} is not under the old fixed {panel.Size.Y * 0.4f - 12f}");
    }

    [GodotTest]
    public async Task Markup_like_text_in_content_shows_literally(TestContext t)
    {
        var game = await Open(t, "chat");
        var panel = PanelOf(game);

        var line = Assert.NotNull(Walk(panel).OfType<Label>().FirstOrDefault(l => l.Text == ConversationPacks.MarkupText), "the line with markup, as exact text");
        Assert.True(Walk(panel).OfType<RichTextLabel>().Count() == 0, "a rich text label interprets markup");
        Assert.True(line.IsVisibleInTree(), "the line is visible");
    }
}
