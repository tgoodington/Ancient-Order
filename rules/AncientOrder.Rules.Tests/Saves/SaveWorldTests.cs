using System.Collections.Immutable;
using System.Text.Json.Nodes;
using AncientOrder.Rules.Common;
using AncientOrder.Rules.Content.Packs;
using AncientOrder.Rules.Conversations;
using AncientOrder.Rules.HandOff;
using AncientOrder.Rules.Saves;
using AncientOrder.Rules.Saves.Format;
using AncientOrder.Rules.State;
using AncientOrder.Rules.Story;
using AncientOrder.Rules.Tests.Fixtures;
using AncientOrder.Rules.Tests.Support;
using AncientOrder.Rules.Town;
using static AncientOrder.Rules.Tests.Support.SaveFixtures;

namespace AncientOrder.Rules.Tests.Saves;

// Version 2 saves: the town, conversation, story memory, party, hand-off, and the envelope summary, and how a load fits them to changed content.
public class SaveWorldTests
{
    private const string Pack = """{ "format": "ancient-order-content", "version": 1, "id": "t", "name": "Test", "standIn": false, "files": ["base.json"] }""";

    private const string Rat = """{ "id": "rat", "name": "Rat", "archetype": "x", "rank": 1, "stamina": 10, "power": 5, "speed": 5, "elementalPath": "fire", "reactionSkills": { "block": { "sr": 1, "smr": 1, "fmr": 1 }, "dodge": { "sr": 1, "smr": 1, "fmr": 1 }, "parry": { "sr": 1, "smr": 1, "fmr": 1 } } }""";

    private const string Combat = """{ "rank": 1, "stamina": 50, "power": 10, "speed": 10, "elementalPath": "fire" }""";

    private const string Person = "\"personality\": { \"patience\": 20, \"empathy\": 20, \"cunning\": 10, \"logic\": 15, \"kindness\": 20, \"charisma\": 15 }, \"startingAffection\": 0, \"startingTrust\": 0";

    private const string Square = """{ "id": "square", "name": "Square", "ground": [ [[0, 0], [40, 0], [40, 40], [0, 40]] ], "start": [1, 1] }""";

    private const string Dock = """{ "id": "dock", "name": "The Dock", "ground": [ [[100, 100], [140, 100], [140, 140], [100, 140]] ], "start": [101, 101] }""";

    private const string Brawl = """{ "id": "brawl", "name": "The Brawl", "enemies": [ RAT ] }""";

    private const string Melee = """{ "id": "melee", "name": "The Melee", "lineup": { "min": 2, "max": 2 }, "enemies": [ RAT ] }""";

    private const string Template = """
        {
          "settings": { "playerName": "Kael", "lineup": { "min": 1, "max": 2 }, "startingTown": "square", "startingParty": ["ann", "bo"] },
          "archetypes": [{ "id": "x" }],
          "characters": [
            { "id": "ann", "name": "Ann", "archetypeId": "x", PERSON, "combat": COMBAT },
            { "id": "bo", "name": "Bo", "archetypeId": "x", PERSON, "combat": COMBAT }
          ],
          "conversations": [
            { "id": "talk", "participants": ["ann"], "start": [{ "step": "hello" }],
              "steps": [
                { "id": "hello", "lines": [ { "speaker": "ann", "text": "Hi" } ], "choices": [
                  { "id": "go", "text": "Go", "effects": [ { "type": "setFlag", "flag": "met" } ], "next": [ { "step": "bye" } ] } ] },
                { "id": "bye", "lines": [] }
              ] },
            { "id": "other", "participants": [], "steps": [ { "id": "s", "lines": [] } ] }
          ],
          "fights": [ FIGHTS ],
          "towns": [ TOWNS ]
        }
        """;

