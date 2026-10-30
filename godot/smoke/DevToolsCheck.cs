#if AO_DEVTOOLS
using System.Collections.Immutable;
using AncientOrder.DevTools;
using AncientOrder.Rules.Content.Check;
using AncientOrder.Rules.Personality;
using AncientOrder.Rules.Saves;
using AncientOrder.Rules.Session;
using AncientOrder.Ui;
using Godot;

namespace AncientOrder;

/// <summary>
/// Headless check of the dev tools (T37) on the stand-in pack: F3 toggles the overlay without changing the session, a locked choice shows its requirement against the current value,
/// a load report with adjustments shows the dismissible notice, and a failed content check lists every problem on the error screen.
/// Saves go under user://smoke/devtools/saves, emptied before and after. Run: godot --headless --path godot res://smoke/devtoolscheck.tscn. Exits 0, or 1 after printing the failing step.
/// </summary>
public partial class DevToolsCheck : Node
{
    private const string SaveFolder = "user://smoke/devtools/saves";

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
                GD.Print("devtools: " + s);
            });
            Empty(folder);
            GD.Print("DEVTOOLS PASSED");
            GetTree().Quit(0);
        }
        catch (Exception ex)
        {
            Empty(folder);
            GD.PrintErr($"DEVTOOLS FAILED at step '{step}': {ex.Message}");
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

    private ScreenFlow Make()
    {
        var flow = GD.Load<PackedScene>("res://main.tscn").Instantiate<ScreenFlow>();
        flow.AutoStart = false;
        AddChild(flow);
        return flow;
    }

    private async Task Run(string folder, Action<string> step)
    {
        var report = ContentCheck.Run(new GodotContentSource("res://content/standin"));
        var content = report.Content ?? throw new InvalidOperationException("stand-in pack failed");
        var session = new GameSession(content, new GameSessionOptions { Saves = new SaveStore(new FileSaveBackend(folder), content) });
        var flow = Make();
        flow.Start(content, session, report);
        flow.StartNewGame();
        await Frames();

        step("the tools install and the overlay starts hidden");
        var overlay = flow.GetNodeOrNull<DevOverlay>("DevToolsLayer/DevOverlay") ?? throw new InvalidOperationException("overlay not installed");
        Need(!overlay.Visible, "overlay visible at start");
        Need(overlay.MouseFilter == Control.MouseFilterEnum.Ignore, "overlay panel takes the mouse");

        step("a locked choice is set up");
        Need(session.StartConversation("standin_elder_talk").IsOk, "elder start refused");
        Need(session.Choose("cool").IsOk, "cool refused");
        Need(session.Continue().IsOk, "continue refused");
        Need(session.StartConversation("standin_elder_talk").IsOk, "second start refused");
        await Frames();
        var favor = session.Snapshot.Conversation!.Choices.First(c => c.Id == "favor");
        Need(!favor.Available, "favor is not locked (player empathy too high for this check)");
        var empathy = session.Snapshot.Game!.Player.Personality[Trait.Empathy];

        step("F3 shows the overlay with the requirement and changes nothing");
        var version = session.Snapshot.Version;
        Input.ParseInputEvent(new InputEventAction { Action = "dev_overlay", Pressed = true });
        await Frames();
        Need(overlay.Visible, "F3 did not show the overlay");
        Need(session.Snapshot.Version == version, "the overlay changed the session");
        var text = overlay.PlainText;
        var expected = $"Empathy 20%+ (you: {RequirementText.Num(empathy)}%) - unmet";
        Need(text.Contains(expected), $"requirement line '{expected}' missing from:\n{text}");
        Need(text.Contains("LOCKED") && text.Contains("Last choice") && text.Contains("Synergy") && text.Contains("Memory") && text.Contains("People"), "sections missing:\n" + text);
        Need(session.Choose("smalltalk").IsOk, "smalltalk refused");
        await Frames();
        Need(overlay.PlainText.Contains("standin_elder_talk/smalltalk"), "last choice not redrawn on the new snapshot:\n" + overlay.PlainText);
        version = session.Snapshot.Version;

        step("F3 hides the overlay");
        Input.ParseInputEvent(new InputEventAction { Action = "dev_overlay", Pressed = true });
        await Frames();
        Need(!overlay.Visible, "F3 did not hide the overlay");
        Need(session.Snapshot.Version == version, "toggling changed the session");

        step("a load with adjustments shows a dismissible notice");
        var notice = flow.GetNode<LoadReportNotice>("DevToolsLayer/LoadReportNotice");
        flow.NotifyLoaded(LoadReport.Empty);
        Need(!notice.Visible, "notice shown for an empty report");
        flow.NotifyLoaded(new LoadReport([new LoadAdjustment(LoadAdjustmentKind.ConversationRestarted, "standin_elder_talk", "STAND-IN: the conversation moved on")]));
        Need(notice.Visible, "notice not shown");
        Need(notice.GetNode<Label>("Box/Scroll/List").Text.Contains("ConversationRestarted") && notice.GetNode<Label>("Box/Scroll/List").Text.Contains("the conversation moved on"), "notice text wrong");
        notice.GetNode<Button>("Box/Dismiss").EmitSignal(BaseButton.SignalName.Pressed);
        Need(!notice.Visible, "notice not dismissed");
        flow.QueueFree();
        await Frames();

        step("a failed content check lists every problem on the error screen");
        var broken = Make();
        var bad = ContentCheck.Run(new GodotContentSource("res://smoke/no_such_pack"));
        Need(bad.Content is null && !bad.Problems.IsDefaultOrEmpty, "missing pack passed");
        broken.Start(null, null, bad);
        await Frames();
        Need(broken.IsContentErrorShown, "error screen not shown");
        var list = FindList(broken) ?? throw new InvalidOperationException("problem list not shown");
        var listText = list.GetNode<Label>("Scroll/List").Text;
        foreach (var problem in bad.Problems)
        {
            Need(listText.Contains(problem.Path + ": " + problem.Message), "problem missing from the list: " + problem.Path);
        }

        broken.QueueFree();
        await Frames();
    }

    private static ContentProblemsList? FindList(Node node)
    {
        foreach (var child in node.GetChildren())
        {
            if (child is ContentProblemsList list)
            {
                return list;
            }

            if (FindList(child) is { } found)
            {
                return found;
            }
        }

        return null;
    }
}
#endif
