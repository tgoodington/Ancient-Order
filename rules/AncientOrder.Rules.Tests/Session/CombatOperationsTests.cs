using System.Collections.Immutable;
using AncientOrder.Rules.Combat;
using AncientOrder.Rules.Common;
using AncientOrder.Rules.Session;
using AncientOrder.Rules.Tests.Fixtures;
using AncientOrder.Rules.Tests.Support;

namespace AncientOrder.Rules.Tests.Session;

// Carried over from src/session/combat.test.ts and src/session/operations/combat.test.ts.
// The player's side is now the player (Hero) plus the lineup (Elena, Lars), so the TypeScript fighters map as
// player_warrior to Hero, player_scout to Elena, player_mage to Lars; the enemies keep their ids.
// Shape messages say "actions/{i}..." where the TypeScript said "body/actions...".
internal static class CombatSupport
{
    public static readonly CombatAction WarriorAttack = TestSessions.Attack(TestSessions.Hero, "enemy_brute");
    public static readonly CombatAction ScoutAttack = TestSessions.Attack(TestCast.ElenaId, "enemy_archer");

    /// <summary>Ascension level 3 gives the Hero 2 starting energy segments, so no random rounds are needed.</summary>
    public static GameSession Energized(string encounterId = SessionEncounters.Test) =>
        TestSessions.InCombat(
            encounterId,
            TestSessions.Content(TestSessions.HeroProfile with { AscensionLevel = AscensionLevel.Level3 }));

    public static double HeroEnergy(GameSession session) =>
        session.Snapshot.Game!.Combat!.PlayerParty.Single(c => c.Id == TestSessions.Hero).Energy;

    public static CombatAction Special(double? segments) =>
        new(TestSessions.Hero, ActionType.Special, "enemy_brute", segments);

    /// <summary>Declares one order that must be wholly refused; returns the reason and checks the snapshot did not change.</summary>
    public static string? RefusedReason(GameSession session, CombatAction order)
    {
        var snap = session.Snapshot;
        var error = SessionAssert.Refused(session.DeclareOrders([order]), ErrorCode.COMBAT_INVALID_DECLARATION);
        Assert.Same(snap, session.Snapshot);
        return error.Details.FirstOrDefault()?.Message;
    }
}

public class CombatSmoke
{
    [Fact(DisplayName = "startEncounter refuses prototype keys with ENCOUNTER_NOT_FOUND")]
    public void StartEncounter_refuses_prototype_keys()
    {
        var session = TestSessions.WithGame();
        var snap = session.Snapshot;
        foreach (var key in new[] { "constructor", "__proto__", "toString" })
        {
            SessionAssert.Refused(session.StartEncounter(key), ErrorCode.ENCOUNTER_NOT_FOUND);
        }

        Assert.Same(snap, session.Snapshot);
    }

    [Fact(DisplayName = "starts, refuses a second start, and reads the view")]
    public void Starts_refuses_a_second_start_and_reads_the_view()
    {
        var session = TestSessions.Create();
        SessionAssert.Refused(session.GetCombatView(), ErrorCode.GAME_NOT_FOUND);
        session.NewGame();
        session.SetLineup(TestSessions.Pair);
        var started = SessionAssert.Ok(session.StartEncounter(SessionEncounters.Test));
        Assert.Equal(CombatStatus.Active, started.Status);
        SessionAssert.Refused(session.StartEncounter(SessionEncounters.Test), ErrorCode.COMBAT_ALREADY_ACTIVE);
        Assert.True(session.GetCombatView().IsOk);
        Assert.Equal(CombatResultStatus.Active, SessionAssert.Ok(session.GetCombatResult()).Status);
    }

    [Fact(DisplayName = "refuses a fully invalid declaration and keeps previous orders")]
    public void Refuses_a_fully_invalid_declaration()
    {
        var session = TestSessions.InCombat();
        Assert.True(session.DeclareOrders([CombatSupport.WarriorAttack]).IsOk);
        var snap = session.Snapshot;
        var pending = snap.PendingOrders;
        SessionAssert.Refused(session.DeclareOrders([new CombatAction("nobody", ActionType.Attack, null, null)]), ErrorCode.COMBAT_INVALID_DECLARATION);
        Assert.Same(snap, session.Snapshot);
        Assert.Equal(pending, session.Snapshot.PendingOrders);
    }

