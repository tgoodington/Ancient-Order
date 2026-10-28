namespace AncientOrder.Rules.Combat.Ai;

public sealed class RoundPhaseFactor : IScoringFactor
{
    private static readonly ActionScores Early = new(0.1, 0.2, 0.3, -0.2, 0);
    private static readonly ActionScores Mid = new(0.2, 0.0, 0.0, 0.2, 0);
    private static readonly ActionScores Late = new(0.3, -0.1, -0.1, 0.4, 0);

    public string Name => "roundPhase";

    public ActionScores Evaluate(CombatPerception self, TargetPerception? target)
    {
        if (self.Round <= 2)
        {
            return Early;
        }

        return self.Round <= 5 ? Mid : Late;
    }
}
