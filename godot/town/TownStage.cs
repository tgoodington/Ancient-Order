using AncientOrder.Rules.Common;
using AncientOrder.Rules.Reactions;
using AncientOrder.Rules.Session;
using AncientOrder.Rules.Town;
using Godot;

namespace AncientOrder.Stage;

/// <summary>What a person's marker (the look, T15) is told by the stage. A marker is the node a PersonBuilder returns, if it implements this.</summary>
public interface ITownPersonMarker
{
    void SetDemeanor(Demeanor demeanor);

    void SetCanOpen(bool canOpen);

    /// <summary>True while the player is within reach of this person (the stage refreshes it as the avatar moves).</summary>
    void SetInReach(bool inReach);

    /// <summary>Called whenever the stage reports a position and when the avatar is placed, so posture can face toward or away from the player.</summary>
    void PlayerStands(Vector3 position);

    void PlayTell(TellDirection direction, TellStrength strength);
}

/// <summary>What a spot's marker is told by the stage. A marker is the node a SpotBuilder returns, if it implements this.</summary>
public interface ITownSpotMarker
{
    void SetShown(bool shown);

    void SetCanOpen(bool canOpen);

    /// <summary>True while the player is within reach of this spot (the stage refreshes it as the avatar moves).</summary>
    void SetInReach(bool inReach);
}

/// <summary>
/// The 3D town, built entirely from SessionSnapshot.Town at run time. The owner (screen flow) calls Init once, then Apply with each new
/// TownView; the stage must be in the scene tree first. The player walks by click (WalkTo, WalkToPerson, WalkToSpot) along navigation paths
/// and reports positions to the session: on arrival, on entering or leaving a walk-over spot, and before a trigger.
/// A refused report snaps the avatar back to the last accepted position and shows the refusal. Looks come from the builder hooks (T15).
/// </summary>
public partial class TownStage : Node3D
{
    private const float PersonGlideSpeed = 3f;
    private const float SubStep = 0.25f;
    private const double ArrivalFraction = 0.8;
    private const double SamePosition = 1e-6;
    private const double MarkerRefresh = 0.15;

    private readonly Dictionary<string, PersonEntry> _people = [];
    private readonly Dictionary<string, SpotEntry> _spots = [];
    private readonly HashSet<string> _inside = [];

    private GameSession _session = null!;
    private Action<RuleError> _showError = _ => { };
    private TownView? _view;
    private TownView? _arrivalView;
    private string? _townId;
    private Node3D _ground = null!;
    private Node3D _obstacles = null!;
    private Node3D _peopleRoot = null!;
    private Node3D _spotsRoot = null!;
    private Node3D _player = null!;
    private TownCamera _camera = null!;
    private TownNavigation? _navigation;
    private Point _reported = new(0, 0);
    private bool _paused;
    private double _markerClock;
    private Vector3[]? _path;
    private int _pathIndex;
    private string? _targetPerson;
    private string? _targetSpot;

    [Export]
    public float WalkSpeed { get; set; } = 4.5f;

    /// <summary>Hooks for T15's meshes and markers. Each returns a node to add under the stage's matching container, or null for none.</summary>
    public Func<TownView, Node3D?>? GroundBuilder { get; set; }

    public Func<ObstacleView, Node3D?>? ObstacleBuilder { get; set; }

    public Func<TownPersonView, Node3D?>? PersonBuilder { get; set; }

    public Func<TownSpotView, Node3D?>? SpotBuilder { get; set; }

    public Func<Node3D?>? PlayerBuilder { get; set; }

    public bool IsNavigationReady => _navigation?.IsReady ?? false;

    public bool IsWalking => _path is not null && _pathIndex < _path.Length;

    public Vector3 PlayerPosition => _player.GlobalPosition;

    public override void _Ready()
    {
        _ground = GetNode<Node3D>("Ground");
        _obstacles = GetNode<Node3D>("Obstacles");
        _peopleRoot = GetNode<Node3D>("People");
        _spotsRoot = GetNode<Node3D>("Spots");
        _player = GetNode<Node3D>("Player");
        _camera = GetNode<TownCamera>("TownCamera");
        _navigation = new TownNavigation(GetNode<NavigationRegion3D>("NavigationRegion3D"));
        _camera.Target = _player;
        PlaceholderMeshes.Install(this);
    }

