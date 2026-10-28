using System.Collections.Immutable;
using AncientOrder.Rules.Combat;
using AncientOrder.Rules.Combat.Ai;

namespace AncientOrder.Rules.Tests.Combat.Ai;

// Carried over from src/combat/behaviorTree/factors/factors.test.ts. factor.evaluate(p, null) becomes factor.Evaluate(p, null); the FACTORS registry is
// ScoringFactors.All. toBeCloseTo(x) with no precision is 2 decimals; toBeCloseTo(x, 1) is precision 1.
internal static class FactorFixtures
{
    public static CombatPerception Perception(
        double selfStaminaPct = 0.8,
        double selfEnergy = 0,
        double lowestAllyStaminaPct = 1.0,
        double teamAvgStaminaPct = 0.8,
        int allyCount = 0,
        double enemyAvgStaminaPct = 0.5,
        int round = 1) =>
        new(
            "self",
            selfStaminaPct,
            selfEnergy,
            AscensionLevel.Level0,
            3.0,
            ElementalPath.Fire,
            [],
            lowestAllyStaminaPct,
            teamAvgStaminaPct,
            allyCount,
            [],
            0.5,
            enemyAvgStaminaPct,
            1,
            round);

    public static TargetPerception Target(double staminaPct = 0.5, double speedDelta = 0) => new("target", staminaPct, speedDelta, 0, 20);
}

public class OwnStaminaFactorTests
{
    private static readonly OwnStaminaFactor Factor = new();

    [Fact(DisplayName = "at selfStaminaPct=0.0: EVADE score is 0.9 (low bracket start)")]
    public void Zero_evade()
    {
        var scores = Factor.Evaluate(FactorFixtures.Perception(selfStaminaPct: 0.0), null);
        Assert.Equal(0.9, scores.Evade, 2);
        Assert.Equal(-0.5, scores.Attack, 2);
    }

    [Fact(DisplayName = "at selfStaminaPct=0.0: ATTACK score is -0.5 (low bracket start)")]
    public void Zero_attack()
    {
        var scores = Factor.Evaluate(FactorFixtures.Perception(selfStaminaPct: 0.0), null);
        Assert.Equal(-0.5, scores.Attack, 2);
    }

    [Fact(DisplayName = "low bracket interior (pct=0.15): EVADE and ATTACK interpolated (t=0.5 between LOW and MID)")]
    public void Low_interior()
    {
        var scores = Factor.Evaluate(FactorFixtures.Perception(selfStaminaPct: 0.15), null);
        Assert.Equal(0.55, scores.Evade, 2);
        Assert.Equal(-0.25, scores.Attack, 2);
    }

    [Fact(DisplayName = "at selfStaminaPct=0.3: transitions to mid bracket (EVADE near 0.2, ATTACK near 0)")]
    public void Low_to_mid()
    {
        var scores = Factor.Evaluate(FactorFixtures.Perception(selfStaminaPct: 0.3), null);
        Assert.Equal(0.2, scores.Evade, 2);
        Assert.Equal(0.0, scores.Attack, 2);
    }

    [Fact(DisplayName = "mid bracket interior (pct=0.45): EVADE and ATTACK interpolated (t=0.5 between MID and HIGH)")]
    public void Mid_interior()
    {
        var scores = Factor.Evaluate(FactorFixtures.Perception(selfStaminaPct: 0.45), null);
        Assert.Equal(-0.05, scores.Evade, 2);
        Assert.Equal(0.1, scores.Attack, 2);
    }

    [Fact(DisplayName = "at selfStaminaPct=0.6: transitions to high bracket (ATTACK near 0.2, EVADE near -0.3)")]
    public void Mid_to_high()
    {
        var scores = Factor.Evaluate(FactorFixtures.Perception(selfStaminaPct: 0.6), null);
        Assert.Equal(0.2, scores.Attack, 2);
        Assert.Equal(-0.3, scores.Evade, 2);
    }

    [Fact(DisplayName = "high stamina (> 0.6): returns HIGH_SCORES flat")]
    public void High()
    {
        var scores = Factor.Evaluate(FactorFixtures.Perception(selfStaminaPct: 0.9), null);
        Assert.Equal(0.2, scores.Attack, 2);
        Assert.Equal(-0.3, scores.Evade, 2);
        Assert.Equal(0.1, scores.Special, 2);
    }

