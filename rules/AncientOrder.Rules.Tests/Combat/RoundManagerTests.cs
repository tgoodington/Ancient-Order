using System.Collections.Immutable;
using AncientOrder.Rules.Combat;
using AncientOrder.Rules.Combat.Ai;
using AncientOrder.Rules.Common;
using AncientOrder.Rules.Tests.Fixtures;
using AncientOrder.Rules.Tests.Support;

namespace AncientOrder.Rules.Tests.Combat;

// Carried over from src/combat/roundManager.test.ts. The TypeScript archetype 'test' (stub AI) is a Basic archetype 'test'.
// runRound(state, orders, FIXED_ROLL(n)) is RoundManager.RunRound(state, orders, archetypes, new ConstantRolls(n)).
// Changed: the two stubBehaviorTree tests call EnemyDecisions.Decide with the Basic archetype (the stub is retired).
// Dropped: the six buildVisualInfo tests (visual info is not ported; T7).
// New: snapshots without earlier history, Evade recovery and counter exchanges recorded, no enemy GROUP, one order per fighter, dice.
internal static class RoundFixtures
{
    public static readonly ReactionSkills Standard = new(
        new DefenseSkill(0.6, 0.5, 0.2),
        new DefenseSkill(0.5, 0.8, 0.15),
        new DefenseSkill(0.4, 0.9, 0.1));

    public static ArchetypeCatalog Archetypes { get; } = ArchetypeCatalog.Create(
        [TestArchetypes.Basic("test"), TestArchetypes.ElenaLoyalScout]).Value;

    public static Combatant Make(string id) => new(
        id,
        id,
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

    public static CombatState State(Combatant[] players, Combatant[] enemies, int round = 1) =>
        new(
            round,
            CombatPhase.AiDecision,
            [.. players],
            [.. enemies],
            [],
            [],
            CombatStatus.Active,
            DiceState.FromSeed(7));

    public static CombatAction Act(string who, ActionType type, string? target, double? segments = null) =>
        new(who, type, target, segments);

    public static CombatState Run(CombatState state, IReadOnlyList<CombatAction> orders, double roll) =>
        RoundManager.RunRound(state, orders, Archetypes, new ConstantRolls(roll));

    public static CombatState Run(CombatState state, IReadOnlyList<CombatAction> orders, IRollSource rolls) =>
        RoundManager.RunRound(state, orders, Archetypes, rolls);

    public static Combatant Find(CombatState state, string id) =>
        state.PlayerParty.Concat(state.EnemyParty).Single(c => c.Id == id);

    public static CombatAction[] P1AttacksE1 { get; } = [Act("p1", ActionType.Attack, "e1")];
}

public class RunRoundBasicExecution
{
    [Fact(DisplayName = "returns a new CombatState (reference inequality)")]
    public void New_state()
    {
        var state = RoundFixtures.State([RoundFixtures.Make("p1")], [RoundFixtures.Make("e1")]);

        var result = RoundFixtures.Run(state, RoundFixtures.P1AttacksE1, 10);

        Assert.NotSame(state, result);
    }

    [Fact(DisplayName = "increments the round counter after resolution")]
    public void Increments_round()
    {
        var state = RoundFixtures.State([RoundFixtures.Make("p1")], [RoundFixtures.Make("e1")], 1);

        Assert.Equal(2, RoundFixtures.Run(state, RoundFixtures.P1AttacksE1, 10).Round);
    }

    [Fact(DisplayName = "resets phase to AI_DECISION after resolution")]
    public void Resets_phase()
    {
        var state = RoundFixtures.State([RoundFixtures.Make("p1")], [RoundFixtures.Make("e1")]);

        Assert.Equal(CombatPhase.AiDecision, RoundFixtures.Run(state, RoundFixtures.P1AttacksE1, 10).Phase);
    }