    // Changed: a null list stands in for the non-array 'x'.
    [Fact(DisplayName = "refuses malformed declarations")]
    public void Refuses_malformed_declarations()
    {
        var session = TestSessions.WithGame();
        SessionAssert.Refused(session.DeclareOrders(null), ErrorCode.VALIDATION_ERROR);
    }
}

public class PlayerOrderRules
{
    [Fact(DisplayName = "rejects an enemy combatant order")]
    public void Rejects_an_enemy_combatant_order()
    {
        var session = CombatSupport.Energized();
        var reason = CombatSupport.RefusedReason(session, TestSessions.Attack("enemy_brute", TestSessions.Hero));
        Assert.Equal("Combatant 'enemy_brute' is not in the player's party", reason);
    }

    [Fact(DisplayName = "rejects negative, zero, fractional and over-energy segments")]
    public void Rejects_bad_segments()
    {
        var session = CombatSupport.Energized();
        var energy = CombatSupport.HeroEnergy(session);
        Assert.Equal(2, energy);
        foreach (var segments in new[] { -1, 0, 1.5, energy + 1 })
        {
            var reason = CombatSupport.RefusedReason(session, CombatSupport.Special(segments));
            Assert.Equal("energySegments must be an integer between 1 and 2", reason);
        }
    }

    [Fact(DisplayName = "rejects segments on an ATTACK")]
    public void Rejects_segments_on_an_attack()
    {
        var session = CombatSupport.Energized();
        var reason = CombatSupport.RefusedReason(session, CombatSupport.WarriorAttack with { EnergySegments = 1 });
        Assert.Equal("energySegments is only allowed for SPECIAL", reason);
    }

    [Fact(DisplayName = "accepts a SPECIAL with in-range segments")]
    public void Accepts_a_special_with_in_range_segments()
    {
        var session = CombatSupport.Energized();
        var energy = CombatSupport.HeroEnergy(session);
        Assert.True(energy >= 1);
        var r = SessionAssert.Ok(session.DeclareOrders([CombatSupport.Special(energy)]));
        Assert.Single(r.Accepted);
    }
}

public class StartEncounter
{
    // Changed: a null id stands in for the non-string 42.
    [Fact(DisplayName = "refuses a non-string id with VALIDATION_ERROR")]
    public void Refuses_a_non_string_id()
    {
        var session = TestSessions.WithGame();
        SessionAssert.Refused(session.StartEncounter(null), ErrorCode.VALIDATION_ERROR);
        SessionAssert.Refused(session.StartEncounter(new string('x', 65)), ErrorCode.VALIDATION_ERROR);
    }

    [Fact(DisplayName = "refuses without a game with GAME_NOT_FOUND")]
    public void Refuses_without_a_game()
    {
        SessionAssert.Refused(TestSessions.Create().StartEncounter(SessionEncounters.Test), ErrorCode.GAME_NOT_FOUND);
    }

    [Fact(DisplayName = "refuses an unknown encounter with ENCOUNTER_NOT_FOUND and leaves state unchanged")]
    public void Refuses_an_unknown_encounter()
    {
        var session = TestSessions.WithGame();
        var snap = session.Snapshot;
        SessionAssert.Refused(session.StartEncounter("nope"), ErrorCode.ENCOUNTER_NOT_FOUND);
        Assert.Same(snap, session.Snapshot);
    }

    [Fact(DisplayName = "refuses a second start with COMBAT_ALREADY_ACTIVE and keeps the running fight")]
    public void Refuses_a_second_start()
    {
        var session = TestSessions.InCombat();
        session.DeclareOrders([CombatSupport.WarriorAttack]);
        var snap = session.Snapshot;
        var calls = 0;
        using var subscription = session.Subscribe(_ => calls++);

        SessionAssert.Refused(session.StartEncounter(SessionEncounters.Demo), ErrorCode.COMBAT_ALREADY_ACTIVE);

        Assert.Same(snap, session.Snapshot);
        Assert.Equal(0, calls);
        var view = SessionAssert.Ok(session.GetCombatView());
        Assert.Contains(view.Fighters, c => c.Id == TestSessions.Hero);
    }

