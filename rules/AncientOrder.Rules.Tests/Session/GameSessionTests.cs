using System.Collections.Immutable;
using AncientOrder.Rules.Combat;
using AncientOrder.Rules.Common;
using AncientOrder.Rules.Personality;
using AncientOrder.Rules.Session;
using AncientOrder.Rules.Tests.Fixtures;
using AncientOrder.Rules.Tests.Support;
using Microsoft.Extensions.Time.Testing;

namespace AncientOrder.Rules.Tests.Session;

// Carried over from src/session/gameSession.test.ts, plus version, synergy-in-snapshot, id and clock, and listener cases.
// Every TypeScript test named *Narrative/dialogue is retired; the combat setup now needs a lineup before StartEncounter.
public class CreateGameSessionSmoke
{
    [Fact(DisplayName = "refuses with no game, then starts and notifies once")]
    public void Refuses_then_starts_and_notifies_once()
    {
        var session = TestSessions.Create();
        var before = session.Snapshot;
        var calls = 0;
        using var subscription = session.Subscribe(_ => calls++);
        SessionAssert.Refused(session.GetState(), ErrorCode.GAME_NOT_FOUND);
        Assert.Same(before, session.Snapshot);
        Assert.Equal(0, calls);
        Assert.True(session.NewGame().IsOk);
        Assert.Equal(1, calls);
        Assert.NotSame(before, session.Snapshot);
    }

    [Fact(DisplayName = "rejects an empty personality adjustment without changing the snapshot")]
    public void Rejects_an_empty_adjustment()
    {
        var session = TestSessions.WithGame();
        var snap = session.Snapshot;
        var r = session.AdjustPersonality(new PersonalityAdjustment(ImmutableDictionary<Trait, double>.Empty));
        SessionAssert.Refused(r, ErrorCode.VALIDATION_ERROR);
        Assert.Same(snap, session.Snapshot);
    }
}

public class SnapshotImmutability
{
    [Fact(DisplayName = "leaves earlier snapshots untouched by later operations")]
    public void Earlier_snapshots_are_untouched()
    {
        var session = TestSessions.WithLineup();
        var first = session.Snapshot;
        var firstGame = first.Game;
        var patience = firstGame!.Player.Personality.Patience;

        session.AdjustPersonality(Adj.Of((Trait.Patience, 5)));
        session.StartEncounter(SessionEncounters.Test);
        session.DeclareOrders([TestSessions.Attack(TestCast.ElenaId, "enemy_brute")]);
        session.AdvanceRound();

        Assert.Same(firstGame, first.Game);
        Assert.Equal(patience, first.Game!.Player.Personality.Patience);
        Assert.Null(first.Game.Combat);
        Assert.Empty(first.PendingOrders);
        Assert.NotSame(first, session.Snapshot);
    }

    [Fact(DisplayName = "does not mutate the pending orders array when new orders replace it")]
    public void Pending_orders_are_not_mutated()
    {
        var session = TestSessions.InCombat();
        session.DeclareOrders([TestSessions.Attack(TestCast.ElenaId, "enemy_brute")]);
        var before = session.Snapshot.PendingOrders;
        session.DeclareOrders([TestSessions.Attack(TestCast.LarsId, "enemy_archer")]);
        Assert.Single(before);
        Assert.Equal(TestCast.ElenaId, before[0].CombatantId);
    }
}

public class Subscribe
{
    [Fact(DisplayName = "notifies once per state change")]
    public void Notifies_once_per_change()
    {
        var session = TestSessions.Create();
        var calls = 0;
        using var subscription = session.Subscribe(_ => calls++);
        session.NewGame();
        session.AdjustPersonality(Adj.Of((Trait.Patience, 1)));
        session.SetLineup(TestSessions.Pair);
        Assert.Equal(3, calls);
    }

    [Fact(DisplayName = "never notifies on reads or refusals")]
    public void Never_notifies_on_reads_or_refusals()
    {
        var session = TestSessions.Create();
        var calls = 0;
        using var subscription = session.Subscribe(_ => calls++);
        session.GetState();
        session.GetCharacter(TestCast.ElenaId);
        session.ListCharacters();
        session.AdjustPersonality(Adj.Of((Trait.Patience, 1)));
        session.NewGame();
        calls = 0;

        session.GetState();
        session.GetPlayer();
        session.GetPersonality();
        session.GetSynergy();
        session.GetCombatResult();
        session.AdjustPersonality(new PersonalityAdjustment(ImmutableDictionary<Trait, double>.Empty));
        session.StartEncounter("nope");
        session.SetLineup(["a"]);

        Assert.Equal(0, calls);
    }

