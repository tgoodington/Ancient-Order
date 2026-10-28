using System.Diagnostics;
using AncientOrder.Rules.Combat;
using AncientOrder.Rules.Combat.Ai;
using AncientOrder.Rules.Tests.Fixtures;
using AncientOrder.Rules.Tests.Support;

namespace AncientOrder.Rules.Tests.Combat;

// Carried over from src/combat/integration.test.ts (16 tests, five full-fight scenarios). The registered archetypes are the test archetypes
// (Elena, Lars, Kade scored; 'test' basic). runRound(state, orders, ROLL_x) is RoundManager.RunRound(..., new ConstantRolls(x)).
// The expected results are the TypeScript ones; the roll sequences are constant, so the speed-tie rule changes nothing here.
// Changed: the timing assertion of the chain-cap test uses a Stopwatch.
internal static class IntegrationFixtures
{
    public static readonly ReactionSkills Standard = new(
        new DefenseSkill(0.6, 0.5, 0.2),
        new DefenseSkill(0.5, 0.8, 0.15),
        new DefenseSkill(0.4, 0.9, 0.1));

    public static readonly ReactionSkills HighParry = new(
        new DefenseSkill(0.6, 0.5, 0.2),
        new DefenseSkill(0.5, 0.8, 0.15),
        new DefenseSkill(1.0, 0.9, 0.1));

    public static ArchetypeCatalog Archetypes { get; } = ArchetypeCatalog.Create(
        [TestArchetypes.Basic("test"), TestArchetypes.ElenaLoyalScout, TestArchetypes.LarsSchemingMerchant, TestArchetypes.KadeRogueOutlaw]).Value;

    public static Combatant Build(string id = "combatant", string name = "Combatant") => new(
        id,
        name,
        "test",
        1.0,
        100,
        100,
        50,
        10,
        0,
        5,
        AscensionLevel.Level0,
        [],
        ElementalPath.Fire,
        Standard,
        null,
        false);

    public static CombatState State(Combatant[] players, Combatant[] enemies) =>
        new(
            1,
            CombatPhase.AiDecision,
            [.. players],
            [.. enemies],
            [],
            [],
            CombatStatus.Active,
            AncientOrder.Rules.Common.DiceState.FromSeed(7));

    public static CombatState Run(CombatState state, IReadOnlyList<CombatAction> orders, double roll) =>
        RoundManager.RunRound(state, orders, Archetypes, new ConstantRolls(roll));

    public static CombatAction Act(string who, ActionType type, string? target, double? segments = null) =>
        new(who, type, target, segments);

    public static Combatant Find(CombatState state, string id) =>
        state.PlayerParty.Concat(state.EnemyParty).Single(c => c.Id == id);

    public const double RollLow = 1;
    public const double RollHigh = 20;
    public const double RollMid = 10;
}

public class IntegrationScenario13v3RoundWithRealArchetypeAI
{
    private static Combatant Elena() => IntegrationFixtures.Build("elena", "Elena") with { ArchetypeId = TestArchetypes.Elena, ElementalPath = ElementalPath.Light, Power = 40, Speed = 12 };

    private static Combatant Lars() => IntegrationFixtures.Build("lars", "Lars") with { ArchetypeId = TestArchetypes.Lars, ElementalPath = ElementalPath.Earth, Stamina = 120, MaxStamina = 120, Power = 50, Speed = 8 };

    private static Combatant Kade() => IntegrationFixtures.Build("kade", "Kade") with { ArchetypeId = TestArchetypes.Kade, ElementalPath = ElementalPath.Fire, Stamina = 80, MaxStamina = 80, Power = 60, Speed = 15 };

    private static Combatant Player(string id) => IntegrationFixtures.Build(id, id) with { Stamina = 150, MaxStamina = 150 };

    private static CombatAction[] AttackEachEnemy() =>
    [
        IntegrationFixtures.Act("p1", ActionType.Attack, "elena"),
        IntegrationFixtures.Act("p2", ActionType.Attack, "lars"),
        IntegrationFixtures.Act("p3", ActionType.Attack, "kade"),
    ];