    private static GameContent Load(string towns = Square + "," + Dock, string fights = Brawl + "," + Melee, string? replaceFrom = null, string? replaceTo = null)
    {
        var text = Template
            .Replace("TOWNS", towns, StringComparison.Ordinal)
            .Replace("FIGHTS", fights, StringComparison.Ordinal)
            .Replace("PERSON", Person, StringComparison.Ordinal)
            .Replace("COMBAT", Combat, StringComparison.Ordinal)
            .Replace("RAT", Rat, StringComparison.Ordinal);
        if (replaceFrom is not null)
        {
            text = text.Replace(replaceFrom, replaceTo, StringComparison.Ordinal);
        }

        var loaded = ContentPackLoader.Load(new MemoryContentSource([KeyValuePair.Create("pack.json", Pack), KeyValuePair.Create("base.json", text)]));
        Assert.True(loaded.IsOk, loaded.IsOk ? string.Empty : string.Join("\n", loaded.Error.Details));
        return loaded.Value;
    }

    private static GameContent Full { get; } = Load();

    private static GameState Played(GameContent content) =>
        TestContent.NewGame(content) with
        {
            Lineup = ["bo"],
            Town = new TownPresence("square", new Point(12.5, 7.25)),
            Memory = StoryMemory.Empty.WithFlag("met").WithFlag("angry").WithChosen("talk", "go").WithSeen("talk"),
            Characters = TestContent.NewGame(content).Characters.SetItem("ann", new CharacterState(33.5, -12)),
        };

    private static GameState Talking(GameContent content) =>
        Played(content) with { Conversation = new ActiveConversation("talk", "bye") };

    private static GameState HandedOff(GameContent content) =>
        Played(content) with { HandOff = new FightHandOff("brawl", ["bo"], "square") };

    private static LoadedSave RoundTrip(GameState game, GameContent savedUnder, GameContent? loadedUnder = null) =>
        ReadOk(WriteOk(game, savedUnder), loadedUnder ?? savedUnder);

    private static void AssertSameWorld(GameState expected, GameState actual)
    {
        Assert.Equal(expected.Party, actual.Party);
        Assert.Equal(expected.Lineup, actual.Lineup);
        Assert.Equal(expected.Town, actual.Town);
        Assert.Equal(expected.Conversation, actual.Conversation);
        Assert.Equal(expected.HandOff?.FightId, actual.HandOff?.FightId);
        Assert.Equal(expected.HandOff?.Lineup, actual.HandOff?.Lineup);
        Assert.Equal(expected.HandOff?.TownId, actual.HandOff?.TownId);
        Assert.Equal(expected.Player, actual.Player);
        Assert.Equal(expected.Characters["ann"], actual.Characters["ann"]);
        Assert.True(expected.Memory.Flags.SetEquals(actual.Memory.Flags));
        Assert.True(expected.Memory.Chosen.SetEquals(actual.Memory.Chosen));
        Assert.True(expected.Memory.Seen.SetEquals(actual.Memory.Seen));
    }

    [Fact(DisplayName = "a game in the town round-trips its town, position, party, lineup, relationships, and memory with an empty report")]
    public void Town_round_trip()
    {
        var game = Played(Full);
        var loaded = RoundTrip(game, Full);
        AssertSameWorld(game, loaded.Game);
        Assert.True(loaded.Report.IsEmpty);
        Assert.Equal(new TownPresence("square", new Point(12.5, 7.25)), loaded.Game.Town);
    }

    [Fact(DisplayName = "a save made during a conversation resumes at the same step")]
    public void Conversation_round_trip()
    {
        var game = Talking(Full);
        var loaded = RoundTrip(game, Full);
        AssertSameWorld(game, loaded.Game);
        Assert.True(loaded.Report.IsEmpty);
        Assert.Equal(new ActiveConversation("talk", "bye"), loaded.Game.Conversation);
    }

    [Fact(DisplayName = "a recorded hand-off round-trips")]
    public void Hand_off_round_trip()
    {
        var game = HandedOff(Full);
        var loaded = RoundTrip(game, Full);
        AssertSameWorld(game, loaded.Game);
        Assert.True(loaded.Report.IsEmpty);
        Assert.Equal(new FightHandOff("brawl", ["bo"], "square").FightId, loaded.Game.HandOff!.FightId);
    }

