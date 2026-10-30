using AncientOrder.Rules.Common;
using AncientOrder.Rules.Content.Packs;
using AncientOrder.Rules.Session;
using AncientOrder.Rules.Tests.Support;
using AncientOrder.Rules.Town;

namespace AncientOrder.Rules.Tests.TownTesting;

/// <summary>
/// What people and spots open (ES-3): reach, first matching entry, walk-over boundaries, hidden spots and people, and refusals.
/// ContentPacks/TownTriggerTests already covers the basic person, interact and walk-over paths; this adds the edges around them.
/// </summary>
public class TriggerTests
{
    private const string Pack = """{ "format": "ancient-order-content", "version": 1, "id": "t", "name": "Test", "standIn": false, "files": ["all.json"] }""";

    private const string All = """
        {
          "settings": { "playerName": "Kael", "lineup": { "min": 1, "max": 1 }, "startingTown": "square", "reach": 3 },
          "archetypes": [{ "id": "x" }],
          "characters": [
            { "id": "ann", "name": "Ann", "archetypeId": "x", "personality": { "patience": 20, "empathy": 20, "cunning": 10, "logic": 15, "kindness": 20, "charisma": 15 }, "startingAffection": 0, "startingTrust": 0 },
            { "id": "bo", "name": "Bo", "archetypeId": "x", "personality": { "patience": 20, "empathy": 20, "cunning": 10, "logic": 15, "kindness": 20, "charisma": 15 }, "startingAffection": 0, "startingTrust": 0 }
          ],
          "conversations": [
            { "id": "first", "participants": ["ann"], "start": [{ "step": "s" }], "steps": [ { "id": "s", "lines": [], "choices": [ { "id": "bye", "text": "Bye", "effects": [ { "type": "setFlag", "flag": "seen" } ] } ] } ] },
            { "id": "second", "participants": ["ann"], "start": [{ "step": "s" }], "steps": [ { "id": "s", "lines": [], "choices": [ { "id": "bye", "text": "Bye" } ] } ] },
            { "id": "third", "participants": ["ann"], "start": [{ "step": "s" }], "steps": [ { "id": "s", "lines": [], "choices": [ { "id": "bye", "text": "Bye" } ] } ] }
          ],
          "towns": [ {
            "id": "square", "name": "Square",
            "ground": [ [[0, 0], [60, 0], [60, 60], [0, 60]] ],
            "start": [1, 1],
            "people": [
              { "character": "ann", "placements": [ { "at": [10, 10] } ],
                "opens": [ { "conversation": "second", "when": [ { "type": "flag", "flag": "seen" } ] }, { "conversation": "first" }, { "conversation": "third" } ] },
              { "character": "bo", "placements": [ { "at": [40, 40], "when": [ { "type": "flag", "flag": "bo_here" } ] }, { "at": [50, 50] } ],
                "opens": [ { "conversation": "third" } ] }
            ],
            "spots": [
              { "id": "bench", "area": { "circle": { "center": [20, 30], "radius": 1 } }, "trigger": "interact",
                "opens": [ { "conversation": "second", "when": [ { "type": "flag", "flag": "seen" } ] }, { "conversation": "first" } ] },
              { "id": "ghost", "area": { "circle": { "center": [20, 40], "radius": 1 } }, "trigger": "interact", "when": [ { "type": "flag", "flag": "never" } ],
                "opens": [ { "conversation": "first" } ] },
              { "id": "mat", "area": { "polygon": [[30, 5], [34, 5], [34, 9], [30, 9]] }, "trigger": "walkOver",
                "opens": [ { "conversation": "second", "when": [ { "type": "flag", "flag": "seen" } ] }, { "conversation": "first" } ] },
              { "id": "hidden_mat", "area": { "polygon": [[40, 5], [44, 5], [44, 9], [40, 9]] }, "trigger": "walkOver", "when": [ { "type": "flag", "flag": "never" } ],
                "opens": [ { "conversation": "first" } ] }
            ]
          } ]
        }
        """;

