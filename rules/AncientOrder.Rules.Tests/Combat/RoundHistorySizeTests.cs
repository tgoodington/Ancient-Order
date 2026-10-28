using System.Collections.Immutable;
using AncientOrder.Rules.Combat;
using AncientOrder.Rules.Saves;
using AncientOrder.Rules.State;
using AncientOrder.Rules.Tests.Fixtures;
using AncientOrder.Rules.Tests.Support;

namespace AncientOrder.Rules.Tests.Combat;

// New (T7, T11): a round's record holds the end-of-round fighters without earlier history, so a fight's saved size grows linearly with its
// rounds (today each record embeds every earlier record, so the size roughly doubles each round and a long fight cannot be saved).
public class RoundHistorySize
{
    private const int Rounds = 30;

    private static readonly GameContent Content = BuildContent();

    private static GameContent BuildContent()
    {
        var tough = new CombatProfile(2.0, 1_000_000, 10, 10, ElementalPath.Fire);
        var foe = new EncounterDefinition(
            SetupFixtures.FoeId,
            "Long fight",
            [
                new CombatantConfig("enemy_1", "Foe", "enemy", 2.0, 1_000_000, 10, 10, ElementalPath.Fire, SetupFixtures.FoeSkills),
                new CombatantConfig("enemy_2", "Foe Two", "enemy", 2.0, 1_000_000, 10, 10, ElementalPath.Water, SetupFixtures.FoeSkills),
            ]);
        return SetupFixtures.Content(tough, 2, TestCast.All.Select(c => c with { Combat = tough }).ToImmutableArray(), TestContent.Synergy, [foe]);
    }

    private static CombatState FightRounds(int rounds, Action<CombatState>? afterEach = null)
    {
        var game = SetupFixtures.Start(Content, SetupFixtures.Game(Content, TestCast.ElenaId, TestCast.LarsId));
        var combat = game.Combat!;
        for (var i = 0; i < rounds; i++)
        {
            var orders = combat.PlayerParty
                .Where(c => !c.IsKO)
                .Select((c, index) => new CombatAction(c.Id, ActionType.Attack, index % 2 == 0 ? "enemy_1" : "enemy_2", null))
                .ToList();
            combat = RoundManager.RunRound(combat, orders, Content.Archetypes);
            afterEach?.Invoke(combat);
        }

        return combat;
    }

    private static async Task<int> SavedSize(CombatState combat)
    {
        var backend = new MemorySaveBackend();
        var store = new SaveStore(backend, Content);
        var game = FightSetup.Sync(TestContent.NewGame(Content), combat, TestContent.Now);
        var saved = await store.SaveAsync(1, game, TestContent.Now, TestContext.Current.CancellationToken);
        Assert.True(saved.IsOk, saved.IsOk ? string.Empty : saved.Error.Message);
        var bytes = await backend.ReadAsync(SaveStore.KeyFor(1), TestContext.Current.CancellationToken);
        return bytes!.Length;
    }

    [Fact(DisplayName = "every round's snapshot has an empty round history")]
    public void Snapshots_have_empty_history()
    {
        var combat = FightRounds(Rounds);

        Assert.Equal(Rounds, combat.RoundHistory.Length);
        Assert.Equal(Rounds + 1, combat.Round);
        Assert.Equal(CombatStatus.Active, combat.Status);
        Assert.All(combat.RoundHistory, r => Assert.Empty(r.StateSnapshot.RoundHistory));
    }

    [Fact(DisplayName = "a 30-round fight's saved size grows linearly with its rounds")]
    public async Task Saved_size_grows_linearly()
    {
        var at10 = await SavedSize(FightRounds(10));
        var at20 = await SavedSize(FightRounds(20));
        var at30 = await SavedSize(FightRounds(30));

        var firstTen = at20 - at10;
        var secondTen = at30 - at20;
        Assert.True(secondTen < firstTen * 1.5, $"history grew {firstTen} bytes over rounds 11-20 and {secondTen} over rounds 21-30");
        Assert.True(secondTen > firstTen * 0.5, $"history grew {firstTen} bytes over rounds 11-20 and {secondTen} over rounds 21-30");
        Assert.True(at30 < at10 * 4, $"30 rounds saved as {at30} bytes against {at10} for 10 rounds");
    }

    [Fact(DisplayName = "a 30-round fight stays well under the save size limit")]
    public async Task Well_under_limit()
    {
        var size = await SavedSize(FightRounds(Rounds));

        Assert.True(size < SaveConstants.MaxSaveBytes / 4, $"30 rounds saved as {size} bytes");
    }

    [Fact(DisplayName = "a 30-round fight can be saved and loaded back with its history")]
    public async Task Round_trips()
    {
        var combat = FightRounds(Rounds);
        var store = new SaveStore(new MemorySaveBackend(), Content);
        var game = FightSetup.Sync(TestContent.NewGame(Content), combat, TestContent.Now);

        Assert.True((await store.SaveAsync(1, game, TestContent.Now, TestContext.Current.CancellationToken)).IsOk);
        var loaded = (await store.LoadAsync(1, TestContext.Current.CancellationToken)).Value.Game.Combat!;

        Assert.Equal(Rounds, loaded.RoundHistory.Length);
        Assert.Equal(combat.Round, loaded.Round);
        Assert.Equal(combat.Dice, loaded.Dice);
    }
}
