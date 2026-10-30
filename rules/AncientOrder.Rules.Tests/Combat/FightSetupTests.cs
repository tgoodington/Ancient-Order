using System.Collections.Immutable;
using AncientOrder.Rules.Combat;
using AncientOrder.Rules.Common;
using AncientOrder.Rules.Party;
using AncientOrder.Rules.Saves;
using AncientOrder.Rules.State;
using AncientOrder.Rules.Tests.Fixtures;
using AncientOrder.Rules.Tests.Support;

namespace AncientOrder.Rules.Tests.Combat;

// Carried over from src/combat/sync.test.ts (42 tests) and rewritten for the player-plus-lineup side (T8):
//  - initCombatState(gameState, encounter) is FightSetup.Start(content, game, encounterId, seed, now).Value.Combat; the player's side
//    is the player (when the content gives the player combat stats) plus the lineup, so the TypeScript encounters' playerParty
//    configs become content (a hero profile and lineup characters) and fighter ids are character ids.
//  - syncToGameState is FightSetup.Sync; endCombat is FightSetup.End (it takes no result).
//  - Dropped: the two endCombat conversationLog tests (the log entry is retired), the conversationLog / currentDialogueNode / saveSlot
//    preservation tests (GameState no longer has those fields), and the missing-personality guard (a player always has one).
// New: refusals, reaction progress by character id, stats not rounded without a modifier, enemy ascension, demo-fight parity,
// EncounterCatalog validation.
internal static class SetupFixtures
{
    public const ulong Seed = 12345;
    public const long Later = TestContent.Now + 5000;
    public const string FoeId = "encounter_foe";

    public static CombatProfile Hero { get; } = new(2.0, 100, 10, 10, ElementalPath.Fire);

    public static ReactionSkills FoeSkills { get; } = TestEncounters.Skills(0.6, 0.3, 0.1, 0.5, 0.8, 0.1, 0.4, 0.9, 0.1);

    public static EncounterDefinition Foe { get; } = new(
        FoeId,
        "Test Encounter",
        [new CombatantConfig("enemy_1", "Foe", "enemy", 2.0, 100, 10, 10, ElementalPath.Fire, FoeSkills, AscensionLevel.Level0)]);

    public static GameContent Content(
        CombatProfile? hero,
        int lineupSize,
        ImmutableArray<CharacterDefinition>? characters = null,
        SynergySettings? synergy = null,
        ImmutableArray<EncounterDefinition>? encounters = null) =>
        TestContent.Create(
            new GameSettings("Kael", new LineupLimits(lineupSize, lineupSize), null, hero) { StartingParty = [.. (characters ?? TestCast.All).Select(c => c.Id)] },
            characters ?? TestCast.All,
            synergy ?? SynergySettings.None,
            encounters ?? [Foe]);

    public static GameState Game(GameContent content, params string[] lineup) =>
        TestContent.NewGame(content) with { Lineup = [.. lineup] };

    public static GameState Start(GameContent content, GameState game, string encounterId = FoeId, long now = Later)
    {
        var result = FightSetup.Start(content, game, encounterId, Seed, now);
        Assert.True(result.IsOk, result.IsOk ? string.Empty : result.Error.Message);
        return result.Value;
    }

    public static CombatState StartCombat(GameContent content, GameState game, string encounterId = FoeId) =>
        Start(content, game, encounterId).Combat!;

    public static RuleError Refused(Result<GameState> result, ErrorCode code)
    {
        Assert.False(result.IsOk);
        Assert.Equal(code, result.Error.Code);
        return result.Error;
    }

    /// <summary>The state makeCombatState built in sync.test.ts: player_1 at 75 of 100 stamina and enemy_1 at 50 of 90, round 2.</summary>
    public static CombatState MadeCombat(int round = 2, CombatStatus status = CombatStatus.Active)
    {
        var skills1 = new ReactionSkills(new DefenseSkill(0.6, 0.3, 0.1), new DefenseSkill(0.5, 0.8, 0.1), new DefenseSkill(0.4, 0.9, 0.1));
        var skills2 = new ReactionSkills(new DefenseSkill(0.5, 0.25, 0.1), new DefenseSkill(0.4, 0.8, 0.1), new DefenseSkill(0.3, 0.9, 0.1));
        var player = new Combatant("player_1", "Hero", "scout", 2.0, 75, 100, 12, 10, 1, 3, AscensionLevel.Level0, [], ElementalPath.Fire, skills1, null, false);
        var enemy = new Combatant("enemy_1", "Foe", "raider", 2.0, 50, 90, 11, 9, 0, 3, AscensionLevel.Level0, [], ElementalPath.Water, skills2, null, false);
        return new CombatState(round, CombatPhase.AiDecision, [player], [enemy], [], [], status, DiceState.FromSeed(Seed));
    }

    public static Combatant Fighter(
        string id, string name, string? archetype, double rank, double stamina, double power, double speed, ElementalPath path,
        ReactionSkills skills, ReactionProgress? progress) =>
        new(id, name, archetype, rank, stamina, stamina, power, speed, 0, 3, AscensionLevel.Level0, [], path, skills, progress, false);

    public static ReactionSkills Rank1 { get; } = ReactionProgression.ReactionSkillsFromProgress(ReactionProgression.Zero);
}

public class InitCombatState
{
    private static readonly GameContent OnePlayer = SetupFixtures.Content(SetupFixtures.Hero, 0);

    [Fact(DisplayName = "produces a CombatState with round=1, phase=AI_DECISION, status=active")]
    public void Round_phase_status()
    {
        var combat = SetupFixtures.StartCombat(OnePlayer, SetupFixtures.Game(OnePlayer));

        Assert.Equal(1, combat.Round);
        Assert.Equal(CombatPhase.AiDecision, combat.Phase);
        Assert.Equal(CombatStatus.Active, combat.Status);
    }

    [Fact(DisplayName = "produces empty actionQueue and roundHistory")]
    public void Empty_queue_and_history()
    {
        var combat = SetupFixtures.StartCombat(OnePlayer, SetupFixtures.Game(OnePlayer));

        Assert.Empty(combat.ActionQueue);
        Assert.Empty(combat.RoundHistory);
    }

    [Fact(DisplayName = "maps playerParty from encounter config (1 player)")]
    public void One_player()
    {
        // Rewritten: the player is the player character (id and name from the game, not the encounter).
        var combat = SetupFixtures.StartCombat(OnePlayer, SetupFixtures.Game(OnePlayer));

        var hero = Assert.Single(combat.PlayerParty);
        Assert.Equal(TestContent.PlayerId, hero.Id);
        Assert.Equal("Kael", hero.Name);
        Assert.False(hero.IsKO);
    }

    [Fact(DisplayName = "maps enemyParty from encounter config (1 enemy)")]
    public void One_enemy()
    {
        var combat = SetupFixtures.StartCombat(OnePlayer, SetupFixtures.Game(OnePlayer));

        var enemy = Assert.Single(combat.EnemyParty);
        Assert.Equal("enemy_1", enemy.Id);
        Assert.Equal("Foe", enemy.Name);
        Assert.False(enemy.IsKO);
    }

