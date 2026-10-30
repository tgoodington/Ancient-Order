using AncientOrder.Rules.Session;
using AncientOrder.Rules.Town;
using AncientOrder.Stage;
using Godot;

namespace AncientOrder.Tests;

/// <summary>Walking a town built from content (ES-2): the stage builds from the TownView, navigation agrees with the rules' own walkability, session changes reach the scene, the camera and the real-ray click.</summary>
public class TownStageTests
{
    private const double Step = 1.0 / 60;
    private const float Arrived = 0.6f;

    private sealed record Stage(TownStage Node, GameSession Session, TownDefinition Town, AuthoredGround Ground);

    private static async Task<Stage> StartStage(TestContext t, bool waitForNavigation = true)
    {
        var report = TestContext.Check(TownStagePacks.Walled());
        var content = Assert.NotNull(report.Content, "town pack failed its check");
        var session = TestContext.NewSession(content);
        Assert.True(session.NewGame().IsOk, "new game refused");
        Assert.True(content.Towns.TryGet(TownStagePacks.TownId, out var town), "town missing from content");

        var stage = GD.Load<PackedScene>("res://town/TownStage.tscn").Instantiate<TownStage>();
        t.Root.AddChild(stage);
        stage.Init(session, error => Assert.Fail("a refusal reached the player: " + error.Message));
        session.Subscribe(snapshot =>
        {
            if (snapshot.Town is not null)
            {
                stage.Apply(snapshot.Town);
            }
        });
        stage.Apply(session.Snapshot.Town!);
        if (waitForNavigation)
        {
            await t.WaitUntil(() => stage.IsNavigationReady, "navigation to become ready");
        }

        return new Stage(stage, session, town, new AuthoredGround(town));
    }

    private static bool OnGround(Stage s, Vector3 p) => s.Ground.IsWalkable(new Point(p.X, p.Z));

    /// <summary>Ticks until the walk ends, checking after every tick that the avatar stands on ground the rules call walkable and outside every footprint. Returns the distance walked.</summary>
    private static double Drive(Stage s, int maxTicks = 20000)
    {
        var walked = 0.0;
        for (var i = 0; i < maxTicks && s.Node.IsWalking; i++)
        {
            var before = s.Node.PlayerPosition;
            s.Node.Tick(Step);
            var p = s.Node.PlayerPosition;
            walked += before.DistanceTo(p);
            Assert.True(OnGround(s, p), $"avatar left walkable ground at {p}");
            foreach (var obstacle in s.Session.Snapshot.Town!.Obstacles)
            {
                Assert.False(Geometry.Contains(obstacle.Footprint, new Point(p.X, p.Z)), $"avatar inside the footprint of {obstacle.Id} at {p}");
            }
        }

        Assert.False(s.Node.IsWalking, "still walking after the tick limit");
        return walked;
    }

    private static bool OnScreen(Camera3D camera, Vector3 world, Viewport viewport)
    {
        if (camera.IsPositionBehind(world))
        {
            return false;
        }

        return viewport.GetVisibleRect().HasPoint(camera.UnprojectPosition(world));
    }

    [GodotTest]
    public async Task The_stage_builds_one_marker_per_person_and_spot_and_one_block_per_obstacle(TestContext t)
    {
        var s = await StartStage(t);
        var view = s.Session.Snapshot.Town!;

        Assert.Equal(1, view.People.Length, "people in view");
        Assert.Equal(1, view.Spots.Length, "spots in view");
        Assert.Equal(2, view.Obstacles.Length, "obstacles in view");
        Assert.Equal(view.People.Length, s.Node.GetNode("People").GetChildCount(), "person markers");
        Assert.Equal(view.Spots.Length, s.Node.GetNode("Spots").GetChildCount(), "spot markers");
        Assert.Equal(view.Obstacles.Length, s.Node.GetNode("Obstacles").GetChildCount(), "obstacle blocks");
        Assert.NotNull(s.Node.GetNode("People").GetNodeOrNull("Person_ann"), "Ann's marker");
        Assert.NotNull(s.Node.GetNode("Spots").GetNodeOrNull("Spot_post"), "the post's marker");
        Assert.NotNull(s.Node.GetNode("Obstacles").GetNodeOrNull("Block_wall"), "the wall's block");
        Assert.NotNull(s.Node.GetNode("Obstacles").GetNodeOrNull("Block_crate"), "the crate's block");
        Assert.NotNull(s.Node.GetNode("Ground").GetNodeOrNull("GroundBody"), "the ground's pick body");
        Assert.True(s.Node.GetNode<Node3D>("People/Person_ann").Position.DistanceTo(new Vector3(10, 0, 10)) < 0.01f, "Ann stands at her authored point");
        Assert.True(s.Node.PlayerPosition.DistanceTo(new Vector3(2, 0, 2)) < 0.01f, "the avatar stands at the town's start");
    }

