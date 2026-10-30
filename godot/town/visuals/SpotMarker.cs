using AncientOrder.Rules.Town;
using Godot;

namespace AncientOrder.Stage;

/// <summary>A placeholder spot: a flat translucent area, drawn only while shown, and a marker above it when it has something to open.</summary>
public partial class SpotMarker : Node3D, ITownSpotMarker
{
    private const float SpinSpeed = 1.6f;
    private const float NearScale = 1.35f;

    private MeshInstance3D? _area;
    private MeshInstance3D? _interact;
    private Material? _plain;
    private Material? _highlight;
    private Material? _marker;
    private Material? _markerNear;
    private bool _shown;
    private bool _canOpen;
    private bool _inReach;
    private float _spin;

    private MeshInstance3D Area => _area ??= GetNode<MeshInstance3D>("Area");

    private MeshInstance3D Interact => _interact ??= GetNode<MeshInstance3D>("Interact");

    /// <summary>Builds the area mesh, centered on the spot's center (the stage places this node there).</summary>
    public void Build(TownSpotView spot)
    {
        var center = spot.Area is Rules.Town.Area.Circle circle
            ? new Vector3((float)circle.Center.X, 0, (float)circle.Center.Z)
            : PolygonCenter(((Rules.Town.Area.PolygonArea)spot.Area).Polygon);
        Area.Mesh = PlaceholderMeshes.AreaMesh(spot.Area, center);
        Refresh();
    }

    public void SetShown(bool shown)
    {
        _shown = shown;
        Refresh();
    }

    public void SetCanOpen(bool canOpen)
    {
        _canOpen = canOpen;
        Refresh();
    }

    public void SetInReach(bool inReach)
    {
        _inReach = inReach;
        Refresh();
    }

    public override void _Process(double delta)
    {
        if (Interact.Visible)
        {
            _spin += SpinSpeed * (float)delta;
            Interact.Position = new Vector3(0, 1.4f + (Mathf.Sin(_spin * 2f) * 0.06f), 0);
            Interact.Rotation = new Vector3(0, _spin, 0);
        }
    }

    private static Vector3 PolygonCenter(Polygon polygon)
    {
        var corners = polygon.Points;
        return new Vector3((float)corners.Average(c => c.X), 0, (float)corners.Average(c => c.Z));
    }

    private void Refresh()
    {
        _plain ??= PlaceholderMeshes.Material("spot");
        _highlight ??= PlaceholderMeshes.Material("spot_near");
        _marker ??= PlaceholderMeshes.Material("marker");
        _markerNear ??= PlaceholderMeshes.Material("marker_near");

        var lit = _canOpen && _inReach;
        Area.Visible = _shown && Area.Mesh is not null;
        Area.MaterialOverride = lit ? _highlight : _plain;
        Interact.Visible = _shown && _canOpen;
        Interact.MaterialOverride = _inReach ? _markerNear : _marker;
        Interact.Scale = _inReach ? Vector3.One * NearScale : Vector3.One;
    }
}
