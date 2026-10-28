using AncientOrder.Rules.Common;
using AncientOrder.Rules.Personality;
using AncientOrder.Rules.Tests.Support;
using Persona = AncientOrder.Rules.Personality.Personality;

namespace AncientOrder.Rules.Tests.Personality;

// Carried over from src/personality/personalitySystem.test.ts. Each TypeScript it() title is the DisplayName.
internal static class PersonalityTestHelpers
{
    // Tolerance of 0.02 as in the TypeScript helper (the strict check lives in PersonalityInvariantTests).
    public static void ExpectSumTo100(Persona p) =>
        Assert.True(Math.Abs(PersonalityRules.Sum(p) - PersonalityRules.TotalPercentage) <= 0.02);

    public static void ExpectAllTraitsInRange(Persona p)
    {
        foreach (var trait in PersonalityRules.Traits)
        {
            Assert.True(p[trait] >= PersonalityRules.MinTraitValue);
            Assert.True(p[trait] <= PersonalityRules.MaxTraitValue);
        }
    }
}

public class ClampTrait
{
    // The TypeScript clampTrait is private in C#; its behavior is observable through Adjust on a single trait.
    private static double Clamped(double delta)
    {
        var personality = TestPersonality.Of(20, 16, 16, 16, 16, 16);
        return PersonalityRules.Adjust(personality, Adj.Of((Trait.Patience, delta)))[Trait.Patience];
    }

    [Fact(DisplayName = "returns the value unchanged when it is within [5, 35]")]
    public void Within_range_unchanged()
    {
        Assert.Equal(20.0, Clamped(0));
        Assert.Equal(PersonalityRules.MinTraitValue, Clamped(-15));
        Assert.Equal(PersonalityRules.MaxTraitValue, Clamped(15));
    }

    [Fact(DisplayName = "clamps values below minimum (4.9 → 5)")]
    public void Clamps_below_minimum()
    {
        Assert.Equal(PersonalityRules.MinTraitValue, Clamped(-15.1));
        Assert.Equal(PersonalityRules.MinTraitValue, Clamped(-20));
        Assert.Equal(PersonalityRules.MinTraitValue, Clamped(-30));
    }

    [Fact(DisplayName = "clamps values above maximum (35.1 → 35)")]
    public void Clamps_above_maximum()
    {
        Assert.Equal(PersonalityRules.MaxTraitValue, Clamped(15.1));
        Assert.Equal(PersonalityRules.MaxTraitValue, Clamped(80));
        Assert.Equal(PersonalityRules.MaxTraitValue, Clamped(30));
    }
}

public class GetPersonalitySum
{
    [Fact(DisplayName = "returns the sum of all six traits")]
    public void Sum_of_six()
    {
        var p = TestPersonality.Of(16.67, 16.67, 16.67, 16.67, 16.67, 16.65);
        Assert.InRange(PersonalityRules.Sum(p), 99.95, 100.05);
    }

    [Fact(DisplayName = "returns 0 for a zero personality")]
    public void Zero()
    {
        Assert.Equal(0.0, PersonalityRules.Sum(TestPersonality.Of(0, 0, 0, 0, 0, 0)));
    }
}

public class ValidatePersonality
{
    [Fact(DisplayName = "accepts a valid personality")]
    public void Accepts_valid()
    {
        var result = PersonalityRules.Validate(PersonalityRules.CreateDefault());
        Assert.True(result.Valid);
        Assert.Empty(result.Errors);
    }

    [Fact(DisplayName = "rejects a trait below minimum")]
    public void Rejects_below_minimum()
    {
        var result = PersonalityRules.Validate(TestPersonality.Of(4, 19, 19, 19, 19, 20));
        Assert.False(result.Valid);
        Assert.Contains(result.Errors, e => e.Contains("patience", StringComparison.Ordinal));
    }

    [Fact(DisplayName = "rejects a trait above maximum")]
    public void Rejects_above_maximum()
    {
        var result = PersonalityRules.Validate(TestPersonality.Of(36, 13, 13, 13, 13, 12));
        Assert.False(result.Valid);
        Assert.Contains(result.Errors, e => e.Contains("patience", StringComparison.Ordinal));
    }