    [Fact(DisplayName = "a hand-off made with no town round-trips its null town")]
    public void Hand_off_without_a_town()
    {
        var game = Played(Full) with { Town = null, HandOff = new FightHandOff("brawl", ["bo"], null) };
        var doc = Doc(game, Full);
        Assert.Null(doc["game"]!["handOff"]!["town"]);
        Assert.Null(RoundTrip(game, Full).Game.HandOff!.TownId);
    }

    [Fact(DisplayName = "a session load publishes the conversation view at the saved step")]
    public async Task Session_load_reopens_the_conversation()
    {
        var backend = new MemorySaveBackend();
        var first = SessionOver(backend, Full);
        Assert.True(first.NewGame().IsOk);
        Assert.True(first.StartConversation("talk").IsOk);
        Assert.True(first.Choose("go").IsOk);
        Assert.Equal("bye", first.Snapshot.Conversation!.StepId);
        Assert.True((await first.SaveToSlot(1)).IsOk);

        var second = SessionOver(backend, Full);
        var report = SessionAssert.Ok(await second.LoadFromSlot(1));
        Assert.True(report.IsEmpty);
        Assert.Equal("bye", second.Snapshot.Conversation!.StepId);
        Assert.Equal("square", second.Snapshot.Town!.Id);
        Assert.True(second.Snapshot.Game!.Memory.HasFlag("met"));
    }

    [Fact(DisplayName = "saving the same game twice gives identical bytes, whatever order the memory was built in")]
    public void Identical_bytes()
    {
        var forward = StoryMemory.Empty.WithFlag("zeta").WithFlag("alpha").WithFlag("mid")
            .WithChosen("talk", "b").WithChosen("other", "z").WithChosen("talk", "a")
            .WithSeen("talk").WithSeen("other");
        var backward = StoryMemory.Empty.WithSeen("other").WithSeen("talk")
            .WithChosen("talk", "a").WithChosen("other", "z").WithChosen("talk", "b")
            .WithFlag("mid").WithFlag("alpha").WithFlag("zeta");
        var a = Played(Full) with { Memory = forward };
        var b = Played(Full) with { Memory = backward };

        var bytes = WriteOk(a, Full);
        Assert.Equal(bytes, WriteOk(a, Full));
        Assert.Equal(bytes, WriteOk(b, Full));

        var memory = JsonNode.Parse(bytes)!["game"]!["memory"]!;
        Assert.Equal(["alpha", "mid", "zeta"], memory["flags"]!.AsArray().Select(n => (string?)n));
        Assert.Equal(["other", "talk"], memory["seen"]!.AsArray().Select(n => (string?)n));
        Assert.Equal(
            ["other/z", "talk/a", "talk/b"],
            memory["chosen"]!.AsArray().Select(n => (string?)n!["conversation"] + "/" + (string?)n["choice"]));
    }

    [Fact(DisplayName = "party order is kept as saved, not sorted")]
    public void Party_order_is_kept()
    {
        var game = Played(Full) with { Party = ["bo", "ann"], Lineup = [] };
        Assert.Equal(["bo", "ann"], RoundTrip(game, Full).Game.Party);
    }

    private static JsonObject VersionOne(GameState game, GameContent content)
    {
        var doc = Doc(game, content);
        doc["version"] = 1;
        doc.Remove("summary");
        var g = (JsonObject)doc["game"]!;
        foreach (var key in new[] { "party", "town", "conversation", "memory", "handOff" })
        {
            g.Remove(key);
        }

        return doc;
    }

    [Fact(DisplayName = "a version 1 save loads through the upgrade: its party is its lineup, memory is empty, and the player starts in the starting town")]
    public void Upgrade_from_version_one()
    {
        var game = Played(Full) with { Memory = StoryMemory.Empty, Town = null };
        var read = Read(VersionOne(game, Full), Full);
        Assert.True(read.IsOk, read.IsOk ? string.Empty : read.Error.Message);
        var loaded = read.Value;

        Assert.Equal(["bo"], loaded.Game.Party);
        Assert.Equal(["bo"], loaded.Game.Lineup);
        Assert.Empty(loaded.Game.Memory.Flags);
        Assert.Empty(loaded.Game.Memory.Chosen);
        Assert.Empty(loaded.Game.Memory.Seen);
        Assert.Null(loaded.Game.Conversation);
        Assert.Null(loaded.Game.HandOff);
        Assert.Equal(new TownPresence("square", new Point(1, 1)), loaded.Game.Town);
        var reset = Assert.Single(loaded.Report.Adjustments);
        Assert.Equal(LoadAdjustmentKind.TownReset, reset.Kind);
        Assert.Equal(SaveSituation.None, loaded.Summary.Situation);
        Assert.Null(loaded.Summary.TownName);
        Assert.Equal(2, (int)JsonNode.Parse(WriteOk(loaded.Game, Full))!["version"]!);
    }

