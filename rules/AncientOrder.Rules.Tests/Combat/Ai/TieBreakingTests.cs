using AncientOrder.Rules.Combat;
using AncientOrder.Rules.Combat.Ai;

namespace AncientOrder.Rules.Tests.Combat.Ai;

// Carried over from src/combat/behaviorTree/tieBreaking.test.ts. The path argument is the combatant's own path (the old profile path is gone);
// every path in these tests is passed directly, so no expected value changes. The candidate has no scoreBreakdown field (not ported).
public class PathTiebreakTable
{
    [Fact(DisplayName = "Fire path — ATTACK first (offensive-leaning)")]
    public void Fire() => Assert.Equal(ActionType.Attack, TieBreaking.PathTiebreak(ElementalPath.Fire)[0]);

    [Fact(DisplayName = "Shadow path — SPECIAL first")]
    public void Shadow() => Assert.Equal(ActionType.Special, TieBreaking.PathTiebreak(ElementalPath.Shadow)[0]);

    [Fact(DisplayName = "Earth path — DEFEND first among action paths")]
    public void Earth() => Assert.Equal(ActionType.Defend, TieBreaking.PathTiebreak(ElementalPath.Earth)[0]);

    [Fact(DisplayName = "Water path — DEFEND first (defensive-leaning)")]
    public void Water() => Assert.Equal(ActionType.Defend, TieBreaking.PathTiebreak(ElementalPath.Water)[0]);

    [Fact(DisplayName = "Air path — EVADE first")]
    public void Air() => Assert.Equal(ActionType.Evade, TieBreaking.PathTiebreak(ElementalPath.Air)[0]);

    [Fact(DisplayName = "Light path — SPECIAL first")]
    public void Light() => Assert.Equal(ActionType.Special, TieBreaking.PathTiebreak(ElementalPath.Light)[0]);

    [Fact(DisplayName = "all paths include all 5 action types")]
    public void All_action_types()
    {
        ElementalPath[] paths = [ElementalPath.Fire, ElementalPath.Shadow, ElementalPath.Earth, ElementalPath.Water, ElementalPath.Air, ElementalPath.Light];
        foreach (var path in paths)
        {
            var order = TieBreaking.PathTiebreak(path);
            Assert.Equal(5, order.Length);
            Assert.Contains(ActionType.Attack, order);
            Assert.Contains(ActionType.Defend, order);
            Assert.Contains(ActionType.Evade, order);
            Assert.Contains(ActionType.Special, order);
            Assert.Contains(ActionType.Group, order);
        }
    }
}

public class ResolveTie
{
    private static ScoredCandidate Candidate(ActionType type, string? targetId, double score) => new(type, targetId, score);

    [Fact(DisplayName = "picks ATTACK over DEFEND for Fire path when tied")]
    public void Fire_attack_over_defend()
    {
        var tied = new[] { Candidate(ActionType.Defend, "enemy_1", 1.5), Candidate(ActionType.Attack, "enemy_1", 1.5) };
        var map = new Dictionary<string, double> { ["enemy_1"] = 0.5 };
        Assert.Equal(ActionType.Attack, TieBreaking.ResolveTie(tied, ElementalPath.Fire, map).ActionType);
    }

    [Fact(DisplayName = "picks SPECIAL over ATTACK for Shadow path when tied")]
    public void Shadow_special_over_attack()
    {
        var tied = new[] { Candidate(ActionType.Attack, "enemy_1", 2.0), Candidate(ActionType.Special, "enemy_1", 2.0) };
        var map = new Dictionary<string, double> { ["enemy_1"] = 0.4 };
        Assert.Equal(ActionType.Special, TieBreaking.ResolveTie(tied, ElementalPath.Shadow, map).ActionType);
    }

    [Fact(DisplayName = "picks EVADE over DEFEND for Air path when tied")]
    public void Air_evade_over_defend()
    {
        var tied = new[] { Candidate(ActionType.Defend, "ally_1", 1.0), Candidate(ActionType.Evade, null, 1.0) };
        var map = new Dictionary<string, double> { ["ally_1"] = 0.3 };
        Assert.Equal(ActionType.Evade, TieBreaking.ResolveTie(tied, ElementalPath.Air, map).ActionType);
    }

    [Fact(DisplayName = "prefers DEFEND over EVADE for Water path when tied")]
    public void Water_defend_over_evade()
    {
        var tied = new[] { Candidate(ActionType.Evade, null, 1.5), Candidate(ActionType.Defend, "ally_2", 1.5) };
        var map = new Dictionary<string, double> { ["ally_2"] = 0.2 };
        Assert.Equal(ActionType.Defend, TieBreaking.ResolveTie(tied, ElementalPath.Water, map).ActionType);
    }

    [Fact(DisplayName = "same action type: picks target with lower stamina")]
    public void Lower_stamina_target()
    {
        var tied = new[] { Candidate(ActionType.Attack, "enemy_high", 2.0), Candidate(ActionType.Attack, "enemy_low", 2.0) };
        var map = new Dictionary<string, double> { ["enemy_high"] = 0.7, ["enemy_low"] = 0.2 };
        Assert.Equal("enemy_low", TieBreaking.ResolveTie(tied, ElementalPath.Fire, map).TargetId);
    }

    [Fact(DisplayName = "single candidate: returns it unchanged")]
    public void Single_candidate()
    {
        var tied = new[] { Candidate(ActionType.Attack, "enemy_1", 3.0) };
        var map = new Dictionary<string, double> { ["enemy_1"] = 0.5 };
        var winner = TieBreaking.ResolveTie(tied, ElementalPath.Fire, map);
        Assert.Equal(ActionType.Attack, winner.ActionType);
        Assert.Equal("enemy_1", winner.TargetId);
    }

    [Fact(DisplayName = "Light path prefers SPECIAL over DEFEND")]
    public void Light_special_over_defend()
    {
        var tied = new[] { Candidate(ActionType.Defend, "ally_1", 1.0), Candidate(ActionType.Special, "enemy_1", 1.0) };
        var map = new Dictionary<string, double> { ["ally_1"] = 0.4, ["enemy_1"] = 0.5 };
        Assert.Equal(ActionType.Special, TieBreaking.ResolveTie(tied, ElementalPath.Light, map).ActionType);
    }
}
