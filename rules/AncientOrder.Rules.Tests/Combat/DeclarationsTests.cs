using System.Text.Json;
using AncientOrder.Rules.Combat;
using AncientOrder.Rules.Tests.Fixtures;

namespace AncientOrder.Rules.Tests.Combat;

// Carried over from src/combat/declaration.test.ts. maxEnergyForAscensionLevel is not ported (it only returned the maxEnergy
// field), so its describe block (2 tests) is dropped. The last block is new: one order per fighter (T7).
internal static class DeclarationFixtures
{
    public static Combatant Make(string id, string name = "Test Fighter") => new(
        id,
        name,
        "scout",
        2.0,
        100,
        100,
        10,
        10,
        2,
        3,
        AscensionLevel.Level0,
        [],
        ElementalPath.Fire,
        new ReactionSkills(new DefenseSkill(0.6, 0.3, 0.1), new DefenseSkill(0.5, 0.8, 0.1), new DefenseSkill(0.4, 0.9, 0.1)),
        null,
        false);

    public static Combatant Attacker { get; } = Make("player_1", "Hero");

    public static Combatant Ally2 { get; } = Make("player_2", "Ally Two");

    public static Combatant Ally3 { get; } = Make("player_3", "Ally Three") with { Energy = 3, MaxEnergy = 3 };

    public static Combatant Enemy1 { get; } = Make("enemy_1", "Enemy One");

    public static Combatant Enemy2 { get; } = Make("enemy_2", "Enemy Two");

    public static Combatant Full(string id) => Make(id) with { Energy = 3, MaxEnergy = 3 };

    public static Combatant KoOf(string id) => Make(id) with { IsKO = true, Stamina = 0 };

    public static CombatState State(Combatant[]? players = null, Combatant[]? enemies = null) =>
        TestCombatants.State(players ?? [Attacker, Ally2, Ally3], enemies ?? [Enemy1, Enemy2]);

    public static CombatState FullEnergyState(Combatant[]? enemies = null) =>
        State([Full("player_1"), Full("player_2"), Full("player_3")], enemies);

    public static CombatAction Act(string who, ActionType type, string? target) => new(who, type, target, null);
}

public class ValidateDeclarationValidDeclarationsAC1
{
    private readonly CombatState _state = DeclarationFixtures.State();

    [Fact(DisplayName = "accepts a valid ATTACK action targeting a non-KO enemy")]
    public void Attack()
    {
        Assert.True(Declarations.Validate(_state, DeclarationFixtures.Act("player_1", ActionType.Attack, "enemy_1")).Valid);
    }

    [Fact(DisplayName = "accepts a valid DEFEND action targeting a non-KO ally")]
    public void Defend()
    {
        Assert.True(Declarations.Validate(_state, DeclarationFixtures.Act("player_1", ActionType.Defend, "player_2")).Valid);
    }

    [Fact(DisplayName = "accepts a valid EVADE action with null target")]
    public void Evade()
    {
        Assert.True(Declarations.Validate(_state, DeclarationFixtures.Act("player_1", ActionType.Evade, null)).Valid);
    }

    [Fact(DisplayName = "accepts a valid SPECIAL action targeting a non-KO enemy")]
    public void Special()
    {
        var action = new CombatAction("player_1", ActionType.Special, "enemy_1", 2);
        Assert.True(Declarations.Validate(_state, action).Valid);
    }

    [Fact(DisplayName = "accepts a valid GROUP action when all allies have full energy")]
    public void Group()
    {
        var result = Declarations.Validate(DeclarationFixtures.FullEnergyState(), DeclarationFixtures.Act("player_1", ActionType.Group, "enemy_1"));
        Assert.True(result.Valid);
    }
}

public class ValidateDeclarationTargetValidationAC2
{
    private readonly CombatState _state = DeclarationFixtures.State();

    private static CombatState WithKoEnemy() =>
        DeclarationFixtures.State(enemies: [DeclarationFixtures.KoOf("enemy_1"), DeclarationFixtures.Enemy2]);