    [Fact(DisplayName = "a version 1 save loads in content with no starting town and puts the player nowhere without a report")]
    public void Upgrade_without_a_starting_town()
    {
        var noTown = Load(replaceFrom: "\"startingTown\": \"square\", ", replaceTo: string.Empty);
        var loaded = ReadOk(Utf8(VersionOne(TestContent.NewGame(noTown), noTown)), noTown);
        Assert.Null(loaded.Game.Town);
        Assert.True(loaded.Report.IsEmpty);
    }

    [Fact(DisplayName = "a town no longer in the content puts the player at the starting town's start")]
    public void Missing_town_resets()
    {
        var game = Played(Full) with { Town = new TownPresence("dock", new Point(110, 110)) };
        var loaded = RoundTrip(game, Full, Load(towns: Square));
        Assert.Equal(new TownPresence("square", new Point(1, 1)), loaded.Game.Town);
        var adjustment = Assert.Single(loaded.Report.Adjustments);
        Assert.Equal(LoadAdjustmentKind.TownReset, adjustment.Kind);
        Assert.Equal("dock", adjustment.Id);
    }

    [Fact(DisplayName = "a position the town no longer allows moves the player to the town's start")]
    public void Unwalkable_position_resets()
    {
        var game = Played(Full) with { Town = new TownPresence("dock", new Point(110, 110)) };
        var smaller = Load(towns: Square + ", " + Dock.Replace("[140, 100], [140, 140]", "[105, 100], [105, 140]", StringComparison.Ordinal));
        var loaded = RoundTrip(game, Full, smaller);
        Assert.Equal(new TownPresence("dock", new Point(101, 101)), loaded.Game.Town);
        var adjustment = Assert.Single(loaded.Report.Adjustments);
        Assert.Equal(LoadAdjustmentKind.PositionReset, adjustment.Kind);
        Assert.Equal("dock", adjustment.Id);
    }

    [Fact(DisplayName = "a conversation the content no longer has is ended")]
    public void Missing_conversation_ends()
    {
        var game = Played(Full) with { Conversation = new ActiveConversation("talk", "bye") };
        var without = Load(replaceFrom: "\"id\": \"talk\"", replaceTo: "\"id\": \"chat\"");
        var loaded = RoundTrip(game, Full, without);
        Assert.Null(loaded.Game.Conversation);
        var adjustment = Assert.Single(loaded.Report.Adjustments);
        Assert.Equal(LoadAdjustmentKind.ConversationEnded, adjustment.Kind);
        Assert.Equal("talk", adjustment.Id);
        Assert.True(loaded.Game.Memory.HasChosen("talk", "go"));
    }

    [Fact(DisplayName = "a conversation whose step was removed restarts at its opening step with ConversationRestarted, and applied effects stay")]
    public void Missing_step_restarts()
    {
        var game = Talking(Full);
        var withoutBye = Load(replaceFrom: "\"bye\"", replaceTo: "\"farewell\"");
        var restarted = RoundTrip(game, Full, withoutBye);
        Assert.Equal(new ActiveConversation("talk", "hello"), restarted.Game.Conversation);
        var adjustment = Assert.Single(restarted.Report.Adjustments);
        Assert.Equal(LoadAdjustmentKind.ConversationRestarted, adjustment.Kind);
        Assert.Equal("talk", adjustment.Id);
        Assert.True(restarted.Game.Memory.HasFlag("met"));
        Assert.True(restarted.Game.Memory.HasChosen("talk", "go"));
    }