    [Fact(DisplayName = "sets stamina equal to config stamina (maxStamina matches stamina)")]
    public void Stamina_equals_max()
    {
        var combat = SetupFixtures.StartCombat(OnePlayer, SetupFixtures.Game(OnePlayer));

        Assert.Equal(100, combat.PlayerParty[0].Stamina);
        Assert.Equal(100, combat.PlayerParty[0].MaxStamina);
    }

    [Fact(DisplayName = "correctly maps 3v3 encounter with all combatants")]
    public void Three_v_three()
    {
        // Rewritten: the player plus a lineup of two is the 3-strong player side; encounter_test has 3 enemies.
        var content = SetupFixtures.Content(SetupFixtures.Hero, 2, encounters: TestEncounters.All);
        var game = SetupFixtures.Game(content, TestCast.ElenaId, TestCast.LarsId);

        var combat = SetupFixtures.StartCombat(content, game, TestEncounters.TestId);

        Assert.Equal(3, combat.PlayerParty.Length);
        Assert.Equal(3, combat.EnemyParty.Length);
        var ids = combat.PlayerParty.Select(c => c.Id).ToList();
        Assert.Contains(TestContent.PlayerId, ids);
        Assert.Contains(TestCast.ElenaId, ids);
        Assert.Contains(TestCast.LarsId, ids);
    }

    [Fact(DisplayName = "preserves combatant stats from config (rank, power, speed, archetype)")]
    public void Preserves_stats()
    {
        // Rewritten: no synergy settings, so base values are unchanged. The player has no archetype; a lineup member has its own.
        var hero = new CombatProfile(3.5, 100, 20, 15, ElementalPath.Fire);
        var content = SetupFixtures.Content(hero, 1, TestEncounters.TestParty);
        var game = SetupFixtures.Game(content, TestEncounters.WarriorId);

        var combat = SetupFixtures.StartCombat(content, game);

        Assert.Equal(3.5, combat.PlayerParty[0].Rank);
        Assert.Equal(20, combat.PlayerParty[0].Power);
        Assert.Equal(15, combat.PlayerParty[0].Speed);
        Assert.Null(combat.PlayerParty[0].ArchetypeId);
        Assert.Equal("warrior", combat.PlayerParty[1].ArchetypeId);
        Assert.Equal(18, combat.PlayerParty[1].Power);
    }

    [Fact(DisplayName = "initialises combatants with ascensionLevel defaulting to 0")]
    public void Ascension_default()
    {
        var combat = SetupFixtures.StartCombat(OnePlayer, SetupFixtures.Game(OnePlayer));

        Assert.Equal(AscensionLevel.Level0, combat.PlayerParty[0].AscensionLevel);
        Assert.Equal(AscensionLevel.Level0, combat.EnemyParty[0].AscensionLevel);
    }

    [Fact(DisplayName = "is a pure function: same inputs produce identical output")]
    public void Pure()
    {
        var content = TestEncounters.DemoPartyContent;
        var game = TestEncounters.DemoPartyGame();

        var first = SetupFixtures.Start(content, game, TestEncounters.DemoId);
        var second = SetupFixtures.Start(content, game, TestEncounters.DemoId);

        StateAssert.Equivalent(first, second);
    }

    [Fact(DisplayName = "does not mutate the input gameState")]
    public void No_mutation()
    {
        var game = SetupFixtures.Game(OnePlayer);
        var originalTimestamp = game.Timestamp;

        SetupFixtures.Start(OnePlayer, game);

        Assert.Equal(originalTimestamp, game.Timestamp);
        Assert.Null(game.Combat);
        Assert.Empty(game.ReactionProgress);
    }
}

public class InitCombatStateReactionProgressionSeeding
{
    private static readonly GameContent OnePlayer = SetupFixtures.Content(SetupFixtures.Hero, 0);

    [Fact(DisplayName = "replaces player-party fixture rates with rank-1 derived rates for a new game")]
    public void New_game_rank_1()
    {
        var player = SetupFixtures.StartCombat(OnePlayer, SetupFixtures.Game(OnePlayer)).PlayerParty[0];

        Assert.Equal(ReactionProgression.Zero, player.ReactionProgress);
        StateAssert.Equivalent(ReactionProgression.ReactionSkillsFromProgress(ReactionProgression.Zero), player.ReactionSkills);
        Assert.Equal(0.55, player.ReactionSkills.Block.SMR, 5);
    }

    [Fact(DisplayName = "derives rates from an existing per-combatant XP entry")]
    public void Existing_xp()
    {
        var game = SetupFixtures.Game(OnePlayer);
        game = game with { ReactionProgress = game.ReactionProgress.SetItem(TestContent.PlayerId, new ReactionProgress(215, 0, 0)) };

        var player = SetupFixtures.StartCombat(OnePlayer, game).PlayerParty[0];

        Assert.Equal(new ReactionProgress(215, 0, 0), player.ReactionProgress);
        Assert.Equal(ReactionProgression.RatesForRank(ReactionKind.Block, 3), player.ReactionSkills.Block);
        Assert.Equal(ReactionProgression.RatesForRank(ReactionKind.Dodge, 1), player.ReactionSkills.Dodge);
    }

    [Fact(DisplayName = "leaves enemy reaction skills as literal fixtures with no progress")]
    public void Enemy_literal()
    {
        var enemy = SetupFixtures.StartCombat(OnePlayer, SetupFixtures.Game(OnePlayer)).EnemyParty[0];

        Assert.Null(enemy.ReactionProgress);
        Assert.Equal(new DefenseSkill(0.6, 0.3, 0.1), enemy.ReactionSkills.Block);
    }
}

public class SyncToGameStateReactionProgressionHarvest
{
    [Fact(DisplayName = "writes each player combatant live XP back into the persistent map")]
    public void Writes_back()
    {
        var game = TestContent.NewGame();
        var baseCombat = SetupFixtures.MadeCombat();
        var earned = new ReactionProgress(8, 4, 0);
        var combat = baseCombat with { PlayerParty = [.. baseCombat.PlayerParty.Select(c => c with { ReactionProgress = earned })] };

        var synced = FightSetup.Sync(game, combat, SetupFixtures.Later);

        Assert.Equal(earned, synced.ReactionProgress[baseCombat.PlayerParty[0].Id]);
    }

    [Fact(DisplayName = "never records enemy progress and preserves unrelated map entries")]
    public void No_enemy_progress()
    {
        var game = TestContent.NewGame();
        game = game with { ReactionProgress = game.ReactionProgress.SetItem("someoneElse", new ReactionProgress(100, 0, 0)) };

        var synced = FightSetup.Sync(game, SetupFixtures.MadeCombat(), SetupFixtures.Later);

        Assert.Equal(new ReactionProgress(100, 0, 0), synced.ReactionProgress["someoneElse"]);
        Assert.False(synced.ReactionProgress.ContainsKey("enemy_1"));
    }
}

