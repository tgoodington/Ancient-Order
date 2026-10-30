using System.Collections.Immutable;
using AncientOrder.Rules.Combat;
using AncientOrder.Rules.Combat.Ai;
using AncientOrder.Rules.Common;
using AncientOrder.Rules.Content.Packs;
using AncientOrder.Rules.Party;
using AncientOrder.Rules.State;

namespace AncientOrder.Rules.Tests.ContentPacks;

public class ContentPackLoaderTests
{
    private const string Pack = """{ "format": "ancient-order-content", "version": 1, "id": "t", "name": "Test", "standIn": false, "files": ["a.json", "b.json"] }""";

    private const string Settings = """{ "settings": { "playerName": "P", "lineup": { "min": 1, "max": 1 } } }""";

    private static MemoryContentSource Memory(params (string Path, string Text)[] files) =>
        new(files.Select(f => KeyValuePair.Create(f.Path, f.Text)));

    private static string[] Paths(Result<GameContent> result) => [.. result.Error.Details.Select(d => d.Path)];

    [Fact]
    public void Standin_pack_on_disk_equals_the_content_the_old_C_sharp_stand_in_built()
    {
        var folder = Path.Combine(SolutionRoot(), "godot", "content", "standin");
        var loaded = ContentPackLoader.Load(new FolderContentSource(folder));
        Assert.True(loaded.IsOk, loaded.IsOk ? string.Empty : string.Join("\n", loaded.Error.Details));
        var actual = loaded.Value;
        var expected = ExpectedStandIn();

        // The pack has grown past what the old C# stand-in built: a bigger lineup, a town, a reach, display switches, and a locked line. The rest must still match.
        var grown = expected.Settings with
        {
            StartingParty = [],
            Lineup = actual.Settings.Lineup,
            StartingTown = actual.Settings.StartingTown,
            Reach = actual.Settings.Reach,
            Display = actual.Settings.Display,
            Conversations = actual.Settings.Conversations,
        };
        Assert.Equal(grown, actual.Settings with { StartingParty = [] });
        Assert.Equal(expected.Settings.StartingParty, actual.Settings.StartingParty);
        Assert.Equal(expected.Characters.Definitions, actual.Characters.Definitions.Take(expected.Characters.Definitions.Length));
        Assert.Equal(expected.Synergy.Bonuses, actual.Synergy.Bonuses.Select(b => b with { Feel = null }));
        Assert.Equal(expected.Synergy.Resolution, actual.Synergy.Resolution);

        Assert.True(actual.Archetypes.Definitions.Length >= expected.Archetypes.Definitions.Length);
        for (var i = 0; i < expected.Archetypes.Definitions.Length; i++)
        {
            var e = expected.Archetypes.Definitions[i];
            var a = actual.Archetypes.Definitions[i];
            Assert.Equal(e.Id, a.Id);
            if (e.Behavior is ArchetypeBehavior.Scored es)
            {
                var asc = Assert.IsType<ArchetypeBehavior.Scored>(a.Behavior);
                foreach (var type in Enum.GetValues<ActionType>())
                {
                    Assert.Equal(es.Profile.BaseScores[type], asc.Profile.BaseScores[type]);
                }

                Assert.Equal(es.Profile.FactorWeights.Count, asc.Profile.FactorWeights.Count);
                foreach (var name in ScoringFactors.Names)
                {
                    Assert.Equal(es.Profile.FactorWeights[name], asc.Profile.FactorWeights[name]);
                }
            }
            else
            {
                Assert.IsType<ArchetypeBehavior.Basic>(a.Behavior);
            }
        }

        Assert.Equal(expected.Encounters.Definitions.Length, actual.Encounters.Definitions.Length);
        for (var i = 0; i < expected.Encounters.Definitions.Length; i++)
        {
            var e = expected.Encounters.Definitions[i];
            var a = actual.Encounters.Definitions[i];
            Assert.Equal(e.Id, a.Id);
            Assert.Equal(e.Name, a.Name);
            Assert.Equal(e.Enemies, a.Enemies);
        }

        Assert.Equal(new PackInfo("standin", "STAND-IN Pack", true), actual.Pack);
        Assert.Equal(PackInfo.Unnamed, expected.Pack);
    }

