using AncientOrder.Rules.Common;
using AncientOrder.Rules.Effects;
using AncientOrder.Rules.Party;
using AncientOrder.Rules.State;
using AncientOrder.Rules.Tests.Fixtures;

namespace AncientOrder.Rules.Tests.Party;

// T32: the party as choices shape it. PartyTests already covers join order, double join, leave also leaving the lineup, and the
// join/leave effect reports; this adds the starting party from content, who stays, errors, and the lineup after a departure.
public class PartyRulesChoices
{
    private const string ElenaId = TestCast.ElenaId;
    private const string LarsId = TestCast.LarsId;
    private const string KadeId = TestCast.KadeId;
    private const long Later = TestContent.Now + 5_000;

    private static GameContent ContentWith(params string[] startingParty) =>
        TestContent.Create(TestContent.Settings with { StartingParty = [.. startingParty] });

    [Fact]
    public void A_new_game_starts_with_exactly_the_content_party_in_its_order()
    {
        var game = TestContent.NewGame(ContentWith(KadeId, ElenaId));
        Assert.Equal([KadeId, ElenaId], game.Party);
        Assert.Empty(game.Lineup);
    }

    [Fact]
    public void A_new_game_with_no_starting_party_has_nobody()
    {
        Assert.Empty(TestContent.NewGame(ContentWith()).Party);
    }

    [Fact]
    public void Joining_adds_one_member_and_leaves_the_lineup_alone()
    {
        var content = ContentWith(ElenaId);
        var game = LineupRules.Set(content, TestContent.NewGame(content), [ElenaId], Later).Value;
        var joined = PartyRules.Join(content, game, LarsId, Later + 1).Value;
        Assert.Equal([ElenaId, LarsId], joined.Party);
        Assert.Equal([ElenaId], joined.Lineup);
        Assert.Equal(Later + 1, joined.Timestamp);
    }

    [Fact]
    public void Leaving_removes_a_member_from_the_lineup_and_keeps_the_others_in_order()
    {
        var content = ContentWith(ElenaId, LarsId, KadeId);
        var game = LineupRules.Set(content, TestContent.NewGame(content), [KadeId, ElenaId], Later).Value;
        var left = PartyRules.Leave(content, game, ElenaId, Later + 1).Value;
        Assert.Equal([LarsId, KadeId], left.Party);
        Assert.Equal([KadeId], left.Lineup);
    }

    [Fact]
    public void Leaving_a_member_not_in_the_lineup_keeps_the_lineup()
    {
        var content = ContentWith(ElenaId, LarsId);
        var game = LineupRules.Set(content, TestContent.NewGame(content), [ElenaId], Later).Value;
        var left = PartyRules.Leave(content, game, LarsId, Later + 1).Value;
        Assert.Equal([ElenaId], left.Party);
        Assert.Equal([ElenaId], left.Lineup);
    }

    [Fact]
    public void Someone_who_left_cannot_be_picked_until_they_rejoin_at_the_end()
    {
        var content = ContentWith(ElenaId, LarsId);
        var game = PartyRules.Leave(content, TestContent.NewGame(content), ElenaId, Later).Value;
        var refused = LineupRules.Set(content, game, [ElenaId], Later);
        Assert.False(refused.IsOk);
        Assert.Equal(ErrorCode.TEAM_COMPOSITION_INVALID, refused.Error.Code);

        game = PartyRules.Join(content, game, ElenaId, Later + 1).Value;
        Assert.Equal([LarsId, ElenaId], game.Party);
        Assert.True(LineupRules.Set(content, game, [ElenaId], Later + 2).IsOk);
    }

    [Fact]
    public void Joining_or_leaving_an_unknown_character_is_an_error_with_the_state_unchanged()
    {
        var content = ContentWith(ElenaId);
        var game = TestContent.NewGame(content);
        var join = PartyRules.Join(content, game, "nobody", Later);
        Assert.False(join.IsOk);
        Assert.Equal(ErrorCode.NPC_NOT_FOUND, join.Error.Code);
        var leave = PartyRules.Leave(content, game, "nobody", Later);
        Assert.False(leave.IsOk);
        Assert.Equal(ErrorCode.NPC_NOT_FOUND, leave.Error.Code);
        Assert.Equal([ElenaId], game.Party);
    }

    [Fact]
    public void Joining_and_leaving_are_allowed_during_a_conversation_but_picking_a_lineup_is_not()
    {
        var content = ContentWith(ElenaId);
        var talking = TestContent.NewGame(content) with { Conversation = new AncientOrder.Rules.Conversations.ActiveConversation("c", "s") };
        var joined = PartyRules.Join(content, talking, LarsId, Later).Value;
        Assert.Equal([ElenaId, LarsId], joined.Party);
        Assert.False(LineupRules.Set(content, joined, [LarsId], Later).IsOk);
        Assert.Equal([LarsId], PartyRules.Leave(content, joined, ElenaId, Later).Value.Party);
    }

    [Fact]
    public void Effects_in_order_can_join_then_leave_the_same_person()
    {
        var content = ContentWith();
        var game = TestContent.NewGame(content);
        var applied = EffectRules.Apply(content, game, [new Effect.Join(ElenaId), new Effect.Join(LarsId), new Effect.Leave(ElenaId)], Later).Value;
        Assert.Equal([LarsId], applied.Game.Party);
    }
}
