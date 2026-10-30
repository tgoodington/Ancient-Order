using System.Collections.Immutable;
using AncientOrder.Rules.Combat;
using AncientOrder.Rules.Party;

namespace AncientOrder.Rules.Content.Packs.Format;

public sealed record SynergyFormat(ImmutableArray<SynergyBonusFormat> Bonuses, SynergyResolution Resolution);

public sealed record SynergyBonusFormat(
    string Name,
    SynergyRule Rule,
    double Threshold,
    CombatStat Stat,
    StatStep Step,
    double Value,
    string? Feel = null);
