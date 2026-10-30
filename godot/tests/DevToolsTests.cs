#if AO_DEVTOOLS
using AncientOrder.DevTools;
using AncientOrder.Rules.Content.Check;
using AncientOrder.Rules.Personality;
using AncientOrder.Rules.Saves;
using AncientOrder.Ui;
using Godot;

namespace AncientOrder.Tests;

/// <summary>
/// The designer-only dev tools (ES-11, ES-12): the F3 overlay with hidden numbers, locked requirements against current values, the load-adjustments notice, and the full
/// content-problem list. Read-only is checked by the session version staying put. Release-build exclusion is proved by the release-build scan in verify.ps1 (not repeated here).
/// Uses the stand-in pack read from res://, with in-memory saves.
/// </summary>
public class DevToolsTests
{
    private static async Task<(ScreenFlow Flow, Rules.Session.GameSession Session, DevOverlay Overlay)> StartStandIn(TestContext t)
    {
        var report = ContentCheck.Run(new GodotContentSource("res://content/standin"));
        var content = Assert.NotNull(report.Content, "stand-in pack failed its check");
        var session = TestContext.NewSession(content);
        var flow = GD.Load<PackedScene>("res://main.tscn").Instantiate<ScreenFlow>();
        flow.AutoStart = false;
        t.Root.AddChild(flow);
        flow.Start(content, session, report);
        flow.StartNewGame();
        await t.Frames(2);
        var overlay = Assert.NotNull(flow.GetNodeOrNull<DevOverlay>("DevToolsLayer/DevOverlay"), "overlay not installed");
        return (flow, session, overlay);
    }

    private static async Task ToggleOverlay(TestContext t) => await t.Press("dev_overlay");

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

    [GodotTest]
    public async Task F3_toggles_the_overlay_without_touching_the_mouse_or_the_session(TestContext t)
    {
        var (_, session, overlay) = await StartStandIn(t);
        Assert.False(overlay.Visible, "overlay visible at start");
        Assert.True(overlay.MouseFilter == Control.MouseFilterEnum.Ignore, "overlay panel takes the mouse");
        var version = session.Snapshot.Version;

        await ToggleOverlay(t);
        Assert.True(overlay.Visible, "F3 did not show the overlay");
        Assert.Equal(version, session.Snapshot.Version, "showing the overlay changed the session");

        await ToggleOverlay(t);
        Assert.False(overlay.Visible, "F3 did not hide the overlay");
        Assert.Equal(version, session.Snapshot.Version, "hiding the overlay changed the session");
    }

    [GodotTest]
    public async Task The_overlay_shows_the_hidden_numbers_after_a_choice_and_redraws_on_each_snapshot(TestContext t)
    {
        var (_, session, overlay) = await StartStandIn(t);
        Assert.True(session.StartConversation("standin_elder_talk").IsOk, "elder start refused");
        await t.Frames(2);
        await ToggleOverlay(t);
        Assert.Contains("(none yet)", overlay.PlainText, "last choice before any choice");

        Assert.True(session.Choose("warm").IsOk, "warm refused");
        await t.Frames(2);

        var text = overlay.PlainText;
        Assert.Contains("standin_elder_talk/warm", text, "last choice not redrawn");
        var game = Assert.NotNull(session.Snapshot.Game, "no game");
        foreach (var trait in Enum.GetValues<Trait>())
        {
            Assert.Contains($"{trait} {RequirementText.Num(game.Player.Personality[trait])}%", text, "exact trait " + trait);
        }

        Assert.True(text.Contains("Synergy") && text.Contains("Memory") && text.Contains("People"), "overlay sections missing:\n" + text);
    }

    [GodotTest]
    public async Task A_locked_choice_shows_its_requirement_against_the_current_value(TestContext t)
    {
        var (_, session, overlay) = await StartStandIn(t);
        Assert.True(session.StartConversation("standin_elder_talk").IsOk, "elder start refused");
        Assert.True(session.Choose("cool").IsOk, "cool refused");
        Assert.True(session.Continue().IsOk, "continue refused");
        Assert.True(session.StartConversation("standin_elder_talk").IsOk, "second start refused");
        await t.Frames(2);
        var favor = session.Snapshot.Conversation!.Choices.First(c => c.Id == "favor");
        Assert.False(favor.Available, "favor is not locked");
        var empathy = Assert.NotNull(session.Snapshot.Game, "no game").Player.Personality[Trait.Empathy];
        var version = session.Snapshot.Version;

        await ToggleOverlay(t);

        var text = overlay.PlainText;
        Assert.Contains($"Empathy 20%+ (you: {RequirementText.Num(empathy)}%) - unmet", text, "requirement line");
        Assert.Contains("LOCKED", text, "locked marker");
        Assert.Equal(version, session.Snapshot.Version, "the overlay changed the session");
    }

    [GodotTest]
    public async Task The_load_notice_appears_only_when_a_load_adjusted_something_and_can_be_dismissed(TestContext t)
    {
        var (flow, _, _) = await StartStandIn(t);
        var notice = Assert.NotNull(flow.GetNodeOrNull<LoadReportNotice>("DevToolsLayer/LoadReportNotice"), "notice not installed");
        Assert.False(notice.Visible, "notice visible at start");

        flow.NotifyLoaded(LoadReport.Empty);
        await t.Frames(1);
        Assert.False(notice.Visible, "notice shown for a load that adjusted nothing");

        flow.NotifyLoaded(new LoadReport([new LoadAdjustment(LoadAdjustmentKind.ConversationRestarted, "standin_elder_talk", "STAND-IN: the conversation moved on")]));
        await t.Frames(1);
        Assert.True(notice.Visible, "notice not shown for an adjusted load");
        var list = notice.GetNode<Label>("Box/Scroll/List").Text;
        Assert.Contains("ConversationRestarted", list, "adjustment kind");
        Assert.Contains("the conversation moved on", list, "adjustment message");

        await t.Click(notice.GetNode<Button>("Box/Dismiss"));
        Assert.False(notice.Visible, "notice not dismissed");
    }

    [GodotTest]
    public async Task A_broken_pack_lists_every_problem_on_the_content_error_screen(TestContext t)
    {
        var game = await t.StartGame(TestPacks.UnknownStartingTown());

        Assert.True(game.Flow.IsContentErrorShown, "error screen not shown");
        Assert.True(game.Report.Problems.Length > 0, "the broken pack reported no problems");
        var list = Assert.NotNull(FindList(game.Flow), "problem list not shown");
        var text = list.GetNode<Label>("Scroll/List").Text;
        foreach (var problem in game.Report.Problems)
        {
            Assert.Contains(problem.Path + ": " + problem.Message, text, "problem missing from the list: " + problem.Path);
        }
    }
}
#endif
