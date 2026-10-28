using System.Collections.Immutable;
using AncientOrder.Rules.Common;
using AncientOrder.Rules.State;

namespace AncientOrder.Rules.Party;

public static class LineupRules
{
    /// <summary>Sets the fight lineup. Refusals, in order: combat active, wrong size, unknown id, no combat stats, duplicate id.</summary>
    public static Result<GameState> Set(GameContent content, GameState state, ImmutableArray<string> ids, long now)
    {
        if (state.Combat is not null)
        {
            return Refuse("Cannot change team during combat");
        }

        var size = content.Settings.LineupSize;
        if (ids.IsDefault || ids.Length != size)
        {
            return Refuse($"Team must contain exactly {size} NPCs");
        }

        foreach (var id in ids)
        {
            if (!content.Characters.TryGet(id, out _))
            {
                return Refuse($"Invalid NPC ID: {id}");
            }
        }

        foreach (var id in ids)
        {
            content.Characters.TryGet(id, out var definition);
            if (definition.Combat is null)
            {
                return Refuse($"{id} has no combat stats");
            }
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var id in ids)
        {
            if (!seen.Add(id))
            {
                return Refuse($"Duplicate NPC ID: {id}");
            }
        }

        return Result<GameState>.Ok(state with { Lineup = ids, Timestamp = now });
    }

    private static Result<GameState> Refuse(string message) =>
        Result<GameState>.Fail(new RuleError(ErrorCode.TEAM_COMPOSITION_INVALID, message));
}
