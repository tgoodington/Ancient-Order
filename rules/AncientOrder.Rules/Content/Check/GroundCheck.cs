using System.Globalization;
using AncientOrder.Rules.Common;
using AncientOrder.Rules.Content.Packs;
using AncientOrder.Rules.State;
using AncientOrder.Rules.Town;

namespace AncientOrder.Rules.Content.Check;

/// <summary>
/// A town's start, every person placement, and every spot anchor must be on the walkable ground the town authors (AuthoredGround).
/// A circle's anchor is its center. A polygon is reachable when any vertex or the vertex average is walkable.
/// </summary>
internal static class GroundCheck
{
    public static void Run(GameContent content, ContentProvenance provenance, List<RuleErrorDetail> problems)
    {
        for (var t = 0; t < content.Towns.Definitions.Length; t++)
        {
            var town = content.Towns.Definitions[t];
            var ground = new AuthoredGround(town);
            var tp = "towns/" + PackWalk.Num(t);

            if (!ground.IsWalkable(town.Start))
            {
                problems.Add(new RuleErrorDetail(provenance.Translate(tp + "/start"), $"town '{town.Id}' starts the player off the walkable ground at {Show(town.Start)}"));
            }

            for (var p = 0; !town.People.IsDefault && p < town.People.Length; p++)
            {
                var person = town.People[p];
                for (var l = 0; l < person.Placements.Length; l++)
                {
                    if (!ground.IsWalkable(person.Placements[l].At))
                    {
                        problems.Add(new RuleErrorDetail(
                            provenance.Translate(tp + "/people/" + PackWalk.Num(p) + "/placements/" + PackWalk.Num(l) + "/at"),
                            $"town '{town.Id}' places '{person.Character}' off the walkable ground at {Show(person.Placements[l].At)}"));
                    }
                }
            }

            for (var s = 0; !town.Spots.IsDefault && s < town.Spots.Length; s++)
            {
                var spot = town.Spots[s];
                if (!AnchorWalkable(spot.Area, ground, out var anchor))
                {
                    problems.Add(new RuleErrorDetail(
                        provenance.Translate(tp + "/spots/" + PackWalk.Num(s) + "/area"),
                        $"town '{town.Id}' spot '{spot.Id}' is off the walkable ground at {Show(anchor)}"));
                }
            }
        }
    }

    private static bool AnchorWalkable(Area area, AuthoredGround ground, out Point anchor)
    {
        switch (area)
        {
            case Area.Circle circle:
                anchor = circle.Center;
                return ground.IsWalkable(circle.Center);
            case Area.PolygonArea polygon:
                var corners = polygon.Polygon.Points;
                double x = 0;
                double z = 0;
                foreach (var corner in corners)
                {
                    if (ground.IsWalkable(corner))
                    {
                        anchor = corner;
                        return true;
                    }

                    x += corner.X;
                    z += corner.Z;
                }

                anchor = new Point(x / corners.Length, z / corners.Length);
                return ground.IsWalkable(anchor);
            default:
                anchor = new Point(0, 0);
                return false;
        }
    }

    private static string Show(Point point) =>
        string.Create(CultureInfo.InvariantCulture, $"[{point.X}, {point.Z}]");
}
