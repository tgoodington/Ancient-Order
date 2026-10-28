using System.Collections.Immutable;
using AncientOrder.Rules.Combat;
using AncientOrder.Rules.Common;
using AncientOrder.Rules.Tests.Support;

namespace AncientOrder.Rules.Tests.Combat;

// Carried over from src/combat/pipeline.test.ts. sortByPriority is Pipeline.Order; resolvePerAttack and resolveAction are both
// Pipeline.ResolveAction (the result is read from .State and .Result). Constant roll functions are ConstantRolls; roll arrays
// are FixedRolls (which fails if more rolls are drawn than scripted). calculateBaseDamage's second argument is gone (it never
// affected the result). Changed: "speed ties are broken by random factor" (tie rule), and "returns a new array without
// mutating the input" (the result is an ImmutableArray). New (T7): tie rule, KO'd interceptor, action-power Crushing Blow,
// recorded counter exchanges and Evade recovery.
internal static class PipelineFixtures
{
    public static readonly ReactionSkills Standard = new(
        new DefenseSkill(0.6, 0.5, 0.2),
        new DefenseSkill(0.5, 0.8, 0.15),
        new DefenseSkill(0.4, 0.9, 0.1));

    public static readonly ReactionSkills HighParry = new(
        new DefenseSkill(0.9, 0.5, 0.2),
        new DefenseSkill(0.9, 0.8, 0.15),
        new DefenseSkill(0.9, 0.9, 0.1));

    public static readonly ReactionSkills LowParry = new(
        new DefenseSkill(0.1, 0.2, 0.05),
        new DefenseSkill(0.1, 0.8, 0.05),
        new DefenseSkill(0.1, 0.9, 0.2));

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

    public static CombatState State(Combatant[] players, Combatant[] enemies, params CombatAction[] queue) =>
        new(
            1,
            CombatPhase.PerAttack,
            [.. players],
            [.. enemies],
            [.. queue],
            [],
            CombatStatus.Active,
            DiceState.FromSeed(1));

    public static Combatant Find(CombatState state, string id) =>
        state.PlayerParty.Concat(state.EnemyParty).Single(c => c.Id == id);

    public static CombatAction Act(string who, ActionType type, string? target, double? segments = null) =>
        new(who, type, target, segments);

    public static ActionResolution Resolve(CombatState state, CombatAction action, IRollSource rolls) =>
        Pipeline.ResolveAction(state, action, rolls);

    public static IReadOnlyList<string> Ids(IEnumerable<CombatAction> actions) => [.. actions.Select(a => a.CombatantId)];
}

public class SortByPriority
{
    public class PriorityOrdering
    {
        [Fact(DisplayName = "GROUP (0) resolves before DEFEND (1)")]
        public void Group_before_defend()
        {
            var state = PipelineFixtures.State([PipelineFixtures.Make("p1")], [PipelineFixtures.Make("e1")]);
            var actions = new[]
            {
                PipelineFixtures.Act("p1", ActionType.Defend, "p1"),
                PipelineFixtures.Act("e1", ActionType.Group, "p1"),
            };

            var sorted = Pipeline.Order(actions, state, new ConstantRolls(10));

            Assert.Equal(ActionType.Group, sorted[0].Type);
            Assert.Equal(ActionType.Defend, sorted[1].Type);
        }

        [Fact(DisplayName = "DEFEND (1) resolves before ATTACK (2)")]
        public void Defend_before_attack()
        {
            var state = PipelineFixtures.State([PipelineFixtures.Make("p1")], [PipelineFixtures.Make("e1")]);
            var actions = new[]
            {
                PipelineFixtures.Act("p1", ActionType.Attack, "e1"),
                PipelineFixtures.Act("e1", ActionType.Defend, "e1"),
            };

            var sorted = Pipeline.Order(actions, state, new ConstantRolls(10));

            Assert.Equal(ActionType.Defend, sorted[0].Type);
            Assert.Equal(ActionType.Attack, sorted[1].Type);
        }

        [Fact(DisplayName = "ATTACK (2) resolves before EVADE (3)")]
        public void Attack_before_evade()
        {
            var state = PipelineFixtures.State([PipelineFixtures.Make("p1")], [PipelineFixtures.Make("e1")]);
            var actions = new[]
            {
                PipelineFixtures.Act("p1", ActionType.Evade, null),
                PipelineFixtures.Act("e1", ActionType.Attack, "p1"),
            };

            var sorted = Pipeline.Order(actions, state, new ConstantRolls(10));

            Assert.Equal(ActionType.Attack, sorted[0].Type);
            Assert.Equal(ActionType.Evade, sorted[1].Type);
        }

        [Fact(DisplayName = "SPECIAL (2) shares priority with ATTACK (2)")]
        public void Special_shares_priority()
        {
            var state = PipelineFixtures.State([PipelineFixtures.Make("p1")], [PipelineFixtures.Make("e1")]);
            var actions = new[]
            {
                PipelineFixtures.Act("p1", ActionType.Special, "e1", 2),
                PipelineFixtures.Act("e1", ActionType.Attack, "p1"),
            };

            var sorted = Pipeline.Order(actions, state, new ConstantRolls(10));

            Assert.Equal(2, sorted.Length);
            var types = sorted.Select(a => a.Type).ToList();
            Assert.Contains(ActionType.Special, types);
            Assert.Contains(ActionType.Attack, types);
        }

