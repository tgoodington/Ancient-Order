using AncientOrder.Rules.Common;
using AncientOrder.Rules.Content.Packs;
using AncientOrder.Rules.Reactions;
using AncientOrder.Rules.Session;
using AncientOrder.Rules.State;
using AncientOrder.Rules.Tests.Fixtures;
using AncientOrder.Rules.Tests.Support;
using AncientOrder.Rules.Town;

namespace AncientOrder.Rules.Tests.ContentPacks;

public class TownTests
{
    private const string Pack = """{ "format": "ancient-order-content", "version": 1, "id": "t", "name": "Test", "standIn": false, "files": ["base.json", "town.json"] }""";

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
              "steps": [ { "id": "hello", "lines": [], "choices": [
                { "id": "mark", "text": "Mark", "effects": [ { "type": "setFlag", "flag": "met" } ] },
                { "id": "kind", "text": "Kind", "effects": [ { "type": "relationship", "character": "ann", "affection": 60, "trust": 0 } ] },
                { "id": "join", "text": "Join", "effects": [ { "type": "join", "character": "bo" } ] }
              ] } ] }
          ]
        }
        """;

    private const string TownFile = """
        {
          "towns": [
            {
              "id": "square", "name": "Market Square",
              "ground": [ [[0, 0], [20, 0], [20, 20], [0, 20]], [[20, 5], [30, 5], [30, 10], [20, 10]] ],
              "obstacles": [ { "id": "fountain", "footprint": [[8, 8], [12, 8], [12, 12], [8, 12]], "height": 1.5, "label": "Fountain" } ],
              "start": [1, 1],
              "people": [
                { "character": "ann", "placements": [
                    { "at": [15, 15], "facing": 90, "when": [ { "type": "flag", "flag": "met" } ] },
                    { "at": [5, 5] } ] },
                { "character": "bo", "placements": [ { "at": [18, 3], "when": [ { "type": "inParty", "character": "bo" } ] } ] }
              ],
              "spots": [
                { "id": "well", "name": "Well", "area": { "circle": { "center": [2, 2], "radius": 1 } }, "trigger": "interact" },
                { "id": "gate", "area": { "polygon": [[18, 0], [20, 0], [20, 2], [18, 2]] }, "trigger": "walkOver", "when": [ { "type": "flag", "flag": "met" } ] }
              ]
            },
            { "id": "farm", "name": "Farm", "ground": [ [[0, 0], [5, 0], [5, 5]] ], "start": [1, 1], "reach": 4 }
          ]
        }
        """;

    private static MemoryContentSource Memory(string town, string baseFile = Base) =>
        new([KeyValuePair.Create("pack.json", Pack), KeyValuePair.Create("base.json", baseFile), KeyValuePair.Create("town.json", town)]);

    private static GameContent Content(string town = TownFile)
    {
        var loaded = ContentPackLoader.Load(Memory(town));
        Assert.True(loaded.IsOk, loaded.IsOk ? string.Empty : string.Join("\n", loaded.Error.Details));
        return loaded.Value;
    }

    private static GameSession Session()
    {
        var session = TestSessions.Create(Content());
        Assert.True(session.NewGame().IsOk);
        return session;
    }

    private static string[] Problems(string town) =>
        ContentPackLoader.Load(Memory(town, Base.Replace(", \"startingTown\": \"square\"", string.Empty, StringComparison.Ordinal))) is { IsOk: false } loaded ? [.. loaded.Error.Details.Select(d => d.Path)] : throw new InvalidOperationException("expected a refusal");

    [Fact]
    public void A_new_game_starts_in_the_starting_town_at_its_start_with_the_town_in_content_order()
    {
        var session = Session();
        var town = session.Snapshot.Town!;

        Assert.Equal("square", session.Snapshot.Game!.Town!.TownId);
        Assert.Equal(new Point(1, 1), town.Player);
        Assert.Equal("Market Square", town.Name);
        Assert.Equal(2, town.Ground.Length);
        Assert.Equal("Fountain", Assert.Single(town.Obstacles).Label);
        Assert.Equal(2.0, town.Reach);
        Assert.Equal(["ann"], town.People.Select(p => p.CharacterId));
        Assert.Equal(new Point(5, 5), town.People[0].Position);
        Assert.Equal("Ann", town.People[0].Name);
        Assert.Equal(Demeanor.Neutral, town.People[0].Demeanor);
        Assert.Equal(["well"], town.Spots.Select(s => s.Id));
        Assert.True(town.Spots[0].Shown);
    }

    [Fact]
    public void Presence_follows_flags_relationships_and_the_party_after_every_change()
    {
        var session = Session();
        session.StartConversation("chat");

        session.Choose("kind");
        Assert.Equal(Demeanor.Warm, session.Snapshot.Town!.People.Single().Demeanor);

        session.StartConversation("chat");
        session.Choose("join");
        Assert.Equal(["ann", "bo"], session.Snapshot.Town!.People.Select(p => p.CharacterId));

        session.StartConversation("chat");
        session.Choose("mark");
        var town = session.Snapshot.Town!;
        Assert.Equal(new Point(15, 15), town.People[0].Position);
        Assert.Equal(90, town.People[0].Facing);
        Assert.Equal(["well", "gate"], town.Spots.Select(s => s.Id));
        Assert.False(town.Spots[1].Shown);
    }

    [Fact]
    public void Move_player_changes_the_position_only_on_walkable_ground()
    {
        var session = Session();

        Assert.IsType<TownEvent.Nothing>(session.MovePlayer(25, 7).Value);
        Assert.Equal(new Point(25, 7), session.Snapshot.Town!.Player);
        Assert.Equal(new Point(25, 7), session.Snapshot.Game!.Town!.Position);
        Assert.True(session.MovePlayer(20, 7).IsOk);
        Assert.True(session.MovePlayer(0, 0).IsOk);
    }

    [Fact]
    public void Move_player_is_refused_off_ground_in_an_obstacle_and_on_its_edge_leaving_the_game_unchanged()
    {
        var session = Session();
        var before = session.Snapshot;

        foreach (var (x, z) in new[] { (-1.0, 1.0), (25.0, 15.0), (10.0, 10.0), (8.0, 10.0), (12.0, 12.0) })
        {
            var refused = session.MovePlayer(x, z);
            Assert.Equal(ErrorCode.NOT_WALKABLE, refused.Error.Code);
        }

        Assert.Equal(ErrorCode.VALIDATION_ERROR, session.MovePlayer(double.NaN, 1).Error.Code);
        Assert.Equal(ErrorCode.VALIDATION_ERROR, session.MovePlayer(1, double.PositiveInfinity).Error.Code);
        Assert.Same(before, session.Snapshot);
    }

    [Fact]
    public void Move_player_is_refused_during_a_conversation_without_a_town_and_without_a_game()
    {
        var session = Session();
        session.StartConversation("chat");
        var during = session.Snapshot;
        Assert.Equal(ErrorCode.CONVERSATION_ALREADY_ACTIVE, session.MovePlayer(3, 3).Error.Code);
        Assert.Same(during, session.Snapshot);

        var noTown = TestSessions.Create(TestContent.Create());
        Assert.Equal(ErrorCode.GAME_NOT_FOUND, noTown.MovePlayer(1, 1).Error.Code);
        noTown.NewGame();
        Assert.Null(noTown.Snapshot.Town);
        Assert.Equal(ErrorCode.TOWN_NOT_ACTIVE, noTown.MovePlayer(1, 1).Error.Code);
    }

    [Fact]
    public void Helpers_answer_inside_and_reach_with_squared_distances_and_a_towns_own_reach()
    {
        var town = Session().Snapshot.Town!;

        Assert.True(town.IsInside("well", 3, 2));
        Assert.False(town.IsInside("well", 3.01, 2));
        Assert.False(town.IsInside("gate", 19, 1));
        Assert.True(town.InReach("ann", 5, 7));
        Assert.False(town.InReach("ann", 5, 7.01));
        Assert.True(town.InReach("well", 5, 2));
        Assert.False(town.InReach("well", 5.01, 2));
        Assert.False(town.InReach("nobody", 5, 5));

        var game = Session().Snapshot.Game!;
        var content = Content();
        var farm = TownView.From(content, game with { Town = new TownPresence("farm", new Point(1, 1)) })!;
        Assert.Equal(4, farm.Reach);
    }

    [Fact]
    public void Geometry_counts_edges_inside_and_handles_concave_shapes()
    {
        var l = new Polygon([new(0, 0), new(4, 0), new(4, 2), new(2, 2), new(2, 4), new(0, 4)]);

        Assert.True(Geometry.Contains(l, new Point(1, 3)));
        Assert.True(Geometry.Contains(l, new Point(3, 1)));
        Assert.False(Geometry.Contains(l, new Point(3, 3)));
        Assert.True(Geometry.Contains(l, new Point(4, 1)));
        Assert.True(Geometry.Contains(l, new Point(0, 0)));
        Assert.True(Geometry.Contains(l, new Point(3, 2)));
        Assert.False(Geometry.Contains(l, new Point(5, 1)));
        Assert.Equal(1, Geometry.DistanceSquared(l, new Point(5, 1)));
        Assert.Equal(0, Geometry.DistanceSquared(l, new Point(1, 1)));
    }

    [Fact]
    public void Content_refusals_name_the_file_and_place()
    {
        Assert.Equal(["town.json:/towns/0/ground/0"], Problems("""{ "towns": [ { "id": "a", "name": "A", "ground": [ [[0, 0], [1, 1]] ], "start": [0, 0] } ] }"""));
        Assert.Equal(["town.json:/towns/0/start"], Problems("""{ "towns": [ { "id": "a", "name": "A", "ground": [ [[0, 0], [1, 0], [1, 1]] ], "start": [0] } ] }"""));
        Assert.Equal(
            ["town.json:/towns/0/spots/0/area"],
            Problems("""{ "towns": [ { "id": "a", "name": "A", "ground": [ [[0, 0], [1, 0], [1, 1]] ], "start": [0, 0], "spots": [ { "id": "s", "area": {}, "trigger": "interact" } ] } ] }"""));
        Assert.Equal(
            ["town.json:/towns/0/spots/1/id", "town.json:/towns/0/spots/1/area/circle/radius"],
            Problems("""{ "towns": [ { "id": "a", "name": "A", "ground": [ [[0, 0], [1, 0], [1, 1]] ], "start": [0, 0], "spots": [ { "id": "s", "area": { "circle": { "center": [0, 0], "radius": 1 } }, "trigger": "interact" }, { "id": "s", "area": { "circle": { "center": [0, 0], "radius": 0 } }, "trigger": "interact" } ] } ] }"""));
        Assert.Equal(
            ["town.json:/towns/0/people/0/character", "town.json:/towns/0/people/0/placements/0/when/0/flag"],
            Problems("""{ "towns": [ { "id": "a", "name": "A", "ground": [ [[0, 0], [1, 0], [1, 1]] ], "start": [0, 0], "people": [ { "character": "zed", "placements": [ { "at": [0, 0], "when": [ { "type": "flag", "flag": "" } ] } ] } ] } ] }"""));
        Assert.Equal(["town.json:/towns/0/obstacles/0/height"], Problems("""{ "towns": [ { "id": "a", "name": "A", "ground": [ [[0, 0], [1, 0], [1, 1]] ], "start": [0, 0], "obstacles": [ { "id": "o", "footprint": [[0, 0], [1, 0], [1, 1]], "height": 0 } ] } ] }"""));
        Assert.Equal(["town.json:/towns/1/id"], Problems("""{ "towns": [ { "id": "a", "name": "A", "ground": [ [[0, 0], [1, 0], [1, 1]] ], "start": [0, 0] }, { "id": "a", "name": "A", "ground": [ [[0, 0], [1, 0], [1, 1]] ], "start": [0, 0] } ] }"""));
    }

    [Fact]
    public void An_unknown_starting_town_is_refused()
    {
        var loaded = ContentPackLoader.Load(Memory(TownFile, Base.Replace("\"startingTown\": \"square\"", "\"startingTown\": \"nowhere\"", StringComparison.Ordinal)));

        Assert.False(loaded.IsOk);
        Assert.Contains("base.json:/settings/startingTown", loaded.Error.Details.Select(d => d.Path));
    }
}