public class SyncToGameState
{
    [Fact(DisplayName = "returns a new GameState with combatState set")]
    public void Sets_combat()
    {
        var game = TestContent.NewGame();
        var combat = SetupFixtures.MadeCombat();

        var synced = FightSetup.Sync(game, combat, SetupFixtures.Later);

        Assert.NotNull(synced.Combat);
        Assert.Same(combat, synced.Combat);
    }

    [Fact(DisplayName = "preserves player personality unchanged")]
    public void Personality()
    {
        var game = TestContent.NewGame();

        var synced = FightSetup.Sync(game, SetupFixtures.MadeCombat(), SetupFixtures.Later);

        Assert.Equal(game.Player.Personality, synced.Player.Personality);
    }

    [Fact(DisplayName = "preserves NPC data unchanged")]
    public void Npc_data()
    {
        var game = TestContent.NewGame();

        var synced = FightSetup.Sync(game, SetupFixtures.MadeCombat(), SetupFixtures.Later);

        foreach (var definition in TestCast.All)
        {
            Assert.Equal(game.Characters[definition.Id], synced.Characters[definition.Id]);
        }
    }

    [Fact(DisplayName = "preserves player id and name unchanged")]
    public void Player_identity()
    {
        var game = TestContent.NewGame();

        var synced = FightSetup.Sync(game, SetupFixtures.MadeCombat(), SetupFixtures.Later);

        Assert.Equal(game.Player.Id, synced.Player.Id);
        Assert.Equal(game.Player.Name, synced.Player.Name);
    }

    [Fact(DisplayName = "returns a different object reference (immutable)")]
    public void New_reference()
    {
        var game = TestContent.NewGame();

        Assert.NotSame(game, FightSetup.Sync(game, SetupFixtures.MadeCombat(), SetupFixtures.Later));
    }

    [Fact(DisplayName = "does not mutate the input gameState")]
    public void No_mutation()
    {
        var game = TestContent.NewGame();

        FightSetup.Sync(game, SetupFixtures.MadeCombat(), SetupFixtures.Later);

        Assert.Null(game.Combat);
    }

    [Fact(DisplayName = "reflects updated round number from combatState")]
    public void Round_number()
    {
        var synced = FightSetup.Sync(TestContent.NewGame(), SetupFixtures.MadeCombat(round: 5), SetupFixtures.Later);

        Assert.Equal(5, synced.Combat?.Round);
    }
}

public class EndCombat
{
    [Fact(DisplayName = "clears combatState to null on victory")]
    public void Clears_on_victory()
    {
        var withCombat = FightSetup.Sync(TestContent.NewGame(), SetupFixtures.MadeCombat(status: CombatStatus.Victory), SetupFixtures.Later);

        Assert.Null(FightSetup.End(withCombat, SetupFixtures.Later).Combat);
    }

    [Fact(DisplayName = "clears combatState to null on defeat")]
    public void Clears_on_defeat()
    {
        var withCombat = FightSetup.Sync(TestContent.NewGame(), SetupFixtures.MadeCombat(status: CombatStatus.Defeat), SetupFixtures.Later);

        Assert.Null(FightSetup.End(withCombat, SetupFixtures.Later).Combat);
    }

    [Fact(DisplayName = "preserves all non-combat fields after endCombat")]
    public void Preserves_fields()
    {
        var game = TestContent.NewGame();
        var withCombat = FightSetup.Sync(game, SetupFixtures.MadeCombat(), SetupFixtures.Later);

        var ended = FightSetup.End(withCombat, SetupFixtures.Later);

        Assert.Equal(game.Player.Personality, ended.Player.Personality);
        Assert.Equal(game.Player.Id, ended.Player.Id);
        Assert.Equal(game.Player.Name, ended.Player.Name);
    }

    [Fact(DisplayName = "returns a new object reference (immutable)")]
    public void New_reference()
    {
        var withCombat = FightSetup.Sync(TestContent.NewGame(), SetupFixtures.MadeCombat(), SetupFixtures.Later);

        Assert.NotSame(withCombat, FightSetup.End(withCombat, SetupFixtures.Later));
    }

    [Fact(DisplayName = "does not mutate the input gameState")]
    public void No_mutation()
    {
        var withCombat = FightSetup.Sync(TestContent.NewGame(), SetupFixtures.MadeCombat(), SetupFixtures.Later);
        var original = withCombat.Combat;

        FightSetup.End(withCombat, SetupFixtures.Later);

        Assert.Same(original, withCombat.Combat);
    }

    [Fact(DisplayName = "keeps the reaction progress the fight earned")]
    public void Keeps_progress()
    {
        var game = TestContent.NewGame();
        var baseCombat = SetupFixtures.MadeCombat();
        var combat = baseCombat with { PlayerParty = [.. baseCombat.PlayerParty.Select(c => c with { ReactionProgress = new ReactionProgress(8, 4, 0) })] };
        var withCombat = FightSetup.Sync(game, combat, SetupFixtures.Later);

        var ended = FightSetup.End(withCombat, SetupFixtures.Later);

        Assert.Equal(new ReactionProgress(8, 4, 0), ended.ReactionProgress["player_1"]);
        Assert.Equal(SetupFixtures.Later, ended.Timestamp);
    }
}

public class SaveLoadRoundTripDuringActiveCombat
{
    private static readonly GameContent Content = TestEncounters.DemoPartyContent;

    private static GameState ActiveGame() =>
        SetupFixtures.Start(Content, TestEncounters.DemoPartyGame(), TestEncounters.DemoId);

    private static SaveStore NewStore() => new(new MemorySaveBackend(), Content);

    [Fact(DisplayName = "preserves CombatState across a save/load cycle")]
    public async Task Preserves_combat_state()
    {
        var gameWithCombat = ActiveGame();
        var store = NewStore();

        Assert.True((await store.SaveAsync(1, gameWithCombat, TestContent.Now, TestContext.Current.CancellationToken)).IsOk);
        var loaded = (await store.LoadAsync(1, TestContext.Current.CancellationToken)).Value.Game;

        Assert.NotNull(loaded.Combat);
        Assert.Equal(gameWithCombat.Combat!.Round, loaded.Combat.Round);
        Assert.Equal(gameWithCombat.Combat.Phase, loaded.Combat.Phase);
        Assert.Equal(gameWithCombat.Combat.Status, loaded.Combat.Status);
        Assert.Equal(gameWithCombat.Combat.Dice, loaded.Combat.Dice);
    }

    [Fact(DisplayName = "preserves all combatant data across a save/load cycle")]
    public async Task Preserves_combatants()
    {
        var gameWithCombat = ActiveGame();
        var store = NewStore();

        await store.SaveAsync(2, gameWithCombat, TestContent.Now, TestContext.Current.CancellationToken);
        var loaded = (await store.LoadAsync(2, TestContext.Current.CancellationToken)).Value.Game;

        Assert.Equal(3, loaded.Combat!.PlayerParty.Length);
        Assert.Equal(3, loaded.Combat.EnemyParty.Length);
        var loadedHero = loaded.Combat.PlayerParty[0];
        var originalHero = gameWithCombat.Combat!.PlayerParty[0];
        Assert.Equal(originalHero.Id, loadedHero.Id);
        Assert.Equal(originalHero.Stamina, loadedHero.Stamina);
        Assert.Equal(originalHero.MaxStamina, loadedHero.MaxStamina);
        Assert.Equal(originalHero.Power, loadedHero.Power);
        Assert.Equal(originalHero.Speed, loadedHero.Speed);
    }

