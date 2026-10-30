using System.Collections.Immutable;
using AncientOrder.Rules.Town;

namespace AncientOrder.Rules.Tests.TownWalking;

public class GeometryTests
{
    private static Polygon Square(double min, double max) =>
        new([new(min, min), new(max, min), new(max, max), new(min, max)]);

    [Fact]
    public void A_polygon_holds_its_inside_its_edges_and_its_corners_but_not_outside_points()
    {
        var square = Square(0, 4);

        Assert.True(Geometry.Contains(square, new Point(2, 2)));
        Assert.True(Geometry.Contains(square, new Point(0, 2)));
        Assert.True(Geometry.Contains(square, new Point(4, 2)));
        Assert.True(Geometry.Contains(square, new Point(2, 0)));
        Assert.True(Geometry.Contains(square, new Point(2, 4)));
        Assert.True(Geometry.Contains(square, new Point(0, 0)));
        Assert.True(Geometry.Contains(square, new Point(4, 4)));
        Assert.False(Geometry.Contains(square, new Point(4.0001, 2)));
        Assert.False(Geometry.Contains(square, new Point(-0.0001, 2)));
        Assert.False(Geometry.Contains(square, new Point(2, 4.0001)));
    }

    [Fact]
    public void A_polygon_without_corners_holds_nothing_and_the_winding_order_does_not_matter()
    {
        Assert.False(Geometry.Contains(new Polygon(default), new Point(0, 0)));
        Assert.False(Geometry.Contains(new Polygon(ImmutableArray<Point>.Empty), new Point(0, 0)));

        var reversed = new Polygon([new(0, 4), new(4, 4), new(4, 0), new(0, 0)]);
        Assert.True(Geometry.Contains(reversed, new Point(1, 1)));
        Assert.True(Geometry.Contains(reversed, new Point(4, 2)));
        Assert.False(Geometry.Contains(reversed, new Point(5, 2)));
    }

    [Fact]
    public void A_triangle_includes_its_slanted_edge()
    {
        var triangle = new Polygon([new(0, 0), new(4, 0), new(0, 4)]);

        Assert.True(Geometry.Contains(triangle, new Point(2, 2)));
        Assert.True(Geometry.Contains(triangle, new Point(1, 1)));
        Assert.False(Geometry.Contains(triangle, new Point(3, 3)));
        Assert.False(Geometry.Contains(triangle, new Point(2.01, 2.01)));
    }

    [Fact]
    public void A_circle_holds_its_rim_and_nothing_further_by_squared_distance()
    {
        var circle = new Area.Circle(new Point(2, 2), 3);

        Assert.True(Geometry.Contains(circle, new Point(2, 2)));
        Assert.True(Geometry.Contains(circle, new Point(5, 2)));
        Assert.True(Geometry.Contains(circle, new Point(2, -1)));
        Assert.False(Geometry.Contains(circle, new Point(5.001, 2)));
        Assert.True(Geometry.Contains(new Area.Circle(new Point(0, 0), 5), new Point(3, 4)));
        Assert.False(Geometry.Contains(new Area.Circle(new Point(0, 0), 5), new Point(3, 4.001)));
    }

    [Fact]
    public void An_area_dispatches_to_its_own_shape()
    {
        var polygon = new Area.PolygonArea(Square(0, 2));

        Assert.True(Geometry.Contains(polygon, new Point(2, 2)));
        Assert.False(Geometry.Contains(polygon, new Point(2.5, 1)));
    }

    [Fact]
    public void Distances_are_squared_and_never_rooted()
    {
        Assert.Equal(25, Geometry.DistanceSquared(new Point(0, 0), new Point(3, 4)));
        Assert.Equal(25, Geometry.DistanceSquared(new Point(3, 4), new Point(0, 0)));
        Assert.Equal(0, Geometry.DistanceSquared(new Point(1.5, -2), new Point(1.5, -2)));
    }

    [Fact]
    public void The_distance_to_a_polygon_is_zero_inside_and_on_the_edge_and_otherwise_measured_to_the_nearest_edge_or_corner()
    {
        var square = Square(0, 4);

        Assert.Equal(0, Geometry.DistanceSquared(square, new Point(2, 2)));
        Assert.Equal(0, Geometry.DistanceSquared(square, new Point(4, 1)));
        Assert.Equal(9, Geometry.DistanceSquared(square, new Point(7, 2)));
        Assert.Equal(2, Geometry.DistanceSquared(square, new Point(5, 5)));
        Assert.Equal(25, Geometry.DistanceSquared(square, new Point(-3, -4)));
    }

    [Fact]
    public void Reach_to_a_point_includes_exactly_the_reach()
    {
        Assert.True(Geometry.WithinReach(new Point(0, 0), new Point(3, 4), 5));
        Assert.False(Geometry.WithinReach(new Point(0, 0), new Point(3, 4), 4.999));
        Assert.True(Geometry.WithinReach(new Point(1, 1), new Point(1, 1), 0));
    }

    [Fact]
    public void Reach_to_a_circle_counts_radius_plus_reach_and_to_a_polygon_counts_the_distance_from_its_edge()
    {
        var circle = new Area.Circle(new Point(0, 0), 2);
        Assert.True(Geometry.WithinReach(circle, new Point(5, 0), 3));
        Assert.False(Geometry.WithinReach(circle, new Point(5.01, 0), 3));
        Assert.True(Geometry.WithinReach(circle, new Point(0, 0), 0));

        var polygon = new Area.PolygonArea(Square(0, 4));
        Assert.True(Geometry.WithinReach(polygon, new Point(6, 2), 2));
        Assert.False(Geometry.WithinReach(polygon, new Point(6.01, 2), 2));
        Assert.True(Geometry.WithinReach(polygon, new Point(2, 2), 0));
        Assert.True(Geometry.WithinReach(polygon, new Point(5, 5), Math.Sqrt(2) + 0.000001));
        Assert.False(Geometry.WithinReach(polygon, new Point(5, 5), 1.4));
    }
}