    [Fact(DisplayName = "rejects a personality whose sum deviates from 100")]
    public void Rejects_bad_sum()
    {
        var result = PersonalityRules.Validate(TestPersonality.Of(17, 17, 17, 17, 17, 17));
        Assert.False(result.Valid);
        Assert.Contains(result.Errors, e => e.Contains("Sum", StringComparison.Ordinal));
    }

    [Fact(DisplayName = "collects multiple errors when multiple traits are invalid")]
    public void Collects_multiple()
    {
        var result = PersonalityRules.Validate(TestPersonality.Of(4, 36, 20, 20, 20, 0));
        Assert.False(result.Valid);
        Assert.True(result.Errors.Length > 1);
    }
}

public class CreateDefaultPersonality
{
    [Fact(DisplayName = "returns a personality whose sum equals 100")]
    public void Sum_is_100() => PersonalityTestHelpers.ExpectSumTo100(PersonalityRules.CreateDefault());

    [Fact(DisplayName = "returns a personality where all traits are within [5, 35]")]
    public void Traits_in_range() => PersonalityTestHelpers.ExpectAllTraitsInRange(PersonalityRules.CreateDefault());

    [Fact(DisplayName = "passes validatePersonality")]
    public void Passes_validate() => Assert.True(PersonalityRules.Validate(PersonalityRules.CreateDefault()).Valid);

    [Fact(DisplayName = "produces traits that are approximately equal (~16.67)")]
    public void Approximately_equal()
    {
        foreach (var v in TestPersonality.Values(PersonalityRules.CreateDefault()))
        {
            Assert.InRange(v, 16.0, 17.0);
        }
    }

    [Fact(DisplayName = "returns a new object on each call (no shared state)")]
    public void New_object_each_call() =>
        Assert.NotSame(PersonalityRules.CreateDefault(), PersonalityRules.CreateDefault());
}

public class AdjustPersonalityImmutability
{
    [Fact(DisplayName = "does not mutate the input personality")]
    public void Does_not_mutate()
    {
        var original = PersonalityRules.CreateDefault();
        var copy = original with { };
        PersonalityRules.Adjust(original, Adj.Of((Trait.Cunning, 5)));
        Assert.Equal(copy, original);
    }

    [Fact(DisplayName = "returns a new object reference (not the same object)")]
    public void New_reference()
    {
        var p = PersonalityRules.CreateDefault();
        Assert.NotSame(p, PersonalityRules.Adjust(p, Adj.Of((Trait.Patience, 3))));
    }
}

public class AdjustPersonalitySumInvariant
{
    [Fact(DisplayName = "sum equals 100 after a single positive adjustment")]
    public void Single_positive() =>
        PersonalityTestHelpers.ExpectSumTo100(PersonalityRules.Adjust(PersonalityRules.CreateDefault(), Adj.Of((Trait.Cunning, 5))));

    [Fact(DisplayName = "sum equals 100 after a single negative adjustment")]
    public void Single_negative() =>
        PersonalityTestHelpers.ExpectSumTo100(PersonalityRules.Adjust(PersonalityRules.CreateDefault(), Adj.Of((Trait.Patience, -5))));

    [Fact(DisplayName = "sum equals 100 after multi-trait adjustments")]
    public void Multi_trait() =>
        PersonalityTestHelpers.ExpectSumTo100(
            PersonalityRules.Adjust(PersonalityRules.CreateDefault(), Adj.Of((Trait.Cunning, 8), (Trait.Empathy, -3))));

    [Fact(DisplayName = "sum equals 100 even when an adjustment would push a trait past 35")]
    public void Past_35() =>
        PersonalityTestHelpers.ExpectSumTo100(PersonalityRules.Adjust(PersonalityRules.CreateDefault(), Adj.Of((Trait.Charisma, 25))));

    [Fact(DisplayName = "sum equals 100 even when an adjustment would push a trait below 5")]
    public void Below_5() =>
        PersonalityTestHelpers.ExpectSumTo100(PersonalityRules.Adjust(PersonalityRules.CreateDefault(), Adj.Of((Trait.Empathy, -20))));
}