    [Fact(DisplayName = "clears actionQueue after round resolves")]
    public void Clears_queue()
    {
        var state = RoundFixtures.State([RoundFixtures.Make("p1")], [RoundFixtures.Make("e1")]);

        Assert.Empty(RoundFixtures.Run(state, RoundFixtures.P1AttacksE1, 10).ActionQueue);
    }

    [Fact(DisplayName = "applies damage to the enemy combatant after a player ATTACK")]
    public void Applies_damage()
    {
        var state = RoundFixtures.State([RoundFixtures.Make("p1")], [RoundFixtures.Make("e1")]);

        var result = RoundFixtures.Run(state, RoundFixtures.P1AttacksE1, 20);

        Assert.True(RoundFixtures.Find(result, "e1").Stamina < 100);
    }

    [Fact(DisplayName = "input state is immutable — original state unchanged after runRound")]
    public void Input_unchanged()
    {
        var state = RoundFixtures.State([RoundFixtures.Make("p1")], [RoundFixtures.Make("e1")]);
        var originalPlayerStamina = state.PlayerParty[0].Stamina;
        var originalEnemyStamina = state.EnemyParty[0].Stamina;

        RoundFixtures.Run(state, RoundFixtures.P1AttacksE1, 20);

        Assert.Equal(originalPlayerStamina, state.PlayerParty[0].Stamina);
        Assert.Equal(originalEnemyStamina, state.EnemyParty[0].Stamina);
        Assert.Equal(1, state.Round);
    }
}

public class RunRoundPhase1AIStubIntegration
{
    [Fact(DisplayName = "AI stub generates one ATTACK action per non-KO'd enemy")]
    public void One_attack_per_enemy()
    {
        var state = RoundFixtures.State([RoundFixtures.Make("p1")], [RoundFixtures.Make("e1"), RoundFixtures.Make("e2")]);

        var result = RoundFixtures.Run(state, [], 10);

        Assert.True(RoundFixtures.Find(result, "p1").Stamina < 100);
        Assert.Equal(2, result.RoundHistory[0].Actions.Count(a => a.Type == ActionType.Attack));
    }

    [Fact(DisplayName = "AI stub targets the first non-KO'd player combatant")]
    public void Targets_first_living()
    {
        var p1 = RoundFixtures.Make("p1") with { IsKO = true, Stamina = 0 };
        var enemy = RoundFixtures.Make("e1");
        var state = RoundFixtures.State([p1, RoundFixtures.Make("p2")], [enemy]);

        var aiAction = EnemyDecisions.Decide(enemy, state, RoundFixtures.Archetypes, EvaluatorConfig.Default);

        Assert.Equal("p2", aiAction.TargetId);
    }

    [Fact(DisplayName = "AI stub produces ATTACK type action")]
    public void Produces_attack()
    {
        var enemy = RoundFixtures.Make("e1");
        var state = RoundFixtures.State([RoundFixtures.Make("p1")], [enemy]);

        var aiAction = EnemyDecisions.Decide(enemy, state, RoundFixtures.Archetypes, EvaluatorConfig.Default);

        Assert.Equal(ActionType.Attack, aiAction.Type);
        Assert.Equal("e1", aiAction.CombatantId);
    }

    [Fact(DisplayName = "KO'd enemies do not produce AI actions")]
    public void Ko_enemies_skip()
    {
        var player = RoundFixtures.Make("p1") with { Stamina = 1000, MaxStamina = 1000 };
        var koEnemy = RoundFixtures.Make("e2") with { IsKO = true, Stamina = 0 };
        var state = RoundFixtures.State([player], [RoundFixtures.Make("e1"), koEnemy]);

        var result = RoundFixtures.Run(state, [], 10);

        var roundRecord = result.RoundHistory[0];
        Assert.DoesNotContain("e2", roundRecord.Actions.Select(a => a.CombatantId));
    }
}

public class RunRoundPhase4PrioritySorting
{
    [Fact(DisplayName = "higher priority actions resolve first in the round")]
    public void Priority_order()
    {
        var p1 = RoundFixtures.Make("p1");
        var p2 = RoundFixtures.Make("p2");
        var state = RoundFixtures.State([p1, p2], [RoundFixtures.Make("e1")]);
        var orders = new[]
        {
            RoundFixtures.Act("p1", ActionType.Attack, "e1"),
            RoundFixtures.Act("p2", ActionType.Defend, "p1"),
        };

        var result = RoundFixtures.Run(state, orders, 20);

        Assert.True(RoundFixtures.Find(result, "p2").Stamina < 100);
        Assert.Equal(100, RoundFixtures.Find(result, "p1").Stamina);
    }
}

public class RunRound3v3RoundResolvesWithoutErrors
{
    private static CombatState ThreeVsThree() => RoundFixtures.State(
        [RoundFixtures.Make("p1"), RoundFixtures.Make("p2"), RoundFixtures.Make("p3")],
        [RoundFixtures.Make("e1"), RoundFixtures.Make("e2"), RoundFixtures.Make("e3")]);

