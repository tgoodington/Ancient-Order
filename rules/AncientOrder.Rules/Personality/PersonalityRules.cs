using System.Collections.Immutable;
using System.Globalization;
using AncientOrder.Rules.Common;

namespace AncientOrder.Rules.Personality;

public sealed record PersonalityValidation(bool Valid, ImmutableArray<string> Errors);

public static class PersonalityRules
{
    public const double MinTraitValue = 5;
    public const double MaxTraitValue = 35;
    public const double TotalPercentage = 100;
    public const double FloatTolerance = 0.01;
    public const double CorrectionThreshold = 0.001;
    public const int RedistributionPasses = 10;

    public static ImmutableArray<Trait> Traits { get; } =
    [
        Trait.Patience,
        Trait.Empathy,
        Trait.Cunning,
        Trait.Logic,
        Trait.Kindness,
        Trait.Charisma,
    ];

    public static double Sum(Personality p)
    {
        double sum = 0;
        foreach (var trait in Traits)
        {
            sum += p[trait];
        }

        return sum;
    }

    public static PersonalityValidation Validate(Personality p)
    {
        var errors = ImmutableArray.CreateBuilder<string>();

        foreach (var trait in Traits)
        {
            var value = p[trait];
            var name = TraitName(trait);
            // Deliberate difference from the TypeScript, which also accepted NaN/Infinity.
            if (!double.IsFinite(value))
            {
                errors.Add($"{name} ({Num(value)}) is not a finite number");
                continue;
            }

            if (value < MinTraitValue)
            {
                errors.Add($"{name} ({Num(value)}) is below minimum {Num(MinTraitValue)}");
            }

            if (value > MaxTraitValue)
            {
                errors.Add($"{name} ({Num(value)}) is above maximum {Num(MaxTraitValue)}");
            }
        }

        var sum = Sum(p);
        if (!(Math.Abs(sum - TotalPercentage) <= FloatTolerance))
        {
            errors.Add($"Sum ({sum.ToString("F2", CultureInfo.InvariantCulture)}) does not equal {Num(TotalPercentage)}");
        }

        return new PersonalityValidation(errors.Count == 0, errors.ToImmutable());
    }

    // The TypeScript comment says "all equal", but the value is 16.65 on charisma: six at 16.67 would total 100.02.
    public static Personality CreateDefault()
    {
        var baseValue = TotalPercentage / Traits.Length;
        var rounded = JsMath.Round(baseValue * 100) / 100;

        var personality = new Personality
        {
            Patience = rounded,
            Empathy = rounded,
            Cunning = rounded,
            Logic = rounded,
            Kindness = rounded,
            Charisma = rounded,
        };

        var diff = TotalPercentage - Sum(personality);
        if (Math.Abs(diff) > CorrectionThreshold)
        {
            personality = personality with { Charisma = JsMath.Round((personality.Charisma + diff) * 100) / 100 };
        }

        return personality;
    }

    public static Personality Adjust(Personality current, PersonalityAdjustment adjustment)
    {
        var result = current;

        // Pass 1: apply each delta present (a zero delta still counts as adjusted), clamp.
        foreach (var trait in Traits)
        {
            if (adjustment.Deltas.TryGetValue(trait, out var delta))
            {
                result = result.With(trait, Clamp(result[trait] + delta));
            }
        }

        // Pass 2: distribute the difference proportionally over unadjusted traits (all six when none).
        var unadjusted = Traits.Where(t => !adjustment.Deltas.ContainsKey(t)).ToImmutableArray();
        var targets = unadjusted.Length > 0 ? unadjusted : Traits;

        var difference = TotalPercentage - Sum(result);
        if (Math.Abs(difference) > FloatTolerance)
        {
            double targetTotal = 0;
            foreach (var t in targets)
            {
                targetTotal += result[t];
            }

            if (targetTotal > 0)
            {
                foreach (var trait in targets)
                {
                    var proportion = result[trait] / targetTotal;
                    result = result.With(trait, Clamp(result[trait] + (difference * proportion)));
                }
            }
            else
            {
                var perTrait = difference / targets.Length;
                foreach (var trait in targets)
                {
                    result = result.With(trait, Clamp(result[trait] + perTrait));
                }
            }
        }

        // Pass 3: up to 10 even-split passes over traits with headroom.
        for (var iteration = 0; iteration < RedistributionPasses; iteration++)
        {
            var residual = TotalPercentage - Sum(result);
            if (Math.Abs(residual) <= FloatTolerance)
            {
                break;
            }

            var snapshot = result;
            var absorbable = Traits
                .Where(t => residual > 0 ? snapshot[t] < MaxTraitValue : snapshot[t] > MinTraitValue)
                .ToImmutableArray();
            if (absorbable.Length == 0)
            {
                break;
            }

            var perTrait = residual / absorbable.Length;
            foreach (var trait in absorbable)
            {
                result = result.With(trait, Clamp(result[trait] + perTrait));
            }
        }

        foreach (var trait in Traits)
        {
            result = result.With(trait, JsMath.Round(result[trait] * 100) / 100);
        }

        // Final micro-correction (deliberate fix): the leftover goes to the largest trait that can take it
        // and stay within 5-35 after rounding; first in trait order on ties.
        var finalDiff = TotalPercentage - Sum(result);
        if (Math.Abs(finalDiff) > CorrectionThreshold)
        {
            Trait? chosen = null;
            foreach (var trait in Traits)
            {
                var corrected = JsMath.Round((result[trait] + finalDiff) * 100) / 100;
                if (corrected < MinTraitValue || corrected > MaxTraitValue)
                {
                    continue;
                }

                if (chosen is null || result[trait] > result[chosen.Value])
                {
                    chosen = trait;
                }
            }

            if (chosen is null)
            {
                // Not reachable in practice: fall back to the original behavior (largest trait, clamped).
                var largest = Traits[0];
                foreach (var trait in Traits)
                {
                    if (result[trait] > result[largest])
                    {
                        largest = trait;
                    }
                }

                chosen = largest;
            }

            var target = chosen.Value;
            result = result.With(target, Clamp(JsMath.Round((result[target] + finalDiff) * 100) / 100));
        }

        return result;
    }

    private static double Clamp(double value) => Math.Max(MinTraitValue, Math.Min(MaxTraitValue, value));

    private static string TraitName(Trait trait) => trait switch
    {
        Trait.Patience => "patience",
        Trait.Empathy => "empathy",
        Trait.Cunning => "cunning",
        Trait.Logic => "logic",
        Trait.Kindness => "kindness",
        Trait.Charisma => "charisma",
        _ => throw new ArgumentOutOfRangeException(nameof(trait)),
    };

    private static string Num(double value) => value.ToString("R", CultureInfo.InvariantCulture);
}
