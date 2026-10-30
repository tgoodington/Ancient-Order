using System.Collections.Immutable;
using AncientOrder.Rules.Conversations;
using AncientOrder.Rules.Reactions;
using AncientOrder.Ui;
using Godot;

namespace AncientOrder;

/// <summary>
/// Headless check of the real conversation panel (T17) with a hand-built stand-in conversation: speakers and narration, choices without number prefixes, a locked choice shown with its line and never raised,
/// number-key selection, a staged outcome shown above the next step, Continue, and a final outcome with a focused Close. Run: godot --headless --path godot res://smoke/panelcheck.tscn.
/// Exits 0, or 1 after printing the failing step.
/// </summary>
public partial class PanelCheck : Node
{
    public override async void _Ready()
    {
        var step = "start";
        try
        {
            var scene = GD.Load<PackedScene>("res://ui/conversation/ConversationPanel.tscn");
            var root = new Control();
            AddChild(root);
            root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            var panel = scene.Instantiate<Control>();
            root.AddChild(panel);
            var api = (IConversationPanel)panel;
            var taken = new List<string>();
            var continues = 0;
            var closes = 0;
            api.ChoiceTaken += taken.Add;
            api.ContinueTaken += () => continues++;
            api.CloseTaken += () => closes++;

            step = "step shows";
            var view = new ConversationView(
                "c", "A Test Talk", [new ParticipantView("a", "Ada", Demeanor.Neutral)], "s1",
                [
                    new LineView(SpeakerKind.Narrator, null, null, "Rain taps the roof [b]bold[/b]."),
                    new LineView(SpeakerKind.Character, "a", "Ada", "Well?"),
                    new LineView(SpeakerKind.Player, "p", "Pat", "Hello."),
                ],
                [
                    new ChoiceView("open", "Say something", true, null, null),
                    new ChoiceView("locked", "Say the hard thing", false, null, "You cannot find the words."),
                    new ChoiceView("third", "Leave", true, null, null),
                ],
                false);
            api.ShowStep(view);
            await Frames(3);
            var texts = AllText(panel);
            Need(texts.Contains("Say something"), "choice 1 text");
            Need(texts.Any(t => t.StartsWith("Say the hard thing") && t.Contains("You cannot find the words.")), "locked line shown");
            Need(texts.Any(t => t.Contains("[b]bold[/b]")), "markup kept as plain text");
            Need(Buttons(panel).Single(b => b.Text.StartsWith("Say the hard thing")).Disabled, "locked disabled");
            Need(Buttons(panel).Single(b => b.Text.StartsWith("Say the hard thing")).ThemeTypeVariation == "LockedChoice", "locked variation");
            Need(Buttons(panel).First().HasFocus(), "first choice focused");
            Need(!texts.Any(t => t.Any(char.IsDigit)), "digits shown");
            var shape = panel.GetNode<Control>("Panel");
            Need(panel.MouseFilter == Control.MouseFilterEnum.Ignore && shape.MouseFilter == Control.MouseFilterEnum.Stop, "mouse filters");

            step = "locked key ignored";
            Press("choice_2");
            Need(taken.Count == 0, "locked choice raised");

            step = "number key";
            Press("choice_3");
            Need(taken.SequenceEqual(["third"]), "choice_3 not raised");
            Press("choice_9");
            Need(taken.Count == 1, "out of range key raised");

            step = "double press";
            api.ShowStep(view);
            await Frames(2);
            Press("choice_1");
            Press("choice_1");
            Need(taken.SequenceEqual(["third", "open"]), "double press raised twice");
            api.ShowStep(view);
            await Frames(2);

            step = "mouse click";
            Buttons(panel).First().EmitSignal(BaseButton.SignalName.Pressed);
            Need(taken.SequenceEqual(["third", "open", "open"]), "button press not raised");

            step = "staged outcome";
            api.ShowOutcome(new ConversationOutcome("Pat", "I said it.", ["Ada looks away."], ["Ada has joined."]), false);
            api.ShowStep(view with { Choices = [], IsContinue = true, Lines = [new LineView(SpeakerKind.Narrator, null, null, "Later.")] });
            await Frames(3);
            texts = AllText(panel);
            Need(texts.Contains("I said it.") && texts.Contains("Ada looks away.") && texts.Contains("Ada has joined.") && texts.Contains("Later."), "outcome and step");
            Need(Buttons(panel).Single().Text == "Continue" && Buttons(panel).Single().HasFocus(), "continue focused");
            Buttons(panel).Single().EmitSignal(BaseButton.SignalName.Pressed);
            Need(continues == 1, "continue not raised");
            api.ShowStep(view);
            await Frames(2);
            Need(!AllText(panel).Contains("I said it."), "outcome shown twice");

            step = "final outcome";
            api.ShowOutcome(new ConversationOutcome("Pat", "Goodbye.", ["Ada nods."], []), true);
            await Frames(3);
            texts = AllText(panel);
            Need(texts.Contains("Goodbye.") && !texts.Contains("Well?"), "final shows only outcome");
            var close = Buttons(panel).Single();
            Need(close.Text == "Close" && close.HasFocus(), "close focused");
            close.EmitSignal(BaseButton.SignalName.Pressed);
            Need(closes == 1 && panel.Visible, "close raised or panel hid");

            step = "close clears";
            api.Close();
            await Frames(2);
            Need(Buttons(panel).Count == 0, "buttons remain");

            GD.Print("PANEL PASSED");
            GetTree().Quit(0);
        }
        catch (Exception ex)
        {
            GD.PrintErr($"PANEL FAILED at step '{step}': {ex.Message}");
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

    private async Task Frames(int n)
    {
        for (var i = 0; i < n; i++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
    }

    private static void Press(string action)
    {
        Input.ParseInputEvent(new InputEventAction { Action = action, Pressed = true });
        Input.ParseInputEvent(new InputEventAction { Action = action, Pressed = false });
        Input.FlushBufferedEvents();
    }

    private static List<Button> Buttons(Node root) => Walk(root).OfType<Button>().ToList();

    private static List<string> AllText(Node root) =>
        Walk(root).Select(n => n switch { Label l => l.Text, Button b => b.Text, _ => null }).OfType<string>().ToList();

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
}