    [Fact(DisplayName = "preserves non-combat GameState fields across a save/load cycle")]
    public async Task Preserves_non_combat()
    {
        var gameWithCombat = ActiveGame();
        var store = NewStore();

        await store.SaveAsync(3, gameWithCombat, TestContent.Now, TestContext.Current.CancellationToken);
        var loaded = (await store.LoadAsync(3, TestContext.Current.CancellationToken)).Value.Game;

        Assert.Equal(gameWithCombat.Player.Personality, loaded.Player.Personality);
        Assert.Equal(
            TestCast.All.Select(c => c.Id).Order(StringComparer.Ordinal),
            loaded.Characters.Keys.Order(StringComparer.Ordinal));
    }

    [Fact(DisplayName = "allows endCombat after a save/load cycle")]
    public async Task End_after_load()
    {
        var gameWithCombat = ActiveGame();
        var store = NewStore();

        await store.SaveAsync(4, gameWithCombat, TestContent.Now, TestContext.Current.CancellationToken);
        var loaded = (await store.LoadAsync(4, TestContext.Current.CancellationToken)).Value.Game;
        var ended = FightSetup.End(loaded, SetupFixtures.Later);

        Assert.Null(ended.Combat);
        Assert.Equal(gameWithCombat.Player.Personality, ended.Player.Personality);
    }
}

public class InitCombatStateSynergyIntegration
{
    private static CharacterDefinition Companion(string id, AncientOrder.Rules.Personality.Personality personality) =>
        new(id, id, "scout", personality, 0, 0, new CombatProfile(2.0, 100, 10, 10, ElementalPath.Fire));

    // Content refuses a character whose traits are all 25 (the sum must be 100), so the companion's top two traits are 25 and 25: with a
    // player of 25 in every trait, Well Rounded and Bond both score 1.0 and Well Rounded wins the tie, as in the TypeScript test.
    private static readonly AncientOrder.Rules.Personality.Personality Dominant = TestPersonality.Of(25, 25, 10, 10, 15, 15);

    private static AncientOrder.Rules.Personality.Personality Uniform(double value) =>
        TestPersonality.Of(value, value, value, value, value, value);

    private static CombatState StartWith(AncientOrder.Rules.Personality.Personality player, AncientOrder.Rules.Personality.Personality companion, double power = 10)
    {
        var content = SetupFixtures.Content(
            new CombatProfile(2.0, 100, power, 10, ElementalPath.Fire),
            1,
            [Companion("c1", companion) with { Combat = new CombatProfile(2.0, 100, power, 10, ElementalPath.Fire) }],
            TestContent.Synergy,
            [new EncounterDefinition(
                SetupFixtures.FoeId,
                "Test Encounter",
                [new CombatantConfig("e1", "Foe", "enemy", 2.0, 100, 15, 10, ElementalPath.Fire, SetupFixtures.FoeSkills)])]);
        var game = SetupFixtures.Game(content, "c1");
        game = game with { Player = game.Player with { Personality = player } };
        return SetupFixtures.StartCombat(content, game);
    }

    [Fact(DisplayName = "applies no synergy boost when NPCs are empty")]
    public void Empty_lineup()
    {
        // Rewritten: "no NPCs" is a lineup of zero, with synergy settings that would otherwise apply.
        var content = SetupFixtures.Content(new CombatProfile(2.0, 100, 10, 10, ElementalPath.Fire), 0, synergy: TestContent.Synergy);

        var combat = SetupFixtures.StartCombat(content, SetupFixtures.Game(content));

        Assert.Equal(10, combat.PlayerParty[0].Power);
        Assert.Equal(10, combat.PlayerParty[0].Speed);
    }

    [Fact(DisplayName = "produces same CombatState as before when personality does not meet synergy threshold")]
    public void Below_threshold()
    {
        var combat = StartWith(Uniform(5), TestCast.Elena.Personality);

        Assert.Equal(10, combat.PlayerParty[0].Power);
        Assert.Equal(10, combat.PlayerParty[0].Speed);
    }

    [Fact(DisplayName = "applies synergy power boost (Well Rounded) when personality threshold is met")]
    public void Well_rounded()
    {
        var combat = StartWith(Uniform(25), Dominant);

        Assert.Equal(JsMath.Round(10 * 1.10), combat.PlayerParty[0].Power);
    }

    [Fact(DisplayName = "does not apply synergy boost to enemy party")]
    public void Not_to_enemies()
    {
        var combat = StartWith(Uniform(25), Dominant);

        Assert.Equal(15, combat.EnemyParty[0].Power);
        Assert.Equal(11, combat.PlayerParty[0].Power);
    }

    [Fact(DisplayName = "uses Math.round when applying synergy multiplier")]
    public void Rounds()
    {
        var combat = StartWith(Uniform(25), Dominant, power: 9);

        Assert.Equal(JsMath.Round(9 * 1.10), combat.PlayerParty[0].Power);
        Assert.Equal(10, combat.PlayerParty[0].Power);
    }
}

// New (T8): who fights and how they are built.
public class FightSetupPlayerSide
{
    [Fact(DisplayName = "the player's side is the player followed by the lineup in lineup order")]
    public void Player_then_lineup()
    {
        var content = SetupFixtures.Content(SetupFixtures.Hero, 2);

        var combat = SetupFixtures.StartCombat(content, SetupFixtures.Game(content, TestCast.LarsId, TestCast.ElenaId));

        Assert.Equal([TestContent.PlayerId, TestCast.LarsId, TestCast.ElenaId], combat.PlayerParty.Select(c => c.Id));
        Assert.Equal(["Kael", "Lars", "Elena"], combat.PlayerParty.Select(c => c.Name));
    }

    [Fact(DisplayName = "without player combat stats the player does not fight")]
    public void No_player_stats()
    {
        var content = SetupFixtures.Content(null, 2);

        var combat = SetupFixtures.StartCombat(content, SetupFixtures.Game(content, TestCast.ElenaId, TestCast.KadeId));

        Assert.Equal([TestCast.ElenaId, TestCast.KadeId], combat.PlayerParty.Select(c => c.Id));
    }

