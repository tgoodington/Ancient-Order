using AncientOrder.Rules.Reactions;
using AncientOrder.Stage;
using AncientOrder.Ui;
using Godot;

namespace AncientOrder.Tests;

/// <summary>Archetype reactions (ES-6) on screen: the body-language lines in the panel, tells and demeanor on the person markers in the town, and no numbers anywhere in the panel.</summary>
public class ReactionTellTests
{
    private static PersonMarker MarkerOf(StartedGame game, string id)
    {
        var person = Assert.NotNull(game.Flow.Stage.GetNodeOrNull<Node3D>("People/Person_" + id), "no person node for " + id);
        return Assert.NotNull(Find<PersonMarker>(person), "no marker on " + id);
    }

    private static T? Find<T>(Node node) where T : Node
    {
        foreach (var child in node.GetChildren())
        {
            if (child is T found)
            {
                return found;
            }

            if (Find<T>(child) is { } deeper)
            {
                return deeper;
            }
        }

        return null;
    }

    private static void Collect<T>(Node node, List<T> into) where T : Node
    {
        foreach (var child in node.GetChildren())
        {
            if (child is T found)
            {
                into.Add(found);
            }

            Collect(child, into);
        }
    }

    private static List<Label> Labels(StartedGame game)
    {
        var labels = new List<Label>();
        Collect(Assert.NotNull(game.Flow.ConversationPanel, "no panel"), labels);
        return labels;
    }

    private static string PanelText(StartedGame game) => string.Join("\n", Labels(game).Select(l => l.Text));

    private static List<string> TellLines(StartedGame game) => Labels(game).Where(l => l.ThemeTypeVariation == "TellLine").Select(l => l.Text).ToList();

    private static (TellDirection Direction, TellStrength Strength) TellOf(StartedGame game, string id)
    {
        var tell = MarkerOf(game, id).LastTell;
        Assert.True(tell is not null, "no tell on " + id);
        return tell!.Value;
    }

    private static async Task<StartedGame> InConversation(TestContext t)
    {
        var game = await t.StartGame(ReactionPacks.Town());
        game.Flow.StartNewGame();
        await t.WaitUntil(() => game.Flow.IsAtTown, "the town");
        await t.WaitUntil(() => game.Flow.Stage.GetNodeOrNull("People/Person_ed") is not null, "people in the town");
        Assert.True(game.Session!.StartConversation("chat").IsOk, "conversation refused");
        await t.WaitUntil(() => game.Flow.ConversationPanel!.Visible, "the panel");
        return game;
    }

    private static async Task Choose(TestContext t, StartedGame game, string text)
    {
        var buttons = new List<Button>();
        Collect(game.Flow.ConversationPanel!, buttons);
        await t.Click(Assert.NotNull(buttons.FirstOrDefault(b => b.Text.EndsWith(text, StringComparison.Ordinal)), "no choice " + text));
        await t.WaitUntil(() => game.Flow.ConversationHold, "the outcome");
    }

    [GodotTest]
    public async Task Each_person_present_carries_their_demeanor(TestContext t)
    {
        var game = await t.StartGame(ReactionPacks.Town());
        game.Flow.StartNewGame();
        await t.WaitUntil(() => game.Flow.IsAtTown, "the town");
        await t.WaitUntil(() => game.Flow.Stage.GetNodeOrNull("People/Person_ed") is not null, "people in the town");

        Assert.Equal(Demeanor.Warm, MarkerOf(game, "ann").Demeanor, "ann");
        Assert.Equal(Demeanor.Cold, MarkerOf(game, "bo").Demeanor, "bo");
        Assert.Equal(Demeanor.Wary, MarkerOf(game, "cy").Demeanor, "cy");
        Assert.Equal(Demeanor.Neutral, MarkerOf(game, "ed").Demeanor, "ed");
        Assert.True(MarkerOf(game, "ann").LastTell is null, "a tell before any reaction");
    }

    [GodotTest]
    public async Task A_kind_choice_shows_each_archetypes_line_and_tells_each_marker(TestContext t)
    {
        var game = await InConversation(t);
        await Choose(t, game, "Be kind");

        Assert.Equal(string.Join("|", new[] { "Ann smiles.", "Bo sneers.", "Di smiles." }), string.Join("|", TellLines(game)), "body-language lines in the panel (di reacts too, though not in town)");
        Assert.Equal((TellDirection.Warmer, TellStrength.Clear), TellOf(game, "ann"), "ann's tell");
        Assert.Equal((TellDirection.Cooler, TellStrength.Clear), TellOf(game, "bo"), "bo's tell");
        Assert.True(MarkerOf(game, "cy").LastTell is null, "cy has no reaction so no tell");
        Assert.True(MarkerOf(game, "ed").LastTell is null, "ed is not a participant so no tell");
        Assert.True(game.Flow.Stage.GetNodeOrNull("People/Person_di") is null, "di is not in the town");
    }

    [GodotTest]
    public async Task A_bigger_choice_makes_a_stronger_tell_and_line(TestContext t)
    {
        var game = await InConversation(t);
        await Choose(t, game, "Be very kind");

        Assert.Equal(string.Join("|", new[] { "Ann beams.", "Bo recoils.", "Di beams." }), string.Join("|", TellLines(game)), "strong lines");
        Assert.Equal(TellStrength.Strong, TellOf(game, "ann").Strength, "ann strength");
        Assert.Equal(TellStrength.Strong, TellOf(game, "bo").Strength, "bo strength");
    }

    [GodotTest]
    public async Task A_small_choice_makes_a_slight_tell(TestContext t)
    {
        var game = await InConversation(t);
        await Choose(t, game, "Be a little kind");

        Assert.Equal(string.Join("|", new[] { "Ann nods.", "Bo sneers.", "Di nods." }), string.Join("|", TellLines(game)), "slight line, falling back to clear when none is written");
        Assert.Equal((TellDirection.Warmer, TellStrength.Slight), TellOf(game, "ann"), "ann");
    }

    [GodotTest]
    public async Task A_written_reaction_replaces_the_archetype_and_silence_removes_it(TestContext t)
    {
        var game = await InConversation(t);
        await Choose(t, game, "Say nothing");

        Assert.Equal(string.Join("|", new[] { "Cy watches you." }), string.Join("|", TellLines(game)), "only the written line");
        Assert.Equal((TellDirection.Neutral, TellStrength.Slight), TellOf(game, "cy"), "a written line with no change leans neither way");
        Assert.True(MarkerOf(game, "ann").LastTell is null, "a silenced reaction plays no tell");
    }

    [GodotTest]
    public async Task The_panel_never_shows_relationship_numbers(TestContext t)
    {
        var game = await InConversation(t);
        Assert.NoDigits(PanelText(game), "step labels");
        await Choose(t, game, "Be very kind");

        Assert.True(TellLines(game).Count > 0, "no reaction shown");
        Assert.NoDigits(PanelText(game), "outcome labels");
    }
}
