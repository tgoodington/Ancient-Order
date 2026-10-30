using Godot;

namespace AncientOrder.Stage;

/// <summary>
/// A perspective camera at a fixed yaw and pitch that follows a target smoothly. The mouse wheel (actions camera_zoom_in and
/// camera_zoom_out) changes only the distance, between MinDistance and MaxDistance. Angles are in degrees; the field of view is the camera's own Fov property (set in the scene).
/// </summary>
public partial class TownCamera : Camera3D
{
    [Export]
    public float Yaw { get; set; } = 45f;

    [Export]
    public float Pitch { get; set; } = 55f;

    [Export]
    public float Distance { get; set; } = 18f;

    [Export]
    public float MinDistance { get; set; } = 8f;

    [Export]
    public float MaxDistance { get; set; } = 32f;

    [Export]
    public float ZoomStep { get; set; } = 1.5f;

    [Export]
    public float FollowSpeed { get; set; } = 6f;

    public Node3D? Target { get; set; }

    private Vector3 _focus;

    public override void _Ready()
    {
        SnapToTarget();
    }

    public override void _Process(double delta)
    {
        if (Target is not null)
        {
            var weight = 1f - Mathf.Exp(-FollowSpeed * (float)delta);
            _focus = _focus.Lerp(Target.GlobalPosition, weight);
        }

        Place();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionPressed("camera_zoom_in"))
        {
            Zoom(-1);
            GetViewport().SetInputAsHandled();
        }
        else if (@event.IsActionPressed("camera_zoom_out"))
        {
            Zoom(1);
            GetViewport().SetInputAsHandled();
        }
    }

    /// <summary>Moves the distance by steps (positive is farther), kept within the limits.</summary>
    public void Zoom(float steps) => Distance = Mathf.Clamp(Distance + (steps * ZoomStep), MinDistance, MaxDistance);

    /// <summary>Jumps to the target with no smoothing (after the player is placed).</summary>
    public void SnapToTarget()
    {
        if (Target is not null)
        {
            _focus = Target.GlobalPosition;
        }

        Place();
    }

    private void Place()
    {
        var yaw = Mathf.DegToRad(Yaw);
        var pitch = Mathf.DegToRad(Pitch);
        var offset = new Vector3(Mathf.Cos(pitch) * Mathf.Sin(yaw), Mathf.Sin(pitch), Mathf.Cos(pitch) * Mathf.Cos(yaw)) * Distance;
        LookAtFromPosition(_focus + offset, _focus, Vector3.Up);
    }
}
