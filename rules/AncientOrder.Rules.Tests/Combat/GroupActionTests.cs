using AncientOrder.Rules.Combat;
using AncientOrder.Rules.Tests.Fixtures;
using AncientOrder.Rules.Tests.Support;

namespace AncientOrder.Rules.Tests.Combat;

// Carried over from src/combat/groupAction.test.ts. Changed: resolveGroup returned a CombatState, GroupAction.Resolve returns an
// ActionResolution (State + Result); calculateBaseDamage(power, enemyPower) is CalculateBaseDamage(power) (the second argument
// never affected it); sortByPriority is Pipeline.Order. Constant rolls are ConstantRolls. The last block is new: buffed block rates (T7).
internal static class GroupFixtures
{
    public static readonly ReactionSkills BaseSkills = new(
        new DefenseSkill(0.5, 0.4, 0.1),
        new DefenseSkill(0.5, 0.8, 0.1),
        new DefenseSkill(0.5, 0.9, 0.1));

    public static Combatant Make(string id, double power = 50) => new(
        id,
        id,
        "warrior",
        2.0,
        100,
        100,
        power,
        30,
        5,
        5,
        AscensionLevel.Level0,
        [],
        ElementalPath.Fire,
        BaseSkills,
        null,
        false);

    public static Combatant Leader { get; } = Make("leader", 60);

    public static Combatant Ally1 { get; } = Make("ally1", 50);

    public static Combatant Ally2 { get; } = Make("ally2", 40);

    public static Combatant Enemy { get; } = Make("enemy1", 55) with { Stamina = 300, MaxStamina = 300 };

    public static CombatState State(Combatant[] players, Combatant[] enemies) =>
        new(
            1,
            CombatPhase.ActionResolution,
            [.. players],
            [.. enemies],
            [],
            [],
            CombatStatus.Active,
            AncientOrder.Rules.Common.DiceState.FromSeed(1));

    public static CombatState BaseState { get; } = State([Leader, Ally1, Ally2], [Enemy]);

    public static GroupActionDeclaration Declaration { get; } = new("leader", "enemy1");

    public static CombatState Resolve(CombatState state, double roll, GroupActionConfig? config = null) =>
        GroupAction.Resolve(state, Declaration, config ?? GroupAction.DefaultConfig, new ConstantRolls(roll)).State;

    public static Combatant Find(CombatState state, string id) =>
        state.PlayerParty.Concat(state.EnemyParty).Single(c => c.Id == id);

    public static double DamageDealt(CombatState after) => Enemy.Stamina - Find(after, "enemy1").Stamina;

    public static double Base(Combatant c) => Formulas.CalculateBaseDamage(c.Power);
}

public class GroupDeclarationValidation
{
    [Fact(DisplayName = "accepts GROUP when all non-KO'd members have full energy")]
    public void Accepts_full_energy()
    {
        var action = new CombatAction("leader", ActionType.Group, "enemy1", null);
        Assert.True(Declarations.Validate(GroupFixtures.BaseState, action).Valid);
    }

    [Fact(DisplayName = "rejects GROUP when the leader lacks full energy")]
    public void Rejects_leader_low()
    {
        var state = GroupFixtures.State([GroupFixtures.Leader with { Energy = 3 }, GroupFixtures.Ally1, GroupFixtures.Ally2], [GroupFixtures.Enemy]);
        var result = Declarations.Validate(state, new CombatAction("leader", ActionType.Group, "enemy1", null));
        Assert.False(result.Valid);
        Assert.Contains("full energy", result.Error, StringComparison.Ordinal);
        Assert.Equal(new CombatAction("leader", ActionType.Attack, "enemy1", null), result.Fallback);
    }

    [Fact(DisplayName = "rejects GROUP when ally1 lacks full energy")]
    public void Rejects_ally1_low()
    {
        var state = GroupFixtures.State([GroupFixtures.Leader, GroupFixtures.Ally1 with { Energy = 2 }, GroupFixtures.Ally2], [GroupFixtures.Enemy]);
        var result = Declarations.Validate(state, new CombatAction("leader", ActionType.Group, "enemy1", null));
        Assert.False(result.Valid);
        Assert.Contains("ally1", result.Error, StringComparison.Ordinal);
        Assert.Equal(ActionType.Attack, result.Fallback?.Type);
    }

