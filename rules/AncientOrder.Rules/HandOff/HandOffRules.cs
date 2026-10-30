using System.Collections.Immutable;
using AncientOrder.Rules.Common;
using AncientOrder.Rules.Party;
using AncientOrder.Rules.State;

namespace AncientOrder.Rules.HandOff;

/// <summary>Leaving the town for a fight. Pure: a refusal leaves the given game untouched.</summary>
public static class HandOffRules
{
    /// <summary>The lineup limits a fight uses: its own when it has them, else the settings limits (also for an unknown or absent fight).</summary>
    public static LineupLimits LimitsFor(GameContent content, string? fightId) =>
        fightId is not null && content.Encounters.TryGet(fightId, out var fight) && fight.Lineup is { } own
            ? own
            : content.Settings.Lineup;

    /// <summary>
    /// Checks the lineup against the fight's limits and, when ready, records the hand-off (fight, a copy of the lineup, the current
    /// town) and ends any running conversation. Refusals, in order: unknown fight (ENCOUNTER_NOT_FOUND), combat active
    /// (COMBAT_ALREADY_ACTIVE), already handed off (TOWN_NOT_ACTIVE), lineup not ready (LINEUP_NOT_READY, with the readiness reason).
    /// </summary>
    public static Result<GameState> Leave(GameContent content, GameState game, string fightId, long now)
    {
        if (!content.Encounters.TryGet(fightId, out var fight))
        {
            return Fail(ErrorCode.ENCOUNTER_NOT_FOUND, $"Encounter '{fightId}' not found in registry");
        }

        if (game.Combat is not null)
        {
            return Fail(ErrorCode.COMBAT_ALREADY_ACTIVE, "Cannot leave for a fight during combat");
        }

        if (game.HandOff is not null)
        {
            return Fail(ErrorCode.TOWN_NOT_ACTIVE, "The town is already complete");
        }

        var readiness = LineupRules.Ready(content, game, LimitsFor(content, fight.Id));
        if (!readiness.Ready)
        {
            return Fail(ErrorCode.LINEUP_NOT_READY, readiness.Reason ?? "The lineup is not ready");
        }

        var lineup = game.Lineup.IsDefault ? ImmutableArray<string>.Empty : ImmutableArray.CreateRange(game.Lineup);
        return Result<GameState>.Ok(game with
        {
            HandOff = new FightHandOff(fight.Id, lineup, game.Town?.TownId),
            Conversation = null,
            Timestamp = now,
        });
    }

    private static Result<GameState> Fail(ErrorCode code, string message) =>
        Result<GameState>.Fail(new RuleError(code, message));
}
