using System.Collections.Immutable;
using AncientOrder.Rules.Combat;
using AncientOrder.Rules.Party;
using AncientOrder.Rules.Saves;
using AncientOrder.Rules.State;
using AncientOrder.Rules.Tests.Fixtures;
using AncientOrder.Rules.Tests.Support;
using static AncientOrder.Rules.Tests.Support.SaveFixtures;

namespace AncientOrder.Rules.Tests.Saves;

// New: loading a sound save against content that has changed. The save is written under one content and read under another.
public class SaveReconcilerTests
{
    private static GameContent ContentOf(params CharacterDefinition[] characters) =>
        TestSessions.Content(characters: [.. characters]);

    private static GameContent WithoutKade { get; } = ContentOf(TestCast.Elena, TestCast.Lars);

    private static GameContent WithoutLars { get; } = ContentOf(TestCast.Elena, TestCast.Kade);

    private static GameState PlayedGame(GameContent content)
    {
        var game = MakeGame("Rin", content);
        var characters = game.Characters
            .SetItem(TestCast.ElenaId, new CharacterState(12.5, 40))
            .SetItem(TestCast.LarsId, new CharacterState(-30, 7.25))
            .SetItem(TestCast.KadeId, new CharacterState(60, -60));
        return game with
        {
            Characters = characters.Where(c => content.Characters.TryGet(c.Key, out _)).ToImmutableDictionary(StringComparer.Ordinal),
            Lineup = [TestCast.ElenaId, content.Characters.TryGet(TestCast.LarsId, out _) ? TestCast.LarsId : TestCast.KadeId],
            Timestamp = 1_234,
        };
    }

    private static LoadedSave Load(GameState game, GameContent savedUnder, GameContent loadedUnder) =>
        ReadOk(WriteOk(game, savedUnder), loadedUnder);

    [Fact(DisplayName = "an unchanged content loads with an empty report")]
    public void Unchanged_content_has_an_empty_report()
    {
        var game = PlayedGame(SaveFixtures.Content);
        var loaded = Load(game, SaveFixtures.Content, SaveFixtures.Content);
        StateAssert.Equivalent(game, loaded.Game);
        Assert.True(loaded.Report.IsEmpty);
        Assert.True(LoadReport.Empty.IsEmpty);
    }

    [Fact(DisplayName = "drops characters no longer in the content and keeps the others as saved")]
    public void Drops_characters_no_longer_in_content()
    {
        var loaded = Load(PlayedGame(SaveFixtures.Content) with { Lineup = [] }, SaveFixtures.Content, WithoutKade);
        Assert.Equal(2, loaded.Game.Characters.Count);
        Assert.False(loaded.Game.Characters.ContainsKey(TestCast.KadeId));
        Assert.Equal(new CharacterState(12.5, 40), loaded.Game.Characters[TestCast.ElenaId]);
        Assert.Equal(new CharacterState(-30, 7.25), loaded.Game.Characters[TestCast.LarsId]);

        Assert.Equal([LoadAdjustmentKind.CharacterDropped, LoadAdjustmentKind.PartyMemberDropped], loaded.Report.Adjustments.Select(a => a.Kind));
        var adjustment = loaded.Report.Adjustments[0];
        Assert.Equal(TestCast.KadeId, adjustment.Id);
        Assert.Equal("npc_outlaw_kade is no longer in the content and was dropped.", adjustment.Message);
        Assert.Equal(new[] { TestCast.ElenaId, TestCast.LarsId }, loaded.Game.Party);
    }

    [Fact(DisplayName = "a dropped character in the lineup is removed from the party and the lineup one by one")]
    public void Dropped_lineup_member_is_removed()
    {
        var game = PlayedGame(SaveFixtures.Content) with { Lineup = [TestCast.ElenaId, TestCast.KadeId] };
        var loaded = Load(game, SaveFixtures.Content, WithoutKade);
        Assert.Equal([TestCast.ElenaId], loaded.Game.Lineup);

        Assert.Equal(
            [LoadAdjustmentKind.CharacterDropped, LoadAdjustmentKind.PartyMemberDropped, LoadAdjustmentKind.LineupMemberDropped],
            loaded.Report.Adjustments.Select(a => a.Kind));
        var removed = loaded.Report.Adjustments[2];
        Assert.Equal(TestCast.KadeId, removed.Id);
        Assert.Equal("npc_outlaw_kade is no longer in the content and was removed from the lineup.", removed.Message);
    }