        [Fact(DisplayName = "full mixed queue: GROUP < DEFEND < ATTACK = SPECIAL < EVADE")]
        public void Full_mixed_queue()
        {
            var state = PipelineFixtures.State(
                [PipelineFixtures.Make("p1"), PipelineFixtures.Make("p2")],
                [PipelineFixtures.Make("e1"), PipelineFixtures.Make("e2")]);
            var actions = new[]
            {
                PipelineFixtures.Act("p1", ActionType.Evade, null),
                PipelineFixtures.Act("p2", ActionType.Attack, "e1"),
                PipelineFixtures.Act("e1", ActionType.Defend, "e2"),
                PipelineFixtures.Act("e2", ActionType.Group, "p1"),
            };

            var sorted = Pipeline.Order(actions, state, new ConstantRolls(10));

            Assert.Equal(ActionType.Group, sorted[0].Type);
            Assert.Equal(ActionType.Defend, sorted[1].Type);
            Assert.Equal(ActionType.Attack, sorted[2].Type);
            Assert.Equal(ActionType.Evade, sorted[3].Type);
        }
    }

    public class SpeedBasedTieBreakingWithinSamePriority
    {
        [Fact(DisplayName = "higher speed combatant resolves first within same priority")]
        public void Higher_speed_first()
        {
            var fast = PipelineFixtures.Make("fast") with { Speed = 20 };
            var slow = PipelineFixtures.Make("slow") with { Speed = 5 };
            var state = PipelineFixtures.State([fast], [slow]);
            var actions = new[]
            {
                PipelineFixtures.Act("slow", ActionType.Attack, "fast"),
                PipelineFixtures.Act("fast", ActionType.Attack, "slow"),
            };

            var sorted = Pipeline.Order(actions, state, new ConstantRolls(10));

            Assert.Equal("fast", sorted[0].CombatantId);
            Assert.Equal("slow", sorted[1].CombatantId);
        }

        [Fact(DisplayName = "speed ties are broken by random factor")]
        public void Ties_by_roll()
        {
            // Changed (tie rule): each tied action draws one roll in queue order, highest first. The TypeScript asserted only the
            // length; rolls 15 then 5 now put a (15) ahead of b (5).
            var state = PipelineFixtures.State([PipelineFixtures.Make("a")], [PipelineFixtures.Make("b")]);
            var actions = new[]
            {
                PipelineFixtures.Act("a", ActionType.Attack, "b"),
                PipelineFixtures.Act("b", ActionType.Attack, "a"),
            };

            var sorted = Pipeline.Order(actions, state, new FixedRolls(15, 5));

            Assert.Equal(2, sorted.Length);
            Assert.Equal(["a", "b"], PipelineFixtures.Ids(sorted));
        }

        [Fact(DisplayName = "DEFEND actions are independent of ATTACK speed in their own bracket")]
        public void Defend_bracket()
        {
            var fastDefend = PipelineFixtures.Make("fastDefend") with { Speed = 20 };
            var slowDefend = PipelineFixtures.Make("slowDefend") with { Speed = 5 };
            var enemy = PipelineFixtures.Make("e1");
            var state = PipelineFixtures.State([fastDefend, slowDefend], [enemy]);
            var actions = new[]
            {
                PipelineFixtures.Act("slowDefend", ActionType.Defend, "slowDefend"),
                PipelineFixtures.Act("fastDefend", ActionType.Defend, "fastDefend"),
                PipelineFixtures.Act("e1", ActionType.Attack, "fastDefend"),
            };

            var sorted = Pipeline.Order(actions, state, new ConstantRolls(10));

            Assert.Equal(ActionType.Defend, sorted[0].Type);
            Assert.Equal(ActionType.Defend, sorted[1].Type);
            Assert.Equal(ActionType.Attack, sorted[2].Type);
            Assert.Equal("fastDefend", sorted[0].CombatantId);
        }
    }

    public class GROUPTeamAverageSpeedTieBreaking
    {
        [Fact(DisplayName = "GROUP action uses team average speed for priority ordering")]
        public void Team_average()
        {
            var p1 = PipelineFixtures.Make("p1") with { Speed = 10 };
            var p2 = PipelineFixtures.Make("p2") with { Speed = 20 };
            var e1 = PipelineFixtures.Make("e1") with { Speed = 8 };
            var e2 = PipelineFixtures.Make("e2") with { Speed = 12 };
            var state = PipelineFixtures.State([p1, p2], [e1, e2]);
            var actions = new[]
            {
                PipelineFixtures.Act("e1", ActionType.Group, "p1"),
                PipelineFixtures.Act("p1", ActionType.Group, "e1"),
            };

            var sorted = Pipeline.Order(actions, state, new ConstantRolls(10));

            Assert.Equal("p1", sorted[0].CombatantId);
            Assert.Equal("e1", sorted[1].CombatantId);
        }

        [Fact(DisplayName = "KO'd allies are excluded from GROUP average speed calculation")]
        public void Ko_excluded()
        {
            var p1 = PipelineFixtures.Make("p1") with { Speed = 10 };
            var p2 = PipelineFixtures.Make("p2") with { Speed = 30, IsKO = true, Stamina = 0 };
            var enemy = PipelineFixtures.Make("e1") with { Speed = 12 };
            var state = PipelineFixtures.State([p1, p2], [enemy]);
            var actions = new[]
            {
                PipelineFixtures.Act("p1", ActionType.Group, "e1"),
                PipelineFixtures.Act("e1", ActionType.Attack, "p1"),
            };

            var sorted = Pipeline.Order(actions, state, new ConstantRolls(10));

            Assert.Equal(ActionType.Group, sorted[0].Type);
            Assert.Equal(ActionType.Attack, sorted[1].Type);
        }
    }
}

public class ResolvePerAttackGROUPRealImplementation
{
    [Fact(DisplayName = "delegates to resolveGroup and returns a new state (not the original reference)")]
    public void New_state()
    {
        var state = PipelineFixtures.State([PipelineFixtures.Make("p1")], [PipelineFixtures.Make("e1")]);

        var result = PipelineFixtures.Resolve(state, PipelineFixtures.Act("p1", ActionType.Group, "e1"), new ConstantRolls(10));

        Assert.NotSame(state, result.State);
    }