    [GodotTest]
    public async Task Navigation_becomes_ready_within_a_bounded_number_of_frames_and_walking_waits_for_it(TestContext t)
    {
        var s = await StartStage(t, waitForNavigation: false);

        Assert.False(s.Node.IsNavigationReady, "navigation ready before the server took the mesh");
        Assert.False(s.Node.WalkTo(new Vector3(10, 0, 3)), "a walk was accepted before navigation was ready");
        Assert.False(s.Node.IsWalking, "walking before navigation was ready");

        await t.WaitUntil(() => s.Node.IsNavigationReady, "navigation to become ready");

        Assert.True(s.Node.WalkTo(new Vector3(10, 0, 3)), "a walk was refused once navigation was ready");
        Assert.True(s.Node.IsWalking, "not walking after an accepted walk");
    }

    [GodotTest]
    public async Task Walking_to_a_point_behind_the_wall_goes_around_it_on_ground_the_rules_call_walkable(TestContext t)
    {
        var s = await StartStage(t);
        var target = new Vector3(38, 0, 3);

        Assert.True(s.Node.WalkTo(target), "walk refused");
        var walked = Drive(s);

        Assert.True(s.Node.PlayerPosition.DistanceTo(target) < Arrived, $"did not arrive: at {s.Node.PlayerPosition}");
        Assert.True(walked > 60, $"the path did not go around the wall: walked {walked:F1} m");
    }

    [GodotTest]
    public async Task Arriving_reports_the_position_and_the_session_agrees_with_the_avatar(TestContext t)
    {
        var s = await StartStage(t);
        var versionBefore = s.Session.Snapshot.Version;

        Assert.True(s.Node.WalkTo(new Vector3(38, 0, 3)), "walk refused");
        Drive(s);

        var reported = s.Session.Snapshot.Town!.Player;
        var avatar = s.Node.PlayerPosition;
        Assert.True(Math.Abs(reported.X - avatar.X) < 1e-3 && Math.Abs(reported.Z - avatar.Z) < 1e-3, $"session {reported} differs from avatar {avatar}");
        Assert.True(s.Session.Snapshot.Version > versionBefore, "the position was never reported");
        Assert.True(s.Ground.IsWalkable(reported), "the session holds a position the rules call unwalkable");
    }

    [GodotTest]
    public async Task A_click_outside_the_ground_ends_on_the_ground_and_a_click_into_the_wall_ends_beside_it(TestContext t)
    {
        var s = await StartStage(t);

        Assert.True(s.Node.WalkTo(new Vector3(100, 0, 3)), "walk toward outside refused");
        Drive(s);
        Assert.True(OnGround(s, s.Node.PlayerPosition), "avatar ended off the ground");

        Assert.True(s.Node.WalkTo(new Vector3(20, 0, 15)), "walk toward the wall refused");
        Drive(s);
        Assert.True(OnGround(s, s.Node.PlayerPosition), "avatar ended inside the wall");
    }

