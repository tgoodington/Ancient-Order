using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Globalization;
using AncientOrder.Rules.Combat;
using AncientOrder.Rules.Common;
using AncientOrder.Rules.Conditions;
using AncientOrder.Rules.Conversations;
using AncientOrder.Rules.State;

namespace AncientOrder.Rules.Town;

/// <summary>The towns in content order plus a lookup by id.</summary>
public sealed record TownCatalog
{
    private TownCatalog(ImmutableArray<TownDefinition> definitions)
    {
        Definitions = definitions;
        Index = definitions.ToFrozenDictionary(d => d.Id, StringComparer.Ordinal);
    }

    public static TownCatalog Empty { get; } = new(ImmutableArray<TownDefinition>.Empty);

    public ImmutableArray<TownDefinition> Definitions { get; }

    public FrozenDictionary<string, TownDefinition> Index { get; }

    public bool TryGet(string? id, out TownDefinition definition)
    {
        if (id is not null && Index.TryGetValue(id, out var found))
        {
            definition = found;
            return true;
        }

        definition = null!;
        return false;
    }

    /// <summary>
    /// Checks unique town ids; at least one ground polygon; polygons of 3 or more finite points; obstacle ids unique per town with
    /// a positive height; finite start; positive reach; people name real characters and have a placement; spot ids unique per town with a
    /// valid area; conditions (see Conditions.Check). Whether people, spots, and starts sit on walkable ground is not checked here.
    /// A default array means no towns. Paths read "towns/0/spots/1/when/0".
    /// </summary>
    public static Result<TownCatalog> Create(ImmutableArray<TownDefinition> definitions, CharacterCatalog characters, ConversationCatalog conversations, EncounterCatalog? encounters = null)
    {
        if (definitions.IsDefault)
        {
            return Result<TownCatalog>.Ok(Empty);
        }

        var choices = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (var conversation in conversations.Definitions)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var step in conversation.Steps)
            {
                foreach (var choice in step.Choices)
                {
                    ids.Add(choice.Id);
                }
            }

