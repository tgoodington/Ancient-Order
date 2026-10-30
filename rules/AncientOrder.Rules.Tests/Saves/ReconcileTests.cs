using AncientOrder.Rules.Content.Packs;
using AncientOrder.Rules.Conversations;
using AncientOrder.Rules.HandOff;
using AncientOrder.Rules.Saves;
using AncientOrder.Rules.State;
using AncientOrder.Rules.Story;
using AncientOrder.Rules.Tests.Fixtures;
using AncientOrder.Rules.Town;
using static AncientOrder.Rules.Tests.Support.SaveFixtures;

namespace AncientOrder.Rules.Tests.Saves;

/// <summary>In-memory content with two towns, a conversation, two fights, and a two-person party, for the save and load tests.</summary>
internal static class SaveWorlds
{
    private const string Pack = """{ "format": "ancient-order-content", "version": 1, "id": "t", "name": "Test", "standIn": false, "files": ["base.json"] }""";

    private const string Rat = """{ "id": "rat", "name": "Rat", "archetype": "x", "rank": 1, "stamina": 10, "power": 5, "speed": 5, "elementalPath": "fire", "reactionSkills": { "block": { "sr": 1, "smr": 1, "fmr": 1 }, "dodge": { "sr": 1, "smr": 1, "fmr": 1 }, "parry": { "sr": 1, "smr": 1, "fmr": 1 } } }""";

    private const string Combat = """{ "rank": 1, "stamina": 50, "power": 10, "speed": 10, "elementalPath": "fire" }""";

    private const string Person = "\"personality\": { \"patience\": 20, \"empathy\": 20, \"cunning\": 10, \"logic\": 15, \"kindness\": 20, \"charisma\": 15 }, \"startingAffection\": 0, \"startingTrust\": 0";

    public const string Square = """{ "id": "square", "name": "Square", "ground": [ [[0, 0], [40, 0], [40, 40], [0, 40]] ], "start": [1, 1] }""";

    public const string Dock = """{ "id": "dock", "name": "The Dock", "ground": [ [[100, 100], [140, 100], [140, 140], [100, 140]] ], "start": [101, 101] }""";

    public const string Brawl = """{ "id": "brawl", "name": "The Brawl", "enemies": [ RAT ] }""";

