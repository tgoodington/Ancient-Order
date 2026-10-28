using System.Collections.Immutable;
using AncientOrder.Rules.Common;
using AncientOrder.Rules.Party;
using AncientOrder.Rules.Personality;
using AncientOrder.Rules.State;

namespace AncientOrder.Rules.Session;

/// <summary>
/// The one entry point to the game: holds the one active game and returns every result as a success or a refusal.
/// The library's only mutable object. Not thread-safe; call it from one thread (Godot's main thread).
/// Every operation is a function (context, snapshot, input) to Outcome; Run commits it and notifies.
/// Re-entrancy: a listener may call operations. A nested change commits at once (its call returns its result and
/// Snapshot is already updated) but its notification is queued and delivered after the current pass finishes, in
/// commit order, so every listener sees versions in increasing order, each change once. A listener disposed during
/// a pass is not called again in that pass. If one outermost call leads to more than MaxDeliveries deliveries,
/// the rest are dropped and an InvalidOperationException goes to OnListenerError.
/// </summary>
public sealed partial class GameSession
{
    private readonly SessionContext _context;
    private readonly Action<Exception>? _onListenerError;
    private readonly Saves.SaveStore _saves;
    private const int MaxDeliveries = 1000;
    private readonly List<Registration> _registrations = [];
    private readonly Queue<SessionSnapshot> _queued = new();
    private bool _notifying;
    private SessionSnapshot _snapshot = SessionSnapshot.Initial;

    public GameSession(GameContent content, GameSessionOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(content);
        options ??= new GameSessionOptions();
        _context = new SessionContext(content, options.TimeProvider, options.IdSource, options.SeedSource);
        _onListenerError = options.OnListenerError;
        _saves = options.Saves ?? new Saves.SaveStore(new Saves.MemorySaveBackend(), content);
    }

    public SessionSnapshot Snapshot => _snapshot;

    public IDisposable Subscribe(Action<SessionSnapshot> listener)
    {
        ArgumentNullException.ThrowIfNull(listener);
        var registration = new Registration(listener);
        _registrations.Add(registration);
        return new Subscription(this, registration);
    }

    public Result<GameState> NewGame() => Run(GameOperations.NewGame);

    public Result<GameState> GetState() => Run(GameOperations.GetState);

    public Result<PlayerCharacter> GetPlayer() => Run(PlayerOperations.GetPlayer);

    public Result<AncientOrder.Rules.Personality.Personality> GetPersonality() => Run(PlayerOperations.GetPersonality);

    public Result<GameState> AdjustPersonality(PersonalityAdjustment? adjustment) =>
        Run((c, s) => PlayerOperations.AdjustPersonality(c, s, adjustment));

    /// <summary>Sets the fight lineup; succeeds with the new lineup ids.</summary>
    public Result<ImmutableArray<string>> SetLineup(ImmutableArray<string?> ids) =>
        Run((c, s) => PlayerOperations.SetLineup(c, s, ids));

    public Result<ImmutableArray<CharacterView>> ListCharacters() => Run(CharacterOperations.ListCharacters);

    public Result<CharacterView> GetCharacter(string? id) => Run((c, s) => CharacterOperations.GetCharacter(c, s, id));

    public Result<ImmutableArray<SynergyResult>> GetSynergy() => Run(CharacterOperations.GetSynergy);

    internal Result<T> Run<T>(Func<SessionContext, SessionSnapshot, Outcome<T>> operation)
    {
        var started = _snapshot;
        SessionSnapshot committed;
        Result<T> result;
        try
        {
            var outcome = operation(_context, started);
            result = outcome.Result;
            if (ReferenceEquals(outcome.Next, started))
            {
                return result;
            }

            committed = outcome.Next with
            {
                Version = started.Version + 1,
                Synergy = outcome.Next.Game is null
                    ? ImmutableArray<SynergyResult>.Empty
                    : Party.Synergy.Current(_context.Content, outcome.Next.Game),
            };
        }
        catch (Exception)
        {
            return Result<T>.Fail(new RuleError(ErrorCode.INTERNAL_ERROR, "An unexpected error occurred"));
        }

        _snapshot = committed;
        Notify(committed);
        return result;
    }

    private void Notify(SessionSnapshot snapshot)
    {
        _queued.Enqueue(snapshot);
        if (_notifying)
        {
            return;
        }

        _notifying = true;
        try
        {
            var delivered = 0;
            while (_queued.TryDequeue(out var next))
            {
                if (++delivered > MaxDeliveries)
                {
                    _queued.Clear();
                    ReportListenerError(new InvalidOperationException("Listeners kept changing the game; further notifications were dropped."));
                    break;
                }

                Deliver(next);
            }
        }
        finally
        {
            _notifying = false;
        }
    }

    private void Deliver(SessionSnapshot snapshot)
    {
        foreach (var registration in _registrations.ToArray())
        {
            if (!registration.Live)
            {
                continue;
            }

            try
            {
                registration.Listener(snapshot);
            }
            catch (Exception error)
            {
                ReportListenerError(error);
            }
        }
    }

    private void ReportListenerError(Exception error)
    {
        try
        {
            _onListenerError?.Invoke(error);
        }
        catch (Exception)
        {
            // A failing error hook must not undo the change or stop the other listeners.
        }
    }

    private sealed class Registration(Action<SessionSnapshot> listener)
    {
        public Action<SessionSnapshot> Listener { get; } = listener;

        public bool Live { get; set; } = true;
    }

    private sealed class Subscription(GameSession session, Registration registration) : IDisposable
    {
        public void Dispose()
        {
            registration.Live = false;
            session._registrations.Remove(registration);
        }
    }
}