    [Fact(DisplayName = "fighters are built from their content combat profile with the starting energy of their ascension level")]
    public void Built_from_profile()
    {
        var elena = TestCast.Elena with { Combat = new CombatProfile(2.5, 90, 30, 40, ElementalPath.Water, AscensionLevel.Level2) };
        var content = SetupFixtures.Content(null, 1, [elena]);

        var fighter = SetupFixtures.StartCombat(content, SetupFixtures.Game(content, TestCast.ElenaId)).PlayerParty[0];

        Assert.Equal(TestCast.ElenaId, fighter.Id);
        Assert.Equal(TestArchetypes.Elena, fighter.ArchetypeId);
        Assert.Equal(2.5, fighter.Rank);
        Assert.Equal(90, fighter.Stamina);
        Assert.Equal(90, fighter.MaxStamina);
        Assert.Equal(30, fighter.Power);
        Assert.Equal(40, fighter.Speed);
        Assert.Equal(ElementalPath.Water, fighter.ElementalPath);
        Assert.Equal(AscensionLevel.Level2, fighter.AscensionLevel);
        Assert.Equal(1, fighter.Energy);
        Assert.Equal(1, fighter.MaxEnergy);
        Assert.Empty(fighter.ActiveBuffs);
        Assert.False(fighter.IsKO);
    }

    [Fact(DisplayName = "an enemy's ascension level sets its starting energy; a missing level means level 0")]
    public void Enemy_ascension()
    {
        var foe = new EncounterDefinition(
            SetupFixtures.FoeId,
            "Foe",
            [
                new CombatantConfig("e1", "Elite", "enemy", 2, 100, 10, 10, ElementalPath.Fire, SetupFixtures.FoeSkills, AscensionLevel.Level3),
                new CombatantConfig("e2", "Plain", "enemy", 2, 100, 10, 10, ElementalPath.Fire, SetupFixtures.FoeSkills),
            ]);
        var content = SetupFixtures.Content(SetupFixtures.Hero, 0, encounters: [foe]);

        var combat = SetupFixtures.StartCombat(content, SetupFixtures.Game(content));

        Assert.Equal((AscensionLevel.Level3, 2d, 2d), (combat.EnemyParty[0].AscensionLevel, combat.EnemyParty[0].Energy, combat.EnemyParty[0].MaxEnergy));
        Assert.Equal((AscensionLevel.Level0, 0d, 3d), (combat.EnemyParty[1].AscensionLevel, combat.EnemyParty[1].Energy, combat.EnemyParty[1].MaxEnergy));
    }

    [Fact(DisplayName = "the fight starts with the given dice seed and the game is stamped with now")]
    public void Dice_and_timestamp()
    {
        var content = SetupFixtures.Content(SetupFixtures.Hero, 0);

        var started = SetupFixtures.Start(content, SetupFixtures.Game(content));

        Assert.Equal(DiceState.FromSeed(SetupFixtures.Seed), started.Combat!.Dice);
        Assert.Equal(SetupFixtures.Later, started.Timestamp);
    }

    [Fact(DisplayName = "stamina resets between fights")]
    public void Stamina_resets()
    {
        var content = SetupFixtures.Content(SetupFixtures.Hero, 0);
        var first = SetupFixtures.Start(content, SetupFixtures.Game(content));
        var hurt = first.Combat! with { PlayerParty = [.. first.Combat.PlayerParty.Select(c => c with { Stamina = 7 })] };
        var ended = FightSetup.End(FightSetup.Sync(first, hurt, SetupFixtures.Later), SetupFixtures.Later);

        var second = SetupFixtures.StartCombat(content, ended);

        Assert.Equal(100, second.PlayerParty[0].Stamina);
    }

    [Fact(DisplayName = "synergy is applied to the whole player side and not to enemies")]
    public void Synergy_player_side()
    {
        var synergy = new SynergySettings(
            [new SynergyBonusDefinition("Always", SynergyRule.Coverage, 1, CombatStat.Power, StatStep.Multiply, 2.0)],
            SynergyResolution.BestOnly);
        var content = SetupFixtures.Content(SetupFixtures.Hero, 1, synergy: synergy);

        var combat = SetupFixtures.StartCombat(content, SetupFixtures.Game(content, TestCast.ElenaId));

        Assert.Equal(20, combat.PlayerParty[0].Power);
        Assert.Equal(170, combat.PlayerParty[1].Power);
        Assert.Equal(10, combat.EnemyParty[0].Power);
    }

    [Fact(DisplayName = "a stat with no synergy modifier is not rounded")]
    public void Unmodified_stats_not_rounded()
    {
        var synergy = new SynergySettings(
            [new SynergyBonusDefinition("Quick", SynergyRule.Coverage, 1, CombatStat.Speed, StatStep.Multiply, 2.0)],
            SynergyResolution.BestOnly);
        var hero = new CombatProfile(2.37, 100.5, 10.4, 10.25, ElementalPath.Fire);
        var content = SetupFixtures.Content(hero, 1, synergy: synergy);

        var fighter = SetupFixtures.StartCombat(content, SetupFixtures.Game(content, TestCast.ElenaId)).PlayerParty[0];

        Assert.Equal(2.37, fighter.Rank);
        Assert.Equal(100.5, fighter.Stamina);
        Assert.Equal(100.5, fighter.MaxStamina);
        Assert.Equal(10.4, fighter.Power);
        Assert.Equal(21, fighter.Speed);
    }

    [Fact(DisplayName = "a max stamina synergy sets stamina and max stamina together")]
    public void Max_stamina_synergy()
    {
        var synergy = new SynergySettings(
            [new SynergyBonusDefinition("Tough", SynergyRule.Coverage, 1, CombatStat.MaxStamina, StatStep.Multiply, 1.5)],
            SynergyResolution.BestOnly);
        var content = SetupFixtures.Content(SetupFixtures.Hero, 1, synergy: synergy);

        var fighter = SetupFixtures.StartCombat(content, SetupFixtures.Game(content, TestCast.ElenaId)).PlayerParty[0];

        Assert.Equal(150, fighter.MaxStamina);
        Assert.Equal(150, fighter.Stamina);
    }
}

public class FightSetupReactionProgressByCharacterId
{
    [Fact(DisplayName = "a lineup character is seeded from the progress kept under its character id")]
    public void Seeded_by_character_id()
    {
        var content = SetupFixtures.Content(SetupFixtures.Hero, 1);
        var game = SetupFixtures.Game(content, TestCast.ElenaId);
        game = game with { ReactionProgress = game.ReactionProgress.SetItem(TestCast.ElenaId, new ReactionProgress(0, 215, 345)) };

        var elena = SetupFixtures.StartCombat(content, game).PlayerParty[1];

        Assert.Equal(new ReactionProgress(0, 215, 345), elena.ReactionProgress);
        Assert.Equal(ReactionProgression.RatesForRank(ReactionKind.Dodge, 3), elena.ReactionSkills.Dodge);
        Assert.Equal(ReactionProgression.RatesForRank(ReactionKind.Parry, 4), elena.ReactionSkills.Parry);
        Assert.Equal(ReactionProgression.RatesForRank(ReactionKind.Block, 1), elena.ReactionSkills.Block);
    }

    [Fact(DisplayName = "progress under an encounter-style id such as player_elena is not read")]
    public void Old_ids_ignored()
    {
        var content = SetupFixtures.Content(null, 1);
        var game = SetupFixtures.Game(content, TestCast.ElenaId);
        game = game with { ReactionProgress = game.ReactionProgress.SetItem("player_elena", new ReactionProgress(500, 0, 0)) };

        var elena = SetupFixtures.StartCombat(content, game).PlayerParty[0];

        Assert.Equal(ReactionProgression.Zero, elena.ReactionProgress);
    }