    [Fact(DisplayName = "GROUP zeros the leader's energy and applies damage to the target")]
    public void Zeros_energy_and_damages()
    {
        var player = PipelineFixtures.Make("p1") with { Stamina = 80, Energy = 5, MaxEnergy = 5, Power = 50 };
        var enemy = PipelineFixtures.Make("e1") with { Stamina = 90, Power = 50 };
        var state = PipelineFixtures.State([player], [enemy]);

        var result = PipelineFixtures.Resolve(state, PipelineFixtures.Act("p1", ActionType.Group, "e1"), new ConstantRolls(15));

        Assert.Equal(0, result.State.PlayerParty[0].Energy);
        Assert.True(result.State.EnemyParty[0].Stamina < 90);
    }
}

public class ResolvePerAttackDEFENDIntercept
{
    [Fact(DisplayName = "redirects an attack to the DEFEND-er when one declares DEFEND on the target")]
    public void Redirects()
    {
        var attacker = PipelineFixtures.Make("attacker") with { Power = 100 };
        var state = PipelineFixtures.State(
            [attacker],
            [PipelineFixtures.Make("target"), PipelineFixtures.Make("defender")],
            PipelineFixtures.Act("defender", ActionType.Defend, "target"));

        var result = PipelineFixtures.Resolve(state, PipelineFixtures.Act("attacker", ActionType.Attack, "target"), new ConstantRolls(20));

        Assert.Equal(100, PipelineFixtures.Find(result.State, "target").Stamina);
        Assert.True(PipelineFixtures.Find(result.State, "defender").Stamina < 100);
    }

    [Fact(DisplayName = "does NOT redirect when DEFEND targets a different ally")]
    public void No_redirect_for_other_ally()
    {
        var attacker = PipelineFixtures.Make("attacker") with { Power = 100 };
        var state = PipelineFixtures.State(
            [attacker],
            [PipelineFixtures.Make("target"), PipelineFixtures.Make("other"), PipelineFixtures.Make("defender")],
            PipelineFixtures.Act("defender", ActionType.Defend, "other"));

        var result = PipelineFixtures.Resolve(state, PipelineFixtures.Act("attacker", ActionType.Attack, "target"), new ConstantRolls(20));

        Assert.True(PipelineFixtures.Find(result.State, "target").Stamina < 100);
        Assert.Equal(100, PipelineFixtures.Find(result.State, "defender").Stamina);
    }

    [Fact(DisplayName = "redirects attack even when DEFEND comes from the queue after the attack action")]
    public void Redirects_with_later_defend()
    {
        var attacker = PipelineFixtures.Make("attacker") with { Power = 100 };
        var state = PipelineFixtures.State(
            [attacker],
            [PipelineFixtures.Make("target"), PipelineFixtures.Make("defender")],
            PipelineFixtures.Act("attacker", ActionType.Attack, "target"),
            PipelineFixtures.Act("defender", ActionType.Defend, "target"));

        var result = PipelineFixtures.Resolve(state, PipelineFixtures.Act("attacker", ActionType.Attack, "target"), new ConstantRolls(20));

        Assert.Equal(100, PipelineFixtures.Find(result.State, "target").Stamina);
        Assert.True(PipelineFixtures.Find(result.State, "defender").Stamina < 100);
    }

    // New (T7): a knocked-out guard does not intercept.
    [Fact(DisplayName = "a knocked-out DEFEND-er does not intercept; the target takes the hit")]
    public void Ko_interceptor_skipped()
    {
        var attacker = PipelineFixtures.Make("attacker") with { Power = 100 };
        var koDefender = PipelineFixtures.Make("defender") with { Stamina = 0, IsKO = true };
        var state = PipelineFixtures.State(
            [attacker],
            [PipelineFixtures.Make("target"), koDefender],
            PipelineFixtures.Act("defender", ActionType.Defend, "target"));

        var result = PipelineFixtures.Resolve(state, PipelineFixtures.Act("attacker", ActionType.Attack, "target"), new ConstantRolls(20));

        Assert.Equal("target", result.Result.AttackResult!.TargetId);
        Assert.True(PipelineFixtures.Find(result.State, "target").Stamina < 100);
        Assert.Equal(0, PipelineFixtures.Find(result.State, "defender").Stamina);
    }

    [Fact(DisplayName = "a knocked-out DEFEND-er falls through to the next DEFEND-er in queue order")]
    public void Ko_interceptor_falls_through()
    {
        var attacker = PipelineFixtures.Make("attacker") with { Power = 100 };
        var koDefender = PipelineFixtures.Make("defender1") with { Stamina = 0, IsKO = true };
        var state = PipelineFixtures.State(
            [attacker],
            [PipelineFixtures.Make("target"), koDefender, PipelineFixtures.Make("defender2")],
            PipelineFixtures.Act("defender1", ActionType.Defend, "target"),
            PipelineFixtures.Act("defender2", ActionType.Defend, "target"));

        var result = PipelineFixtures.Resolve(state, PipelineFixtures.Act("attacker", ActionType.Attack, "target"), new ConstantRolls(20));

        Assert.Equal("defender2", result.Result.AttackResult!.TargetId);
        Assert.Equal(100, PipelineFixtures.Find(result.State, "target").Stamina);
        Assert.True(PipelineFixtures.Find(result.State, "defender2").Stamina < 100);
    }