    [Fact(DisplayName = "3v3 round completes with all standard actions")]
    public void Standard_actions()
    {
        var orders = new[]
        {
            RoundFixtures.Act("p1", ActionType.Attack, "e1"),
            RoundFixtures.Act("p2", ActionType.Attack, "e2"),
            RoundFixtures.Act("p3", ActionType.Evade, null),
        };

        Assert.Null(Record.Exception(() => RoundFixtures.Run(ThreeVsThree(), orders, 10)));
    }

    [Fact(DisplayName = "3v3 round produces valid CombatState after resolution")]
    public void Valid_state()
    {
        var orders = new[]
        {
            RoundFixtures.Act("p1", ActionType.Attack, "e1"),
            RoundFixtures.Act("p2", ActionType.Attack, "e2"),
            RoundFixtures.Act("p3", ActionType.Attack, "e3"),
        };

        var result = RoundFixtures.Run(ThreeVsThree(), orders, 10);

        Assert.Equal(3, result.PlayerParty.Length);
        Assert.Equal(3, result.EnemyParty.Length);
        Assert.Equal(2, result.Round);
        Assert.Contains(result.Status, new[] { CombatStatus.Active, CombatStatus.Victory, CombatStatus.Defeat });
    }

    [Fact(DisplayName = "3v3 round with mixed action types resolves without errors")]
    public void Mixed_actions()
    {
        var state = RoundFixtures.State(
            [RoundFixtures.Make("p1") with { Energy = 3, MaxEnergy = 5 }, RoundFixtures.Make("p2"), RoundFixtures.Make("p3")],
            [RoundFixtures.Make("e1"), RoundFixtures.Make("e2"), RoundFixtures.Make("e3")]);
        var orders = new[]
        {
            RoundFixtures.Act("p1", ActionType.Special, "e1", 2),
            RoundFixtures.Act("p2", ActionType.Defend, "p3"),
            RoundFixtures.Act("p3", ActionType.Evade, null),
        };

        Assert.Null(Record.Exception(() => RoundFixtures.Run(state, orders, 10)));
    }

    [Fact(DisplayName = "multiple consecutive rounds update state progressively")]
    public void Consecutive_rounds()
    {
        var p1 = RoundFixtures.Make("p1") with { Stamina = 200, MaxStamina = 200 };
        var e1 = RoundFixtures.Make("e1") with { Stamina = 200, MaxStamina = 200 };
        var state = RoundFixtures.State([p1], [e1]);

        state = RoundFixtures.Run(state, RoundFixtures.P1AttacksE1, 20);
        state = RoundFixtures.Run(state, RoundFixtures.P1AttacksE1, 20);
        state = RoundFixtures.Run(state, RoundFixtures.P1AttacksE1, 20);

        Assert.Equal(4, state.Round);
        Assert.Equal(3, state.RoundHistory.Length);
    }
}

public class RunRoundPhase6RoundResultRecordedInRoundHistory
{
    [Fact(DisplayName = "appends one RoundResult to roundHistory per round")]
    public void One_per_round()
    {
        var state = RoundFixtures.State([RoundFixtures.Make("p1")], [RoundFixtures.Make("e1")]);

        Assert.Single(RoundFixtures.Run(state, RoundFixtures.P1AttacksE1, 10).RoundHistory);
    }

