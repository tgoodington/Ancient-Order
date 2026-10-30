using AncientOrder.Rules.Common;
using AncientOrder.Rules.Content.Packs;
using AncientOrder.Rules.State;
using AncientOrder.Rules.Tests.Support;
using AncientOrder.Rules.Town;

namespace AncientOrder.Rules.Tests.Session;

// ES-1: a new game's player, party, town, and starting point come from content, and bad starting settings are refused when the pack loads.
public class NewGameFromContent
{
    private const string Pack = """{ "format": "ancient-order-content", "version": 1, "id": "t", "name": "Test", "standIn": false, "files": ["all.json"] }""";

    private static string All(string settingsExtra = "\"startingParty\": [\"bo\", \"ann\"], \"startingTown\": \"square\"") => $$"""
        {
          "settings": { "playerName": "Rowan", "lineup": { "min": 1, "max": 2 }, "playerCombat": { "rank": 3, "stamina": 100, "power": 80, "speed": 80, "elementalPath": "air" }, {{settingsExtra}} },
          "archetypes": [{ "id": "x" }],
          "characters": [
            { "id": "ann", "name": "Ann", "archetypeId": "x", "personality": { "patience": 20, "empathy": 20, "cunning": 10, "logic": 15, "kindness": 20, "charisma": 15 }, "startingAffection": 5, "startingTrust": -3 },
            { "id": "bo", "name": "Bo", "archetypeId": "x", "personality": { "patience": 20, "empathy": 20, "cunning": 10, "logic": 15, "kindness": 20, "charisma": 15 }, "startingAffection": 0, "startingTrust": 0, "combat": { "rank": 3, "stamina": 100, "power": 80, "speed": 80, "elementalPath": "fire" } }
          ],
          "towns": [
            {
              "id": "square", "name": "Market Square",
              "ground": [ [[0, 0], [20, 0], [20, 20], [0, 20]] ],
              "start": [3, 4],
              "people": [ { "character": "ann", "placements": [ { "at": [10, 10], "facing": 90 } ] } ]
            }
          ]
        }
        """;

    private static Result<GameContent> Load(string all) =>
        ContentPackLoader.Load(new MemoryContentSource([KeyValuePair.Create("pack.json", Pack), KeyValuePair.Create("all.json", all)]));

    private static GameContent Content(string all) => SessionAssert.Ok(Load(all));

    [Fact(DisplayName = "a new game takes the player from content")]
    public void Player_from_content()
    {
        var session = TestSessions.Create(Content(All()));
        SessionAssert.Ok(session.NewGame());

        var player = session.Snapshot.Game!.Player;
        Assert.Equal("Rowan", player.Name);
        Assert.Equal("player_fixed", player.Id);
        Assert.Equal(5, session.Snapshot.Game.Characters["ann"].Affection);
        Assert.Equal(-3, session.Snapshot.Game.Characters["ann"].Trust);
    }

    [Fact(DisplayName = "the party starts in startingParty order, with no lineup and no story memory")]
    public void Party_in_content_order()
    {
        var session = TestSessions.Create(Content(All()));
        SessionAssert.Ok(session.NewGame());
        var game = session.Snapshot.Game!;

        Assert.Equal(["bo", "ann"], game.Party);
        Assert.Empty(game.Lineup);
        Assert.Equal(Story.StoryMemory.Empty, game.Memory);
        Assert.Null(game.Conversation);
        Assert.Null(game.Combat);
        Assert.Null(game.HandOff);
    }

    [Fact(DisplayName = "the party is empty when content names none")]
    public void Party_empty_by_default()
    {
        var session = TestSessions.Create(Content(All("\"startingTown\": \"square\"")));
        SessionAssert.Ok(session.NewGame());

        Assert.Empty(session.Snapshot.Game!.Party);
    }

    [Fact(DisplayName = "the player starts in the starting town at its start point, and the town shows people at their placements and facing")]
    public void Town_and_start_point()
    {
        var session = TestSessions.Create(Content(All()));
        SessionAssert.Ok(session.NewGame());

        Assert.Equal(new TownPresence("square", new Point(3, 4)), session.Snapshot.Game!.Town);
        var town = session.Snapshot.Town!;
        Assert.Equal("Market Square", town.Name);
        Assert.Equal(new Point(3, 4), town.Player);
        var ann = Assert.Single(town.People);
        Assert.Equal("ann", ann.CharacterId);
        Assert.Equal(new Point(10, 10), ann.Position);
        Assert.Equal(90.0, ann.Facing);
    }

    [Fact(DisplayName = "without a starting town the new game has no town")]
    public void No_town_without_starting_town()
    {
        var session = TestSessions.Create(Content(All("\"startingParty\": [\"ann\"]")));
        SessionAssert.Ok(session.NewGame());

        Assert.Null(session.Snapshot.Game!.Town);
        Assert.Null(session.Snapshot.Town);
    }

    [Fact(DisplayName = "a second new game starts over from content")]
    public void New_game_starts_over()
    {
        var session = TestSessions.Create(Content(All()));
        SessionAssert.Ok(session.NewGame());
        SessionAssert.Ok(session.SetLineup(["bo"]));
        Assert.NotEmpty(session.Snapshot.Game!.Lineup);

        SessionAssert.Ok(session.NewGame());

        Assert.Empty(session.Snapshot.Game!.Lineup);
        Assert.Equal(["bo", "ann"], session.Snapshot.Game.Party);
        Assert.Equal(new Point(3, 4), session.Snapshot.Town!.Player);
    }

    [Fact(DisplayName = "an unknown starting town is refused at load, with its place")]
    public void Unknown_starting_town_refused()
    {
        var loaded = Load(All("\"startingTown\": \"nowhere\""));

        Assert.False(loaded.IsOk);
        Assert.Contains(loaded.Error.Details, d => d.Path == "all.json:/settings/startingTown" && d.Message.Contains("nowhere", StringComparison.Ordinal));
    }

    [Fact(DisplayName = "an unknown starting party member is refused at load, with its place")]
    public void Unknown_party_member_refused()
    {
        var loaded = Load(All("\"startingParty\": [\"ann\", \"ghost\"], \"startingTown\": \"square\""));

        Assert.False(loaded.IsOk);
        Assert.Contains(loaded.Error.Details, d => d.Path == "all.json:/settings/startingParty/1" && d.Message.Contains("ghost", StringComparison.Ordinal));
    }

    [Fact(DisplayName = "a duplicate starting party member is refused at load, with its place")]
    public void Duplicate_party_member_refused()
    {
        var loaded = Load(All("\"startingParty\": [\"ann\", \"ann\"], \"startingTown\": \"square\""));

        Assert.False(loaded.IsOk);
        Assert.Contains(loaded.Error.Details, d => d.Path == "all.json:/settings/startingParty/1");
    }
}