    [Fact(DisplayName = "rejects ATTACK targeting a KO'd enemy")]
    public void Attack_ko_enemy()
    {
        var result = Declarations.Validate(WithKoEnemy(), DeclarationFixtures.Act("player_1", ActionType.Attack, "enemy_1"));
        Assert.False(result.Valid);
        Assert.Matches("(?i)KO", result.Error);
    }

    [Fact(DisplayName = "rejects ATTACK targeting a non-existent combatant ID")]
    public void Attack_ghost()
    {
        var result = Declarations.Validate(_state, DeclarationFixtures.Act("player_1", ActionType.Attack, "ghost_enemy"));
        Assert.False(result.Valid);
        Assert.False(string.IsNullOrEmpty(result.Error));
    }

    [Fact(DisplayName = "rejects ATTACK with null target")]
    public void Attack_null_target()
    {
        var result = Declarations.Validate(_state, DeclarationFixtures.Act("player_1", ActionType.Attack, null));
        Assert.False(result.Valid);
        Assert.False(string.IsNullOrEmpty(result.Error));
    }

    [Fact(DisplayName = "rejects ATTACK targeting an ally (must target enemy)")]
    public void Attack_ally()
    {
        Assert.False(Declarations.Validate(_state, DeclarationFixtures.Act("player_1", ActionType.Attack, "player_2")).Valid);
    }

    [Fact(DisplayName = "rejects SPECIAL targeting a KO'd enemy")]
    public void Special_ko_enemy()
    {
        var result = Declarations.Validate(WithKoEnemy(), DeclarationFixtures.Act("player_1", ActionType.Special, "enemy_1"));
        Assert.False(result.Valid);
        Assert.Matches("(?i)KO", result.Error);
    }

    [Fact(DisplayName = "rejects DEFEND targeting a KO'd ally")]
    public void Defend_ko_ally()
    {
        var state = DeclarationFixtures.State(players: [DeclarationFixtures.Attacker, DeclarationFixtures.KoOf("player_2"), DeclarationFixtures.Ally3]);
        var result = Declarations.Validate(state, DeclarationFixtures.Act("player_1", ActionType.Defend, "player_2"));
        Assert.False(result.Valid);
        Assert.Matches("(?i)KO", result.Error);
    }

    [Fact(DisplayName = "rejects DEFEND targeting a non-existent ally")]
    public void Defend_ghost()
    {
        Assert.False(Declarations.Validate(_state, DeclarationFixtures.Act("player_1", ActionType.Defend, "ghost_ally")).Valid);
    }

    [Fact(DisplayName = "rejects DEFEND targeting an enemy (must target ally)")]
    public void Defend_enemy()
    {
        Assert.False(Declarations.Validate(_state, DeclarationFixtures.Act("player_1", ActionType.Defend, "enemy_1")).Valid);
    }

    [Fact(DisplayName = "rejects EVADE when a non-null target is supplied")]
    public void Evade_with_target()
    {
        var result = Declarations.Validate(_state, DeclarationFixtures.Act("player_1", ActionType.Evade, "enemy_1"));
        Assert.False(result.Valid);
        Assert.False(string.IsNullOrEmpty(result.Error));
    }

    [Fact(DisplayName = "rejects GROUP targeting a KO'd enemy")]
    public void Group_ko_enemy()
    {
        var state = DeclarationFixtures.FullEnergyState([DeclarationFixtures.KoOf("enemy_1"), DeclarationFixtures.Enemy2]);
        var result = Declarations.Validate(state, DeclarationFixtures.Act("player_1", ActionType.Group, "enemy_1"));
        Assert.False(result.Valid);
        Assert.Matches("(?i)KO", result.Error);
    }
}

public class ValidateDeclarationStaminaCheckAC3
{
    [Fact(DisplayName = "rejects any action when combatant has zero stamina (but isKO might be false — belt-and-suspenders)")]
    public void Zero_stamina()
    {
        var low = DeclarationFixtures.Make("player_1") with { Stamina = 0, IsKO = false };
        var state = DeclarationFixtures.State(players: [low, DeclarationFixtures.Ally2, DeclarationFixtures.Ally3]);
        var result = Declarations.Validate(state, DeclarationFixtures.Act("player_1", ActionType.Attack, "enemy_1"));
        Assert.False(result.Valid);
        Assert.Matches("(?i)stamina", result.Error);
    }

