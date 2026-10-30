using Godot;

namespace AncientOrder.Stage;

/// <summary>The placeholder player: a capsule in a color no one else uses, with a flat arrow on the ground that turns toward the direction of travel.</summary>
public partial class PlayerAvatar : Node3D
{
    private const float TurnSpeed = 12f;
    private const float MovedAtLeast = 0.0005f;

    private Node3D? _facing;
    private Vector3 _last;
    private bool _placed;
    private float _yaw;

    public override void _Process(double delta)
    {
        _facing ??= GetNode<Node3D>("Facing");
        var here = GlobalPosition;
        if (_placed)
        {
            var step = new Vector2(here.X - _last.X, here.Z - _last.Z);
            if (step.Length() > MovedAtLeast)
            {
                _yaw = Mathf.LerpAngle(_yaw, Mathf.Atan2(-step.X, -step.Y), 1f - Mathf.Exp(-TurnSpeed * (float)delta));
                _facing.Rotation = new Vector3(0, _yaw, 0);
            }
        }

        _last = here;
        _placed = true;
    }
}