    [Fact(DisplayName = "3v3 round with Elena/Lars/Kade enemies resolves without errors")]
    public void Resolves_without_errors()
    {
        var state = IntegrationFixtures.State([Player("p1"), Player("p2"), Player("p3")], [Elena(), Lars(), Kade()]);

        Assert.Null(Record.Exception(() => IntegrationFixtures.Run(state, AttackEachEnemy(), IntegrationFixtures.RollMid)));
    }

    [Fact(DisplayName = "Elena and Kade produce different action types given the same combat state")]
    public void Elena_and_kade()
    {
        var injuredAlly = IntegrationFixtures.Build("ally_e") with { Stamina = 15 };
        var weakPlayer = IntegrationFixtures.Build("p1") with { Stamina = 20 };
        var elenaState = IntegrationFixtures.State([weakPlayer], [Elena(), injuredAlly]);
        var kadeState = IntegrationFixtures.State([weakPlayer], [Kade(), injuredAlly]);

        var elenaResult = IntegrationFixtures.Run(elenaState, [], IntegrationFixtures.RollMid);
        var kadeResult = IntegrationFixtures.Run(kadeState, [], IntegrationFixtures.RollMid);

        Assert.Equal(2, elenaResult.Round);
        Assert.Equal(2, kadeResult.Round);
        Assert.NotEmpty(elenaResult.RoundHistory);
        Assert.NotEmpty(kadeResult.RoundHistory);
    }

    [Fact(DisplayName = "round history reflects actions taken by all 6 combatants in a 3v3")]
    public void History_has_all_actions()
    {
        var state = IntegrationFixtures.State([Player("p1"), Player("p2"), Player("p3")], [Elena(), Lars(), Kade()]);

        var result = IntegrationFixtures.Run(state, AttackEachEnemy(), IntegrationFixtures.RollMid);

        Assert.Single(result.RoundHistory);
        Assert.True(result.RoundHistory[0].Actions.Length >= 3);
    }
}

public class IntegrationScenario2GROUPActionThroughPipeline
{
    [Fact(DisplayName = "GROUP with all-full-energy players resolves and drains energy significantly")]
    public void Drains_energy()
    {
        var p1 = IntegrationFixtures.Build("p1") with { Energy = 3, MaxEnergy = 3 };
        var p2 = IntegrationFixtures.Build("p2") with { Energy = 3, MaxEnergy = 3 };
        var p3 = IntegrationFixtures.Build("p3") with { Energy = 3, MaxEnergy = 3 };
        var enemy = IntegrationFixtures.Build("e1") with { Stamina = 500, MaxStamina = 500, Power = 1 };
        var state = IntegrationFixtures.State([p1, p2, p3], [enemy]);

        var result = IntegrationFixtures.Run(state, [IntegrationFixtures.Act("p1", ActionType.Group, "e1")], IntegrationFixtures.RollMid);

        Assert.True(IntegrationFixtures.Find(result, "p1").Energy < 3);
        Assert.True(IntegrationFixtures.Find(result, "p2").Energy < 3);
        Assert.True(IntegrationFixtures.Find(result, "p3").Energy < 3);
        Assert.Equal(0, IntegrationFixtures.Find(result, "p2").Energy);
        Assert.Equal(0, IntegrationFixtures.Find(result, "p3").Energy);
    }

    [Fact(DisplayName = "GROUP applies 1.5x multiplier — enemy takes more damage than a single ATTACK")]
    public void Multiplier()
    {
        var singleState = IntegrationFixtures.State(
            [IntegrationFixtures.Build("p1") with { MaxEnergy = 3 }],
            [IntegrationFixtures.Build("e1") with { Stamina = 500, MaxStamina = 500, Power = 30 }]);
        var groupState = IntegrationFixtures.State(
            [IntegrationFixtures.Build("p1") with { Energy = 3, MaxEnergy = 3 }, IntegrationFixtures.Build("p2") with { Energy = 3, MaxEnergy = 3 }],
            [IntegrationFixtures.Build("e1") with { Stamina = 500, MaxStamina = 500, Power = 30 }]);

        var singleResult = IntegrationFixtures.Run(singleState, [IntegrationFixtures.Act("p1", ActionType.Attack, "e1")], IntegrationFixtures.RollHigh);
        var groupResult = IntegrationFixtures.Run(groupState, [IntegrationFixtures.Act("p1", ActionType.Group, "e1")], IntegrationFixtures.RollHigh);

        var singleDamage = 500 - IntegrationFixtures.Find(singleResult, "e1").Stamina;
        var groupDamage = 500 - IntegrationFixtures.Find(groupResult, "e1").Stamina;
        Assert.True(groupDamage > singleDamage);
    }