    [Fact(DisplayName = "clears the previous result and orders when a new fight starts")]
    public void Clears_the_previous_result_and_orders()
    {
        var session = QuickFight.Start(win: true);
        Assert.Equal(CombatStatus.Victory, QuickFight.FightToEnd(session));
        Assert.NotNull(session.Snapshot.LastCombatOutcome);

        session.StartEncounter(QuickFight.WinEncounterId);

        Assert.Null(session.Snapshot.LastCombatOutcome);
        Assert.Empty(session.Snapshot.PendingOrders);
        Assert.Equal(CombatResultStatus.Active, SessionAssert.Ok(session.GetCombatResult()).Status);
    }

    [Fact(DisplayName = "refuses without a full lineup with TEAM_COMPOSITION_INVALID and changes nothing")]
    public void Refuses_without_a_full_lineup()
    {
        var session = TestSessions.WithGame();
        var snap = session.Snapshot;
        var error = SessionAssert.Refused(session.StartEncounter(SessionEncounters.Test), ErrorCode.TEAM_COMPOSITION_INVALID);
        Assert.Equal("Choose a full lineup before starting a fight", error.Message);
        Assert.Same(snap, session.Snapshot);
    }

    [Fact(DisplayName = "starts at round 1, active, with the encounter id, and notifies once")]
    public void Starts_and_notifies_once()
    {
        var session = TestSessions.WithLineup();
        var calls = 0;
        using var subscription = session.Subscribe(_ => calls++);

        var started = SessionAssert.Ok(session.StartEncounter(SessionEncounters.Demo));

        Assert.Equal(new StartEncounterResult(SessionEncounters.Demo, 1, CombatStatus.Active), started);
        Assert.Equal(1, calls);
        Assert.NotNull(session.Snapshot.Game!.Combat);
    }
}

public class DeclareActions
{
    [Fact(DisplayName = "refuses without a game, and without a fight")]
    public void Refuses_without_a_game_and_without_a_fight()
    {
        SessionAssert.Refused(TestSessions.Create().DeclareOrders([CombatSupport.WarriorAttack]), ErrorCode.GAME_NOT_FOUND);
        SessionAssert.Refused(TestSessions.WithGame().DeclareOrders([CombatSupport.WarriorAttack]), ErrorCode.NO_ACTIVE_COMBAT);
    }

    [Fact(DisplayName = "stores accepted orders in the session and reports them")]
    public void Stores_accepted_orders()
    {
        var session = TestSessions.InCombat();
        var r = SessionAssert.Ok(session.DeclareOrders([CombatSupport.WarriorAttack, CombatSupport.ScoutAttack]));
        Assert.Equal(2, r.Accepted.Length);
        Assert.Empty(r.Rejected);
        Assert.Equal(2, session.Snapshot.PendingOrders.Length);
    }

    [Fact(DisplayName = "normalizes a missing target to null and omits energySegments when not given")]
    public void Normalizes_a_missing_target()
    {
        var session = TestSessions.InCombat();
        var r = session.DeclareOrders([new CombatAction(TestSessions.Hero, ActionType.Evade, null, null)]);
        Assert.True(r.IsOk);
        var evade = session.Snapshot.PendingOrders[0];
        Assert.Null(evade.TargetId);
        Assert.Null(evade.EnergySegments);
    }

    [Fact(DisplayName = "refuses energySegments on a non-SPECIAL order")]
    public void Refuses_segments_on_a_non_special_order()
    {
        var session = TestSessions.InCombat();
        var r = SessionAssert.Ok(session.DeclareOrders([CombatSupport.WarriorAttack, CombatSupport.ScoutAttack with { EnergySegments = 2 }]));
        Assert.Single(r.Rejected);
        Assert.Equal("energySegments is only allowed for SPECIAL", r.Rejected[0].Error);
        Assert.Equal([CombatSupport.WarriorAttack], session.Snapshot.PendingOrders);
    }

    [Fact(DisplayName = "refuses an order for a combatant outside the player party")]
    public void Refuses_an_order_outside_the_player_party()
    {
        var session = TestSessions.InCombat();
        var enemyOrder = TestSessions.Attack("enemy_brute", TestSessions.Hero);
        var r = SessionAssert.Ok(session.DeclareOrders([CombatSupport.WarriorAttack, enemyOrder]));
        Assert.Single(r.Rejected);
        Assert.Equal("Combatant 'enemy_brute' is not in the player's party", r.Rejected[0].Error);
        Assert.Equal([CombatSupport.WarriorAttack], session.Snapshot.PendingOrders);

        SessionAssert.Refused(session.DeclareOrders([enemyOrder]), ErrorCode.COMBAT_INVALID_DECLARATION);
        Assert.Equal([CombatSupport.WarriorAttack], session.Snapshot.PendingOrders);
    }

