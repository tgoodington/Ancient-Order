using AncientOrder.Rules.Combat;

namespace AncientOrder.Rules.Party;

/// <summary>Coverage is today's Well Rounded scoring, Alignment is today's Bond scoring.</summary>
public enum SynergyRule
{
    Coverage,
    Alignment,
}

/// <summary>Threshold is the minimum trait value for Coverage and a percent (80 means 0.80) for Alignment, as in the TypeScript.</summary>
public sealed record SynergyBonusDefinition(
    string Name,
    SynergyRule Rule,
    double Threshold,
    CombatStat Stat,
    StatStep Step,
    double Value);