    [Fact(DisplayName = "RoundResult records the correct round number")]
    public void Round_number()
    {
        var state = RoundFixtures.State([RoundFixtures.Make("p1")], [RoundFixtures.Make("e1")], 3);

        Assert.Equal(3, RoundFixtures.Run(state, RoundFixtures.P1AttacksE1, 10).RoundHistory[0].Round);
    }

    [Fact(DisplayName = "RoundResult contains a state snapshot")]
    public void Has_snapshot()
    {
        var state = RoundFixtures.State([RoundFixtures.Make("p1")], [RoundFixtures.Make("e1")]);

        var roundRecord = RoundFixtures.Run(state, RoundFixtures.P1AttacksE1, 10).RoundHistory[0];

        Assert.NotNull(roundRecord.StateSnapshot);
        Assert.Single(roundRecord.StateSnapshot.PlayerParty);
    }

    [Fact(DisplayName = "RoundResult actions array contains entries for each action resolved")]
    public void Actions_recorded()
    {
        var state = RoundFixtures.State([RoundFixtures.Make("p1")], [RoundFixtures.Make("e1")]);

        var roundRecord = RoundFixtures.Run(state, RoundFixtures.P1AttacksE1, 10).RoundHistory[0];

        Assert.True(roundRecord.Actions.Length >= 1);
        Assert.Equal(2, roundRecord.Actions.Length);
    }

    [Fact(DisplayName = "accumulates roundHistory across multiple rounds")]
    public void Accumulates()
    {
        var player = RoundFixtures.Make("p1") with { Stamina = 500, MaxStamina = 500 };
        var enemy = RoundFixtures.Make("e1") with { Stamina = 500, MaxStamina = 500 };
        var state = RoundFixtures.State([player], [enemy]);

        state = RoundFixtures.Run(state, RoundFixtures.P1AttacksE1, 10);
        state = RoundFixtures.Run(state, RoundFixtures.P1AttacksE1, 10);

        Assert.Equal(2, state.RoundHistory.Length);
        Assert.Equal(1, state.RoundHistory[0].Round);
        Assert.Equal(2, state.RoundHistory[1].Round);
    }
}

public class RunRoundVictoryDefeatDetection
{
    [Fact(DisplayName = "status becomes \"victory\" when all enemies are KO'd")]
    public void Victory()
    {
        var player = RoundFixtures.Make("p1") with { Power = 10000 };
        var enemy = RoundFixtures.Make("e1") with { Stamina = 1, MaxStamina = 100, Power = 1 };
        var state = RoundFixtures.State([player], [enemy]);

        var result = RoundFixtures.Run(state, RoundFixtures.P1AttacksE1, 20);

        Assert.True(RoundFixtures.Find(result, "e1").IsKO);
        Assert.Equal(CombatStatus.Victory, result.Status);
    }

    [Fact(DisplayName = "status becomes \"defeat\" when all players are KO'd")]
    public void Defeat()
    {
        var player = RoundFixtures.Make("p1") with { Stamina = 1, MaxStamina = 100, Power = 1 };
        var enemy = RoundFixtures.Make("e1") with { Power = 10000 };
        var state = RoundFixtures.State([player], [enemy]);

        var result = RoundFixtures.Run(state, [], 20);

        Assert.True(RoundFixtures.Find(result, "p1").IsKO);
        Assert.Equal(CombatStatus.Defeat, result.Status);
    }

