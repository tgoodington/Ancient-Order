using AncientOrder.Rules.Common;
using AncientOrder.Rules.Tests.Fixtures;
using AncientOrder.Rules.Tests.Support;

namespace AncientOrder.Rules.Tests.Session;

// New: the session's character reads (list, lookup, synergy), with and without a game.
public class ListCharacters
{
    [Fact(DisplayName = "lists the cast in catalog order from content without a game, as a read")]
    public void Lists_content_without_a_game()
    {
        var session = TestSessions.Create();
        var snap = session.Snapshot;

        var views = SessionAssert.Ok(session.ListCharacters());

        Assert.Equal([TestCast.ElenaId, TestCast.LarsId, TestCast.KadeId], views.Select(v => v.Definition.Id));
        Assert.Equal(TestCast.Lars.StartingTrust, views[1].State.Trust);
        Assert.Equal(TestCast.Lars.StartingAffection, views[1].State.Affection);
        Assert.Same(snap, session.Snapshot);
    }

    [Fact(DisplayName = "lists the live game's characters when a game is active")]
    public void Lists_live_characters_with_a_game()
    {
        var session = TestSessions.WithGame();
        var snap = session.Snapshot;

        var views = SessionAssert.Ok(session.ListCharacters());

        Assert.Equal([TestCast.ElenaId, TestCast.LarsId, TestCast.KadeId], views.Select(v => v.Definition.Id));
        Assert.Equal(snap.Game!.Characters[TestCast.LarsId], views[1].State);
        Assert.Same(snap, session.Snapshot);
    }
}

public class GetCharacter
{
    [Fact(DisplayName = "returns the content's starting values without a game")]
    public void Starting_values_without_a_game()
    {
        var view = SessionAssert.Ok(TestSessions.Create().GetCharacter(TestCast.LarsId));
        Assert.Equal(TestCast.Lars, view.Definition);
        Assert.Equal(TestCast.Lars.StartingAffection, view.State.Affection);
        Assert.Equal(TestCast.Lars.StartingTrust, view.State.Trust);
    }

    [Fact(DisplayName = "returns the live values with a game")]
    public void Live_values_with_a_game()
    {
        var session = TestSessions.WithGame();
        var view = SessionAssert.Ok(session.GetCharacter(TestCast.LarsId));
        Assert.Equal(session.Snapshot.Game!.Characters[TestCast.LarsId], view.State);
    }

    [Fact(DisplayName = "an unknown id is NPC_NOT_FOUND with and without a game, and changes nothing")]
    public void Unknown_id_is_not_found()
    {
        foreach (var session in new[] { TestSessions.Create(), TestSessions.WithGame() })
        {
            var snap = session.Snapshot;
            var error = SessionAssert.Refused(session.GetCharacter("npc_nobody"), ErrorCode.NPC_NOT_FOUND);
            Assert.Equal("NPC not found: \"npc_nobody\"", error.Message);
            Assert.Same(snap, session.Snapshot);
        }
    }

    [Fact(DisplayName = "a null or over-long id is VALIDATION_ERROR")]
    public void Bad_ids_are_validation_errors()
    {
        var session = TestSessions.Create();
        SessionAssert.Refused(session.GetCharacter(null), ErrorCode.VALIDATION_ERROR);
        SessionAssert.Refused(session.GetCharacter(new string('x', 65)), ErrorCode.VALIDATION_ERROR);
    }
}

public class GetSynergy
{
    [Fact(DisplayName = "refuses without a game")]
    public void Refuses_without_a_game()
    {
        var session = TestSessions.Create();
        var snap = session.Snapshot;
        SessionAssert.Refused(session.GetSynergy(), ErrorCode.GAME_NOT_FOUND);
        Assert.Same(snap, session.Snapshot);
    }

    [Fact(DisplayName = "is empty with no lineup")]
    public void Empty_with_no_lineup()
    {
        Assert.Empty(SessionAssert.Ok(TestSessions.WithGame().GetSynergy()));
    }

    [Fact(DisplayName = "follows the lineup")]
    public void Follows_the_lineup()
    {
        var session = TestSessions.WithGame();

        session.SetLineup([TestCast.ElenaId, TestCast.LarsId]);
        var withElena = SessionAssert.Ok(session.GetSynergy());
        Assert.Equal("Bond", Assert.Single(withElena).Name);
        Assert.Equal(session.Snapshot.Synergy.Select(s => s.Name), withElena.Select(s => s.Name));

        session.SetLineup([TestCast.LarsId, TestCast.KadeId]);
        Assert.Empty(SessionAssert.Ok(session.GetSynergy()));
        Assert.Empty(session.Snapshot.Synergy);
    }
}
