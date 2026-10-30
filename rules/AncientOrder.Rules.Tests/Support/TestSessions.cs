using System.Collections.Immutable;
using System.Globalization;
using AncientOrder.Rules.Combat;
using AncientOrder.Rules.Common;
using AncientOrder.Rules.Party;
using AncientOrder.Rules.Session;
using AncientOrder.Rules.State;
using AncientOrder.Rules.Tests.Fixtures;
using Microsoft.Extensions.Time.Testing;

namespace AncientOrder.Rules.Tests.Support;

public sealed class FixedIdSource(string id) : IIdSource
{
    public string NextId() => id;
}

/// <summary>Ids "1", "2", "3", ... so each new game gets its own player id.</summary>
public sealed class CountingIdSource : IIdSource
{
    private int _next;

    public string NextId() => (++_next).ToString(CultureInfo.InvariantCulture);
}

public sealed class FixedSeedSource(ulong seed) : ISeedSource
{
    public ulong NextSeed() => seed;
}

public sealed class ThrowingSeedSource : ISeedSource
{
    public ulong NextSeed() => throw new InvalidOperationException("seed source failed");
}

/// <summary>
/// The two enemy-only encounters the TypeScript session tests used (encounter_test and encounter_demo).
/// Built here so session tests do not depend on other test files' content builders.
/// </summary>
public static class SessionEncounters
{
    public const string Test = "encounter_test";
    public const string Demo = "encounter_demo";

    public static ImmutableArray<string> TestEnemies { get; } = ["enemy_brute", "enemy_archer", "enemy_shaman"];

    public static ImmutableArray<string> DemoEnemies { get; } = ["enemy_knight", "enemy_rogue", "enemy_mage"];

    public static EncounterDefinition TestEncounter { get; } = new(
        Test,
        "Test Encounter (3v3)",
        [
            Enemy("enemy_brute", "Iron Brute", TestArchetypes.Elena, 2.0, 110, 20, 10, ElementalPath.Earth, Skills(0.65, 0.55, 0.25, 0.2, 0.8, 0.3, 0.2, 0.93, 0.2)),
            Enemy("enemy_archer", "Shadow Archer", "kade_bold_outlaw", 1.5, 85, 16, 18, ElementalPath.Shadow, Skills(0.35, 0.35, 0.15, 0.6, 0.89, 0.2, 0.25, 0.97, 0.2)),
            Enemy("enemy_shaman", "Bog Shaman", "lars_cautious_merchant", 1.5, 75, 19, 11, ElementalPath.Water, Skills(0.45, 0.4, 0.15, 0.35, 0.83, 0.2, 0.3, 1.0, 0.2)),
        ]);

    public static EncounterDefinition DemoEncounter { get; } = new(
        Demo,
        "Demo Encounter: Order vs Shadow Guard (3v3)",
        [
            Enemy("enemy_knight", "Shadow Knight", "enemy", 3, 130, 88, 75, ElementalPath.Earth, Skills(0.7, 0.55, 0.25, 0.2, 0.8, 0.1, 0.2, 0.9, 0.1)),
            Enemy("enemy_rogue", "Shadow Rogue", "enemy", 3, 100, 95, 95, ElementalPath.Fire, Skills(0.5, 0.45, 0.2, 0.45, 0.88, 0.2, 0.4, 0.98, 0.2)),
            Enemy("enemy_mage", "Shadow Mage", "enemy", 3, 90, 105, 80, ElementalPath.Shadow, Skills(0.3, 0.35, 0.1, 0.5, 0.9, 0.25, 0.3, 0.94, 0.15)),
        ]);

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

/// <summary>
/// Builds sessions for the session tests: a fake clock, a fixed id source (player id "player_fixed"), a fixed seed source,
/// and in-memory saves. Kael fights too, so the player plus a lineup of Elena and Lars is a 3 versus 3.
/// </summary>
public static class TestSessions
{
    public const ulong Seed = 20_261_028;

    public const string Hero = "player_fixed";

    public static CombatProfile HeroProfile { get; } = new(3, 120, 85, 90, ElementalPath.Light);

    public static ImmutableArray<string?> Pair { get; } = [TestCast.ElenaId, TestCast.LarsId];

    public static GameContent Content(
        CombatProfile? hero = null,
        ImmutableArray<CharacterDefinition>? characters = null,
        ImmutableArray<EncounterDefinition>? encounters = null)
    {
        var result = GameContent.Create(
            new GameSettings("Kael", new LineupLimits(2, 2), null, hero ?? HeroProfile) { StartingParty = [.. (characters ?? TestCast.All).Select(c => c.Id)] },
            characters ?? TestCast.All,
            TestArchetypes.All,
            TestContent.Synergy,
            encounters ?? [SessionEncounters.TestEncounter, SessionEncounters.DemoEncounter]);
        return result.IsOk ? result.Value : throw new InvalidOperationException("Session test content is invalid: " + result.Error.Message);
    }

    public static GameSession Create(
        GameContent? content = null,
        Action<Exception>? onListenerError = null,
        IIdSource? ids = null,
        ISeedSource? seeds = null,
        FakeTimeProvider? clock = null)
    {
        return new GameSession(
            content ?? Content(),
            new GameSessionOptions
            {
                TimeProvider = clock ?? new FakeTimeProvider(DateTimeOffset.FromUnixTimeMilliseconds(TestContent.Now)),
                IdSource = ids ?? new FixedIdSource("fixed"),
                SeedSource = seeds ?? new FixedSeedSource(Seed),
                OnListenerError = onListenerError,
            });
    }

    public static GameSession WithGame(GameContent? content = null)
    {
        var session = Create(content);
        Assert.True(session.NewGame().IsOk);
        return session;
    }

    /// <summary>A new game with Elena and Lars in the lineup.</summary>
    public static GameSession WithLineup(GameContent? content = null, ISeedSource? seeds = null)
    {
        var session = Create(content, seeds: seeds);
        Assert.True(session.NewGame().IsOk);
        Assert.True(session.SetLineup(Pair).IsOk);
        return session;
    }

    /// <summary>A new game, the lineup set, and the named encounter started.</summary>
    public static GameSession InCombat(string encounterId = SessionEncounters.Test, GameContent? content = null, ISeedSource? seeds = null)
    {
        var session = WithLineup(content, seeds);
        var started = session.StartEncounter(encounterId);
        Assert.True(started.IsOk, started.IsOk ? string.Empty : started.Error.Message);
        return session;
    }

    public static CombatAction Attack(string who, string target) => new(who, ActionType.Attack, target, null);
}
