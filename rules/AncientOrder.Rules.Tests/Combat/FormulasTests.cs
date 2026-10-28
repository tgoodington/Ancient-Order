using System.Collections.Immutable;
using AncientOrder.Rules.Combat;

namespace AncientOrder.Rules.Tests.Combat;

// Carried over from src/combat/formulas.test.ts. toBeCloseTo(x, 10) becomes Assert.Equal(x, actual, 10); toBe is exact.
public class CalculateRankKOThreshold
{
    [Fact(DisplayName = "returns correct threshold when attacker rank is 5 and target rank is 3")]
    public void Rank_5_vs_3() => Assert.Equal(0.6, Formulas.CalculateRankKOThreshold(5, 3), 10);

    [Fact(DisplayName = "returns 0.3 when rank difference is 1")]
    public void Diff_1() => Assert.Equal(0.3, Formulas.CalculateRankKOThreshold(2, 1), 10);

    [Fact(DisplayName = "returns 0.9 when rank difference is 3")]
    public void Diff_3() => Assert.Equal(0.9, Formulas.CalculateRankKOThreshold(4, 1), 10);

    [Fact(DisplayName = "handles fractional rank differences")]
    public void Fractional() => Assert.Equal(0.45, Formulas.CalculateRankKOThreshold(3.5, 2.0), 10);

    [Fact(DisplayName = "returns 0 when ranks are equal")]
    public void Equal_ranks() => Assert.Equal(0, Formulas.CalculateRankKOThreshold(3, 3), 10);
}

public class CheckRankKO
{
    [Fact(DisplayName = "returns true when roll/20 >= (1 - threshold) — example from spec: threshold=0.6, roll=14")]
    public void Spec_example() => Assert.True(Formulas.CheckRankKO(0.6, 14));

    [Fact(DisplayName = "returns false when roll/20 < (1 - threshold)")]
    public void Below() => Assert.False(Formulas.CheckRankKO(0.6, 7));

    [Fact(DisplayName = "returns true at the exact threshold boundary")]
    public void Boundary() => Assert.True(Formulas.CheckRankKO(0.6, 8));

    [Fact(DisplayName = "returns false for threshold=0 (equal ranks)")]
    public void Zero_threshold() => Assert.False(Formulas.CheckRankKO(0, 15));

    [Fact(DisplayName = "returns true at high threshold with moderate roll")]
    public void High_threshold() => Assert.True(Formulas.CheckRankKO(0.9, 3));

    [Fact(DisplayName = "returns false just below the boundary")]
    public void Just_below() => Assert.False(Formulas.CheckRankKO(0.5, 9));
}

public class CalculateBlindsideThreshold
{
    [Fact(DisplayName = "returns 0.25 when attacker speed=10, target speed=8 — from spec")]
    public void Spec() => Assert.Equal(0.25, Formulas.CalculateBlindsideThreshold(10, 8), 10);

    [Fact(DisplayName = "returns 0.5 when attacker speed=15, target speed=10")]
    public void Half() => Assert.Equal(0.5, Formulas.CalculateBlindsideThreshold(15, 10), 10);

    [Fact(DisplayName = "returns 0 when speeds are equal")]
    public void Equal_speeds() => Assert.Equal(0, Formulas.CalculateBlindsideThreshold(10, 10), 10);

    [Fact(DisplayName = "handles larger speed differences")]
    public void Larger() => Assert.Equal(1.0, Formulas.CalculateBlindsideThreshold(20, 10), 10);
}

public class CheckBlindside
{
    [Fact(DisplayName = "returns true when roll/20 >= (1 - threshold) — threshold=0.25, roll=16 from spec")]
    public void Spec() => Assert.True(Formulas.CheckBlindside(0.25, 16));

    [Fact(DisplayName = "returns false when roll is below threshold boundary")]
    public void Below() => Assert.False(Formulas.CheckBlindside(0.25, 14));

    [Fact(DisplayName = "returns true at the exact boundary")]
    public void Boundary() => Assert.True(Formulas.CheckBlindside(0.25, 15));

    [Fact(DisplayName = "returns false for threshold=0 (equal speeds)")]
    public void Zero_threshold() => Assert.False(Formulas.CheckBlindside(0, 19));

    [Fact(DisplayName = "returns true at high threshold with low roll")]
    public void High_threshold() => Assert.True(Formulas.CheckBlindside(0.9, 3));
}

