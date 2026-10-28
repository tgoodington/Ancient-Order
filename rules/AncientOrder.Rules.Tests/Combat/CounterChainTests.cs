using AncientOrder.Rules.Combat;
using AncientOrder.Rules.Tests.Support;

namespace AncientOrder.Rules.Tests.Combat;

// Carried over from src/combat/counterChain.test.ts. Changed: the TypeScript chain returned AttackResult "actions"; the C# chain
// returns CounterExchange records (T7), so "actions" reads as Exchanges, "defenseType" as Defense, "defenseOutcome" as Outcome.
// The two AttackResult-field tests are adapted (an exchange has no counterChain/rankKO/blindside/crushingBlow flags).
// The roll sequences are exact: FixedRolls fails if more rolls are drawn than scripted.
internal static class CounterChainFixtures
{
    public static readonly ReactionSkills HighParrySkills = new(
        new DefenseSkill(0.9, 0.5, 0.2),
        new DefenseSkill(0.9, 0.8, 0.15),
        new DefenseSkill(0.9, 0.9, 0.1));

    public static readonly ReactionSkills LowParrySkills = new(
        new DefenseSkill(0.1, 0.2, 0.05),
        new DefenseSkill(0.1, 0.8, 0.05),
        new DefenseSkill(0.1, 0.9, 0.2));

    public static Combatant Make(string id, double stamina, ReactionSkills skills, double power = 50) => new(
        id,
        id,
        "test",
        1.0,
        stamina,
        stamina,
        power,
        10,
        0,
        5,
        AscensionLevel.Level0,
        [],
        ElementalPath.Fire,
        skills,
        null,
        stamina <= 0);

    public static CombatState State(Combatant player, Combatant enemy) =>
        new(
            1,
            CombatPhase.PerAttack,
            [player],
            [enemy],
            [],
            [],
            CombatStatus.Active,
            AncientOrder.Rules.Common.DiceState.FromSeed(1));

    public static Combatant Enemy(CombatState state, string id) => state.EnemyParty.Single(c => c.Id == id);

    public static Combatant Player(CombatState state, string id) => state.PlayerParty.Single(c => c.Id == id);
}

public class ResolveCounterChainChainLength1
{
    [Fact(DisplayName = "terminates on first Parry failure and applies damage to the original attacker")]
    public void First_failure()
    {
        var parrier = CounterChainFixtures.Make("player_1", 200, CounterChainFixtures.LowParrySkills, 50);
        var originalAttacker = CounterChainFixtures.Make("enemy_1", 200, CounterChainFixtures.LowParrySkills, 50);
        var state = CounterChainFixtures.State(parrier, originalAttacker);

        var result = CounterChain.Resolve(state, originalAttacker, parrier, new FixedRolls(19));

        Assert.Equal(1, result.ChainLength);
        var exchange = Assert.Single(result.Exchanges);
        Assert.Equal("player_1", exchange.AttackerId);
        Assert.Equal("enemy_1", exchange.DefenderId);
        Assert.Equal(DefenseType.Parry, exchange.Defense);
        Assert.False(exchange.Outcome.Success);
        Assert.True(CounterChainFixtures.Enemy(result.State, "enemy_1").Stamina < 200);
    }

    [Fact(DisplayName = "records the correct damage value in the action result")]
    public void Damage_value()
    {
        var parrier = CounterChainFixtures.Make("player_1", 200, CounterChainFixtures.LowParrySkills, 50);
        var originalAttacker = CounterChainFixtures.Make("enemy_1", 200, CounterChainFixtures.LowParrySkills, 60);
        var state = CounterChainFixtures.State(parrier, originalAttacker);

        var result = CounterChain.Resolve(state, originalAttacker, parrier, new FixedRolls(19));

        var expectedBase = 50;
        var expectedDamage = expectedBase * (1 - CounterChainFixtures.LowParrySkills.Parry.FMR);
        Assert.Equal(expectedDamage, result.Exchanges[0].Damage, 5);
    }
}

