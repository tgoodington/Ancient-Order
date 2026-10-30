using AncientOrder.Rules.Common;
using AncientOrder.Stage;
using Godot;

namespace AncientOrder;

/// <summary>
/// Headless check of the town stage (T14) on the STAND-IN pack res://smoke/townpack: bakes navigation, walks around an obstacle,
/// walks to a person, a walk-over spot and an interact spot, pauses during a conversation, diffs people, zooms, and picks with a real ray.
/// Run: godot --headless --path godot res://smoke/townwalk.tscn. Exits 0 on success, or 1 after printing the failing step.
/// </summary>
public partial class TownWalkCheck : Node
{
    private const double Step = 1.0 / 60;

    private readonly List<RuleError> _errors = [];

    public override async void _Ready()
    {
        var step = "start";
        try
        {
            await Run(s =>
            {
                step = s;
                GD.Print("townwalk: " + s);
            });
            GD.Print("TOWN WALK PASSED");
            GetTree().Quit(0);
        }
        catch (Exception ex)
        {
            GD.PrintErr($"TOWN WALK FAILED at step '{step}': {ex.Message}");
            GetTree().Quit(1);
        }
    }

    private static void Need(bool ok, string message)
    {
        if (!ok)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static double Drive(TownStage stage, int maxTicks)
    {
        var walked = 0.0;
        for (var i = 0; i < maxTicks && stage.IsWalking; i++)
        {
            var before = stage.PlayerPosition;
            stage.Tick(Step);
            walked += before.DistanceTo(stage.PlayerPosition);
            var p = stage.PlayerPosition;
            Need(!(p.X is > 18.0f and < 22.0f && p.Z < 30.0f), $"avatar entered the wall at {p}");
            Need(p.X is >= 0 and <= 40 && p.Z is >= 0 and <= 40, $"avatar left the ground at {p}");
        }

        return walked;
    }

    private async Task Run(Action<string> step)
    {
        step("host");
        var host = new GameHost("res://smoke/townpack");
        Need(host.Session is not null, "pack did not load: " + string.Join("; ", host.ContentProblems.Select(p => p.Path + " " + p.Message)));
        var session = host.Session!;
        Need(session.NewGame().IsOk, "new game");

        step("build stage");
        var stage = GD.Load<PackedScene>("res://town/TownStage.tscn").Instantiate<TownStage>();
        AddChild(stage);
        stage.Init(session, _errors.Add);
        session.Subscribe(s =>
        {
            if (s.Town is not null)
            {
                stage.Apply(s.Town);
            }
        });
        stage.Apply(session.Snapshot.Town!);
        Need(!stage.IsNavigationReady, "navigation should not be ready before the server syncs");
        Need(!stage.WalkTo(new Vector3(10, 0, 10)), "a click before navigation is ready must be ignored");
        for (var i = 0; i < 60 && !stage.IsNavigationReady; i++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        }

        Need(stage.IsNavigationReady, "navigation never became ready");
        Need(stage.PlayerPosition.DistanceTo(new Vector3(2, 0, 2)) < 0.01f, "avatar not at the start");

        step("walk around the wall");
        Need(stage.WalkTo(new Vector3(38, 0, 3)), "WalkTo refused");
        var walked = Drive(stage, 20000);
        Need(!stage.IsWalking, "still walking");
        Need(stage.PlayerPosition.DistanceTo(new Vector3(38, 0, 3)) < 0.6f, $"did not arrive: {stage.PlayerPosition}");
        Need(walked > 60, $"path was not around the wall: walked {walked:F1} m");
        Need(Math.Abs(session.Snapshot.Town!.Player.X - stage.PlayerPosition.X) < 1e-4, "session position differs from the avatar");
        NoErrors();

        step("click outside the ground");
        Need(stage.WalkTo(new Vector3(100, 0, 3)), "WalkTo outside refused");
        Drive(stage, 20000);
        Need(stage.PlayerPosition.X <= 40f, "avatar left the ground");
        NoErrors();

        step("walk to a person");
        Need(stage.WalkToPerson("standin_companion_a"), "WalkToPerson refused");
        Drive(stage, 20000);
        Need(session.Snapshot.Conversation is not null, "conversation did not start");
        Need(!stage.IsWalking, "still walking in conversation");
        NoErrors();

        step("movement stops in a conversation");
        var held = stage.PlayerPosition;
        Need(!stage.WalkTo(new Vector3(30, 0, 30)), "WalkTo accepted during a conversation");
        stage.Tick(1);
        stage.Pause();
        stage.Resume();
        Need(stage.PlayerPosition == held, "avatar moved during a conversation");
        NoErrors();
        Need(session.Choose("bye").IsOk, "end conversation");
        Need(session.Snapshot.Conversation is null, "conversation still open");
        Need(stage.PlayerPosition == held, "avatar moved when the conversation ended");

        step("walk-over spot");
        Need(stage.WalkTo(new Vector3(36, 0, 37)), "WalkTo zone refused");
        Drive(stage, 20000);
        Need(session.Snapshot.Conversation is not null, "walk-over did not fire");
        Need(session.Snapshot.Town!.Player.Z is > 34.9 and < 39.1, "stopped outside the zone");
        NoErrors();
        Need(session.Choose("bye").IsOk, "end conversation");

        step("interact spot");
        Need(stage.WalkToSpot("standin_post"), "WalkToSpot refused");
        Drive(stage, 20000);
        Need(session.Snapshot.Conversation is not null, "spot did not trigger");
        NoErrors();
        Need(session.Choose("bye").IsOk, "end conversation");

        step("diff people");
        var people = stage.GetNode("People");
        Need(people.GetChildCount() == 1, "expected one person");
        var view = session.Snapshot.Town!;
        stage.Apply(view with { People = [] });
        Need(people.GetChildCount() == 0, "person not removed");
        stage.Apply(view);
        Need(people.GetChildCount() == 1, "person not added");
        var moved = view.People[0] with { Position = new Rules.Town.Point(7, 7) };
        stage.Apply(view with { People = [moved] });
        Need(people.GetChildCount() == 1, "person duplicated by a move");

        step("zoom");
        var camera = stage.GetNode<TownCamera>("TownCamera");
        for (var i = 0; i < 50; i++)
        {
            camera.Zoom(1);
        }

        Need(Math.Abs(camera.Distance - camera.MaxDistance) < 1e-4, "zoom out not limited");
        for (var i = 0; i < 50; i++)
        {
            camera.Zoom(-1);
        }

        Need(Math.Abs(camera.Distance - camera.MinDistance) < 1e-4, "zoom in not limited");

        step("real ray");
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        var center = GetViewport().GetVisibleRect().Size / 2;
        Need(stage.Pick(center), "ray pick at the screen center found nothing to walk to");
        Drive(stage, 20000);
        NoErrors();

        step("pause");
        Need(stage.WalkTo(new Vector3(30, 0, 5)), "walk");
        stage.Pause();
        Need(!stage.IsWalking, "pause did not stop");
        Need(!stage.WalkTo(new Vector3(30, 0, 5)), "clicks accepted while paused");
        stage.Resume();
        Need(stage.WalkTo(new Vector3(30, 0, 5)), "clicks refused after resume");
        NoErrors();
    }

    private void NoErrors() => Need(_errors.Count == 0, "refusal shown: " + string.Join("; ", _errors.Select(e => e.Message)));
}