    [Fact]
    public void A_file_listed_in_the_index_is_picked_up_and_merged_in_index_order()
    {
        var result = ContentPackLoader.Load(Memory(
            ("pack.json", Pack),
            ("a.json", Settings),
            ("b.json", """{ "archetypes": [{ "id": "x" }], "characters": [] }""")));

        Assert.True(result.IsOk);
        Assert.Equal("P", result.Value.Settings.PlayerName);
        Assert.True(result.Value.Archetypes.TryGet("x", out _));
        Assert.Equal(new PackInfo("t", "Test", false), result.Value.Pack);
    }

    [Fact]
    public void Problems_in_several_files_are_all_reported_with_file_and_place()
    {
        var result = ContentPackLoader.Load(Memory(
            ("pack.json", Pack),
            ("a.json", "{ \"settings\": {\n  \"playerName\": \"P\" \"lineup\": { \"min\": 1, \"max\": 1 } } }"),
            ("b.json", """{ "characters": [ { "id": "c", "name": "C", "archetypeId": "x", "personality": { "patience": 1, "empathy": 1, "cunning": 1, "logic": 1, "kindness": 1, "charisma": 1 }, "startingAffection": 0, "startingTrust": 0, "combat": { "rank": 1, "stamina": 1, "power": 1, "speed": 1, "elementalPath": "Fire" } } ] }""")));

        Assert.False(result.IsOk);
        Assert.Equal(ErrorCode.VALIDATION_ERROR, result.Error.Code);
        var paths = Paths(result);
        Assert.Contains(paths, p => p.StartsWith("a.json:line 2, column ", StringComparison.Ordinal));
        Assert.Contains("b.json:/characters/0/combat/elementalPath", paths);
    }

    [Fact]
    public void Unknown_reference_is_told_as_the_file_and_pointer_it_came_from()
    {
        var result = ContentPackLoader.Load(Memory(
            ("pack.json", Pack),
            ("a.json", Settings),
            ("b.json", """{ "characters": [ { "id": "c", "name": "C", "archetypeId": "nope", "personality": { "patience": 20, "empathy": 20, "cunning": 10, "logic": 15, "kindness": 20, "charisma": 15 }, "startingAffection": 0, "startingTrust": 0 } ] }""")));

        Assert.False(result.IsOk);
        Assert.Equal(["b.json:/characters/0/archetypeId"], Paths(result));
    }

    [Fact]
    public void Unreadable_file_unknown_field_and_bad_paths_are_refused_not_thrown()
    {
        var result = ContentPackLoader.Load(Memory(
            ("pack.json", """{ "format": "ancient-order-content", "version": 1, "id": "t", "name": "T", "standIn": false, "files": ["missing.json", "a.json", "../x.json", "sub\\y.json"] }"""),
            ("a.json", """{ "settings": { "playerName": "P", "lineup": { "min": 1, "max": 1 }, "bogus": 3 } }""")));

        Assert.False(result.IsOk);
        var paths = Paths(result);
        Assert.Contains("missing.json", paths);
        Assert.Contains(paths, p => p.StartsWith("a.json:/settings", StringComparison.Ordinal));
        Assert.Contains("pack.json:/files/2", paths);
        Assert.Contains("pack.json:/files/3", paths);
    }

    [Fact]
    public void Missing_index_is_refused()
    {
        var result = ContentPackLoader.Load(Memory());
        Assert.False(result.IsOk);
        Assert.Equal(["pack.json"], Paths(result));
    }

