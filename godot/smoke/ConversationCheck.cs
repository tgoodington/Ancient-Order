using AncientOrder.Rules.Content.Check;
using AncientOrder.Rules.Conversations;
using AncientOrder.Rules.Saves;
using AncientOrder.Rules.Session;
using AncientOrder.Ui;
using Godot;

namespace AncientOrder;

/// <summary>
/// Headless check of the conversation presenter (T16) on the stand-in pack, with a recording stand-in for the panel (T17's real panel is not needed): conversations start, advance through
/// Choose and Continue, deliver outcomes (player line, body-language lines, party notices) before the next step, end with a final outcome and a Close, and reopen at the same step after a load.
/// Saves go under user://smoke/conversation/saves, emptied before and after. Run: godot --headless --path godot res://smoke/conversationcheck.tscn. Exits 0, or 1 after printing the failing step.
/// </summary>
public partial class ConversationCheck : Node
{
    private const string SaveFolder = "user://smoke/conversation/saves";

    private sealed partial class RecordingPanel : Control, IConversationPanel
    {
        public readonly List<string> Log = [];

        public ConversationView? Step;

        public ConversationOutcome? Outcome;

        public event Action<string>? ChoiceTaken;

        public event Action? ContinueTaken;

        public event Action? CloseTaken;

        public void ShowStep(ConversationView view)
        {
            Step = view;
            Log.Add("step:" + view.StepId);
        }

        public void ShowOutcome(ConversationOutcome outcome, bool final)
        {
            Outcome = outcome;
            Log.Add(final ? "final" : "outcome");
        }

        public void Close() => Log.Add("close");

        public void Take(string id) => ChoiceTaken?.Invoke(id);

        public void TakeContinue() => ContinueTaken?.Invoke();