            choices[conversation.Id] = ids;
        }

        var refs = new ConditionReferences(characters, encounters, choices);
        var details = ImmutableArray.CreateBuilder<RuleErrorDetail>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < definitions.Length; i++)
        {
            var d = definitions[i];
            var path = "towns/" + Num(i);
            if (d is null)
            {
                details.Add(new RuleErrorDetail(path, "town must not be null"));
                continue;
            }

            if (string.IsNullOrEmpty(d.Id))
            {
                details.Add(new RuleErrorDetail(path + "/id", "id must not be empty"));
            }
            else
            {
                if (d.Id.Length > CharacterCatalog.MaxIdLength)
                {
                    details.Add(new RuleErrorDetail(path + "/id", $"id must be 1 to {CharacterCatalog.MaxIdLength} characters"));
                }

                if (!seen.Add(d.Id))
                {
                    details.Add(new RuleErrorDetail(path + "/id", $"duplicate town id: {d.Id}"));
                }
            }

            CheckTown(d, path, characters, conversations, refs, details);
        }

        if (details.Count > 0)
        {
            return Result<TownCatalog>.Fail(
                new RuleError(ErrorCode.VALIDATION_ERROR, "Town catalog is invalid: " + details[0].Message, details.ToImmutable()));
        }

        return Result<TownCatalog>.Ok(new TownCatalog(definitions));
    }

    private static void CheckTown(TownDefinition d, string path, CharacterCatalog characters, ConversationCatalog conversations, ConditionReferences refs, ImmutableArray<RuleErrorDetail>.Builder details)
    {
        if (string.IsNullOrEmpty(d.Name))
        {
            details.Add(new RuleErrorDetail(path + "/name", "name must not be empty"));
        }

        if (d.Ground.IsDefaultOrEmpty)
        {
            details.Add(new RuleErrorDetail(path + "/ground", "a town needs at least one ground polygon"));
        }
        else
        {
            for (var g = 0; g < d.Ground.Length; g++)
            {
                CheckPolygon(d.Ground[g], path + "/ground/" + Num(g), details);
            }
        }

        var obstacleIds = new HashSet<string>(StringComparer.Ordinal);
        for (var o = 0; !d.Obstacles.IsDefault && o < d.Obstacles.Length; o++)
        {
            var obstacle = d.Obstacles[o];
            var op = path + "/obstacles/" + Num(o);
            if (obstacle is null)
            {
                details.Add(new RuleErrorDetail(op, "obstacle must not be null"));
                continue;
            }

            if (string.IsNullOrEmpty(obstacle.Id))
            {
                details.Add(new RuleErrorDetail(op + "/id", "id must not be empty"));
            }
            else if (!obstacleIds.Add(obstacle.Id))
            {
                details.Add(new RuleErrorDetail(op + "/id", $"duplicate obstacle id: {obstacle.Id}"));
            }

            CheckPolygon(obstacle.Footprint, op + "/footprint", details);
            if (!double.IsFinite(obstacle.Height) || obstacle.Height <= 0)
            {
                details.Add(new RuleErrorDetail(op + "/height", "height must be a positive number"));
            }
        }

        CheckPoint(d.Start, path + "/start", details);
        if (d.Reach is { } reach && (!double.IsFinite(reach) || reach <= 0))
        {
            details.Add(new RuleErrorDetail(path + "/reach", "reach must be a positive number"));
        }

        for (var p = 0; !d.People.IsDefault && p < d.People.Length; p++)
        {
            var person = d.People[p];
            var pp = path + "/people/" + Num(p);
            if (person is null)
            {
                details.Add(new RuleErrorDetail(pp, "person must not be null"));
                continue;
            }

            if (!characters.TryGet(person.Character, out _))
            {
                details.Add(new RuleErrorDetail(pp + "/character", $"unknown character: {person.Character}"));
            }

            if (person.Placements.IsDefaultOrEmpty)
            {
                details.Add(new RuleErrorDetail(pp + "/placements", "a person needs at least one placement"));
                continue;
            }

            for (var l = 0; l < person.Placements.Length; l++)
            {
                var placement = person.Placements[l];
                var lp = pp + "/placements/" + Num(l);
                if (placement is null)
                {
                    details.Add(new RuleErrorDetail(lp, "placement must not be null"));
                    continue;
                }

                CheckPoint(placement.At, lp + "/at", details);
                if (placement.Facing is { } facing && !double.IsFinite(facing))
                {
                    details.Add(new RuleErrorDetail(lp + "/facing", "facing must be a finite number"));
                }

                Conditions.Conditions.Check(placement.When, lp + "/when", refs, details);
            }

            CheckOpens(person.Opens, pp + "/opens", conversations, refs, details);
        }

        var spotIds = new HashSet<string>(StringComparer.Ordinal);
        for (var s = 0; !d.Spots.IsDefault && s < d.Spots.Length; s++)
        {
            var spot = d.Spots[s];
            var sp = path + "/spots/" + Num(s);
            if (spot is null)
            {
                details.Add(new RuleErrorDetail(sp, "spot must not be null"));
                continue;
            }

            if (string.IsNullOrEmpty(spot.Id))
            {
                details.Add(new RuleErrorDetail(sp + "/id", "id must not be empty"));
            }
            else if (!spotIds.Add(spot.Id))
            {
                details.Add(new RuleErrorDetail(sp + "/id", $"duplicate spot id: {spot.Id}"));
            }

            switch (spot.Area)
            {
                case Area.Circle circle:
                    CheckPoint(circle.Center, sp + "/area/circle/center", details);
                    if (!double.IsFinite(circle.Radius) || circle.Radius <= 0)
                    {
                        details.Add(new RuleErrorDetail(sp + "/area/circle/radius", "radius must be a positive number"));
                    }

                    break;
                case Area.PolygonArea polygon:
                    CheckPolygon(polygon.Polygon, sp + "/area/polygon", details);
                    break;
                default:
                    details.Add(new RuleErrorDetail(sp + "/area", "a spot needs an area"));
                    break;
            }

            Conditions.Conditions.Check(spot.When, sp + "/when", refs, details);
            CheckOpens(spot.Opens, sp + "/opens", conversations, refs, details);
        }
    }

    private static void CheckOpens(ImmutableArray<OpensEntry> opens, string path, ConversationCatalog conversations, ConditionReferences refs, ImmutableArray<RuleErrorDetail>.Builder details)
    {
        for (var i = 0; !opens.IsDefault && i < opens.Length; i++)
        {
            var entry = opens[i];
            var ep = path + "/" + Num(i);
            if (entry is null)
            {
                details.Add(new RuleErrorDetail(ep, "opens entry must not be null"));
                continue;
            }

            if ((entry.ConversationId is null) == (entry.FightId is null))
            {
                details.Add(new RuleErrorDetail(ep, "an opens entry needs exactly one of a conversation or a hand-off"));
            }
            else if (entry.ConversationId is { } id && !conversations.TryGet(id, out _))
            {
                details.Add(new RuleErrorDetail(ep + "/conversation", $"unknown conversation: {id}"));
            }
            else if (entry.FightId is { } fightId && refs.Encounters is not null && !refs.Encounters.TryGet(fightId, out _))
            {
                details.Add(new RuleErrorDetail(ep + "/handOff", $"unknown fight: {fightId}"));
            }

            Conditions.Conditions.Check(entry.When, ep + "/when", refs, details);
        }
    }

    private static void CheckPolygon(Polygon? polygon, string path, ImmutableArray<RuleErrorDetail>.Builder details)
    {
        if (polygon is null || polygon.Points.IsDefault || polygon.Points.Length < 3)
        {
            details.Add(new RuleErrorDetail(path, "a polygon needs at least 3 points"));
            return;
        }

        for (var i = 0; i < polygon.Points.Length; i++)
        {
            CheckPoint(polygon.Points[i], path + "/" + Num(i), details);
        }
    }

    private static void CheckPoint(Point? point, string path, ImmutableArray<RuleErrorDetail>.Builder details)
    {
        if (point is null || !double.IsFinite(point.X) || !double.IsFinite(point.Z))
        {
            details.Add(new RuleErrorDetail(path, "a point must be two finite numbers"));
        }
    }

    private static string Num(int i) => i.ToString(CultureInfo.InvariantCulture);
}
