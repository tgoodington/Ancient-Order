namespace AncientOrder.Rules.Combat.Ai;

public sealed class OwnStaminaFactor : IScoringFactor
{
    private static readonly ActionScores Low = new(-0.5, 0.1, 0.9, -0.3, 0);
    private static readonly ActionScores Mid = new(0.0, 0.0, 0.2, 0.0, 0);
    private static readonly ActionScores High = new(0.2, 0.0, -0.3, 0.1, 0);

    public string Name => "ownStamina";

    public ActionScores Evaluate(CombatPerception self, TargetPerception? target)
    {
        var pct = self.SelfStaminaPct;
        if (pct < 0.3)
        {
            return ActionScores.Lerp(Low, Mid, pct / 0.3);
        }

        if (pct <= 0.6)
        {
            return ActionScores.Lerp(Mid, High, (pct - 0.3) / 0.3);
        }

        return High;
    }
}
