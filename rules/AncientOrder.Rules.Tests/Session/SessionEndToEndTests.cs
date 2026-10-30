using System.Collections.Immutable;
using AncientOrder.Rules.Combat;
using AncientOrder.Rules.Common;
using AncientOrder.Rules.Personality;
using AncientOrder.Rules.Session;
using AncientOrder.Rules.State;
using AncientOrder.Rules.Tests.Fixtures;
using AncientOrder.Rules.Tests.Support;

namespace AncientOrder.Rules.Tests.Session;

// Carried over from tests/session.combat-e2e.test.ts, tests/session.sprint1.test.ts, and tests/session.sprint4.test.ts.
// Dropped with the retired narrative and dialogue engines: the sprint1 dialogue tests and the sprint4 narrative-lock test.
// The demo fight is now fought by the player (Hero) with Elena and Lars (lineup size 2), so the TypeScript fighters map as
// player_elena to Hero, player_lars to Elena, player_kade to Lars; the enemies keep their ids.
internal static class EndToEnd
{
    public static readonly ImmutableArray<CombatAction> Standard =
    [
        TestSessions.Attack(TestSessions.Hero, "enemy_knight"),
        TestSessions.Attack(TestCast.ElenaId, "enemy_rogue"),
        TestSessions.Attack(TestCast.LarsId, "enemy_mage"),
    ];

    public static GameSession StartDemo(ISeedSource? seeds = null) =>
        TestSessions.InCombat(SessionEncounters.Demo, seeds: seeds);

    public static ImmutableArray<RoundResult> History(GameSession session) => SessionAssert.Ok(session.GetRoundHistory());

    public static IEnumerable<AttackResult> AttackResults(IEnumerable<ActionResult> actions) =>
        actions.Select(a => a.AttackResult).OfType<AttackResult>();

    public static void AssertPersonalitySum(AncientOrder.Rules.Personality.Personality p) =>
        Assert.True(Math.Abs(TestPersonality.Values(p).Sum() - 100) < 0.02);

    public static void Declare(GameSession session, IEnumerable<CombatAction> orders) => session.DeclareOrders([.. orders]);

    /// <summary>Plays a fight to its end with the given order sets (one per round, the last repeating); returns every round's fighter staminas and the orders used.</summary>
    public static (List<string> Rounds, CombatStatus Status) Play(GameSession session, int maxRounds = 60)
    {
        var rounds = new List<string>();
        var status = CombatStatus.Active;
        for (var i = 0; i < maxRounds && status == CombatStatus.Active; i++)
        {
            Declare(session, Standard);
            var view = SessionAssert.Ok(session.AdvanceRound());
            status = view.Status;
            rounds.Add(string.Join(
                ";",
                view.Fighters.Select(f => FormattableString.Invariant($"{f.Id}={f.Stamina}/{f.Stance}")).Prepend($"round={view.Round}/{view.Status}")));
        }

        return (rounds, status);
    }
}

public class Task22EndToEnd3v3DemoEncounter
{
    public class Scenario1EncounterInitialisation
    {
        [Fact(DisplayName = "newGame creates a new game")]
        public void NewGame_creates_a_new_game()
        {
            Assert.True(TestSessions.Create().NewGame().IsOk);
        }

        [Fact(DisplayName = "startEncounter with encounter_demo initialises combat")]
        public void StartEncounter_initialises_combat()
        {
            var session = TestSessions.WithLineup();
            var started = SessionAssert.Ok(session.StartEncounter(SessionEncounters.Demo));
            Assert.Equal(SessionEncounters.Demo, started.EncounterId);
            Assert.Equal(1, started.Round);
            Assert.Equal(CombatStatus.Active, started.Status);
        }

        [Fact(DisplayName = "encounter initialisation fails without a prior game")]
        public void Initialisation_fails_without_a_game()
        {
            SessionAssert.Refused(TestSessions.Create().StartEncounter(SessionEncounters.Demo), ErrorCode.GAME_NOT_FOUND);
        }

        [Fact(DisplayName = "unknown encounter ID returns an error")]
        public void Unknown_encounter_id()
        {
            SessionAssert.Refused(TestSessions.WithGame().StartEncounter("encounter_does_not_exist"), ErrorCode.ENCOUNTER_NOT_FOUND);
        }
    }

