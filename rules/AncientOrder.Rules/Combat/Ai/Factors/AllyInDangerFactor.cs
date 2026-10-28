namespace AncientOrder.Rules.Combat.Ai;

public sealed class AllyInDangerFactor : IScoringFactor
{
    private static readonly ActionScores Critical = new(-0.2, 0.8, -0.1, 0.4, 0.2);
    private static readonly ActionScores Mid = new(0.0, 0.3, 0.0, 0.1, 0.0);
    private static readonly ActionScores Healthy = new(0.0, 0.0, 0.0, 0.0, 0.0);

    public string Name => "allyInDanger";

    public ActionScores Evaluate(CombatPerception self, TargetPerception? target)
    {
        if (self.AllyCount == 0)
        {
            return Healthy;
        }

        var pct = self.LowestAllyStaminaPct;
        if (pct < 0.3)
        {
            return Critical;
        }

        return pct <= 0.6 ? Mid : Healthy;
    }
}
