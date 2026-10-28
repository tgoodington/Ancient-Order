using System.Collections.Immutable;
using AncientOrder.Rules.Common;
using AncientOrder.Rules.Party;

namespace AncientOrder.Rules.Combat;

public static class StatModifiers
{
    /// <summary>
    /// Applies the modifiers that target one stat: ((base + sum of flat) * (1 + sum of add-percent)) * product of multipliers,
    /// rounded once at the end. A stat with no modifiers is returned untouched, not rounded.
    /// </summary>
    public static double Apply(double baseValue, ImmutableArray<StatModifier> modifiers)
    {
        if (modifiers.IsDefaultOrEmpty)
        {
            return baseValue;
        }

        double flat = 0;
        double addPercent = 0;
        double multiply = 1;
        foreach (var modifier in modifiers)
        {
            switch (modifier.Step)
            {
                case StatStep.Flat:
                    flat += modifier.Value;
                    break;
                case StatStep.AddPercent:
                    addPercent += modifier.Value;
                    break;
                case StatStep.Multiply:
                    multiply *= modifier.Value;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(modifiers));
            }
        }

        return JsMath.Round(((baseValue + flat) * (1 + addPercent)) * multiply);
    }

    /// <summary>Applies the modifiers in the list that target the given stat.</summary>
    public static double Apply(CombatStat stat, double baseValue, ImmutableArray<StatModifier> modifiers) =>
        Apply(baseValue, ForStat(stat, modifiers));

    public static ImmutableArray<StatModifier> ForStat(CombatStat stat, ImmutableArray<StatModifier> modifiers) =>
        modifiers.IsDefaultOrEmpty ? ImmutableArray<StatModifier>.Empty : modifiers.Where(m => m.Stat == stat).ToImmutableArray();

    public static ImmutableArray<StatModifier> FromSynergy(ImmutableArray<SynergyResult> synergy) =>
        synergy.IsDefaultOrEmpty
            ? ImmutableArray<StatModifier>.Empty
            : synergy.Select(s => new StatModifier(s.Stat, s.Step, s.Value, s.Name)).ToImmutableArray();
}