    [GodotTest]
    public async Task A_person_added_by_a_session_change_appears_without_the_ground_being_rebuilt(TestContext t)
    {
        var s = await StartStage(t);
        var ground = s.Node.GetNode("Ground");
        var groundBody = Assert.NotNull(ground.GetNodeOrNull("GroundBody"), "ground body");
        var wall = Assert.NotNull(s.Node.GetNode("Obstacles").GetNodeOrNull("Block_wall"), "wall block");
        var ann = Assert.NotNull(s.Node.GetNode("People").GetNodeOrNull("Person_ann"), "Ann's marker");
        Assert.Null(s.Node.GetNode("People").GetNodeOrNull("Person_bo"), "Bo present before the flag");

        Assert.True(s.Session.StartConversation("chat").IsOk, "conversation refused");
        Assert.True(s.Session.Choose("mark").IsOk, "choice refused");
        await t.NextFrame();

        Assert.Equal(2, s.Node.GetNode("People").GetChildCount(), "person markers after the change");
        Assert.NotNull(s.Node.GetNode("People").GetNodeOrNull("Person_bo"), "Bo's marker");
        Assert.True(ReferenceEquals(groundBody, ground.GetNodeOrNull("GroundBody")), "the ground body was rebuilt");
        Assert.True(ReferenceEquals(wall, s.Node.GetNode("Obstacles").GetNodeOrNull("Block_wall")), "the obstacle blocks were rebuilt");
        Assert.True(ReferenceEquals(ann, s.Node.GetNode("People").GetNodeOrNull("Person_ann")), "an existing person's marker was rebuilt");
        Assert.True(s.Node.IsNavigationReady, "navigation was reset by a person change");
    }

    [GodotTest]
    public async Task The_camera_keeps_the_player_inside_the_viewport_while_following_and_at_both_zoom_limits(TestContext t)
    {
        var s = await StartStage(t);
        var camera = s.Node.GetNode<TownCamera>("TownCamera");
        var viewport = camera.GetViewport();
        await t.NextFrame();
        Assert.True(camera.Current, "the town camera is not the current camera");
        Assert.True(OnScreen(camera, s.Node.PlayerPosition, viewport), "player off screen at the start");

        Assert.True(s.Node.WalkTo(new Vector3(38, 0, 3)), "walk refused");
        Drive(s);
        await t.WaitUntil(() => OnScreen(camera, s.Node.PlayerPosition, viewport), "the camera to bring the player into view");

        foreach (var steps in new[] { 100f, -100f })
        {
            camera.Zoom(steps);
            camera.SnapToTarget();
            await t.NextFrame();
            Assert.True(OnScreen(camera, s.Node.PlayerPosition, viewport), $"player off screen at distance {camera.Distance}");
        }
    }

    [GodotTest]
    public async Task Zoom_stays_within_its_limits_by_method_and_by_input(TestContext t)
    {
        var s = await StartStage(t);
        var camera = s.Node.GetNode<TownCamera>("TownCamera");

        for (var i = 0; i < 50; i++)
        {
            camera.Zoom(1);
        }

        Assert.True(Math.Abs(camera.Distance - camera.MaxDistance) < 1e-4, "zooming out is not limited");
        for (var i = 0; i < 50; i++)
        {
            camera.Zoom(-1);
        }

        Assert.True(Math.Abs(camera.Distance - camera.MinDistance) < 1e-4, "zooming in is not limited");

        for (var i = 0; i < 40; i++)
        {
            await t.Press("camera_zoom_out");
        }

        Assert.True(Math.Abs(camera.Distance - camera.MaxDistance) < 1e-4, "zoom out by input is not limited");
        for (var i = 0; i < 40; i++)
        {
            await t.Press("camera_zoom_in");
        }

        Assert.True(Math.Abs(camera.Distance - camera.MinDistance) < 1e-4, "zoom in by input is not limited");
    }

    [GodotTest(TimeoutSeconds = 20)]
    public async Task A_real_mouse_click_on_the_ground_starts_a_walk_toward_it(TestContext t)
    {
        var s = await StartStage(t);
        var camera = s.Node.GetNode<TownCamera>("TownCamera");
        var ground = new Vector3(5, 0, 4);
        await t.Frames(3);
        var screen = camera.UnprojectPosition(ground);
        Assert.True(camera.GetViewport().GetVisibleRect().HasPoint(screen), $"the known ground point projects off screen at {screen}");
        Assert.False(s.Node.IsWalking, "walking before the click");

        var click = new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true, Position = screen, GlobalPosition = screen };
        // Local coordinates: the headless window has no size, so its stretch transform would remap a global position.
        camera.GetViewport().PushInput(click, true);
        await t.NextFrame();

        Assert.True(s.Node.IsWalking, "the click did not start a walk");
        Drive(s);
        Assert.True(s.Node.PlayerPosition.DistanceTo(ground) < Arrived, $"the walk did not end at the clicked point: at {s.Node.PlayerPosition}");
    }
}
