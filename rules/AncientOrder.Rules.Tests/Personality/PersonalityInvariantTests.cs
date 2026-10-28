using System.Collections.Immutable;
using AncientOrder.Rules.Personality;
using AncientOrder.Rules.Tests.Support;

namespace AncientOrder.Rules.Tests.Personality;

public class PersonalityInvariantTests
{
    private static readonly Trait[] AllTraits = [.. PersonalityRules.Traits];

    [Fact(DisplayName = "100,000 seeded random adjustments always sum to 100 within 0.001 and keep every trait in 5-35")]
    public void Random_adjustments_hold_the_invariants()
    {
        var random = new Random(20261028);
        var current = PersonalityRules.CreateDefault();

        for (var i = 0; i < 100_000; i++)
        {
            var count = random.Next(0, 7);
            var chosen = AllTraits.OrderBy(_ => random.Next()).Take(count);
            var deltas = ImmutableDictionary.CreateBuilder<Trait, double>();
            foreach (var trait in chosen)
            {
                deltas[trait] = Math.Round((random.NextDouble() * 80) - 40, random.Next(0, 4));
            }

            var before = current;
            var beforeCopy = before with { };
            current = PersonalityRules.Adjust(before, new PersonalityAdjustment(deltas.ToImmutable()));

            Assert.Equal(beforeCopy, before);
            var sum = PersonalityRules.Sum(current);
            Assert.True(Math.Abs(sum - 100) <= 0.001, $"iteration {i}: sum {sum}");
            foreach (var trait in PersonalityRules.Traits)
            {
                Assert.InRange(current[trait], 5.0, 35.0);
            }

            Assert.True(PersonalityRules.Validate(current).Valid, $"iteration {i}: not valid");

            // Restart from the default now and then so extreme corners are reached from varied starts.
            if (i % 500 == 499)
            {
                current = PersonalityRules.CreateDefault();
            }
        }
    }

    [Fact(DisplayName = "default personality plus a logic shift of +21.35 gives logic 35 and a valid personality summing to 100")]
    public void Logic_plus_21_35_regression()
    {
        var result = PersonalityRules.Adjust(PersonalityRules.CreateDefault(), Adj.Of((Trait.Logic, 21.35)));

        Assert.Equal(35.0, result.Logic);
        Assert.True(PersonalityRules.Validate(result).Valid);
        Assert.True(Math.Abs(PersonalityRules.Sum(result) - 100) <= 0.001);
    }

    [Fact(DisplayName = "a non-finite trait is reported invalid")]
    public void Non_finite_trait_is_invalid()
    {
        var p = PersonalityRules.CreateDefault() with { Logic = double.NaN };
        var result = PersonalityRules.Validate(p);
        Assert.False(result.Valid);
        Assert.Contains(result.Errors, e => e.Contains("logic", StringComparison.Ordinal));
        Assert.False(PersonalityRules.Validate(PersonalityRules.CreateDefault() with { Cunning = double.PositiveInfinity }).Valid);
    }

    [Fact(DisplayName = "the default personality has today's values")]
    public void Default_values()
    {
        var p = PersonalityRules.CreateDefault();
        Assert.Equal(16.67, p.Patience);
        Assert.Equal(16.67, p.Empathy);
        Assert.Equal(16.67, p.Cunning);
        Assert.Equal(16.67, p.Logic);
        Assert.Equal(16.67, p.Kindness);
        Assert.Equal(16.65, p.Charisma);
    }

    [Fact(DisplayName = "a zero adjustment on a personality that already sums to 100 leaves it identical")]
    public void Zero_delta_on_valid_personality_is_identity()
    {
        var p = TestPersonality.Of(20, 20, 10, 15, 20, 15);
        Assert.Equal(p, PersonalityRules.Adjust(p, Adj.Of()));
    }
}