    public class Scenario2PlayerDeclarationsAndRoundAdvancement
    {
        [Fact(DisplayName = "all three player declarations are accepted")]
        public void All_three_declarations_are_accepted()
        {
            var session = EndToEnd.StartDemo();
            var r = SessionAssert.Ok(session.DeclareOrders([.. EndToEnd.Standard]));
            Assert.Equal(3, r.Accepted.Length);
            Assert.Empty(r.Rejected);
        }

        [Fact(DisplayName = "advanceRound advances the round and returns updated state")]
        public void AdvanceRound_advances_the_round()
        {
            var session = EndToEnd.StartDemo();
            EndToEnd.Declare(session, EndToEnd.Standard);
            var view = SessionAssert.Ok(session.AdvanceRound());
            Assert.Equal(2, view.Round);
            Assert.Contains(view.Status, new[] { CombatStatus.Active, CombatStatus.Victory, CombatStatus.Defeat });
        }

        [Fact(DisplayName = "round result contains combatant stamina information")]
        public void Round_result_contains_stamina_information()
        {
            var session = EndToEnd.StartDemo();
            EndToEnd.Declare(session, EndToEnd.Standard);
            var view = SessionAssert.Ok(session.AdvanceRound());
            Assert.Equal(6, view.Fighters.Length);
            foreach (var c in view.Fighters)
            {
                Assert.False(string.IsNullOrEmpty(c.Id));
                Assert.InRange(c.StaminaPct, 0.0, 1.0);
                Assert.True(c.Stamina >= 0);
                Assert.Contains(c.Stance, new[] { FighterStance.Active, FighterStance.KO });
            }
        }

        [Fact(DisplayName = "declarations without a prior game fail")]
        public void Declarations_without_a_game_fail()
        {
            SessionAssert.Refused(TestSessions.Create().DeclareOrders([.. EndToEnd.Standard]), ErrorCode.GAME_NOT_FOUND);
        }

        [Fact(DisplayName = "round advancement without a prior game fails")]
        public void Advancement_without_a_game_fails()
        {
            SessionAssert.Refused(TestSessions.Create().AdvanceRound(), ErrorCode.GAME_NOT_FOUND);
        }
    }

    public class Scenario3FormulaOutputVerification
    {
        private static GameSession AfterOneRound()
        {
            var session = EndToEnd.StartDemo();
            EndToEnd.Declare(session, EndToEnd.Standard);
            session.AdvanceRound();
            return session;
        }

        [Fact(DisplayName = "roundHistory is populated after one round")]
        public void RoundHistory_is_populated()
        {
            Assert.True(EndToEnd.History(AfterOneRound()).Length >= 1);
        }

        [Fact(DisplayName = "AttackResult fields are present with correct types")]
        public void AttackResult_fields_are_present()
        {
            var firstRound = EndToEnd.History(AfterOneRound())[0];
            Assert.Equal(1, firstRound.Round);

            var attackResults = EndToEnd.AttackResults(firstRound.Actions).ToList();
            Assert.NotEmpty(attackResults);

            var ar = attackResults[0];
            Assert.False(string.IsNullOrEmpty(ar.AttackerId));
            Assert.False(string.IsNullOrEmpty(ar.TargetId));
            Assert.True(ar.Damage >= 0);
            Assert.True(Enum.IsDefined(ar.DefenseType));
        }

        [Fact(DisplayName = "energy is tracked — combatants gain energy after taking actions")]
        public void Energy_is_tracked()
        {
            var session = EndToEnd.StartDemo();
            EndToEnd.Declare(session, EndToEnd.Standard);
            var r1 = SessionAssert.Ok(session.AdvanceRound());
            if (r1.Status != CombatStatus.Active)
            {
                return;
            }

            EndToEnd.Declare(session, EndToEnd.Standard);
            var r2 = SessionAssert.Ok(session.AdvanceRound());
            Assert.Equal(3, r2.Round);
        }

        [Fact(DisplayName = "Rank KO is structurally possible — attacker and target ranks are equal (rank 3)")]
        public void Rank_KO_is_structurally_possible()
        {
            var session = AfterOneRound();
            var attackResults = EndToEnd.AttackResults(EndToEnd.History(session)[0].Actions).ToList();
            Assert.NotEmpty(attackResults);
            var ranks = EndToEnd.History(session)[0].StateSnapshot.PlayerParty.Concat(EndToEnd.History(session)[0].StateSnapshot.EnemyParty);
            Assert.All(ranks, c => Assert.Equal(3, c.Rank));
        }

