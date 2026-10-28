using System.Collections.Immutable;
using AncientOrder.Rules.Combat;
using AncientOrder.Rules.Common;
using AncientOrder.Rules.Personality;
using AncientOrder.Rules.State;
using AncientOrder.Rules.Tests.Fixtures;
using AncientOrder.Rules.Tests.Support;

namespace AncientOrder.Rules.Tests.State;

// Carried over from the live blocks of src/state/stateUpdaters.test.ts.
// Dropped (retired narrative/dialogue engines): addConversationEntry (5), processDialogueChoice (10),
// initializeNarrative (8), updateNarrativeState (4), clearNarrative (4).
// updateTeamComposition (11) moved to Party/LineupRulesTests.cs.
internal static class StateUpdatesFixture
{
    public const long Later = TestContent.Now + 5_000;

    public static readonly GameContent Content = TestContent.Create();

    public static GameState Base() => TestContent.NewGame(Content);

    public static readonly AncientOrder.Rules.Personality.Personality Balanced = TestPersonality.Of(16.67, 16.67, 16.67, 16.67, 16.67, 16.65);

    public static CombatState FakeCombat(int round) => new(
        round,
        CombatPhase.AiDecision,
        [],
        [],
        [],
        [],
        CombatStatus.Active,
        new DiceState(1, 2, 3, 4));
}

// Changed: there is no separate updateTimestamp; every update stamps the explicit now, so the cases run through GameStateUpdates.WithCombat.
public class UpdateTimestamp
{
    private static readonly GameState Base = StateUpdatesFixture.Base();

    [Fact(DisplayName = "returns a new object (reference inequality)")]
    public void New_object() =>
        Assert.NotSame(Base, GameStateUpdates.WithCombat(Base, null, StateUpdatesFixture.Later));

    [Fact(DisplayName = "updates the timestamp field")]
    public void Sets_timestamp() =>
        Assert.Equal(StateUpdatesFixture.Later, GameStateUpdates.WithCombat(Base, null, StateUpdatesFixture.Later).Timestamp);

    [Fact(DisplayName = "does not change other fields")]
    public void Other_fields_same()
    {
        var updated = GameStateUpdates.WithCombat(Base, null, StateUpdatesFixture.Later);
        Assert.Same(Base.Player, updated.Player);
        Assert.Same(Base.Characters, updated.Characters);
        Assert.Equal(Base.Lineup, updated.Lineup);
    }

    [Fact(DisplayName = "does not mutate the input state")]
    public void Input_unchanged()
    {
        var original = Base.Timestamp;
        GameStateUpdates.WithCombat(Base, null, StateUpdatesFixture.Later);
        Assert.Equal(original, Base.Timestamp);
    }
}

public class UpdatePlayerPersonality
{
    private static readonly GameState Base = StateUpdatesFixture.Base();

    [Fact(DisplayName = "returns a new object (reference inequality)")]
    public void New_object() =>
        Assert.NotSame(Base, GameStateUpdates.SetPlayerPersonality(Base, StateUpdatesFixture.Balanced, StateUpdatesFixture.Later));

    [Fact(DisplayName = "sets the player personality to the new value")]
    public void Sets_value()
    {
        var personality = TestPersonality.Of(20, 20, 15, 15, 15, 15);
        Assert.Equal(personality, GameStateUpdates.SetPlayerPersonality(Base, personality, StateUpdatesFixture.Later).Player.Personality);
    }

    [Fact(DisplayName = "does not mutate the input state")]
    public void Input_unchanged()
    {
        var original = Base.Player.Personality with { };
        GameStateUpdates.SetPlayerPersonality(Base, TestPersonality.Of(20, 20, 15, 15, 15, 15), StateUpdatesFixture.Later);
        Assert.Equal(original, Base.Player.Personality);
    }

    [Fact(DisplayName = "creates a new player object (nested reference inequality)")]
    public void New_player() =>
        Assert.NotSame(Base.Player, GameStateUpdates.SetPlayerPersonality(Base, StateUpdatesFixture.Balanced, StateUpdatesFixture.Later).Player);

