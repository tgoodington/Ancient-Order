using AncientOrder.Rules.Conditions;
using AncientOrder.Rules.Conversations;
using AncientOrder.Rules.State;
using AncientOrder.Rules.Town;
using Eval = AncientOrder.Rules.Conditions.Conditions;

namespace AncientOrder.Rules.Tests.ConditionTesting;

/// <summary>
/// Every place content can put a condition, checked with a condition that holds and one that does not.
/// The pack is built per case with the condition text dropped into every hook point.
/// </summary>
public class ConditionHookTests
{
    private const string Flag = """{ "type": "flag", "flag": "f" }""";

    private const string Extra = """
        {
          "conversations": [
            { "id": "c", "participants": ["ann"],
              "start": [ { "step": "special", "when": [ COND ] }, { "step": "plain" } ],
              "steps": [
                { "id": "plain",
                  "lines": [ { "speaker": "narrator", "text": "always" }, { "speaker": "narrator", "text": "gated", "when": [ COND ] } ],
                  "choices": [
                    { "id": "pick", "text": "Pick" },
                    { "id": "other", "text": "Other" },
                    { "id": "gated", "text": "Gated", "when": [ COND ] },
                    { "id": "locked", "text": "Locked", "lockedText": "No.", "requires": [ COND ] },
                    { "id": "go", "text": "Go", "next": [ { "step": "yes", "when": [ COND ] }, { "step": "no" } ] }
                  ] },
                { "id": "special", "lines": [] },
                { "id": "yes", "lines": [] },
                { "id": "no", "lines": [] }
              ] },
            { "id": "d", "participants": [], "steps": [ { "id": "s", "lines": [] } ] }
          ],
          "towns": [ {
            "id": "square", "name": "Square",
            "ground": [ [[0, 0], [40, 0], [40, 40], [0, 40]] ],
            "start": [1, 1],
            "people": [
              { "character": "ann", "placements": [ { "at": [5, 5], "when": [ COND ] } ], "opens": [ { "conversation": "d" } ] },
              { "character": "bo", "placements": [ { "at": [30, 30] } ], "opens": [ { "conversation": "d", "when": [ COND ] } ] }
            ],
            "spots": [
              { "id": "gated", "area": { "circle": { "center": [2, 20], "radius": 1 } }, "trigger": "interact", "when": [ COND ], "opens": [ { "conversation": "d" } ] },
              { "id": "opener", "area": { "circle": { "center": [35, 35], "radius": 1 } }, "trigger": "interact", "opens": [ { "conversation": "d", "when": [ COND ] } ] },
              { "id": "walker", "area": { "polygon": [[10, 10], [14, 10], [14, 14], [10, 14]] }, "trigger": "walkOver", "when": [ COND ], "opens": [ { "conversation": "d" } ] }
            ]
          } ]
        }
        """;

    private static GameContent Build(string condition) =>
        ConditionPack.Load(Extra.Replace("COND", condition, StringComparison.Ordinal), "square");

    private static GameState Game(GameContent content, bool flag)
    {
        var game = ConditionPack.NewGame(content);
        var memory = flag ? game.Memory.WithFlag("f") : game.Memory;
        return game with { Memory = memory };
    }

    /// <summary>The state every condition kind below is judged in.</summary>
    private static GameState Story(GameContent content)
    {
        var game = ConditionPack.NewGame(content);
        return game with
        {
            Memory = game.Memory.WithFlag("f").WithChosen("c", "pick").WithSeen("c"),
            Party = ["ann"],
            Lineup = ["ann"],
        };
    }

    private static GameState AtStep(GameState game, string step) => game with { Conversation = new ActiveConversation("c", step) };

    private static GameState StandAt(GameState game, double x, double z) => game with { Town = game.Town! with { Position = new Point(x, z) } };

    public static readonly string[] HookNames =
    [
        "line when", "choice when", "choice requires", "destination when", "start entry when",
        "person placement", "spot when", "person opens", "spot opens",
    ];

