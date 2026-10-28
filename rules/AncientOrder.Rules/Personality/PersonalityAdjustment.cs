using System.Collections.Immutable;

namespace AncientOrder.Rules.Personality;

/// <summary>Trait deltas keyed by trait. Lookup only; never iterated for order.</summary>
public sealed record PersonalityAdjustment(ImmutableDictionary<Trait, double> Deltas);
