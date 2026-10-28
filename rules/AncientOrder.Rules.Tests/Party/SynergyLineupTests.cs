using AncientOrder.Rules.Combat;
using AncientOrder.Rules.Party;
using AncientOrder.Rules.Personality;
using AncientOrder.Rules.State;
using AncientOrder.Rules.Tests.Fixtures;
using AncientOrder.Rules.Tests.Support;

namespace AncientOrder.Rules.Tests.Party;

// New: synergy is worked out from the player and the current lineup only, on demand.
public class SynergyLineupTests
{
    private const long Now = TestContent.Now;

    private static GameState WithLineup(GameContent content, GameState state, params string[] ids) =>
        LineupRules.Set(content, state, [.. ids], Now).Value;

    // Aligned with Kade's top traits (charisma, cunning) and with neither Elena nor Lars.
    private static readonly AncientOrder.Rules.Personality.Personality KadeLike = TestPersonality.Of(8, 8, 30, 6, 13, 35);

    [Fact(DisplayName = "no lineup means no bonus")]
    public void No_lineup_no_bonus()
    {
        var content = TestContent.Create();
        var state = GameStateUpdates.SetPlayerPersonality(TestContent.NewGame(content), KadeLike, Now);
        Assert.Empty(Synergy.Current(content, state));
    }

    [Fact(DisplayName = "the bonus is worked out from the player and the lineup in lineup order")]
    public void Lineup_gives_bonus()
    {
        var content = TestContent.Create();
        var state = WithLineup(content, TestContent.NewGame(content), TestCast.ElenaId, TestCast.LarsId);

        var bonus = Assert.Single(Synergy.Current(content, state));
        Assert.Equal("Bond", bonus.Name);
        Assert.Equal(CombatStat.Speed, bonus.Stat);
        Assert.Equal(StatStep.Multiply, bonus.Step);
        Assert.Equal(1.10, bonus.Value);
        Assert.Equal((16.67 + 16.67) / 40, bonus.MatchQuality);
    }

    [Fact(DisplayName = "only the lineup counts, not the rest of the cast")]
    public void Only_lineup_counts()
    {
        var content = TestContent.Create();
        var start = GameStateUpdates.SetPlayerPersonality(TestContent.NewGame(content), KadeLike, Now);

        Assert.Empty(Synergy.Current(content, WithLineup(content, start, TestCast.ElenaId, TestCast.LarsId)));

        var bonus = Assert.Single(Synergy.Current(content, WithLineup(content, start, TestCast.KadeId, TestCast.ElenaId)));
        Assert.Equal("Bond", bonus.Name);
    }

    [Fact(DisplayName = "synergy follows the player's current personality and is not stored")]
    public void Follows_the_player()
    {
        var content = TestContent.Create();
        var state = WithLineup(content, TestContent.NewGame(content), TestCast.KadeId, TestCast.LarsId);
        Assert.Empty(Synergy.Current(content, state));

        var shifted = GameStateUpdates.SetPlayerPersonality(state, KadeLike, Now);
        Assert.Single(Synergy.Current(content, shifted));
        Assert.Empty(Synergy.Current(content, state));
    }

    private static readonly AncientOrder.Rules.Personality.Personality CoversEverything = TestPersonality.Of(30, 30, 5, 5, 25, 5);

    private static SynergySettings Settings(SynergyResolution resolution, params SynergyBonusDefinition[] bonuses) =>
        new([.. bonuses], resolution);

    private static readonly SynergyBonusDefinition WellRounded =
        new("Well Rounded", SynergyRule.Coverage, 25, CombatStat.Power, StatStep.Multiply, 1.10);

    private static readonly SynergyBonusDefinition LooseBond =
        new("Loose Bond", SynergyRule.Alignment, 10, CombatStat.Speed, StatStep.AddPercent, 0.05);

    [Fact(DisplayName = "AllThatQualify returns every qualifying bonus in content order")]
    public void All_that_qualify_in_content_order()
    {
        var content = TestContent.Create(synergy: Settings(SynergyResolution.AllThatQualify, WellRounded, LooseBond));
        var state = GameStateUpdates.SetPlayerPersonality(
            WithLineup(content, TestContent.NewGame(content), TestCast.LarsId, TestCast.KadeId), CoversEverything, Now);

        var results = Synergy.Current(content, state);
        Assert.Equal(["Well Rounded", "Loose Bond"], results.Select(r => r.Name));
        Assert.Equal(165.0 / (6 * 25.0), results[0].MatchQuality);
        Assert.Equal(StatStep.AddPercent, results[1].Step);
        Assert.Equal(0.05, results[1].Value);

        var reversed = TestContent.Create(synergy: Settings(SynergyResolution.AllThatQualify, LooseBond, WellRounded));
        Assert.Equal(["Loose Bond", "Well Rounded"], Synergy.Current(reversed, state).Select(r => r.Name));
    }

    [Fact(DisplayName = "BestOnly returns only the highest match quality from the same bonuses")]
    public void Best_only_returns_one()
    {
        var content = TestContent.Create(synergy: Settings(SynergyResolution.BestOnly, LooseBond, WellRounded));
        var state = GameStateUpdates.SetPlayerPersonality(
            WithLineup(content, TestContent.NewGame(content), TestCast.LarsId, TestCast.KadeId), CoversEverything, Now);

        var bonus = Assert.Single(Synergy.Current(content, state));
        Assert.Equal("Well Rounded", bonus.Name);
    }

    [Fact(DisplayName = "content without synergy bonuses gives no bonus")]
    public void No_bonuses_defined()
    {
        var content = TestContent.Create(synergy: SynergySettings.None);
        var state = WithLineup(content, TestContent.NewGame(content), TestCast.ElenaId, TestCast.LarsId);
        Assert.Empty(Synergy.Current(content, state));
    }
}
