namespace AncientOrder.Rules.Combat;

/// <summary>Flat adds to the base, AddPercent adds to one shared percent pool (0.10 is +10%), Multiply is applied last, one factor each.</summary>
public enum StatStep
{
    Flat,
    AddPercent,
    Multiply,
}

public sealed record StatModifier(CombatStat Stat, StatStep Step, double Value, string Source);
