using System.Collections.Immutable;
using AncientOrder.Rules.Combat;
using AncientOrder.Rules.Party;
using AncientOrder.Rules.State;

namespace AncientOrder.Rules.Tests.Fixtures;

/// <summary>
/// Test-only encounters: encounter_demo and encounter_test as today's enemies, enemies only. Today's player parties live in the
/// characters: the demo's is Elena, Lars, Kade (<see cref="TestCast"/>), encounter_test's is the warrior, scout, and mage below.
/// The parity contents use a lineup of 3 and no player combat stats, so a fight starts with today's three player-side fighters
/// (only the fighter ids differ: character ids instead of player_elena and the like).
/// </summary>
public static class TestEncounters
{
    public const string DemoId = "encounter_demo";
    public const string TestId = "encounter_test";

    public const string WarriorId = "npc_warrior";
    public const string ScoutId = "npc_scout";
    public const string MageId = "npc_mage";

    public static EncounterDefinition Demo { get; } = new(
        DemoId,
        "Demo Encounter: Order vs Shadow Guard (3v3)",
        [
            Enemy("enemy_knight", "Shadow Knight", "enemy", 3, 130, 88, 75, ElementalPath.Earth, Skills(0.7, 0.55, 0.25, 0.2, 0.8, 0.1, 0.2, 0.9, 0.1)),
            Enemy("enemy_rogue", "Shadow Rogue", "enemy", 3, 100, 95, 95, ElementalPath.Fire, Skills(0.5, 0.45, 0.2, 0.45, 0.88, 0.2, 0.4, 0.98, 0.2)),
            Enemy("enemy_mage", "Shadow Mage", "enemy", 3, 90, 105, 80, ElementalPath.Shadow, Skills(0.3, 0.35, 0.1, 0.5, 0.9, 0.25, 0.3, 0.94, 0.15)),
        ]);

    public static EncounterDefinition Test { get; } = new(
        TestId,
        "Test Encounter (3v3)",
        [
            Enemy("enemy_brute", "Iron Brute", TestArchetypes.Elena, 2.0, 110, 20, 10, ElementalPath.Earth, Skills(0.65, 0.55, 0.25, 0.2, 0.8, 0.3, 0.2, 0.93, 0.2)),
            Enemy("enemy_archer", "Shadow Archer", "kade_bold_outlaw", 1.5, 85, 16, 18, ElementalPath.Shadow, Skills(0.35, 0.35, 0.15, 0.6, 0.89, 0.2, 0.25, 0.97, 0.2)),
            Enemy("enemy_shaman", "Bog Shaman", "lars_cautious_merchant", 1.5, 75, 19, 11, ElementalPath.Water, Skills(0.45, 0.4, 0.15, 0.35, 0.83, 0.2, 0.3, 1.0, 0.2)),
        ]);

    public static ImmutableArray<EncounterDefinition> All { get; } = [Demo, Test];

    /// <summary>encounter_test's player party: personalities are Elena's, Lars's, and Kade's, so synergy matches today's (which read the three NPCs).</summary>
    public static CharacterDefinition Warrior { get; } = new(
        WarriorId, "Warrior", "warrior", TestCast.Elena.Personality, 0, 0,
        new CombatProfile(2.0, 120, 18, 14, ElementalPath.Fire));

    public static CharacterDefinition Scout { get; } = new(
        ScoutId, "Scout", "scout", TestCast.Lars.Personality, 0, 0,
        new CombatProfile(1.5, 90, 14, 20, ElementalPath.Air));

    public static CharacterDefinition Mage { get; } = new(
        MageId, "Mage", "mage", TestCast.Kade.Personality, 0, 0,
        new CombatProfile(1.5, 80, 22, 12, ElementalPath.Shadow));

    public static ImmutableArray<CharacterDefinition> TestParty { get; } = [Warrior, Scout, Mage];

    public static ImmutableArray<string> DemoLineup { get; } = [TestCast.ElenaId, TestCast.LarsId, TestCast.KadeId];

    public static ImmutableArray<string> TestLineup { get; } = [WarriorId, ScoutId, MageId];

    /// <summary>Elena, Lars, and Kade with today's demo stats, lineup size 3, no player combat stats, both encounters.</summary>
    public static GameContent DemoPartyContent { get; } = TestContent.Create(new GameSettings("Kael", new LineupLimits(3, 3)) { StartingParty = [.. TestCast.All.Select(c => c.Id)] }, TestCast.All);

    /// <summary>Warrior, scout, and mage with encounter_test's player-party stats, lineup size 3, no player combat stats, both encounters.</summary>
    public static GameContent TestPartyContent { get; } = TestContent.Create(new GameSettings("Kael", new LineupLimits(3, 3)) { StartingParty = [.. TestParty.Select(c => c.Id)] }, TestParty);

    public static GameState DemoPartyGame() => WithLineup(DemoPartyContent, DemoLineup);

    public static GameState TestPartyGame() => WithLineup(TestPartyContent, TestLineup);

    public static GameState WithLineup(GameContent content, ImmutableArray<string> lineup) =>
        TestContent.NewGame(content) with { Lineup = lineup };

    public static CombatantConfig Enemy(
        string id, string name, string archetype, double rank, double stamina, double power, double speed, ElementalPath path, ReactionSkills skills) =>
        new(id, name, archetype, rank, stamina, power, speed, path, skills);

    public static ReactionSkills Skills(
        double blockSr, double blockSmr, double blockFmr,
        double dodgeSr, double dodgeSmr, double dodgeFmr,
        double parrySr, double parrySmr, double parryFmr) =>
        new(
            new DefenseSkill(blockSr, blockSmr, blockFmr),
            new DefenseSkill(dodgeSr, dodgeSmr, dodgeFmr),
            new DefenseSkill(parrySr, parrySmr, parryFmr));
}