    [Fact(DisplayName = "passes when combatant has positive stamina")]
    public void Positive_stamina()
    {
        Assert.True(Declarations.Validate(DeclarationFixtures.State(), DeclarationFixtures.Act("player_1", ActionType.Attack, "enemy_1")).Valid);
    }
}

public class ValidateDeclarationSpecialEnergyCheckAC4
{
    [Fact(DisplayName = "rejects SPECIAL when combatant energy is 0")]
    public void No_energy()
    {
        var none = DeclarationFixtures.Make("player_1") with { Energy = 0 };
        var state = DeclarationFixtures.State(players: [none, DeclarationFixtures.Ally2, DeclarationFixtures.Ally3]);
        var result = Declarations.Validate(state, DeclarationFixtures.Act("player_1", ActionType.Special, "enemy_1"));
        Assert.False(result.Valid);
        Assert.Matches("(?i)energy", result.Error);
    }

    [Fact(DisplayName = "accepts SPECIAL when combatant energy is greater than 0")]
    public void Some_energy()
    {
        Assert.True(Declarations.Validate(DeclarationFixtures.State(), DeclarationFixtures.Act("player_1", ActionType.Special, "enemy_1")).Valid);
    }

    [Fact(DisplayName = "accepts SPECIAL at exactly 1 energy segment")]
    public void One_segment()
    {
        var one = DeclarationFixtures.Make("player_1") with { Energy = 1 };
        var state = DeclarationFixtures.State(players: [one, DeclarationFixtures.Ally2, DeclarationFixtures.Ally3]);
        Assert.True(Declarations.Validate(state, DeclarationFixtures.Act("player_1", ActionType.Special, "enemy_1")).Valid);
    }
}

public class ValidateDeclarationGroupEnergyGateAC5
{
    private static CombatState Party(double leader, double ally2, double ally3)
    {
        var l = DeclarationFixtures.Make("player_1") with { Energy = leader, MaxEnergy = 3 };
        var a2 = DeclarationFixtures.Make("player_2") with { Energy = ally2, MaxEnergy = 3 };
        var a3 = DeclarationFixtures.Make("player_3") with { Energy = ally3, MaxEnergy = 3 };
        return DeclarationFixtures.State(players: [l, a2, a3]);
    }

    [Fact(DisplayName = "rejects GROUP when the leader lacks full energy, provides ATTACK fallback")]
    public void Leader_partial()
    {
        var result = Declarations.Validate(Party(1, 3, 3), DeclarationFixtures.Act("player_1", ActionType.Group, "enemy_1"));
        Assert.False(result.Valid);
        Assert.Matches("(?i)energy", result.Error);
        Assert.NotNull(result.Fallback);
        Assert.Equal(ActionType.Attack, result.Fallback.Type);
        Assert.Equal("player_1", result.Fallback.CombatantId);
        Assert.Equal("enemy_1", result.Fallback.TargetId);
    }

    [Fact(DisplayName = "rejects GROUP when a non-leader ally lacks full energy, provides ATTACK fallback")]
    public void Ally_partial()
    {
        var result = Declarations.Validate(Party(3, 2, 3), DeclarationFixtures.Act("player_1", ActionType.Group, "enemy_1"));
        Assert.False(result.Valid);
        Assert.Matches("(?i)energy", result.Error);
        Assert.NotNull(result.Fallback);
        Assert.Equal(ActionType.Attack, result.Fallback.Type);
        Assert.Equal("enemy_1", result.Fallback.TargetId);
    }

    [Fact(DisplayName = "accepts GROUP when all non-KO allies have full energy")]
    public void All_full()
    {
        Assert.True(Declarations.Validate(Party(3, 3, 3), DeclarationFixtures.Act("player_1", ActionType.Group, "enemy_1")).Valid);
    }

