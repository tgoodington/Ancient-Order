using System.Collections.Immutable;
using AncientOrder.Rules.Reactions;

namespace AncientOrder.Rules.Combat.Ai;

/// <summary>How an archetype decides: Basic (attack the first living opponent) or Scored (utility scoring from a profile).</summary>
public abstract record ArchetypeBehavior
{
    private protected ArchetypeBehavior()
    {
    }

    public sealed record Basic : ArchetypeBehavior;

    public sealed record Scored(DecisionProfile Profile) : ArchetypeBehavior;
}

/// <summary>An archetype from content. Characters and encounter combatants point to one by Id. Reactions are the optional leanings that drive how characters of the archetype react to a choice. Archetypes are read-only content.</summary>
public sealed record ArchetypeDefinition(string Id, ArchetypeBehavior Behavior, ReactionProfile? Reactions = null);

/// <summary>
/// Base scores per action type and a weight per scoring factor. Both maps must define every key
/// (every ActionType; every name in ScoringFactors.Names); content validation refuses a profile that does not.
/// Lookup only, never iterated. No elemental path: tie-breaks use the combatant's own path.
/// </summary>
public sealed record DecisionProfile(
    ImmutableDictionary<ActionType, double> BaseScores,
    ImmutableDictionary<string, double> FactorWeights);