    [Fact(DisplayName = "story memory is kept whole, even ids the content no longer uses")]
    public void Memory_keeps_unknown_ids()
    {
        var game = Played(Full) with { Memory = StoryMemory.Empty.WithFlag("ghost").WithChosen("gone", "x").WithSeen("gone") };
        var loaded = RoundTrip(game, Full, Load(towns: Square));
        Assert.True(loaded.Game.Memory.HasFlag("ghost"));
        Assert.True(loaded.Game.Memory.HasChosen("gone", "x"));
        Assert.True(loaded.Game.Memory.HasSeen("gone"));
    }

    [Fact(DisplayName = "a hand-off to a fight the content no longer has is cleared and the player stays in the town")]
    public void Missing_fight_clears_the_hand_off()
    {
        var game = HandedOff(Full);
        var loaded = RoundTrip(game, Full, Load(fights: Melee));
        Assert.Null(loaded.Game.HandOff);
        Assert.Equal(new TownPresence("square", new Point(12.5, 7.25)), loaded.Game.Town);
        var adjustment = Assert.Single(loaded.Report.Adjustments);
        Assert.Equal(LoadAdjustmentKind.HandOffCleared, adjustment.Kind);
        Assert.Equal("brawl", adjustment.Id);
    }

    [Fact(DisplayName = "a hand-off whose recorded lineup is no longer ready for its fight is cleared")]
    public void Unready_lineup_clears_the_hand_off()
    {
        var game = HandedOff(Full);
        var needsTwo = Load(fights: Brawl.Replace("\"enemies\"", "\"lineup\": { \"min\": 2, \"max\": 2 }, \"enemies\"", StringComparison.Ordinal) + "," + Melee);
        var loaded = RoundTrip(game, Full, needsTwo);
        Assert.Null(loaded.Game.HandOff);
        Assert.Equal(LoadAdjustmentKind.HandOffCleared, Assert.Single(loaded.Report.Adjustments).Kind);
    }

    [Fact(DisplayName = "a party member no longer in the content leaves the party, and the lineup member goes with them")]
    public void Party_member_dropped()
    {
        var onlyAnn = Load(replaceFrom: "\"bo\"", replaceTo: "\"cy\"");
        var loaded = RoundTrip(Played(Full), Full, onlyAnn);
        Assert.Equal(["ann"], loaded.Game.Party);
        Assert.Empty(loaded.Game.Lineup);
        Assert.Contains(loaded.Report.Adjustments, a => a.Kind == LoadAdjustmentKind.PartyMemberDropped && a.Id == "bo");
        Assert.Contains(loaded.Report.Adjustments, a => a.Kind == LoadAdjustmentKind.LineupMemberDropped && a.Id == "bo");
    }

    [Fact(DisplayName = "the slot list reports each save's town name and situation from the save itself")]
    public async Task Slot_list_shows_town_and_situation()
    {
        var backend = new MemorySaveBackend();
        var store = new SaveStore(backend, Full);
        Assert.True((await store.SaveAsync(1, Played(Full), 1, Ct)).IsOk);
        Assert.True((await store.SaveAsync(2, Talking(Full) with { Town = new TownPresence("dock", new Point(110, 110)) }, 2, Ct)).IsOk);
        var handedOff = await store.SaveAsync(3, HandedOff(Full), 3, Ct);
        Assert.True(handedOff.IsOk);
        Assert.Equal("The Dock", (await store.SaveAsync(4, Played(Full) with { Town = new TownPresence("dock", new Point(110, 110)) }, 4, Ct)).Value.TownName);
        Assert.True((await store.SaveAsync(5, Played(Full) with { Town = null }, 5, Ct)).IsOk);

        // Listed under content that has no towns at all: the names still come from the saves.
        var listed = (await new SaveStore(backend, Load(towns: string.Empty, replaceFrom: "\"startingTown\": \"square\", ", replaceTo: string.Empty)).ListAsync(Ct)).Value;
        Assert.Equal((SaveSituation.Town, "Square"), (listed[0].Metadata!.Situation, listed[0].Metadata!.TownName));
        Assert.Equal((SaveSituation.Conversation, "The Dock"), (listed[1].Metadata!.Situation, listed[1].Metadata!.TownName));
        Assert.Equal((SaveSituation.HandOff, "Square"), (listed[2].Metadata!.Situation, listed[2].Metadata!.TownName));
        Assert.Equal((SaveSituation.Town, "The Dock"), (listed[3].Metadata!.Situation, listed[3].Metadata!.TownName));
        Assert.Equal((SaveSituation.None, (string?)null), (listed[4].Metadata!.Situation, listed[4].Metadata!.TownName));
        Assert.Equal("Square", handedOff.Value.TownName);
        Assert.Equal(SaveSituation.HandOff, handedOff.Value.Situation);
    }

