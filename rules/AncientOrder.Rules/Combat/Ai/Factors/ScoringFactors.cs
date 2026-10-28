using System.Collections.Immutable;

namespace AncientOrder.Rules.Combat.Ai;

/// <summary>One factor's scores for all five action types.</summary>
public sealed record ActionScores(double Attack, double Defend, double Evade, double Special, double Group)
{
    public static ActionScores Zero { get; } = new(0, 0, 0, 0, 0);

    public double For(ActionType type) => type switch
    {
        ActionType.Attack => Attack,
        ActionType.Defend => Defend,
        ActionType.Evade => Evade,
        ActionType.Special => Special,
        ActionType.Group => Group,
        _ => throw new ArgumentOutOfRangeException(nameof(type)),
    };

    public static ActionScores Lerp(ActionScores a, ActionScores b, double t) => new(
        a.Attack + ((b.Attack - a.Attack) * t),
        a.Defend + ((b.Defend - a.Defend) * t),
        a.Evade + ((b.Evade - a.Evade) * t),
        a.Special + ((b.Special - a.Special) * t),
        a.Group + ((b.Group - a.Group) * t));
}

/// <summary>Scores one aspect of the fight. Factors are code; only their weights are content.</summary>
public interface IScoringFactor
{
    string Name { get; }

    ActionScores Evaluate(CombatPerception self, TargetPerception? target);
}

public static class ScoringFactors
{
    /// <summary>The factors in scoring order (the score is summed in this order).</summary>
    public static ImmutableArray<IScoringFactor> All { get; } =
    [
        new OwnStaminaFactor(),
        new AllyInDangerFactor(),
        new TargetVulnerabilityFactor(),
        new EnergyAvailabilityFactor(),
        new SpeedAdvantageFactor(),
        new RoundPhaseFactor(),
        new TeamBalanceFactor(),
    ];

    public static ImmutableArray<string> Names { get; } = [.. All.Select(f => f.Name)];
}