    private static void AssertHook(string hook, GameContent content, GameState game, bool holds)
    {
        switch (hook)
        {
            case "line when":
                var lines = ConversationView.From(content, AtStep(game, "plain"))!.Lines;
                Assert.Equal(holds ? ["always", "gated"] : ["always"], lines.Select(l => l.Text));
                break;
            case "choice when":
                var shown = ConversationView.From(content, AtStep(game, "plain"))!.Choices.Select(c => c.Id);
                Assert.Equal(holds, shown.Contains("gated"));
                Assert.Contains("pick", shown);
                break;
            case "choice requires":
                var locked = ConversationView.From(content, AtStep(game, "plain"))!.Choices.Single(c => c.Id == "locked");
                Assert.Equal(holds, locked.Available);
                if (holds)
                {
                    Assert.Null(locked.Requirement);
                }
                else
                {
                    Assert.Equal("No.", locked.LockedText);
                    Assert.False(locked.Requirement!.Met);
                    Assert.False(ConversationRules.Choose(content, AtStep(game, "plain"), "locked", 3).IsOk);
                }

                break;
            case "destination when":
                var moved = ConversationRules.Choose(content, AtStep(game, "plain"), "go", 3).Value.Game;
                Assert.Equal(holds ? "yes" : "no", moved.Conversation!.StepId);
                break;
            case "start entry when":
                var started = ConversationRules.Start(content, game, "c", 2).Value.Game;
                Assert.Equal(holds ? "special" : "plain", started.Conversation!.StepId);
                break;
            case "person placement":
                var people = TownView.From(content, game)!.People;
                Assert.Equal(holds, people.Any(p => p.CharacterId == "ann"));
                Assert.Contains(people, p => p.CharacterId == "bo");
                if (holds)
                {
                    Assert.Equal(new Point(5, 5), people.Single(p => p.CharacterId == "ann").Position);
                }

                break;
            case "spot when":
                var spots = TownView.From(content, game)!.Spots.Select(s => s.Id);
                Assert.Equal(holds, spots.Contains("gated"));
                Assert.Equal(holds, spots.Contains("walker"));
                Assert.Contains("opener", spots);
                break;
            case "person opens":
                var bo = TownView.From(content, game)!.People.Single(p => p.CharacterId == "bo");
                Assert.Equal(holds, bo.CanOpen);
                var person = TownTriggers.TriggerPerson(content, StandAt(game, 30, 29), "bo", 4).Value;
                Assert.Equal(holds ? new TownEvent.ConversationStarted("d") : new TownEvent.Nothing(), person.Event);
                Assert.Equal(holds, person.Game.Conversation is not null);
                break;
            case "spot opens":
                var opener = TownView.From(content, game)!.Spots.Single(s => s.Id == "opener");
                Assert.Equal(holds, opener.CanOpen);
                var spot = TownTriggers.TriggerSpot(content, StandAt(game, 35, 34), "opener", 4).Value;
                Assert.Equal(holds ? new TownEvent.ConversationStarted("d") : new TownEvent.Nothing(), spot.Event);
                Assert.Equal(holds, spot.Game.Conversation is not null);
                break;
            default:
                throw new InvalidOperationException(hook);
        }
    }

    public static TheoryData<string, bool> HookCases()
    {
        var data = new TheoryData<string, bool>();
        foreach (var hook in HookNames)
        {
            data.Add(hook, true);
            data.Add(hook, false);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(HookCases))]
    public void Each_hook_follows_a_passing_and_a_failing_condition(string hook, bool holds)
    {
        var content = Build(Flag);
        AssertHook(hook, content, Game(content, holds), holds);
    }