    [Fact(DisplayName = "preserves other player fields (id, name)")]
    public void Keeps_id_and_name()
    {
        var updated = GameStateUpdates.SetPlayerPersonality(Base, StateUpdatesFixture.Balanced, StateUpdatesFixture.Later);
        Assert.Equal(Base.Player.Id, updated.Player.Id);
        Assert.Equal(Base.Player.Name, updated.Player.Name);
    }

    [Fact(DisplayName = "preserves other state fields (npcs, conversationLog)")]
    public void Keeps_characters()
    {
        var updated = GameStateUpdates.SetPlayerPersonality(Base, StateUpdatesFixture.Balanced, StateUpdatesFixture.Later);
        Assert.Same(Base.Characters, updated.Characters);
    }
}

public class ApplyPersonalityAdjustment
{
    private static readonly GameState Base = StateUpdatesFixture.Base();

    private static double Sum(GameState s) => PersonalityRules.Sum(s.Player.Personality);

    [Fact(DisplayName = "returns a new object (reference inequality)")]
    public void New_object() =>
        Assert.NotSame(Base, GameStateUpdates.AdjustPlayerPersonality(Base, Adj.Of((Trait.Cunning, 5)), StateUpdatesFixture.Later));

    [Fact(DisplayName = "adjusts the specified trait")]
    public void Adjusts_trait()
    {
        var updated = GameStateUpdates.AdjustPlayerPersonality(Base, Adj.Of((Trait.Cunning, 5)), StateUpdatesFixture.Later);
        Assert.True(updated.Player.Personality.Cunning > Base.Player.Personality.Cunning);
    }

    [Fact(DisplayName = "maintains 100% sum after adjustment")]
    public void Keeps_sum()
    {
        var updated = GameStateUpdates.AdjustPlayerPersonality(
            Base, Adj.Of((Trait.Patience, 10), (Trait.Empathy, -5)), StateUpdatesFixture.Later);
        Assert.InRange(Sum(updated), 99.95, 100.05);
    }

    [Fact(DisplayName = "clamps traits to the [5, 35] range")]
    public void Clamps()
    {
        var updated = GameStateUpdates.AdjustPlayerPersonality(Base, Adj.Of((Trait.Patience, 50)), StateUpdatesFixture.Later);
        Assert.InRange(updated.Player.Personality.Patience, 5.0, 35.0);
    }

    [Fact(DisplayName = "does not mutate the input state")]
    public void Input_unchanged()
    {
        var original = Base.Player.Personality with { };
        GameStateUpdates.AdjustPlayerPersonality(Base, Adj.Of((Trait.Cunning, 5)), StateUpdatesFixture.Later);
        Assert.Equal(original, Base.Player.Personality);
    }

    [Fact(DisplayName = "empty adjustment preserves personality (within float tolerance)")]
    public void Empty_adjustment()
    {
        var updated = GameStateUpdates.AdjustPlayerPersonality(Base, Adj.Of(), StateUpdatesFixture.Later);
        Assert.InRange(Sum(updated), 99.95, 100.05);
    }
}

public class UpdateNPCAffection
{
    private static readonly GameContent Content = StateUpdatesFixture.Content;
    private static readonly GameState Base = StateUpdatesFixture.Base();
    private const string ElenaId = TestCast.ElenaId;

    private static GameState Change(double amount) =>
        CharacterRules.ChangeAffection(Content, Base, ElenaId, amount, StateUpdatesFixture.Later).Value;

    [Fact(DisplayName = "returns a new object (reference inequality)")]
    public void New_object() => Assert.NotSame(Base, Change(10));

    [Fact(DisplayName = "increases affection by the specified amount")]
    public void Increases()
    {
        var original = Base.Characters[ElenaId].Affection;
        Assert.Equal(original + 15, Change(15).Characters[ElenaId].Affection);
    }

    [Fact(DisplayName = "decreases affection by the specified amount")]
    public void Decreases()
    {
        var original = Base.Characters[ElenaId].Affection;
        Assert.Equal(original - 20, Change(-20).Characters[ElenaId].Affection);
    }

    [Fact(DisplayName = "clamps affection at +100")]
    public void Clamps_high() => Assert.Equal(100.0, Change(200).Characters[ElenaId].Affection);

    [Fact(DisplayName = "clamps affection at -100")]
    public void Clamps_low() => Assert.Equal(-100.0, Change(-200).Characters[ElenaId].Affection);

