namespace AncientOrder.Rules.Town;

/// <summary>Answers whether a point can be stood on. A level's navigation data can implement it later.</summary>
public interface IWalkableGround
{
    bool IsWalkable(Point point);
}

/// <summary>Ground authored in content: inside some ground polygon and inside no obstacle footprint (an obstacle's edge is blocked). No town means nothing is walkable.</summary>
public sealed class AuthoredGround(TownDefinition? town) : IWalkableGround
{
    public bool IsWalkable(Point point)
    {
        if (town is null || point is null || !double.IsFinite(point.X) || !double.IsFinite(point.Z))
        {
            return false;
        }

        var onGround = false;
        foreach (var polygon in town.Ground)
        {
            if (Geometry.Contains(polygon, point))
            {
                onGround = true;
                break;
            }
        }

        if (!onGround)
        {
            return false;
        }

        foreach (var obstacle in town.Obstacles)
        {
            if (Geometry.Contains(obstacle.Footprint, point))
            {
                return false;
            }
        }

        return true;
    }
}