    [Fact(DisplayName = "stops notifying after unsubscribe and supports several listeners")]
    public void Unsubscribe_and_several_listeners()
    {
        var session = TestSessions.Create();
        var a = 0;
        var b = 0;
        var offA = session.Subscribe(_ => a++);
        using var offB = session.Subscribe(_ => b++);
        session.NewGame();
        offA.Dispose();
        session.AdjustPersonality(Adj.Of((Trait.Patience, 1)));
        Assert.Equal(1, a);
        Assert.Equal(2, b);
    }

    [Fact(DisplayName = "notifies after the snapshot is updated")]
    public void Notifies_after_the_snapshot_is_updated()
    {
        var session = TestSessions.Create();
        object? seen = null;
        using var subscription = session.Subscribe(_ => seen = session.Snapshot.Game);
        session.NewGame();
        Assert.NotNull(seen);
    }
}

public class NewGame
{
    [Fact(DisplayName = "clears pending orders and the last combat result (approved fix)")]
    public void Clears_orders_and_result()
    {
        var session = QuickFight.Start(win: true);
        Assert.Equal(CombatStatus.Victory, QuickFight.FightToEnd(session));
        session.StartEncounter(QuickFight.WinEncounterId);
        session.DeclareOrders([TestSessions.Attack(TestCast.ElenaId, "enemy_brute")]);
        Assert.Single(session.Snapshot.PendingOrders);

        session.NewGame();

        var snap = session.Snapshot;
        Assert.Empty(snap.PendingOrders);
        Assert.Null(snap.LastCombatOutcome);
        Assert.Null(snap.Game?.Combat);
        SessionAssert.Refused(session.GetCombatResult(), ErrorCode.NO_ACTIVE_COMBAT);
    }

    // Changed: dialogue position and narrative are retired; the whole game is still replaced (lineup, personality, relationships).
    [Fact(DisplayName = "replaces the whole game, including dialogue position and narrative")]
    public void Replaces_the_whole_game()
    {
        var session = TestSessions.WithLineup();
        session.AdjustPersonality(Adj.Of((Trait.Patience, 8)));
        var before = session.Snapshot.Game!;

        session.NewGame();

        var game = session.Snapshot.Game!;
        Assert.NotSame(before, game);
        Assert.Empty(game.Lineup);
        Assert.Null(game.Combat);
        Assert.Equal(TestContent.Create().Settings.PlayerName, game.Player.Name);
        Assert.NotEqual(before.Player.Personality.Patience, game.Player.Personality.Patience);
    }

    [Fact(DisplayName = "the player id is player_ plus the id source's next id")]
    public void Player_id_comes_from_the_id_source()
    {
        var session = TestSessions.Create(ids: new CountingIdSource());
        session.NewGame();
        Assert.Equal("player_1", session.Snapshot.Game!.Player.Id);
        session.NewGame();
        Assert.Equal("player_2", session.Snapshot.Game!.Player.Id);
    }

    [Fact(DisplayName = "the clock supplies the timestamp of each change")]
    public void Clock_supplies_the_timestamp()
    {
        var clock = new FakeTimeProvider(DateTimeOffset.FromUnixTimeMilliseconds(1_000));
        var session = TestSessions.Create(clock: clock);
        session.NewGame();
        Assert.Equal(1_000, session.Snapshot.Game!.Timestamp);
        clock.Advance(TimeSpan.FromMilliseconds(250));
        session.AdjustPersonality(Adj.Of((Trait.Patience, 1)));
        Assert.Equal(1_250, session.Snapshot.Game!.Timestamp);
    }

    [Fact(DisplayName = "keeps the version counting up across games")]
    public void Version_keeps_counting()
    {
        var session = TestSessions.Create();
        session.NewGame();
        session.NewGame();
        Assert.Equal(2, session.Snapshot.Version);
    }
}

