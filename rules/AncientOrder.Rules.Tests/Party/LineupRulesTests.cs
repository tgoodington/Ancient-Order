using System.Collections.Immutable;
using AncientOrder.Rules.Combat;
using AncientOrder.Rules.Common;
using AncientOrder.Rules.Party;
using AncientOrder.Rules.State;
using AncientOrder.Rules.Tests.Fixtures;

namespace AncientOrder.Rules.Tests.Party;

// Carried over from the updateTeamComposition block of src/state/stateUpdaters.test.ts (updateTeamComposition becomes LineupRules.Set,
// whose TeamCompositionInvalid throw becomes a TEAM_COMPOSITION_INVALID refusal), plus the combat lock and the content-defined size.
public class UpdateTeamComposition
{
    private const string ElenaId = TestCast.ElenaId;
    private const string LarsId = TestCast.LarsId;
    private const long Later = TestContent.Now + 5_000;

    private static readonly GameContent Content = TestContent.Create();
    private static readonly GameState Base = TestContent.NewGame(Content);

    private static Result<GameState> Set(params string[] ids) => LineupRules.Set(Content, Base, [.. ids], Later);

    private static void AssertRefused(Result<GameState> result)
    {
        Assert.False(result.IsOk);
        Assert.Equal(ErrorCode.TEAM_COMPOSITION_INVALID, result.Error.Code);
    }

    [Fact(DisplayName = "sets team to provided NPC IDs")]
    public void Sets_team() => Assert.Equal([ElenaId, LarsId], Set(ElenaId, LarsId).Value.Lineup);

    [Fact(DisplayName = "returns a new object (reference inequality)")]
    public void New_object() => Assert.NotSame(Base, Set(ElenaId, LarsId).Value);

    [Fact(DisplayName = "does not mutate the input state")]
    public void Input_unchanged()
    {
        var original = Base.Lineup;
        Set(ElenaId, LarsId);
        Assert.Equal(original, Base.Lineup);
        Assert.Empty(Base.Lineup);
    }

    [Fact(DisplayName = "updates timestamp")]
    public void Updates_timestamp()
    {
        var stale = Base with { Timestamp = 0 };
        Assert.Equal(Later, LineupRules.Set(Content, stale, [ElenaId, LarsId], Later).Value.Timestamp);
    }

    [Fact(DisplayName = "preserves other fields (player, npcs, conversationLog)")]
    public void Preserves_other_fields()
    {
        var updated = Set(ElenaId, LarsId).Value;
        Assert.Same(Base.Player, updated.Player);
        Assert.Same(Base.Characters, updated.Characters);
    }

    [Fact(DisplayName = "accepts 0 to max ids while picking")]
    public void Accepts_partial()
    {
        Assert.Empty(Set().Value.Lineup);
        Assert.Equal([ElenaId], Set(ElenaId).Value.Lineup);
    }

    [Fact(DisplayName = "rejects array with 3 elements")]
    public void Rejects_three() => AssertRefused(Set(ElenaId, LarsId, TestCast.KadeId));

    [Fact(DisplayName = "rejects unknown NPC ID")]
    public void Rejects_unknown()
    {
        const string badId = "npc_unknown_xyz";
        var result = Set(ElenaId, badId);
        AssertRefused(result);
        Assert.Contains(badId, result.Error.Message, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "rejects duplicate NPC IDs")]
    public void Rejects_duplicates() => AssertRefused(Set(ElenaId, ElenaId));

    [Fact(DisplayName = "thrown error has code TEAM_COMPOSITION_INVALID")]
    public void Error_code() => Assert.Equal(ErrorCode.TEAM_COMPOSITION_INVALID, Set(ElenaId, ElenaId).Error.Code);

    [Fact(DisplayName = "refusals carry today's messages")]
    public void Messages()
    {
        Assert.Equal("Team can have at most 2 NPCs", Set(ElenaId, LarsId, TestCast.KadeId).Error.Message);
        Assert.Equal("Invalid NPC ID: npc_unknown_xyz", Set(ElenaId, "npc_unknown_xyz").Error.Message);
        Assert.Equal("Duplicate NPC ID: npc_scout_elena", Set(ElenaId, ElenaId).Error.Message);
    }

    [Fact(DisplayName = "a refusal leaves the state unchanged")]
    public void Refusal_leaves_state()
    {
        var snapshot = Base with { };
        Set(ElenaId);
        Assert.Equal(snapshot, Base);
    }

