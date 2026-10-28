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

    [Fact(DisplayName = "rejects empty array")]
    public void Rejects_empty()
    {
        AssertRefused(Set());
    }

    [Fact(DisplayName = "rejects array with 1 element")]
    public void Rejects_one() => AssertRefused(Set(ElenaId));

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
    public void Error_code() => Assert.Equal(ErrorCode.TEAM_COMPOSITION_INVALID, Set().Error.Code);

    [Fact(DisplayName = "refusals carry today's messages")]
    public void Messages()
    {
        Assert.Equal("Team must contain exactly 2 NPCs", Set(ElenaId).Error.Message);
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

    [Fact(DisplayName = "the lineup size comes from content")]
    public void Size_from_content()
    {
        var content = TestContent.Create(TestContent.Settings with { LineupSize = 3 });
        var state = TestContent.NewGame(content);

        var refused = LineupRules.Set(content, state, [ElenaId, LarsId], Later);
        AssertRefused(refused);
        Assert.Equal("Team must contain exactly 3 NPCs", refused.Error.Message);

        var ok = LineupRules.Set(content, state, [ElenaId, LarsId, TestCast.KadeId], Later);
        Assert.Equal([ElenaId, LarsId, TestCast.KadeId], ok.Value.Lineup);
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
