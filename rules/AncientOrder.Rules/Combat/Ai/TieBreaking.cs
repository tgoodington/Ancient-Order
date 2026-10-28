using System.Collections.Immutable;

namespace AncientOrder.Rules.Combat.Ai;

public sealed record ScoredCandidate(ActionType ActionType, string? TargetId, double Score);

public static class TieBreaking
{
    /// <summary>Action types in priority order for a combatant's own elemental path.</summary>
    public static ImmutableArray<ActionType> PathTiebreak(ElementalPath path) => path switch
    {
        ElementalPath.Fire => [ActionType.Attack, ActionType.Special, ActionType.Defend, ActionType.Evade, ActionType.Group],
        ElementalPath.Shadow => [ActionType.Special, ActionType.Attack, ActionType.Evade, ActionType.Defend, ActionType.Group],
        ElementalPath.Earth => [ActionType.Defend, ActionType.Attack, ActionType.Special, ActionType.Evade, ActionType.Group],
        ElementalPath.Water => [ActionType.Defend, ActionType.Evade, ActionType.Special, ActionType.Attack, ActionType.Group],
        ElementalPath.Air => [ActionType.Evade, ActionType.Defend, ActionType.Special, ActionType.Attack, ActionType.Group],
        ElementalPath.Light => [ActionType.Special, ActionType.Defend, ActionType.Evade, ActionType.Attack, ActionType.Group],
        _ => throw new ArgumentOutOfRangeException(nameof(path)),
    };

    /// <summary>Among candidates sharing the top score: earliest action type in the path's order, then lowest target stamina % (no target counts as 0). Stable.</summary>
    public static ScoredCandidate ResolveTie(
        IReadOnlyList<ScoredCandidate> tied,
        ElementalPath path,
        IReadOnlyDictionary<string, double> targetStaminaPct)
    {
        var order = PathTiebreak(path);
        return tied
            .OrderBy(c => order.IndexOf(c.ActionType))
            .ThenBy(c => c.TargetId is not null && targetStaminaPct.TryGetValue(c.TargetId, out var pct) ? pct : 0)
            .First();
    }
}
