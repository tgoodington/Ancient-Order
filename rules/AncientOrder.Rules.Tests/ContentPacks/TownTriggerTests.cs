using AncientOrder.Rules.Common;
using AncientOrder.Rules.Content.Packs;
using AncientOrder.Rules.Session;
using AncientOrder.Rules.Tests.Fixtures;
using AncientOrder.Rules.Tests.Support;
using AncientOrder.Rules.Town;

namespace AncientOrder.Rules.Tests.ContentPacks;

public class TownTriggerTests
{
    private const string Pack = """{ "format": "ancient-order-content", "version": 1, "id": "t", "name": "Test", "standIn": false, "files": ["base.json", "town.json"] }""";

    private const string Rat = """{ "id": "rat", "name": "Rat", "archetype": "x", "rank": 1, "stamina": 10, "power": 5, "speed": 5, "elementalPath": "fire", "reactionSkills": { "block": { "sr": 1, "smr": 1, "fmr": 1 }, "dodge": { "sr": 1, "smr": 1, "fmr": 1 }, "parry": { "sr": 1, "smr": 1, "fmr": 1 } } }""";

    private const string Base = """
        {
          "settings": { "playerName": "Kael", "lineup": { "min": 1, "max": 1 }, "startingTown": "square" },
          "archetypes": [{ "id": "x" }],
          "characters": [
            { "id": "ann", "name": "Ann", "archetypeId": "x", "personality": { "patience": 20, "empathy": 20, "cunning": 10, "logic": 15, "kindness": 20, "charisma": 15 }, "startingAffection": 0, "startingTrust": 0 },
            { "id": "bo", "name": "Bo", "archetypeId": "x", "personality": { "patience": 20, "empathy": 20, "cunning": 10, "logic": 15, "kindness": 20, "charisma": 15 }, "startingAffection": 0, "startingTrust": 0 }
          ],
          "conversations": [
            { "id": "chat", "participants": ["ann"], "start": [{ "step": "hello" }],
              "steps": [ { "id": "hello", "lines": [], "choices": [ { "id": "mark", "text": "Mark", "effects": [ { "type": "setFlag", "flag": "met" } ] } ] } ] },
            { "id": "late", "participants": ["ann"], "start": [{ "step": "hello" }],
              "steps": [ { "id": "hello", "lines": [], "choices": [ { "id": "ok", "text": "Ok" } ] } ] }
          ],
          "fights": [ { "id": "brawl", "name": "Brawl", "enemies": [ RAT ] } ]
        }
        """;

    private const string TownFile = """
        {
          "towns": [ {
            "id": "square", "name": "Square",
            "ground": [ [[0, 0], [40, 0], [40, 40], [0, 40]] ],
            "start": [1, 1],
            "people": [
              { "character": "ann", "placements": [ { "at": [5, 5] } ],
                "opens": [ { "conversation": "late", "when": [ { "type": "flag", "flag": "met" } ] }, { "conversation": "chat" } ] },
              { "character": "bo", "placements": [ { "at": [30, 30] } ],
                "opens": [ { "conversation": "chat", "when": [ { "type": "flag", "flag": "never" } ] } ] }
            ],
            "spots": [
              { "id": "well", "area": { "circle": { "center": [2, 20], "radius": 1 } }, "trigger": "interact", "opens": [ { "conversation": "chat" } ] },
              { "id": "post", "area": { "circle": { "center": [35, 35], "radius": 1 } }, "trigger": "interact", "opens": [ { "handOff": "brawl" } ] },
              { "id": "quiet", "area": { "polygon": [[10, 10], [14, 10], [14, 14], [10, 14]] }, "trigger": "walkOver" },
              { "id": "gate", "area": { "polygon": [[10, 10], [14, 10], [14, 14], [10, 14]] }, "trigger": "walkOver", "opens": [ { "conversation": "chat", "when": [ { "type": "flag", "flag": "never" } ] }, { "conversation": "chat" } ] }
            ]
          } ]
        }
        """;

    private static MemoryContentSource Memory(string town) =>
        new([KeyValuePair.Create("pack.json", Pack), KeyValuePair.Create("base.json", Base.Replace("RAT", Rat, StringComparison.Ordinal)), KeyValuePair.Create("town.json", town)]);

    private static GameSession Session()
    {
        var loaded = ContentPackLoader.Load(Memory(TownFile));
        Assert.True(loaded.IsOk, loaded.IsOk ? string.Empty : string.Join("\n", loaded.Error.Details));
        var session = TestSessions.Create(loaded.Value);
        Assert.True(session.NewGame().IsOk);
        return session;
    }

    private static string[] Problems(string town) =>
        ContentPackLoader.Load(Memory(town)) is { IsOk: false } loaded ? [.. loaded.Error.Details.Select(d => d.Path)] : throw new InvalidOperationException("expected a refusal");

    [Fact]
    public void A_person_opens_the_first_entry_that_holds_as_the_story_moves()
    {
        var session = Session();
        session.MovePlayer(5, 6);

        Assert.Equal("chat", Assert.IsType<TownEvent.ConversationStarted>(session.TriggerPerson("ann").Value).ConversationId);
        Assert.Equal("chat", session.Snapshot.Game!.Conversation!.ConversationId);
        session.Choose("mark");

        Assert.Equal("late", Assert.IsType<TownEvent.ConversationStarted>(session.TriggerPerson("ann").Value).ConversationId);
    }