    [Fact(DisplayName = "GROUP rejected when not all allies have full energy — falls back to ATTACK")]
    public void Falls_back()
    {
        var p1 = IntegrationFixtures.Build("p1") with { Energy = 3, MaxEnergy = 3 };
        var p2 = IntegrationFixtures.Build("p2") with { Energy = 1, MaxEnergy = 3 };
        var enemy = IntegrationFixtures.Build("e1") with { Stamina = 200, MaxStamina = 200, Power = 30 };
        var state = IntegrationFixtures.State([p1, p2], [enemy]);
        var orders = new[] { IntegrationFixtures.Act("p1", ActionType.Group, "e1") };

        Assert.Null(Record.Exception(() => IntegrationFixtures.Run(state, orders, IntegrationFixtures.RollMid)));

        var result = IntegrationFixtures.Run(state, orders, IntegrationFixtures.RollMid);
        Assert.Equal(2, result.Round);
        Assert.Equal(ActionType.Attack, result.RoundHistory[0].Actions.Single(a => a.CombatantId == "p1").Type);
    }

    [Fact(DisplayName = "GROUP uses Block-only defense on target — no parry counter chain")]
    public void Block_only()
    {
        var target = IntegrationFixtures.Build("e1") with { Stamina = 500, MaxStamina = 500, Power = 30, ReactionSkills = IntegrationFixtures.HighParry };
        var p1 = IntegrationFixtures.Build("p1") with { Energy = 3, MaxEnergy = 3 };
        var p2 = IntegrationFixtures.Build("p2") with { Energy = 3, MaxEnergy = 3 };
        var state = IntegrationFixtures.State([p1, p2], [target]);

        var result = IntegrationFixtures.Run(state, [IntegrationFixtures.Act("p1", ActionType.Group, "e1")], IntegrationFixtures.RollLow);

        Assert.NotEmpty(result.RoundHistory);
        var group = result.RoundHistory[0].Actions.Single(a => a.Type == ActionType.Group);
        Assert.Equal(DefenseType.Block, group.AttackResult!.DefenseType);
        Assert.False(group.AttackResult.CounterChain);
        Assert.Empty(group.CounterExchanges);
    }
}

public class IntegrationScenario3SPECIALActionThroughPipeline
{
    [Fact(DisplayName = "SPECIAL consumes energy segments after resolution")]
    public void Consumes_energy()
    {
        var p1 = IntegrationFixtures.Build("p1") with { Energy = 3 };
        var enemy = IntegrationFixtures.Build("e1") with { Stamina = 200, MaxStamina = 200, Power = 30 };
        var state = IntegrationFixtures.State([p1], [enemy]);

        var result = IntegrationFixtures.Run(state, [IntegrationFixtures.Act("p1", ActionType.Special, "e1", 2)], IntegrationFixtures.RollMid);

        Assert.True(IntegrationFixtures.Find(result, "p1").Energy < 3);
    }

    [Fact(DisplayName = "SPECIAL deals more damage than a plain ATTACK with the same attacker power")]
    public void Special_more_damage()
    {
        var attackState = IntegrationFixtures.State(
            [IntegrationFixtures.Build("p1")],
            [IntegrationFixtures.Build("e1") with { Stamina = 500, MaxStamina = 500, Power = 30 }]);
        var specialState = IntegrationFixtures.State(
            [IntegrationFixtures.Build("p1") with { Energy = 3 }],
            [IntegrationFixtures.Build("e1") with { Stamina = 500, MaxStamina = 500, Power = 30 }]);

        var attackResult = IntegrationFixtures.Run(attackState, [IntegrationFixtures.Act("p1", ActionType.Attack, "e1")], IntegrationFixtures.RollHigh);
        var specialResult = IntegrationFixtures.Run(specialState, [IntegrationFixtures.Act("p1", ActionType.Special, "e1", 3)], IntegrationFixtures.RollHigh);

        var attackDamage = 500 - IntegrationFixtures.Find(attackResult, "e1").Stamina;
        var specialDamage = 500 - IntegrationFixtures.Find(specialResult, "e1").Stamina;
        Assert.True(specialDamage > attackDamage);
    }

