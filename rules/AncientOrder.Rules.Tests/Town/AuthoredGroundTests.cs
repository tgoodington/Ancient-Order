using AncientOrder.Rules.Town;

namespace AncientOrder.Rules.Tests.TownWalking;

public class AuthoredGroundTests
{
    private static Polygon Rectangle(double x0, double z0, double x1, double z1) =>
        new([new(x0, z0), new(x1, z0), new(x1, z1), new(x0, z1)]);

    private static AuthoredGround Ground(Polygon[] ground, params ObstacleDefinition[] obstacles) =>
        new(new TownDefinition("t", "Town", [.. ground], [.. obstacles], new Point(1, 1), [], []));

    [Fact]
    public void Ground_is_the_union_of_its_polygons()
    {
        var ground = Ground([Rectangle(0, 0, 10, 10), Rectangle(10, 4, 20, 6)]);

        Assert.True(ground.IsWalkable(new Point(5, 5)));
        Assert.True(ground.IsWalkable(new Point(15, 5)));
        Assert.True(ground.IsWalkable(new Point(10, 5)));
        Assert.False(ground.IsWalkable(new Point(15, 8)));
        Assert.False(ground.IsWalkable(new Point(-0.01, 5)));
        Assert.False(ground.IsWalkable(new Point(20.01, 5)));
    }

    [Fact]
    public void An_obstacle_footprint_is_cut_out_of_the_ground_and_its_edge_is_blocked()
    {
        var ground = Ground([Rectangle(0, 0, 20, 20)], new ObstacleDefinition("fountain", Rectangle(8, 8, 12, 12), 1.5));

        Assert.True(ground.IsWalkable(new Point(7.99, 10)));
        Assert.False(ground.IsWalkable(new Point(8, 10)));
        Assert.False(ground.IsWalkable(new Point(10, 10)));
        Assert.False(ground.IsWalkable(new Point(12, 12)));
        Assert.True(ground.IsWalkable(new Point(12.01, 10)));
        Assert.True(ground.IsWalkable(new Point(10, 14)));
    }

    [Fact]
    public void Every_obstacle_blocks_and_one_that_overhangs_the_ground_blocks_only_where_it_covers()
    {
        var ground = Ground(
            [Rectangle(0, 0, 10, 10)],
            new ObstacleDefinition("a", Rectangle(1, 1, 3, 3), 1),
            new ObstacleDefinition("b", Rectangle(6, 6, 14, 14), 1));

        Assert.False(ground.IsWalkable(new Point(2, 2)));
        Assert.False(ground.IsWalkable(new Point(8, 8)));
        Assert.True(ground.IsWalkable(new Point(5, 5)));
        Assert.True(ground.IsWalkable(new Point(8, 2)));
        Assert.False(ground.IsWalkable(new Point(12, 12)));
    }

    [Fact]
    public void An_obstacle_standing_where_two_ground_polygons_overlap_blocks_both()
    {
        var ground = Ground(
            [Rectangle(0, 0, 10, 10), Rectangle(5, 0, 15, 10)],
            new ObstacleDefinition("pillar", Rectangle(6, 4, 8, 6), 1));

        Assert.False(ground.IsWalkable(new Point(7, 5)));
        Assert.True(ground.IsWalkable(new Point(12, 5)));
        Assert.True(ground.IsWalkable(new Point(3, 5)));
    }

    [Fact]
    public void No_town_means_nothing_is_walkable()
    {
        var ground = new AuthoredGround(null);

        Assert.False(ground.IsWalkable(new Point(0, 0)));
        Assert.False(ground.IsWalkable(new Point(5, 5)));
    }

    [Fact]
    public void Points_that_are_not_finite_or_missing_are_never_walkable()
    {
        var ground = Ground([Rectangle(0, 0, 10, 10)]);

        Assert.False(ground.IsWalkable(new Point(double.NaN, 5)));
        Assert.False(ground.IsWalkable(new Point(5, double.NaN)));
        Assert.False(ground.IsWalkable(new Point(double.PositiveInfinity, 5)));
        Assert.False(ground.IsWalkable(new Point(5, double.NegativeInfinity)));
        Assert.False(ground.IsWalkable(null!));
    }

    [Fact]
    public void A_town_with_no_obstacles_is_walkable_wherever_it_has_ground()
    {
        var ground = Ground([Rectangle(0, 0, 4, 4)]);

        Assert.True(ground.IsWalkable(new Point(4, 4)));
        Assert.False(ground.IsWalkable(new Point(4.1, 4)));
    }
}