    [Fact(DisplayName = "starting a fight records every player-side fighter's progress by id and never an enemy's")]
    public void Start_records_by_id()
    {
        var content = SetupFixtures.Content(SetupFixtures.Hero, 1);

        var started = SetupFixtures.Start(content, SetupFixtures.Game(content, TestCast.ElenaId));

        Assert.Equal(
            [TestCast.ElenaId, TestContent.PlayerId],
            started.ReactionProgress.Keys.Order(StringComparer.Ordinal));
        Assert.False(started.ReactionProgress.ContainsKey("enemy_1"));
    }

    [Fact(DisplayName = "Sync copies earned progress to the character id and the player id")]
    public void Sync_by_id()
    {
        var content = SetupFixtures.Content(SetupFixtures.Hero, 1);
        var started = SetupFixtures.Start(content, SetupFixtures.Game(content, TestCast.ElenaId));
        var earned = started.Combat! with
        {
            PlayerParty = [.. started.Combat.PlayerParty.Select(c => c with { ReactionProgress = new ReactionProgress(c.Id == TestCast.ElenaId ? 12 : 4, 0, 2) })],
        };

        var synced = FightSetup.Sync(started, earned, SetupFixtures.Later);

        Assert.Equal(new ReactionProgress(12, 0, 2), synced.ReactionProgress[TestCast.ElenaId]);
        Assert.Equal(new ReactionProgress(4, 0, 2), synced.ReactionProgress[TestContent.PlayerId]);
    }
}

public class FightSetupRefusals
{
    private static readonly GameContent Content = SetupFixtures.Content(SetupFixtures.Hero, 2, encounters: TestEncounters.All);

    private static GameState Ready() => SetupFixtures.Game(Content, TestCast.ElenaId, TestCast.LarsId);

    [Fact(DisplayName = "an unknown encounter is refused with ENCOUNTER_NOT_FOUND")]
    public void Unknown_encounter()
    {
        var error = SetupFixtures.Refused(FightSetup.Start(Content, Ready(), "encounter_missing", SetupFixtures.Seed, SetupFixtures.Later), ErrorCode.ENCOUNTER_NOT_FOUND);

        Assert.Equal("Encounter 'encounter_missing' not found in registry", error.Message);
    }

    [Fact(DisplayName = "starting a fight during a fight is refused with COMBAT_ALREADY_ACTIVE")]
    public void Already_active()
    {
        var active = SetupFixtures.Start(Content, Ready(), TestEncounters.TestId);

        var error = SetupFixtures.Refused(FightSetup.Start(Content, active, TestEncounters.DemoId, SetupFixtures.Seed, SetupFixtures.Later), ErrorCode.COMBAT_ALREADY_ACTIVE);

        Assert.Equal("A combat encounter is already active", error.Message);
    }

    [Fact(DisplayName = "an unknown encounter is reported before an active fight")]
    public void Unknown_before_active()
    {
        var active = SetupFixtures.Start(Content, Ready(), TestEncounters.TestId);

        SetupFixtures.Refused(FightSetup.Start(Content, active, "encounter_missing", SetupFixtures.Seed, SetupFixtures.Later), ErrorCode.ENCOUNTER_NOT_FOUND);
    }

    [Fact(DisplayName = "starting without a full lineup is refused with TEAM_COMPOSITION_INVALID")]
    public void No_full_lineup()
    {
        const string message = "Choose a full lineup before starting a fight";
        var empty = TestContent.NewGame(Content);
        var one = SetupFixtures.Game(Content, TestCast.ElenaId);
        var three = SetupFixtures.Game(Content, TestCast.ElenaId, TestCast.LarsId, TestCast.KadeId);

        foreach (var game in new[] { empty, one, three })
        {
            var error = SetupFixtures.Refused(FightSetup.Start(Content, game, TestEncounters.TestId, SetupFixtures.Seed, SetupFixtures.Later), ErrorCode.TEAM_COMPOSITION_INVALID);
            Assert.Equal(message, error.Message);
        }
    }

    [Fact(DisplayName = "a default (never set) lineup is refused with TEAM_COMPOSITION_INVALID")]
    public void Default_lineup()
    {
        var game = TestContent.NewGame(Content) with { Lineup = default };

        SetupFixtures.Refused(FightSetup.Start(Content, game, TestEncounters.TestId, SetupFixtures.Seed, SetupFixtures.Later), ErrorCode.TEAM_COMPOSITION_INVALID);
    }

    [Fact(DisplayName = "a lineup of zero with no player combat stats is refused with TEAM_COMPOSITION_INVALID")]
    public void Nobody_fights()
    {
        var content = SetupFixtures.Content(null, 0);
        var game = SetupFixtures.Game(content);

        var error = SetupFixtures.Refused(FightSetup.Start(content, game, SetupFixtures.FoeId, SetupFixtures.Seed, SetupFixtures.Later), ErrorCode.TEAM_COMPOSITION_INVALID);

        Assert.Equal("Choose a full lineup before starting a fight", error.Message);
    }

    [Fact(DisplayName = "a lineup member who cannot fight is refused with TEAM_COMPOSITION_INVALID")]
    public void Member_without_combat()
    {
        var content = SetupFixtures.Content(SetupFixtures.Hero, 1, [TestCast.Elena with { Combat = null }]);
        var game = SetupFixtures.Game(content, TestCast.ElenaId);

        SetupFixtures.Refused(FightSetup.Start(content, game, SetupFixtures.FoeId, SetupFixtures.Seed, SetupFixtures.Later), ErrorCode.TEAM_COMPOSITION_INVALID);
    }

    [Fact(DisplayName = "a lineup member missing from the content is refused with TEAM_COMPOSITION_INVALID")]
    public void Unknown_member()
    {
        var content = SetupFixtures.Content(SetupFixtures.Hero, 1);
        var game = SetupFixtures.Game(content, "npc_ghost");

        SetupFixtures.Refused(FightSetup.Start(content, game, SetupFixtures.FoeId, SetupFixtures.Seed, SetupFixtures.Later), ErrorCode.TEAM_COMPOSITION_INVALID);
    }

    [Fact(DisplayName = "the same character twice in the lineup is refused with a duplicate fighter id")]
    public void Duplicate_lineup_member()
    {
        var game = SetupFixtures.Game(Content, TestCast.ElenaId, TestCast.ElenaId);

        var error = SetupFixtures.Refused(FightSetup.Start(Content, game, TestEncounters.TestId, SetupFixtures.Seed, SetupFixtures.Later), ErrorCode.TEAM_COMPOSITION_INVALID);

        Assert.Equal("Duplicate fighter id: " + TestCast.ElenaId, error.Message);
    }

