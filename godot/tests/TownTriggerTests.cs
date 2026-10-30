using AncientOrder.Rules.Town;
using AncientOrder.Stage;
using Godot;

namespace AncientOrder.Tests;

/// <summary>Walking to people and spots (ES-3): conversations open on arrival, the panel shows, walking stops during one and works again after. Wiring only; the rules tests carry the trigger logic.</summary>
public class TownTriggerTests
{
    private const double Step = 1.0 / 60;
    private const int MaxTicks = 20000;
    private const int MaxPhysicsFrames = 600;
    private const float Near = 0.05f;

    private static async Task<StartedGame> TownReady(TestContext t)
    {
        var game = await t.StartGame(TownTriggerPacks.Town());
        // The previous test's town is still leaving the navigation map; starting before the server settles leaves navigation never ready.
        for (var i = 0; i < 5; i++)
        {
            await t.Root.ToSignal(t.Root.GetTree(), SceneTree.SignalName.PhysicsFrame);
        }

        game.Flow.StartNewGame();
        Assert.True(game.Flow.IsAtTown, "flow is not at the town");
        for (var i = 0; i < MaxPhysicsFrames && !game.Flow.Stage.IsNavigationReady; i++)
        {
            await t.Root.ToSignal(t.Root.GetTree(), SceneTree.SignalName.PhysicsFrame);
        }

        Assert.True(game.Flow.Stage.IsNavigationReady, "navigation never became ready");
        return game;
    }

    private static async Task PanelShown(TestContext t, StartedGame game) =>
        await t.WaitUntil(() => game.Flow.ConversationPanel!.Visible, "the conversation panel to show");

    /// <summary>Drives the stage by fixed ticks until it stops walking.</summary>
    private static void Drive(TownStage stage)
    {
        for (var i = 0; i < MaxTicks && stage.IsWalking; i++)
        {
            stage.Tick(Step);
        }

        Assert.False(stage.IsWalking, "still walking after the tick budget");
    }

    private static string OpenConversation(StartedGame game) =>
        Assert.NotNull(game.Session!.Snapshot.Game!.Conversation, "no conversation open").ConversationId;

    /// <summary>Ends the open conversation by its only choice and lets the panel's close step pass.</summary>
    private static void EndConversation(StartedGame game)
    {
        Assert.True(game.Session!.Choose("bye").IsOk, "ending the conversation");
        game.Flow.ConversationHold = false;
    }

    [GodotTest(TimeoutSeconds = 30)]
    public async Task Walking_to_a_person_opens_the_conversation_shows_the_panel_and_stops_walking(TestContext t)
    {
        var game = await TownReady(t);
        var stage = game.Flow.Stage;
        Assert.False(game.Flow.ConversationPanel!.Visible, "panel visible before any conversation");

        Assert.True(stage.WalkToPerson("ann"), "WalkToPerson refused");
        Drive(stage);

        Assert.Equal(TownTriggerPacks.PersonConversation, OpenConversation(game), "conversation opened");
        await PanelShown(t, game);
        Assert.False(stage.IsWalking, "still walking");
        var reach = game.Session!.Snapshot.Town!.Reach;
        var distance = stage.PlayerPosition.DistanceTo(new Vector3(12, 0, 2));
        Assert.True(distance <= reach, $"opened from {distance} away, past the reach {reach}");
        Assert.True(distance > 0.5f, "walked onto the person");
    }

    [GodotTest(TimeoutSeconds = 30)]
    public async Task Clicks_to_walk_are_refused_during_a_conversation(TestContext t)
    {
        var game = await TownReady(t);
        var stage = game.Flow.Stage;
        stage.WalkToPerson("ann");
        Drive(stage);
        var held = stage.PlayerPosition;

        Assert.False(stage.WalkTo(new Vector3(25, 0, 25)), "WalkTo accepted during a conversation");
        Assert.False(stage.WalkToPerson("ann"), "WalkToPerson accepted during a conversation");
        Assert.False(stage.WalkToSpot("bench"), "WalkToSpot accepted during a conversation");
        stage.Tick(1);

        Assert.True(stage.PlayerPosition == held, "avatar moved during the conversation");
        Assert.Equal(TownTriggerPacks.PersonConversation, OpenConversation(game), "conversation changed");
    }