    [Fact(DisplayName = "no changes during combat")]
    public void Combat_lock()
    {
        var inCombat = GameStateUpdates.WithCombat(
            Base,
            new CombatState(1, CombatPhase.AiDecision, [], [], [], [], CombatStatus.Active, new DiceState(1, 2, 3, 4)),
            Later);
        var result = LineupRules.Set(Content, inCombat, [ElenaId, LarsId], Later);
        AssertRefused(result);
        Assert.Equal("Cannot change team during combat", result.Error.Message);
        Assert.Empty(inCombat.Lineup);
    }

    [Fact(DisplayName = "the lineup limit comes from content")]
    public void Size_from_content()
    {
        var content = TestContent.Create(TestContent.Settings with { Lineup = new LineupLimits(1, 3) });
        var state = TestContent.NewGame(content);

        var ok = LineupRules.Set(content, state, [ElenaId, LarsId, TestCast.KadeId], Later);
        Assert.Equal([ElenaId, LarsId, TestCast.KadeId], ok.Value.Lineup);
        AssertRefused(LineupRules.Set(Content, state, [ElenaId, LarsId, TestCast.KadeId], Later));
    }

    [Fact(DisplayName = "only party members can be picked")]
    public void Party_only()
    {
        var state = Base with { Party = [ElenaId] };
        var result = LineupRules.Set(Content, state, [ElenaId, LarsId], Later);
        AssertRefused(result);
        Assert.Equal($"{LarsId} is not in the party", result.Error.Message);
    }

    [Fact(DisplayName = "no changes during a conversation")]
    public void Conversation_lock()
    {
        var talking = Base with { Conversation = new AncientOrder.Rules.Conversations.ActiveConversation("c", "s") };
        var result = LineupRules.Set(Content, talking, [ElenaId], Later);
        AssertRefused(result);
        Assert.Equal("Cannot change team during a conversation", result.Error.Message);
    }

    [Fact(DisplayName = "Ready reports whether the lineup can fight, with a reason")]
    public void Readiness()
    {
        var limits = new LineupLimits(2, 3);
        var none = LineupRules.Ready(Content, Base, limits);
        Assert.False(none.Ready);
        Assert.Equal("Pick at least 2 fighters", none.Reason);

        var one = Base with { Lineup = [ElenaId] };
        Assert.Equal("Pick at least 2 fighters", LineupRules.Ready(Content, one, limits).Reason);

        var two = Base with { Lineup = [ElenaId, LarsId] };
        var ready = LineupRules.Ready(Content, two, limits);
        Assert.True(ready.Ready);
        Assert.Null(ready.Reason);

        Assert.Equal("Pick at most 1 fighter", LineupRules.Ready(Content, two, new LineupLimits(0, 1)).Reason);

        var outsider = two with { Party = [ElenaId] };
        Assert.False(LineupRules.Ready(Content, outsider, limits).Ready);
    }

    [Fact(DisplayName = "a character without combat stats is refused")]
    public void No_combat_stats()
    {
        var content = TestContent.Create(characters: [TestCast.Elena, TestCast.Lars with { Combat = null }]);
        var state = TestContent.NewGame(content);
        var result = LineupRules.Set(content, state, [ElenaId, LarsId], Later);
        AssertRefused(result);
        Assert.Equal("npc_merchant_lars has no combat stats", result.Error.Message);
    }

    [Fact(DisplayName = "a default array is refused with the size message")]
    public void Default_array() =>
        AssertRefused(LineupRules.Set(Content, Base, default(ImmutableArray<string>), Later));
}

// T32: the order refusals come in when several things are wrong at once, and the readiness reasons.
public class LineupRefusalOrder
{
    private const string ElenaId = TestCast.ElenaId;
    private const string LarsId = TestCast.LarsId;
    private const string KadeId = TestCast.KadeId;
    private const string Unknown = "npc_unknown_xyz";
    private const long Later = TestContent.Now + 5_000;

    private static readonly GameContent Content = TestContent.Create();
    private static readonly GameState Base = TestContent.NewGame(Content);

    private static readonly CombatState ActiveCombat =
        new(1, CombatPhase.AiDecision, [], [], [], [], CombatStatus.Active, new DiceState(1, 2, 3, 4));

    private static readonly AncientOrder.Rules.Conversations.ActiveConversation Talking = new("c", "s");

    private static string Refusal(GameContent content, GameState state, params string[] ids)
    {
        var result = LineupRules.Set(content, state, [.. ids], Later);
        Assert.False(result.IsOk);
        Assert.Equal(ErrorCode.TEAM_COMPOSITION_INVALID, result.Error.Code);
        return result.Error.Message;
    }

    [Fact]
    public void Combat_is_refused_before_everything_else()
    {
        var state = Base with { Combat = ActiveCombat, Conversation = Talking };
        Assert.Equal("Cannot change team during combat", Refusal(Content, state, ElenaId, LarsId, KadeId, Unknown, Unknown));
    }

