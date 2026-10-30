using System.Globalization;
using AncientOrder.Rules.Content.Packs;
using AncientOrder.Rules.Conversations;
using AncientOrder.Rules.Personality;
using AncientOrder.Rules.State;
using Persona = AncientOrder.Rules.Personality.Personality;

namespace AncientOrder.Rules.Tests.Personality;

/// <summary>Shift effects end to end through a conversation: bounds and sum after every shift, and what the choice report says. Basic shift cases live in ContentPacks/PersonalityShiftTests.</summary>
public class ShiftEffectTests
{
    private const string Pack = """{ "format": "ancient-order-content", "version": 1, "id": "t", "name": "Test", "standIn": false, "files": ["base.json", "talk.json"] }""";

    private const string Base = """
        {
          "settings": { "playerName": "Kael", "lineup": { "min": 1, "max": 1 }, "startingPersonality": { "patience": 20, "empathy": 20, "cunning": 10, "logic": 15, "kindness": 20, "charisma": 15 } },
          "archetypes": [{ "id": "x" }],
          "characters": [],
          "fights": [ { "id": "brawl", "name": "Brawl", "enemies": [ { "id": "rat", "name": "Rat", "archetype": "x", "rank": 1, "stamina": 10, "power": 5, "speed": 5, "elementalPath": "fire", "reactionSkills": { "block": { "sr": 1, "smr": 1, "fmr": 1 }, "dodge": { "sr": 1, "smr": 1, "fmr": 1 }, "parry": { "sr": 1, "smr": 1, "fmr": 1 } } } ] } ]
        }
        """;

    private static readonly string[] TraitNames = ["patience", "empathy", "cunning", "logic", "kindness", "charisma"];

    private static string Num(double value) => value.ToString("R", CultureInfo.InvariantCulture);

    private static string ShiftJson((int Trait, double Amount) shift) => "{ \"type\": \"shift\", \"" + TraitNames[shift.Trait] + "\": " + Num(shift.Amount) + " }";

    private static string ChoiceJson(string id, params (int Trait, double Amount)[] shifts) =>
        "{ \"id\": \"" + id + "\", \"text\": \"Pick\", \"effects\": [ " + string.Join(", ", shifts.Select(ShiftJson)) + " ] }";

    private static (GameContent Content, GameState Game) Setup(IEnumerable<string> choices)
    {
        var talk = "{ \"conversations\": [ { \"id\": \"chat\", \"participants\": [], \"start\": [ { \"step\": \"s\" } ], \"steps\": [ { \"id\": \"s\", \"lines\": [], \"choices\": [ "
            + string.Join(",\n", choices) + " ] } ] } ] }";
        var loaded = ContentPackLoader.Load(new MemoryContentSource([KeyValuePair.Create("pack.json", Pack), KeyValuePair.Create("base.json", Base), KeyValuePair.Create("talk.json", talk)]));
        Assert.True(loaded.IsOk, loaded.IsOk ? string.Empty : string.Join("\n", loaded.Error.Details));
        return (loaded.Value, GameStateUpdates.NewGame(loaded.Value, "p", 1));
    }

    private static (GameState Game, ChoiceReport Report) Take(GameContent content, GameState game, string choiceId, long now)
    {
        var started = ConversationRules.Start(content, game, "chat", now);
        Assert.True(started.IsOk, started.IsOk ? string.Empty : started.Error.Message);
        var chosen = ConversationRules.Choose(content, started.Value.Game, choiceId, now + 1);
        Assert.True(chosen.IsOk, chosen.IsOk ? string.Empty : chosen.Error.Message);
        return (chosen.Value.Game, chosen.Value.Report);
    }

    private static void AssertWithinLimits(Persona p, string context)
    {
        foreach (var trait in PersonalityRules.Traits)
        {
            Assert.True(p[trait] >= 5 && p[trait] <= 35, $"{context}: {trait} is {p[trait]}");
        }

        Assert.True(Math.Abs(PersonalityRules.Sum(p) - 100) <= PersonalityRules.FloatTolerance, $"{context}: sum is {PersonalityRules.Sum(p)}");
        Assert.True(PersonalityRules.Validate(p).Valid, context);
    }

    private static void AssertReportMatches(Persona before, Persona after, ChoiceReport report, string context)
    {
        Assert.Equal(PersonalityRules.Traits.Where(t => before[t] != after[t]).ToList(), report.TraitChanges.Select(c => c.Trait).ToList());
        foreach (var change in report.TraitChanges)
        {
            Assert.Equal(before[change.Trait], change.Before);
            Assert.Equal(after[change.Trait], change.After);
            Assert.Equal(Math.Round(after[change.Trait] - before[change.Trait], 2), change.Change);
        }

        var written = report.WrittenShift.Select(s => (int)s.Trait).ToList();
        Assert.True(written.SequenceEqual(written.Order()), $"{context}: written shifts not in trait order");
    }

    private static readonly (string Name, (int Trait, double Amount)[] Shifts)[] Table =
    [
        ("small push up", [(2, 3)]),
        ("past the ceiling", [(2, 100)]),
        ("past the floor", [(2, -100)]),
        ("everything up", [(0, 50), (1, 50), (2, 50), (3, 50), (4, 50), (5, 50)]),
        ("everything down", [(0, -50), (1, -50), (2, -50), (3, -50), (4, -50), (5, -50)]),
        ("opposed pair", [(0, 30), (4, -30)]),
        ("zero", [(1, 0)]),
        ("out of order writes", [(5, 4), (0, -2)]),
    ];