    [GodotTest(TimeoutSeconds = 30)]
    public async Task Ending_the_conversation_leaves_the_avatar_and_a_new_walk_moves_it_again(TestContext t)
    {
        var game = await TownReady(t);
        var stage = game.Flow.Stage;
        stage.WalkToPerson("ann");
        Drive(stage);
        var stopped = stage.PlayerPosition;

        EndConversation(game);
        await t.WaitUntil(() => !game.Flow.ConversationPanel!.Visible, "the panel to hide");

        Assert.Null(game.Session!.Snapshot.Game!.Conversation, "conversation still open");
        Assert.False(stage.IsPaused, "stage still paused");
        Assert.True(stage.PlayerPosition == stopped, "avatar moved when the conversation ended");

        Assert.True(stage.WalkTo(new Vector3(6, 0, 2)), "WalkTo refused after the conversation");
        Assert.True(stage.IsWalking, "not walking");
        Drive(stage);
        Assert.True(stage.PlayerPosition.DistanceTo(new Vector3(6, 0, 2)) < 0.6f, $"did not arrive, at {stage.PlayerPosition}");
        Assert.True(stage.PlayerPosition.DistanceTo(stopped) > 1f, "avatar did not move");
        Assert.Null(game.Session.Snapshot.Game!.Conversation, "walking opened a conversation");
    }

    [GodotTest(TimeoutSeconds = 30)]
    public async Task Walking_across_a_walk_over_spot_opens_its_scene_inside_the_area(TestContext t)
    {
        var game = await TownReady(t);
        var stage = game.Flow.Stage;
        Assert.True(stage.WalkTo(new Vector3(22, 0, 12)), "WalkTo refused");
        Drive(stage);

        Assert.Equal(TownTriggerPacks.WalkOverConversation, OpenConversation(game), "scene opened");
        await PanelShown(t, game);
        Assert.False(stage.IsWalking, "still walking");
        var at = game.Session!.Snapshot.Town!.Player;
        Assert.True(at.X is >= 19.9 and <= 24.1 && at.Z is >= 9.9 and <= 14.1, $"stopped outside the area at {at}");
        Assert.True(stage.PlayerPosition.X < 22f, "stopped past the spot's edge, not on entering it");
    }

    [GodotTest(TimeoutSeconds = 30)]
    public async Task Leaving_a_walk_over_spot_opens_nothing_and_walking_back_in_fires_again(TestContext t)
    {
        var game = await TownReady(t);
        var stage = game.Flow.Stage;
        stage.WalkTo(new Vector3(22, 0, 12));
        Drive(stage);
        EndConversation(game);
        await t.WaitUntil(() => !stage.IsPaused, "the stage to resume");

        Assert.True(stage.WalkTo(new Vector3(28, 0, 28)), "WalkTo refused");
        Drive(stage);
        Assert.Null(game.Session!.Snapshot.Game!.Conversation, "leaving the spot opened something");
        Assert.True(stage.PlayerPosition.DistanceTo(new Vector3(28, 0, 28)) < 0.6f, "did not arrive outside");

        Assert.True(stage.WalkTo(new Vector3(22, 0, 12)), "WalkTo back refused");
        Drive(stage);
        Assert.Equal(TownTriggerPacks.WalkOverConversation, OpenConversation(game), "scene did not fire again on re-entering");
    }

    [GodotTest(TimeoutSeconds = 30)]
    public async Task Walking_to_an_interact_spot_opens_its_first_entry_that_holds(TestContext t)
    {
        var game = await TownReady(t);
        var stage = game.Flow.Stage;

        Assert.True(stage.WalkToSpot("bench"), "WalkToSpot refused");
        Drive(stage);

        Assert.Equal(TownTriggerPacks.SpotConversation, OpenConversation(game), "the entry whose conditions hold");
        await PanelShown(t, game);
        Assert.False(stage.IsWalking, "still walking");
        var reach = game.Session!.Snapshot.Town!.Reach;
        Assert.True(stage.PlayerPosition.DistanceTo(new Vector3(2, 0, 16)) <= reach + 1f + Near, "opened from out of reach");
    }

    [GodotTest(TimeoutSeconds = 30)]
    public async Task Walking_stops_short_of_the_target_when_a_walk_over_spot_is_crossed_on_the_way(TestContext t)
    {
        var game = await TownReady(t);
        var stage = game.Flow.Stage;
        stage.WalkTo(new Vector3(22, 0, 2));
        Drive(stage);
        Assert.Null(game.Session!.Snapshot.Game!.Conversation, "walking along the bottom edge opened something");

        Assert.True(stage.WalkTo(new Vector3(22, 0, 20)), "WalkTo refused");
        Drive(stage);

        Assert.Equal(TownTriggerPacks.WalkOverConversation, OpenConversation(game), "scene on the way");
        Assert.True(stage.PlayerPosition.Z < 19f, "walked on past the spot to the target");
    }
}