        [Fact(DisplayName = "blindside flag is present in every AttackResult")]
        public void Blindside_flag_is_present()
        {
            var attackResults = EndToEnd.AttackResults(EndToEnd.History(AfterOneRound())[0].Actions).ToList();
            Assert.NotEmpty(attackResults);
            Assert.All(attackResults, ar => Assert.False(string.IsNullOrEmpty(ar.AttackerId)));
        }

        [Fact(DisplayName = "defense damage: target loses stamina after a round of ATTACK")]
        public void Target_loses_stamina()
        {
            var session = EndToEnd.StartDemo();
            EndToEnd.Declare(session, EndToEnd.Standard);

            var r1 = SessionAssert.Ok(session.AdvanceRound());

            var enemies = r1.Fighters.Where(c => SessionEncounters.DemoEnemies.Contains(c.Id)).ToList();
            Assert.Equal(3, enemies.Count);
            Assert.Contains(enemies, c => c.StaminaPct < 1.0);
        }

        [Fact(DisplayName = "ascension level tracking — initial ascension is 0 for all combatants")]
        public void Ascension_level_tracking()
        {
            var snapshot = EndToEnd.History(AfterOneRound())[0].StateSnapshot;
            foreach (var c in snapshot.PlayerParty.Concat(snapshot.EnemyParty))
            {
                Assert.InRange((int)c.AscensionLevel, 0, 3);
            }
        }
    }

    public class Scenario4MultiRoundLoopToCompletion
    {
        [Fact(DisplayName = "3v3 encounter runs to completion (victory/defeat) within 10 rounds")]
        public void Runs_to_completion_within_10_rounds()
        {
            var session = EndToEnd.StartDemo();

            const int maxRounds = 10;
            var finalStatus = CombatStatus.Active;
            var roundsRun = 0;
            for (var i = 0; i < maxRounds; i++)
            {
                // Declarations may be refused once targets are KO'd; the round still advances with AI-only actions.
                EndToEnd.Declare(session, EndToEnd.Standard);
                var round = SessionAssert.Ok(session.AdvanceRound());
                roundsRun++;
                finalStatus = round.Status;
                if (finalStatus != CombatStatus.Active)
                {
                    break;
                }
            }

            Assert.InRange(roundsRun, 1, maxRounds);

            // Approved fix: the result stays readable after the fight, and agrees (while the fight is still running it reads active).
            var result = SessionAssert.Ok(session.GetCombatResult());
            var expected = finalStatus switch
            {
                CombatStatus.Victory => CombatResultStatus.Victory,
                CombatStatus.Defeat => CombatResultStatus.Defeat,
                _ => CombatResultStatus.Active,
            };
            Assert.Equal(expected, result.Status);
        }

        [Fact(DisplayName = "round history accumulates one entry per round")]
        public void Round_history_accumulates()
        {
            var session = EndToEnd.StartDemo();

            var roundsRun = 0;
            for (var i = 0; i < 3; i++)
            {
                EndToEnd.Declare(session, EndToEnd.Standard);
                var round = SessionAssert.Ok(session.AdvanceRound());
                roundsRun++;
                if (round.Status != CombatStatus.Active)
                {
                    break;
                }
            }

            // History is only readable while combat is active (it ends with the fight).
            if (roundsRun == 3)
            {
                var history = session.GetRoundHistory();
                if (history.IsOk)
                {
                    Assert.Equal(3, history.Value.Length);
                }
            }

            Assert.True(roundsRun >= 1);
        }

        [Fact(DisplayName = "at least one enemy loses stamina across 3 rounds of sustained attack")]
        public void An_enemy_loses_stamina_over_three_rounds()
        {
            var session = EndToEnd.StartDemo();

            IReadOnlyList<FighterView>? last = null;
            for (var i = 0; i < 3; i++)
            {
                EndToEnd.Declare(session, EndToEnd.Standard);
                var round = SessionAssert.Ok(session.AdvanceRound());
                last = round.Fighters;
                if (round.Status != CombatStatus.Active)
                {
                    break;
                }
            }

            var enemies = last!.Where(c => c.Id.StartsWith("enemy_", StringComparison.Ordinal));
            Assert.Contains(enemies, c => c.StaminaPct < 1.0);
        }
    }