    [Fact(DisplayName = "status remains \"active\" when neither side is fully KO'd")]
    public void Active()
    {
        var player = RoundFixtures.Make("p1") with { Stamina = 1000, MaxStamina = 1000, Power = 1 };
        var enemy = RoundFixtures.Make("e1") with { Stamina = 1000, MaxStamina = 1000, Power = 1 };
        var state = RoundFixtures.State([player], [enemy]);

        var result = RoundFixtures.Run(state, RoundFixtures.P1AttacksE1, 10);

        Assert.Equal(CombatStatus.Active, result.Status);
    }
}

public class RunRoundPhase3DeclarationValidation
{
    [Fact(DisplayName = "invalid player declaration is dropped (combatant not found)")]
    public void Unknown_combatant()
    {
        var state = RoundFixtures.State([RoundFixtures.Make("p1")], [RoundFixtures.Make("e1")]);
        var orders = new[] { RoundFixtures.Act("DOES_NOT_EXIST", ActionType.Attack, "e1") };

        Assert.Null(Record.Exception(() => RoundFixtures.Run(state, orders, 10)));
    }

    [Fact(DisplayName = "KO'd player combatant cannot declare actions")]
    public void Ko_cannot_declare()
    {
        var koPlayer = RoundFixtures.Make("p1") with { IsKO = true, Stamina = 0 };
        var state = RoundFixtures.State([koPlayer, RoundFixtures.Make("p2")], [RoundFixtures.Make("e1")]);
        var orders = new[]
        {
            RoundFixtures.Act("p1", ActionType.Attack, "e1"),
            RoundFixtures.Act("p2", ActionType.Attack, "e1"),
        };

        var result = RoundFixtures.Run(state, orders, 10);

        var actors = result.RoundHistory[0].Actions.Select(a => a.CombatantId).ToList();
        Assert.DoesNotContain("p1", actors);
        Assert.Contains("p2", actors);
    }

    [Fact(DisplayName = "GROUP declaration with insufficient ally energy falls back to ATTACK")]
    public void Group_falls_back()
    {
        var p1 = RoundFixtures.Make("p1") with { Energy = 5, MaxEnergy = 5 };
        var p2 = RoundFixtures.Make("p2") with { Energy = 0, MaxEnergy = 5 };
        var state = RoundFixtures.State([p1, p2], [RoundFixtures.Make("e1")]);
        var orders = new[] { RoundFixtures.Act("p1", ActionType.Group, "e1") };

        var result = RoundFixtures.Run(state, orders, 10);

        var p1Action = result.RoundHistory[0].Actions.Single(a => a.CombatantId == "p1");
        Assert.NotEqual(ActionType.Group, p1Action.Type);
        Assert.Equal(ActionType.Attack, p1Action.Type);
    }

    [Fact(DisplayName = "empty player declarations still runs round with AI actions only")]
    public void Empty_declarations()
    {
        var state = RoundFixtures.State([RoundFixtures.Make("p1")], [RoundFixtures.Make("e1")]);

        var result = RoundFixtures.Run(state, [], 10);

        Assert.Single(result.RoundHistory);
        Assert.Equal("e1", Assert.Single(result.RoundHistory[0].Actions).CombatantId);
    }
}

// New (T7): one order per fighter, enemies never choose GROUP.
public class RunRoundOneOrderPerFighter
{
    [Fact(DisplayName = "a fighter's second order is dropped and only the first resolves")]
    public void Second_order_dropped()
    {
        var p1 = RoundFixtures.Make("p1") with { Energy = 3 };
        var state = RoundFixtures.State([p1], [RoundFixtures.Make("e1") with { Stamina = 1000, MaxStamina = 1000 }]);
        var orders = new[]
        {
            RoundFixtures.Act("p1", ActionType.Special, "e1", 1),
            RoundFixtures.Act("p1", ActionType.Special, "e1", 1),
        };

        var result = RoundFixtures.Run(state, orders, 20);

        var p1Actions = result.RoundHistory[0].Actions.Where(a => a.CombatantId == "p1").ToList();
        Assert.Single(p1Actions);
        Assert.Equal(ActionType.Special, p1Actions[0].Type);
    }

