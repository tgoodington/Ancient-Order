using AncientOrder.Rules.Combat;

namespace AncientOrder.Rules.Tests.Combat;

// Carried over from src/combat/defense.test.ts. toBeCloseTo(x) with no precision is 2 decimals, kept as precision 2; toBe is exact.
internal static class DefenseFixtures
{
    public static readonly ReactionSkills StandardReactionSkills = new(
        new DefenseSkill(0.6, 0.5, 0.2),
        new DefenseSkill(0.5, 0.8, 0.15),
        new DefenseSkill(0.4, 0.9, 0.1));

    public const double RawDamage = 100;
}

public class ResolveBlock
{
    [Fact(DisplayName = "succeeds when roll is at threshold (roll === SR * 20)")]
    public void At_threshold()
    {
        var result = Defense.ResolveBlock(DefenseFixtures.RawDamage, 0.6, 0.5, 0.2, 12);
        Assert.True(result.Success);
        Assert.Equal(50, result.Damage);
        Assert.True(result.CrushingBlowEligible);
    }

    [Fact(DisplayName = "succeeds when roll is below threshold (roll < SR * 20)")]
    public void Below_threshold()
    {
        var result = Defense.ResolveBlock(DefenseFixtures.RawDamage, 0.6, 0.5, 0.2, 5);
        Assert.True(result.Success);
        Assert.Equal(50, result.Damage);
        Assert.True(result.CrushingBlowEligible);
    }

    [Fact(DisplayName = "fails when roll is above threshold (roll > SR * 20)")]
    public void Above_threshold()
    {
        var result = Defense.ResolveBlock(DefenseFixtures.RawDamage, 0.6, 0.5, 0.2, 13);
        Assert.False(result.Success);
        Assert.Equal(80, result.Damage);
        Assert.True(result.CrushingBlowEligible);
    }

    [Fact(DisplayName = "fails when roll is at maximum (roll = 20)")]
    public void Max_roll()
    {
        var result = Defense.ResolveBlock(DefenseFixtures.RawDamage, 0.6, 0.5, 0.2, 20);
        Assert.False(result.Success);
        Assert.Equal(80, result.Damage);
    }

    [Fact(DisplayName = "always marks crushingBlowEligible as true (eligibility is the caller's responsibility)")]
    public void Always_eligible()
    {
        var success = Defense.ResolveBlock(DefenseFixtures.RawDamage, 0.1, 0.5, 0.2, 1);
        var failure = Defense.ResolveBlock(DefenseFixtures.RawDamage, 0.1, 0.5, 0.2, 19);
        Assert.True(success.CrushingBlowEligible);
        Assert.True(failure.CrushingBlowEligible);
    }

    [Fact(DisplayName = "applies different SMR and FMR values correctly")]
    public void Different_rates()
    {
        var success = Defense.ResolveBlock(200, 0.8, 0.9, 0.1, 10);
        Assert.Equal(200 * 0.1, success.Damage, 2);

        var failure = Defense.ResolveBlock(200, 0.8, 0.9, 0.1, 17);
        Assert.Equal(200 * 0.9, failure.Damage, 2);
    }
}

public class ResolveDodge
{
    [Fact(DisplayName = "succeeds with SMR-mitigated damage when roll is at threshold (roll === SR * 20)")]
    public void At_threshold()
    {
        var result = Defense.ResolveDodge(DefenseFixtures.RawDamage, 0.5, 0.8, 0.15, 10);
        Assert.True(result.Success);
        Assert.Equal(20, result.Damage, 2);
    }

    [Fact(DisplayName = "succeeds with SMR-mitigated damage when roll is below threshold")]
    public void Below_threshold()
    {
        var result = Defense.ResolveDodge(DefenseFixtures.RawDamage, 0.5, 0.8, 0.15, 1);
        Assert.True(result.Success);
        Assert.Equal(20, result.Damage, 2);
    }

    [Fact(DisplayName = "fails with partial damage when roll is above threshold")]
    public void Above_threshold()
    {
        var result = Defense.ResolveDodge(DefenseFixtures.RawDamage, 0.5, 0.8, 0.15, 11);
        Assert.False(result.Success);
        Assert.Equal(85, result.Damage);
    }

