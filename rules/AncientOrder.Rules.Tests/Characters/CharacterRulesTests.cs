using AncientOrder.Rules.Common;
using AncientOrder.Rules.Party;
using AncientOrder.Rules.State;
using AncientOrder.Rules.Tests.Fixtures;
using AncientOrder.Rules.Tests.Support;

namespace AncientOrder.Rules.Tests.Characters;

// New: the characters module (Get, All, InLineup, the relationship changes) and the guarantee that play cannot reach fixed traits.
public class CharacterRulesTests
{
    private static readonly GameContent Content = TestContent.Create();

    private static GameState NewGame() => TestContent.NewGame(Content);

    [Fact(DisplayName = "Get returns the definition and the current state")]
    public void Get_returns_view()
    {
        var view = CharacterRules.Get(Content, NewGame(), TestCast.LarsId).Value;
        Assert.Equal("Lars", view.Definition.Name);
        Assert.Equal(new CharacterState(0, -20), view.State);
    }

    [Fact(DisplayName = "Get of an unknown character is refused with NPC_NOT_FOUND")]
    public void Get_unknown_refused()
    {
        var result = CharacterRules.Get(Content, NewGame(), "npc_unknown_xyz");
        Assert.False(result.IsOk);
        Assert.Equal(ErrorCode.NPC_NOT_FOUND, result.Error.Code);
        Assert.Equal("NPC not found: \"npc_unknown_xyz\"", result.Error.Message);
    }

    [Fact(DisplayName = "Get of a character missing from the game's character states is refused with NPC_NOT_FOUND")]
    public void Get_missing_from_state_refused()
    {
        var state = NewGame();
        state = state with { Characters = state.Characters.Remove(TestCast.KadeId) };
        Assert.Equal(ErrorCode.NPC_NOT_FOUND, CharacterRules.Get(Content, state, TestCast.KadeId).Error.Code);
    }

    [Fact(DisplayName = "All returns the characters in content order")]
    public void All_in_content_order()
    {
        var content = TestContent.Create(characters: [TestCast.Kade, TestCast.Elena, TestCast.Lars]);
        var views = CharacterRules.All(content, TestContent.NewGame(content));
        Assert.Equal([TestCast.KadeId, TestCast.ElenaId, TestCast.LarsId], views.Select(v => v.Definition.Id));
    }

    [Fact(DisplayName = "All skips a character missing from the game's character states")]
    public void All_skips_missing()
    {
        var state = NewGame();
        state = state with { Characters = state.Characters.Remove(TestCast.LarsId) };
        Assert.Equal([TestCast.ElenaId, TestCast.KadeId], CharacterRules.All(Content, state).Select(v => v.Definition.Id));
    }

    [Fact(DisplayName = "InLineup is empty before a lineup is chosen")]
    public void InLineup_empty() => Assert.Empty(CharacterRules.InLineup(Content, NewGame()));

    [Fact(DisplayName = "InLineup returns the lineup characters in lineup order")]
    public void InLineup_in_lineup_order()
    {
        var state = LineupRules.Set(Content, NewGame(), [TestCast.KadeId, TestCast.ElenaId], TestContent.Now).Value;
        Assert.Equal([TestCast.KadeId, TestCast.ElenaId], CharacterRules.InLineup(Content, state).Select(v => v.Definition.Id));
    }

    [Fact(DisplayName = "a change to an unknown character is refused with NPC_NOT_FOUND and leaves the state unchanged")]
    public void Change_unknown_refused()
    {
        var state = NewGame();
        var snapshot = state with { };
        foreach (var result in new[]
        {
            CharacterRules.ChangeAffection(Content, state, "nobody", 5, TestContent.Now + 1),
            CharacterRules.ChangeTrust(Content, state, "nobody", 5, TestContent.Now + 1),
            CharacterRules.ChangeRelationship(Content, state, "nobody", 5, 5, TestContent.Now + 1),
        })
        {
            Assert.False(result.IsOk);
            Assert.Equal(ErrorCode.NPC_NOT_FOUND, result.Error.Code);
        }

        Assert.Equal(snapshot, state);
    }

    [Fact(DisplayName = "a relationship change stamps the explicit now")]
    public void Change_stamps_now()
    {
        var updated = CharacterRules.ChangeAffection(Content, NewGame(), TestCast.ElenaId, 5, 42).Value;
        Assert.Equal(42, updated.Timestamp);
    }

    [Fact(DisplayName = "a non-finite change is refused with VALIDATION_ERROR")]
    public void Non_finite_change_refused()
    {
        var state = NewGame();
        Assert.Equal(ErrorCode.VALIDATION_ERROR, CharacterRules.ChangeAffection(Content, state, TestCast.ElenaId, double.NaN, 1).Error.Code);
        Assert.Equal(ErrorCode.VALIDATION_ERROR, CharacterRules.ChangeTrust(Content, state, TestCast.ElenaId, double.PositiveInfinity, 1).Error.Code);
        Assert.Equal(ErrorCode.VALIDATION_ERROR, CharacterRules.ChangeRelationship(Content, state, TestCast.ElenaId, 1, double.NegativeInfinity, 1).Error.Code);
    }

    [Fact(DisplayName = "character state holds only affection and trust")]
    public void Character_state_holds_only_what_changes()
    {
        var properties = typeof(CharacterState).GetProperties()
            .Where(p => p.GetIndexParameters().Length == 0 && p.Name != "EqualityContract")
            .Select(p => p.Name)
            .Order(StringComparer.Ordinal);
        Assert.Equal(["Affection", "Trust"], properties);
    }

    [Fact(DisplayName = "no update reaches a character's archetype, name, or fixed personality")]
    public void Fixed_traits_unreachable()
    {
        var before = Content.Characters.Definitions;
        var state = NewGame();
        state = CharacterRules.ChangeRelationship(Content, state, TestCast.ElenaId, 30, -30, 10).Value;
        state = CharacterRules.ChangeAffection(Content, state, TestCast.LarsId, 200, 11).Value;
        state = CharacterRules.ChangeTrust(Content, state, TestCast.KadeId, -200, 12).Value;
        state = GameStateUpdates.AdjustPlayerPersonality(state, Adj.Of((AncientOrder.Rules.Personality.Trait.Cunning, 30)), 13);
        state = LineupRules.Set(Content, state, [TestCast.ElenaId, TestCast.LarsId], 14).Value;

        Assert.Equal(before, Content.Characters.Definitions);

        var elena = CharacterRules.Get(Content, state, TestCast.ElenaId).Value;
        Assert.Equal(TestCast.Elena, elena.Definition);
        Assert.Equal(TestPersonality.Of(20, 20, 10, 15, 20, 15), elena.Definition.Personality);
        Assert.Equal(TestArchetypes.Elena, elena.Definition.ArchetypeId);
    }

    [Fact(DisplayName = "changing one character leaves the others' states the same objects")]
    public void Others_unchanged()
    {
        var state = NewGame();
        var updated = CharacterRules.ChangeTrust(Content, state, TestCast.ElenaId, 10, 1).Value;
        Assert.Same(state.Characters[TestCast.LarsId], updated.Characters[TestCast.LarsId]);
        Assert.Same(state.Characters[TestCast.KadeId], updated.Characters[TestCast.KadeId]);
        Assert.Equal(new CharacterState(0, 0), state.Characters[TestCast.ElenaId]);
    }
}