    [Fact(DisplayName = "an enemy that reuses the player's id is refused with a duplicate fighter id")]
    public void Enemy_reuses_player_id()
    {
        var game = Ready() with { Player = Ready().Player with { Id = "enemy_knight" } };

        var error = SetupFixtures.Refused(FightSetup.Start(Content, game, TestEncounters.DemoId, SetupFixtures.Seed, SetupFixtures.Later), ErrorCode.TEAM_COMPOSITION_INVALID);

        Assert.Equal("Duplicate fighter id: enemy_knight", error.Message);
    }

    [Fact(DisplayName = "a synergy that leaves a fighter with an invalid stat is refused with VALIDATION_ERROR")]
    public void Invalid_stat_after_synergy()
    {
        var synergy = new SynergySettings(
            [new SynergyBonusDefinition("Curse", SynergyRule.Coverage, 1, CombatStat.Power, StatStep.Flat, -1000)],
            SynergyResolution.BestOnly);
        var content = SetupFixtures.Content(null, 1, synergy: synergy);
        var game = SetupFixtures.Game(content, TestCast.ElenaId);

        var error = SetupFixtures.Refused(FightSetup.Start(content, game, SetupFixtures.FoeId, SetupFixtures.Seed, SetupFixtures.Later), ErrorCode.VALIDATION_ERROR);

        Assert.Equal("Fight setup gave " + TestCast.ElenaId + " an invalid power", error.Message);
    }

    [Fact(DisplayName = "a refused start changes nothing")]
    public void Refusal_changes_nothing()
    {
        var game = Ready();

        FightSetup.Start(Content, game, "encounter_missing", SetupFixtures.Seed, SetupFixtures.Later);

        Assert.Null(game.Combat);
        Assert.Empty(game.ReactionProgress);
    }

    [Fact(DisplayName = "the rules library has no encounter built in")]
    public void No_built_in_encounters()
    {
        var content = TestContent.Create(encounters: ImmutableArray<EncounterDefinition>.Empty);
        var game = TestContent.NewGame(content) with { Lineup = [TestCast.ElenaId, TestCast.LarsId] };

        Assert.Empty(content.Encounters.Definitions);
        SetupFixtures.Refused(FightSetup.Start(content, game, TestEncounters.DemoId, SetupFixtures.Seed, SetupFixtures.Later), ErrorCode.ENCOUNTER_NOT_FOUND);
        Assert.Empty(GameContent.Create(new GameSettings("Kael", new LineupLimits(2, 2)) { StartingParty = [.. TestCast.All.Select(c => c.Id)] }, TestCast.All, TestArchetypes.All).Value.Encounters.Definitions);
    }
}

// New (T8): demo-fight parity. Expected values are what the TypeScript initCombatState gave for the same game
// (createNewGameState, the default personality, today's synergy settings), apart from fighter ids and names that are now character ids.
public class FightSetupDemoFightParity
{
    private static readonly ReactionProgress Zero = ReactionProgression.Zero;

    [Fact(DisplayName = "encounter_demo starts with today's combatants and stats, including the synergy speed bonus")]
    public void Demo_encounter()
    {
        var combat = SetupFixtures.StartCombat(TestEncounters.DemoPartyContent, TestEncounters.DemoPartyGame(), TestEncounters.DemoId);

        var expected = new[]
        {
            SetupFixtures.Fighter(TestCast.ElenaId, "Elena", TestArchetypes.Elena, 3, 120, 85, 99, ElementalPath.Light, SetupFixtures.Rank1, Zero),
            SetupFixtures.Fighter(TestCast.LarsId, "Lars", TestArchetypes.Lars, 3, 140, 90, 77, ElementalPath.Earth, SetupFixtures.Rank1, Zero),
            SetupFixtures.Fighter(TestCast.KadeId, "Kade", TestArchetypes.Kade, 3, 110, 100, 110, ElementalPath.Fire, SetupFixtures.Rank1, Zero),
        };
        StateAssert.Equivalent(expected.ToImmutableArray(), combat.PlayerParty);

        var enemies = new[]
        {
            SetupFixtures.Fighter("enemy_knight", "Shadow Knight", "enemy", 3, 130, 88, 75, ElementalPath.Earth, TestEncounters.Skills(0.7, 0.55, 0.25, 0.2, 0.8, 0.1, 0.2, 0.9, 0.1), null),
            SetupFixtures.Fighter("enemy_rogue", "Shadow Rogue", "enemy", 3, 100, 95, 95, ElementalPath.Fire, TestEncounters.Skills(0.5, 0.45, 0.2, 0.45, 0.88, 0.2, 0.4, 0.98, 0.2), null),
            SetupFixtures.Fighter("enemy_mage", "Shadow Mage", "enemy", 3, 90, 105, 80, ElementalPath.Shadow, TestEncounters.Skills(0.3, 0.35, 0.1, 0.5, 0.9, 0.25, 0.3, 0.94, 0.15), null),
        };
        StateAssert.Equivalent(enemies.ToImmutableArray(), combat.EnemyParty);
        Assert.Equal(1, combat.Round);
        Assert.Equal(CombatStatus.Active, combat.Status);
        Assert.Empty(combat.ActionQueue);
        Assert.Empty(combat.RoundHistory);
    }

    [Fact(DisplayName = "encounter_test starts with today's combatants and stats, including the synergy speed bonus")]
    public void Test_encounter()
    {
        var combat = SetupFixtures.StartCombat(TestEncounters.TestPartyContent, TestEncounters.TestPartyGame(), TestEncounters.TestId);

        var expected = new[]
        {
            SetupFixtures.Fighter(TestEncounters.WarriorId, "Warrior", "warrior", 2.0, 120, 18, 15, ElementalPath.Fire, SetupFixtures.Rank1, Zero),
            SetupFixtures.Fighter(TestEncounters.ScoutId, "Scout", "scout", 1.5, 90, 14, 22, ElementalPath.Air, SetupFixtures.Rank1, Zero),
            SetupFixtures.Fighter(TestEncounters.MageId, "Mage", "mage", 1.5, 80, 22, 13, ElementalPath.Shadow, SetupFixtures.Rank1, Zero),
        };
        StateAssert.Equivalent(expected.ToImmutableArray(), combat.PlayerParty);

        var enemies = new[]
        {
            SetupFixtures.Fighter("enemy_brute", "Iron Brute", TestArchetypes.Elena, 2.0, 110, 20, 10, ElementalPath.Earth, TestEncounters.Skills(0.65, 0.55, 0.25, 0.2, 0.8, 0.3, 0.2, 0.93, 0.2), null),
            SetupFixtures.Fighter("enemy_archer", "Shadow Archer", "kade_bold_outlaw", 1.5, 85, 16, 18, ElementalPath.Shadow, TestEncounters.Skills(0.35, 0.35, 0.15, 0.6, 0.89, 0.2, 0.25, 0.97, 0.2), null),
            SetupFixtures.Fighter("enemy_shaman", "Bog Shaman", "lars_cautious_merchant", 1.5, 75, 19, 11, ElementalPath.Water, TestEncounters.Skills(0.45, 0.4, 0.15, 0.35, 0.83, 0.2, 0.3, 1.0, 0.2), null),
        };
        StateAssert.Equivalent(enemies.ToImmutableArray(), combat.EnemyParty);
    }