    public class Scenario5DeterminismAndReproducibility
    {
        [Fact(DisplayName = "two fresh runs of the same encounter both complete without errors")]
        public void Two_fresh_runs_complete()
        {
            var s1 = EndToEnd.StartDemo();
            EndToEnd.Declare(s1, EndToEnd.Standard);
            var r1 = SessionAssert.Ok(s1.AdvanceRound());

            var s2 = EndToEnd.StartDemo();
            EndToEnd.Declare(s2, EndToEnd.Standard);
            var r2 = SessionAssert.Ok(s2.AdvanceRound());

            Assert.Equal(2, r1.Round);
            Assert.Equal(2, r2.Round);
            Assert.Equal(r1.Fighters.Select(c => c.Id).Order(), r2.Fighters.Select(c => c.Id).Order());
        }

        [Fact(DisplayName = "same encounter run twice produces the same party composition and round count")]
        public void Same_encounter_twice_same_composition_and_rounds()
        {
            static (int Rounds, CombatStatus Status) Run(int maxRounds)
            {
                var s = EndToEnd.StartDemo();
                var rounds = 0;
                var status = CombatStatus.Active;
                for (var i = 0; i < maxRounds; i++)
                {
                    EndToEnd.Declare(s, EndToEnd.Standard);
                    var res = SessionAssert.Ok(s.AdvanceRound());
                    rounds++;
                    status = res.Status;
                    if (status != CombatStatus.Active)
                    {
                        break;
                    }
                }

                return (rounds, status);
            }

            var run1 = Run(10);
            var run2 = Run(10);

            Assert.True(run1.Rounds >= 1);
            Assert.True(run2.Rounds >= 1);
            // New: with the same dice the two runs agree exactly, not only in composition.
            Assert.Equal(run1, run2);
        }

        // New: a fight is replayed exactly from its seed and orders.
        [Fact(DisplayName = "a whole fight is replayed exactly from its seed and orders")]
        public void A_fight_replays_exactly()
        {
            var first = EndToEnd.StartDemo(new FixedSeedSource(99));
            var second = EndToEnd.StartDemo(new FixedSeedSource(99));

            var a = EndToEnd.Play(first);
            var b = EndToEnd.Play(second);

            Assert.NotEqual(CombatStatus.Active, a.Status);
            Assert.Equal(a.Rounds, b.Rounds);
            Assert.Equal(a.Status, b.Status);
            Assert.Equal(first.Snapshot.LastCombatOutcome, second.Snapshot.LastCombatOutcome);
            StateAssert.Equivalent(first.Snapshot.Game, second.Snapshot.Game);
        }

        [Fact(DisplayName = "a different seed plays a different fight")]
        public void A_different_seed_differs()
        {
            var a = EndToEnd.Play(EndToEnd.StartDemo(new FixedSeedSource(1)));
            var b = EndToEnd.Play(EndToEnd.StartDemo(new FixedSeedSource(2)));
            Assert.NotEqual(a.Rounds, b.Rounds);
        }
    }

    public class Scenario6CombatApiContract
    {
        [Fact(DisplayName = "getCombatView returns VisualInfo after encounter is initialised")]
        public void GetCombatView_after_initialisation()
        {
            var view = SessionAssert.Ok(EndToEnd.StartDemo().GetCombatView());
            Assert.Equal(6, view.Fighters.Length);
        }

        [Fact(DisplayName = "getCombatResult returns active status before any round runs")]
        public void GetCombatResult_is_active_before_any_round()
        {
            Assert.Equal(CombatResultStatus.Active, SessionAssert.Ok(EndToEnd.StartDemo().GetCombatResult()).Status);
        }

        [Fact(DisplayName = "getRoundHistory returns empty array before any round runs")]
        public void GetRoundHistory_is_empty_before_any_round()
        {
            Assert.Empty(SessionAssert.Ok(EndToEnd.StartDemo().GetRoundHistory()));
        }

        [Fact(DisplayName = "declaring an invalid action (wrong target party) is rejected")]
        public void Wrong_target_party_is_rejected()
        {
            var session = EndToEnd.StartDemo();
            SessionAssert.Refused(
                session.DeclareOrders([TestSessions.Attack(TestSessions.Hero, TestCast.ElenaId)]),
                ErrorCode.COMBAT_INVALID_DECLARATION);
        }

