namespace AncientOrder.Rules.Combat.Ai;

public sealed class SpeedAdvantageFactor : IScoringFactor
{
    private static readonly ActionScores Fast = new(0.6, -0.1, -0.2, 0.3, 0);
    private static readonly ActionScores Neutral = new(0.2, 0.0, 0.0, 0.1, 0);
    private static readonly ActionScores Slow = new(-0.1, 0.1, 0.1, 0.0, 0);

    public string Name => "speedAdvantage";

    public ActionScores Evaluate(CombatPerception self, TargetPerception? target)
    {
        if (target is null)
        {
            return ActionScores.Zero;
        }

        var delta = target.SpeedDelta;
        if (delta > 0.3)
        {
            return Fast;
        }

        // Kept as today: at exactly 0 the score jumps from Slow to Neutral.
        if (delta >= 0)
        {
            return ActionScores.Lerp(Neutral, Fast, delta / 0.3);
        }

        return Slow;
    }
}