    [Fact(DisplayName = "refuses bad version 2 fields with a clear reason and never throws")]
    public void Refuses_bad_fields()
    {
        var game = Played(Full);
        static string Reason(Result<LoadedSave> result) => Refused(result).Message;

        Assert.Contains("duplicate id ann", Reason(ReadEdited(game, g => g["party"] = new JsonArray("ann", "ann"), Full)), StringComparison.Ordinal);
        Assert.Contains("memory.flags: duplicate id met", Reason(ReadEdited(game, g => g["memory"]!["flags"] = new JsonArray("met", "met"), Full)), StringComparison.Ordinal);
        Assert.Contains("memory.seen: duplicate id talk", Reason(ReadEdited(game, g => g["memory"]!["seen"] = new JsonArray("talk", "talk"), Full)), StringComparison.Ordinal);
        Assert.Contains("memory.chosen: duplicate entry", Reason(ReadEdited(
            game,
            g => g["memory"]!["chosen"] = new JsonArray(new JsonObject { ["conversation"] = "a", ["choice"] = "b" }, new JsonObject { ["conversation"] = "a", ["choice"] = "b" }),
            Full)), StringComparison.Ordinal);
        Assert.Contains("party: each id must be 1 to 64 characters", Reason(ReadEdited(game, g => g["party"] = new JsonArray(new string('x', 65)), Full)), StringComparison.Ordinal);
        Assert.Contains("town.town", Reason(ReadEdited(game, g => g["town"]!["town"] = string.Empty, Full)), StringComparison.Ordinal);
        Refused(ReadEdited(game, g => g["town"]!["x"] = "far", Full));
        Refused(ReadEdited(game, g => g["memory"] = null, Full));
        Refused(ReadEdited(game, g => g.Remove("party"), Full));
        Assert.Contains("conversation:", Reason(ReadEdited(game, g => g["conversation"] = new JsonObject { ["conversation"] = "talk", ["step"] = string.Empty }, Full)), StringComparison.Ordinal);
        Assert.Contains("handOff.lineup: duplicate id bo", Reason(ReadEdited(
            HandedOff(Full), g => g["handOff"]!["lineup"] = new JsonArray("bo", "bo"), Full)), StringComparison.Ordinal);
        Refused(ReadEnvelopeEdited(game, e => e["summary"]!["situation"] = "elsewhere"));
        Refused(ReadEnvelopeEdited(game, e => e["summary"]!["situation"] = 1));
        Refused(ReadEnvelopeEdited(game, e => e.Remove("summary")));
    }