public class CalculateCrushingBlowThreshold
{
    [Fact(DisplayName = "returns 0.25 when actionPower=125, targetPower=100")]
    public void Quarter() => Assert.Equal(0.25, Formulas.CalculateCrushingBlowThreshold(125, 100), 10);

    [Fact(DisplayName = "returns 0.5 when actionPower=150, targetPower=100")]
    public void Half() => Assert.Equal(0.5, Formulas.CalculateCrushingBlowThreshold(150, 100), 10);

    [Fact(DisplayName = "returns 0 when action power equals target power")]
    public void Equal_power() => Assert.Equal(0, Formulas.CalculateCrushingBlowThreshold(100, 100), 10);

    [Fact(DisplayName = "handles non-round numbers")]
    public void Non_round() => Assert.Equal(0.5, Formulas.CalculateCrushingBlowThreshold(120, 80), 10);
}

public class CheckCrushingBlow
{
    [Fact(DisplayName = "returns true when roll/20 >= (1 - threshold)")]
    public void Above() => Assert.True(Formulas.CheckCrushingBlow(0.25, 16));

    [Fact(DisplayName = "returns false when roll is below boundary")]
    public void Below() => Assert.False(Formulas.CheckCrushingBlow(0.25, 14));

    [Fact(DisplayName = "returns true at the exact boundary")]
    public void Boundary() => Assert.True(Formulas.CheckCrushingBlow(0.25, 15));

    [Fact(DisplayName = "returns false for threshold=0 (equal power)")]
    public void Zero_threshold() => Assert.False(Formulas.CheckCrushingBlow(0, 19));
}

public class CalculateBlockDamage
{
    [Fact(DisplayName = "returns damage * (1 - SMR) on block success — damage=100, SMR=0.4 → 60")]
    public void Success() => Assert.Equal(60, Formulas.CalculateBlockDamage(100, 0.4, 0.2, true), 10);

    [Fact(DisplayName = "returns damage * (1 - FMR) on block failure — damage=100, FMR=0.2 → 80")]
    public void Failure() => Assert.Equal(80, Formulas.CalculateBlockDamage(100, 0.4, 0.2, false), 10);

    [Fact(DisplayName = "handles SMR=0 on success — full damage if no mitigation")]
    public void Smr_zero() => Assert.Equal(100, Formulas.CalculateBlockDamage(100, 0, 0.1, true), 10);

    [Fact(DisplayName = "handles SMR=1 on success — zero damage if full mitigation")]
    public void Smr_one() => Assert.Equal(0, Formulas.CalculateBlockDamage(100, 1.0, 0.5, true), 10);

    [Fact(DisplayName = "handles FMR=0 on failure — full damage taken")]
    public void Fmr_zero() => Assert.Equal(100, Formulas.CalculateBlockDamage(100, 0.4, 0, false), 10);

    [Fact(DisplayName = "scales correctly with non-round damage values")]
    public void Non_round() => Assert.Equal(37.5, Formulas.CalculateBlockDamage(75, 0.5, 0.2, true), 10);
}

public class CalculateDodgeDamage
{
    [Fact(DisplayName = "returns damage * (1 - SMR) on dodge success — ADR-054")]
    public void Success() => Assert.Equal(20, Formulas.CalculateDodgeDamage(100, 0.8, 0.2, true), 10);

    [Fact(DisplayName = "returns damage * (1 - FMR) on dodge failure")]
    public void Failure() => Assert.Equal(80, Formulas.CalculateDodgeDamage(100, 0.8, 0.2, false), 10);

    [Fact(DisplayName = "scales success damage with SMR regardless of damage value")]
    public void Scales() => Assert.Equal(499.5, Formulas.CalculateDodgeDamage(999, 0.5, 0.5, true), 10);

    [Fact(DisplayName = "returns full damage on failure with FMR=0")]
    public void Fmr_zero() => Assert.Equal(100, Formulas.CalculateDodgeDamage(100, 0.8, 0, false), 10);

    [Fact(DisplayName = "scales correctly with non-round damage values")]
    public void Non_round() => Assert.Equal(42, Formulas.CalculateDodgeDamage(60, 0.8, 0.3, false), 10);
}

public class CalculateParryDamage
{
    [Fact(DisplayName = "returns damage * (1 - SMR) on parry success (counter still triggered) — ADR-054")]
    public void Success() => Assert.Equal(10, Formulas.CalculateParryDamage(100, 0.9, 0.2, true), 10);

    [Fact(DisplayName = "returns damage * (1 - FMR) on parry failure")]
    public void Failure() => Assert.Equal(80, Formulas.CalculateParryDamage(100, 0.9, 0.2, false), 10);