public class AdjustPersonalityRangeEnforcement
{
    [Fact(DisplayName = "clamps a directly adjusted trait that would exceed 35")]
    public void Clamps_high() =>
        Assert.Equal(PersonalityRules.MaxTraitValue, PersonalityRules.Adjust(PersonalityRules.CreateDefault(), Adj.Of((Trait.Cunning, 30))).Cunning);

    [Fact(DisplayName = "clamps a directly adjusted trait that would go below 5")]
    public void Clamps_low() =>
        Assert.Equal(PersonalityRules.MinTraitValue, PersonalityRules.Adjust(PersonalityRules.CreateDefault(), Adj.Of((Trait.Patience, -30))).Patience);

    [Fact(DisplayName = "redistributed traits also stay within [5, 35]")]
    public void Redistributed_in_range()
    {
        var result = PersonalityRules.Adjust(PersonalityRules.CreateDefault(), Adj.Of((Trait.Cunning, 18)));
        PersonalityTestHelpers.ExpectAllTraitsInRange(result);
        PersonalityTestHelpers.ExpectSumTo100(result);
    }

    [Fact(DisplayName = "all traits remain in range after a zero adjustment")]
    public void Zero_adjustment()
    {
        var result = PersonalityRules.Adjust(PersonalityRules.CreateDefault(), Adj.Of());
        PersonalityTestHelpers.ExpectAllTraitsInRange(result);
        PersonalityTestHelpers.ExpectSumTo100(result);
    }
}

public class AdjustPersonalityBoundaryCases
{
    [Fact(DisplayName = "a trait that lands at exactly 4.9 is clamped to 5")]
    public void Lands_at_4_9()
    {
        var baseP = TestPersonality.Of(10, 18, 18, 18, 18, 18);
        var result = PersonalityRules.Adjust(baseP, Adj.Of((Trait.Patience, -5.1)));
        Assert.Equal(PersonalityRules.MinTraitValue, result.Patience);
        PersonalityTestHelpers.ExpectSumTo100(result);
    }

    [Fact(DisplayName = "a trait that lands at exactly 35.1 is clamped to 35")]
    public void Lands_at_35_1()
    {
        var baseP = TestPersonality.Of(15, 15, 15, 15, 15, 25);
        var result = PersonalityRules.Adjust(baseP, Adj.Of((Trait.Charisma, 10.1)));
        Assert.Equal(PersonalityRules.MaxTraitValue, result.Charisma);
        PersonalityTestHelpers.ExpectSumTo100(result);
    }

    [Fact(DisplayName = "clamping at min boundary still preserves sum invariant")]
    public void Min_boundary()
    {
        var p = PersonalityRules.CreateDefault();
        var delta = p.Empathy - PersonalityRules.MinTraitValue;
        var result = PersonalityRules.Adjust(p, Adj.Of((Trait.Empathy, -delta)));
        Assert.Equal(PersonalityRules.MinTraitValue, result.Empathy);
        PersonalityTestHelpers.ExpectSumTo100(result);
    }

    [Fact(DisplayName = "clamping at max boundary still preserves sum invariant")]
    public void Max_boundary()
    {
        var p = PersonalityRules.CreateDefault();
        var delta = PersonalityRules.MaxTraitValue - p.Logic;
        var result = PersonalityRules.Adjust(p, Adj.Of((Trait.Logic, delta)));
        Assert.Equal(PersonalityRules.MaxTraitValue, result.Logic);
        PersonalityTestHelpers.ExpectSumTo100(result);
    }
}

public class AdjustPersonalityMultiTraitInteractions
{
    [Fact(DisplayName = "adjusting multiple traits simultaneously maintains sum invariant")]
    public void Multiple_simultaneously()
    {
        var result = PersonalityRules.Adjust(
            PersonalityRules.CreateDefault(),
            Adj.Of((Trait.Patience, 5), (Trait.Empathy, 5), (Trait.Cunning, -3), (Trait.Logic, -3)));
        PersonalityTestHelpers.ExpectSumTo100(result);
        PersonalityTestHelpers.ExpectAllTraitsInRange(result);
    }