    [Fact(DisplayName = "SPECIAL with no energy is rejected by declaration validation")]
    public void No_energy_rejected()
    {
        var p1 = IntegrationFixtures.Build("p1");
        var enemy = IntegrationFixtures.Build("e1") with { Stamina = 200, MaxStamina = 200, Power = 30 };
        var state = IntegrationFixtures.State([p1], [enemy]);
        var orders = new[] { IntegrationFixtures.Act("p1", ActionType.Special, "e1", 2) };

        Assert.Null(Record.Exception(() => IntegrationFixtures.Run(state, orders, IntegrationFixtures.RollMid)));

        var result = IntegrationFixtures.Run(state, orders, IntegrationFixtures.RollMid);
        Assert.Equal(2, result.Round);
        Assert.DoesNotContain(result.RoundHistory[0].Actions, a => a.CombatantId == "p1");
    }
}

public class IntegrationScenario4CounterChainViaParry
{
    [Fact(DisplayName = "attack on a target with very high Parry SR triggers counter chain")]
    public void Triggers_chain()
    {
        var p1 = IntegrationFixtures.Build("p1") with { Stamina = 200, MaxStamina = 200 };
        var e1 = IntegrationFixtures.Build("e1") with { Stamina = 200, MaxStamina = 200, ReactionSkills = IntegrationFixtures.HighParry };
        var state = IntegrationFixtures.State([p1], [e1]);
        var orders = new[] { IntegrationFixtures.Act("p1", ActionType.Attack, "e1") };

        Assert.Null(Record.Exception(() => IntegrationFixtures.Run(state, orders, IntegrationFixtures.RollLow)));

        var result = IntegrationFixtures.Run(state, orders, IntegrationFixtures.RollLow);

        Assert.Equal(2, result.Round);
        Assert.Contains(result.Status, new[] { CombatStatus.Active, CombatStatus.Victory, CombatStatus.Defeat });
        var parriedWithCounter = result.RoundHistory[0].Actions.Any(
            a => a.AttackResult?.DefenseType == DefenseType.Parry && a.AttackResult.CounterChain);
        Assert.True(parriedWithCounter);
        Assert.Contains(result.RoundHistory[0].Actions, a => a.CounterExchanges.Length > 0);
    }

    [Fact(DisplayName = "counter chain terminates and does not cause infinite loops")]
    public void Terminates()
    {
        var p1 = IntegrationFixtures.Build("p1") with { Stamina = 500, MaxStamina = 500, Power = 10, ReactionSkills = IntegrationFixtures.HighParry };
        var e1 = IntegrationFixtures.Build("e1") with { Stamina = 500, MaxStamina = 500, Power = 10, ReactionSkills = IntegrationFixtures.HighParry };
        var state = IntegrationFixtures.State([p1], [e1]);
        var orders = new[] { IntegrationFixtures.Act("p1", ActionType.Attack, "e1") };

        var watch = Stopwatch.StartNew();
        Assert.Null(Record.Exception(() => IntegrationFixtures.Run(state, orders, IntegrationFixtures.RollLow)));
        watch.Stop();

        Assert.True(watch.ElapsedMilliseconds < 1000);
        var result = IntegrationFixtures.Run(state, orders, IntegrationFixtures.RollLow);
        Assert.Equal(2, result.Round);
        var longest = result.RoundHistory[0].Actions.Max(a => a.CounterExchanges.Length);
        Assert.Equal(CombatConstants.MaxChainDepth, longest);
    }
}