    [Fact(DisplayName = "scales success damage with SMR regardless of damage value")]
    public void Scales() => Assert.Equal(100, Formulas.CalculateParryDamage(500, 0.8, 0.3, true), 10);

    [Fact(DisplayName = "returns full damage on failure with FMR=0")]
    public void Fmr_zero() => Assert.Equal(100, Formulas.CalculateParryDamage(100, 0.9, 0, false), 10);
}

public class CalculateDefenselessDamage
{
    [Fact(DisplayName = "returns full damage (100%)")]
    public void Full() => Assert.Equal(100, Formulas.CalculateDefenselessDamage(100));

    [Fact(DisplayName = "passes through any damage value unchanged")]
    public void Passes_through()
    {
        Assert.Equal(250, Formulas.CalculateDefenselessDamage(250));
        Assert.Equal(0, Formulas.CalculateDefenselessDamage(0));
        Assert.Equal(37.5, Formulas.CalculateDefenselessDamage(37.5));
    }
}

// The TypeScript calculateBaseDamage took a target-power argument it never used; the C# one does not (T5 decision). The target argument is dropped from each call, the expected values are unchanged.
public class CalculateBaseDamage
{
    [Fact(DisplayName = "returns the attacker Power when powers are equal (100)")]
    public void Equal_powers() => Assert.Equal(100, Formulas.CalculateBaseDamage(100));

    [Fact(DisplayName = "returns the attacker Power regardless of a weaker target (150)")]
    public void Weaker_target() => Assert.Equal(150, Formulas.CalculateBaseDamage(150));

    [Fact(DisplayName = "returns the attacker Power regardless of a stronger target (80)")]
    public void Stronger_target() => Assert.Equal(80, Formulas.CalculateBaseDamage(80));

    [Fact(DisplayName = "adds a flat modifier (the Group N term): 100 + 10 = 110")]
    public void Flat_modifier() => Assert.Equal(110, Formulas.CalculateBaseDamage(100, 10));

    [Fact(DisplayName = "rounds to the nearest 0.25 per Math!O MROUND")]
    public void Rounds_to_quarter()
    {
        Assert.Equal(10.0, Formulas.CalculateBaseDamage(10.1));
        Assert.Equal(10.25, Formulas.CalculateBaseDamage(10.2));
    }

    [Fact(DisplayName = "ignores target power entirely (same result with any target)")]
    public void Ignores_target()
    {
        Assert.Equal(200, Formulas.CalculateBaseDamage(200));
        Assert.Equal(200, Formulas.CalculateBaseDamage(200));
        Assert.Equal(200, Formulas.CalculateBaseDamage(200));
    }
}

public class CalculateSpecialDamageBonus
{
    [Fact(DisplayName = "returns baseDamage * (1 + 0.10 * 1 segment) — 10% bonus for 1 segment")]
    public void One_segment() => Assert.Equal(110, Formulas.CalculateSpecialDamageBonus(100, 1), 10);

    [Fact(DisplayName = "returns baseDamage * (1 + 0.10 * 3) — 30% bonus for 3 segments")]
    public void Three_segments() => Assert.Equal(130, Formulas.CalculateSpecialDamageBonus(100, 3), 10);

    [Fact(DisplayName = "returns baseDamage * (1 + 0.10 * 5) — 50% bonus for 5 segments (max)")]
    public void Five_segments() => Assert.Equal(150, Formulas.CalculateSpecialDamageBonus(100, 5), 10);

    [Fact(DisplayName = "returns base damage unchanged with 0 segments")]
    public void Zero_segments() => Assert.Equal(100, Formulas.CalculateSpecialDamageBonus(100, 0), 10);

    [Fact(DisplayName = "scales correctly with non-round base damage")]
    public void Non_round() => Assert.Equal(90, Formulas.CalculateSpecialDamageBonus(75, 2), 10);
}

public class CalculateEvadeRegen
{
    [Fact(DisplayName = "returns maxStamina * 0.30 — from spec: maxStamina=100 → 30")]
    public void Spec() => Assert.Equal(30, Formulas.CalculateEvadeRegen(100), 10);

    [Fact(DisplayName = "returns 60 for maxStamina=200")]
    public void Two_hundred() => Assert.Equal(60, Formulas.CalculateEvadeRegen(200), 10);

    [Fact(DisplayName = "handles non-round max stamina values")]
    public void Non_round() => Assert.Equal(45, Formulas.CalculateEvadeRegen(150), 10);