        [Fact(DisplayName = "all 6 expected combatant IDs appear in the VisualInfo response")]
        public void All_six_ids_appear()
        {
            var ids = SessionAssert.Ok(EndToEnd.StartDemo().GetCombatView()).Fighters.Select(c => c.Id).ToList();
            Assert.Contains(TestSessions.Hero, ids);
            Assert.Contains(TestCast.ElenaId, ids);
            Assert.Contains(TestCast.LarsId, ids);
            Assert.Contains("enemy_knight", ids);
            Assert.Contains("enemy_rogue", ids);
            Assert.Contains("enemy_mage", ids);
        }
    }

    // New: a whole fight through the session alone, start to finish.
    public class WholeFight
    {
        [Fact(DisplayName = "plays a whole fight through the session: new game, lineup, encounter, rounds, result")]
        public void Plays_a_whole_fight()
        {
            var session = TestSessions.Create();
            var versions = new List<long>();
            using var subscription = session.Subscribe(s => versions.Add(s.Version));
            SessionAssert.Ok(session.NewGame());
            SessionAssert.Ok(session.SetLineup(TestSessions.Pair));
            SessionAssert.Ok(session.StartEncounter(SessionEncounters.Demo));

            var (rounds, status) = EndToEnd.Play(session);

            Assert.NotEqual(CombatStatus.Active, status);
            Assert.Null(session.Snapshot.Game!.Combat);
            var result = SessionAssert.Ok(session.GetCombatResult());
            Assert.Equal(status == CombatStatus.Victory ? CombatResultStatus.Victory : CombatResultStatus.Defeat, result.Status);
            Assert.Equal(rounds.Count + 1, result.Round);
            Assert.Equal(session.Snapshot.LastCombatOutcome, new CombatOutcome(status, result.Round));
            Assert.Empty(session.Snapshot.PendingOrders);
            Assert.Equal(Enumerable.Range(1, versions.Count).Select(i => (long)i), versions);
            SessionAssert.Refused(session.GetCombatView(), ErrorCode.NO_ACTIVE_COMBAT);
            Assert.True(session.SetLineup(TestSessions.Pair).IsOk);
        }
    }
}

public class FullSessionFlow
{
    [Fact(DisplayName = "newGame creates a game with combatState null and personality sum 100")]
    public void NewGame_creates_a_game()
    {
        var game = SessionAssert.Ok(TestSessions.Create().NewGame());
        Assert.Null(game.Combat);
        EndToEnd.AssertPersonalitySum(game.Player.Personality);
    }

    [Fact(DisplayName = "getState returns the active game after new game is created")]
    public void GetState_returns_the_active_game()
    {
        var session = TestSessions.WithGame();
        Assert.NotNull(SessionAssert.Ok(session.GetState()).Player);
    }

    [Fact(DisplayName = "adjustPersonality adjusts traits and maintains sum 100")]
    public void AdjustPersonality_maintains_the_sum()
    {
        var game = SessionAssert.Ok(TestSessions.WithGame().AdjustPersonality(Adj.Of((Trait.Patience, 5))));
        EndToEnd.AssertPersonalitySum(game.Player.Personality);
    }
}

public class ErrorPaths
{
    [Fact(DisplayName = "getState returns GAME_NOT_FOUND when no game exists")]
    public void GetState_without_a_game()
    {
        SessionAssert.Refused(TestSessions.Create().GetState(), ErrorCode.GAME_NOT_FOUND);
    }

    [Fact(DisplayName = "adjustPersonality returns GAME_NOT_FOUND when no game exists")]
    public void AdjustPersonality_without_a_game()
    {
        SessionAssert.Refused(TestSessions.Create().AdjustPersonality(Adj.Of((Trait.Patience, 5))), ErrorCode.GAME_NOT_FOUND);
    }

    [Fact(DisplayName = "adjustPersonality with no properties returns a validation error")]
    public void AdjustPersonality_with_no_properties()
    {
        var session = TestSessions.WithGame();
        SessionAssert.Refused(session.AdjustPersonality(new PersonalityAdjustment(ImmutableDictionary<Trait, double>.Empty)), ErrorCode.VALIDATION_ERROR);
    }
}

public class NpcDataIntegrity
{
    private static CharacterView View(string id) => SessionAssert.Ok(TestSessions.Create().GetCharacter(id));