    // Changed: an unknown character is now an NPC_NOT_FOUND refusal (TypeScript returned the same state silently).
    [Fact(DisplayName = "returns state unchanged if NPC not found")]
    public void Unknown_character_refused()
    {
        var before = Base with { };
        var result = CharacterRules.ChangeAffection(Content, Base, "npc_unknown_xyz", 10, StateUpdatesFixture.Later);
        Assert.False(result.IsOk);
        Assert.Equal(ErrorCode.NPC_NOT_FOUND, result.Error.Code);
        Assert.Equal(before, Base);
    }

    [Fact(DisplayName = "does not mutate the input state")]
    public void Input_unchanged()
    {
        var original = Base.Characters[ElenaId].Affection;
        Change(10);
        Assert.Equal(original, Base.Characters[ElenaId].Affection);
    }

    [Fact(DisplayName = "creates new npcs record (nested reference inequality)")]
    public void New_characters_record() => Assert.NotSame(Base.Characters, Change(10).Characters);

    [Fact(DisplayName = "does not affect other NPCs")]
    public void Others_untouched()
    {
        var updated = Change(10);
        Assert.Same(Base.Characters[TestCast.LarsId], updated.Characters[TestCast.LarsId]);
        Assert.Same(Base.Characters[TestCast.KadeId], updated.Characters[TestCast.KadeId]);
    }
}

public class UpdateNPCTrust
{
    private static readonly GameContent Content = StateUpdatesFixture.Content;
    private static readonly GameState Base = StateUpdatesFixture.Base();
    private const string LarsId = TestCast.LarsId;

    private static GameState Change(double amount) =>
        CharacterRules.ChangeTrust(Content, Base, LarsId, amount, StateUpdatesFixture.Later).Value;

    [Fact(DisplayName = "returns a new object (reference inequality)")]
    public void New_object() => Assert.NotSame(Base, Change(10));

    [Fact(DisplayName = "increases trust by the specified amount")]
    public void Increases()
    {
        var original = Base.Characters[LarsId].Trust;
        Assert.Equal(-20.0, original);
        Assert.Equal(original + 10, Change(10).Characters[LarsId].Trust);
    }

    [Fact(DisplayName = "clamps trust at +100")]
    public void Clamps_high() => Assert.Equal(100.0, Change(200).Characters[LarsId].Trust);

    [Fact(DisplayName = "clamps trust at -100")]
    public void Clamps_low() => Assert.Equal(-100.0, Change(-200).Characters[LarsId].Trust);

    // Changed: an unknown character is now an NPC_NOT_FOUND refusal (TypeScript returned the same state silently).
    [Fact(DisplayName = "returns state unchanged if NPC not found")]
    public void Unknown_character_refused()
    {
        var before = Base with { };
        var result = CharacterRules.ChangeTrust(Content, Base, "npc_unknown_xyz", 10, StateUpdatesFixture.Later);
        Assert.False(result.IsOk);
        Assert.Equal(ErrorCode.NPC_NOT_FOUND, result.Error.Code);
        Assert.Equal(before, Base);
    }

    [Fact(DisplayName = "does not mutate the input state")]
    public void Input_unchanged()
    {
        var original = Base.Characters[LarsId].Trust;
        Change(10);
        Assert.Equal(original, Base.Characters[LarsId].Trust);
    }
}

public class UpdateNPCRelationship
{
    private static readonly GameContent Content = StateUpdatesFixture.Content;
    private static readonly GameState Base = StateUpdatesFixture.Base();
    private const string KadeId = TestCast.KadeId;

    private static GameState Change(double affection, double trust) =>
        CharacterRules.ChangeRelationship(Content, Base, KadeId, affection, trust, StateUpdatesFixture.Later).Value;

    [Fact(DisplayName = "returns a new object (reference inequality)")]
    public void New_object() => Assert.NotSame(Base, Change(10, 5));

    [Fact(DisplayName = "updates both affection and trust in one call")]
    public void Updates_both()
    {
        var origAffection = Base.Characters[KadeId].Affection;
        var origTrust = Base.Characters[KadeId].Trust;
        var updated = Change(15, -10);
        Assert.Equal(origAffection + 15, updated.Characters[KadeId].Affection);
        Assert.Equal(origTrust - 10, updated.Characters[KadeId].Trust);
    }