    [Fact(DisplayName = "substitutes the fallback ATTACK for a GROUP the allies lack energy for")]
    public void Substitutes_the_fallback_attack()
    {
        var session = TestSessions.InCombat();
        var r = SessionAssert.Ok(session.DeclareOrders([new CombatAction(TestSessions.Hero, ActionType.Group, "enemy_brute", null)]));
        var rejected = Assert.Single(r.Rejected);
        Assert.Equal(ActionType.Group, rejected.Type);
        Assert.False(rejected.Valid);
        Assert.Contains("full energy", rejected.Error, StringComparison.Ordinal);
        Assert.Equal([new DeclarationEntry(TestSessions.Hero, ActionType.Attack, "enemy_brute", true, null)], r.Accepted);
        Assert.Equal([CombatSupport.WarriorAttack], session.Snapshot.PendingOrders);
    }

    [Fact(DisplayName = "accepts the valid part of a mixed declaration and reports the rest as rejected")]
    public void Accepts_the_valid_part_of_a_mixed_declaration()
    {
        var session = TestSessions.InCombat();
        var r = SessionAssert.Ok(session.DeclareOrders([CombatSupport.WarriorAttack, TestSessions.Attack(TestCast.ElenaId, TestCast.LarsId)]));
        Assert.Single(r.Accepted);
        Assert.Single(r.Rejected);
        Assert.Single(session.Snapshot.PendingOrders);
    }

    [Fact(DisplayName = "refuses a fully invalid declaration and keeps the previous orders (approved fix)")]
    public void Refuses_a_fully_invalid_declaration_and_keeps_orders()
    {
        var session = TestSessions.InCombat();
        session.DeclareOrders([CombatSupport.WarriorAttack]);
        var pending = session.Snapshot.PendingOrders;
        var snap = session.Snapshot;
        var calls = 0;
        using var subscription = session.Subscribe(_ => calls++);

        var error = SessionAssert.Refused(session.DeclareOrders([new CombatAction("nobody", ActionType.Attack, null, null)]), ErrorCode.COMBAT_INVALID_DECLARATION);

        Assert.Equal("All declarations were invalid", error.Message);
        var detail = Assert.Single(error.Details);
        Assert.Equal("actions/0", detail.Path);
        Assert.Equal("Combatant 'nobody' is not in the player's party", detail.Message);
        Assert.Same(snap, session.Snapshot);
        Assert.Equal(pending, session.Snapshot.PendingOrders);
        Assert.Equal([CombatSupport.WarriorAttack], pending);
        Assert.Equal(0, calls);
    }

    [Fact(DisplayName = "replaces earlier orders instead of appending")]
    public void Replaces_earlier_orders()
    {
        var session = TestSessions.InCombat();
        session.DeclareOrders([CombatSupport.WarriorAttack]);
        session.DeclareOrders([CombatSupport.ScoutAttack]);
        Assert.Equal([CombatSupport.ScoutAttack], session.Snapshot.PendingOrders);
    }

    [Fact(DisplayName = "clears orders when given an empty array")]
    public void Clears_orders_on_an_empty_array()
    {
        var session = TestSessions.InCombat();
        session.DeclareOrders([CombatSupport.WarriorAttack]);
        Assert.True(session.DeclareOrders([]).IsOk);
        Assert.Empty(session.Snapshot.PendingOrders);
    }

    // Changed: a null combatant id stands in for the missing 'type'.
    [Fact(DisplayName = "refuses malformed input with VALIDATION_ERROR and keeps orders")]
    public void Refuses_malformed_input()
    {
        var session = TestSessions.InCombat();
        session.DeclareOrders([CombatSupport.WarriorAttack]);
        var snap = session.Snapshot;
        var error = SessionAssert.Refused(session.DeclareOrders([new CombatAction(null!, ActionType.Attack, null, null)]), ErrorCode.VALIDATION_ERROR);
        Assert.Equal("actions/0/combatantId must be string", error.Message);
        Assert.Same(snap, session.Snapshot);
    }