    [Fact(DisplayName = "accepts GROUP even when one ally is KO'd, as long as non-KO'd allies all have full energy")]
    public void One_ko()
    {
        var leader = DeclarationFixtures.Make("player_1") with { Energy = 3, MaxEnergy = 3 };
        var ko = DeclarationFixtures.Make("player_2") with { IsKO = true, Stamina = 0, Energy = 0, MaxEnergy = 3 };
        var ally3 = DeclarationFixtures.Make("player_3") with { Energy = 3, MaxEnergy = 3 };
        var state = DeclarationFixtures.State(players: [leader, ko, ally3]);
        Assert.True(Declarations.Validate(state, DeclarationFixtures.Act("player_1", ActionType.Group, "enemy_1")).Valid);
    }

    [Fact(DisplayName = "rejects GROUP when energy is 0 (empty, not just partial)")]
    public void Empty()
    {
        var result = Declarations.Validate(Party(0, 3, 3), DeclarationFixtures.Act("player_1", ActionType.Group, "enemy_1"));
        Assert.False(result.Valid);
        Assert.Equal(ActionType.Attack, result.Fallback?.Type);
    }
}

public class ValidateDeclarationPureFunctionAC6
{
    [Fact(DisplayName = "returns identical result when called twice with identical inputs")]
    public void Twice_same()
    {
        var state = DeclarationFixtures.State();
        var action = DeclarationFixtures.Act("player_1", ActionType.Attack, "enemy_1");
        Assert.Equal(Declarations.Validate(state, action), Declarations.Validate(state, action));
    }

    [Fact(DisplayName = "does not mutate the CombatState on a valid declaration")]
    public void No_mutation_valid()
    {
        var state = DeclarationFixtures.State();
        var before = JsonSerializer.Serialize(state);
        Declarations.Validate(state, DeclarationFixtures.Act("player_1", ActionType.Attack, "enemy_1"));
        Assert.Equal(before, JsonSerializer.Serialize(state));
    }

    [Fact(DisplayName = "does not mutate the CombatState on an invalid declaration")]
    public void No_mutation_invalid()
    {
        var state = DeclarationFixtures.State(enemies: [DeclarationFixtures.KoOf("enemy_1"), DeclarationFixtures.Enemy2]);
        var before = JsonSerializer.Serialize(state);
        Declarations.Validate(state, DeclarationFixtures.Act("player_1", ActionType.Attack, "enemy_1"));
        Assert.Equal(before, JsonSerializer.Serialize(state));
    }

    [Fact(DisplayName = "returns identical failure result when called twice with an invalid declaration")]
    public void Twice_same_failure()
    {
        var state = DeclarationFixtures.State();
        var action = DeclarationFixtures.Act("nonexistent_id", ActionType.Attack, "enemy_1");
        var first = Declarations.Validate(state, action);
        Assert.Equal(first, Declarations.Validate(state, action));
        Assert.False(first.Valid);
    }
}

public class ValidateDeclarationCombatantExistenceAndKOCheck1
{
    [Fact(DisplayName = "rejects when combatant ID does not exist in either party")]
    public void Unknown()
    {
        var result = Declarations.Validate(DeclarationFixtures.State(), DeclarationFixtures.Act("nonexistent", ActionType.Attack, "enemy_1"));
        Assert.False(result.Valid);
        Assert.Matches("(?i)not found", result.Error);
    }

    [Fact(DisplayName = "rejects when combatant is KO'd")]
    public void Ko()
    {
        var state = DeclarationFixtures.State(players: [DeclarationFixtures.KoOf("player_1"), DeclarationFixtures.Ally2, DeclarationFixtures.Ally3]);
        var result = Declarations.Validate(state, DeclarationFixtures.Act("player_1", ActionType.Attack, "enemy_1"));
        Assert.False(result.Valid);
        Assert.Matches("(?i)KO", result.Error);
    }

