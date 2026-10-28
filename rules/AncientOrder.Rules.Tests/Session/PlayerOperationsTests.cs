using System.Collections.Immutable;
using AncientOrder.Rules.Common;
using AncientOrder.Rules.Personality;
using AncientOrder.Rules.Tests.Fixtures;
using AncientOrder.Rules.Tests.Support;

namespace AncientOrder.Rules.Tests.Session;

// Carried over from src/session/operations/player.test.ts and protoKeys.test.ts. setTeam is SetLineup, getNpc is GetCharacter.
// The narrative-lock test ('is locked during a narrative scene...') is dropped with the retired narrative engine.
public class PlayerReads
{
    [Fact(DisplayName = "refuse without a game")]
    public void Refuse_without_a_game()
    {
        var session = TestSessions.Create();
        SessionAssert.Refused(session.GetPlayer(), ErrorCode.GAME_NOT_FOUND);
        SessionAssert.Refused(session.GetPersonality(), ErrorCode.GAME_NOT_FOUND);
    }
}

public class AdjustPersonality
{
    [Fact(DisplayName = "refuses without a game")]
    public void Refuses_without_a_game()
    {
        SessionAssert.Refused(TestSessions.Create().AdjustPersonality(Adj.Of((Trait.Patience, 5))), ErrorCode.GAME_NOT_FOUND);
    }

    [Fact(DisplayName = "applies the adjustment to a new game state and leaves the earlier one untouched")]
    public void Applies_to_a_new_state()
    {
        var session = TestSessions.WithGame();
        var before = session.Snapshot.Game!;
        var patienceBefore = before.Player.Personality.Patience;

        var r = session.AdjustPersonality(Adj.Of((Trait.Patience, 5)));

        Assert.True(r.IsOk);
        var after = session.Snapshot.Game!;
        Assert.NotSame(before, after);
        Assert.True(after.Player.Personality.Patience > patienceBefore);
        Assert.Equal(patienceBefore, before.Player.Personality.Patience);
    }

    // Changed: the bad inputs are the ones the typed API can express (no string, array, or non-number value).
    [Fact(DisplayName = "refuses invalid input without changing or notifying")]
    public void Refuses_invalid_input()
    {
        var session = TestSessions.WithGame();
        var snap = session.Snapshot;
        var calls = 0;
        using var subscription = session.Subscribe(_ => calls++);
        PersonalityAdjustment?[] bad =
        [
            new PersonalityAdjustment(ImmutableDictionary<Trait, double>.Empty),
            Adj.Of(((Trait)99, 5)),
            Adj.Of((Trait.Patience, double.NaN)),
            Adj.Of((Trait.Patience, double.PositiveInfinity)),
            new PersonalityAdjustment(null!),
            null,
        ];
        foreach (var adjustment in bad)
        {
            SessionAssert.Refused(session.AdjustPersonality(adjustment), ErrorCode.VALIDATION_ERROR);
        }

        Assert.Same(snap, session.Snapshot);
        Assert.Equal(0, calls);
    }
}

public class SetTeam
{
    [Fact(DisplayName = "refuses without a game")]
    public void Refuses_without_a_game()
    {
        SessionAssert.Refused(TestSessions.Create().SetLineup(TestSessions.Pair), ErrorCode.GAME_NOT_FOUND);
    }

    [Fact(DisplayName = "sets a team of two companions")]
    public void Sets_a_team_of_two()
    {
        var session = TestSessions.WithGame();
        var r = session.SetLineup(TestSessions.Pair);
        Assert.Equal([TestCast.ElenaId, TestCast.LarsId], SessionAssert.Ok(r));
        Assert.Equal([TestCast.ElenaId, TestCast.LarsId], session.Snapshot.Game!.Lineup);
    }

    [Fact(DisplayName = "refuses anything that is not exactly two string ids")]
    public void Refuses_anything_but_two_string_ids()
    {
        var session = TestSessions.WithGame();
        var snap = session.Snapshot;
        ImmutableArray<string?>[] cases =
        [
            default,
            [],
            [TestCast.ElenaId],
            [TestCast.ElenaId, TestCast.LarsId, TestCast.KadeId],
            [TestCast.ElenaId, null],
            [TestCast.ElenaId, new string('x', 65)],
        ];
        foreach (var bad in cases)
        {
            SessionAssert.Refused(session.SetLineup(bad), ErrorCode.VALIDATION_ERROR);
        }

        Assert.Same(snap, session.Snapshot);
    }

    [Fact(DisplayName = "refuses an unknown companion with TEAM_COMPOSITION_INVALID")]
    public void Refuses_an_unknown_companion()
    {
        var session = TestSessions.WithGame();
        var error = SessionAssert.Refused(session.SetLineup([TestCast.ElenaId, "npc_nobody"]), ErrorCode.TEAM_COMPOSITION_INVALID);
        Assert.Equal("Invalid NPC ID: npc_nobody", error.Message);
    }

    [Fact(DisplayName = "refuses the same companion twice with TEAM_COMPOSITION_INVALID")]
    public void Refuses_the_same_companion_twice()
    {
        var session = TestSessions.WithGame();
        var snap = session.Snapshot;
        SessionAssert.Refused(session.SetLineup([TestCast.ElenaId, TestCast.ElenaId]), ErrorCode.TEAM_COMPOSITION_INVALID);
        Assert.Same(snap, session.Snapshot);
    }

    [Fact(DisplayName = "is locked during combat, and the refusal changes nothing")]
    public void Is_locked_during_combat()
    {
        var session = TestSessions.InCombat();
        var snap = session.Snapshot;
        var error = SessionAssert.Refused(session.SetLineup(TestSessions.Pair), ErrorCode.TEAM_COMPOSITION_INVALID);
        Assert.Equal("Cannot change team during combat", error.Message);
        Assert.Same(snap, session.Snapshot);
    }
}

public class PrototypeKeys
{
    public static IEnumerable<TheoryDataRow<string, bool>> GetNpcCases()
    {
        foreach (var withGame in new[] { false, true })
        {
            foreach (var key in new[] { "constructor", "__proto__", "toString" })
            {
                yield return new TheoryDataRow<string, bool>(key, withGame)
                {
                    TestDisplayName = $"getNpc('{key}') is NPC_NOT_FOUND {(withGame ? "with" : "without")} a game",
                };
            }
        }
    }

    // Changed: these ids are simply unknown ids in C#; the refusal is the same.
    [Theory]
    [MemberData(nameof(GetNpcCases))]
    public void GetNpc_prototype_key_is_not_found(string key, bool withGame)
    {
        var session = withGame ? TestSessions.WithGame() : TestSessions.Create();
        SessionAssert.Refused(session.GetCharacter(key), ErrorCode.NPC_NOT_FOUND);
    }

    // Changed: a null id stands in for a non-string id.
    [Fact(DisplayName = "getNpc refuses a non-string id")]
    public void GetNpc_refuses_a_non_string_id()
    {
        SessionAssert.Refused(TestSessions.Create().GetCharacter(null), ErrorCode.VALIDATION_ERROR);
    }

    [Fact(DisplayName = "setTeam refuses prototype keys and leaves the snapshot unchanged")]
    public void SetTeam_refuses_prototype_keys()
    {
        var session = TestSessions.WithGame();
        var snap = session.Snapshot;
        var error = SessionAssert.Refused(session.SetLineup(["constructor", "toString"]), ErrorCode.TEAM_COMPOSITION_INVALID);
        Assert.Equal("Invalid NPC ID: constructor", error.Message);
        Assert.Same(snap, session.Snapshot);
    }
}