public class ResolveCounterChainChainLength2
{
    [Fact(DisplayName = "resolves two exchanges: first parry succeeds (no damage), second parry fails (damage applied)")]
    public void Two_exchanges()
    {
        var playerLowParry = CounterChainFixtures.Make("player_2", 200, CounterChainFixtures.LowParrySkills, 50);
        var enemyHighParry = CounterChainFixtures.Make("enemy_2", 200, CounterChainFixtures.HighParrySkills, 50);
        var state = CounterChainFixtures.State(playerLowParry, enemyHighParry);

        var result = CounterChain.Resolve(state, enemyHighParry, playerLowParry, new FixedRolls(1, 19));

        Assert.Equal(2, result.ChainLength);
        Assert.Equal(2, result.Exchanges.Length);

        var parrySuccessDamage = 50 * (1 - CounterChainFixtures.HighParrySkills.Parry.SMR);
        Assert.Equal("player_2", result.Exchanges[0].AttackerId);
        Assert.Equal("enemy_2", result.Exchanges[0].DefenderId);
        Assert.True(result.Exchanges[0].Outcome.Success);
        Assert.Equal(parrySuccessDamage, result.Exchanges[0].Damage, 5);

        Assert.Equal("enemy_2", result.Exchanges[1].AttackerId);
        Assert.Equal("player_2", result.Exchanges[1].DefenderId);
        Assert.False(result.Exchanges[1].Outcome.Success);
        Assert.True(result.Exchanges[1].Damage > 0);

        Assert.True(CounterChainFixtures.Player(result.State, "player_2").Stamina < 200);
        Assert.Equal(200 - parrySuccessDamage, CounterChainFixtures.Enemy(result.State, "enemy_2").Stamina, 5);
    }
}

public class ResolveCounterChainChainLength3
{
    [Fact(DisplayName = "resolves three exchanges: two parries succeed, third fails and applies damage")]
    public void Three_exchanges()
    {
        var playerHighParry = CounterChainFixtures.Make("player_3", 500, CounterChainFixtures.HighParrySkills, 50);
        var enemyHighParry = CounterChainFixtures.Make("enemy_3", 500, CounterChainFixtures.HighParrySkills, 50);
        var state = CounterChainFixtures.State(playerHighParry, enemyHighParry);

        var result = CounterChain.Resolve(state, enemyHighParry, playerHighParry, new FixedRolls(1, 1, 19));

        Assert.Equal(3, result.ChainLength);
        Assert.Equal(3, result.Exchanges.Length);

        var parrySuccessDamage = 50 * (1 - CounterChainFixtures.HighParrySkills.Parry.SMR);

        Assert.Equal("player_3", result.Exchanges[0].AttackerId);
        Assert.Equal("enemy_3", result.Exchanges[0].DefenderId);
        Assert.True(result.Exchanges[0].Outcome.Success);
        Assert.Equal(parrySuccessDamage, result.Exchanges[0].Damage, 5);

        Assert.Equal("enemy_3", result.Exchanges[1].AttackerId);
        Assert.Equal("player_3", result.Exchanges[1].DefenderId);
        Assert.True(result.Exchanges[1].Outcome.Success);
        Assert.Equal(parrySuccessDamage, result.Exchanges[1].Damage, 5);

        Assert.Equal("player_3", result.Exchanges[2].AttackerId);
        Assert.Equal("enemy_3", result.Exchanges[2].DefenderId);
        Assert.False(result.Exchanges[2].Outcome.Success);
        Assert.True(result.Exchanges[2].Damage > 0);

        var exchange3Damage = 50 * (1 - CounterChainFixtures.HighParrySkills.Parry.FMR);
        Assert.Equal(500 - parrySuccessDamage - exchange3Damage, CounterChainFixtures.Enemy(result.State, "enemy_3").Stamina, 5);
        Assert.Equal(500 - parrySuccessDamage, CounterChainFixtures.Player(result.State, "player_3").Stamina, 5);
    }
}

public class ResolveCounterChainTerminationParryFailure
{
    [Fact(DisplayName = "terminates immediately when the first Parry attempt fails")]
    public void First_failure()
    {
        var parrier = CounterChainFixtures.Make("p", 100, CounterChainFixtures.LowParrySkills, 50);
        var attacker = CounterChainFixtures.Make("e", 100, CounterChainFixtures.LowParrySkills, 50);
        var state = CounterChainFixtures.State(parrier, attacker);

        var result = CounterChain.Resolve(state, attacker, parrier, new FixedRolls(19));

        Assert.Equal(1, result.ChainLength);
        Assert.False(result.Exchanges[0].Outcome.Success);
    }
}

