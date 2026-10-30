using AncientOrder.Rules.Reactions;
using Godot;

namespace AncientOrder.Stage;

/// <summary>
/// A placeholder person: a capsule with a nose that shows which way they face, a name tag, and a marker when they have something to open.
/// Body language stands in for animation (designer decision 1A): the resting posture says how they carry themselves toward the player
/// (Warm turns toward and leans in, Neutral stands as placed, Wary turns partly away, Cold turns away and hunches), and a tell leans them
/// briefly toward (warmer) or back (cooler), more for a stronger reaction. No numbers are ever shown.
/// </summary>
public partial class PersonMarker : Node3D, ITownPersonMarker
{
    private const float PostureSpeed = 7f;
    private const float SpinSpeed = 1.6f;
    private const float NearScale = 1.35f;
    private const float ShortestDistance = 0.05f;

    private Label3D? _tag;
    private Node3D? _lean;
    private Node3D? _posture;
    private MeshInstance3D? _interact;
    private MeshInstance3D? _ring;
    private Material? _marker;
    private Material? _markerNear;
    private bool _canOpen;
    private bool _inReach;
    private bool _hasPlayer;
    private bool _settled;
    private Vector3 _player;
    private float _yaw;
    private float _tilt;
    private float _height = 1f;
    private float _tellLean;
    private float _spin;
    private Tween? _tween;

    public Demeanor Demeanor { get; private set; } = Demeanor.Neutral;

    /// <summary>The last tell played, or null before any. Read by screen tests.</summary>
    public (TellDirection Direction, TellStrength Strength)? LastTell { get; private set; }

    private Label3D Tag => _tag ??= GetNode<Label3D>("Tag");

    private Node3D Lean => _lean ??= GetNode<Node3D>("Lean");

    private Node3D Posture => _posture ??= GetNode<Node3D>("Lean/Posture");

    private MeshInstance3D Interact => _interact ??= GetNode<MeshInstance3D>("Interact");

    private MeshInstance3D Ring => _ring ??= GetNode<MeshInstance3D>("ReachRing");

    public void SetDisplayName(string name) => Tag.Text = name;

    public void SetDemeanor(Demeanor demeanor) => Demeanor = demeanor;

    public void SetCanOpen(bool canOpen)
    {
        _canOpen = canOpen;
        ShowInteraction();
    }

    public void SetInReach(bool inReach)
    {
        _inReach = inReach;
        ShowInteraction();
    }

    public void PlayerStands(Vector3 position)
    {
        _player = position;
        _hasPlayer = true;
    }

    public void PlayTell(TellDirection direction, TellStrength strength)
    {
        LastTell = (direction, strength);
        if (direction == TellDirection.Neutral || !IsInsideTree())
        {
            return;
        }

        var (degrees, seconds) = strength switch
        {
            TellStrength.Slight => (3f, 0.5f),
            TellStrength.Clear => (8f, 0.7f),
            _ => (15f, 0.9f),
        };

        // A lean toward the player tips forward (negative about the tell frame's X); pulling back tips the other way and a little less.
        var target = direction == TellDirection.Warmer ? -degrees : degrees * 0.8f;
        _tween?.Kill();
        _tween = CreateTween();
        _tween.TweenMethod(Callable.From<float>(SetTellLean), _tellLean, target, seconds * 0.3).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
        _tween.TweenInterval(seconds * 0.15);
        _tween.TweenMethod(Callable.From<float>(SetTellLean), target, 0f, seconds * 0.55).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
    }

    public override void _Process(double delta)
    {
        var dt = (float)delta;
        var (rootYaw, playerYaw) = Headings();
        var (yawTarget, tiltTarget, heightTarget) = RestingPosture(Demeanor, Wrap(rootYaw - playerYaw));

        if (!_settled)
        {
            _yaw = yawTarget;
            _tilt = tiltTarget;
            _height = heightTarget;
            _settled = true;
        }
        else
        {
            var weight = 1f - Mathf.Exp(-PostureSpeed * dt);
            _yaw = Mathf.LerpAngle(_yaw, yawTarget, weight);
            _tilt = Mathf.Lerp(_tilt, tiltTarget, weight);
            _height = Mathf.Lerp(_height, heightTarget, weight);
        }

        Lean.Rotation = new Vector3(Mathf.DegToRad(_tellLean), Wrap(playerYaw - rootYaw), 0);
        Posture.Rotation = new Vector3(Mathf.DegToRad(_tilt), _yaw, 0);
        Posture.Scale = new Vector3(1f, _height, 1f);

        if (Interact.Visible)
        {
            _spin += SpinSpeed * dt;
            var bob = Mathf.Sin(_spin * 2f) * 0.06f;
            Interact.Position = new Vector3(0, 2.95f + bob, 0);
            Interact.Rotation = new Vector3(0, _spin, 0);
        }
    }

    /// <summary>The resting posture for a demeanor: yaw relative to the direction of the player (radians), forward tilt in degrees (negative leans toward the facing), and height scale.</summary>
    private static (float Yaw, float Tilt, float Height) RestingPosture(Demeanor demeanor, float placedRelativeYaw) => demeanor switch
    {
        Demeanor.Warm => (0f, -6f, 1f),
        Demeanor.Wary => (Mathf.DegToRad(70f), 3f, 1f),
        Demeanor.Cold => (Mathf.DegToRad(150f), -16f, 0.88f),
        _ => (placedRelativeYaw, 0f, 1f),
    };

    private (float RootYaw, float PlayerYaw) Headings()
    {
        var z = GlobalBasis.Z;
        var rootYaw = Mathf.Atan2(z.X, z.Z);
        if (!_hasPlayer)
        {
            return (rootYaw, rootYaw);
        }

        var toPlayer = _player - GlobalPosition;
        if (new Vector2(toPlayer.X, toPlayer.Z).Length() < ShortestDistance)
        {
            return (rootYaw, rootYaw);
        }

        return (rootYaw, Mathf.Atan2(-toPlayer.X, -toPlayer.Z));
    }

    private static float Wrap(float angle) => angle - (Mathf.Tau * Mathf.Round(angle / Mathf.Tau));

    private void SetTellLean(float degrees) => _tellLean = degrees;

    private void ShowInteraction()
    {
        Interact.Visible = _canOpen;
        Ring.Visible = _canOpen && _inReach;
        _marker ??= PlaceholderMeshes.Material("marker");
        _markerNear ??= PlaceholderMeshes.Material("marker_near");
        Interact.MaterialOverride = _inReach ? _markerNear : _marker;
        Interact.Scale = _inReach ? Vector3.One * NearScale : Vector3.One;
    }
}
