using System.Collections.Immutable;
using AncientOrder.Rules.Common;
using AncientOrder.Rules.State;

namespace AncientOrder.Rules.Party;

/// <summary>Whether a lineup can go into a fight. Reason is short and plain, written to be shown to the player, and is null when Ready.</summary>
public sealed record LineupReadiness(bool Ready, string? Reason);

public static class LineupRules
{
    /// <summary>
    /// Sets the fight lineup (0 to max, picked only from the party). Refusals, in order: combat active, conversation active,
    /// more than max, unknown id, not in the party, no combat stats, duplicate id.
    /// </summary>
    public static Result<GameState> Set(GameContent content, GameState state, ImmutableArray<string> ids, long now)
    {
        if (state.Combat is not null)
        {
            return Refuse("Cannot change team during combat");
        }

        if (state.Conversation is not null)
        {
            return Refuse("Cannot change team during a conversation");
        }

        var max = content.Settings.Lineup.Max;
        if (ids.IsDefault || ids.Length > max)
        {
            return Refuse($"Team can have at most {max} NPCs");
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
            if (!state.Party.Contains(id, StringComparer.Ordinal))
            {
                return Refuse($"{id} is not in the party");
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

    /// <summary>Ready when the lineup size is within the limits and every member is in the party and can fight.</summary>
    public static LineupReadiness Ready(GameContent content, GameState state, LineupLimits limits)
    {
        var lineup = state.Lineup.IsDefault ? ImmutableArray<string>.Empty : state.Lineup;
        if (lineup.Length < limits.Min)
        {
            return Not($"Pick at least {limits.Min} {(limits.Min == 1 ? "fighter" : "fighters")}");
        }

        if (lineup.Length > limits.Max)
        {
            return Not($"Pick at most {limits.Max} {(limits.Max == 1 ? "fighter" : "fighters")}");
        }

        foreach (var id in lineup)
        {
            var known = content.Characters.TryGet(id, out var definition);
            var name = known ? definition.Name : id;
            if (!known || !state.Party.Contains(id, StringComparer.Ordinal))
            {
                return Not($"{name} is not in the party");
            }

            if (definition.Combat is null)
            {
                return Not($"{name} cannot fight");
            }
        }

        return new LineupReadiness(true, null);
    }

    private static LineupReadiness Not(string reason) => new(false, reason);

    private static Result<GameState> Refuse(string message) =>
        Result<GameState>.Fail(new RuleError(ErrorCode.TEAM_COMPOSITION_INVALID, message));
}
