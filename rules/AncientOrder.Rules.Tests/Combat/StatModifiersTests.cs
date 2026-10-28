using System.Collections.Immutable;
using AncientOrder.Rules.Combat;
using AncientOrder.Rules.Common;

namespace AncientOrder.Rules.Tests.Combat;

public class StatModifiersTests
{
    private static ImmutableArray<StatModifier> Single(StatStep step, double value) =>
        [new StatModifier(CombatStat.Power, step, value, "test")];

    [Fact]
    public void Single_multiply_1_10_is_bit_identical_to_round_of_stat_times_1_10_for_every_integer_stat()
    {
        var modifiers = Single(StatStep.Multiply, 1.10);
        for (var stat = 1; stat <= 500; stat++)
        {
            Assert.Equal(BitConverter.DoubleToInt64Bits(JsMath.Round(stat * 1.10)), BitConverter.DoubleToInt64Bits(StatModifiers.Apply(stat, modifiers)));
        }
    }

    [Fact]
    public void Single_multiply_1_10_is_bit_identical_for_a_spread_of_fractional_stats()
    {
        var modifiers = Single(StatStep.Multiply, 1.10);
        for (var i = 1; i <= 5000; i++)
        {
            var stat = i * 0.1;
            Assert.Equal(BitConverter.DoubleToInt64Bits(JsMath.Round(stat * 1.10)), BitConverter.DoubleToInt64Bits(StatModifiers.Apply(stat, modifiers)));
        }

        foreach (var stat in new[] { 0.05, 0.5, 2.5, 4.55, 12.345, 33.333333, 99.95, 100.0 / 3, 249.5, 454.54545 })
        {
            Assert.Equal(BitConverter.DoubleToInt64Bits(JsMath.Round(stat * 1.10)), BitConverter.DoubleToInt64Bits(StatModifiers.Apply(stat, modifiers)));
        }
    }

    [Fact]
    public void A_stat_with_no_modifiers_is_left_untouched_and_not_rounded()
    {
        Assert.Equal(12.34, StatModifiers.Apply(12.34, ImmutableArray<StatModifier>.Empty));
        Assert.Equal(12.34, StatModifiers.Apply(12.34, default));
        Assert.Equal(12.34, StatModifiers.Apply(CombatStat.Speed, 12.34, Single(StatStep.Multiply, 1.10)));
    }

    [Fact]
    public void Flat_then_add_percent_then_multiply_with_one_rounding_at_the_end()
    {
        ImmutableArray<StatModifier> modifiers =
        [
            new StatModifier(CombatStat.Power, StatStep.Multiply, 1.5, "m"),
            new StatModifier(CombatStat.Power, StatStep.AddPercent, 0.10, "a1"),
            new StatModifier(CombatStat.Power, StatStep.Flat, 3, "f1"),
            new StatModifier(CombatStat.Power, StatStep.AddPercent, 0.20, "a2"),
            new StatModifier(CombatStat.Power, StatStep.Flat, 2, "f2"),
        ];

        // ((10 + 5) * 1.30) * 1.5 = 29.25, rounded once.
        Assert.Equal(29.0, StatModifiers.Apply(10, modifiers));
    }

    [Fact]
    public void Apply_by_stat_only_uses_that_stats_modifiers()
    {
        ImmutableArray<StatModifier> modifiers =
        [
            new StatModifier(CombatStat.Power, StatStep.Multiply, 1.10, "p"),
            new StatModifier(CombatStat.Speed, StatStep.Flat, 5, "s"),
        ];

        Assert.Equal(11.0, StatModifiers.Apply(CombatStat.Power, 10, modifiers));
        Assert.Equal(15.0, StatModifiers.Apply(CombatStat.Speed, 10, modifiers));
        Assert.Equal(10.0, StatModifiers.Apply(CombatStat.Rank, 10, modifiers));
    }
}