    [Fact]
    public void A_conversation_is_refused_before_size_and_content_problems()
    {
        var state = Base with { Conversation = Talking };
        Assert.Equal("Cannot change team during a conversation", Refusal(Content, state, ElenaId, LarsId, KadeId, Unknown));
    }

    [Fact]
    public void Too_many_is_refused_before_an_unknown_id()
    {
        Assert.Equal("Team can have at most 2 NPCs", Refusal(Content, Base, Unknown, ElenaId, LarsId));
    }

    [Fact]
    public void An_unknown_id_is_refused_before_not_being_in_the_party()
    {
        var state = Base with { Party = [] };
        Assert.Equal($"Invalid NPC ID: {Unknown}", Refusal(Content, state, ElenaId, Unknown));
    }

    [Fact]
    public void Not_being_in_the_party_is_refused_before_having_no_combat_stats()
    {
        var content = TestContent.Create(characters: [TestCast.Elena, TestCast.Lars with { Combat = null }]);
        var state = TestContent.NewGame(content) with { Party = [ElenaId] };
        Assert.Equal($"{LarsId} is not in the party", Refusal(content, state, LarsId, ElenaId));
    }

    [Fact]
    public void Having_no_combat_stats_is_refused_before_a_duplicate()
    {
        var content = TestContent.Create(characters: [TestCast.Elena, TestCast.Lars with { Combat = null }]);
        var state = TestContent.NewGame(content);
        Assert.Equal($"{LarsId} has no combat stats", Refusal(content, state, LarsId, LarsId));
    }

    [Fact]
    public void A_duplicate_is_the_last_refusal()
    {
        Assert.Equal($"Duplicate NPC ID: {ElenaId}", Refusal(Content, Base, ElenaId, ElenaId));
    }

    [Fact]
    public void A_refusal_leaves_an_earlier_lineup_as_it_was()
    {
        var state = LineupRules.Set(Content, Base, [ElenaId], Later).Value;
        Assert.False(LineupRules.Set(Content, state, [ElenaId, ElenaId], Later).IsOk);
        Assert.False(LineupRules.Set(Content, state with { Conversation = Talking }, [], Later).IsOk);
        Assert.Equal([ElenaId], state.Lineup);
    }

    [Fact]
    public void A_lineup_can_be_cleared_and_reordered()
    {
        var state = LineupRules.Set(Content, Base, [ElenaId, LarsId], Later).Value;
        Assert.Equal([LarsId, ElenaId], LineupRules.Set(Content, state, [LarsId, ElenaId], Later).Value.Lineup);
        Assert.Empty(LineupRules.Set(Content, state, [], Later).Value.Lineup);
    }

    [Fact]
    public void Limits_of_zero_allow_only_an_empty_lineup()
    {
        var content = TestContent.Create(TestContent.Settings with { Lineup = new LineupLimits(0, 0) });
        var state = TestContent.NewGame(content);
        Assert.Empty(LineupRules.Set(content, state, [], Later).Value.Lineup);
        Assert.Equal("Team can have at most 0 NPCs", Refusal(content, state, ElenaId));
        Assert.True(LineupRules.Ready(content, state, new LineupLimits(0, 0)).Ready);
    }

    [Fact]
    public void Ready_says_fighter_in_the_singular_for_a_limit_of_one_and_plural_otherwise()
    {
        Assert.Equal("Pick at least 1 fighter", LineupRules.Ready(Content, Base, new LineupLimits(1, 3)).Reason);
        var two = Base with { Lineup = [ElenaId, LarsId] };
        Assert.Equal("Pick at most 1 fighter", LineupRules.Ready(Content, two, new LineupLimits(0, 1)).Reason);
        var three = Base with { Lineup = [ElenaId, LarsId, KadeId] };
        Assert.Equal("Pick at most 2 fighters", LineupRules.Ready(Content, three, new LineupLimits(0, 2)).Reason);
    }

    [Fact]
    public void Ready_names_a_member_who_left_the_party_or_cannot_fight()
    {
        var limits = new LineupLimits(1, 3);
        var gone = Base with { Lineup = [ElenaId], Party = [LarsId] };
        Assert.Equal(new LineupReadiness(false, "Elena is not in the party"), LineupRules.Ready(Content, gone, limits));

        var content = TestContent.Create(characters: [TestCast.Elena, TestCast.Lars with { Combat = null }]);
        var state = TestContent.NewGame(content) with { Lineup = [LarsId] };
        Assert.Equal(new LineupReadiness(false, "Lars cannot fight"), LineupRules.Ready(content, state, limits));

        var stray = Base with { Lineup = [Unknown] };
        Assert.Equal($"{Unknown} is not in the party", LineupRules.Ready(Content, stray, limits).Reason);
    }
}