    [Fact(DisplayName = "the first standing DEFEND-er in queue order takes the hit")]
    public void First_in_queue_order()
    {
        var attacker = PipelineFixtures.Make("attacker") with { Power = 100 };
        var state = PipelineFixtures.State(
            [attacker],
            [PipelineFixtures.Make("target"), PipelineFixtures.Make("defender1"), PipelineFixtures.Make("defender2")],
            PipelineFixtures.Act("defender1", ActionType.Defend, "target"),
            PipelineFixtures.Act("defender2", ActionType.Defend, "target"));

        var result = PipelineFixtures.Resolve(state, PipelineFixtures.Act("attacker", ActionType.Attack, "target"), new ConstantRolls(20));

        Assert.Equal("defender1", result.Result.AttackResult!.TargetId);
    }
}

public class ResolvePerAttackATTACKFullResolution
{
    private static Combatant EarthTarget() => PipelineFixtures.Make("target") with { ElementalPath = ElementalPath.Earth };

    [Fact(DisplayName = "Scenario 1: standard ATTACK with Block success reduces target stamina by mitigation")]
    public void Scenario_1()
    {
        var state = PipelineFixtures.State([PipelineFixtures.Make("attacker")], [EarthTarget()]);

        var result = PipelineFixtures.Resolve(state, PipelineFixtures.Act("attacker", ActionType.Attack, "target"), new FixedRolls(5));

        var targetAfter = PipelineFixtures.Find(result.State, "target");
        Assert.Equal(75, targetAfter.Stamina, 2);
        Assert.False(targetAfter.IsKO);
    }

    [Fact(DisplayName = "Scenario 2: standard ATTACK with Block failure applies FMR damage")]
    public void Scenario_2()
    {
        var state = PipelineFixtures.State([PipelineFixtures.Make("attacker")], [EarthTarget()]);

        var result = PipelineFixtures.Resolve(state, PipelineFixtures.Act("attacker", ActionType.Attack, "target"), new ConstantRolls(20));

        Assert.Equal(60, PipelineFixtures.Find(result.State, "target").Stamina, 2);
    }

    [Fact(DisplayName = "KO occurs when accumulated damage depletes target stamina")]
    public void Ko_from_damage()
    {
        var attacker = PipelineFixtures.Make("attacker") with { Power = 200 };
        var state = PipelineFixtures.State([attacker], [PipelineFixtures.Make("target")]);

        var result = PipelineFixtures.Resolve(state, PipelineFixtures.Act("attacker", ActionType.Attack, "target"), new ConstantRolls(20));

        var targetAfter = PipelineFixtures.Find(result.State, "target");
        Assert.Equal(0, targetAfter.Stamina);
        Assert.True(targetAfter.IsKO);
    }

    [Fact(DisplayName = "attacker gains energy after a successful attack")]
    public void Attacker_gains_energy()
    {
        var state = PipelineFixtures.State([PipelineFixtures.Make("attacker")], [PipelineFixtures.Make("target")]);

        var result = PipelineFixtures.Resolve(state, PipelineFixtures.Act("attacker", ActionType.Attack, "target"), new ConstantRolls(20));

        Assert.True(PipelineFixtures.Find(result.State, "attacker").Energy > 0);
    }

    [Fact(DisplayName = "state is immutable — original state is unchanged")]
    public void Immutable()
    {
        var state = PipelineFixtures.State([PipelineFixtures.Make("attacker")], [PipelineFixtures.Make("target")]);

        PipelineFixtures.Resolve(state, PipelineFixtures.Act("attacker", ActionType.Attack, "target"), new ConstantRolls(20));

        Assert.Equal(100, state.EnemyParty[0].Stamina);
        Assert.Equal(0, state.PlayerParty[0].Energy);
    }
}

public class ResolvePerAttackBlindside
{
    [Fact(DisplayName = "Blindside forces target to be Defenseless (full damage taken)")]
    public void Defenseless()
    {
        var attacker = PipelineFixtures.Make("attacker") with { Speed = 40 };
        var state = PipelineFixtures.State([attacker], [PipelineFixtures.Make("target")]);

        var result = PipelineFixtures.Resolve(state, PipelineFixtures.Act("attacker", ActionType.Attack, "target"), new ConstantRolls(1));

        Assert.Equal(50, PipelineFixtures.Find(result.State, "target").Stamina, 2);
    }
}

public class ResolvePerAttackRankKO
{
    [Fact(DisplayName = "Rank KO forces target to KO (stamina = 0) when threshold triggers")]
    public void Triggers()
    {
        var attacker = PipelineFixtures.Make("attacker") with { Rank = 3.0 };
        var state = PipelineFixtures.State([attacker], [PipelineFixtures.Make("target")]);

        var result = PipelineFixtures.Resolve(state, PipelineFixtures.Act("attacker", ActionType.Attack, "target"), new FixedRolls(10, 20, 20, 20));

        var targetAfter = PipelineFixtures.Find(result.State, "target");
        Assert.True(targetAfter.IsKO);
        Assert.Equal(0, targetAfter.Stamina);
    }

    [Fact(DisplayName = "Rank KO does NOT trigger when attacker rank is not sufficiently higher")]
    public void Not_eligible()
    {
        var state = PipelineFixtures.State([PipelineFixtures.Make("attacker")], [PipelineFixtures.Make("target")]);

        var result = PipelineFixtures.Resolve(state, PipelineFixtures.Act("attacker", ActionType.Attack, "target"), new ConstantRolls(1));

        Assert.False(PipelineFixtures.Find(result.State, "target").IsKO);
    }
}