    [Fact]
    public void Nothing_holding_does_nothing_and_leaves_the_game_unchanged()
    {
        var session = Session();
        session.MovePlayer(30, 29);
        var before = session.Snapshot;

        Assert.IsType<TownEvent.Nothing>(session.TriggerPerson("bo").Value);
        Assert.Same(before.Game, session.Snapshot.Game);
    }

    [Fact]
    public void Triggering_needs_reach_and_squared_distance_decides_it_with_the_edge_counting()
    {
        var session = Session();
        var before = session.Snapshot;
        Assert.Equal(ErrorCode.OUT_OF_REACH, session.TriggerPerson("ann").Error.Code);
        Assert.Equal(ErrorCode.OUT_OF_REACH, session.TriggerSpot("well").Error.Code);
        Assert.Same(before, session.Snapshot);

        session.MovePlayer(5, 7);
        Assert.IsType<TownEvent.ConversationStarted>(session.TriggerPerson("ann").Value);
        session.Choose("mark");
        session.MovePlayer(5, 7.01);
        Assert.Equal(ErrorCode.OUT_OF_REACH, session.TriggerPerson("ann").Error.Code);

        session.MovePlayer(2, 21.5);
        Assert.IsType<TownEvent.ConversationStarted>(session.TriggerSpot("well").Value);
    }

    [Fact]
    public void Triggers_are_refused_for_missing_targets_walk_over_spots_running_conversations_and_no_town()
    {
        var session = Session();
        session.MovePlayer(5, 6);
        var before = session.Snapshot;
        Assert.Equal(ErrorCode.TARGET_NOT_FOUND, session.TriggerPerson("zed").Error.Code);
        Assert.Equal(ErrorCode.TARGET_NOT_FOUND, session.TriggerSpot("ann").Error.Code);
        Assert.Equal(ErrorCode.VALIDATION_ERROR, session.TriggerSpot("gate").Error.Code);
        Assert.Same(before, session.Snapshot);

        session.TriggerPerson("ann");
        var during = session.Snapshot;
        Assert.Equal(ErrorCode.CONVERSATION_ALREADY_ACTIVE, session.TriggerPerson("ann").Error.Code);
        Assert.Equal(ErrorCode.CONVERSATION_ALREADY_ACTIVE, session.TriggerSpot("well").Error.Code);
        Assert.Same(during, session.Snapshot);

        var noTown = TestSessions.Create(TestContent.Create());
        Assert.Equal(ErrorCode.GAME_NOT_FOUND, noTown.TriggerPerson("ann").Error.Code);
        noTown.NewGame();
        Assert.Equal(ErrorCode.TOWN_NOT_ACTIVE, noTown.TriggerPerson("ann").Error.Code);
        Assert.Equal(ErrorCode.TOWN_NOT_ACTIVE, noTown.TriggerSpot("well").Error.Code);
    }

    [Fact]
    public void A_walk_over_spot_fires_on_entering_not_on_moves_inside_and_is_not_blocked_by_an_empty_spot()
    {
        var session = Session();

        Assert.IsType<TownEvent.Nothing>(session.MovePlayer(9, 12).Value);
        Assert.Null(session.Snapshot.Game!.Conversation);

        Assert.Equal("chat", Assert.IsType<TownEvent.ConversationStarted>(session.MovePlayer(12, 12).Value).ConversationId);
        Assert.Equal(new Point(12, 12), session.Snapshot.Game!.Town!.Position);
        session.Choose("mark");

        Assert.IsType<TownEvent.Nothing>(session.MovePlayer(13, 13).Value);
        Assert.Null(session.Snapshot.Game!.Conversation);

        session.MovePlayer(20, 20);
        Assert.IsType<TownEvent.ConversationStarted>(session.MovePlayer(10, 10).Value);
    }

    [Fact]
    public void The_town_view_marks_people_and_spots_with_something_to_open_and_a_hand_off_counts()
    {
        var town = Session().Snapshot.Town!;

        Assert.Equal([true, false], town.People.Select(p => p.CanOpen));
        Assert.Equal(["well", "post", "quiet", "gate"], town.Spots.Select(s => s.Id));
        Assert.Equal([true, true, false, true], town.Spots.Select(s => s.CanOpen));
    }

    [Fact]
    public void The_loader_checks_opens_entries()
    {
        var shapes = Problems("""
            { "towns": [ { "id": "a", "name": "A", "ground": [ [[0, 0], [9, 0], [9, 9]] ], "start": [1, 1],
              "people": [ { "character": "ann", "placements": [ { "at": [1, 1] } ], "opens": [ { "conversation": "chat", "handOff": "brawl" } ] } ],
              "spots": [ { "id": "s", "area": { "circle": { "center": [1, 1], "radius": 1 } }, "trigger": "interact", "opens": [ {} ] } ] } ] }
            """);
        Assert.Contains("town.json:/towns/0/people/0/opens/0", shapes);
        Assert.Contains("town.json:/towns/0/spots/0/opens/0", shapes);

        var unknown = Problems("""
            { "towns": [ { "id": "a", "name": "A", "ground": [ [[0, 0], [9, 0], [9, 9]] ], "start": [1, 1],
              "people": [ { "character": "ann", "placements": [ { "at": [1, 1] } ], "opens": [ { "conversation": "nope" } ] } ] } ] }
            """);
        Assert.Contains(unknown, p => p.Contains("opens/0", StringComparison.Ordinal));
    }
}
