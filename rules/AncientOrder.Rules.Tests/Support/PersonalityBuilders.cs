using System.Collections.Immutable;
using AncientOrder.Rules.Personality;

namespace AncientOrder.Rules.Tests.Support;

public static class TestPersonality
{
    /// <summary>Builds a personality in trait order: patience, empathy, cunning, logic, kindness, charisma.</summary>
    public static AncientOrder.Rules.Personality.Personality Of(
        double patience, double empathy, double cunning, double logic, double kindness, double charisma) =>
        new()
        {
            Patience = patience,
            Empathy = empathy,
            Cunning = cunning,
            Logic = logic,
            Kindness = kindness,
            Charisma = charisma,
        };

    public static double[] Values(AncientOrder.Rules.Personality.Personality p) =>
        [p.Patience, p.Empathy, p.Cunning, p.Logic, p.Kindness, p.Charisma];
}

public static class Adj
{
    public static PersonalityAdjustment Of(params (Trait Trait, double Delta)[] deltas) =>
        new(deltas.ToImmutableDictionary(d => d.Trait, d => d.Delta));
}