    [Fact(DisplayName = "fails when roll is at maximum (roll = 20)")]
    public void Max_roll()
    {
        var result = Defense.ResolveDodge(DefenseFixtures.RawDamage, 0.5, 0.8, 0.15, 20);
        Assert.False(result.Success);
        Assert.Equal(85, result.Damage);
    }

    [Fact(DisplayName = "applies SMR correctly on success")]
    public void Smr_on_success() => Assert.Equal(50 * 0.4, Defense.ResolveDodge(50, 0.4, 0.6, 0.3, 1).Damage, 2);

    [Fact(DisplayName = "applies FMR correctly on failure")]
    public void Fmr_on_failure() => Assert.Equal(50 * 0.7, Defense.ResolveDodge(50, 0.4, 0.8, 0.3, 20).Damage, 2);
}

public class ResolveParry
{
    [Fact(DisplayName = "succeeds (SMR-mitigated damage, counter triggered) when roll is at threshold (roll === SR * 20)")]
    public void At_threshold()
    {
        var result = Defense.ResolveParry(DefenseFixtures.RawDamage, 0.4, 0.9, 0.1, 8);
        Assert.True(result.Success);
        Assert.Equal(10, result.Damage, 2);
        Assert.True(result.CounterTriggered);
    }

    [Fact(DisplayName = "succeeds when roll is below threshold")]
    public void Below_threshold()
    {
        var result = Defense.ResolveParry(DefenseFixtures.RawDamage, 0.4, 0.9, 0.1, 1);
        Assert.True(result.Success);
        Assert.Equal(10, result.Damage, 2);
        Assert.True(result.CounterTriggered);
    }

    [Fact(DisplayName = "fails with partial damage and no counter when roll is above threshold")]
    public void Above_threshold()
    {
        var result = Defense.ResolveParry(DefenseFixtures.RawDamage, 0.4, 0.9, 0.1, 9);
        Assert.False(result.Success);
        Assert.Equal(90, result.Damage);
        Assert.False(result.CounterTriggered);
    }

    [Fact(DisplayName = "fails when roll is at maximum (roll = 20)")]
    public void Max_roll()
    {
        var result = Defense.ResolveParry(DefenseFixtures.RawDamage, 0.4, 0.9, 0.1, 20);
        Assert.False(result.Success);
        Assert.Equal(90, result.Damage);
        Assert.False(result.CounterTriggered);
    }

    [Fact(DisplayName = "counterTriggered is false on failure")]
    public void No_counter_on_failure() => Assert.False(Defense.ResolveParry(DefenseFixtures.RawDamage, 0.1, 0.9, 0.2, 20).CounterTriggered);

    [Fact(DisplayName = "applies SMR correctly on success")]
    public void Smr_on_success() => Assert.Equal(80 * 0.3, Defense.ResolveParry(80, 0.9, 0.7, 0.25, 1).Damage, 2);

    [Fact(DisplayName = "applies FMR correctly on failure")]
    public void Fmr_on_failure() => Assert.Equal(80 * 0.75, Defense.ResolveParry(80, 0.2, 0.9, 0.25, 20).Damage, 2);
}

// resolveDefenseless returned { success: false, damage }; the C# one returns the damage (the success flag is implied false and lives on ResolveDefense's DefenseResult).
public class ResolveDefenseless
{
    [Fact(DisplayName = "always returns success = false")]
    public void Always_fails()
    {
        var result = Defense.ResolveDefense(DefenseType.Defenseless, 100, DefenseFixtures.StandardReactionSkills, 0);
        Assert.False(result.Success);
    }

    [Fact(DisplayName = "always returns full damage (no mitigation)")]
    public void Full_damage()
    {
        Assert.Equal(100, Defense.ResolveDefenseless(100));
        Assert.Equal(0, Defense.ResolveDefenseless(0));
        Assert.Equal(42.5, Defense.ResolveDefenseless(42.5));
    }