    private static GameSession Session()
    {
        var loaded = ContentPackLoader.Load(new MemoryContentSource([KeyValuePair.Create("pack.json", Pack), KeyValuePair.Create("all.json", All)]));
        Assert.True(loaded.IsOk, loaded.IsOk ? string.Empty : string.Join("\n", loaded.Error.Details));
        var session = TestSessions.Create(loaded.Value);
        Assert.True(session.NewGame().IsOk);
        return session;
    }

    private static string Started(GameSession session, Result<TownEvent> result)
    {
        Assert.True(result.IsOk, result.IsOk ? string.Empty : result.Error.Message);
        var started = Assert.IsType<TownEvent.ConversationStarted>(result.Value);
        Assert.Equal(started.ConversationId, session.Snapshot.Game!.Conversation!.ConversationId);
        return started.ConversationId;
    }

    [Fact]
    public void A_person_exactly_at_reach_opens_and_just_past_it_is_refused()
    {
        var session = Session();
        session.MovePlayer(10, 13);
        Assert.Equal("first", Started(session, session.TriggerPerson("ann")));
        session.Choose("bye");

        session.MovePlayer(10, 13.001);
        var before = session.Snapshot;
        Assert.Equal(ErrorCode.OUT_OF_REACH, session.TriggerPerson("ann").Error.Code);
        Assert.Same(before, session.Snapshot);
    }

    [Fact]
    public void An_interact_spot_counts_reach_to_its_area_edge_not_its_center()
    {
        var session = Session();

        session.MovePlayer(20, 34);
        Assert.Equal("first", Started(session, session.TriggerSpot("bench")));
        session.Choose("bye");

        session.MovePlayer(20, 34.5);
        Assert.Equal(ErrorCode.OUT_OF_REACH, session.TriggerSpot("bench").Error.Code);
    }

    [Fact]
    public void The_first_entry_whose_conditions_hold_is_opened_and_later_ones_are_skipped()
    {
        var session = Session();
        session.MovePlayer(10, 11);

        Assert.Equal("first", Started(session, session.TriggerPerson("ann")));
        session.Choose("bye");

        Assert.Equal("second", Started(session, session.TriggerPerson("ann")));
        session.Choose("bye");

        session.MovePlayer(20, 29);
        Assert.Equal("second", Started(session, session.TriggerSpot("bench")));
    }

    [Fact]
    public void A_spot_with_a_later_matching_entry_opens_it_while_earlier_ones_do_not_hold()
    {
        var session = Session();
        session.MovePlayer(20, 29);

        Assert.Equal("first", Started(session, session.TriggerSpot("bench")));
    }

    [Fact]
    public void A_person_who_stands_at_a_later_placement_is_reached_there_not_at_the_first()
    {
        var session = Session();
        session.MovePlayer(40, 41);
        var before = session.Snapshot;
        Assert.Equal(ErrorCode.OUT_OF_REACH, session.TriggerPerson("bo").Error.Code);
        Assert.Same(before, session.Snapshot);

        session.MovePlayer(50, 51);
        Assert.Equal("third", Started(session, session.TriggerPerson("bo")));
    }

    [Fact]
    public void A_hidden_interact_spot_cannot_be_triggered_even_standing_on_it()
    {
        var session = Session();
        session.MovePlayer(20, 40);
        var before = session.Snapshot;

        Assert.Equal(ErrorCode.TARGET_NOT_FOUND, session.TriggerSpot("ghost").Error.Code);
        Assert.Same(before, session.Snapshot);
    }

    [Fact]
    public void A_hidden_walk_over_spot_never_fires_when_crossed()
    {
        var session = Session();
        session.MovePlayer(38, 7);

        Assert.IsType<TownEvent.Nothing>(session.MovePlayer(42, 7).Value);
        Assert.IsType<TownEvent.Nothing>(session.MovePlayer(46, 7).Value);
        Assert.Null(session.Snapshot.Game!.Conversation);
    }