    [Fact]
    public void Duplicate_ids_across_files_and_a_second_settings_section_are_refused_at_the_later_item()
    {
        var result = ContentPackLoader.Load(Memory(
            ("pack.json", Pack),
            ("a.json", """{ "settings": { "playerName": "P", "lineup": { "min": 1, "max": 1 } }, "archetypes": [{ "id": "x" }] }"""),
            ("b.json", """{ "settings": { "playerName": "Q", "lineup": { "min": 1, "max": 1 } }, "archetypes": [{ "id": "x" }] }""")));

        Assert.False(result.IsOk);
        Assert.Equal(["b.json:/archetypes/0/id", "b.json:/settings"], Paths(result).Order(StringComparer.Ordinal));
    }

    private static string SolutionRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "AncientOrder.sln")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("AncientOrder.sln not found above the test folder");
    }

    // The stand-in content exactly as godot/StandIn/StandInContent.cs built it before the pack replaced it.
    private static GameContent ExpectedStandIn()
    {
        var settings = new GameSettings(
            "STAND-IN Player",
            new LineupLimits(2, 2),
            PlayerCombat: new CombatProfile(3, 120, 80, 80, ElementalPath.Air))
        {
            StartingParty = ["standin_companion_a", "standin_companion_b"],
        };

        ImmutableArray<CharacterDefinition> characters =
        [
            new("standin_companion_a", "STAND-IN Companion A", "standin_archetype_scored", Traits(20, 20, 10, 15, 20, 15), 0, 0,
                new CombatProfile(3, 120, 85, 90, ElementalPath.Light)),
            new("standin_companion_b", "STAND-IN Companion B", "standin_archetype_basic", Traits(12, 8, 25, 18, 10, 27), 0, 0,
                new CombatProfile(3, 110, 100, 100, ElementalPath.Fire)),
        ];

        ImmutableArray<ArchetypeDefinition> archetypes =
        [
            new("standin_archetype_scored", new ArchetypeBehavior.Scored(new DecisionProfile(
                new Dictionary<ActionType, double>
                {
                    [ActionType.Attack] = 0.3,
                    [ActionType.Defend] = 0.5,
                    [ActionType.Evade] = 0.3,
                    [ActionType.Special] = 0.4,
                    [ActionType.Group] = 0.2,
                }.ToImmutableDictionary(),
                new Dictionary<string, double>
                {
                    ["ownStamina"] = 1.0,
                    ["allyInDanger"] = 1.8,
                    ["targetVulnerability"] = 0.5,
                    ["energyAvailability"] = 1.2,
                    ["speedAdvantage"] = 0.3,
                    ["roundPhase"] = 0.8,
                    ["teamBalance"] = 1.4,
                }.ToImmutableDictionary(StringComparer.Ordinal)))),
            new("standin_archetype_basic", new ArchetypeBehavior.Basic()),
        ];

        var synergy = new SynergySettings(
            [
                new SynergyBonusDefinition("STAND-IN Well Rounded", SynergyRule.Coverage, 25, CombatStat.Power, StatStep.Multiply, 1.10),
                new SynergyBonusDefinition("STAND-IN Bond", SynergyRule.Alignment, 80, CombatStat.Speed, StatStep.Multiply, 1.10),
            ],
            SynergyResolution.BestOnly);

        ImmutableArray<EncounterDefinition> encounters =
        [
            new("standin_encounter", "STAND-IN Encounter", [
                new CombatantConfig(
                    "standin_enemy",
                    "STAND-IN Enemy",
                    "standin_archetype_basic",
                    3,
                    400,
                    40,
                    60,
                    ElementalPath.Earth,
                    new ReactionSkills(new DefenseSkill(0.5, 0.3, 0.1), new DefenseSkill(0.4, 0.8, 0.15), new DefenseSkill(0.3, 0.9, 0.1))),
            ]),
        ];

        return GameContent.Create(settings, characters, archetypes, synergy, encounters).Value;
    }

    private static AncientOrder.Rules.Personality.Personality Traits(double a, double b, double c, double d, double e, double f) =>
        new() { Patience = a, Empathy = b, Cunning = c, Logic = d, Kindness = e, Charisma = f };
}