public class ResolvePerAttackSPECIALResolution
{
    [Fact(DisplayName = "SPECIAL damage includes energy segment bonus")]
    public void Energy_bonus()
    {
        var attacker = PipelineFixtures.Make("attacker") with { Energy = 3, ElementalPath = ElementalPath.Earth };
        var state = PipelineFixtures.State([attacker], [PipelineFixtures.Make("target")]);

        var result = PipelineFixtures.Resolve(state, PipelineFixtures.Act("attacker", ActionType.Special, "target", 3), new ConstantRolls(20));

        Assert.Equal(48, PipelineFixtures.Find(result.State, "target").Stamina, 2);
    }

    [Fact(DisplayName = "SPECIAL consumes attacker energy segments")]
    public void Consumes_energy()
    {
        var attacker = PipelineFixtures.Make("attacker") with { Energy = 3 };
        var state = PipelineFixtures.State([attacker], [PipelineFixtures.Make("target")]);

        var result = PipelineFixtures.Resolve(state, PipelineFixtures.Act("attacker", ActionType.Special, "target", 3), new ConstantRolls(20));

        Assert.True(PipelineFixtures.Find(result.State, "attacker").Energy < 3);
    }

    [Fact(DisplayName = "SPECIAL rounds Action Power once (MROUND of boosted power), per Math!O")]
    public void Rounds_once()
    {
        var attacker = PipelineFixtures.Make("attacker") with { Power = 53, Energy = 1, ElementalPath = ElementalPath.Earth };
        var target = PipelineFixtures.Make("target") with
        {
            Power = 200,
            Stamina = 200,
            MaxStamina = 200,
            ReactionSkills = new ReactionSkills(new DefenseSkill(0.6, 0.5, 0), new DefenseSkill(0.5, 0.8, 0.15), new DefenseSkill(0.4, 0.9, 0.1)),
        };
        var state = PipelineFixtures.State([attacker], [target]);

        var result = PipelineFixtures.Resolve(state, PipelineFixtures.Act("attacker", ActionType.Special, "target", 1), new ConstantRolls(20));

        Assert.Equal(141.75, PipelineFixtures.Find(result.State, "target").Stamina, 2);
    }
}

public class ResolvePerAttackEVADEResolution
{
    [Fact(DisplayName = "EVADE restores 30% of maxStamina")]
    public void Restores_thirty_percent()
    {
        var state = PipelineFixtures.State([PipelineFixtures.Make("c1") with { Stamina = 50 }], [PipelineFixtures.Make("e1")]);

        var result = PipelineFixtures.Resolve(state, PipelineFixtures.Act("c1", ActionType.Evade, null), new ConstantRolls(10));

        Assert.Equal(80, PipelineFixtures.Find(result.State, "c1").Stamina, 2);
    }

    [Fact(DisplayName = "EVADE does not exceed maxStamina")]
    public void Capped()
    {
        var state = PipelineFixtures.State([PipelineFixtures.Make("c1") with { Stamina = 90 }], [PipelineFixtures.Make("e1")]);

        var result = PipelineFixtures.Resolve(state, PipelineFixtures.Act("c1", ActionType.Evade, null), new ConstantRolls(10));

        Assert.Equal(100, PipelineFixtures.Find(result.State, "c1").Stamina);
    }

    [Fact(DisplayName = "EVADE grants energy to the evader")]
    public void Grants_energy()
    {
        var state = PipelineFixtures.State([PipelineFixtures.Make("c1") with { Stamina = 50 }], [PipelineFixtures.Make("e1")]);

        var result = PipelineFixtures.Resolve(state, PipelineFixtures.Act("c1", ActionType.Evade, null), new ConstantRolls(10));

        Assert.True(PipelineFixtures.Find(result.State, "c1").Energy > 0);
    }

    // New (T7): the result records how much stamina an Evade actually restored.
    [Fact(DisplayName = "EVADE records the stamina it restored")]
    public void Records_recovery()
    {
        var state = PipelineFixtures.State([PipelineFixtures.Make("c1") with { Stamina = 50 }], [PipelineFixtures.Make("e1")]);

        var result = PipelineFixtures.Resolve(state, PipelineFixtures.Act("c1", ActionType.Evade, null), new FixedRolls());

        Assert.Equal(30, result.Result.EvadeRecovery!.Value, 5);
    }

    [Fact(DisplayName = "EVADE records the recovery after the cap at max stamina")]
    public void Records_capped_recovery()
    {
        var state = PipelineFixtures.State([PipelineFixtures.Make("c1") with { Stamina = 90 }], [PipelineFixtures.Make("e1")]);

        var result = PipelineFixtures.Resolve(state, PipelineFixtures.Act("c1", ActionType.Evade, null), new FixedRolls());

        Assert.Equal(10, result.Result.EvadeRecovery!.Value, 5);
    }

    [Fact(DisplayName = "actions other than EVADE record no Evade recovery")]
    public void Others_record_none()
    {
        var state = PipelineFixtures.State([PipelineFixtures.Make("a")], [PipelineFixtures.Make("b")]);

        var result = PipelineFixtures.Resolve(state, PipelineFixtures.Act("a", ActionType.Attack, "b"), new ConstantRolls(10));

        Assert.Null(result.Result.EvadeRecovery);
    }
}

public class ResolvePerAttackCounterChain
{
    [Fact(DisplayName = "successful Parry triggers a counter chain")]
    public void Triggers_chain()
    {
        var attacker = PipelineFixtures.Make("attacker") with { Energy = 2, ReactionSkills = PipelineFixtures.LowParry };
        var target = PipelineFixtures.Make("target") with { ReactionSkills = PipelineFixtures.HighParry };
        var state = PipelineFixtures.State([attacker], [target]);

        var result = PipelineFixtures.Resolve(state, PipelineFixtures.Act("attacker", ActionType.Special, "target", 2), new FixedRolls(1, 19));

        var specialActionPower = 50 * (1 + 0.1 * 2);
        var expectedParryDamage = specialActionPower * (1 - PipelineFixtures.HighParry.Parry.SMR);
        Assert.Equal(100 - expectedParryDamage, PipelineFixtures.Find(result.State, "target").Stamina, 5);
        Assert.True(PipelineFixtures.Find(result.State, "attacker").Stamina < 100);
    }