    [Fact(DisplayName = "a GROUP leader does not also attack")]
    public void Group_leader_does_not_attack()
    {
        var p1 = RoundFixtures.Make("p1") with { Energy = 5, MaxEnergy = 5 };
        var p2 = RoundFixtures.Make("p2") with { Energy = 5, MaxEnergy = 5 };
        var state = RoundFixtures.State([p1, p2], [RoundFixtures.Make("e1") with { Stamina = 1000, MaxStamina = 1000 }]);
        var orders = new[]
        {
            RoundFixtures.Act("p2", ActionType.Attack, "e1"),
            RoundFixtures.Act("p1", ActionType.Group, "e1"),
            RoundFixtures.Act("p1", ActionType.Attack, "e1"),
        };

        var result = RoundFixtures.Run(state, orders, 20);

        var playerActions = result.RoundHistory[0].Actions.Where(a => a.CombatantId is "p1" or "p2").ToList();
        Assert.Equal(ActionType.Group, Assert.Single(playerActions).Type);
        Assert.Equal("p1", playerActions[0].CombatantId);
    }
}

public class RunRoundEnemiesNeverChooseGroup
{
    [Fact(DisplayName = "enemies with every ally at full energy still never GROUP")]
    public void No_enemy_group()
    {
        var elena = RoundFixtures.Make("e1") with { ArchetypeId = TestArchetypes.Elena, Energy = 5, MaxEnergy = 5, Stamina = 5000, MaxStamina = 5000 };
        var enemies = new[] { elena, elena with { Id = "e2", Name = "e2" }, elena with { Id = "e3", Name = "e3" } };
        var player = RoundFixtures.Make("p1") with { Stamina = 5000, MaxStamina = 5000 };
        var state = RoundFixtures.State([player], enemies);

        for (var i = 0; i < 6; i++)
        {
            state = RoundFixtures.Run(state, [RoundFixtures.Act("p1", ActionType.Evade, null)], 12);
        }

        Assert.All(state.RoundHistory, r => Assert.DoesNotContain(r.Actions, a => a.Type == ActionType.Group));
    }
}

// New (T7): each round record holds the end-of-round fighters without earlier history, and the missing events.
public class RunRoundRecordedRounds
{
    [Fact(DisplayName = "a round's snapshot holds the end-of-round fighters")]
    public void Snapshot_is_end_of_round()
    {
        var state = RoundFixtures.State([RoundFixtures.Make("p1")], [RoundFixtures.Make("e1")]);

        var result = RoundFixtures.Run(state, RoundFixtures.P1AttacksE1, 20);

        var snapshot = result.RoundHistory[0].StateSnapshot;
        Assert.Equal(result.PlayerParty, snapshot.PlayerParty);
        Assert.Equal(result.EnemyParty, snapshot.EnemyParty);
        Assert.Equal(result.Status, snapshot.Status);
    }

    [Fact(DisplayName = "snapshots carry no earlier history in any round")]
    public void Snapshots_have_no_history()
    {
        var player = RoundFixtures.Make("p1") with { Stamina = 5000, MaxStamina = 5000 };
        var enemy = RoundFixtures.Make("e1") with { Stamina = 5000, MaxStamina = 5000 };
        var state = RoundFixtures.State([player], [enemy]);

        for (var i = 0; i < 5; i++)
        {
            state = RoundFixtures.Run(state, RoundFixtures.P1AttacksE1, 10);
        }

        Assert.Equal(5, state.RoundHistory.Length);
        Assert.All(state.RoundHistory, r => Assert.Empty(r.StateSnapshot.RoundHistory));
    }

    [Fact(DisplayName = "a counter-attack exchange is recorded in the round's actions")]
    public void Records_counter_exchange()
    {
        var p1 = RoundFixtures.Make("p1") with { ReactionSkills = PipelineFixtures.HighParry };
        var e1 = RoundFixtures.Make("e1") with { ReactionSkills = PipelineFixtures.LowParry };
        var state = RoundFixtures.State([p1], [e1]);

        var result = RoundFixtures.Run(state, [], new FixedRolls(1, 19));

        var action = Assert.Single(result.RoundHistory[0].Actions);
        Assert.True(action.AttackResult!.CounterChain);
        var exchange = Assert.Single(action.CounterExchanges);
        Assert.Equal("p1", exchange.AttackerId);
        Assert.Equal("e1", exchange.DefenderId);
        Assert.False(exchange.Outcome.Success);
    }