public class ResolveCounterChainTerminationKO
{
    [Fact(DisplayName = "terminates when target is KO'd after damage application")]
    public void Ko_after_damage()
    {
        var parrier = CounterChainFixtures.Make("player_ko", 200, CounterChainFixtures.HighParrySkills, 100);
        var originalAttacker = CounterChainFixtures.Make("enemy_ko", 1, CounterChainFixtures.LowParrySkills, 10);
        var state = CounterChainFixtures.State(parrier, originalAttacker);

        var result = CounterChain.Resolve(state, originalAttacker, parrier, new FixedRolls(19));

        Assert.Equal(1, result.ChainLength);
        var updatedEnemy = CounterChainFixtures.Enemy(result.State, "enemy_ko");
        Assert.Equal(0, updatedEnemy.Stamina);
        Assert.True(updatedEnemy.IsKO);
    }

    [Fact(DisplayName = "terminates when target is already KO'd at chain start")]
    public void Already_ko()
    {
        var parrier = CounterChainFixtures.Make("player_ko2", 200, CounterChainFixtures.HighParrySkills, 50);
        var koAttacker = CounterChainFixtures.Make("enemy_ko2", 0, CounterChainFixtures.LowParrySkills, 50) with { IsKO = true };
        var state = CounterChainFixtures.State(parrier, koAttacker);
        var rolls = new FixedRolls(1, 1, 1);

        var result = CounterChain.Resolve(state, koAttacker, parrier, rolls);

        Assert.Equal(0, result.ChainLength);
        Assert.Empty(result.Exchanges);
        Assert.Equal(0, rolls.Consumed);
    }
}

public class ResolveCounterChainTerminationStaminaDepletion
{
    [Fact(DisplayName = "terminates when target stamina reaches 0 and KO is set")]
    public void Stamina_zero()
    {
        var parrier = CounterChainFixtures.Make("player_sd", 500, CounterChainFixtures.LowParrySkills, 100);
        var originalAttacker = CounterChainFixtures.Make("enemy_sd", 5, CounterChainFixtures.LowParrySkills, 10);
        var state = CounterChainFixtures.State(parrier, originalAttacker);

        var result = CounterChain.Resolve(state, originalAttacker, parrier, new FixedRolls(19));

        Assert.Equal(1, result.ChainLength);
        var updatedEnemy = CounterChainFixtures.Enemy(result.State, "enemy_sd");
        Assert.Equal(0, updatedEnemy.Stamina);
        Assert.True(updatedEnemy.IsKO);
    }
}

public class ResolveCounterChainImmutability
{
    [Fact(DisplayName = "does not mutate the original state")]
    public void No_mutation()
    {
        var parrier = CounterChainFixtures.Make("player_imm", 200, CounterChainFixtures.LowParrySkills, 50);
        var originalAttacker = CounterChainFixtures.Make("enemy_imm", 200, CounterChainFixtures.LowParrySkills, 50);
        var state = CounterChainFixtures.State(parrier, originalAttacker);
        var originalEnemyStamina = state.EnemyParty[0].Stamina;

        CounterChain.Resolve(state, originalAttacker, parrier, new FixedRolls(19));

        Assert.Equal(originalEnemyStamina, state.EnemyParty[0].Stamina);
    }

    [Fact(DisplayName = "returns a new state object (not the same reference) when damage is applied")]
    public void New_state()
    {
        var parrier = CounterChainFixtures.Make("player_ref", 200, CounterChainFixtures.LowParrySkills, 50);
        var originalAttacker = CounterChainFixtures.Make("enemy_ref", 200, CounterChainFixtures.LowParrySkills, 50);
        var state = CounterChainFixtures.State(parrier, originalAttacker);

        var result = CounterChain.Resolve(state, originalAttacker, parrier, new FixedRolls(19));

        Assert.NotSame(state, result.State);
    }

    [Fact(DisplayName = "returns the same state reference when chain length is 0")]
    public void Same_state_when_empty()
    {
        var parrier = CounterChainFixtures.Make("player_same", 200, CounterChainFixtures.HighParrySkills, 50);
        var koAttacker = CounterChainFixtures.Make("enemy_same", 0, CounterChainFixtures.LowParrySkills, 50) with { IsKO = true };
        var state = CounterChainFixtures.State(parrier, koAttacker);

        var result = CounterChain.Resolve(state, koAttacker, parrier, new FixedRolls());

        Assert.Same(state, result.State);
        Assert.Equal(0, result.ChainLength);
    }
}