    [Fact(DisplayName = "is deterministic regardless of any external state")]
    public void Deterministic()
    {
        var a = Defense.ResolveDefenseless(75);
        var b = Defense.ResolveDefenseless(75);
        Assert.Equal(a, b);
    }
}

public class ResolveDefense
{
    public class BlockDispatch
    {
        [Fact(DisplayName = "returns type = block and correct success/damageMultiplier on success")]
        public void Success()
        {
            var result = Defense.ResolveDefense(DefenseType.Block, DefenseFixtures.RawDamage, DefenseFixtures.StandardReactionSkills, 5);
            Assert.Equal(DefenseType.Block, result.Type);
            Assert.True(result.Success);
            Assert.Equal(0.5, result.DamageMultiplier, 2);
        }

        [Fact(DisplayName = "returns type = block and correct success/damageMultiplier on failure")]
        public void Failure()
        {
            var result = Defense.ResolveDefense(DefenseType.Block, DefenseFixtures.RawDamage, DefenseFixtures.StandardReactionSkills, 20);
            Assert.Equal(DefenseType.Block, result.Type);
            Assert.False(result.Success);
            Assert.Equal(0.8, result.DamageMultiplier, 2);
        }
    }

    public class DodgeDispatch
    {
        [Fact(DisplayName = "returns type = dodge and SMR-based damageMultiplier on success")]
        public void Success()
        {
            var result = Defense.ResolveDefense(DefenseType.Dodge, DefenseFixtures.RawDamage, DefenseFixtures.StandardReactionSkills, 5);
            Assert.Equal(DefenseType.Dodge, result.Type);
            Assert.True(result.Success);
            Assert.Equal(0.2, result.DamageMultiplier, 2);
        }

        [Fact(DisplayName = "returns type = dodge and partial damageMultiplier on failure")]
        public void Failure()
        {
            var result = Defense.ResolveDefense(DefenseType.Dodge, DefenseFixtures.RawDamage, DefenseFixtures.StandardReactionSkills, 20);
            Assert.Equal(DefenseType.Dodge, result.Type);
            Assert.False(result.Success);
            Assert.Equal(0.85, result.DamageMultiplier, 2);
        }
    }

    public class ParryDispatch
    {
        [Fact(DisplayName = "returns type = parry and SMR-based damageMultiplier on success")]
        public void Success()
        {
            var result = Defense.ResolveDefense(DefenseType.Parry, DefenseFixtures.RawDamage, DefenseFixtures.StandardReactionSkills, 1);
            Assert.Equal(DefenseType.Parry, result.Type);
            Assert.True(result.Success);
            Assert.Equal(0.1, result.DamageMultiplier, 2);
        }

        [Fact(DisplayName = "returns type = parry and partial damageMultiplier on failure")]
        public void Failure()
        {
            var result = Defense.ResolveDefense(DefenseType.Parry, DefenseFixtures.RawDamage, DefenseFixtures.StandardReactionSkills, 20);
            Assert.Equal(DefenseType.Parry, result.Type);
            Assert.False(result.Success);
            Assert.Equal(0.9, result.DamageMultiplier, 2);
        }
    }

    public class DefenselessDispatch
    {
        [Fact(DisplayName = "returns type = defenseless, success = false, damageMultiplier = 1.0")]
        public void Defenseless()
        {
            var result = Defense.ResolveDefense(DefenseType.Defenseless, DefenseFixtures.RawDamage, DefenseFixtures.StandardReactionSkills, 5);
            Assert.Equal(DefenseType.Defenseless, result.Type);
            Assert.False(result.Success);
            Assert.Equal(1.0, result.DamageMultiplier);
        }

        [Fact(DisplayName = "ignores the roll value (always full damage)")]
        public void Ignores_roll()
        {
            var low = Defense.ResolveDefense(DefenseType.Defenseless, DefenseFixtures.RawDamage, DefenseFixtures.StandardReactionSkills, 0);
            var high = Defense.ResolveDefense(DefenseType.Defenseless, DefenseFixtures.RawDamage, DefenseFixtures.StandardReactionSkills, 20);
            Assert.Equal(1.0, low.DamageMultiplier);
            Assert.Equal(1.0, high.DamageMultiplier);
        }
    }
}