    [Fact(DisplayName = "GROUP score is always 0 in all brackets")]
    public void Group_zero()
    {
        foreach (var pct in new[] { 0.0, 0.15, 0.45, 0.9 })
        {
            Assert.Equal(0, Factor.Evaluate(FactorFixtures.Perception(selfStaminaPct: pct), null).Group, 2);
        }
    }
}

public class AllyInDangerFactorTests
{
    private static readonly AllyInDangerFactor Factor = new();

    [Fact(DisplayName = "critical ally danger (< 0.3): DEFEND score near 0.8")]
    public void Critical_defend() =>
        Assert.Equal(0.8, Factor.Evaluate(FactorFixtures.Perception(lowestAllyStaminaPct: 0.1, allyCount: 1), null).Defend, 1);

    [Fact(DisplayName = "critical ally danger: ATTACK score negative")]
    public void Critical_attack() =>
        Assert.True(Factor.Evaluate(FactorFixtures.Perception(lowestAllyStaminaPct: 0.15, allyCount: 1), null).Attack < 0);

    [Fact(DisplayName = "moderate ally danger (0.3-0.6): DEFEND score near 0.3")]
    public void Moderate_defend() =>
        Assert.Equal(0.3, Factor.Evaluate(FactorFixtures.Perception(lowestAllyStaminaPct: 0.45, allyCount: 1), null).Defend, 1);

    [Fact(DisplayName = "healthy allies (> 0.6): all zeros")]
    public void Healthy()
    {
        var scores = Factor.Evaluate(FactorFixtures.Perception(lowestAllyStaminaPct: 0.9, allyCount: 1), null);
        Assert.Equal(0, scores.Attack, 2);
        Assert.Equal(0, scores.Defend, 2);
        Assert.Equal(0, scores.Evade, 2);
        Assert.Equal(0, scores.Special, 2);
    }

    [Fact(DisplayName = "no allies: returns all zeros regardless of lowestAllyStaminaPct")]
    public void No_allies()
    {
        var scores = Factor.Evaluate(FactorFixtures.Perception(lowestAllyStaminaPct: 0.1, allyCount: 0), null);
        Assert.Equal(0, scores.Defend, 2);
        Assert.Equal(0, scores.Attack, 2);
    }
}

public class TargetVulnerabilityFactorTests
{
    private static readonly TargetVulnerabilityFactor Factor = new();

    [Fact(DisplayName = "null target: returns all zeros")]
    public void Null_target()
    {
        var scores = Factor.Evaluate(FactorFixtures.Perception(), null);
        Assert.Equal(0, scores.Attack, 2);
        Assert.Equal(0, scores.Defend, 2);
        Assert.Equal(0, scores.Special, 2);
    }

    [Fact(DisplayName = "critical target (< 0.25): ATTACK near 0.8")]
    public void Critical_attack() => Assert.Equal(0.8, Factor.Evaluate(FactorFixtures.Perception(), FactorFixtures.Target(staminaPct: 0.1)).Attack, 1);

    [Fact(DisplayName = "critical target: SPECIAL near 0.6")]
    public void Critical_special() => Assert.Equal(0.6, Factor.Evaluate(FactorFixtures.Perception(), FactorFixtures.Target(staminaPct: 0.1)).Special, 1);

    [Fact(DisplayName = "moderate target (0.25-0.5): ATTACK near 0.4")]
    public void Moderate_attack() => Assert.Equal(0.4, Factor.Evaluate(FactorFixtures.Perception(), FactorFixtures.Target(staminaPct: 0.35)).Attack, 1);

    [Fact(DisplayName = "healthy target (> 0.5): ATTACK near 0.1")]
    public void Healthy_attack() => Assert.Equal(0.1, Factor.Evaluate(FactorFixtures.Perception(), FactorFixtures.Target(staminaPct: 0.9)).Attack, 1);

    [Fact(DisplayName = "healthy target: DEFEND is 0")]
    public void Healthy_defend() => Assert.Equal(0, Factor.Evaluate(FactorFixtures.Perception(), FactorFixtures.Target(staminaPct: 0.8)).Defend, 2);
}

public class EnergyAvailabilityFactorTests
{
    private static readonly EnergyAvailabilityFactor Factor = new();

    [Fact(DisplayName = "high energy (>= 3): SPECIAL near 0.7")]
    public void High_special() => Assert.Equal(0.7, Factor.Evaluate(FactorFixtures.Perception(selfEnergy: 4), null).Special, 2);