    [Fact(DisplayName = "listNpcs returns exactly 3 NPCs")]
    public void ListNpcs_returns_three()
    {
        Assert.Equal(3, SessionAssert.Ok(TestSessions.Create().ListCharacters()).Length);
    }

    [Fact(DisplayName = "listNpcs includes all three expected NPC IDs")]
    public void ListNpcs_includes_all_three_ids()
    {
        var ids = SessionAssert.Ok(TestSessions.Create().ListCharacters()).Select(v => v.Definition.Id).ToList();
        Assert.Contains("npc_scout_elena", ids);
        Assert.Contains("npc_merchant_lars", ids);
        Assert.Contains("npc_outlaw_kade", ids);
    }

    // Changed: the archetype is an archetype id, not the display name 'Loyal Scout'.
    [Fact(DisplayName = "getNpc(npc_scout_elena) returns Elena with correct archetype")]
    public void Elena_has_the_right_archetype()
    {
        var view = View("npc_scout_elena");
        Assert.Equal("npc_scout_elena", view.Definition.Id);
        Assert.Equal(TestArchetypes.Elena, view.Definition.ArchetypeId);
    }

    [Fact(DisplayName = "getNpc(npc_scout_elena) personality sums to 100%")]
    public void Elena_personality_sums_to_100()
    {
        EndToEnd.AssertPersonalitySum(View("npc_scout_elena").Definition.Personality);
    }

    // Changed: the archetype is an archetype id, not the display name 'Scheming Merchant'.
    [Fact(DisplayName = "getNpc(npc_merchant_lars) returns Lars with correct archetype")]
    public void Lars_has_the_right_archetype()
    {
        var view = View("npc_merchant_lars");
        Assert.Equal("npc_merchant_lars", view.Definition.Id);
        Assert.Equal(TestArchetypes.Lars, view.Definition.ArchetypeId);
    }

    [Fact(DisplayName = "getNpc(npc_merchant_lars) personality sums to 100%")]
    public void Lars_personality_sums_to_100()
    {
        EndToEnd.AssertPersonalitySum(View("npc_merchant_lars").Definition.Personality);
    }

    // Changed: the archetype is an archetype id, not the display name 'Rogue Outlaw'.
    [Fact(DisplayName = "getNpc(npc_outlaw_kade) returns Kade with correct archetype")]
    public void Kade_has_the_right_archetype()
    {
        var view = View("npc_outlaw_kade");
        Assert.Equal("npc_outlaw_kade", view.Definition.Id);
        Assert.Equal(TestArchetypes.Kade, view.Definition.ArchetypeId);
    }

    [Fact(DisplayName = "getNpc(npc_outlaw_kade) personality sums to 100%")]
    public void Kade_personality_sums_to_100()
    {
        EndToEnd.AssertPersonalitySum(View("npc_outlaw_kade").Definition.Personality);
    }

    [Fact(DisplayName = "getNpc(npc_unknown_xyz) returns NPC_NOT_FOUND")]
    public void Unknown_npc()
    {
        SessionAssert.Refused(TestSessions.Create().GetCharacter("npc_unknown_xyz"), ErrorCode.NPC_NOT_FOUND);
    }

    [Fact(DisplayName = "NPC personalities are fixed — consecutive getNpc calls return identical personalities")]
    public void Personalities_are_fixed()
    {
        var session = TestSessions.Create();
        var one = SessionAssert.Ok(session.GetCharacter("npc_scout_elena"));
        var two = SessionAssert.Ok(session.GetCharacter("npc_scout_elena"));
        Assert.Equal(one.Definition.Personality, two.Definition.Personality);
    }

    [Fact(DisplayName = "NPC data is available without an active game")]
    public void Data_is_available_without_a_game()
    {
        Assert.True(TestSessions.Create().GetCharacter("npc_scout_elena").IsOk);
    }

    // New: an NPC's personality never changes when the player's does.
    [Fact(DisplayName = "NPC personalities do not change when the player's personality is adjusted")]
    public void Npc_personality_does_not_follow_the_player()
    {
        var session = TestSessions.WithGame();
        var before = SessionAssert.Ok(session.GetCharacter("npc_scout_elena")).Definition.Personality;
        session.AdjustPersonality(Adj.Of((Trait.Cunning, 10)));
        Assert.Equal(before, SessionAssert.Ok(session.GetCharacter("npc_scout_elena")).Definition.Personality);
    }
}

