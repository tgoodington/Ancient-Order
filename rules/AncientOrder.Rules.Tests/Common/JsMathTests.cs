using AncientOrder.Rules.Common;

namespace AncientOrder.Rules.Tests.Common;

public class JsMathTests
{
    [Theory]
    [InlineData(2.5, 3.0)]
    [InlineData(0.5, 1.0)]
    [InlineData(1.5, 2.0)]
    [InlineData(-2.5, -2.0)]
    [InlineData(-1.5, -1.0)]
    [InlineData(2.4, 2.0)]
    [InlineData(-2.6, -3.0)]
    [InlineData(0.49999999999999994, 0.0)]
    [InlineData(1000000000000000.5, 1000000000000001.0)]
    public void Round_matches_JavaScript_Math_round(double input, double expected)
    {
        Assert.Equal(expected, JsMath.Round(input));
    }

    [Fact]
    public void Round_of_negative_half_is_zero()
    {
        Assert.Equal(0.0, JsMath.Round(-0.5));
    }

    [Theory]
    [InlineData(1.005, 1.0)]
    [InlineData(0.285, 0.28)]
    [InlineData(1.255, 1.25)]
    [InlineData(2.675, 2.68)]
    [InlineData(10.125, 10.13)]
    [InlineData(33.335, 33.34)]
    public void Round_to_two_decimals_matches_JavaScript(double input, double expected)
    {
        Assert.Equal(expected, JsMath.Round(input * 100) / 100);
    }
}