    [Fact(DisplayName = "adds characters new in the content at their starting relationships")]
    public void Adds_new_characters_at_their_starting_relationships()
    {
        var savedUnder = WithoutLars;
        var game = PlayedGame(savedUnder);
        var loaded = Load(game, savedUnder, SaveFixtures.Content);

        Assert.Equal(3, loaded.Game.Characters.Count);
        Assert.Equal(new CharacterState(0, -20), loaded.Game.Characters[TestCast.LarsId]);
        Assert.Equal(new CharacterState(12.5, 40), loaded.Game.Characters[TestCast.ElenaId]);
        Assert.Equal(new CharacterState(60, -60), loaded.Game.Characters[TestCast.KadeId]);

        var adjustment = Assert.Single(loaded.Report.Adjustments);
        Assert.Equal(LoadAdjustmentKind.CharacterAdded, adjustment.Kind);
        Assert.Equal(TestCast.LarsId, adjustment.Id);
        Assert.Equal("npc_merchant_lars is new in the content and starts at its starting relationship.", adjustment.Message);
        Assert.Equal([TestCast.ElenaId, TestCast.KadeId], loaded.Game.Lineup);
    }

    [Fact(DisplayName = "reports additions in content order, then drops in saved order, then the party, then the lineup")]
    public void Report_order()
    {
        var savedUnder = ContentOf(TestCast.Kade, TestCast.Elena);
        var game = PlayedGame(SaveFixtures.Content) with
        {
            Characters = PlayedGame(SaveFixtures.Content).Characters.Remove(TestCast.LarsId),
            Lineup = [TestCast.KadeId, TestCast.ElenaId],
        };
        var loadedUnder = ContentOf(TestCast.Lars, TestCast.Elena);
        var loaded = Load(game, savedUnder, loadedUnder);

        Assert.Equal(
            [
                (LoadAdjustmentKind.CharacterAdded, TestCast.LarsId),
                (LoadAdjustmentKind.CharacterDropped, TestCast.KadeId),
                (LoadAdjustmentKind.PartyMemberDropped, TestCast.KadeId),
                (LoadAdjustmentKind.LineupMemberDropped, TestCast.KadeId),
            ],
            loaded.Report.Adjustments.Select(a => (a.Kind, a.Id)));
    }

    [Fact(DisplayName = "keeps a lineup that still fits the content")]
    public void Keeps_a_fitting_lineup()
    {
        var game = PlayedGame(SaveFixtures.Content);
        var loaded = Load(game, SaveFixtures.Content, SaveFixtures.Content);
        Assert.Equal([TestCast.ElenaId, TestCast.LarsId], loaded.Game.Lineup);
        Assert.DoesNotContain(loaded.Report.Adjustments, a => a.Kind == LoadAdjustmentKind.LineupCleared);
    }

    [Fact(DisplayName = "removes a lineup member with no combat stats and keeps the others")]
    public void Removes_a_lineup_member_with_no_combat_stats()
    {
        var noCombat = ContentOf(TestCast.Elena with { Combat = null }, TestCast.Lars, TestCast.Kade);
        var loaded = Load(PlayedGame(SaveFixtures.Content), SaveFixtures.Content, noCombat);
        Assert.Equal([TestCast.LarsId], loaded.Game.Lineup);
        var adjustment = Assert.Single(loaded.Report.Adjustments);
        Assert.Equal(LoadAdjustmentKind.LineupMemberDropped, adjustment.Kind);
        Assert.Equal("npc_scout_elena has no combat stats and was removed from the lineup.", adjustment.Message);
    }

    [Fact(DisplayName = "removes the second mention of a character named twice in the lineup")]
    public void Removes_a_repeat()
    {
        var game = PlayedGame(SaveFixtures.Content) with { Lineup = [TestCast.ElenaId, TestCast.ElenaId] };
        var loaded = Load(game, SaveFixtures.Content, SaveFixtures.Content);
        Assert.Equal([TestCast.ElenaId], loaded.Game.Lineup);
        Assert.Equal("npc_scout_elena appears more than once and was removed from the lineup.", Assert.Single(loaded.Report.Adjustments).Message);
    }

    [Fact(DisplayName = "removes a lineup member who is not in the party")]
    public void Removes_a_member_outside_the_party()
    {
        var game = PlayedGame(SaveFixtures.Content) with { Party = [TestCast.ElenaId, TestCast.KadeId] };
        var loaded = Load(game, SaveFixtures.Content, SaveFixtures.Content);
        Assert.Equal([TestCast.ElenaId], loaded.Game.Lineup);
        Assert.Equal("npc_merchant_lars is not in the party and was removed from the lineup.", Assert.Single(loaded.Report.Adjustments).Message);
    }