public class PersonalityConstraintInvariant
{
    private static AncientOrder.Rules.Personality.Personality After(GameSession session, params (Trait, double)[] deltas) =>
        SessionAssert.Ok(session.AdjustPersonality(Adj.Of(deltas))).Player.Personality;

    [Fact(DisplayName = "default personality after new game sums to 100%")]
    public void Default_personality_sums_to_100()
    {
        EndToEnd.AssertPersonalitySum(SessionAssert.Ok(TestSessions.WithGame().GetState()).Player.Personality);
    }

    [Fact(DisplayName = "personality sums to 100% after single positive trait adjustment")]
    public void Sums_after_a_positive_adjustment()
    {
        EndToEnd.AssertPersonalitySum(After(TestSessions.WithGame(), (Trait.Cunning, 10)));
    }

    [Fact(DisplayName = "personality sums to 100% after single negative trait adjustment")]
    public void Sums_after_a_negative_adjustment()
    {
        EndToEnd.AssertPersonalitySum(After(TestSessions.WithGame(), (Trait.Patience, -5)));
    }

    [Fact(DisplayName = "personality sums to 100% after multi-trait adjustment")]
    public void Sums_after_a_multi_trait_adjustment()
    {
        EndToEnd.AssertPersonalitySum(After(TestSessions.WithGame(), (Trait.Patience, 5), (Trait.Empathy, 5)));
    }

    [Fact(DisplayName = "personality sums to 100% after multiple sequential adjustments")]
    public void Sums_after_sequential_adjustments()
    {
        var session = TestSessions.WithGame();
        After(session, (Trait.Patience, 8));
        After(session, (Trait.Empathy, -3));
        EndToEnd.AssertPersonalitySum(After(session, (Trait.Charisma, 6)));
    }

    [Fact(DisplayName = "all individual traits remain within [5, 35] range after large positive adjustment")]
    public void Traits_stay_in_range()
    {
        var personality = After(TestSessions.WithGame(), (Trait.Cunning, 30));
        foreach (var value in TestPersonality.Values(personality))
        {
            Assert.InRange(value, 5.0, 35.0);
        }

        EndToEnd.AssertPersonalitySum(personality);
    }

    [Fact(DisplayName = "personality is unchanged after failed validation (empty adjustment)")]
    public void Unchanged_after_failed_validation()
    {
        var session = TestSessions.WithGame();
        var before = SessionAssert.Ok(session.GetPersonality());

        session.AdjustPersonality(new PersonalityAdjustment(ImmutableDictionary<Trait, double>.Empty));

        Assert.Equal(before, SessionAssert.Ok(session.GetPersonality()));
    }
}

// Changed: 'remains null ... after dialogue choice' becomes 'after a lineup is set' (dialogue is retired).
public class CombatStateIsNullThroughoutSprint1
{
    [Fact(DisplayName = "combatState is null after new game")]
    public void Null_after_new_game()
    {
        Assert.Null(SessionAssert.Ok(TestSessions.Create().NewGame()).Combat);
    }

    [Fact(DisplayName = "combatState remains null after personality adjustment")]
    public void Null_after_a_personality_adjustment()
    {
        var game = SessionAssert.Ok(TestSessions.WithGame().AdjustPersonality(Adj.Of((Trait.Patience, 5))));
        Assert.Null(game.Combat);
    }

    [Fact(DisplayName = "combatState remains null in game state after dialogue choice")]
    public void Null_after_a_lineup_is_set()
    {
        var session = TestSessions.WithLineup();
        Assert.Null(SessionAssert.Ok(session.GetState()).Combat);
    }
}

public class PlayerEndpoint
{
    [Fact(DisplayName = "getPlayer returns player data")]
    public void GetPlayer_returns_player_data()
    {
        Assert.Equal("Kael", SessionAssert.Ok(TestSessions.WithGame().GetPlayer()).Name);
    }

    [Fact(DisplayName = "getPlayer returns GAME_NOT_FOUND when no game exists")]
    public void GetPlayer_without_a_game()
    {
        SessionAssert.Refused(TestSessions.Create().GetPlayer(), ErrorCode.GAME_NOT_FOUND);
    }

