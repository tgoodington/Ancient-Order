using System.Collections.Immutable;

namespace AncientOrder.Rules.Town;

/// <summary>A point on the ground plane in meters: X east, Z south. Godot maps it to Vector3(x, 0, z), Y up.</summary>
public sealed record Point(double X, double Z);

/// <summary>A closed shape given by its corners in order (the last joins the first). Content needs at least 3.</summary>
public sealed record Polygon(ImmutableArray<Point> Points);

/// <summary>An area on the ground: a circle or a polygon.</summary>
public abstract record Area
{
    private protected Area()
    {
    }

    public sealed record Circle(Point Center, double Radius) : Area;

    public sealed record PolygonArea(Polygon Polygon) : Area;
}

/// <summary>
/// Exact ground-plane geometry: only comparisons, products, and sums (no square roots), so results never vary across machines.
/// A point on an edge or on a circle's rim counts as inside.
/// </summary>
public static class Geometry
{
    public static bool Contains(Polygon polygon, Point point)
    {
        var corners = polygon.Points;
        if (corners.IsDefaultOrEmpty)
        {
            return false;
        }

        var inside = false;
        for (var i = 0; i < corners.Length; i++)
        {
            var a = corners[i];
            var b = corners[(i + 1) % corners.Length];
            var cross = ((b.X - a.X) * (point.Z - a.Z)) - ((point.X - a.X) * (b.Z - a.Z));
            if (cross == 0
                && point.X >= (a.X < b.X ? a.X : b.X) && point.X <= (a.X > b.X ? a.X : b.X)
                && point.Z >= (a.Z < b.Z ? a.Z : b.Z) && point.Z <= (a.Z > b.Z ? a.Z : b.Z))
            {
                return true;
            }

            if ((a.Z > point.Z) != (b.Z > point.Z) && (b.Z > a.Z ? cross > 0 : cross < 0))
            {
                inside = !inside;
            }
        }

        return inside;
    }

    public static bool Contains(Area area, Point point) => area switch
    {
        Area.Circle c => DistanceSquared(c.Center, point) <= c.Radius * c.Radius,
        Area.PolygonArea p => Contains(p.Polygon, point),
        _ => throw new InvalidOperationException($"Unknown area {area.GetType().Name}"),
    };

    public static double DistanceSquared(Point a, Point b)
    {
        var dx = a.X - b.X;
        var dz = a.Z - b.Z;
        return (dx * dx) + (dz * dz);
    }

    /// <summary>Squared distance from a point to a polygon: zero inside it or on its edge.</summary>
    public static double DistanceSquared(Polygon polygon, Point point)
    {
        if (Contains(polygon, point))
        {
            return 0;
        }

        var corners = polygon.Points;
        var best = double.PositiveInfinity;
        for (var i = 0; i < corners.Length; i++)
        {
            var d = DistanceSquaredToSegment(corners[i], corners[(i + 1) % corners.Length], point);
            if (d < best)
            {
                best = d;
            }
        }

        return best;
    }

    /// <summary>
    /// True when the point is within reach of an area: inside it, or no further than reach from its edge.
    /// A circle compares the squared distance to its center with (radius + reach) squared, which needs no square root.
    /// </summary>
    public static bool WithinReach(Area area, Point point, double reach) => area switch
    {
        Area.Circle c => DistanceSquared(c.Center, point) <= (c.Radius + reach) * (c.Radius + reach),
        Area.PolygonArea p => DistanceSquared(p.Polygon, point) <= reach * reach,
        _ => throw new InvalidOperationException($"Unknown area {area.GetType().Name}"),
    };

    /// <summary>True when the two points are no further apart than reach.</summary>
    public static bool WithinReach(Point from, Point to, double reach) => DistanceSquared(from, to) <= reach * reach;

    private static double DistanceSquaredToSegment(Point a, Point b, Point p)
    {
        var dx = b.X - a.X;
        var dz = b.Z - a.Z;
        var length = (dx * dx) + (dz * dz);
        if (length == 0)
        {
            return DistanceSquared(a, p);
        }

        var t = (((p.X - a.X) * dx) + ((p.Z - a.Z) * dz)) / length;
        if (t <= 0)
        {
            return DistanceSquared(a, p);
        }

        return t >= 1 ? DistanceSquared(b, p) : DistanceSquared(new Point(a.X + (t * dx), a.Z + (t * dz)), p);
    }
}