    [Fact(DisplayName = "clamps affection and trust to [-100, +100]")]
    public void Clamps()
    {
        var updated = Change(500, -500);
        Assert.Equal(100.0, updated.Characters[KadeId].Affection);
        Assert.Equal(-100.0, updated.Characters[KadeId].Trust);
    }

    // Changed: an unknown character is now an NPC_NOT_FOUND refusal (TypeScript returned the same state silently).
    [Fact(DisplayName = "returns state unchanged if NPC not found")]
    public void Unknown_character_refused()
    {
        var before = Base with { };
        var result = CharacterRules.ChangeRelationship(Content, Base, "npc_unknown_xyz", 10, 5, StateUpdatesFixture.Later);
        Assert.False(result.IsOk);
        Assert.Equal(ErrorCode.NPC_NOT_FOUND, result.Error.Code);
        Assert.Equal(before, Base);
    }

    [Fact(DisplayName = "does not mutate the input state")]
    public void Input_unchanged()
    {
        var origAffection = Base.Characters[KadeId].Affection;
        var origTrust = Base.Characters[KadeId].Trust;
        Change(10, 5);
        Assert.Equal(origAffection, Base.Characters[KadeId].Affection);
        Assert.Equal(origTrust, Base.Characters[KadeId].Trust);
    }

    [Fact(DisplayName = "produces the same result as sequential updateNPCAffection + updateNPCTrust")]
    public void Same_as_sequential()
    {
        var compound = Change(20, 10);
        var affected = CharacterRules.ChangeAffection(Content, Base, KadeId, 20, StateUpdatesFixture.Later).Value;
        var sequential = CharacterRules.ChangeTrust(Content, affected, KadeId, 10, StateUpdatesFixture.Later).Value;
        Assert.Equal(sequential.Characters[KadeId].Affection, compound.Characters[KadeId].Affection);
        Assert.Equal(sequential.Characters[KadeId].Trust, compound.Characters[KadeId].Trust);
    }
}

public class UpdateCombatState
{
    private static readonly GameState Base = StateUpdatesFixture.Base();

    [Fact(DisplayName = "returns a new object (reference inequality)")]
    public void New_object() =>
        Assert.NotSame(Base, GameStateUpdates.WithCombat(Base, null, StateUpdatesFixture.Later));

    [Fact(DisplayName = "sets combatState to a provided value")]
    public void Sets_combat()
    {
        var combat = StateUpdatesFixture.FakeCombat(1);
        Assert.Same(combat, GameStateUpdates.WithCombat(Base, combat, StateUpdatesFixture.Later).Combat);
    }

    [Fact(DisplayName = "clears combatState when null is passed")]
    public void Clears_combat()
    {
        var withCombat = GameStateUpdates.WithCombat(Base, StateUpdatesFixture.FakeCombat(1), StateUpdatesFixture.Later);
        Assert.Null(GameStateUpdates.WithCombat(withCombat, null, StateUpdatesFixture.Later).Combat);
    }

    [Fact(DisplayName = "does not mutate the input state")]
    public void Input_unchanged()
    {
        var original = Base.Combat;
        GameStateUpdates.WithCombat(Base, StateUpdatesFixture.FakeCombat(99), StateUpdatesFixture.Later);
        Assert.Same(original, Base.Combat);
    }

    [Fact(DisplayName = "preserves other fields (player, npcs, conversationLog)")]
    public void Keeps_other_fields()
    {
        var updated = GameStateUpdates.WithCombat(Base, StateUpdatesFixture.FakeCombat(1), StateUpdatesFixture.Later);
        Assert.Same(Base.Player, updated.Player);
        Assert.Same(Base.Characters, updated.Characters);
        Assert.Equal(Base.Lineup, updated.Lineup);
    }

    [Fact(DisplayName = "updates timestamp")]
    public void Updates_timestamp()
    {
        var stale = Base with { Timestamp = 0 };
        Assert.Equal(StateUpdatesFixture.Later, GameStateUpdates.WithCombat(stale, null, StateUpdatesFixture.Later).Timestamp);
    }
}