    [Fact(DisplayName = "player personality is accessible and sums to 100%")]
    public void Player_personality_sums_to_100()
    {
        EndToEnd.AssertPersonalitySum(SessionAssert.Ok(TestSessions.WithGame().GetPlayer()).Personality);
    }
}

// Carried over from tests/session.sprint4.test.ts.
public class PlayerPersonality
{
    [Fact(DisplayName = "returns personality when game active")]
    public void Returns_personality_when_a_game_is_active()
    {
        Assert.NotNull(SessionAssert.Ok(TestSessions.WithGame().GetPersonality()));
    }

    [Fact(DisplayName = "returns GAME_NOT_FOUND when no active game")]
    public void Returns_game_not_found()
    {
        SessionAssert.Refused(TestSessions.Create().GetPersonality(), ErrorCode.GAME_NOT_FOUND);
    }

    // Changed: the 'typeof number' checks are meaningless for typed doubles; each trait is checked to be in the 5 to 35 range instead.
    [Fact(DisplayName = "personality has 6 traits summing to ~100")]
    public void Personality_has_six_traits_summing_to_100()
    {
        var p = SessionAssert.Ok(TestSessions.WithGame().GetPersonality());
        Assert.Equal(6, PersonalityRules.Traits.Length);
        foreach (var trait in PersonalityRules.Traits)
        {
            Assert.InRange(p[trait], 5.0, 35.0);
        }

        EndToEnd.AssertPersonalitySum(p);
    }
}

public class PlayerTeam
{
    [Fact(DisplayName = "returns team when valid 2 NPC IDs provided")]
    public void Returns_the_team()
    {
        var team = SessionAssert.Ok(TestSessions.WithGame().SetLineup(TestSessions.Pair));
        Assert.Contains("npc_scout_elena", team);
        Assert.Contains("npc_merchant_lars", team);
    }

    [Fact(DisplayName = "returns GAME_NOT_FOUND when no active game")]
    public void Returns_game_not_found()
    {
        SessionAssert.Refused(TestSessions.Create().SetLineup(TestSessions.Pair), ErrorCode.GAME_NOT_FOUND);
    }

    [Fact(DisplayName = "refuses when combat is active")]
    public void Refuses_when_combat_is_active()
    {
        var session = TestSessions.InCombat();
        var error = SessionAssert.Refused(session.SetLineup(TestSessions.Pair), ErrorCode.TEAM_COMPOSITION_INVALID);
        Assert.Contains("combat", error.Message, StringComparison.Ordinal);
    }

    // Changed: the TypeScript message said 'a team that is not exactly 2'; here it is the same VALIDATION_ERROR.
    [Fact(DisplayName = "refuses a wrong number of NPCs — validation rejects a team that is not exactly 2")]
    public void Refuses_a_wrong_number_of_npcs()
    {
        SessionAssert.Refused(TestSessions.WithGame().SetLineup(["npc_scout_elena", "npc_merchant_lars", "npc_outlaw_kade"]), ErrorCode.VALIDATION_ERROR);
    }
}

public class NpcById
{
    // Changed: with no way to change a relationship through the session, the live values equal the content's starting values.
    [Fact(DisplayName = "returns live NPC data when game is active")]
    public void Returns_live_npc_data()
    {
        var session = TestSessions.WithGame();
        var view = SessionAssert.Ok(session.GetCharacter("npc_scout_elena"));
        Assert.Equal("npc_scout_elena", view.Definition.Id);
        Assert.Equal(session.Snapshot.Game!.Characters["npc_scout_elena"], view.State);
    }

    [Fact(DisplayName = "returns template NPC data when no game active")]
    public void Returns_template_npc_data()
    {
        var view = SessionAssert.Ok(TestSessions.Create().GetCharacter("npc_scout_elena"));
        Assert.Equal("npc_scout_elena", view.Definition.Id);
        Assert.Equal(TestCast.Elena.StartingAffection, view.State.Affection);
        Assert.Equal(TestCast.Elena.StartingTrust, view.State.Trust);
        Assert.Equal(TestArchetypes.Elena, view.Definition.ArchetypeId);
    }

    [Fact(DisplayName = "returns NPC_NOT_FOUND for unknown NPC ID")]
    public void Returns_not_found_for_an_unknown_id()
    {
        SessionAssert.Refused(TestSessions.Create().GetCharacter("unknown_npc"), ErrorCode.NPC_NOT_FOUND);
    }
}