    [Fact(DisplayName = "returns 0 for maxStamina=0")]
    public void Zero() => Assert.Equal(0, Formulas.CalculateEvadeRegen(0), 10);
}

// 'actionSuccess' and 'reactionSuccess' (the event kind) become EnergyEvent.Action and EnergyEvent.Reaction; 'success'/'failure' become a bool.
public class CalculateEnergyGain
{
    public class At_ascension_level_0_no_bonus
    {
        [Fact(DisplayName = "actionSuccess → 1.0 segments")]
        public void Action_success() => Assert.Equal(1.0, Formulas.CalculateEnergyGain(EnergyEvent.Action, true, AscensionLevel.Level0), 10);

        [Fact(DisplayName = "actionFailure → 0.5 segments")]
        public void Action_failure() => Assert.Equal(0.5, Formulas.CalculateEnergyGain(EnergyEvent.Action, false, AscensionLevel.Level0), 10);

        [Fact(DisplayName = "reactionSuccess → 0.5 segments")]
        public void Reaction_success() => Assert.Equal(0.5, Formulas.CalculateEnergyGain(EnergyEvent.Reaction, true, AscensionLevel.Level0), 10);

        [Fact(DisplayName = "reactionFailure → 0.25 segments")]
        public void Reaction_failure() => Assert.Equal(0.25, Formulas.CalculateEnergyGain(EnergyEvent.Reaction, false, AscensionLevel.Level0), 10);
    }

    public class At_ascension_level_1_plus_25_percent_bonus
    {
        [Fact(DisplayName = "actionSuccess → 1.0 * 1.25 = 1.25 segments")]
        public void Action_success() => Assert.Equal(1.25, Formulas.CalculateEnergyGain(EnergyEvent.Action, true, AscensionLevel.Level1), 10);

        [Fact(DisplayName = "actionFailure → 0.5 * 1.25 = 0.625 segments")]
        public void Action_failure() => Assert.Equal(0.625, Formulas.CalculateEnergyGain(EnergyEvent.Action, false, AscensionLevel.Level1), 10);

        [Fact(DisplayName = "reactionSuccess → 0.5 * 1.25 = 0.625 segments")]
        public void Reaction_success() => Assert.Equal(0.625, Formulas.CalculateEnergyGain(EnergyEvent.Reaction, true, AscensionLevel.Level1), 10);

        [Fact(DisplayName = "reactionFailure → 0.25 * 1.25 = 0.3125 segments")]
        public void Reaction_failure() => Assert.Equal(0.3125, Formulas.CalculateEnergyGain(EnergyEvent.Reaction, false, AscensionLevel.Level1), 10);
    }

    public class At_ascension_level_2_plus_25_percent_bonus
    {
        [Fact(DisplayName = "actionSuccess → 1.0 * 1.25 = 1.25 segments")]
        public void Action_success() => Assert.Equal(1.25, Formulas.CalculateEnergyGain(EnergyEvent.Action, true, AscensionLevel.Level2), 10);

        [Fact(DisplayName = "reactionFailure → 0.25 * 1.25 = 0.3125 segments")]
        public void Reaction_failure() => Assert.Equal(0.3125, Formulas.CalculateEnergyGain(EnergyEvent.Reaction, false, AscensionLevel.Level2), 10);
    }

    public class At_ascension_level_3_plus_50_percent_bonus
    {
        [Fact(DisplayName = "actionSuccess → 1.0 * 1.50 = 1.5 segments")]
        public void Action_success() => Assert.Equal(1.5, Formulas.CalculateEnergyGain(EnergyEvent.Action, true, AscensionLevel.Level3), 10);

        [Fact(DisplayName = "actionFailure → 0.5 * 1.50 = 0.75 segments")]
        public void Action_failure() => Assert.Equal(0.75, Formulas.CalculateEnergyGain(EnergyEvent.Action, false, AscensionLevel.Level3), 10);

        [Fact(DisplayName = "reactionSuccess → 0.5 * 1.50 = 0.75 segments")]
        public void Reaction_success() => Assert.Equal(0.75, Formulas.CalculateEnergyGain(EnergyEvent.Reaction, true, AscensionLevel.Level3), 10);

        [Fact(DisplayName = "reactionFailure → 0.25 * 1.50 = 0.375 segments")]
        public void Reaction_failure() => Assert.Equal(0.375, Formulas.CalculateEnergyGain(EnergyEvent.Reaction, false, AscensionLevel.Level3), 10);
    }
}