    // New: input shape rules.
    [Fact(DisplayName = "refuses a null list, a null element, more than 10 orders, and ids over 64 characters")]
    public void Refuses_bad_shapes()
    {
        var session = TestSessions.InCombat();
        var snap = session.Snapshot;
        SessionAssert.Refused(session.DeclareOrders(null), ErrorCode.VALIDATION_ERROR);
        SessionAssert.Refused(session.DeclareOrders([null]), ErrorCode.VALIDATION_ERROR);
        SessionAssert.Refused(session.DeclareOrders([.. Enumerable.Repeat<CombatAction?>(CombatSupport.WarriorAttack, 11)]), ErrorCode.VALIDATION_ERROR);
        SessionAssert.Refused(session.DeclareOrders([TestSessions.Attack(new string('x', 65), "enemy_brute")]), ErrorCode.VALIDATION_ERROR);
        SessionAssert.Refused(session.DeclareOrders([TestSessions.Attack(TestSessions.Hero, new string('x', 65))]), ErrorCode.VALIDATION_ERROR);
        Assert.Same(snap, session.Snapshot);
    }

    [Fact(DisplayName = "checks the shape before the game, so a null list is VALIDATION_ERROR even without a game")]
    public void Shape_is_checked_first()
    {
        SessionAssert.Refused(TestSessions.Create().DeclareOrders(null), ErrorCode.VALIDATION_ERROR);
    }

    [Fact(DisplayName = "one order per fighter: a second order from the same fighter is rejected")]
    public void One_order_per_fighter()
    {
        var session = TestSessions.InCombat();
        var r = SessionAssert.Ok(session.DeclareOrders([CombatSupport.WarriorAttack, TestSessions.Attack(TestSessions.Hero, "enemy_archer")]));
        Assert.Single(r.Accepted);
        var rejected = Assert.Single(r.Rejected);
        Assert.Equal($"Combatant '{TestSessions.Hero}' already has an order this round", rejected.Error);
        Assert.Equal([CombatSupport.WarriorAttack], session.Snapshot.PendingOrders);
    }

    [Fact(DisplayName = "a list the caller changes afterwards does not change the stored orders")]
    public void Caller_list_is_copied()
    {
        var session = TestSessions.InCombat();
        var list = new List<CombatAction?> { CombatSupport.WarriorAttack };
        session.DeclareOrders(list);
        list.Add(CombatSupport.ScoutAttack);
        list[0] = CombatSupport.ScoutAttack;
        Assert.Equal([CombatSupport.WarriorAttack], session.Snapshot.PendingOrders);
    }

    // The SPECIAL energySegments describe of the TypeScript, nested.
    public class SpecialEnergySegments
    {
        [Fact(DisplayName = "keeps energySegments when it is an integer within the combatant energy")]
        public void Keeps_segments_within_energy()
        {
            var session = CombatSupport.Energized();
            var r = SessionAssert.Ok(session.DeclareOrders([CombatSupport.Special(2)]));
            Assert.Single(r.Accepted);
            Assert.Equal([CombatSupport.Special(2)], session.Snapshot.PendingOrders);
        }

        [Fact(DisplayName = "accepts an omitted energySegments (spend all) without adding the key")]
        public void Accepts_omitted_segments()
        {
            var session = CombatSupport.Energized();
            Assert.True(session.DeclareOrders([CombatSupport.Special(null)]).IsOk);
            Assert.Null(session.Snapshot.PendingOrders[0].EnergySegments);
        }

        public static IEnumerable<TheoryDataRow<double>> BadSegments() =>
            new[] { 0.0, 3.0, -1.0, 1.5 }.Select(n => new TheoryDataRow<double>(n)
            {
                TestDisplayName = $"refuses energySegments {n.ToString(System.Globalization.CultureInfo.InvariantCulture)} and keeps previous orders",
            });

        [Theory]
        [MemberData(nameof(BadSegments))]
        public void Refuses_bad_segments_and_keeps_previous_orders(double n)
        {
            var session = CombatSupport.Energized();
            session.DeclareOrders([CombatSupport.Special(1)]);
            var snap = session.Snapshot;
            SessionAssert.Refused(session.DeclareOrders([CombatSupport.Special(n)]), ErrorCode.COMBAT_INVALID_DECLARATION);
            Assert.Same(snap, session.Snapshot);
            Assert.Equal([CombatSupport.Special(1)], session.Snapshot.PendingOrders);
        }
    }
}

