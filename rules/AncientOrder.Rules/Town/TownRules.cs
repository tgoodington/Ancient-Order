using System.Collections.Immutable;
using AncientOrder.Rules.Common;
using AncientOrder.Rules.State;

namespace AncientOrder.Rules.Town;

/// <summary>Where the player is: the town and a point on its ground, in world coordinates.</summary>
public sealed record TownPresence(string TownId, Point Position);

/// <summary>A person who is present: the character, where they stand, and which way they face (degrees, 0 when content gives none).</summary>
public sealed record PresentPerson(string Character, Point At, double Facing);

/// <summary>Who and what is present right now, in content order.</summary>
public sealed record TownPresent(ImmutableArray<PresentPerson> People, ImmutableArray<SpotDefinition> Spots)
{
    public static TownPresent None { get; } = new([], []);
}

public static class TownRules
{
    /// <summary>The walkable ground of a town. An unknown town has none (nothing is walkable).</summary>
    public static IWalkableGround Ground(GameContent content, string? townId) =>
        new AuthoredGround(content.Towns.TryGet(townId, out var town) ? town : null);

    /// <summary>
    /// The people and spots present in the player's town. A person is present when some placement's When holds and stands at the first
    /// such placement; a spot is present while its When holds. Worked out from state each time and never stored.
    /// </summary>
    public static TownPresent Present(GameContent content, GameState game)
    {
        if (game.Town is null || !content.Towns.TryGet(game.Town.TownId, out var town))
        {
            return TownPresent.None;
        }

        var people = ImmutableArray.CreateBuilder<PresentPerson>();
        foreach (var person in town.People)
        {
            foreach (var placement in person.Placements)
            {
                if (Conditions.Conditions.Holds(content, game, placement.When))
                {
                    people.Add(new PresentPerson(person.Character, placement.At, placement.Facing ?? 0));
                    break;
                }
            }
        }

        var spots = ImmutableArray.CreateBuilder<SpotDefinition>();
        foreach (var spot in town.Spots)
        {
            if (Conditions.Conditions.Holds(content, game, spot.When))
            {
                spots.Add(spot);
            }
        }

        return new TownPresent(people.ToImmutable(), spots.ToImmutable());
    }

    /// <summary>The player's town, or null when there is none.</summary>
    public static TownDefinition? Current(GameContent content, GameState game) =>
        game.Town is not null && content.Towns.TryGet(game.Town.TownId, out var town) ? town : null;

    /// <summary>Puts the player at a point of a town. Refused off walkable ground (NOT_WALKABLE) or in an unknown town (TOWN_NOT_ACTIVE).</summary>
    public static Result<GameState> Place(GameContent content, GameState game, string townId, Point point, long now)
    {
        if (!content.Towns.TryGet(townId, out _))
        {
            return Result<GameState>.Fail(new RuleError(ErrorCode.TOWN_NOT_ACTIVE, $"Unknown town: \"{townId}\""));
        }

        if (!Ground(content, townId).IsWalkable(point))
        {
            return Result<GameState>.Fail(new RuleError(ErrorCode.NOT_WALKABLE, "That point is not walkable ground"));
        }

        return Result<GameState>.Ok(game with { Town = new TownPresence(townId, point), Timestamp = now });
    }

    /// <summary>
    /// Moves the player within the current town. Refused with no town (TOWN_NOT_ACTIVE), during a conversation or a fight, and off walkable
    /// ground (NOT_WALKABLE). Non-finite values are the caller's to refuse first.
    /// </summary>
    public static Result<GameState> Move(GameContent content, GameState game, Point point, long now)
    {
        if (Current(content, game) is null)
        {
            return Result<GameState>.Fail(new RuleError(ErrorCode.TOWN_NOT_ACTIVE, "There is no active town"));
        }

        if (game.Conversation is not null)
        {
            return Result<GameState>.Fail(new RuleError(ErrorCode.CONVERSATION_ALREADY_ACTIVE, "Cannot move during a conversation"));
        }

        if (game.Combat is not null)
        {
            return Result<GameState>.Fail(new RuleError(ErrorCode.COMBAT_ALREADY_ACTIVE, "Cannot move during combat"));
        }

        return Place(content, game, game.Town!.TownId, point, now);
    }

    /// <summary>
    /// Moves the player (see Move), then fires the walk-over spot the move entered, if any: it opens its first entry that holds
    /// (see TownTriggers.WalkOver). Refusals are Move's; the game is unchanged then.
    /// </summary>
    public static Result<TownStep> MovePlayer(GameContent content, GameState game, Point point, long now)
    {
        var moved = Move(content, game, point, now);
        return moved.IsOk
            ? TownTriggers.WalkOver(content, game, moved.Value, now)
            : Result<TownStep>.Fail(moved.Error);
    }
}