        public void PressClose() => CloseTaken?.Invoke();
    }

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
                GD.Print("conversation: " + s);
            });
            Empty(folder);
            GD.Print("CONVERSATION PASSED");
            GetTree().Quit(0);
        }
        catch (Exception ex)
        {
            Empty(folder);
            GD.PrintErr($"CONVERSATION FAILED at step '{step}': {ex.Message}");
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

    private async Task Frames()
    {
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
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
        var panel = new RecordingPanel { Visible = false };
        AddChild(panel);
        var presenter = new ConversationPresenter(flow, panel);
        flow.StartNewGame();
        await Frames();

        step("a started conversation opens at its first step");
        Need(session.StartConversation("standin_recruit_talk").IsOk, "start refused");
        await Frames();
        Need(panel.Log.LastOrDefault() == "step:greeting" && panel.Step!.Choices.Length == 2, "panel not at greeting: " + string.Join(",", panel.Log));
        var before = session.Snapshot.Version;
        panel.Take("no_such_choice");
        await Frames();
        Need(session.Snapshot.Version == before && panel.Log.LastOrDefault() == "step:greeting", "a refused choice changed the panel");

        step("a choice that adds a party member gives its line and a notice before the next step");
        panel.Log.Clear();
        panel.Take("invite");
        await Frames();
        Need(panel.Log.SequenceEqual(["outcome", "step:joined"]), "wrong call order: " + string.Join(",", panel.Log));
        var outcome = panel.Outcome!;
        Need(outcome.PlayerLine == "STAND-IN: invite the Recruit to join (a new recruit joins)", "player line: " + outcome.PlayerLine);
        Need(outcome.Notices.SequenceEqual(["STAND-IN Recruit joins the party."]), "notices: " + string.Join("|", outcome.Notices));
        Need(!string.IsNullOrEmpty(outcome.PlayerName), "player name missing");
        Need(panel.Step!.IsContinue, "joined step should offer Continue");

        step("a mid-conversation save reopens at the same step");
        var saved = await session.SaveToSlot(1);
        Need(saved.IsOk, "save refused");
        flow.ReturnToStart();
        await Frames();
        var loaded = await session.LoadFromSlot(1);
        Need(loaded.IsOk, "load refused");
        flow.NotifyLoaded(loaded.Value);
        await Frames();
        Need(panel.Log.LastOrDefault() == "step:joined", "load did not reopen the step: " + string.Join(",", panel.Log));

        step("Continue ends the conversation with nothing to show, so the panel closes at once");
        panel.Log.Clear();
        panel.TakeContinue();
        await Frames();
        Need(panel.Log.SequenceEqual(["close"]) && !presenter.IsClosing && session.Snapshot.Conversation is null, "continue: " + string.Join(",", panel.Log));

        step("a conversation-ending choice shows its outcome and waits for Close");
        Need(session.StartConversation("standin_recruit_talk").IsOk, "second start refused");
        await Frames();
        panel.Log.Clear();
        panel.Take("bye");
        await Frames();
        Need(panel.Log.SequenceEqual(["final"]) && presenter.IsClosing, "final: " + string.Join(",", panel.Log));
        Need(flow.ConversationPanel!.Visible && flow.Stage.IsPaused, "panel hidden or town running while the final outcome shows");

        step("the held outcome survives a menu round-trip");
        var logged = panel.Log.Count;
        flow.OpenMenu();
        await Frames();
        flow.CloseMenu();
        await Frames();
        Need(presenter.IsClosing && flow.ConversationPanel.Visible && flow.Stage.IsPaused, "hold lost after the menu round-trip");
        Need(panel.Log.Count == logged, "panel redrawn by the menu round-trip: " + string.Join(",", panel.Log));

        step("Close lifts the hold, hides the panel and resumes the town");
        panel.PressClose();
        await Frames();
        Need(!presenter.IsClosing && panel.Log.LastOrDefault() == "close" && !flow.ConversationPanel.Visible, "close: " + string.Join(",", panel.Log));
        Need(flow.IsAtTown && !flow.Stage.IsPaused, "town did not resume");

        step("a load drops a held outcome");
        Need(session.StartConversation("standin_recruit_talk").IsOk, "third start refused");
        await Frames();
        panel.Take("bye");
        await Frames();
        Need(presenter.IsClosing, "no hold before the load");
        var reloaded = await session.LoadFromSlot(1);
        Need(reloaded.IsOk, "reload refused");
        flow.NotifyLoaded(reloaded.Value);
        await Frames();
        Need(!presenter.IsClosing && panel.Log.LastOrDefault() == "step:joined" && flow.ConversationPanel.Visible, "load did not win: " + string.Join(",", panel.Log));
        panel.TakeContinue();
        await Frames();

        step("Return to Start and New Game drop a held outcome");
        Need(session.StartConversation("standin_recruit_talk").IsOk, "fourth start refused");
        await Frames();
        panel.Take("bye");
        await Frames();
        Need(presenter.IsClosing, "no hold before return to start");
        flow.ReturnToStart();
        await Frames();
        Need(!presenter.IsClosing && !flow.ConversationPanel.Visible, "hold survived return to start");
        flow.StartNewGame();
        await Frames();
        Need(flow.IsAtTown && !presenter.IsClosing && !flow.ConversationPanel.Visible && !flow.Stage.IsPaused, "stale outcome or paused town after new game");

        step("reactions arrive as body-language lines and tells do not fail");
        Need(session.StartConversation("standin_elder_talk").IsOk, "elder start refused");
        await Frames();
        panel.Log.Clear();
        panel.Take("override");
        await Frames();
        Need(panel.Log.SequenceEqual(["outcome", "step:warm_reply"]), "elder order: " + string.Join(",", panel.Log));
        Need(panel.Outcome!.BodyLines.Any(l => l.Contains("gives a written line") && !l.Contains("{name}")), "no written reaction line: " + string.Join("|", panel.Outcome.BodyLines));
        Need(!panel.Outcome.BodyLines.Concat(panel.Outcome.Notices).Any(l => l.Any(char.IsDigit)), "a number reached the outcome");

        flow.QueueFree();
        panel.QueueFree();
    }
}
