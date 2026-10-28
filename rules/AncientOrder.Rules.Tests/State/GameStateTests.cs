using AncientOrder.Rules.Personality;
using AncientOrder.Rules.State;
using AncientOrder.Rules.Tests.Fixtures;
using AncientOrder.Rules.Tests.Support;

namespace AncientOrder.Rules.Tests.State;

// Carried over from src/state/gameState.test.ts (createNewGameState() becomes GameStateUpdates.NewGame over the test content).
public class CreateNewGameState
{
    private static GameState NewGame(long now = TestContent.Now) => TestContent.NewGame(now);

    [Fact(DisplayName = "returns a valid GameState object")]
    public void Returns_a_state() => Assert.NotNull(NewGame());

    [Fact(DisplayName = "each call returns a new object (no shared references)")]
    public void New_object_each_call() => Assert.NotSame(NewGame(), NewGame());

    [Fact(DisplayName = "player has a non-empty id")]
    public void Player_has_id()
    {
        var player = NewGame().Player;
        Assert.False(string.IsNullOrEmpty(player.Id));
    }

    // Changed: the id comes from the caller (the session's id source), so uniqueness is the caller's job; NewGame keeps the id it is given.
    [Fact(DisplayName = "each call generates a unique player id")]
    public void Player_id_is_the_one_given()
    {
        var content = TestContent.Create();
        var s1 = GameStateUpdates.NewGame(content, "player-a", TestContent.Now);
        var s2 = GameStateUpdates.NewGame(content, "player-b", TestContent.Now);
        Assert.Equal("player-a", s1.Player.Id);
        Assert.Equal("player-b", s2.Player.Id);
        Assert.NotEqual(s1.Player.Id, s2.Player.Id);
    }

    [Fact(DisplayName = "player name is set to a non-empty string")]
    public void Player_name_is_set()
    {
        var player = NewGame().Player;
        Assert.False(string.IsNullOrEmpty(player.Name));
    }

    [Fact(DisplayName = "player personality sums to 100%")]
    public void Player_personality_valid()
    {
        var result = PersonalityRules.Validate(NewGame().Player.Personality);
        Assert.True(result.Valid);
        Assert.Empty(result.Errors);
    }

    [Fact(DisplayName = "player personality traits are all within [5, 35]")]
    public void Player_traits_in_range()
    {
        var p = NewGame().Player.Personality;
        foreach (var trait in PersonalityRules.Traits)
        {
            Assert.InRange(p[trait], 5.0, 35.0);
        }
    }

    // Rewritten against the test cast (TS lines 69-84 checked the hard-coded roster).
    [Fact(DisplayName = "npcs record contains exactly 3 NPCs")]
    public void Characters_hold_the_three_cast_members() => Assert.Equal(3, NewGame().Characters.Count);

    [Fact(DisplayName = "Elena is retrievable by id from npcs")]
    public void Elena_retrievable()
    {
        var content = TestContent.Create();
        var view = CharacterRules.Get(content, TestContent.NewGame(content), TestCast.ElenaId).Value;
        Assert.Equal(TestArchetypes.Elena, view.Definition.ArchetypeId);
    }

    [Fact(DisplayName = "Lars is retrievable by id from npcs")]
    public void Lars_retrievable()
    {
        var content = TestContent.Create();
        var view = CharacterRules.Get(content, TestContent.NewGame(content), TestCast.LarsId).Value;
        Assert.Equal(TestArchetypes.Lars, view.Definition.ArchetypeId);
    }

    [Fact(DisplayName = "Kade is retrievable by id from npcs")]
    public void Kade_retrievable()
    {
        var content = TestContent.Create();
        var view = CharacterRules.Get(content, TestContent.NewGame(content), TestCast.KadeId).Value;
        Assert.Equal(TestArchetypes.Kade, view.Definition.ArchetypeId);
    }

    [Fact(DisplayName = "combatState is null")]
    public void Combat_is_null() => Assert.Null(NewGame().Combat);

    // Dropped from the port: currentDialogueNode, saveSlot, conversationLog (retired dialogue engine / not part of the state).

    // Changed: timestamp is the explicit now handed to NewGame, not the clock.
    [Fact(DisplayName = "timestamp is a positive number close to now")]
    public void Timestamp_is_the_given_now()
    {
        Assert.Equal(1_700_000_123_456, NewGame(1_700_000_123_456).Timestamp);
    }

    [Fact(DisplayName = "all NPC personalities in the initial state sum to 100%")]
    public void Cast_personalities_valid()
    {
        var content = TestContent.Create();
        foreach (var view in CharacterRules.All(content, TestContent.NewGame(content)))
        {
            Assert.True(PersonalityRules.Validate(view.Definition.Personality).Valid);
        }
    }
}

// New: a new game is built from the content it is given.
public class NewGameFromContent
{
    [Fact(DisplayName = "the player's name and the default personality come from the content's settings")]
    public void Player_from_settings()
    {
        var state = TestContent.NewGame();
        Assert.Equal("Kael", state.Player.Name);
        Assert.Equal(TestContent.PlayerId, state.Player.Id);
        Assert.Equal(PersonalityRules.CreateDefault(), state.Player.Personality);
    }

    [Fact(DisplayName = "a starting personality in the settings is used when given")]
    public void Settings_personality_used()
    {
        var personality = TestPersonality.Of(20, 20, 15, 15, 15, 15);
        var content = TestContent.Create(TestContent.Settings with { StartingPersonality = personality });
        Assert.Equal(personality, TestContent.NewGame(content).Player.Personality);
    }

    [Fact(DisplayName = "character states start from each character's starting affection and trust")]
    public void Starting_relationships()
    {
        var state = TestContent.NewGame();
        Assert.Equal(new CharacterState(0, 0), state.Characters[TestCast.ElenaId]);
        Assert.Equal(new CharacterState(0, -20), state.Characters[TestCast.LarsId]);
        Assert.Equal(new CharacterState(0, 0), state.Characters[TestCast.KadeId]);
    }

    [Fact(DisplayName = "the lineup starts empty and no combat is active")]
    public void Lineup_empty()
    {
        var state = TestContent.NewGame();
        Assert.Empty(state.Lineup);
        Assert.Null(state.Combat);
        Assert.Empty(state.ReactionProgress);
    }

    [Fact(DisplayName = "a character with different starting values in content starts with them")]
    public void Custom_starting_values()
    {
        var kade = TestCast.Kade with { StartingAffection = 35, StartingTrust = -45 };
        var content = TestContent.Create(characters: [TestCast.Elena, kade]);
        var state = TestContent.NewGame(content);
        Assert.Equal(2, state.Characters.Count);
        Assert.Equal(new CharacterState(35, -45), state.Characters[TestCast.KadeId]);
    }
}