    [Fact(DisplayName = "adjusting all 6 traits uses evenly distributed redistribution")]
    public void All_six()
    {
        var result = PersonalityRules.Adjust(
            PersonalityRules.CreateDefault(),
            Adj.Of(
                (Trait.Patience, 2), (Trait.Empathy, 2), (Trait.Cunning, 2),
                (Trait.Logic, -2), (Trait.Kindness, -2), (Trait.Charisma, -2)));
        PersonalityTestHelpers.ExpectSumTo100(result);
        PersonalityTestHelpers.ExpectAllTraitsInRange(result);
    }

    [Fact(DisplayName = "opposing adjustments (one up, one down) do not drift the sum")]
    public void Opposing()
    {
        var result = PersonalityRules.Adjust(PersonalityRules.CreateDefault(), Adj.Of((Trait.Cunning, 6), (Trait.Kindness, -6)));
        PersonalityTestHelpers.ExpectSumTo100(result);
        PersonalityTestHelpers.ExpectAllTraitsInRange(result);
    }

    [Fact(DisplayName = "chained adjustments each produce valid personality")]
    public void Chained()
    {
        var p = PersonalityRules.CreateDefault();
        p = PersonalityRules.Adjust(p, Adj.Of((Trait.Patience, 5)));
        PersonalityTestHelpers.ExpectSumTo100(p);
        p = PersonalityRules.Adjust(p, Adj.Of((Trait.Empathy, -3), (Trait.Logic, 4)));
        PersonalityTestHelpers.ExpectSumTo100(p);
        p = PersonalityRules.Adjust(p, Adj.Of((Trait.Charisma, -8)));
        PersonalityTestHelpers.ExpectSumTo100(p);
        PersonalityTestHelpers.ExpectAllTraitsInRange(p);
    }

    [Fact(DisplayName = "large adjustment that saturates multiple traits still sums to 100")]
    public void Saturates_multiple()
    {
        var baseP = TestPersonality.Of(15, 15, 20, 20, 15, 15);
        var result = PersonalityRules.Adjust(baseP, Adj.Of((Trait.Patience, 20), (Trait.Empathy, 20)));
        Assert.Equal(PersonalityRules.MaxTraitValue, result.Patience);
        Assert.Equal(PersonalityRules.MaxTraitValue, result.Empathy);
        PersonalityTestHelpers.ExpectSumTo100(result);
        PersonalityTestHelpers.ExpectAllTraitsInRange(result);
    }
}

public class AdjustPersonalityValueCorrectness
{
    [Fact(DisplayName = "a positive adjustment increases the target trait")]
    public void Positive_increases()
    {
        var p = PersonalityRules.CreateDefault();
        Assert.True(PersonalityRules.Adjust(p, Adj.Of((Trait.Cunning, 5))).Cunning > p.Cunning);
    }

    [Fact(DisplayName = "a negative adjustment decreases the target trait")]
    public void Negative_decreases()
    {
        var p = PersonalityRules.CreateDefault();
        Assert.True(PersonalityRules.Adjust(p, Adj.Of((Trait.Kindness, -5))).Kindness < p.Kindness);
    }

    [Fact(DisplayName = "unadjusted traits shift to compensate when one trait is raised")]
    public void Others_compensate()
    {
        var p = PersonalityRules.CreateDefault();
        var result = PersonalityRules.Adjust(p, Adj.Of((Trait.Patience, 10)));
        var othersBefore = p.Empathy + p.Cunning + p.Logic + p.Kindness + p.Charisma;
        var othersAfter = result.Empathy + result.Cunning + result.Logic + result.Kindness + result.Charisma;
        Assert.True(othersAfter < othersBefore + PersonalityRules.FloatTolerance);
    }

    [Fact(DisplayName = "values are rounded to 2 decimal places")]
    public void Rounded_to_2dp()
    {
        var result = PersonalityRules.Adjust(PersonalityRules.CreateDefault(), Adj.Of((Trait.Cunning, 3)));
        foreach (var trait in PersonalityRules.Traits)
        {
            Assert.Equal(result[trait], JsMath.Round(result[trait] * 100) / 100);
        }
    }
}