    public void Init(GameSession session, Action<RuleError> showError)
    {
        _session = session;
        _showError = showError;
    }

    /// <summary>Shows the view. A new town id rebuilds everything; otherwise people and spots are diffed and a changed player position places the avatar.</summary>
    public void Apply(TownView view)
    {
        _view = view;
        _arrivalView = view with { Reach = view.Reach * ArrivalFraction };

        if (BlockedBySession())
        {
            StopWalking();
        }

        if (view.Id != _townId)
        {
            Rebuild(view);
            return;
        }

        DiffPeople(view);
        DiffSpots(view);
        if (Math.Abs(view.Player.X - _reported.X) > SamePosition || Math.Abs(view.Player.Z - _reported.Z) > SamePosition)
        {
            PlaceAvatar(view.Player);
        }

        if (IsWalking && _targetPerson is not null)
        {
            RepathToTarget();
        }
    }

    /// <summary>Stops walking and holds clicks. Reports the current position first, but only while a town is active with no conversation, fight, or hand-off.</summary>
    public void Pause()
    {
        if (!_paused && _view is not null && !BlockedBySession() && _session.Snapshot.Town is not null)
        {
            Report();
        }

        _paused = true;
        StopWalking();
    }

    public bool IsPaused => _paused;

    public void Resume() => _paused = false;

    public bool WalkTo(Vector3 point)
    {
        if (!CanAct())
        {
            return false;
        }

        ClearTarget();
        return StartPath(_navigation!.ClosestPoint(point));
    }

    public bool WalkToPerson(string characterId)
    {
        if (!CanAct() || !_people.TryGetValue(characterId, out var entry))
        {
            return false;
        }

        ClearTarget();
        _targetPerson = characterId;
        return StartPath(_navigation!.ClosestPoint(entry.Goal));
    }

    /// <summary>Walks into reach of an interact spot and triggers it. A walk-over spot is only walked to (its area's center), since the session fires those by position.</summary>
    public bool WalkToSpot(string spotId)
    {
        if (!CanAct() || !_spots.TryGetValue(spotId, out var entry))
        {
            return false;
        }

        ClearTarget();
        if (entry.View.Trigger == SpotTrigger.Interact)
        {
            _targetSpot = spotId;
        }

        return StartPath(_navigation!.ClosestPoint(Center(entry.View.Area)));
    }

    /// <summary>Plays a reaction on a person's marker. Does nothing when that person is not present or has no marker.</summary>
    public void PlayTell(string characterId, TellDirection direction, TellStrength strength)
    {
        if (_people.TryGetValue(characterId, out var entry))
        {
            entry.Marker?.PlayTell(direction, strength);
        }
    }