// New: the C# session has one snapshot version.
public class SnapshotVersion
{
    [Fact(DisplayName = "starts at version 0 with no game, no orders, no result, and no synergy")]
    public void Starts_empty()
    {
        var snap = TestSessions.Create().Snapshot;
        Assert.Equal(0, snap.Version);
        Assert.Null(snap.Game);
        Assert.Empty(snap.PendingOrders);
        Assert.Null(snap.LastCombatOutcome);
        Assert.Empty(snap.Synergy);
    }

    [Fact(DisplayName = "each change raises the version by one; reads and refusals leave it and the same snapshot instance")]
    public void One_version_per_change()
    {
        var session = TestSessions.Create();
        session.NewGame();
        Assert.Equal(1, session.Snapshot.Version);
        session.AdjustPersonality(Adj.Of((Trait.Patience, 1)));
        Assert.Equal(2, session.Snapshot.Version);
        session.SetLineup(TestSessions.Pair);
        Assert.Equal(3, session.Snapshot.Version);

        var snap = session.Snapshot;
        session.GetState();
        session.GetSynergy();
        session.ListCharacters();
        SessionAssert.Refused(session.StartEncounter("nope"), ErrorCode.ENCOUNTER_NOT_FOUND);
        SessionAssert.Refused(session.SetLineup(["x", "y"]), ErrorCode.TEAM_COMPOSITION_INVALID);
        Assert.Same(snap, session.Snapshot);
        Assert.Equal(3, snap.Version);
    }

    [Fact(DisplayName = "the snapshot carries the synergy of the player and the lineup, and it follows the lineup")]
    public void Synergy_is_in_the_snapshot()
    {
        var session = TestSessions.WithGame();
        Assert.Empty(session.Snapshot.Synergy);

        session.SetLineup(TestSessions.Pair);

        var content = TestSessions.Content();
        var expected = AncientOrder.Rules.Party.Synergy.Current(content, session.Snapshot.Game!);
        Assert.Equal(expected.Select(s => s.Name), session.Snapshot.Synergy.Select(s => s.Name));
        Assert.Equal(expected.Select(s => s.MatchQuality), session.Snapshot.Synergy.Select(s => s.MatchQuality));
        Assert.Equal(session.Snapshot.Synergy.Select(s => s.Name), SessionAssert.Ok(session.GetSynergy()).Select(s => s.Name));
    }

    [Fact(DisplayName = "a new game empties the synergy")]
    public void New_game_empties_synergy()
    {
        var session = TestSessions.WithLineup();
        session.NewGame();
        Assert.Empty(session.Snapshot.Synergy);
    }
}

public class EngineFailure
{
    // Changed: the C# RuleError has no cause; a throwing seed source stands in for the null encounter parties.
    [Fact(DisplayName = "turns an unexpected throw into INTERNAL_ERROR with state unchanged and no notification")]
    public void Throw_becomes_internal_error()
    {
        var session = TestSessions.Create(seeds: new ThrowingSeedSource());
        session.NewGame();
        session.SetLineup(TestSessions.Pair);
        var snap = session.Snapshot;
        var calls = 0;
        using var subscription = session.Subscribe(_ => calls++);

        var error = SessionAssert.Refused(session.StartEncounter(SessionEncounters.Test), ErrorCode.INTERNAL_ERROR);

        Assert.Equal("An unexpected error occurred", error.Message);
        Assert.Same(snap, session.Snapshot);
        Assert.Equal(0, calls);
        Assert.True(session.GetState().IsOk);
    }

    [Fact(DisplayName = "a throwing id source in NewGame is INTERNAL_ERROR too")]
    public void Throwing_id_source()
    {
        var session = TestSessions.Create(ids: new ThrowingIdSource());
        var error = SessionAssert.Refused(session.NewGame(), ErrorCode.INTERNAL_ERROR);
        Assert.Equal("An unexpected error occurred", error.Message);
        Assert.Equal(0, session.Snapshot.Version);
    }

    private sealed class ThrowingIdSource : IIdSource
    {
        public string NextId() => throw new InvalidOperationException("id source failed");
    }
}