    public static TheoryData<string, bool> KindCases() => new()
    {
        { Flag, true },
        { """{ "type": "flag", "flag": "g" }""", false },
        { """{ "type": "chose", "conversation": "c", "choice": "pick" }""", true },
        { """{ "type": "chose", "conversation": "c", "choice": "other" }""", false },
        { """{ "type": "seen", "conversation": "c" }""", true },
        { """{ "type": "seen", "conversation": "d" }""", false },
        { """{ "type": "trait", "trait": "logic", "atLeast": 0 }""", true },
        { """{ "type": "trait", "trait": "logic", "atMost": 100 }""", true },
        { """{ "type": "trait", "trait": "logic", "atLeast": 100 }""", false },
        { """{ "type": "relationship", "character": "ann", "measure": "affection", "atLeast": 10 }""", true },
        { """{ "type": "relationship", "character": "ann", "measure": "affection", "atLeast": 11 }""", false },
        { """{ "type": "relationship", "character": "ann", "measure": "trust", "atMost": -5 }""", true },
        { """{ "type": "inParty", "character": "ann" }""", true },
        { """{ "type": "inParty", "character": "bo" }""", false },
        { """{ "type": "lineupReady" }""", true },
        { """{ "type": "lineupReady", "fight": "brawl" }""", true },
        { """{ "type": "not", "condition": { "type": "lineupReady" } }""", false },
        { """{ "type": "all", "conditions": [ { "type": "flag", "flag": "f" }, { "type": "inParty", "character": "ann" } ] }""", true },
        { """{ "type": "all", "conditions": [ { "type": "flag", "flag": "f" }, { "type": "flag", "flag": "g" } ] }""", false },
        { """{ "type": "any", "conditions": [ { "type": "flag", "flag": "g" }, { "type": "flag", "flag": "f" } ] }""", true },
        { """{ "type": "any", "conditions": [ { "type": "flag", "flag": "g" }, { "type": "inParty", "character": "bo" } ] }""", false },
        { """{ "type": "not", "condition": { "type": "flag", "flag": "g" } }""", true },
        { """{ "type": "not", "condition": { "type": "flag", "flag": "f" } }""", false },
        { """{ "type": "any", "conditions": [ { "type": "flag", "flag": "g" }, { "type": "not", "condition": { "type": "all", "conditions": [ { "type": "flag", "flag": "f" }, { "type": "inParty", "character": "bo" } ] } } ] }""", true },
        { """{ "type": "not", "condition": { "type": "any", "conditions": [ { "type": "flag", "flag": "g" }, { "type": "not", "condition": { "type": "all", "conditions": [ { "type": "flag", "flag": "f" }, { "type": "inParty", "character": "bo" } ] } } ] } }""", false },
    };

    [Theory]
    [MemberData(nameof(KindCases))]
    public void Every_kind_of_condition_drives_every_hook_the_same_way(string condition, bool holds)
    {
        var content = Build(condition);
        var game = Story(content);

        Assert.Equal(holds, Eval.Holds(content, game, [ParseHook(content, condition)]));
        foreach (var hook in HookNames)
        {
            AssertHook(hook, content, game, holds);
        }
    }

    // The loaded pack holds the parsed form of the condition at its first use; read it back so the check goes through the real loader.
    private static Condition ParseHook(GameContent content, string condition) =>
        content.Conversations.TryGet("c", out var c)
            ? c.Steps.First(s => s.Id == "plain").Lines[1].When[0]
            : throw new InvalidOperationException(condition);

    [Fact]
    public void A_person_stands_at_the_first_placement_that_holds()
    {
        const string extra = """
            { "towns": [ { "id": "square", "name": "Square", "ground": [ [[0, 0], [40, 0], [40, 40], [0, 40]] ], "start": [1, 1],
              "people": [ { "character": "ann", "placements": [
                { "at": [5, 5], "when": [ { "type": "flag", "flag": "f" } ] },
                { "at": [20, 20], "when": [ { "type": "flag", "flag": "g" } ] },
                { "at": [30, 30], "when": [ { "type": "not", "condition": { "type": "flag", "flag": "f" } } ] } ] } ] } ] }
            """;
        var content = ConditionPack.Load(extra, "square");
        var game = ConditionPack.NewGame(content);

        Point AnnAt(GameState state) => Assert.Single(TownView.From(content, state)!.People).Position;

        Assert.Equal(new Point(30, 30), AnnAt(game));
        Assert.Equal(new Point(5, 5), AnnAt(game with { Memory = game.Memory.WithFlag("f") }));
        Assert.Equal(new Point(5, 5), AnnAt(game with { Memory = game.Memory.WithFlag("f").WithFlag("g") }));
        Assert.Equal(new Point(20, 20), AnnAt(game with { Memory = game.Memory.WithFlag("g") }));
    }