public class ResolveCounterChainAttackResultFields
{
    [Fact(DisplayName = "all actions in the chain have counterChain = true")]
    public void All_are_counter_exchanges()
    {
        // Changed: an exchange has no counterChain flag; every entry in Exchanges is a counter-attack by definition.
        var playerHighParry = CounterChainFixtures.Make("p_cc", 500, CounterChainFixtures.HighParrySkills, 50);
        var enemyHighParry = CounterChainFixtures.Make("e_cc", 500, CounterChainFixtures.HighParrySkills, 50);
        var state = CounterChainFixtures.State(playerHighParry, enemyHighParry);

        var result = CounterChain.Resolve(state, enemyHighParry, playerHighParry, new FixedRolls(1, 1, 19));

        Assert.Equal(3, result.Exchanges.Length);
        Assert.Equal(result.ChainLength, result.Exchanges.Length);
        Assert.All(result.Exchanges, e => Assert.Equal(DefenseType.Parry, e.Defense));
    }

    [Fact(DisplayName = "all actions have rankKO, blindside, crushingBlow = false (not applicable in counter chain)")]
    public void No_rank_ko_blindside_or_crushing_blow_rolls()
    {
        // Changed: an exchange has no such flags; the chain draws exactly one roll per exchange (the defense roll),
        // so no rank KO, blindside, or Crushing Blow roll can happen.
        var parrier = CounterChainFixtures.Make("p_fields", 200, CounterChainFixtures.LowParrySkills, 50);
        var attacker = CounterChainFixtures.Make("e_fields", 200, CounterChainFixtures.LowParrySkills, 50);
        var state = CounterChainFixtures.State(parrier, attacker);
        var rolls = new FixedRolls(19);

        var result = CounterChain.Resolve(state, attacker, parrier, rolls);

        Assert.Equal(result.Exchanges.Length, rolls.Consumed);
    }
}

// New (T7): the chain records every exchange.
public class ResolveCounterChainRecordedExchanges
{
    [Fact(DisplayName = "each exchange records attacker, defender, defense, outcome, and the damage it took")]
    public void Exchange_fields()
    {
        var parrier = CounterChainFixtures.Make("player_1", 200, CounterChainFixtures.LowParrySkills, 50);
        var originalAttacker = CounterChainFixtures.Make("enemy_1", 200, CounterChainFixtures.LowParrySkills, 50);
        var state = CounterChainFixtures.State(parrier, originalAttacker);

        var result = CounterChain.Resolve(state, originalAttacker, parrier, new FixedRolls(19));

        var exchange = Assert.Single(result.Exchanges);
        Assert.Equal("player_1", exchange.AttackerId);
        Assert.Equal("enemy_1", exchange.DefenderId);
        Assert.Equal(DefenseType.Parry, exchange.Defense);
        Assert.Equal(DefenseType.Parry, exchange.Outcome.Type);
        Assert.False(exchange.Outcome.Success);
        Assert.Equal(40, exchange.Damage, 5);
        Assert.Equal(200 - exchange.Damage, CounterChainFixtures.Enemy(result.State, "enemy_1").Stamina, 5);
    }

    [Fact(DisplayName = "a chain of 3 exchanges records 3 exchanges in order")]
    public void Order()
    {
        var player = CounterChainFixtures.Make("player_3", 500, CounterChainFixtures.HighParrySkills, 50);
        var enemy = CounterChainFixtures.Make("enemy_3", 500, CounterChainFixtures.HighParrySkills, 50);

        var result = CounterChain.Resolve(CounterChainFixtures.State(player, enemy), enemy, player, new FixedRolls(1, 1, 19));

        Assert.Equal(["player_3", "enemy_3", "player_3"], result.Exchanges.Select(e => e.AttackerId));
        Assert.Equal([true, true, false], result.Exchanges.Select(e => e.Outcome.Success));
    }

    [Fact(DisplayName = "a chain of successful Parries stops silently at the chain depth limit")]
    public void Depth_limit()
    {
        var player = CounterChainFixtures.Make("player_d", 100000, CounterChainFixtures.HighParrySkills, 50);
        var enemy = CounterChainFixtures.Make("enemy_d", 100000, CounterChainFixtures.HighParrySkills, 50);
        var rolls = new FixedRolls(Enumerable.Repeat(1.0, 30).ToArray());

        var result = CounterChain.Resolve(CounterChainFixtures.State(player, enemy), enemy, player, rolls);

        Assert.Equal(CombatConstants.MaxChainDepth, result.ChainLength);
        Assert.Equal(CombatConstants.MaxChainDepth, result.Exchanges.Length);
        Assert.Equal(CombatConstants.MaxChainDepth, rolls.Consumed);
    }
}
