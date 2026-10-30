using AncientOrder.Rules.Town;
using Godot;

namespace AncientOrder.Stage;

/// <summary>
/// The town's placeholder look, built from content at run time: ground and obstacle meshes, and the builders that hand the stage its markers.
/// Nothing here is final art. Picking bodies come from TownStage itself.
/// </summary>
public static class PlaceholderMeshes
{
    private const string Root = "res://town/visuals/";
    private const float SpotLift = 0.03f;
    private const int CircleSegments = 48;

    private static SystemFont? _labelFont;

    /// <summary>Sets any build hook that is still empty to the placeholder look.</summary>
    public static void Install(TownStage stage)
    {
        stage.GroundBuilder ??= Ground;
        stage.ObstacleBuilder ??= Obstacle;
        stage.PersonBuilder ??= Person;
        stage.SpotBuilder ??= Spot;
        stage.PlayerBuilder ??= Player;
    }

    public static Material Material(string name) => GD.Load<Material>($"{Root}materials/{name}.tres");

    /// <summary>The ground polygons as one flat mesh at y = 0. A polygon that cannot be triangulated is skipped (TownNavigation logs it).</summary>
    public static Node3D? Ground(TownView view)
    {
        var faces = TownNavigation.Triangles(view.Ground);
        if (faces.Length == 0)
        {
            return null;
        }

        var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        surface.SetNormal(Vector3.Up);
        foreach (var vertex in faces)
        {
            surface.AddVertex(vertex);
        }

        return new MeshInstance3D
        {
            Name = "GroundLook",
            Mesh = surface.Commit(),
            MaterialOverride = Material("ground"),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
    }

    /// <summary>A block extruded from the footprint to its height: walls, a lighter top cap, and the label (if any) above it.</summary>
    public static Node3D Obstacle(ObstacleView obstacle)
    {
        var corners = TownNavigation.Outline(obstacle.Footprint);
        var height = (float)obstacle.Height;
        var counterClockwise = SignedArea(corners) > 0;

        var walls = new SurfaceTool();
        walls.Begin(Mesh.PrimitiveType.Triangles);
        walls.SetMaterial(Material("building_wall"));
        for (var i = 0; i < corners.Length; i++)
        {
            var a = corners[i];
            var b = corners[(i + 1) % corners.Length];
            var edge = b - a;
            var outward = counterClockwise ? new Vector3(edge.Z, 0, -edge.X) : new Vector3(-edge.Z, 0, edge.X);
            outward = outward.Normalized();
            var aTop = a + (Vector3.Up * height);
            var bTop = b + (Vector3.Up * height);
            AddTriangle(walls, a, b, bTop, outward);
            AddTriangle(walls, a, bTop, aTop, outward);
        }

        var mesh = new ArrayMesh();
        walls.Commit(mesh);

        var indices = Geometry2D.TriangulatePolygon(corners.Select(c => new Vector2(c.X, c.Z)).ToArray());
        if (indices.Length == 0)
        {
            GD.PushWarning($"PlaceholderMeshes: obstacle '{obstacle.Id}' could not be triangulated, drawn without a top");
        }
        else
        {
            var top = new SurfaceTool();
            top.Begin(Mesh.PrimitiveType.Triangles);
            top.SetMaterial(Material("building_top"));
            for (var i = 0; i + 2 < indices.Length; i += 3)
            {
                AddTriangle(top, corners[indices[i]] + (Vector3.Up * height), corners[indices[i + 1]] + (Vector3.Up * height), corners[indices[i + 2]] + (Vector3.Up * height), Vector3.Up);
            }

            top.Commit(mesh);
        }

        var node = new MeshInstance3D { Name = "Block_" + obstacle.Id, Mesh = mesh };
        if (obstacle.Label is { Length: > 0 } text)
        {
            var center = Vector3.Zero;
            foreach (var corner in corners)
            {
                center += corner;
            }

            center /= corners.Length;
            var label = NewLabel(text, 36, 10);
            label.Position = new Vector3(center.X, height + 0.7f, center.Z);
            node.AddChild(label);
        }

        return node;
    }

    public static Node3D Person(TownPersonView person)
    {
        var marker = GD.Load<PackedScene>(Root + "PersonMarker.tscn").Instantiate<PersonMarker>();
        marker.SetDisplayName(person.Name);
        return marker;
    }

    public static Node3D Spot(TownSpotView spot)
    {
        var marker = GD.Load<PackedScene>(Root + "SpotMarker.tscn").Instantiate<SpotMarker>();
        marker.Build(spot);
        return marker;
    }

    public static Node3D Player() => GD.Load<PackedScene>(Root + "PlayerAvatar.tscn").Instantiate<Node3D>();

    /// <summary>A flat mesh of the area, centered on <paramref name="center"/> (the node's position) and just above the ground. Null when a polygon cannot be triangulated.</summary>
    public static ArrayMesh? AreaMesh(Area area, Vector3 center)
    {
        var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        surface.SetNormal(Vector3.Up);
        var lift = Vector3.Up * SpotLift;

        if (area is Area.Circle circle)
        {
            var radius = (float)circle.Radius;
            for (var i = 0; i < CircleSegments; i++)
            {
                var a0 = Mathf.Tau * i / CircleSegments;
                var a1 = Mathf.Tau * (i + 1) / CircleSegments;
                AddTriangle(
                    surface,
                    lift,
                    lift + new Vector3(Mathf.Cos(a0) * radius, 0, Mathf.Sin(a0) * radius),
                    lift + new Vector3(Mathf.Cos(a1) * radius, 0, Mathf.Sin(a1) * radius),
                    Vector3.Up);
            }

            return surface.Commit();
        }

        var points = ((Area.PolygonArea)area).Polygon.Points;
        var corners = points.Select(p => new Vector3((float)p.X, 0, (float)p.Z) - center).ToArray();
        var indices = Geometry2D.TriangulatePolygon(corners.Select(c => new Vector2(c.X, c.Z)).ToArray());
        if (indices.Length == 0)
        {
            GD.PushWarning("PlaceholderMeshes: a spot area could not be triangulated, not drawn");
            return null;
        }

        for (var i = 0; i + 2 < indices.Length; i += 3)
        {
            AddTriangle(surface, corners[indices[i]] + lift, corners[indices[i + 1]] + lift, corners[indices[i + 2]] + lift, Vector3.Up);
        }

        return surface.Commit();
    }

    private static Label3D NewLabel(string text, int fontSize, int outline) => new()
    {
        Text = text,
        Font = _labelFont ??= new SystemFont { FontNames = ["Segoe UI", "Noto Sans", "Helvetica Neue", "sans-serif"], FontWeight = 600 },
        FontSize = fontSize,
        OutlineSize = outline,
        Modulate = new Color(0.9098f, 0.8627f, 0.7843f),
        OutlineModulate = new Color(0.1020f, 0.0784f, 0.0627f),
        PixelSize = 0.0007f,
        Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
        FixedSize = true,
        NoDepthTest = true,
        AlphaCut = Label3D.AlphaCutMode.Disabled,
        CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
    };

    private static float SignedArea(Vector3[] corners)
    {
        var sum = 0f;
        for (var i = 0; i < corners.Length; i++)
        {
            var a = corners[i];
            var b = corners[(i + 1) % corners.Length];
            sum += (a.X * b.Z) - (b.X * a.Z);
        }

        return sum;
    }

    /// <summary>Adds a triangle wound so its front face (clockwise in Godot) looks along the given normal.</summary>
    private static void AddTriangle(SurfaceTool surface, Vector3 a, Vector3 b, Vector3 c, Vector3 normal)
    {
        if ((b - a).Cross(c - a).Dot(normal) > 0)
        {
            (b, c) = (c, b);
        }

        surface.SetNormal(normal);
        surface.AddVertex(a);
        surface.SetNormal(normal);
        surface.AddVertex(b);
        surface.SetNormal(normal);
        surface.AddVertex(c);
    }
}