    [Fact]
    public void A_person_with_no_placement_that_holds_is_absent()
    {
        const string extra = """
            { "towns": [ { "id": "square", "name": "Square", "ground": [ [[0, 0], [40, 0], [40, 40], [0, 40]] ], "start": [1, 1],
              "people": [ { "character": "ann", "placements": [ { "at": [5, 5], "when": [ { "type": "flag", "flag": "f" } ] } ], "opens": [ { "conversation": "d" } ] } ] } ],
              "conversations": [ { "id": "d", "participants": [], "steps": [ { "id": "s", "lines": [] } ] } ] }
            """;
        var content = ConditionPack.Load(extra, "square");
        var game = StandAt(ConditionPack.NewGame(content), 5, 6);

        Assert.Empty(TownView.From(content, game)!.People);
        Assert.Equal(AncientOrder.Rules.Common.ErrorCode.TARGET_NOT_FOUND, TownTriggers.TriggerPerson(content, game, "ann", 2).Error.Code);
    }

    [Fact]
    public void The_first_opens_entry_that_holds_wins_and_the_next_takes_over_when_it_stops_holding()
    {
        const string extra = """
            { "towns": [ { "id": "square", "name": "Square", "ground": [ [[0, 0], [40, 0], [40, 40], [0, 40]] ], "start": [1, 1],
              "people": [ { "character": "ann", "placements": [ { "at": [5, 5] } ],
                "opens": [
                  { "conversation": "first", "when": [ { "type": "flag", "flag": "f" } ] },
                  { "conversation": "second", "when": [ { "type": "not", "condition": { "type": "seen", "conversation": "second" } } ] },
                  { "conversation": "third" } ] } ] } ],
              "conversations": [
                { "id": "first", "participants": [], "steps": [ { "id": "s", "lines": [] } ] },
                { "id": "second", "participants": [], "steps": [ { "id": "s", "lines": [] } ] },
                { "id": "third", "participants": [], "steps": [ { "id": "s", "lines": [] } ] } ] }
            """;
        var content = ConditionPack.Load(extra, "square");
        var game = StandAt(ConditionPack.NewGame(content), 5, 6);

        string Opened(GameState state) =>
            Assert.IsType<TownEvent.ConversationStarted>(TownTriggers.TriggerPerson(content, state, "ann", 2).Value.Event).ConversationId;

        Assert.Equal("second", Opened(game));
        Assert.Equal("first", Opened(game with { Memory = game.Memory.WithFlag("f") }));
        Assert.Equal("third", Opened(game with { Memory = game.Memory.WithSeen("second") }));
    }

    [Fact]
    public void A_walk_over_spot_fires_only_while_its_when_holds()
    {
        var content = Build(Flag);

        foreach (var holds in new[] { true, false })
        {
            var before = StandAt(Game(content, holds), 9, 12);
            var after = StandAt(before, 12, 12);
            var step = TownTriggers.WalkOver(content, before, after, 5).Value;

            Assert.Equal(holds ? new TownEvent.ConversationStarted("d") : new TownEvent.Nothing(), step.Event);
        }
    }

    [Fact]
    public void A_spot_is_shown_by_its_trigger_unless_content_says_otherwise()
    {
        const string extra = """
            { "towns": [ { "id": "square", "name": "Square", "ground": [ [[0, 0], [40, 0], [40, 40], [0, 40]] ], "start": [1, 1],
              "spots": [
                { "id": "a", "area": { "circle": { "center": [2, 20], "radius": 1 } }, "trigger": "interact" },
                { "id": "b", "area": { "circle": { "center": [4, 20], "radius": 1 } }, "trigger": "interact", "shown": false },
                { "id": "c", "area": { "polygon": [[10, 10], [14, 10], [14, 14], [10, 14]] }, "trigger": "walkOver" },
                { "id": "d", "area": { "polygon": [[20, 10], [24, 10], [24, 14], [20, 14]] }, "trigger": "walkOver", "shown": true } ] } ] }
            """;
        var content = ConditionPack.Load(extra, "square");

        Assert.Equal([true, false, false, true], TownView.From(content, ConditionPack.NewGame(content))!.Spots.Select(s => s.Shown));
    }
}
