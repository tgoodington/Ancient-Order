using System.Collections.Immutable;
using AncientOrder.Rules.Reactions;
using AncientOrder.Rules.State;

namespace AncientOrder.Rules.Town;

public sealed record ObstacleView(string Id, Polygon Footprint, double Height, string? Label);

/// <summary>A person present. Facing is degrees, display only. Demeanor is how they carry themselves toward the player. CanOpen is true when something it opens holds right now.</summary>
public sealed record TownPersonView(string CharacterId, string Name, Point Position, double Facing, bool CanOpen, Demeanor Demeanor);

/// <summary>A spot present. Shown says whether it is drawn. CanOpen is true when something it opens holds right now.</summary>
public sealed record TownSpotView(string Id, string? Name, Area Area, SpotTrigger Trigger, bool Shown, bool CanOpen);

/// <summary>
/// The town the player is in, ready to show: ground and obstacles, where the player stands, and the people and spots present, in content order.
/// IsInside and InReach let a screen ask the rules' geometry without calling it (ADR-057). Reach is in meters.
/// </summary>
public sealed record TownView(
    string Id,
    string Name,
    ImmutableArray<Polygon> Ground,
    ImmutableArray<ObstacleView> Obstacles,
    Point Player,
    double Reach,
    ImmutableArray<TownPersonView> People,
    ImmutableArray<TownSpotView> Spots)
{
    /// <summary>The view for the player's town, or null when there is none.</summary>
    public static TownView? From(GameContent content, GameState game)
    {
        var town = TownRules.Current(content, game);
        if (town is null)
        {
            return null;
        }

        var present = TownRules.Present(content, game);
        var people = ImmutableArray.CreateBuilder<TownPersonView>(present.People.Length);
        foreach (var person in present.People)
        {
            content.Characters.TryGet(person.Character, out var definition);
            var demeanor = game.Characters.TryGetValue(person.Character, out var state)
                ? DemeanorRules.Of(content.Settings, state)
                : Demeanor.Neutral;
            var opens = town.People.FirstOrDefault(p => p.Character == person.Character)?.Opens ?? [];
            people.Add(new TownPersonView(
                person.Character, definition?.Name ?? person.Character, person.At, person.Facing, TownTriggers.CanOpen(content, game, opens), demeanor));
        }

        var spots = ImmutableArray.CreateBuilder<TownSpotView>(present.Spots.Length);
        foreach (var spot in present.Spots)
        {
            spots.Add(new TownSpotView(spot.Id, spot.Name, spot.Area, spot.Trigger, spot.IsShown, TownTriggers.CanOpen(content, game, spot.Opens)));
        }

        return new TownView(
            town.Id,
            town.Name,
            town.Ground,
            [.. town.Obstacles.Select(o => new ObstacleView(o.Id, o.Footprint, o.Height, o.Label))],
            game.Town!.Position,
            town.Reach ?? content.Settings.Reach,
            people.ToImmutable(),
            spots.ToImmutable());
    }

    /// <summary>True when the point is inside a present spot's area (its edge counts). False for an absent or unknown spot.</summary>
    public bool IsInside(string spotId, double x, double z)
    {
        foreach (var spot in Spots)
        {
            if (spot.Id == spotId)
            {
                return Geometry.Contains(spot.Area, new Point(x, z));
            }
        }

        return false;
    }

    /// <summary>
    /// True when the point is within Reach of a present person (their standing point) or a present spot (its area), by squared distance.
    /// A person is looked up before a spot of the same id. False for an absent or unknown id.
    /// </summary>
    public bool InReach(string id, double x, double z)
    {
        var point = new Point(x, z);
        foreach (var person in People)
        {
            if (person.CharacterId == id)
            {
                return Geometry.WithinReach(person.Position, point, Reach);
            }
        }

        foreach (var spot in Spots)
        {
            if (spot.Id == id)
            {
                return Geometry.WithinReach(spot.Area, point, Reach);
            }
        }

        return false;
    }
}