    // New (T7): every counter-attack is recorded on the result.
    [Fact(DisplayName = "the counter-attack exchange is recorded on the action result")]
    public void Records_exchange()
    {
        var attacker = PipelineFixtures.Make("attacker") with { Energy = 2, ReactionSkills = PipelineFixtures.LowParry };
        var target = PipelineFixtures.Make("target") with { ReactionSkills = PipelineFixtures.HighParry };
        var state = PipelineFixtures.State([attacker], [target]);

        var result = PipelineFixtures.Resolve(state, PipelineFixtures.Act("attacker", ActionType.Special, "target", 2), new FixedRolls(1, 19));

        Assert.True(result.Result.AttackResult!.CounterChain);
        var exchange = Assert.Single(result.Result.CounterExchanges);
        Assert.Equal("target", exchange.AttackerId);
        Assert.Equal("attacker", exchange.DefenderId);
        Assert.Equal(DefenseType.Parry, exchange.Defense);
        Assert.False(exchange.Outcome.Success);
        Assert.Equal(50 * (1 - PipelineFixtures.LowParry.Parry.FMR), exchange.Damage, 5);
    }

    [Fact(DisplayName = "an attack that sets off no counter chain records no exchanges")]
    public void No_exchanges()
    {
        var state = PipelineFixtures.State([PipelineFixtures.Make("a")], [PipelineFixtures.Make("b")]);

        var result = PipelineFixtures.Resolve(state, PipelineFixtures.Act("a", ActionType.Attack, "b"), new ConstantRolls(20));

        Assert.False(result.Result.AttackResult!.CounterChain);
        Assert.Empty(result.Result.CounterExchanges);
    }
}

public class SortByPriorityImmutability
{
    [Fact(DisplayName = "returns a new array without mutating the input")]
    public void No_mutation()
    {
        var state = PipelineFixtures.State([PipelineFixtures.Make("p1")], [PipelineFixtures.Make("e1")]);
        var actions = new[]
        {
            PipelineFixtures.Act("p1", ActionType.Evade, null),
            PipelineFixtures.Act("e1", ActionType.Attack, "p1"),
        };
        var originalFirst = actions[0];

        var sorted = Pipeline.Order(actions, state, new ConstantRolls(10));

        Assert.Same(originalFirst, actions[0]);
        Assert.Equal(ActionType.Attack, sorted[0].Type);
    }
}

public class ResolveActionAccurateActionResult
{
    [Fact(DisplayName = "reports the real (non-block) defense type for a SPECIAL forced defense")]
    public void Real_defense_type()
    {
        var attacker = PipelineFixtures.Make("p1") with { ElementalPath = ElementalPath.Water, Energy = 3 };
        var state = PipelineFixtures.State([attacker], [PipelineFixtures.Make("e1")]);

        var result = PipelineFixtures.Resolve(state, PipelineFixtures.Act("p1", ActionType.Special, "e1", 2), new ConstantRolls(10)).Result;

        Assert.Equal(ActionType.Special, result.Type);
        Assert.Equal(DefenseType.Dodge, result.AttackResult?.DefenseType);
    }

    [Fact(DisplayName = "reports EVADE as a non-attack action result")]
    public void Evade_non_attack()
    {
        var state = PipelineFixtures.State([PipelineFixtures.Make("p1") with { Stamina = 50 }], [PipelineFixtures.Make("e1")]);

        var result = PipelineFixtures.Resolve(state, PipelineFixtures.Act("p1", ActionType.Evade, null), new FixedRolls()).Result;

        Assert.Equal(ActionType.Evade, result.Type);
        Assert.Null(result.AttackResult);
    }
}

public class ResolveActionCrushingBlowEffect
{
    [Fact(DisplayName = "degrades the target Block SR/SMR/FMR by 0.1 when Crushing Blow lands")]
    public void Degrades_block()
    {
        var attacker = PipelineFixtures.Make("p1") with { Power = 80 };
        var target = PipelineFixtures.Make("e1") with { Power = 40, ElementalPath = ElementalPath.Earth };
        var state = PipelineFixtures.State([attacker], [target]);

        var resolution = PipelineFixtures.Resolve(state, PipelineFixtures.Act("p1", ActionType.Attack, "e1"), new FixedRolls(10, 10));
        var newTarget = PipelineFixtures.Find(resolution.State, "e1");

        Assert.True(resolution.Result.AttackResult?.CrushingBlow);
        Assert.Equal(0.5, newTarget.ReactionSkills.Block.SR, 2);
        Assert.Equal(0.4, newTarget.ReactionSkills.Block.SMR, 2);
        Assert.Equal(0.1, newTarget.ReactionSkills.Block.FMR, 2);
    }

    [Fact(DisplayName = "does not change Block rates when powers are equal (no Crushing Blow)")]
    public void Equal_powers()
    {
        var state = PipelineFixtures.State([PipelineFixtures.Make("p1")], [PipelineFixtures.Make("e1")]);

        var resolution = PipelineFixtures.Resolve(state, PipelineFixtures.Act("p1", ActionType.Attack, "e1"), new ConstantRolls(10));
        var newTarget = PipelineFixtures.Find(resolution.State, "e1");

        Assert.False(resolution.Result.AttackResult?.CrushingBlow);
        Assert.Equal(0.6, newTarget.ReactionSkills.Block.SR, 2);
        Assert.Equal(0.5, newTarget.ReactionSkills.Block.SMR, 2);
        Assert.Equal(0.2, newTarget.ReactionSkills.Block.FMR, 2);
    }

