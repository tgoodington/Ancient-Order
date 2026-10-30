using AncientOrder.Rules.Personality;
using AncientOrder.Ui;
using Godot;

namespace AncientOrder.Tests;

/// <summary>Locked choices on screen (ES-5): greyed with their in-world line, their number key and Enter do nothing, arrows skip them, no number anywhere in the conversation panel, and the party readout showing a shift.</summary>
public class LockedChoiceTests
{
    private static async Task<(StartedGame Game, ConversationPanel Panel)> InConversation(TestContext t, Dictionary<string, string>? pack = null)
    {
        var game = await t.StartGame(pack ?? LockedChoicePacks.Talk());
        game.Flow.StartNewGame();
        await t.WaitUntil(() => game.Flow.IsAtTown, "the town");
        Assert.True(game.Session!.StartConversation("talk").IsOk, "conversation refused");
        var panel = (ConversationPanel)Assert.NotNull(game.Flow.ConversationPanel, "no conversation panel");
        await t.WaitUntil(() => panel.IsVisibleInTree() && Choices(panel).Count == 3, "the three choices");
        return (game, panel);
    }

    private static List<Button> Buttons(Control panel) => panel.FindChildren("*", "Button", true, false).OfType<Button>().ToList();

    private static List<Button> Choices(Control panel) => Buttons(panel).Where(b => b.Text != "Close" && b.Text != "Continue").ToList();

    private static List<Label> Labels(Control panel) => panel.FindChildren("*", "Label", true, false).OfType<Label>().ToList();

    /// <summary>Every Label's text must be digit-free with no percent sign; so must every button's.</summary>
    private static void AssertNoNumbers(ConversationPanel panel, string when)
    {
        foreach (var label in Labels(panel))
        {
            Assert.NoDigits(label.Text, $"{when}: label {label.Name}");
            Assert.False(label.Text.Contains('%'), $"{when}: percent in label \"{label.Text}\"");
        }

        foreach (var button in Buttons(panel))
        {
            var text = button.Text;
            Assert.NoDigits(text, $"{when}: button \"{text}\"");
            Assert.False(text.Contains('%'), $"{when}: percent in button \"{text}\"");
        }
    }

    [GodotTest]
    public async Task A_locked_choice_is_disabled_and_shows_its_own_or_the_default_line(TestContext t)
    {
        var (_, panel) = await InConversation(t);
        var choices = Choices(panel);

        Assert.False(choices[0].Disabled, "open choice disabled");
        Assert.True(choices[1].Disabled, "locked choice enabled");
        Assert.True(choices[2].Disabled, "default-line locked choice enabled");
        Assert.Contains("Outwit him", choices[1].Text, "locked choice text");
        Assert.Contains(LockedChoicePacks.OwnLockedLine, choices[1].Text, "locked choice line");
        Assert.Contains(LockedChoicePacks.DefaultLockedLine, choices[2].Text, "default locked line");
        Assert.Equal("LockedChoice", choices[1].ThemeTypeVariation.ToString(), "locked choice style");
        Assert.False(choices[0].Text.Contains(LockedChoicePacks.OwnLockedLine, StringComparison.Ordinal), "open choice shows a locked line");
    }

    [GodotTest]
    public async Task The_number_key_of_a_locked_choice_does_nothing(TestContext t)
    {
        var (game, panel) = await InConversation(t);
        var before = game.Session!.Snapshot;

        await t.Press("choice_2");
        await t.Press("choice_3");
        await t.Frames(3);

        var after = game.Session.Snapshot;
        Assert.Equal(before.Version, after.Version, "a locked key changed the game");
        Assert.Equal("bargain", after.Conversation!.StepId, "step changed");
        Assert.Equal(3, Choices(panel).Count, "panel redrawn");
    }

    [GodotTest]
    public async Task Down_and_Up_skip_a_locked_choice_and_Enter_takes_the_highlighted_one(TestContext t)
    {
        var (game, panel) = await InConversation(t, LockedChoicePacks.TalkLockedMiddle());
        var choices = Choices(panel);
        Assert.True(choices[1].Disabled && !choices[0].Disabled && !choices[2].Disabled, "the middle choice must be the only locked one");
        await t.WaitUntil(() => choices[0].HasFocus(), "the first choice to take focus");

        await t.Press("ui_down");
        await t.WaitUntil(() => choices[2].HasFocus(), "Down to skip the locked choice");
        Assert.False(choices[1].HasFocus(), "focus landed on the locked choice");
        await t.Press("ui_up");
        await t.WaitUntil(() => choices[0].HasFocus(), "Up to skip the locked choice");
        await t.Press("ui_down");
        await t.WaitUntil(() => choices[2].HasFocus(), "Down to the third choice");
        await t.Press("ui_accept");

        await t.WaitUntil(() => game.Session!.Snapshot.Conversation is null, "the third choice to end the talk");
        Assert.True(game.Session!.Snapshot.Game!.Memory.HasChosen("talk", "charm"), "the highlighted (third) choice was taken");
    }