    [Fact(DisplayName = "rejects GROUP when ally2 lacks full energy")]
    public void Rejects_ally2_low()
    {
        var state = GroupFixtures.State([GroupFixtures.Leader, GroupFixtures.Ally1, GroupFixtures.Ally2 with { Energy = 0 }], [GroupFixtures.Enemy]);
        var result = Declarations.Validate(state, new CombatAction("leader", ActionType.Group, "enemy1", null));
        Assert.False(result.Valid);
        Assert.Equal(ActionType.Attack, result.Fallback?.Type);
    }

    [Fact(DisplayName = "accepts GROUP when KO'd ally has no energy (KO'd are excluded from energy check)")]
    public void Accepts_ko_ally()
    {
        var state = GroupFixtures.State([GroupFixtures.Leader, GroupFixtures.Ally1, GroupFixtures.Ally2 with { IsKO = true, Energy = 0 }], [GroupFixtures.Enemy]);
        Assert.True(Declarations.Validate(state, new CombatAction("leader", ActionType.Group, "enemy1", null)).Valid);
    }
}

public class GroupWithKODAllyReducedParticipantCount
{
    [Fact(DisplayName = "fires with 2 participants when 1 ally is KO'd")]
    public void Two_participants()
    {
        var state = GroupFixtures.State([GroupFixtures.Leader, GroupFixtures.Ally1, GroupFixtures.Ally2 with { IsKO = true, Stamina = 0 }], [GroupFixtures.Enemy]);

        var after = GroupFixtures.Resolve(state, 15);

        Assert.True(GroupFixtures.Find(after, "ally2").IsKO);
        Assert.Equal(0, GroupFixtures.Find(after, "leader").Energy);
        Assert.Equal(0, GroupFixtures.Find(after, "ally1").Energy);
    }

    [Fact(DisplayName = "damage calculation uses only non-KO'd participants")]
    public void Only_living_participants()
    {
        var state = GroupFixtures.State([GroupFixtures.Leader, GroupFixtures.Ally1, GroupFixtures.Ally2 with { IsKO = true, Stamina = 0 }], [GroupFixtures.Enemy]);

        var totalBeforeMultiplier = GroupFixtures.Base(GroupFixtures.Leader) + GroupFixtures.Base(GroupFixtures.Ally1);
        var groupDamage = totalBeforeMultiplier * 1.5;
        var expectedFinalDamage = groupDamage * (1 - GroupFixtures.Enemy.ReactionSkills.Block.FMR);

        var after = GroupFixtures.Resolve(state, 15);

        Assert.Equal(expectedFinalDamage, GroupFixtures.DamageDealt(after), 5);
    }
}

public class GroupDamageCalculation
{
    private static double SumAll() =>
        GroupFixtures.Base(GroupFixtures.Leader) + GroupFixtures.Base(GroupFixtures.Ally1) + GroupFixtures.Base(GroupFixtures.Ally2);

    [Fact(DisplayName = "applies 1.5x synergy multiplier to the sum of all individual damages")]
    public void Multiplier_applies()
    {
        var after = GroupFixtures.Resolve(GroupFixtures.BaseState, 15);

        var expectedFinal = SumAll() * 1.5 * (1 - GroupFixtures.Enemy.ReactionSkills.Block.FMR);
        Assert.Equal(expectedFinal, GroupFixtures.DamageDealt(after), 5);
    }

    [Fact(DisplayName = "multiplier stays at 1.5x regardless of participant count (solo GROUP)")]
    public void Solo_multiplier()
    {
        var state = GroupFixtures.State(
            [GroupFixtures.Leader, GroupFixtures.Ally1 with { IsKO = true, Stamina = 0 }, GroupFixtures.Ally2 with { IsKO = true, Stamina = 0 }],
            [GroupFixtures.Enemy]);

        var after = GroupFixtures.Resolve(state, 15);

        var expectedFinal = GroupFixtures.Base(GroupFixtures.Leader) * 1.5 * (1 - GroupFixtures.Enemy.ReactionSkills.Block.FMR);
        Assert.Equal(expectedFinal, GroupFixtures.DamageDealt(after), 5);
    }

