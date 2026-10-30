using AncientOrder.Rules.Town;
using Godot;

namespace AncientOrder.Stage;

/// <summary>
/// A thin wrapper over NavigationServer3D (Godot marks that API experimental, so only this class touches it).
/// It bakes a navigation mesh from the town's ground and obstacle polygons, tells when the server has taken the mesh up,
/// and answers path and closest-point questions. It never forces a map update: readiness is the map's iteration id moving past
/// the value it had when the mesh was assigned.
/// </summary>
public sealed class TownNavigation
{
    public const float AgentRadius = 0.5f;
    public const float AgentHeight = 2.0f;

    private readonly NavigationRegion3D _region;
    private uint _baseIteration;
    private bool _assigned;
    private Vector3? _probe;

    public TownNavigation(NavigationRegion3D region)
    {
        _region = region;
    }

    private Rid Map => _region.GetNavigationMap();

    /// <summary>
    /// True once the server has applied the last baked mesh. Path and closest-point answers are only meaningful then.
    /// The map's iteration id moves past its assignment value first, but the very first mesh takes one more sync to show up in queries,
    /// so a vertex of the baked mesh must also be found by a closest-point query.
    /// </summary>
    public bool IsReady => _assigned && _probe is { } probe && NavigationServer3D.MapGetIterationId(Map) > _baseIteration && ClosestPoint(probe).DistanceTo(probe) < 0.1f;

    /// <summary>Bakes from the ground polygons (walkable) and the obstacle footprints (carved out, with their heights) and assigns the mesh to the region.</summary>
    public void Bake(IEnumerable<Polygon> ground, IEnumerable<ObstacleView> obstacles)
    {
        var source = new NavigationMeshSourceGeometryData3D();
        source.AddFaces(Triangles(ground), Transform3D.Identity);
        foreach (var obstacle in obstacles)
        {
            source.AddProjectedObstruction(Inflated(obstacle.Footprint), 0f, (float)obstacle.Height, true);
        }

        var mesh = new NavigationMesh
        {
            CellSize = NavigationServer3D.MapGetCellSize(Map),
            CellHeight = NavigationServer3D.MapGetCellHeight(Map),
            AgentRadius = AgentRadius,
            AgentHeight = AgentHeight,
        };
        NavigationServer3D.BakeFromSourceGeometryData(mesh, source);

        _assigned = false;
        var vertices = mesh.GetVertices();
        _probe = vertices.Length > 0 ? vertices[0] : null;
        if (_probe is null)
        {
            GD.PushError("TownNavigation: the ground produced no walkable navigation mesh");
        }

        _region.NavigationMesh = mesh;
        _baseIteration = NavigationServer3D.MapGetIterationId(Map);
        _assigned = true;
    }

    public Vector3[] GetPath(Vector3 from, Vector3 to) => NavigationServer3D.MapGetPath(Map, from, to, true);

    public Vector3 ClosestPoint(Vector3 point) => NavigationServer3D.MapGetClosestPoint(Map, point);

    /// <summary>Upward-facing triangles that cover the polygons, three vertices each, on the ground plane.</summary>
    public static Vector3[] Triangles(IEnumerable<Polygon> polygons)
    {
        var triangles = new List<Vector3>();
        foreach (var polygon in polygons)
        {
            var corners = Outline(polygon);
            var indices = Geometry2D.TriangulatePolygon(corners.Select(c => new Vector2(c.X, c.Z)).ToArray());
            if (indices.Length == 0)
            {
                GD.PushError("TownNavigation: a ground polygon could not be triangulated");
                continue;
            }

            for (var i = 0; i + 2 < indices.Length; i += 3)
            {
                Vector3 a = corners[indices[i]], b = corners[indices[i + 1]], c = corners[indices[i + 2]];
                if ((b - a).Cross(c - a).Y > 0)
                {
                    (b, c) = (c, b);
                }

                triangles.Add(a);
                triangles.Add(b);
                triangles.Add(c);
            }
        }

        return [.. triangles];
    }

    /// <summary>
    /// The footprint grown by the agent radius. A carved obstruction cuts the mesh along the footprint itself (unlike the ground edge, it is not
    /// pulled back by the agent radius), so a path could touch the edge and a rounded position could land just inside it.
    /// </summary>
    private static Vector3[] Inflated(Polygon footprint)
    {
        var grown = Geometry2D.OffsetPolygon(Outline(footprint).Select(c => new Vector2(c.X, c.Z)).ToArray(), AgentRadius, Geometry2D.PolyJoinType.Miter);
        return grown.Count == 0 ? Outline(footprint) : [.. grown[0].Select(v => new Vector3(v.X, 0f, v.Y))];
    }

    public static Vector3[] Outline(Polygon polygon) =>
        [.. polygon.Points.Select(p => new Vector3((float)p.X, 0f, (float)p.Z))];
}