    [Fact]
    public void A_walk_over_spot_fires_only_on_the_move_from_outside_to_inside()
    {
        var session = Session();
        session.MovePlayer(28, 7);

        Assert.Equal("first", Started(session, session.MovePlayer(31, 7)));
        session.Choose("bye");

        Assert.IsType<TownEvent.Nothing>(session.MovePlayer(33, 8).Value);
        Assert.IsType<TownEvent.Nothing>(session.MovePlayer(28, 7).Value);
        Assert.Null(session.Snapshot.Game!.Conversation);

        Assert.Equal("second", Started(session, session.MovePlayer(30.5, 7)));
    }

    [Fact]
    public void A_move_that_jumps_across_a_walk_over_spot_without_landing_in_it_does_not_fire()
    {
        var session = Session();
        session.MovePlayer(25, 7);

        Assert.IsType<TownEvent.Nothing>(session.MovePlayer(38, 7).Value);
        Assert.Null(session.Snapshot.Game!.Conversation);
    }

    [Fact]
    public void Moving_within_a_walk_over_spot_or_leaving_it_opens_nothing_and_the_position_is_kept()
    {
        var session = Session();
        session.MovePlayer(28, 7);
        session.MovePlayer(31, 7);
        session.Choose("bye");

        Assert.IsType<TownEvent.Nothing>(session.MovePlayer(32, 6).Value);
        Assert.IsType<TownEvent.Nothing>(session.MovePlayer(36, 6).Value);
        Assert.Equal(new Point(36, 6), session.Snapshot.Game!.Town!.Position);
    }

    [Fact]
    public void A_walk_over_spot_cannot_be_triggered_by_hand_and_an_interact_spot_cannot_be_walked_into()
    {
        var session = Session();
        session.MovePlayer(31, 7);
        session.Choose("bye");
        var before = session.Snapshot;
        Assert.Equal(ErrorCode.VALIDATION_ERROR, session.TriggerSpot("mat").Error.Code);
        Assert.Same(before, session.Snapshot);

        session.MovePlayer(20, 28);
        Assert.IsType<TownEvent.Nothing>(session.MovePlayer(20, 30).Value);
        Assert.Null(session.Snapshot.Game!.Conversation);
    }

    [Fact]
    public void Unknown_people_and_spots_are_refused_and_the_game_is_unchanged()
    {
        var session = Session();
        session.MovePlayer(50, 51);
        var before = session.Snapshot;

        Assert.Equal(ErrorCode.TARGET_NOT_FOUND, session.TriggerPerson("nobody").Error.Code);
        Assert.Equal(ErrorCode.TARGET_NOT_FOUND, session.TriggerSpot("nothing").Error.Code);
        Assert.Same(before, session.Snapshot);
    }

    [Fact]
    public void Nothing_can_be_triggered_during_a_conversation_and_the_conversation_is_left_alone()
    {
        var session = Session();
        session.MovePlayer(10, 11);
        Started(session, session.TriggerPerson("ann"));
        var during = session.Snapshot;

        Assert.Equal(ErrorCode.CONVERSATION_ALREADY_ACTIVE, session.TriggerPerson("ann").Error.Code);
        Assert.Equal(ErrorCode.CONVERSATION_ALREADY_ACTIVE, session.TriggerPerson("bo").Error.Code);
        Assert.Equal(ErrorCode.CONVERSATION_ALREADY_ACTIVE, session.TriggerSpot("bench").Error.Code);
        Assert.Same(during, session.Snapshot);
    }

    [Fact]
    public void Walking_into_a_walk_over_spot_during_a_conversation_is_refused_and_walking_works_again_after_it_ends()
    {
        var session = Session();
        session.MovePlayer(10, 11);
        Started(session, session.TriggerPerson("ann"));
        var during = session.Snapshot;

        Assert.False(session.MovePlayer(31, 7).IsOk);
        Assert.Same(during, session.Snapshot);
        Assert.Equal(new Point(10, 11), session.Snapshot.Game!.Town!.Position);

        Assert.True(session.Choose("bye").IsOk);
        Assert.Null(session.Snapshot.Game!.Conversation);
        Assert.True(session.MovePlayer(15, 15).IsOk);
        Assert.Equal(new Point(15, 15), session.Snapshot.Game!.Town!.Position);
    }
}