    [Fact(DisplayName = "uses custom damageMultiplier from config")]
    public void Custom_multiplier()
    {
        var custom = new GroupActionConfig(2.0, GroupEnergyRequirement.Full);

        var after = GroupFixtures.Resolve(GroupFixtures.BaseState, 15, custom);

        var expectedFinal = SumAll() * 2.0 * (1 - GroupFixtures.Enemy.ReactionSkills.Block.FMR);
        Assert.Equal(expectedFinal, GroupFixtures.DamageDealt(after), 5);
    }
}

public class GroupBlockDefense
{
    private static double GroupDamage() =>
        (GroupFixtures.Base(GroupFixtures.Leader) + GroupFixtures.Base(GroupFixtures.Ally1) + GroupFixtures.Base(GroupFixtures.Ally2)) * 1.5;

    [Fact(DisplayName = "applies Block success mitigation (SMR) when roll succeeds")]
    public void Success_mitigation()
    {
        var after = GroupFixtures.Resolve(GroupFixtures.BaseState, 5);

        Assert.Equal(GroupDamage() * (1 - GroupFixtures.Enemy.ReactionSkills.Block.SMR), GroupFixtures.DamageDealt(after), 5);
    }

    [Fact(DisplayName = "applies Block failure mitigation (FMR) when roll fails")]
    public void Failure_mitigation()
    {
        var after = GroupFixtures.Resolve(GroupFixtures.BaseState, 15);

        Assert.Equal(GroupDamage() * (1 - GroupFixtures.Enemy.ReactionSkills.Block.FMR), GroupFixtures.DamageDealt(after), 5);
    }

    [Fact(DisplayName = "only uses Block defense — never dodge or parry (target cannot avoid GROUP)")]
    public void Only_block()
    {
        var after = GroupFixtures.Resolve(GroupFixtures.BaseState, 5);

        Assert.True(GroupFixtures.Find(after, "enemy1").Stamina < GroupFixtures.Enemy.Stamina);
    }
}

public class GroupEnergyConsumption
{
    [Fact(DisplayName = "zeros all non-KO'd participants' energy after GROUP")]
    public void Zeros_all()
    {
        var after = GroupFixtures.Resolve(GroupFixtures.BaseState, 15);

        Assert.Equal(0, GroupFixtures.Find(after, "leader").Energy);
        Assert.Equal(0, GroupFixtures.Find(after, "ally1").Energy);
        Assert.Equal(0, GroupFixtures.Find(after, "ally2").Energy);
    }

    [Fact(DisplayName = "does not zero energy of KO'd allies (they were not participants)")]
    public void Ko_ally_untouched()
    {
        var state = GroupFixtures.State([GroupFixtures.Leader, GroupFixtures.Ally1, GroupFixtures.Ally2 with { IsKO = true, Stamina = 0, Energy = 0 }], [GroupFixtures.Enemy]);

        var after = GroupFixtures.Resolve(state, 15);

        Assert.True(GroupFixtures.Find(after, "ally2").IsKO);
        Assert.Equal(0, GroupFixtures.Find(after, "ally2").Energy);
    }

    [Fact(DisplayName = "zeros energy even when target is already KO'd (energy consumed atomically)")]
    public void Ko_target()
    {
        var state = GroupFixtures.State([GroupFixtures.Leader, GroupFixtures.Ally1, GroupFixtures.Ally2], [GroupFixtures.Enemy with { IsKO = true, Stamina = 0 }]);

        var after = GroupFixtures.Resolve(state, 10);

        Assert.Equal(0, GroupFixtures.Find(after, "leader").Energy);
        Assert.Equal(0, GroupFixtures.Find(after, "ally1").Energy);
        Assert.Equal(0, GroupFixtures.Find(after, "ally2").Energy);
    }
}