    [Fact(DisplayName = "high energy: EVADE penalized (-0.2)")]
    public void High_evade() => Assert.Equal(-0.2, Factor.Evaluate(FactorFixtures.Perception(selfEnergy: 3), null).Evade, 2);

    [Fact(DisplayName = "mid energy (1-2): SPECIAL near 0.3")]
    public void Mid_special() => Assert.Equal(0.3, Factor.Evaluate(FactorFixtures.Perception(selfEnergy: 2), null).Special, 2);

    [Fact(DisplayName = "mid energy: ATTACK and EVADE are 0")]
    public void Mid_attack_evade()
    {
        var scores = Factor.Evaluate(FactorFixtures.Perception(selfEnergy: 1), null);
        Assert.Equal(0, scores.Attack, 2);
        Assert.Equal(0, scores.Evade, 2);
    }

    [Fact(DisplayName = "zero energy: ATTACK and EVADE are 0.1")]
    public void Zero_attack_evade()
    {
        var scores = Factor.Evaluate(FactorFixtures.Perception(selfEnergy: 0), null);
        Assert.Equal(0.1, scores.Attack, 2);
        Assert.Equal(0.1, scores.Evade, 2);
    }

    [Fact(DisplayName = "zero energy: SPECIAL is 0")]
    public void Zero_special() => Assert.Equal(0, Factor.Evaluate(FactorFixtures.Perception(selfEnergy: 0), null).Special, 2);
}

public class SpeedAdvantageFactorTests
{
    private static readonly SpeedAdvantageFactor Factor = new();

    [Fact(DisplayName = "null target: returns all zeros")]
    public void Null_target()
    {
        var scores = Factor.Evaluate(FactorFixtures.Perception(), null);
        Assert.Equal(0, scores.Attack, 2);
        Assert.Equal(0, scores.Evade, 2);
    }

    [Fact(DisplayName = "significantly faster (speedDelta > 0.3): ATTACK near 0.6")]
    public void Faster_attack() => Assert.Equal(0.6, Factor.Evaluate(FactorFixtures.Perception(), FactorFixtures.Target(speedDelta: 0.5)).Attack, 1);

    [Fact(DisplayName = "significantly faster: SPECIAL near 0.3")]
    public void Faster_special() => Assert.Equal(0.3, Factor.Evaluate(FactorFixtures.Perception(), FactorFixtures.Target(speedDelta: 0.6)).Special, 1);

    [Fact(DisplayName = "slight advantage (speedDelta 0-0.3): ATTACK near 0.2")]
    public void Slight_attack() => Assert.Equal(0.2, Factor.Evaluate(FactorFixtures.Perception(), FactorFixtures.Target(speedDelta: 0.0)).Attack, 1);

    [Fact(DisplayName = "slower (speedDelta < 0): ATTACK negative, DEFEND and EVADE positive")]
    public void Slower()
    {
        var scores = Factor.Evaluate(FactorFixtures.Perception(), FactorFixtures.Target(speedDelta: -0.3));
        Assert.True(scores.Attack < 0);
        Assert.True(scores.Defend > 0);
        Assert.True(scores.Evade > 0);
    }
}

public class RoundPhaseFactorTests
{
    private static readonly RoundPhaseFactor Factor = new();

    [Fact(DisplayName = "early rounds (1-2): EVADE positive and highest offensive type")]
    public void Early()
    {
        var s1 = Factor.Evaluate(FactorFixtures.Perception(round: 1), null);
        Assert.Equal(0.3, s1.Evade, 2);
        Assert.True(s1.Special < 0);
    }

    [Fact(DisplayName = "early round 2: same as round 1")]
    public void Early_round_2()
    {
        var s1 = Factor.Evaluate(FactorFixtures.Perception(round: 1), null);
        var s2 = Factor.Evaluate(FactorFixtures.Perception(round: 2), null);
        Assert.Equal(s1.Attack, s2.Attack, 2);
    }

    [Fact(DisplayName = "mid rounds (3-5): ATTACK and SPECIAL both 0.2")]
    public void Mid()
    {
        var scores = Factor.Evaluate(FactorFixtures.Perception(round: 4), null);
        Assert.Equal(0.2, scores.Attack, 2);
        Assert.Equal(0.2, scores.Special, 2);
        Assert.Equal(0, scores.Defend, 2);
        Assert.Equal(0, scores.Evade, 2);
    }

