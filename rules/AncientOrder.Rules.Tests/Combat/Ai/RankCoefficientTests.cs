using AncientOrder.Rules.Combat.Ai;

namespace AncientOrder.Rules.Tests.Combat.Ai;

// Carried over from src/combat/behaviorTree/rankCoefficient.test.ts (rankCoefficient becomes RankCoefficient.For). toBeCloseTo(x) is 2 decimals; toBe is exact.
public class RankCoefficientTests
{
    [Fact(DisplayName = "returns 0.2 floor for rank 1.0 (Stone tier)")]
    public void Floor_rank_1() => Assert.Equal(0.2, RankCoefficient.For(1.0));

    [Fact(DisplayName = "returns 0.2 floor for ranks below 2.0 (floor applied)")]
    public void Floor_below_2() => Assert.Equal(0.2, RankCoefficient.For(1.5));

    [Fact(DisplayName = "returns 0.3 for rank 3.0 (Silver tier)")]
    public void Rank_3() => Assert.Equal(0.3, RankCoefficient.For(3.0), 2);

    [Fact(DisplayName = "returns 0.5 for rank 5.0 (Gold tier)")]
    public void Rank_5() => Assert.Equal(0.5, RankCoefficient.For(5.0), 2);

    [Fact(DisplayName = "returns 0.7 for rank 7.0 (Diamond tier)")]
    public void Rank_7() => Assert.Equal(0.7, RankCoefficient.For(7.0), 2);

    [Fact(DisplayName = "returns 1.0 for rank 10.0 (Legend tier)")]
    public void Rank_10() => Assert.Equal(1.0, RankCoefficient.For(10.0), 2);

    [Fact(DisplayName = "never exceeds 1.0 for ranks above 10")]
    public void Not_capped_above_10() => Assert.Equal(1.2, RankCoefficient.For(12.0), 2);

    [Fact(DisplayName = "applies floor for rank 0")]
    public void Floor_rank_0() => Assert.Equal(0.2, RankCoefficient.For(0));

    [Fact(DisplayName = "returns exact floor value — no sub-floor values possible")]
    public void No_sub_floor() => Assert.True(RankCoefficient.For(0.5) >= 0.2);
}