public class GroupPrioritySortPlacement
{
    [Fact(DisplayName = "GROUP sorts before DEFEND, ATTACK, and EVADE")]
    public void Group_first()
    {
        var actions = new[]
        {
            new CombatAction("ally1", ActionType.Evade, null, null),
            new CombatAction("enemy1", ActionType.Attack, "leader", null),
            new CombatAction("ally2", ActionType.Defend, "leader", null),
            new CombatAction("leader", ActionType.Group, "enemy1", null),
        };
        var state = GroupFixtures.State([GroupFixtures.Leader, GroupFixtures.Ally1, GroupFixtures.Ally2], [GroupFixtures.Enemy]);

        var sorted = Pipeline.Order(actions, state, new ConstantRolls(10));

        Assert.Equal(ActionType.Group, sorted[0].Type);
        Assert.Equal(ActionType.Defend, sorted[1].Type);
        Assert.Equal(ActionType.Attack, sorted[2].Type);
        Assert.Equal(ActionType.Evade, sorted[3].Type);
    }

    [Fact(DisplayName = "GROUP at priority 0, ATTACK at priority 2, EVADE at priority 3")]
    public void Priorities()
    {
        var actions = new[]
        {
            new CombatAction("enemy1", ActionType.Attack, "leader", null),
            new CombatAction("leader", ActionType.Group, "enemy1", null),
            new CombatAction("ally1", ActionType.Evade, null, null),
        };
        var state = GroupFixtures.State([GroupFixtures.Leader, GroupFixtures.Ally1, GroupFixtures.Ally2], [GroupFixtures.Enemy]);

        var sorted = Pipeline.Order(actions, state, new ConstantRolls(10));

        Assert.Equal("leader", sorted[0].CombatantId);
        Assert.Equal(ActionType.Group, sorted[0].Type);
        Assert.Equal(ActionType.Attack, sorted[1].Type);
        Assert.Equal(ActionType.Evade, sorted[2].Type);
    }
}

public class SoloGROUPLeaderOnly
{
    [Fact(DisplayName = "fires with just the leader when all allies are KO'd")]
    public void Leader_only()
    {
        var state = GroupFixtures.State(
            [GroupFixtures.Leader, GroupFixtures.Ally1 with { IsKO = true, Stamina = 0 }, GroupFixtures.Ally2 with { IsKO = true, Stamina = 0 }],
            [GroupFixtures.Enemy]);

        var after = GroupFixtures.Resolve(state, 15);

        Assert.Equal(0, GroupFixtures.Find(after, "leader").Energy);
        var expectedFinal = GroupFixtures.Base(GroupFixtures.Leader) * 1.5 * (1 - GroupFixtures.Enemy.ReactionSkills.Block.FMR);
        Assert.Equal(expectedFinal, GroupFixtures.DamageDealt(after), 5);
    }
}

public class GroupImmutability
{
    [Fact(DisplayName = "returns a new CombatState object (does not mutate input)")]
    public void New_state()
    {
        var after = GroupFixtures.Resolve(GroupFixtures.BaseState, 15);

        Assert.NotSame(GroupFixtures.BaseState, after);
    }

    [Fact(DisplayName = "original player party is not mutated")]
    public void Party_unchanged()
    {
        var originalLeaderEnergy = GroupFixtures.Leader.Energy;

        GroupFixtures.Resolve(GroupFixtures.BaseState, 15);

        Assert.Equal(originalLeaderEnergy, GroupFixtures.Leader.Energy);
        Assert.Equal(originalLeaderEnergy, GroupFixtures.BaseState.PlayerParty[0].Energy);
    }

    [Fact(DisplayName = "original enemy stamina is not mutated")]
    public void Enemy_unchanged()
    {
        var originalStamina = GroupFixtures.Enemy.Stamina;

        GroupFixtures.Resolve(GroupFixtures.BaseState, 15);

        Assert.Equal(originalStamina, GroupFixtures.Enemy.Stamina);
        Assert.Equal(originalStamina, GroupFixtures.BaseState.EnemyParty[0].Stamina);
    }
}

public class GROUPACTIONCONFIGExportedConstant
{
    [Fact(DisplayName = "has damageMultiplier of 1.5")]
    public void Damage_multiplier()
    {
        Assert.Equal(1.5, GroupAction.DefaultConfig.DamageMultiplier);
    }