    [Fact(DisplayName = "a conversation saved together with a hand-off or a fight is ended; a fight with a hand-off is left alone")]
    public void Conversation_cannot_coexist_with_combat_or_hand_off()
    {
        var fight = FightGame().Combat;
        var withHandOff = HandedOff(Full) with { Conversation = new ActiveConversation("talk", "hello") };
        var loadedHandOff = RoundTrip(withHandOff, Full);
        Assert.Null(loadedHandOff.Game.Conversation);
        Assert.NotNull(loadedHandOff.Game.HandOff);
        Assert.Equal(LoadAdjustmentKind.ConversationEnded, Assert.Single(loadedHandOff.Report.Adjustments).Kind);

        var withCombat = Played(Full) with { Combat = fight, Conversation = new ActiveConversation("talk", "hello") };
        var loadedCombat = RoundTrip(withCombat, Full);
        Assert.Null(loadedCombat.Game.Conversation);
        Assert.NotNull(loadedCombat.Game.Combat);
        Assert.Equal(LoadAdjustmentKind.ConversationEnded, Assert.Single(loadedCombat.Report.Adjustments).Kind);

        var both = RoundTrip(HandedOff(Full) with { Combat = fight }, Full);
        Assert.NotNull(both.Game.Combat);
        Assert.NotNull(both.Game.HandOff);
        Assert.True(both.Report.IsEmpty);

        var noTown = RoundTrip(Talking(Full) with { Town = null }, Full, Load(replaceFrom: "\"startingTown\": \"square\", ", replaceTo: string.Empty));
        Assert.Equal(new ActiveConversation("talk", "bye"), noTown.Game.Conversation);
    }

    [Fact(DisplayName = "a hand-off naming a town the content no longer has loads with no town and says so")]
    public void Hand_off_town_is_checked()
    {
        var game = HandedOff(Full) with { HandOff = new FightHandOff("brawl", ["bo"], "dock") };
        var loaded = RoundTrip(game, Full, Load(towns: Square));
        Assert.Equal("brawl", loaded.Game.HandOff!.FightId);
        Assert.Null(loaded.Game.HandOff.TownId);
        var adjustment = Assert.Single(loaded.Report.Adjustments);
        Assert.Equal(LoadAdjustmentKind.HandOffTownCleared, adjustment.Kind);
        Assert.Equal("dock", adjustment.Id);
    }

    [Fact(DisplayName = "refuses a player name or town name that is too long or holds control characters")]
    public void Names_are_checked()
    {
        var game = Played(Full);
        Assert.Contains("playerName: must be at most 200", Refused(ReadEnvelopeEdited(game, e => e["playerName"] = new string('n', 201))).Message, StringComparison.Ordinal);
        Assert.Contains("playerName: must not contain control", Refused(ReadEnvelopeEdited(game, e => e["playerName"] = "Ri\nn")).Message, StringComparison.Ordinal);
        Assert.Contains("summary.townName: must not contain control", Refused(ReadEnvelopeEdited(game, e => e["summary"]!["townName"] = "Sq\u0007uare")).Message, StringComparison.Ordinal);
        Assert.True(Read(Doc(game, Full), Full).IsOk);
        Assert.Equal(200, ReadOk(WriteOk(game with { Player = game.Player with { Name = new string('n', 200) } }, Full), Full).PlayerName.Length);
    }

    [Fact(DisplayName = "content refuses a player name a save could not hold, and a town name's control characters are left out of the summary")]
    public void Content_player_name_and_summary_text()
    {
        var loaded = ContentPackLoader.Load(new MemoryContentSource([KeyValuePair.Create("pack.json", Pack), KeyValuePair.Create("base.json", Template.Replace("TOWNS", Square, StringComparison.Ordinal).Replace("FIGHTS", Brawl, StringComparison.Ordinal).Replace("PERSON", Person, StringComparison.Ordinal).Replace("COMBAT", Combat, StringComparison.Ordinal).Replace("RAT", Rat, StringComparison.Ordinal).Replace("Kael", new string('K', 201), StringComparison.Ordinal))]));
        Assert.False(loaded.IsOk);
        Assert.Contains(loaded.Error.Details, d => d.Path.EndsWith("settings/playerName", StringComparison.Ordinal));

        var content = Load(towns: Square.Replace("\"Square\"", "\"Sq\\u0007uare\"", StringComparison.Ordinal));
        Assert.Equal("Square", (string?)Doc(Played(content), content)["summary"]!["townName"]);
    }

    [Fact(DisplayName = "a town name over 200 characters is cut in the summary and the save still writes")]
    public void Long_town_names_are_cut()
    {
        var longName = new string('n', 300);
        var content = Load(towns: Square.Replace("\"Square\"", "\"" + longName + "\"", StringComparison.Ordinal));
        var doc = Doc(Played(content), content);
        Assert.Equal(200, ((string?)doc["summary"]!["townName"])!.Length);
    }
}