public class AdvanceRound
{
    [Fact(DisplayName = "refuses without a game, and without a fight")]
    public void Refuses_without_a_game_and_without_a_fight()
    {
        SessionAssert.Refused(TestSessions.Create().AdvanceRound(), ErrorCode.GAME_NOT_FOUND);
        SessionAssert.Refused(TestSessions.WithGame().AdvanceRound(), ErrorCode.NO_ACTIVE_COMBAT);
    }

    [Fact(DisplayName = "plays the round, clears the orders and grows the history")]
    public void Plays_the_round()
    {
        var session = TestSessions.InCombat();
        session.DeclareOrders([CombatSupport.WarriorAttack]);

        var view = SessionAssert.Ok(session.AdvanceRound());

        Assert.Equal(2, view.Round);
        Assert.Equal(6, view.Fighters.Length);
        Assert.Empty(session.Snapshot.PendingOrders);
        Assert.Single(SessionAssert.Ok(session.GetRoundHistory()));
    }

    [Fact(DisplayName = "runs a round with no orders (AI only)")]
    public void Runs_a_round_with_no_orders()
    {
        var session = TestSessions.InCombat();
        Assert.Equal(2, SessionAssert.Ok(session.AdvanceRound()).Round);
    }

    [Fact(DisplayName = "notifies once per round")]
    public void Notifies_once_per_round()
    {
        var session = TestSessions.InCombat();
        var calls = 0;
        using var subscription = session.Subscribe(_ => calls++);
        session.AdvanceRound();
        Assert.Equal(1, calls);
    }

    [Fact(DisplayName = "the same seed and orders play the same round")]
    public void Same_seed_same_round()
    {
        var one = TestSessions.InCombat();
        var two = TestSessions.InCombat();
        one.DeclareOrders([CombatSupport.WarriorAttack, CombatSupport.ScoutAttack]);
        two.DeclareOrders([CombatSupport.WarriorAttack, CombatSupport.ScoutAttack]);
        one.AdvanceRound();
        two.AdvanceRound();
        StateAssert.Equivalent(one.Snapshot.Game, two.Snapshot.Game);
    }

    [Fact(DisplayName = "each fight takes its seed from the seed source")]
    public void Each_fight_takes_a_seed()
    {
        var session = TestSessions.WithLineup(seeds: new FixedSeedSource(5));
        session.StartEncounter(SessionEncounters.Test);
        Assert.Equal(DiceState.FromSeed(5), session.Snapshot.Game!.Combat!.Dice);
    }
}

public class CombatViewReads
{
    [Fact(DisplayName = "refuses without a game, and without a fight")]
    public void Refuses_without_a_game_and_without_a_fight()
    {
        SessionAssert.Refused(TestSessions.Create().GetCombatView(), ErrorCode.GAME_NOT_FOUND);

        var session = TestSessions.WithGame();
        Assert.False(session.GetCombatView().IsOk);
        SessionAssert.Refused(session.GetRoundHistory(), ErrorCode.NO_ACTIVE_COMBAT);
    }

    [Fact(DisplayName = "shows the player targets but hides what enemies have decided")]
    public void Shows_player_targets_only()
    {
        var session = TestSessions.InCombat();
        session.DeclareOrders([CombatSupport.WarriorAttack]);

        var view = SessionAssert.Ok(session.GetCombatView());

        var byId = view.Fighters.ToDictionary(c => c.Id);
        Assert.Equal("enemy_brute", byId[TestSessions.Hero].Targeting);
        Assert.Null(byId[TestCast.ElenaId].Targeting);
        foreach (var id in new[] { "enemy_brute", "enemy_archer", "enemy_shaman" })
        {
            Assert.Null(byId[id].Targeting);
        }

        Assert.Equal([CombatSupport.WarriorAttack], view.PendingOrders);
    }

    [Fact(DisplayName = "reading the view or history does not change or notify the session")]
    public void Reading_does_not_change_or_notify()
    {
        var session = TestSessions.InCombat();
        var snap = session.Snapshot;
        var calls = 0;
        using var subscription = session.Subscribe(_ => calls++);
        session.GetCombatView();
        session.GetRoundHistory();
        session.GetCombatResult();
        Assert.Same(snap, session.Snapshot);
        Assert.Equal(0, calls);
    }