    [Fact(DisplayName = "has energyRequirement of \"full\"")]
    public void Energy_requirement()
    {
        Assert.Equal(GroupEnergyRequirement.Full, GroupAction.DefaultConfig.EnergyRequirement);
    }
}

// New (T7): GROUP blocks use the target's buffed rates.
public class GroupBuffedBlockRates
{
    private static CombatState WithTargetBuffs(params Buff[] buffs) =>
        GroupFixtures.State(
            [GroupFixtures.Leader, GroupFixtures.Ally1, GroupFixtures.Ally2],
            [GroupFixtures.Enemy with { ActiveBuffs = [.. buffs] }]);

    private static double GroupDamage() =>
        (GroupFixtures.Base(GroupFixtures.Leader) + GroupFixtures.Base(GroupFixtures.Ally1) + GroupFixtures.Base(GroupFixtures.Ally2)) * 1.5;

    [Fact(DisplayName = "a block success rate buff turns a failing roll into a success")]
    public void Sr_buff_flips_roll()
    {
        // Base block SR 0.5 (roll 15 > 10 fails); +0.4 makes it 0.9 (roll 15 <= 18 succeeds).
        var state = WithTargetBuffs(new Buff(BuffStat.BlockSR, "test", 0.4));
        var resolution = GroupAction.Resolve(state, GroupFixtures.Declaration, GroupAction.DefaultConfig, new ConstantRolls(15));

        Assert.True(resolution.Result.AttackResult!.DefenseOutcome.Success);
        Assert.Equal(GroupDamage() * (1 - GroupFixtures.Enemy.ReactionSkills.Block.SMR), GroupFixtures.DamageDealt(resolution.State), 5);
    }

    [Fact(DisplayName = "a block success rate debuff turns a succeeding roll into a failure")]
    public void Sr_debuff_flips_roll()
    {
        // Base block SR 0.5 (roll 8 succeeds); -0.2 makes it 0.3 (roll 8 > 6 fails).
        var state = WithTargetBuffs(new Buff(BuffStat.BlockSR, "test", -0.2));
        var resolution = GroupAction.Resolve(state, GroupFixtures.Declaration, GroupAction.DefaultConfig, new ConstantRolls(8));

        Assert.False(resolution.Result.AttackResult!.DefenseOutcome.Success);
        Assert.Equal(GroupDamage() * (1 - GroupFixtures.Enemy.ReactionSkills.Block.FMR), GroupFixtures.DamageDealt(resolution.State), 5);
    }

    [Fact(DisplayName = "buffed block mitigation rates set the damage and the recorded multiplier")]
    public void Mitigation_buffs()
    {
        var state = WithTargetBuffs(new Buff(BuffStat.BlockSMR, "test", 0.1), new Buff(BuffStat.BlockFMR, "test", 0.2));

        var success = GroupAction.Resolve(state, GroupFixtures.Declaration, GroupAction.DefaultConfig, new ConstantRolls(5));
        Assert.True(success.Result.AttackResult!.DefenseOutcome.Success);
        Assert.Equal(0.5, success.Result.AttackResult.DefenseOutcome.DamageMultiplier, 5);
        Assert.Equal(GroupDamage() * 0.5, GroupFixtures.DamageDealt(success.State), 5);

        var failure = GroupAction.Resolve(state, GroupFixtures.Declaration, GroupAction.DefaultConfig, new ConstantRolls(15));
        Assert.False(failure.Result.AttackResult!.DefenseOutcome.Success);
        Assert.Equal(0.7, failure.Result.AttackResult.DefenseOutcome.DamageMultiplier, 5);
        Assert.Equal(GroupDamage() * 0.7, GroupFixtures.DamageDealt(failure.State), 5);
    }

    [Fact(DisplayName = "a GROUP draws exactly one roll")]
    public void One_roll()
    {
        var rolls = new ConstantRolls(15);

        GroupAction.Resolve(GroupFixtures.BaseState, GroupFixtures.Declaration, GroupAction.DefaultConfig, rolls);

        Assert.Equal(1, rolls.Draws);
    }
}