    [Fact(DisplayName = "an Evade's stamina recovery is recorded in the round's actions")]
    public void Records_evade_recovery()
    {
        var p1 = RoundFixtures.Make("p1");
        var state = RoundFixtures.State([p1], [RoundFixtures.Make("e1")]);

        var result = RoundFixtures.Run(state, [RoundFixtures.Act("p1", ActionType.Evade, null)], 20);

        var evade = result.RoundHistory[0].Actions.Single(a => a.Type == ActionType.Evade);
        Assert.Equal(30, evade.EvadeRecovery!.Value, 5);
    }

    [Fact(DisplayName = "an Evade's recorded recovery is the amount after the cap at max stamina")]
    public void Records_capped_evade_recovery()
    {
        var p1 = RoundFixtures.Make("p1");
        var e1 = RoundFixtures.Make("e1") with { Power = 5 };
        var state = RoundFixtures.State([p1], [e1]);

        var result = RoundFixtures.Run(state, [RoundFixtures.Act("p1", ActionType.Evade, null)], 20);

        var evade = result.RoundHistory[0].Actions.Single(a => a.Type == ActionType.Evade);
        Assert.Equal(4.5, evade.EvadeRecovery!.Value, 5);
        Assert.Equal(100, RoundFixtures.Find(result, "p1").Stamina);
    }
}

// New (T11/T7): the fight's own dice.
public class RunRoundDice
{
    private static CombatState Start() => RoundFixtures.State(
        [RoundFixtures.Make("p1") with { Stamina = 5000, MaxStamina = 5000, Speed = 10 }],
        [RoundFixtures.Make("e1") with { Stamina = 5000, MaxStamina = 5000, Speed = 10 }]);

    [Fact(DisplayName = "RunRound with the fight's dice advances the dice and the snapshot carries the end-of-round dice")]
    public void Dice_advance()
    {
        var state = Start();

        var result = RoundManager.RunRound(state, RoundFixtures.P1AttacksE1, RoundFixtures.Archetypes);

        Assert.NotEqual(state.Dice, result.Dice);
        Assert.Equal(result.Dice, result.RoundHistory[0].StateSnapshot.Dice);
    }

    [Fact(DisplayName = "the same state and orders always give the same round")]
    public void Deterministic()
    {
        var state = Start();

        var first = RoundManager.RunRound(state, RoundFixtures.P1AttacksE1, RoundFixtures.Archetypes);
        var second = RoundManager.RunRound(state, RoundFixtures.P1AttacksE1, RoundFixtures.Archetypes);

        StateAssert.Equivalent(first, second);
    }

    [Fact(DisplayName = "the roll-source overload leaves the dice unchanged")]
    public void Seam_leaves_dice()
    {
        var state = Start();

        var result = RoundFixtures.Run(state, RoundFixtures.P1AttacksE1, 10);

        Assert.Equal(state.Dice, result.Dice);
        Assert.Equal(state.Dice, result.RoundHistory[0].StateSnapshot.Dice);
    }

    [Fact(DisplayName = "a later round continues from the dice the earlier round left")]
    public void Dice_continue()
    {
        var state = Start();

        var round1 = RoundManager.RunRound(state, RoundFixtures.P1AttacksE1, RoundFixtures.Archetypes);
        var round2 = RoundManager.RunRound(round1, RoundFixtures.P1AttacksE1, RoundFixtures.Archetypes);

        Assert.NotEqual(round1.Dice, round2.Dice);
        Assert.Equal(round2.Dice, round2.RoundHistory[1].StateSnapshot.Dice);
        Assert.Equal(round1.Dice, round2.RoundHistory[0].StateSnapshot.Dice);
    }
}
