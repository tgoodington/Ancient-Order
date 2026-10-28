using AncientOrder.Rules.Common;

namespace AncientOrder.Rules.Tests.Common;

public class SeededRollsTests
{
    private static readonly double[] Golden =
    [
        14.876163263131787,
        2.6009106925566905,
        19.266689860257088,
        0.9668022967269163,
        11.103657106529123,
        0.21356118900748067,
        3.195546045545039,
        5.916089768158901,
    ];

    [Fact]
    public void Seed_expands_through_SplitMix64()
    {
        var state = DiceState.FromSeed(12345);

        Assert.Equal(
            new DiceState(2454886589211414944UL, 3778200017661327597UL, 2205171434679333405UL, 3248800117070709450UL),
            state);
    }

    [Fact]
    public void Fixed_seed_produces_the_pinned_sequence()
    {
        var rolls = SeededRolls.FromSeed(12345);

        var actual = Golden.Select(_ => rolls.Roll()).ToArray();

        Assert.Equal(Golden, actual);
    }

    [Fact]
    public void Rolls_fall_in_zero_to_twenty()
    {
        var rolls = SeededRolls.FromSeed(987654321);

        for (var i = 0; i < 100_000; i++)
        {
            var roll = rolls.Roll();
            Assert.InRange(roll, 0.0, 20.0);
            Assert.NotEqual(20.0, roll);
        }
    }

    [Fact]
    public void Resuming_from_a_saved_state_continues_the_identical_sequence()
    {
        var original = SeededRolls.FromSeed(12345);
        for (var i = 0; i < 3; i++)
        {
            original.Roll();
        }

        var saved = original.State;
        Assert.Equal(
            new DiceState(17276488875664723542UL, 13703907918251744994UL, 14762911507732663688UL, 3565876056189478702UL),
            saved);

        var resumed = SeededRolls.FromState(saved);
        for (var i = 3; i < Golden.Length; i++)
        {
            Assert.Equal(Golden[i], resumed.Roll());
            Assert.Equal(Golden[i], original.Roll());
        }
    }

    [Fact]
    public void State_is_an_immutable_snapshot()
    {
        var rolls = SeededRolls.FromSeed(1);
        var before = rolls.State;

        rolls.Roll();

        Assert.NotEqual(before, rolls.State);
        Assert.Equal(DiceState.FromSeed(1), before);
    }

    [Fact]
    public void All_zero_state_is_rejected()
    {
        Assert.Throws<ArgumentException>(() => SeededRolls.FromState(new DiceState(0, 0, 0, 0)));
    }
}