public class CalculateAscensionLevel
{
    [Fact(DisplayName = "returns 0 for segments < 35")]
    public void Below_35()
    {
        Assert.Equal(AscensionLevel.Level0, Formulas.CalculateAscensionLevel(0));
        Assert.Equal(AscensionLevel.Level0, Formulas.CalculateAscensionLevel(34));
    }

    [Fact(DisplayName = "returns 1 at exactly 35 segments")]
    public void At_35() => Assert.Equal(AscensionLevel.Level1, Formulas.CalculateAscensionLevel(35));

    [Fact(DisplayName = "returns 1 for segments between 35 and 94 inclusive")]
    public void Between_35_and_94()
    {
        Assert.Equal(AscensionLevel.Level1, Formulas.CalculateAscensionLevel(60));
        Assert.Equal(AscensionLevel.Level1, Formulas.CalculateAscensionLevel(94));
    }

    [Fact(DisplayName = "returns 2 at exactly 95 segments")]
    public void At_95() => Assert.Equal(AscensionLevel.Level2, Formulas.CalculateAscensionLevel(95));

    [Fact(DisplayName = "returns 2 for segments between 95 and 179 inclusive")]
    public void Between_95_and_179()
    {
        Assert.Equal(AscensionLevel.Level2, Formulas.CalculateAscensionLevel(150));
        Assert.Equal(AscensionLevel.Level2, Formulas.CalculateAscensionLevel(179));
    }

    [Fact(DisplayName = "returns 3 at exactly 180 segments")]
    public void At_180() => Assert.Equal(AscensionLevel.Level3, Formulas.CalculateAscensionLevel(180));

    [Fact(DisplayName = "returns 3 for segments above 180")]
    public void Above_180()
    {
        Assert.Equal(AscensionLevel.Level3, Formulas.CalculateAscensionLevel(250));
        Assert.Equal(AscensionLevel.Level3, Formulas.CalculateAscensionLevel(1000));
    }
}

// Buffs are Buff(BuffStat, source, modifier) (no duration); a debuff is a buff with a negative modifier, so there is no separate debuff list.
public class ApplyDynamicModifiers
{
    private static readonly ModifiedStats BaseStats = new(100, 10, 0.5, 0.4, 0.2, 0.4, 0.8, 0.2, 0.3, 0.9, 0.15);

    [Fact(DisplayName = "returns base stats unchanged when no buffs or debuffs")]
    public void No_buffs()
    {
        var result = Formulas.ApplyDynamicModifiers(BaseStats, ImmutableArray<Buff>.Empty);
        Assert.Equal(BaseStats, result);
    }

    [Fact(DisplayName = "applies a single buff to the correct stat")]
    public void Single_buff()
    {
        var result = Formulas.ApplyDynamicModifiers(BaseStats, [new Buff(BuffStat.Power, "fire_path", 20)]);
        Assert.Equal(120, result.Power, 10);
        Assert.Equal(10, result.Speed);
    }

    [Fact(DisplayName = "applies a single debuff reducing the correct stat")]
    public void Single_debuff()
    {
        var result = Formulas.ApplyDynamicModifiers(BaseStats, [new Buff(BuffStat.BlockSR, "earth_path", -0.1)]);
        Assert.Equal(0.4, result.BlockSR, 10);
        Assert.Equal(100, result.Power);
    }

    [Fact(DisplayName = "stacks multiple buffs on the same stat additively")]
    public void Stacks()
    {
        var result = Formulas.ApplyDynamicModifiers(BaseStats, [new Buff(BuffStat.Power, "buff1", 10), new Buff(BuffStat.Power, "buff2", 15)]);
        Assert.Equal(125, result.Power, 10);
    }

    [Fact(DisplayName = "applies buffs and debuffs independently without interference")]
    public void Independent()
    {
        var result = Formulas.ApplyDynamicModifiers(BaseStats, [new Buff(BuffStat.ParrySR, "fire_path", 0.1), new Buff(BuffStat.BlockSR, "earth_path", -0.15)]);
        Assert.Equal(0.4, result.ParrySR, 10);
        Assert.Equal(0.35, result.BlockSR, 10);
    }

    [Fact(DisplayName = "returns a new object — does not mutate baseStats")]
    public void Does_not_mutate()
    {
        var result = Formulas.ApplyDynamicModifiers(BaseStats, [new Buff(BuffStat.Power, "test", 50)]);
        Assert.NotSame(BaseStats, result);
        Assert.Equal(100, BaseStats.Power);
    }
}
