namespace AncientOrder.Rules.Combat.Ai;

public sealed class TeamBalanceFactor : IScoringFactor
{
    private static readonly ActionScores Winning = new(0.3, -0.1, -0.2, 0.2, 0);
    private static readonly ActionScores Even = new(0.0, 0.0, 0.0, 0.0, 0);
    private static readonly ActionScores Losing = new(-0.2, 0.4, 0.3, 0.1, 0.2);

    public string Name => "teamBalance";

    public ActionScores Evaluate(CombatPerception self, TargetPerception? target)
    {
        var diff = self.TeamAvgStaminaPct - self.EnemyAvgStaminaPct;
        if (diff > 0.2)
        {
            return Winning;
        }

        return diff < -0.2 ? Losing : Even;
    }
}