    [Fact(DisplayName = "clamps debuffed Block rates at 0 (never negative)")]
    public void Clamps_at_zero()
    {
        var attacker = PipelineFixtures.Make("p1") with { Power = 80 };
        var target = PipelineFixtures.Make("e1") with
        {
            Power = 40,
            ElementalPath = ElementalPath.Earth,
            ReactionSkills = new ReactionSkills(new DefenseSkill(0.05, 0.05, 0.0), new DefenseSkill(0.1, 0.8, 0.05), new DefenseSkill(0.1, 0.9, 0.05)),
        };
        var state = PipelineFixtures.State([attacker], [target]);

        var resolution = PipelineFixtures.Resolve(state, PipelineFixtures.Act("p1", ActionType.Attack, "e1"), new FixedRolls(10, 10));
        var newTarget = PipelineFixtures.Find(resolution.State, "e1");

        Assert.Equal(0, newTarget.ReactionSkills.Block.SR);
        Assert.Equal(0, newTarget.ReactionSkills.Block.SMR);
        Assert.Equal(0, newTarget.ReactionSkills.Block.FMR);
    }

    // New (T7): Crushing Blow compares the attack's action power (Special-boosted, rounded to 0.25) with the target's Power.
    [Fact(DisplayName = "a Special's boosted action power makes Crushing Blow eligible against an equal-power target")]
    public void Special_power_eligible()
    {
        // Base power 50 equals the target's 50 (ineligible before); 3 segments make the action power 65.
        var attacker = PipelineFixtures.Make("p1") with { Energy = 3, ElementalPath = ElementalPath.Earth };
        var state = PipelineFixtures.State([attacker], [PipelineFixtures.Make("e1")]);
        var rolls = new FixedRolls(20, 20);

        var resolution = PipelineFixtures.Resolve(state, PipelineFixtures.Act("p1", ActionType.Special, "e1", 3), rolls);

        Assert.Equal(2, rolls.Consumed);
        Assert.True(resolution.Result.AttackResult?.CrushingBlow);
        Assert.Equal(0.5, PipelineFixtures.Find(resolution.State, "e1").ReactionSkills.Block.SR, 2);
    }

    [Fact(DisplayName = "Crushing Blow threshold uses the action power, not the attacker's base power")]
    public void Threshold_uses_action_power()
    {
        // Action power 65 vs target power 60: threshold 5/60, lands only on a roll of at least 18.33. A base power of 50 would be ineligible.
        var attacker = PipelineFixtures.Make("p1") with { Energy = 3, ElementalPath = ElementalPath.Earth };
        var target = PipelineFixtures.Make("e1") with { Power = 60 };
        var state = PipelineFixtures.State([attacker], [target]);

        var low = PipelineFixtures.Resolve(state, PipelineFixtures.Act("p1", ActionType.Special, "e1", 3), new FixedRolls(20, 10));
        var high = PipelineFixtures.Resolve(state, PipelineFixtures.Act("p1", ActionType.Special, "e1", 3), new FixedRolls(20, 19));

        Assert.False(low.Result.AttackResult?.CrushingBlow);
        Assert.True(high.Result.AttackResult?.CrushingBlow);
    }

    [Fact(DisplayName = "a plain attack's action power is its Power, so equal powers draw no Crushing Blow roll")]
    public void Plain_attack_no_roll()
    {
        var state = PipelineFixtures.State([PipelineFixtures.Make("p1")], [PipelineFixtures.Make("e1") with { ElementalPath = ElementalPath.Earth }]);
        var rolls = new FixedRolls(20);

        PipelineFixtures.Resolve(state, PipelineFixtures.Act("p1", ActionType.Attack, "e1"), rolls);

        Assert.Equal(1, rolls.Consumed);
    }
}

public class ResolveActionReactionXP
{
    private static Combatant EarthDefender() => PipelineFixtures.Make("p1") with
    {
        ElementalPath = ElementalPath.Earth,
        ReactionProgress = new ReactionProgress(0, 0, 0),
    };

    [Fact(DisplayName = "awards +4 to a player defender on a successful reaction")]
    public void Plus_four()
    {
        var state = PipelineFixtures.State([EarthDefender()], [PipelineFixtures.Make("e1")]);

        var resolution = PipelineFixtures.Resolve(state, PipelineFixtures.Act("e1", ActionType.Attack, "p1"), new ConstantRolls(10));

        Assert.True(resolution.Result.AttackResult?.DefenseOutcome.Success);
        Assert.Equal(new ReactionProgress(4, 0, 0), PipelineFixtures.Find(resolution.State, "p1").ReactionProgress);
    }

    [Fact(DisplayName = "awards +2 to a player defender on a failed reaction")]
    public void Plus_two()
    {
        var state = PipelineFixtures.State([EarthDefender()], [PipelineFixtures.Make("e1")]);

        var resolution = PipelineFixtures.Resolve(state, PipelineFixtures.Act("e1", ActionType.Attack, "p1"), new ConstantRolls(19));

        Assert.False(resolution.Result.AttackResult?.DefenseOutcome.Success);
        Assert.Equal(new ReactionProgress(2, 0, 0), PipelineFixtures.Find(resolution.State, "p1").ReactionProgress);
    }

    [Fact(DisplayName = "never accrues XP for an enemy defender (no reactionProgress)")]
    public void Enemy_no_xp()
    {
        var defender = PipelineFixtures.Make("e1") with { ElementalPath = ElementalPath.Earth };
        var state = PipelineFixtures.State([PipelineFixtures.Make("p1")], [defender]);

        var resolution = PipelineFixtures.Resolve(state, PipelineFixtures.Act("p1", ActionType.Attack, "e1"), new ConstantRolls(10));

        Assert.Null(PipelineFixtures.Find(resolution.State, "e1").ReactionProgress);
    }