    [GodotTest]
    public async Task Enter_on_no_highlight_and_the_locked_rows_key_do_nothing_but_key_3_still_takes_the_third_row(TestContext t)
    {
        var (game, panel) = await InConversation(t, LockedChoicePacks.TalkLockedMiddle());
        var choices = Choices(panel);
        var before = game.Session!.Snapshot.Version;

        choices[1].GrabFocus();
        Assert.False(choices[1].HasFocus(), "a locked choice took focus");
        panel.GetViewport().GuiReleaseFocus();
        await t.Press("ui_accept");
        await t.Press("choice_2");
        await t.Frames(3);
        Assert.Equal(before, game.Session.Snapshot.Version, "Enter or the locked row's key changed the game");

        await t.Press("choice_3");
        await t.WaitUntil(() => game.Session.Snapshot.Conversation is null, "key 3 to take the third row");
        Assert.True(game.Session.Snapshot.Game!.Memory.HasChosen("talk", "charm"), "key 3 did not take the third row");
    }

    [GodotTest]
    public async Task A_pressed_locked_choice_is_refused_and_the_open_key_takes_its_choice(TestContext t)
    {
        var (game, panel) = await InConversation(t);
        var choices = Choices(panel);
        var before = game.Session!.Snapshot.Version;

        choices[1].EmitSignal(BaseButton.SignalName.Pressed);
        await t.Frames(2);
        Assert.Equal(before, game.Session.Snapshot.Version, "a pressed locked choice reached the session");

        await t.Press("choice_1");
        await t.WaitUntil(() => game.Session.Snapshot.Conversation?.StepId == "after", "the next step after the open choice");
    }

    [GodotTest]
    public async Task The_panel_shows_no_number_before_and_after_a_shift(TestContext t)
    {
        var (game, panel) = await InConversation(t);
        AssertNoNumbers(panel, "first step");
        Assert.True(Labels(panel).Count > 0, "no labels found to check");

        await t.Press("choice_1");
        await t.WaitUntil(() => game.Session!.Snapshot.Conversation?.StepId == "after", "the step after the shift");
        await t.Frames(2);

        Assert.True(Labels(panel).Any(l => l.Text.Contains("lowers his price", StringComparison.Ordinal)), "the new step is not shown");
        AssertNoNumbers(panel, "after the shift");
        Assert.Equal(1, Choices(panel).Count, "choices after the shift");
    }

    [GodotTest]
    public async Task The_party_readout_shows_the_shifted_trait_values(TestContext t)
    {
        var (game, panel) = await InConversation(t);
        var before = game.Session!.Snapshot.Game!.Player.Personality;

        await t.Press("choice_1");
        await t.WaitUntil(() => game.Session.Snapshot.Conversation?.StepId == "after", "the step after the shift");
        await t.Press("choice_1");
        await t.WaitUntil(() => Buttons(panel).Any(b => b.Text == "Close"), "the Close button");
        await t.Click(Buttons(panel).First(b => b.Text == "Close"));
        await t.WaitUntil(() => game.Session.Snapshot.Conversation is null, "the conversation to end");

        var after = game.Session.Snapshot.Game!.Player.Personality;
        Assert.Equal(before.Cunning + 8, after.Cunning, "cunning after the shift");
        Assert.Equal(before.Kindness - 8, after.Kindness, "kindness after the shift");

        game.Flow.OpenParty();
        await t.WaitUntil(() => game.Flow.IsPartyOpen, "the party screen");
        var party = (PartyScreen)Assert.NotNull(game.Flow.PartyScreen, "no party screen");
        await t.Frames(2);
        var texts = party.GetNode<VBoxContainer>("%PlayerTraits").FindChildren("*", "Label", true, false).OfType<Label>().Select(l => l.Text).ToList();
        foreach (var trait in Enum.GetValues<Trait>())
        {
            Assert.True(texts.Contains($"{after[trait]:0}%"), $"readout has no \"{after[trait]:0}%\" for {trait} (has {string.Join(", ", texts)})");
        }

        Assert.True(texts.Contains("18%") && texts.Contains("12%"), "readout does not show the shifted values");
        Assert.False(texts.Contains("10%"), "readout still shows the old cunning");
    }
}