    /// <summary>Casts a ray through a screen point and walks to what it hits: a person or interact spot first, else the ground.</summary>
    public bool Pick(Vector2 screenPosition)
    {
        var camera = GetViewport().GetCamera3D();
        if (camera is null || !CanAct())
        {
            return false;
        }

        var origin = camera.ProjectRayOrigin(screenPosition);
        var end = origin + (camera.ProjectRayNormal(screenPosition) * 200f);
        var space = GetWorld3D().DirectSpaceState;

        var hit = space.IntersectRay(PhysicsRayQueryParameters3D.Create(origin, end, PickLayers.People | PickLayers.Spots));
        if (hit.Count > 0 && hit["collider"].AsGodotObject() is Node body)
        {
            if (body.HasMeta("person"))
            {
                return WalkToPerson(body.GetMeta("person").AsString());
            }

            if (body.HasMeta("spot"))
            {
                return WalkToSpot(body.GetMeta("spot").AsString());
            }
        }

        hit = space.IntersectRay(PhysicsRayQueryParameters3D.Create(origin, end, PickLayers.Ground));
        return hit.Count > 0 && WalkTo(hit["position"].AsVector3());
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } click && Pick(click.Position))
        {
            GetViewport().SetInputAsHandled();
        }
    }

    public override void _PhysicsProcess(double delta) => Tick(delta);

    public override void _Process(double delta)
    {
        foreach (var entry in _people.Values)
        {
            if (entry.Node.Position != entry.Goal)
            {
                entry.Node.Position = entry.Node.Position.MoveToward(entry.Goal, PersonGlideSpeed * (float)delta);
            }
        }
    }

    /// <summary>Advances the walk by delta seconds. Tests call this to drive time.</summary>
    public void Tick(double delta)
    {
        if (!IsWalking || !CanAct())
        {
            return;
        }

        _markerClock += delta;
        if (_markerClock >= MarkerRefresh)
        {
            _markerClock = 0;
            TellMarkers();
        }

        var remaining = WalkSpeed * (float)delta;
        while (remaining > 0 && IsWalking)
        {
            var position = _player.GlobalPosition;
            var next = _path![_pathIndex];
            var toNext = new Vector3(next.X - position.X, 0, next.Z - position.Z);
            var distance = toNext.Length();
            var move = Mathf.Min(Mathf.Min(remaining, SubStep), distance);
            if (move > 0)
            {
                _player.GlobalPosition = position + (toNext / distance * move);
            }

            remaining -= Mathf.Max(move, 0.0001f);
            if (distance - move <= 0.0001f)
            {
                _pathIndex++;
            }

            if (!CheckWalkOver())
            {
                return;
            }

            if (ReachedTarget())
            {
                Arrive();
                return;
            }
        }

        if (!IsWalking)
        {
            Arrive();
        }
    }

    private bool BlockedBySession()
    {
        var snapshot = _session.Snapshot;
        return snapshot.Conversation is not null || snapshot.Game?.Combat is not null || snapshot.Game?.HandOff is not null;
    }

    private bool CanAct() => _view is not null && _navigation is { IsReady: true } && !_paused && !BlockedBySession();

    private bool StartPath(Vector3 target)
    {
        var path = _navigation!.GetPath(_player.GlobalPosition, target);
        if (path.Length == 0)
        {
            ClearTarget();
            return false;
        }

        _path = path;
        _pathIndex = path.Length > 1 ? 1 : 0;
        return true;
    }

    private void RepathToTarget()
    {
        if (_targetPerson is not null && _people.TryGetValue(_targetPerson, out var entry))
        {
            StartPath(_navigation!.ClosestPoint(entry.Goal));
        }
    }

    private void StopWalking()
    {
        _path = null;
        _pathIndex = 0;
        ClearTarget();
    }

    private void ClearTarget()
    {
        _targetPerson = null;
        _targetSpot = null;
    }

    private bool ReachedTarget()
    {
        var id = _targetPerson ?? _targetSpot;
        var p = _player.GlobalPosition;
        return id is not null && _arrivalView!.InReach(id, p.X, p.Z);
    }

    private void Arrive()
    {
        var person = _targetPerson;
        var spot = _targetSpot;
        StopWalking();

        var result = Report();
        if (result is not TownEvent.Nothing || (person is null && spot is null))
        {
            return;
        }

        var id = person ?? spot!;
        var p = _player.GlobalPosition;
        if (!_view!.InReach(id, p.X, p.Z))
        {
            return;
        }

        var trigger = person is not null ? _session.TriggerPerson(id) : _session.TriggerSpot(id);
        if (!trigger.IsOk)
        {
            _showError(trigger.Error);
        }
    }

    /// <summary>Walk-over tracking: reports when the avatar enters or leaves a walk-over spot's area. Returns false if walking must stop.</summary>
    private bool CheckWalkOver()
    {
        var view = _view!;
        var p = _player.GlobalPosition;
        var changed = false;
        foreach (var spot in view.Spots)
        {
            if (spot.Trigger != SpotTrigger.WalkOver)
            {
                continue;
            }

            var inside = view.IsInside(spot.Id, p.X, p.Z);
            if (inside ? _inside.Add(spot.Id) : _inside.Remove(spot.Id))
            {
                changed = true;
            }
        }

        if (!changed)
        {
            return true;
        }

        var result = Report();
        if (result is null or not TownEvent.Nothing)
        {
            StopWalking();
            return false;
        }

        return true;
    }

    /// <summary>Reports the avatar's position to the session. Null when refused (the avatar is snapped back and the refusal shown).</summary>
    private TownEvent? Report()
    {
        var p = _player.GlobalPosition;
        var previous = _reported;
        _reported = new Point(p.X, p.Z);

        var result = _session.MovePlayer(p.X, p.Z);
        if (!result.IsOk)
        {
            StopWalking();
            PlaceAvatar(previous, false);
            _showError(result.Error);
            return null;
        }

        TellMarkers();
        return result.Value;
    }

    private void PlaceAvatar(Point at, bool snapCamera = true)
    {
        _reported = at;
        StopWalking();
        _player.GlobalPosition = new Vector3((float)at.X, 0f, (float)at.Z);
        SyncInside();
        if (snapCamera)
        {
            _camera.SnapToTarget();
        }

        TellMarkers();
    }

    private void SyncInside()
    {
        _inside.Clear();
        var p = _player.GlobalPosition;
        foreach (var spot in _view!.Spots)
        {
            if (spot.Trigger == SpotTrigger.WalkOver && _view.IsInside(spot.Id, p.X, p.Z))
            {
                _inside.Add(spot.Id);
            }
        }
    }

    private void TellMarkers()
    {
        foreach (var entry in _people.Values)
        {
            entry.Marker?.PlayerStands(_player.GlobalPosition);
            entry.Marker?.SetInReach(InReach(entry.View.CharacterId));
        }

        foreach (var entry in _spots.Values)
        {
            entry.Marker?.SetInReach(InReach(entry.View.Id));
        }
    }

    private bool InReach(string id)
    {
        var p = _player.GlobalPosition;
        return _view is not null && _view.InReach(id, p.X, p.Z);
    }

    private void Rebuild(TownView view)
    {
        StopWalking();
        _townId = view.Id;
        Clear(_ground);
        Clear(_obstacles);
        Clear(_peopleRoot);
        Clear(_spotsRoot);
        Clear(_player);
        _people.Clear();
        _spots.Clear();

        if (PlayerBuilder?.Invoke() is { } avatar)
        {
            _player.AddChild(avatar);
        }

        BuildGround(view);
        foreach (var obstacle in view.Obstacles)
        {
            if (ObstacleBuilder?.Invoke(obstacle) is { } node)
            {
                _obstacles.AddChild(node);
            }
        }

        _navigation!.Bake(view.Ground, view.Obstacles);
        DiffPeople(view);
        DiffSpots(view);
        PlaceAvatar(view.Player);
    }

    private void BuildGround(TownView view)
    {
        if (GroundBuilder?.Invoke(view) is { } look)
        {
            _ground.AddChild(look);
        }

        var faces = TownNavigation.Triangles(view.Ground);
        if (faces.Length == 0)
        {
            return;
        }

        var body = new StaticBody3D { Name = "GroundBody", CollisionLayer = PickLayers.Ground, CollisionMask = 0 };
        body.AddChild(new CollisionShape3D { Shape = new ConcavePolygonShape3D { Data = faces, BackfaceCollision = true } });
        _ground.AddChild(body);
    }

    private void DiffPeople(TownView view)
    {
        foreach (var id in _people.Keys.Where(id => view.People.All(p => p.CharacterId != id)).ToList())
        {
            Discard(_people[id].Node);
            _people.Remove(id);
        }

        foreach (var person in view.People)
        {
            var goal = new Vector3((float)person.Position.X, 0f, (float)person.Position.Z);
            if (!_people.TryGetValue(person.CharacterId, out var entry))
            {
                entry = AddPerson(person, goal);
            }

            entry.Goal = goal;
            entry.Node.RotationDegrees = new Vector3(0, -(float)person.Facing, 0);
            if (entry.View.Demeanor != person.Demeanor)
            {
                entry.Marker?.SetDemeanor(person.Demeanor);
            }

            if (entry.View.CanOpen != person.CanOpen)
            {
                entry.Marker?.SetCanOpen(person.CanOpen);
            }

            entry.View = person;
        }
    }

    private PersonEntry AddPerson(TownPersonView person, Vector3 at)
    {
        var node = new Node3D { Name = "Person_" + person.CharacterId, Position = at };
        var look = PersonBuilder?.Invoke(person);
        if (look is not null)
        {
            node.AddChild(look);
        }

        var body = new StaticBody3D { CollisionLayer = PickLayers.People, CollisionMask = 0 };
        body.SetMeta("person", person.CharacterId);
        body.AddChild(new CollisionShape3D { Shape = new CylinderShape3D { Radius = 0.5f, Height = 1.8f }, Position = new Vector3(0, 0.9f, 0) });
        node.AddChild(body);
        _peopleRoot.AddChild(node);

        var marker = look as ITownPersonMarker;
        marker?.SetDemeanor(person.Demeanor);
        marker?.SetCanOpen(person.CanOpen);
        marker?.PlayerStands(_player.GlobalPosition);
        marker?.SetInReach(InReach(person.CharacterId));
        var entry = new PersonEntry(person, node, marker);
        _people[person.CharacterId] = entry;
        return entry;
    }

    private void DiffSpots(TownView view)
    {
        foreach (var id in _spots.Keys.Where(id => view.Spots.All(s => s.Id != id)).ToList())
        {
            Discard(_spots[id].Node);
            _spots.Remove(id);
        }

        foreach (var spot in view.Spots)
        {
            if (_spots.TryGetValue(spot.Id, out var entry) && (entry.View.Area != spot.Area || entry.View.Trigger != spot.Trigger))
            {
                Discard(entry.Node);
                _spots.Remove(spot.Id);
                entry = null;
            }

            if (entry is null)
            {
                _spots[spot.Id] = AddSpot(spot);
                continue;
            }

            if (entry.View.Shown != spot.Shown)
            {
                entry.Marker?.SetShown(spot.Shown);
            }

            if (entry.View.CanOpen != spot.CanOpen)
            {
                entry.Marker?.SetCanOpen(spot.CanOpen);
            }

            entry.View = spot;
        }
    }

    private SpotEntry AddSpot(TownSpotView spot)
    {
        var center = Center(spot.Area);
        var node = new Node3D { Name = "Spot_" + spot.Id, Position = center };
        var look = SpotBuilder?.Invoke(spot);
        if (look is not null)
        {
            node.AddChild(look);
        }

        if (spot.Trigger == SpotTrigger.Interact)
        {
            var body = new StaticBody3D { CollisionLayer = PickLayers.Spots, CollisionMask = 0 };
            body.SetMeta("spot", spot.Id);
            body.AddChild(new CollisionShape3D { Shape = PickShape(spot.Area, center) });
            node.AddChild(body);
        }

        _spotsRoot.AddChild(node);
        var marker = look as ITownSpotMarker;
        marker?.SetShown(spot.Shown);
        marker?.SetCanOpen(spot.CanOpen);
        marker?.SetInReach(InReach(spot.Id));
        return new SpotEntry(spot, node, marker);
    }

    private static Shape3D PickShape(Area area, Vector3 center)
    {
        if (area is Area.Circle circle)
        {
            return new CylinderShape3D { Radius = (float)circle.Radius, Height = 1.5f };
        }

        var points = new List<Vector3>();
        foreach (var corner in ((Area.PolygonArea)area).Polygon.Points)
        {
            var local = new Vector3((float)corner.X, 0f, (float)corner.Z) - center;
            points.Add(local);
            points.Add(local + new Vector3(0, 1.5f, 0));
        }

        return new ConvexPolygonShape3D { Points = [.. points] };
    }

    private static Vector3 Center(Area area)
    {
        if (area is Area.Circle circle)
        {
            return new Vector3((float)circle.Center.X, 0f, (float)circle.Center.Z);
        }

        var corners = ((Area.PolygonArea)area).Polygon.Points;
        return new Vector3((float)corners.Average(c => c.X), 0f, (float)corners.Average(c => c.Z));
    }

    private static void Clear(Node parent)
    {
        foreach (var child in parent.GetChildren())
        {
            Discard(child);
        }
    }

    private static void Discard(Node node)
    {
        node.GetParent()?.RemoveChild(node);
        node.QueueFree();
    }

    private sealed class PersonEntry(TownPersonView view, Node3D node, ITownPersonMarker? marker)
    {
        public TownPersonView View { get; set; } = view;

        public Node3D Node { get; } = node;

        public ITownPersonMarker? Marker { get; } = marker;

        public Vector3 Goal { get; set; } = node.Position;
    }

    private sealed class SpotEntry(TownSpotView view, Node3D node, ITownSpotMarker? marker)
    {
        public TownSpotView View { get; set; } = view;

        public Node3D Node { get; } = node;

        public ITownSpotMarker? Marker { get; } = marker;
    }
}