// New: listener isolation and delivery rules.
public class Listeners
{
    [Fact(DisplayName = "a throwing listener is isolated, reported, and does not stop the others or undo the change")]
    public void Throwing_listener_is_isolated()
    {
        var errors = new List<Exception>();
        var session = TestSessions.Create(onListenerError: errors.Add);
        var boom = new InvalidOperationException("boom");
        var seen = 0;
        using var bad = session.Subscribe(_ => throw boom);
        using var good = session.Subscribe(_ => seen++);

        var r = session.NewGame();

        Assert.True(r.IsOk);
        Assert.Equal(1, seen);
        Assert.Same(boom, Assert.Single(errors));
        Assert.Equal(1, session.Snapshot.Version);
    }

    [Fact(DisplayName = "a throwing error hook does not break the session")]
    public void Throwing_error_hook()
    {
        var session = TestSessions.Create(onListenerError: _ => throw new InvalidOperationException("hook"));
        var seen = 0;
        using var bad = session.Subscribe(_ => throw new InvalidOperationException("boom"));
        using var good = session.Subscribe(_ => seen++);

        Assert.True(session.NewGame().IsOk);
        Assert.Equal(1, seen);
    }

    [Fact(DisplayName = "a listener that makes a change gets its result at once, and notifications are delivered in commit order")]
    public void Nested_changes_are_queued_in_commit_order()
    {
        var session = TestSessions.Create();
        session.NewGame();
        var first = new List<long>();
        var second = new List<long>();
        var nestedResultOk = false;
        var versionRightAfterNested = 0L;
        var fired = false;
        using var a = session.Subscribe(s =>
        {
            first.Add(s.Version);
            if (!fired)
            {
                fired = true;
                nestedResultOk = session.AdjustPersonality(Adj.Of((Trait.Empathy, 1))).IsOk;
                versionRightAfterNested = session.Snapshot.Version;
            }
        });
        using var b = session.Subscribe(s => second.Add(s.Version));

        session.AdjustPersonality(Adj.Of((Trait.Patience, 1)));

        Assert.True(nestedResultOk);
        Assert.Equal(3, versionRightAfterNested);
        Assert.Equal(new long[] { 2, 3 }, first);
        Assert.Equal(new long[] { 2, 3 }, second);
    }

    [Fact(DisplayName = "a runaway listener loop stops after 1000 deliveries and reports an InvalidOperationException")]
    public void Runaway_loop_stops()
    {
        var errors = new List<Exception>();
        var session = TestSessions.Create(onListenerError: errors.Add);
        session.NewGame();
        var calls = 0;
        using var loop = session.Subscribe(_ =>
        {
            calls++;
            session.AdjustPersonality(Adj.Of((Trait.Patience, 1)));
        });

        session.AdjustPersonality(Adj.Of((Trait.Patience, 1)));

        Assert.Equal(1000, calls);
        Assert.IsType<InvalidOperationException>(Assert.Single(errors));
    }

    [Fact(DisplayName = "a listener disposed during a pass is not called again in that pass")]
    public void Disposed_mid_pass_is_not_called()
    {
        var session = TestSessions.Create();
        var bCalls = 0;
        IDisposable? b = null;
        using var a = session.Subscribe(_ => b!.Dispose());
        b = session.Subscribe(_ => bCalls++);

        session.NewGame();
        session.AdjustPersonality(Adj.Of((Trait.Patience, 1)));

        Assert.Equal(0, bCalls);
    }

    [Fact(DisplayName = "Dispose is idempotent and per registration")]
    public void Dispose_is_idempotent_and_per_registration()
    {
        var session = TestSessions.Create();
        var calls = 0;
        void Listener(SessionSnapshot s) => calls++;
        var first = session.Subscribe(Listener);
        using var second = session.Subscribe(Listener);

        session.NewGame();
        Assert.Equal(2, calls);

        first.Dispose();
        first.Dispose();
        session.AdjustPersonality(Adj.Of((Trait.Patience, 1)));
        Assert.Equal(3, calls);
    }

    [Fact(DisplayName = "subscribing a null listener or building a session on null content is refused")]
    public void Null_arguments_are_refused()
    {
        var session = TestSessions.Create();
        Assert.Throws<ArgumentNullException>(() => session.Subscribe(null!));
        Assert.Throws<ArgumentNullException>(() => new GameSession(null!));
    }
}