    [Fact(DisplayName = "the demo fight's lineup order sets the player side's order")]
    public void Order()
    {
        var content = TestEncounters.DemoPartyContent;
        var game = SetupFixtures.Game(content, TestCast.KadeId, TestCast.ElenaId, TestCast.LarsId);

        var combat = SetupFixtures.StartCombat(content, game, TestEncounters.DemoId);

        Assert.Equal([TestCast.KadeId, TestCast.ElenaId, TestCast.LarsId], combat.PlayerParty.Select(c => c.Id));
    }
}

// New (T8): EncounterCatalog validation, through GameContent.Create.
public class EncounterCatalogValidation
{
    private static CombatantConfig Enemy(
        string id = "e1", string name = "Foe", string archetype = "enemy", double rank = 2, double stamina = 100, double power = 10, double speed = 10,
        ElementalPath path = ElementalPath.Fire, ReactionSkills? skills = null, AscensionLevel? level = null) =>
        new(id, name, archetype, rank, stamina, power, speed, path, skills ?? SetupFixtures.FoeSkills, level);

    private static Result<GameContent> Create(params EncounterDefinition[] encounters) =>
        GameContent.Create(new GameSettings("Kael", new LineupLimits(2, 2)) { StartingParty = [.. TestCast.All.Select(c => c.Id)] }, TestCast.All, TestArchetypes.All, null, [.. encounters]);

    private static EncounterDefinition One(CombatantConfig enemy, string id = "enc", string name = "Encounter") => new(id, name, [enemy]);

    private static void Refused(Result<GameContent> result, string messagePart)
    {
        Assert.False(result.IsOk);
        Assert.Equal(ErrorCode.VALIDATION_ERROR, result.Error.Code);
        Assert.Contains(result.Error.Details, d => d.Message.Contains(messagePart, StringComparison.Ordinal));
    }

    [Fact(DisplayName = "a valid encounter is accepted")]
    public void Valid()
    {
        var result = Create(One(Enemy()));

        Assert.True(result.IsOk);
        Assert.True(result.Value.Encounters.TryGet("enc", out var encounter));
        Assert.Equal("Encounter", encounter.Name);
    }

    [Fact(DisplayName = "content without encounters has an empty catalog")]
    public void None()
    {
        var result = GameContent.Create(new GameSettings("Kael", new LineupLimits(2, 2)) { StartingParty = [.. TestCast.All.Select(c => c.Id)] }, TestCast.All, TestArchetypes.All);

        Assert.True(result.IsOk);
        Assert.Empty(result.Value.Encounters.Definitions);
        Assert.False(result.Value.Encounters.TryGet("anything", out _));
        Assert.False(result.Value.Encounters.TryGet(null, out _));
    }

    [Fact(DisplayName = "an encounter with zero enemies is refused")]
    public void Zero_enemies()
    {
        Refused(Create(new EncounterDefinition("enc", "Encounter", [])), "at least one enemy");
    }

    [Fact(DisplayName = "an encounter or enemy without a name is refused")]
    public void Names()
    {
        Refused(Create(One(Enemy(), name: "")), "name must not be empty");
        Refused(Create(One(Enemy(name: ""))), "name must not be empty");
    }

    [Fact(DisplayName = "an empty or repeated encounter id is refused")]
    public void Encounter_ids()
    {
        Refused(Create(One(Enemy(), id: "")), "id must not be empty");
        Refused(Create(One(Enemy(), id: "enc"), One(Enemy(), id: "enc")), "duplicate encounter id");
    }

    [Fact(DisplayName = "ids longer than 64 characters are refused")]
    public void Long_ids()
    {
        var longId = new string('x', 65);

        Refused(Create(One(Enemy(), id: longId)), "id must be 1 to 64 characters");
        Refused(Create(One(Enemy(id: longId))), "id must be 1 to 64 characters");
        Assert.True(Create(One(Enemy(id: new string('x', 64)), id: new string('y', 64))).IsOk);
    }

    [Fact(DisplayName = "an enemy archetype that is not in content is refused")]
    public void Unknown_archetype()
    {
        Refused(Create(One(Enemy(archetype: "no_such_archetype"))), "unknown archetype");
    }

    [Fact(DisplayName = "a repeated fighter id within an encounter is refused")]
    public void Repeated_fighter_id()
    {
        Refused(Create(new EncounterDefinition("enc", "Encounter", [Enemy("e1"), Enemy("e1")])), "duplicate fighter id");
        Assert.True(Create(new EncounterDefinition("enc", "Encounter", [Enemy("e1"), Enemy("e2")]),
            new EncounterDefinition("enc2", "Encounter 2", [Enemy("e1")])).IsOk);
    }

    [Fact(DisplayName = "an enemy that reuses a character's id is refused")]
    public void Reuses_character_id()
    {
        Refused(Create(One(Enemy(TestCast.ElenaId))), "reuses a character id");
    }

    [Fact(DisplayName = "enemy stats must be finite and in range")]
    public void Stats()
    {
        Refused(Create(One(Enemy(rank: 0))), "rank must be");
        Refused(Create(One(Enemy(rank: double.NaN))), "rank must be");
        Refused(Create(One(Enemy(stamina: 0))), "stamina must be");
        Refused(Create(One(Enemy(stamina: double.PositiveInfinity))), "stamina must be");
        Refused(Create(One(Enemy(power: -1))), "power must be");
        Refused(Create(One(Enemy(speed: -1))), "speed must be");
        Refused(Create(One(Enemy(path: (ElementalPath)99))), "elementalPath");
        Refused(Create(One(Enemy(level: (AscensionLevel)9))), "ascensionLevel");
        Assert.True(Create(One(Enemy(power: 0, speed: 0))).IsOk);
    }

    [Fact(DisplayName = "reaction skill rates must be from 0 to 1")]
    public void Rates()
    {
        var tooHigh = TestEncounters.Skills(1.1, 0.3, 0.1, 0.5, 0.8, 0.1, 0.4, 0.9, 0.1);
        var negative = TestEncounters.Skills(0.6, 0.3, 0.1, 0.5, -0.1, 0.1, 0.4, 0.9, 0.1);
        var nan = TestEncounters.Skills(0.6, 0.3, 0.1, 0.5, 0.8, 0.1, 0.4, 0.9, double.NaN);
        var edges = TestEncounters.Skills(0, 1, 0, 0, 1, 0, 0, 1, 0);

        Refused(Create(One(Enemy(skills: tooHigh))), "rate must be");
        Refused(Create(One(Enemy(skills: negative))), "rate must be");
        Refused(Create(One(Enemy(skills: nan))), "rate must be");
        Assert.True(Create(One(Enemy(skills: edges))).IsOk);
    }

    [Fact(DisplayName = "encounters keep content order")]
    public void Content_order()
    {
        var result = Create(One(Enemy(), id: "b"), One(Enemy(), id: "a"));

        Assert.Equal(["b", "a"], result.Value.Encounters.Definitions.Select(d => d.Id));
    }
}
