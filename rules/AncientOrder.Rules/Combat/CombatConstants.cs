using System.Collections.Immutable;

namespace AncientOrder.Rules.Combat;

public static class CombatConstants
{
    public const double RollScale = 20;
    public const int MaxChainDepth = 10;
    public const double PathBuffModifier = 0.1;
    public const double PathDebuffModifier = 0.1;
    public const double CrushingBlowDebuff = 0.1;
    public const double EvadeRegenRate = 0.3;
    public const double BaseDamageStep = 0.25;
    public const double SpecialBonusPerSegment = 0.1;

    public const double EnergyActionSuccess = 1.0;
    public const double EnergyActionFailure = 0.5;
    public const double EnergyReactionSuccess = 0.5;
    public const double EnergyReactionFailure = 0.25;

    public static readonly ImmutableArray<double> AscensionThresholds = [35, 95, 180];

    public static readonly ImmutableArray<double> AscensionStartingSegments = [0, 0, 1, 2];

    public static readonly ImmutableArray<double> AscensionAccumulationBonus = [0, 0.25, 0.25, 0.5];

    /// <summary>Cumulative XP needed to reach each reaction rank 1 to 11 (index 0 = rank 1).</summary>
    public static readonly ImmutableArray<int> XpThresholds = [0, 100, 215, 345, 490, 650, 825, 1015, 1220, 1440, 1675];

    public static int MaxReactionRank => XpThresholds.Length;

    public static int MaxReactionXp => XpThresholds[^1];

    public const int XpOnSuccess = 4;
    public const int XpOnFailure = 2;

    /// <summary>Resolution priority; lower resolves first.</summary>
    public static int ActionPriority(ActionType type) => type switch
    {
        ActionType.Group => 0,
        ActionType.Defend => 1,
        ActionType.Attack => 2,
        ActionType.Special => 2,
        ActionType.Evade => 3,
        _ => throw new ArgumentOutOfRangeException(nameof(type)),
    };

    public static double EnergyGain(EnergyEvent kind, bool success) => (kind, success) switch
    {
        (EnergyEvent.Action, true) => EnergyActionSuccess,
        (EnergyEvent.Action, false) => EnergyActionFailure,
        (EnergyEvent.Reaction, true) => EnergyReactionSuccess,
        (EnergyEvent.Reaction, false) => EnergyReactionFailure,
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };
}