    [Fact]
    public void A_fixed_table_of_shifts_stays_within_5_to_35_and_sums_to_100_and_the_report_matches()
    {
        var (content, start) = Setup(Table.Select(row => ChoiceJson(row.Name.Replace(' ', '_'), row.Shifts)));
        foreach (var row in Table)
        {
            var before = start.Player.Personality;
            var (game, report) = Take(content, start, row.Name.Replace(' ', '_'), 2);

            AssertWithinLimits(game.Player.Personality, row.Name);
            AssertReportMatches(before, game.Player.Personality, report, row.Name);
        }
    }

    [Fact]
    public void Written_shift_reports_the_written_totals_per_trait_before_limits_in_trait_order()
    {
        var (content, start) = Setup([ChoiceJson("go", (5, 4), (0, -2), (5, 6), (2, 100))]);
        var (game, report) = Take(content, start, "go", 2);

        Assert.Equal([Trait.Patience, Trait.Cunning, Trait.Charisma], report.WrittenShift.Select(s => s.Trait).ToList());
        Assert.Equal([-2d, 100d, 10d], report.WrittenShift.Select(s => s.Amount).ToList());
        Assert.Equal(35, game.Player.Personality.Cunning);
    }

    [Fact]
    public void Trait_changes_report_what_actually_happened_not_what_was_written()
    {
        var (content, start) = Setup([ChoiceJson("go", (2, 100))]);
        var before = start.Player.Personality;
        var (game, report) = Take(content, start, "go", 2);

        var cunning = report.TraitChanges.Single(c => c.Trait == Trait.Cunning);
        Assert.Equal(before.Cunning, cunning.Before);
        Assert.Equal(35, cunning.After);
        Assert.Equal(25, cunning.Change);
        Assert.Equal(100, report.WrittenShift.Single().Amount);
        Assert.Equal(PersonalityRules.Traits.Where(t => before[t] != game.Player.Personality[t]).ToList(), report.TraitChanges.Select(c => c.Trait).ToList());
    }

    [Fact]
    public void A_shift_pinned_at_a_limit_reports_no_gain_for_that_trait_but_still_writes_it()
    {
        var (content, start) = Setup([ChoiceJson("up", (2, 100)), ChoiceJson("again", (2, 5))]);
        var (game, _) = Take(content, start, "up", 2);
        var (after, report) = Take(content, game, "again", 10);

        Assert.DoesNotContain(report.TraitChanges, c => c.Trait == Trait.Cunning && c.Change > 0);
        Assert.Equal(5, report.WrittenShift.Single(s => s.Trait == Trait.Cunning).Amount);
        Assert.Equal(35, after.Player.Personality.Cunning);
    }

    private static List<(int Trait, double Amount)[]> Plans(int seed, int count)
    {
        var random = new Random(seed);
        var plans = new List<(int Trait, double Amount)[]>();
        for (var i = 0; i < count; i++)
        {
            var shifts = new (int Trait, double Amount)[random.Next(1, 4)];
            for (var k = 0; k < shifts.Length; k++)
            {
                var amount = random.Next(0, 6) == 0 ? random.Next(60, 200) * (random.Next(2) == 0 ? -1 : 1) : Math.Round((random.NextDouble() * 24) - 12, 2);
                shifts[k] = (random.Next(6), amount);
            }

            plans.Add(shifts);
        }

        return plans;
    }

    [Fact]
    public void A_seeded_sequence_of_many_shifts_never_leaves_the_limits_and_every_report_matches()
    {
        var plans = Plans(20261030, 250);
        var (content, game) = Setup(plans.Select((p, i) => ChoiceJson($"c{i}", p)));
        var sawCeiling = false;
        var sawFloor = false;
        for (var i = 0; i < plans.Count; i++)
        {
            var before = game.Player.Personality;
            var (next, report) = Take(content, game, $"c{i}", 2 + (i * 2));
            game = next;
            var after = game.Player.Personality;

            AssertWithinLimits(after, $"shift {i}");
            AssertReportMatches(before, after, report, $"shift {i}");
            Assert.Equal(plans[i].Select(s => s.Trait).Distinct().Order().Select(t => (Trait)t).ToList(), report.WrittenShift.Select(s => s.Trait).ToList());
            sawCeiling |= PersonalityRules.Traits.Any(t => after[t] == 35);
            sawFloor |= PersonalityRules.Traits.Any(t => after[t] == 5);
        }

        Assert.True(sawCeiling && sawFloor, "the sequence never pushed a trait to both limits");
    }

    [Fact]
    public void The_same_seeded_sequence_gives_the_same_personality_twice()
    {
        static Persona Run()
        {
            var plans = Plans(7, 60);
            var (content, game) = Setup(plans.Select((p, i) => ChoiceJson($"c{i}", p)));
            for (var i = 0; i < plans.Count; i++)
            {
                game = Take(content, game, $"c{i}", 2 + (i * 2)).Game;
            }

            return game.Player.Personality;
        }

        Assert.Equal(Run(), Run());
    }
}