    public const string Melee = """{ "id": "melee", "name": "The Melee", "lineup": { "min": 2, "max": 2 }, "enemies": [ RAT ] }""";

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
              ] }
          ],
          "fights": [ FIGHTS ],
          "towns": [ TOWNS ]
        }
        """;

    public static GameContent Load(string towns = Square + "," + Dock, string fights = Brawl + "," + Melee, string? replaceFrom = null, string? replaceTo = null)
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

    public static GameContent Full { get; } = Load();

    public static GameState Played(GameContent content) =>
        TestContent.NewGame(content) with
        {
            Party = ["ann", "bo"],
            Lineup = ["ann", "bo"],
            Town = new TownPresence("dock", new Point(110.5, 120.25)),
            Memory = StoryMemory.Empty.WithFlag("met").WithChosen("talk", "go").WithSeen("talk"),
            Characters = TestContent.NewGame(content).Characters.SetItem("ann", new CharacterState(33.5, -12)),
        };

    public static GameState Talking(GameContent content, string step = "bye") =>
        Played(content) with { Conversation = new ActiveConversation("talk", step) };

    public static GameState HandedOff(GameContent content, string fight = "melee", string? town = "dock") =>
        Played(content) with { HandOff = new FightHandOff(fight, ["ann", "bo"], town) };

    public static LoadedSave RoundTrip(GameState game, GameContent loadedUnder) => ReadOk(WriteOk(game, Full), loadedUnder);
}

// One test per load adjustment kind. Each saves under the full content, loads under changed content, and checks the state and the report.
public class ReconcileTests
{
    private const string Cy = "{ \"id\": \"cy\", \"name\": \"Cy\", \"archetypeId\": \"x\", \"personality\": { \"patience\": 20, \"empathy\": 20, \"cunning\": 10, \"logic\": 15, \"kindness\": 20, \"charisma\": 15 }, \"startingAffection\": 7, \"startingTrust\": 3, \"combat\": { \"rank\": 1, \"stamina\": 50, \"power\": 10, \"speed\": 10, \"elementalPath\": \"fire\" } },\n";

    private static LoadAdjustment Only(LoadedSave loaded, LoadAdjustmentKind kind, string? id)
    {
        var adjustment = Assert.Single(loaded.Report.Adjustments);
        Assert.Equal(kind, adjustment.Kind);
        Assert.Equal(id, adjustment.Id);
        Assert.False(string.IsNullOrWhiteSpace(adjustment.Message));
        return adjustment;
    }

    private static GameContent WithoutBo() => SaveWorlds.Load(replaceFrom: "\"bo\"", replaceTo: "\"cy\"");

    [Fact(DisplayName = "CharacterAdded: a character new in the content joins at its starting relationship and is reported")]
    public void Character_added()
    {
        var grown = SaveWorlds.Load(replaceFrom: "{ \"id\": \"bo\", \"name\": \"Bo\"", replaceTo: Cy + "{ \"id\": \"bo\", \"name\": \"Bo\"");
        var loaded = SaveWorlds.RoundTrip(SaveWorlds.Played(SaveWorlds.Full), grown);
        Only(loaded, LoadAdjustmentKind.CharacterAdded, "cy");
        Assert.Equal(new CharacterState(7, 3), loaded.Game.Characters["cy"]);
        Assert.Equal(new CharacterState(33.5, -12), loaded.Game.Characters["ann"]);
        Assert.Equal(["ann", "bo"], loaded.Game.Party);
    }

    [Fact(DisplayName = "CharacterDropped: a character the content no longer has is dropped and reported")]
    public void Character_dropped()
    {
        var loaded = SaveWorlds.RoundTrip(SaveWorlds.Played(SaveWorlds.Full), WithoutBo());
        Assert.Single(loaded.Report.Adjustments, a => a.Kind == LoadAdjustmentKind.CharacterDropped && a.Id == "bo");
        Assert.False(loaded.Game.Characters.ContainsKey("bo"));
        Assert.Equal(new CharacterState(33.5, -12), loaded.Game.Characters["ann"]);
    }

    [Fact(DisplayName = "PartyMemberDropped: a party member the content no longer has leaves the party and is reported")]
    public void Party_member_dropped()
    {
        var loaded = SaveWorlds.RoundTrip(SaveWorlds.Played(SaveWorlds.Full), WithoutBo());
        Assert.Equal(["ann"], loaded.Game.Party);
        Assert.Single(loaded.Report.Adjustments, a => a.Kind == LoadAdjustmentKind.PartyMemberDropped && a.Id == "bo");
    }

    [Fact(DisplayName = "LineupMemberDropped: a lineup member who is no longer in the party is removed from the lineup and reported")]
    public void Lineup_member_dropped()
    {
        var loaded = SaveWorlds.RoundTrip(SaveWorlds.Played(SaveWorlds.Full), WithoutBo());
        Assert.Equal(["ann"], loaded.Game.Lineup);
        Assert.Single(loaded.Report.Adjustments, a => a.Kind == LoadAdjustmentKind.LineupMemberDropped && a.Id == "bo");
    }

    [Fact(DisplayName = "LineupCleared: a lineup larger than the content allows is emptied, with no id in the report")]
    public void Lineup_cleared()
    {
        var smaller = SaveWorlds.Load(fights: SaveWorlds.Brawl, replaceFrom: "\"lineup\": { \"min\": 1, \"max\": 2 }", replaceTo: "\"lineup\": { \"min\": 1, \"max\": 1 }");
        var loaded = SaveWorlds.RoundTrip(SaveWorlds.Played(SaveWorlds.Full), smaller);
        Assert.Empty(loaded.Game.Lineup);
        Assert.Equal(["ann", "bo"], loaded.Game.Party);
        Only(loaded, LoadAdjustmentKind.LineupCleared, null);
    }

    [Fact(DisplayName = "TownReset: a town the content no longer has puts the player at the starting town's start")]
    public void Town_reset()
    {
        var loaded = SaveWorlds.RoundTrip(SaveWorlds.Played(SaveWorlds.Full), SaveWorlds.Load(towns: SaveWorlds.Square));
        Assert.Equal(new TownPresence("square", new Point(1, 1)), loaded.Game.Town);
        Only(loaded, LoadAdjustmentKind.TownReset, "dock");
    }

    [Fact(DisplayName = "PositionReset: a position the town no longer allows moves the player to that town's start")]
    public void Position_reset()
    {
        var smaller = SaveWorlds.Load(towns: SaveWorlds.Square + ", " + SaveWorlds.Dock.Replace("[140, 100], [140, 140]", "[105, 100], [105, 140]", StringComparison.Ordinal));
        var loaded = SaveWorlds.RoundTrip(SaveWorlds.Played(SaveWorlds.Full), smaller);
        Assert.Equal(new TownPresence("dock", new Point(101, 101)), loaded.Game.Town);
        Only(loaded, LoadAdjustmentKind.PositionReset, "dock");
    }

    [Fact(DisplayName = "ConversationEnded: a conversation the content no longer has is ended and the story memory stays")]
    public void Conversation_ended()
    {
        var loaded = SaveWorlds.RoundTrip(SaveWorlds.Talking(SaveWorlds.Full), SaveWorlds.Load(replaceFrom: "\"id\": \"talk\"", replaceTo: "\"id\": \"chat\""));
        Assert.Null(loaded.Game.Conversation);
        Only(loaded, LoadAdjustmentKind.ConversationEnded, "talk");
        Assert.True(loaded.Game.Memory.HasFlag("met"));
        Assert.True(loaded.Game.Memory.HasChosen("talk", "go"));
    }

    [Fact(DisplayName = "ConversationRestarted: a removed step restarts the conversation at its opening step and applied effects stay")]
    public void Conversation_restarted()
    {
        var loaded = SaveWorlds.RoundTrip(SaveWorlds.Talking(SaveWorlds.Full), SaveWorlds.Load(replaceFrom: "\"bye\"", replaceTo: "\"farewell\""));
        Assert.Equal(new ActiveConversation("talk", "hello"), loaded.Game.Conversation);
        var adjustment = Only(loaded, LoadAdjustmentKind.ConversationRestarted, "talk");
        Assert.Contains("hello", adjustment.Message, StringComparison.Ordinal);
        Assert.True(loaded.Game.Memory.HasFlag("met"));
        Assert.True(loaded.Game.Memory.HasChosen("talk", "go"));
    }

    [Fact(DisplayName = "a conversation whose step is still there loads unchanged with an empty report")]
    public void Conversation_unchanged()
    {
        var loaded = SaveWorlds.RoundTrip(SaveWorlds.Talking(SaveWorlds.Full, "hello"), SaveWorlds.Full);
        Assert.Equal(new ActiveConversation("talk", "hello"), loaded.Game.Conversation);
        Assert.True(loaded.Report.IsEmpty);
    }

    [Fact(DisplayName = "HandOffCleared: a hand-off to a fight the content no longer has is cleared and the player stays in town")]
    public void Hand_off_cleared_for_a_missing_fight()
    {
        var loaded = SaveWorlds.RoundTrip(SaveWorlds.HandedOff(SaveWorlds.Full, "brawl"), SaveWorlds.Load(fights: SaveWorlds.Melee));
        Assert.Null(loaded.Game.HandOff);
        Assert.Equal(new TownPresence("dock", new Point(110.5, 120.25)), loaded.Game.Town);
        Only(loaded, LoadAdjustmentKind.HandOffCleared, "brawl");
    }

    [Fact(DisplayName = "HandOffCleared: a hand-off whose lineup no longer fits its fight is cleared")]
    public void Hand_off_cleared_for_an_unready_lineup()
    {
        var oneOnly = SaveWorlds.Load(fights: SaveWorlds.Brawl + "," + SaveWorlds.Melee.Replace("\"min\": 2, \"max\": 2", "\"min\": 1, \"max\": 1", StringComparison.Ordinal));
        var loaded = SaveWorlds.RoundTrip(SaveWorlds.HandedOff(SaveWorlds.Full), oneOnly);
        Assert.Null(loaded.Game.HandOff);
        Only(loaded, LoadAdjustmentKind.HandOffCleared, "melee");
    }

    [Fact(DisplayName = "HandOffTownCleared: a hand-off keeps its fight but loses a town the content no longer has")]
    public void Hand_off_town_cleared()
    {
        var loaded = SaveWorlds.RoundTrip(SaveWorlds.HandedOff(SaveWorlds.Full), SaveWorlds.Load(towns: SaveWorlds.Square));
        Assert.Equal("melee", loaded.Game.HandOff!.FightId);
        Assert.Null(loaded.Game.HandOff.TownId);
        Assert.Contains(loaded.Report.Adjustments, a => a.Kind == LoadAdjustmentKind.HandOffTownCleared && a.Id == "dock");
    }

    [Fact(DisplayName = "several adjustments arrive together in the order the steps run, and the load still succeeds")]
    public void Adjustments_arrive_in_step_order()
    {
        var changed = SaveWorlds.Load(towns: SaveWorlds.Square, fights: SaveWorlds.Brawl);
        var loaded = SaveWorlds.RoundTrip(SaveWorlds.HandedOff(SaveWorlds.Full), changed);
        Assert.Equal(
            [LoadAdjustmentKind.TownReset, LoadAdjustmentKind.HandOffCleared],
            loaded.Report.Adjustments.Select(a => a.Kind));
        Assert.Equal(new TownPresence("square", new Point(1, 1)), loaded.Game.Town);
        Assert.Null(loaded.Game.HandOff);
    }
}
