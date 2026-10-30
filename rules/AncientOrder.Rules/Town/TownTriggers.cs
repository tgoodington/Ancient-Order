using System.Collections.Immutable;
using AncientOrder.Rules.Common;
using AncientOrder.Rules.Conversations;
using AncientOrder.Rules.HandOff;
using AncientOrder.Rules.State;

namespace AncientOrder.Rules.Town;

/// <summary>The new game after a town action, and what it did.</summary>
public sealed record TownStep(GameState Game, TownEvent Event);

/// <summary>
/// What people and spots open. Pure: each function takes content, the game, and the time, and returns a new game or a refusal; a refusal
/// leaves the given game untouched. Positions are only ever the ones reported to the rules, so crossing is judged between reported points.
/// </summary>
public static class TownTriggers
{
    /// <summary>The first entry whose When holds, or null when none does.</summary>
    public static OpensEntry? First(GameContent content, GameState game, ImmutableArray<OpensEntry> opens)
    {
        if (!opens.IsDefault)
        {
            foreach (var entry in opens)
            {
                if (Conditions.Conditions.Holds(content, game, entry.When))
                {
                    return entry;
                }
            }
        }

        return null;
    }

    /// <summary>True when some entry's When holds. A hand-off entry counts whether or not the lineup is ready.</summary>
    public static bool CanOpen(GameContent content, GameState game, ImmutableArray<OpensEntry> opens) =>
        First(content, game, opens) is not null;

    /// <summary>The distance a trigger reaches in a town: the town's reach, or else the settings reach.</summary>
    public static double ReachOf(GameContent content, TownDefinition town) => town.Reach ?? content.Settings.Reach;

    /// <summary>Opens a person's first entry that holds. Refusals are listed on the session method; nothing holding is a success with Nothing.</summary>
    public static Result<TownStep> TriggerPerson(GameContent content, GameState game, string characterId, long now)
    {
        var town = Guard(content, game, out var refusal);
        if (town is null)
        {
            return refusal!;
        }

        var present = TownRules.Present(content, game);
        var standing = present.People.FirstOrDefault(p => p.Character == characterId);
        if (standing is null)
        {
            return Fail(ErrorCode.TARGET_NOT_FOUND, $"No such person here: \"{characterId}\"");
        }

        if (!Geometry.WithinReach(standing.At, game.Town!.Position, ReachOf(content, town)))
        {
            return Fail(ErrorCode.OUT_OF_REACH, $"\"{characterId}\" is out of reach");
        }

        var person = town.People.First(p => p.Character == characterId);
        return Open(content, game, First(content, game, person.Opens), now);
    }

    /// <summary>Opens an interact spot's first entry that holds. A walk-over spot is refused: those fire only by movement.</summary>
    public static Result<TownStep> TriggerSpot(GameContent content, GameState game, string spotId, long now)
    {
        var town = Guard(content, game, out var refusal);
        if (town is null)
        {
            return refusal!;
        }

        var spot = TownRules.Present(content, game).Spots.FirstOrDefault(s => s.Id == spotId);
        if (spot is null)
        {
            return Fail(ErrorCode.TARGET_NOT_FOUND, $"No such spot here: \"{spotId}\"");
        }

        if (spot.Trigger == SpotTrigger.WalkOver)
        {
            return Fail(ErrorCode.VALIDATION_ERROR, $"Spot \"{spotId}\" is a walk-over spot; it fires only when the player walks into it");
        }

        if (!Geometry.WithinReach(spot.Area, game.Town!.Position, ReachOf(content, town)))
        {
            return Fail(ErrorCode.OUT_OF_REACH, $"Spot \"{spotId}\" is out of reach");
        }

        return Open(content, game, First(content, game, spot.Opens), now);
    }

    /// <summary>
    /// After a move from one point to another: for each present walk-over spot in content order, when the player went from outside its
    /// area to inside, opens its first entry that holds. The first spot that opens something wins; a spot with nothing to open does not
    /// block the ones after it.
    /// </summary>
    public static Result<TownStep> WalkOver(GameContent content, GameState before, GameState after, long now)
    {
        if (before.Town is null || after.Town is null)
        {
            return Result<TownStep>.Ok(new TownStep(after, new TownEvent.Nothing()));
        }

        foreach (var spot in TownRules.Present(content, after).Spots)
        {
            if (spot.Trigger != SpotTrigger.WalkOver
                || Geometry.Contains(spot.Area, before.Town.Position)
                || !Geometry.Contains(spot.Area, after.Town.Position))
            {
                continue;
            }

            if (First(content, after, spot.Opens) is { } entry)
            {
                return Open(content, after, entry, now);
            }
        }

        return Result<TownStep>.Ok(new TownStep(after, new TownEvent.Nothing()));
    }

    /// <summary>
    /// The dispatch point for an entry that holds. A conversation entry starts through the conversation rules. A hand-off entry leaves
    /// for the fight through the hand-off rules; a lineup that is not ready is refused (LINEUP_NOT_READY) and the player stays.
    /// </summary>
    private static Result<TownStep> Open(GameContent content, GameState game, OpensEntry? entry, long now)
    {
        if (entry is null)
        {
            return Result<TownStep>.Ok(new TownStep(game, new TownEvent.Nothing()));
        }

        if (entry.ConversationId is { } conversationId)
        {
            var started = ConversationRules.Start(content, game, conversationId, now);
            return started.IsOk
                ? Result<TownStep>.Ok(new TownStep(started.Value.Game, new TownEvent.ConversationStarted(conversationId)))
                : Result<TownStep>.Fail(started.Error);
        }

        if (entry.FightId is { } fightId)
        {
            var left = HandOffRules.Leave(content, game, fightId, now);
            return left.IsOk
                ? Result<TownStep>.Ok(new TownStep(left.Value, new TownEvent.HandedOff(fightId)))
                : Result<TownStep>.Fail(left.Error);
        }

        return Result<TownStep>.Ok(new TownStep(game, new TownEvent.Nothing()));
    }

    private static TownDefinition? Guard(GameContent content, GameState game, out Result<TownStep>? refusal)
    {
        var town = TownRules.Current(content, game);
        if (town is null)
        {
            refusal = Fail(ErrorCode.TOWN_NOT_ACTIVE, "There is no active town");
        }
        else if (game.HandOff is not null)
        {
            town = null;
            refusal = Fail(ErrorCode.TOWN_NOT_ACTIVE, "The town is complete");
        }
        else if (game.Conversation is not null)
        {
            town = null;
            refusal = Fail(ErrorCode.CONVERSATION_ALREADY_ACTIVE, "A conversation is already running");
        }
        else if (game.Combat is not null)
        {
            town = null;
            refusal = Fail(ErrorCode.COMBAT_ALREADY_ACTIVE, "Cannot open anything during combat");
        }
        else
        {
            refusal = null;
        }

        return town;
    }

    private static Result<TownStep> Fail(ErrorCode code, string message) =>
        Result<TownStep>.Fail(new RuleError(code, message));
}
