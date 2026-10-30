using System.Collections.Immutable;
using AncientOrder.Rules.Common;
using AncientOrder.Rules.State;

namespace AncientOrder.Rules.Party;

/// <summary>The party module: who has joined, in join order. Conversations change it through effects, so it is not locked during one.</summary>
public static class PartyRules
{
    /// <summary>Adds a catalog character to the end of the party. Joining twice changes nothing (the same state comes back).</summary>
    public static Result<GameState> Join(GameContent content, GameState state, string id, long now)
    {
        if (!content.Characters.TryGet(id, out _))
        {
            return NotFound(id);
        }

        if (state.Party.Contains(id, StringComparer.Ordinal))
        {
            return Result<GameState>.Ok(state);
        }

        return Result<GameState>.Ok(state with { Party = state.Party.Add(id), Timestamp = now });
    }

    /// <summary>Removes a character from the party and from the lineup. Not being in the party changes nothing.</summary>
    public static Result<GameState> Leave(GameContent content, GameState state, string id, long now)
    {
        if (!content.Characters.TryGet(id, out _))
        {
            return NotFound(id);
        }

        if (!state.Party.Contains(id, StringComparer.Ordinal))
        {
            return Result<GameState>.Ok(state);
        }

        return Result<GameState>.Ok(state with
        {
            Party = state.Party.RemoveAll(p => string.Equals(p, id, StringComparison.Ordinal)),
            Lineup = state.Lineup.IsDefault ? state.Lineup : state.Lineup.RemoveAll(p => string.Equals(p, id, StringComparison.Ordinal)),
            Timestamp = now,
        });
    }

    private static Result<GameState> NotFound(string id) =>
        Result<GameState>.Fail(new RuleError(ErrorCode.NPC_NOT_FOUND, $"NPC not found: \"{id}\""));
}
