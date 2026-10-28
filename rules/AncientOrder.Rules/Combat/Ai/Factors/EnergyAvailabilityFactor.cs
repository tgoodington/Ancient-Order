namespace AncientOrder.Rules.Combat.Ai;

public sealed class EnergyAvailabilityFactor : IScoringFactor
{
    private static readonly ActionScores HighEnergy = new(-0.1, 0, -0.2, 0.7, 0);
    private static readonly ActionScores MidEnergy = new(0.0, 0, 0.0, 0.3, 0);
    private static readonly ActionScores NoEnergy = new(0.1, 0, 0.1, 0.0, 0);

    public string Name => "energyAvailability";

    public ActionScores Evaluate(CombatPerception self, TargetPerception? target)
    {
        var energy = self.SelfEnergy;
        if (energy >= 3)
        {
            return HighEnergy;
        }

        return energy >= 1 ? MidEnergy : NoEnergy;
    }
}