public class IntegrationScenario5MultiRoundStateProgression
{
    [Fact(DisplayName = "running 3 rounds advances the round counter and accumulates history")]
    public void Three_rounds()
    {
        var p1 = IntegrationFixtures.Build("p1") with { Stamina = 500, MaxStamina = 500, Power = 30 };
        var elena = IntegrationFixtures.Build("elena") with { ArchetypeId = TestArchetypes.Elena, ElementalPath = ElementalPath.Light, Stamina = 500, MaxStamina = 500, Power = 30 };
        var state = IntegrationFixtures.State([p1], [elena]);
        var orders = new[] { IntegrationFixtures.Act("p1", ActionType.Attack, "elena") };

        state = IntegrationFixtures.Run(state, orders, IntegrationFixtures.RollMid);
        state = IntegrationFixtures.Run(state, orders, IntegrationFixtures.RollMid);
        state = IntegrationFixtures.Run(state, orders, IntegrationFixtures.RollMid);

        Assert.Equal(4, state.Round);
        Assert.Equal(3, state.RoundHistory.Length);
    }

    [Fact(DisplayName = "stamina decreases across multiple rounds of ATTACK")]
    public void Stamina_decreases()
    {
        var p1 = IntegrationFixtures.Build("p1") with { Stamina = 500, MaxStamina = 500, Power = 30 };
        var lars = IntegrationFixtures.Build("lars") with { ArchetypeId = TestArchetypes.Lars, ElementalPath = ElementalPath.Earth, Stamina = 500, MaxStamina = 500, Power = 30 };
        var state = IntegrationFixtures.State([p1], [lars]);
        var orders = new[] { IntegrationFixtures.Act("p1", ActionType.Attack, "lars") };
        var initialLarsStamina = state.EnemyParty[0].Stamina;

        state = IntegrationFixtures.Run(state, orders, IntegrationFixtures.RollHigh);
        state = IntegrationFixtures.Run(state, orders, IntegrationFixtures.RollHigh);
        state = IntegrationFixtures.Run(state, orders, IntegrationFixtures.RollHigh);

        Assert.True(state.EnemyParty[0].Stamina < initialLarsStamina);
    }

    [Fact(DisplayName = "round counter advances correctly even when status changes to victory or defeat")]
    public void Counter_advances_on_victory()
    {
        var p1 = IntegrationFixtures.Build("p1") with { Power = 10000, Stamina = 500, MaxStamina = 500 };
        var kade = IntegrationFixtures.Build("kade") with { ArchetypeId = TestArchetypes.Kade, Stamina = 50, MaxStamina = 50, Power = 10 };
        var state = IntegrationFixtures.State([p1], [kade]);

        state = IntegrationFixtures.Run(state, [IntegrationFixtures.Act("p1", ActionType.Attack, "kade")], IntegrationFixtures.RollHigh);

        Assert.Contains(state.Status, new[] { CombatStatus.Active, CombatStatus.Victory, CombatStatus.Defeat });
        Assert.Equal(2, state.Round);
        Assert.Single(state.RoundHistory);
    }

    [Fact(DisplayName = "multi-round with GROUP: energy depleted after GROUP, builds up again via ATTACK")]
    public void Group_then_attack()
    {
        var p1 = IntegrationFixtures.Build("p1") with { Energy = 3, MaxEnergy = 3 };
        var p2 = IntegrationFixtures.Build("p2") with { Energy = 3, MaxEnergy = 3 };
        var enemy = IntegrationFixtures.Build("e1") with { Stamina = 1000, MaxStamina = 1000, Power = 10 };
        var state = IntegrationFixtures.State([p1, p2], [enemy]);

        state = IntegrationFixtures.Run(state, [IntegrationFixtures.Act("p1", ActionType.Group, "e1")], IntegrationFixtures.RollMid);

        var p1AfterGroup = IntegrationFixtures.Find(state, "p1");
        Assert.True(p1AfterGroup.Energy < 3);
        Assert.True(IntegrationFixtures.Find(state, "p2").Energy < 3);
        var p1EnergyAfterGroup = p1AfterGroup.Energy;

        state = IntegrationFixtures.Run(
            state,
            [IntegrationFixtures.Act("p1", ActionType.Attack, "e1"), IntegrationFixtures.Act("p2", ActionType.Attack, "e1")],
            IntegrationFixtures.RollMid);

        Assert.True(IntegrationFixtures.Find(state, "p1").Energy > p1EnergyAfterGroup);
        Assert.Equal(3, state.Round);
        Assert.Equal(2, state.RoundHistory.Length);
    }
}