    [Fact(DisplayName = "clears a lineup larger than the content's max")]
    public void Clears_a_lineup_of_the_wrong_size()
    {

        var tooMany = PlayedGame(SaveFixtures.Content) with { Lineup = [TestCast.ElenaId, TestCast.LarsId, TestCast.KadeId] };
        var tooManyLoaded = Load(tooMany, SaveFixtures.Content, SaveFixtures.Content);
        Assert.Empty(tooManyLoaded.Game.Lineup);
        Assert.Equal(LoadAdjustmentKind.LineupCleared, Assert.Single(tooManyLoaded.Report.Adjustments).Kind);
    }

    [Fact(DisplayName = "never clears an empty lineup")]
    public void Empty_lineup_is_left_alone()
    {
        var size3 = TestContent.Create(new GameSettings("Kael", new LineupLimits(3, 3)) { StartingParty = [.. TestCast.All.Select(c => c.Id)] }, TestCast.All);
        var loaded = Load(PlayedGame(SaveFixtures.Content) with { Lineup = [] }, SaveFixtures.Content, size3);
        Assert.Empty(loaded.Game.Lineup);
        Assert.True(loaded.Report.IsEmpty);
    }

    [Fact(DisplayName = "keeps reaction XP, even for fighters the content no longer has")]
    public void Keeps_xp()
    {
        var progress = ImmutableDictionary<string, ReactionProgress>.Empty
            .WithComparers(StringComparer.Ordinal)
            .Add(TestCast.KadeId, new ReactionProgress(215, 4, 0))
            .Add(TestSessions.Hero, new ReactionProgress(1, 2, 3));
        var game = PlayedGame(SaveFixtures.Content) with { ReactionProgress = progress };
        var loaded = Load(game, SaveFixtures.Content, WithoutKade);
        Assert.Equal(new ReactionProgress(215, 4, 0), loaded.Game.ReactionProgress[TestCast.KadeId]);
        Assert.Equal(new ReactionProgress(1, 2, 3), loaded.Game.ReactionProgress[TestSessions.Hero]);
        Assert.Equal(2, loaded.Game.ReactionProgress.Count);
    }

    [Fact(DisplayName = "restores a saved fight as it was, dice included, whatever the content now says")]
    public void Restores_a_saved_fight()
    {
        var game = FightGame(2);
        var loaded = Load(game, LongFightContent, WithoutLars);
        Assert.Equal([TestCast.ElenaId], loaded.Game.Lineup);
        Assert.Equal(LoadAdjustmentKind.LineupMemberDropped, loaded.Report.Adjustments[^1].Kind);
        Assert.NotNull(loaded.Game.Combat);
        StateAssert.Equivalent(game.Combat, loaded.Game.Combat);
        Assert.Equal(game.Combat!.Dice, loaded.Game.Combat!.Dice);
    }

    [Fact(DisplayName = "keeps the player, the timestamp, and the envelope details")]
    public void Keeps_player_and_envelope()
    {
        var game = PlayedGame(SaveFixtures.Content) with { Player = PlayedGame(SaveFixtures.Content).Player with { Name = "Kira", Personality = TestPersonality.Of(30, 10, 10, 20, 10, 20) } };
        var bytes = WriteOk(game, SaveFixtures.Content, 6, 777);
        var loaded = ReadOk(bytes, WithoutKade);
        Assert.Equal(game.Player, loaded.Game.Player);
        Assert.Equal(1_234, loaded.Game.Timestamp);
        Assert.Equal(6, loaded.Slot);
        Assert.Equal(777, loaded.SavedAt);
        Assert.Equal("Kira", loaded.PlayerName);
    }

    [Fact(DisplayName = "a sound save always loads, even when every character changed")]
    public void Sound_save_always_loads()
    {
        var other = Renamed(TestCast.Elena, "npc_new_one");
        var content = ContentOf(other);
        var loaded = Load(PlayedGame(SaveFixtures.Content), SaveFixtures.Content, content);
        Assert.Equal([other.Id], loaded.Game.Characters.Keys);
        Assert.Equal(
            [
                LoadAdjustmentKind.CharacterAdded,
                LoadAdjustmentKind.CharacterDropped,
                LoadAdjustmentKind.CharacterDropped,
                LoadAdjustmentKind.CharacterDropped,
                LoadAdjustmentKind.PartyMemberDropped,
                LoadAdjustmentKind.PartyMemberDropped,
                LoadAdjustmentKind.PartyMemberDropped,
                LoadAdjustmentKind.LineupMemberDropped,
                LoadAdjustmentKind.LineupMemberDropped,
            ],
            loaded.Report.Adjustments.Select(a => a.Kind));
    }

    private static CharacterDefinition Renamed(CharacterDefinition basis, string id) => basis with { Id = id, Name = "Newcomer" };
}