    [Fact(DisplayName = "lists the player side first, all at full stamina and active, at round 1")]
    public void Lists_the_player_side_first()
    {
        var view = SessionAssert.Ok(TestSessions.InCombat().GetCombatView());

        Assert.Equal(
            [TestSessions.Hero, TestCast.ElenaId, TestCast.LarsId, "enemy_brute", "enemy_archer", "enemy_shaman"],
            view.Fighters.Select(f => f.Id));
        Assert.All(view.Fighters, f =>
        {
            Assert.Equal(1.0, f.StaminaPct);
            Assert.Equal(FighterStance.Active, f.Stance);
        });
        Assert.Equal(1, view.Round);
        Assert.Equal(CombatStatus.Active, view.Status);
    }
}

public class CombatResult
{
    [Fact(DisplayName = "refuses without a game, and when no fight ever happened")]
    public void Refuses_without_a_game_and_when_no_fight_happened()
    {
        SessionAssert.Refused(TestSessions.Create().GetCombatResult(), ErrorCode.GAME_NOT_FOUND);
        SessionAssert.Refused(TestSessions.WithGame().GetCombatResult(), ErrorCode.NO_ACTIVE_COMBAT);
    }

    [Fact(DisplayName = "is active, with the round, while the fight runs")]
    public void Is_active_while_the_fight_runs()
    {
        var r = SessionAssert.Ok(TestSessions.InCombat().GetCombatResult());
        Assert.Equal(new CombatResultView(CombatResultStatus.Active, 1), r);
    }

    [Fact(DisplayName = "stays readable after a victory (approved fix)")]
    public void Stays_readable_after_a_victory()
    {
        var session = QuickFight.Start(win: true);
        Assert.Equal(CombatStatus.Victory, QuickFight.FightToEnd(session));

        var r = SessionAssert.Ok(session.GetCombatResult());

        Assert.Equal(CombatResultStatus.Victory, r.Status);
        Assert.True(r.Round >= 1);
        Assert.Equal(r, SessionAssert.Ok(session.GetCombatResult()));
    }

    [Fact(DisplayName = "stays readable after a defeat (approved fix)")]
    public void Stays_readable_after_a_defeat()
    {
        var session = QuickFight.Start(win: false);
        Assert.Equal(CombatStatus.Defeat, QuickFight.FightToEnd(session));

        Assert.Equal(CombatResultStatus.Defeat, SessionAssert.Ok(session.GetCombatResult()).Status);
    }

    [Fact(DisplayName = "ends the fight: view and history refuse, combatState clears, team unlocks")]
    public void Ends_the_fight()
    {
        var session = QuickFight.Start(win: true);
        QuickFight.FightToEnd(session);

        Assert.Null(session.Snapshot.Game?.Combat);
        SessionAssert.Refused(session.GetCombatView(), ErrorCode.NO_ACTIVE_COMBAT);
        SessionAssert.Refused(session.GetRoundHistory(), ErrorCode.NO_ACTIVE_COMBAT);
        SessionAssert.Refused(session.AdvanceRound(), ErrorCode.NO_ACTIVE_COMBAT);
        Assert.True(session.SetLineup([TestCast.ElenaId, TestCast.KadeId]).IsOk);
    }

    [Fact(DisplayName = "is cleared by a new game (approved fix)")]
    public void Is_cleared_by_a_new_game()
    {
        var session = QuickFight.Start(win: true);
        QuickFight.FightToEnd(session);

        session.NewGame();

        SessionAssert.Refused(session.GetCombatResult(), ErrorCode.NO_ACTIVE_COMBAT);
        Assert.Null(session.Snapshot.LastCombatOutcome);
    }

    [Fact(DisplayName = "a finished fight records the outcome and round in the snapshot")]
    public void Records_the_outcome()
    {
        var session = QuickFight.Start(win: false);
        QuickFight.FightToEnd(session);
        var outcome = session.Snapshot.LastCombatOutcome!;
        Assert.Equal(CombatStatus.Defeat, outcome.Status);
        Assert.Equal(SessionAssert.Ok(session.GetCombatResult()).Round, outcome.Round);
        Assert.Empty(session.Snapshot.PendingOrders);
    }
}