    [Fact(DisplayName = "allows actions from enemy party combatants (AI declarations are also validated)")]
    public void Enemy_actions()
    {
        Assert.True(Declarations.Validate(DeclarationFixtures.State(), DeclarationFixtures.Act("enemy_1", ActionType.Attack, "player_1")).Valid);
    }
}

// New (T7): one order per fighter per round, and the batch rules around it.
public class ValidateAllOneOrderPerFighter
{
    [Fact(DisplayName = "a second order for the same fighter is rejected with a reason")]
    public void Second_order_rejected()
    {
        var orders = new[]
        {
            DeclarationFixtures.Act("player_1", ActionType.Attack, "enemy_1"),
            DeclarationFixtures.Act("player_1", ActionType.Evade, null),
        };
        var batch = Declarations.ValidateAll(DeclarationFixtures.State(), orders);
        Assert.Equal(new[] { orders[0] }, batch.Accepted);
        var rejected = Assert.Single(batch.Rejected);
        Assert.Equal(1, rejected.Index);
        Assert.Equal("Combatant 'player_1' already has an order this round", rejected.Reason);
    }

    [Fact(DisplayName = "two Specials from one fighter keep only the first")]
    public void Two_specials()
    {
        var orders = new[]
        {
            new CombatAction("player_1", ActionType.Special, "enemy_1", 1),
            new CombatAction("player_1", ActionType.Special, "enemy_2", 1),
        };
        var batch = Declarations.ValidateAll(DeclarationFixtures.State(), orders);
        Assert.Equal(new[] { orders[0] }, batch.Accepted);
        Assert.Equal(1, Assert.Single(batch.Rejected).Index);
    }

    [Fact(DisplayName = "a refused first order does not use up the fighter's order")]
    public void Refused_first_order_does_not_hold()
    {
        var orders = new[]
        {
            DeclarationFixtures.Act("player_1", ActionType.Attack, "ghost"),
            DeclarationFixtures.Act("player_1", ActionType.Attack, "enemy_1"),
        };
        var batch = Declarations.ValidateAll(DeclarationFixtures.State(), orders);
        Assert.Equal(new[] { orders[1] }, batch.Accepted);
        Assert.Equal(0, Assert.Single(batch.Rejected).Index);
    }

    [Fact(DisplayName = "a GROUP fallback attack holds the leader's order")]
    public void Group_fallback_holds_order()
    {
        var orders = new[]
        {
            DeclarationFixtures.Act("player_1", ActionType.Group, "enemy_1"),
            DeclarationFixtures.Act("player_1", ActionType.Evade, null),
        };
        var batch = Declarations.ValidateAll(DeclarationFixtures.State(), orders);
        Assert.Equal(new[] { DeclarationFixtures.Act("player_1", ActionType.Attack, "enemy_1") }, batch.Accepted);
        Assert.Equal(1, Assert.Single(batch.Rejected).Index);
    }

    [Fact(DisplayName = "an accepted GROUP removes the living allies' orders")]
    public void Group_overrides_allies()
    {
        var ko = DeclarationFixtures.KoOf("player_3") with { MaxEnergy = 3 };
        var state = DeclarationFixtures.State(
            players: [DeclarationFixtures.Full("player_1"), DeclarationFixtures.Full("player_2"), ko]);
        var orders = new[]
        {
            DeclarationFixtures.Act("player_2", ActionType.Attack, "enemy_1"),
            DeclarationFixtures.Act("player_1", ActionType.Group, "enemy_1"),
        };
        var batch = Declarations.ValidateAll(state, orders);
        Assert.Equal(new[] { orders[1] }, batch.Accepted);
        Assert.Empty(batch.Rejected);
    }

    [Fact(DisplayName = "valid orders from different fighters are all accepted in declaration order")]
    public void Different_fighters()
    {
        var orders = new[]
        {
            DeclarationFixtures.Act("player_2", ActionType.Attack, "enemy_1"),
            DeclarationFixtures.Act("player_1", ActionType.Evade, null),
        };
        var batch = Declarations.ValidateAll(DeclarationFixtures.State(), orders);
        Assert.Equal(orders, batch.Accepted);
        Assert.Empty(batch.Rejected);
    }
}