    [Fact(DisplayName = "awards no XP to a Blindsided (defenseless) player defender")]
    public void Blindsided_no_xp()
    {
        var attacker = PipelineFixtures.Make("e1") with { Speed = 20 };
        var defender = EarthDefender() with { Speed = 1 };
        var state = PipelineFixtures.State([defender], [attacker]);

        var resolution = PipelineFixtures.Resolve(state, PipelineFixtures.Act("e1", ActionType.Attack, "p1"), new ConstantRolls(1));

        Assert.True(resolution.Result.AttackResult?.Blindside);
        Assert.Equal(new ReactionProgress(0, 0, 0), PipelineFixtures.Find(resolution.State, "p1").ReactionProgress);
    }
}

// New (T7): speed ties are broken by one roll per tied action, in queue order, highest first; equal rolls keep queue order.
public class OrderSpeedTieRule
{
    private static CombatState Tied() => PipelineFixtures.State(
        [PipelineFixtures.Make("a"), PipelineFixtures.Make("b"), PipelineFixtures.Make("c")],
        [PipelineFixtures.Make("x")]);

    private static CombatAction[] ThreeAttacks() =>
    [
        PipelineFixtures.Act("a", ActionType.Attack, "x"),
        PipelineFixtures.Act("b", ActionType.Attack, "x"),
        PipelineFixtures.Act("c", ActionType.Attack, "x"),
    ];

    [Fact(DisplayName = "tied actions each draw one roll in queue order and the highest roll resolves first")]
    public void Highest_roll_first()
    {
        var rolls = new FixedRolls(5, 15, 10);

        var sorted = Pipeline.Order(ThreeAttacks(), Tied(), rolls);

        Assert.Equal(["b", "c", "a"], PipelineFixtures.Ids(sorted));
        Assert.Equal(3, rolls.Consumed);
    }

    [Fact(DisplayName = "equal rolls keep queue order")]
    public void Equal_rolls_keep_order()
    {
        var sorted = Pipeline.Order(ThreeAttacks(), Tied(), new ConstantRolls(10));

        Assert.Equal(["a", "b", "c"], PipelineFixtures.Ids(sorted));
    }

    [Fact(DisplayName = "the same rolls always give the same order")]
    public void Same_rolls_same_order()
    {
        var first = Pipeline.Order(ThreeAttacks(), Tied(), new FixedRolls(3, 18, 11));
        var second = Pipeline.Order(ThreeAttacks(), Tied(), new FixedRolls(3, 18, 11));

        Assert.Equal(PipelineFixtures.Ids(first), PipelineFixtures.Ids(second));
        Assert.Equal(["b", "c", "a"], PipelineFixtures.Ids(first));
    }

    [Fact(DisplayName = "an action with no tie draws no roll")]
    public void Untied_draws_nothing()
    {
        var state = PipelineFixtures.State(
            [PipelineFixtures.Make("a") with { Speed = 30 }, PipelineFixtures.Make("b"), PipelineFixtures.Make("c")],
            [PipelineFixtures.Make("x")]);
        var rolls = new FixedRolls(1, 2);

        var sorted = Pipeline.Order(ThreeAttacks(), state, rolls);

        Assert.Equal(2, rolls.Consumed);
        Assert.Equal("a", sorted[0].CombatantId);
        Assert.Equal(["c", "b"], PipelineFixtures.Ids(sorted).Skip(1));
    }

    [Fact(DisplayName = "no tie draws no rolls at all")]
    public void No_ties_no_rolls()
    {
        var state = PipelineFixtures.State(
            [PipelineFixtures.Make("a") with { Speed = 30 }, PipelineFixtures.Make("b") with { Speed = 20 }, PipelineFixtures.Make("c")],
            [PipelineFixtures.Make("x")]);
        var rolls = new FixedRolls();

        var sorted = Pipeline.Order(ThreeAttacks(), state, rolls);

        Assert.Equal(["a", "b", "c"], PipelineFixtures.Ids(sorted));
    }

    [Fact(DisplayName = "same speed in different priority brackets is not a tie")]
    public void Different_priority_not_a_tie()
    {
        var state = PipelineFixtures.State([PipelineFixtures.Make("a"), PipelineFixtures.Make("b")], [PipelineFixtures.Make("x")]);
        var actions = new[]
        {
            PipelineFixtures.Act("a", ActionType.Evade, null),
            PipelineFixtures.Act("b", ActionType.Attack, "x"),
        };
        var rolls = new FixedRolls();

        var sorted = Pipeline.Order(actions, state, rolls);

        Assert.Equal(["b", "a"], PipelineFixtures.Ids(sorted));
    }

    [Fact(DisplayName = "a tie inside one priority bracket does not disturb the order between brackets")]
    public void Ties_stay_inside_bracket()
    {
        var state = PipelineFixtures.State(
            [PipelineFixtures.Make("a"), PipelineFixtures.Make("b"), PipelineFixtures.Make("c")],
            [PipelineFixtures.Make("x")]);
        var actions = new[]
        {
            PipelineFixtures.Act("a", ActionType.Evade, null),
            PipelineFixtures.Act("b", ActionType.Attack, "x"),
            PipelineFixtures.Act("c", ActionType.Attack, "x"),
        };

        var sorted = Pipeline.Order(actions, state, new FixedRolls(1, 2));

        Assert.Equal(["c", "b", "a"], PipelineFixtures.Ids(sorted));
    }
}