    [Fact(DisplayName = "late rounds (6+): SPECIAL highest at 0.4")]
    public void Late()
    {
        var scores = Factor.Evaluate(FactorFixtures.Perception(round: 8), null);
        Assert.Equal(0.4, scores.Special, 2);
        Assert.Equal(0.3, scores.Attack, 2);
        Assert.True(scores.Defend < 0);
        Assert.True(scores.Evade < 0);
    }

    [Fact(DisplayName = "boundary round 3 is mid")]
    public void Boundary_3() => Assert.Equal(0.2, Factor.Evaluate(FactorFixtures.Perception(round: 3), null).Attack, 2);

    [Fact(DisplayName = "boundary round 6 is late")]
    public void Boundary_6() => Assert.Equal(0.4, Factor.Evaluate(FactorFixtures.Perception(round: 6), null).Special, 2);
}

public class TeamBalanceFactorTests
{
    private static readonly TeamBalanceFactor Factor = new();

    [Fact(DisplayName = "winning (teamAvg > enemyAvg + 0.2): ATTACK positive")]
    public void Winning()
    {
        var scores = Factor.Evaluate(FactorFixtures.Perception(teamAvgStaminaPct: 0.8, enemyAvgStaminaPct: 0.4), null);
        Assert.Equal(0.3, scores.Attack, 2);
        Assert.Equal(0.2, scores.Special, 2);
    }

    [Fact(DisplayName = "winning: DEFEND and EVADE penalized")]
    public void Winning_penalties()
    {
        var scores = Factor.Evaluate(FactorFixtures.Perception(teamAvgStaminaPct: 0.9, enemyAvgStaminaPct: 0.5), null);
        Assert.True(scores.Defend < 0);
        Assert.True(scores.Evade < 0);
    }

    [Fact(DisplayName = "roughly even (±0.2): all zeros")]
    public void Even()
    {
        var scores = Factor.Evaluate(FactorFixtures.Perception(teamAvgStaminaPct: 0.6, enemyAvgStaminaPct: 0.6), null);
        Assert.Equal(0, scores.Attack, 2);
        Assert.Equal(0, scores.Defend, 2);
        Assert.Equal(0, scores.Evade, 2);
    }

    [Fact(DisplayName = "losing (teamAvg < enemyAvg - 0.2): DEFEND highest")]
    public void Losing()
    {
        var scores = Factor.Evaluate(FactorFixtures.Perception(teamAvgStaminaPct: 0.3, enemyAvgStaminaPct: 0.7), null);
        Assert.Equal(0.4, scores.Defend, 2);
        Assert.Equal(0.3, scores.Evade, 2);
        Assert.True(scores.Attack < 0);
    }

    [Fact(DisplayName = "losing: GROUP score is 0.2")]
    public void Losing_group()
    {
        var scores = Factor.Evaluate(FactorFixtures.Perception(teamAvgStaminaPct: 0.2, enemyAvgStaminaPct: 0.9), null);
        Assert.Equal(0.2, scores.Group, 2);
    }
}

public class FactorsRegistry
{
    [Fact(DisplayName = "contains exactly 7 factors")]
    public void Seven() => Assert.Equal(7, ScoringFactors.All.Length);

    [Fact(DisplayName = "all factors have unique names")]
    public void Unique_names()
    {
        var names = ScoringFactors.All.Select(f => f.Name).ToArray();
        Assert.Equal(names.Length, names.Distinct().Count());
    }

    [Fact(DisplayName = "all factors implement evaluate()")]
    public void All_evaluate()
    {
        foreach (var factor in ScoringFactors.All)
        {
            Assert.NotNull(factor.Evaluate(FactorFixtures.Perception(), null));
        }
    }

    [Fact(DisplayName = "all factors have expected names")]
    public void Expected_names()
    {
        ImmutableArray<string> names = [.. ScoringFactors.All.Select(f => f.Name)];
        Assert.Contains("ownStamina", names);
        Assert.Contains("allyInDanger", names);
        Assert.Contains("targetVulnerability", names);
        Assert.Contains("energyAvailability", names);
        Assert.Contains("speedAdvantage", names);
        Assert.Contains("roundPhase", names);
        Assert.Contains("teamBalance", names);
    }
}
