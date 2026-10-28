using System.Collections.Immutable;
using AncientOrder.Rules.Combat;
using AncientOrder.Rules.Party;
using AncientOrder.Rules.Tests.Fixtures;
using AncientOrder.Rules.Tests.Support;
using Persona = AncientOrder.Rules.Personality.Personality;

namespace AncientOrder.Rules.Tests.Party;

// Carried over from src/narrative/synergyCalculator.test.ts, against the test content's two bonuses.
// evaluateWellRounded / evaluateBond / calculateSynergy all become SynergyCalculator.Evaluate; a TypeScript null is an empty result.
internal static class SynergyTestData
{
    public static readonly Persona Elena = TestCast.Elena.Personality;
    public static readonly Persona Lars = TestCast.Lars.Personality;
    public static readonly Persona Kade = TestCast.Kade.Personality;

    public static readonly SynergyBonusDefinition WellRounded =
        new("Well Rounded", SynergyRule.Coverage, 25, CombatStat.Power, StatStep.Multiply, 1.10);

    public static readonly SynergyBonusDefinition Bond =
        new("Bond", SynergyRule.Alignment, 80, CombatStat.Speed, StatStep.Multiply, 1.10);

    public static SynergySettings Only(SynergyBonusDefinition bonus) => new([bonus], SynergyResolution.BestOnly);

    public static ImmutableArray<Persona> Companions(params Persona[] personalities) => [.. personalities];
}

public class EvaluateWellRounded
{
    private static readonly SynergySettings Settings = SynergyTestData.Only(SynergyTestData.WellRounded);

    [Fact(DisplayName = "returns null when a trait max is below threshold across all personalities")]
    public void Below_threshold()
    {
        var player = TestPersonality.Of(30, 8, 25, 25, 25, 25);
        var result = SynergyCalculator.Evaluate(Settings, player, SynergyTestData.Companions(SynergyTestData.Lars, SynergyTestData.Kade));
        Assert.Empty(result);
    }

    [Fact(DisplayName = "returns SynergyBonus when all traits have max >= threshold across party")]
    public void All_covered()
    {
        var player = TestPersonality.Of(25, 25, 25, 25, 25, 25);
        var result = SynergyCalculator.Evaluate(
            Settings, player, SynergyTestData.Companions(SynergyTestData.Elena, SynergyTestData.Lars, SynergyTestData.Kade));
        var bonus = Assert.Single(result);
        Assert.Equal(CombatStat.Power, bonus.Stat);
        Assert.Equal(StatStep.Multiply, bonus.Step);
        Assert.Equal(1.10, bonus.Value);
        Assert.Equal("Well Rounded", bonus.Name);
    }

    [Fact(DisplayName = "calculates correct matchQuality")]
    public void Match_quality()
    {
        var player = TestPersonality.Of(25, 25, 25, 25, 25, 25);
        var bonus = Assert.Single(SynergyCalculator.Evaluate(Settings, player, []));
        Assert.Equal(1.0, bonus.MatchQuality, 5);
    }

    [Fact(DisplayName = "uses maximum across all personalities per trait")]
    public void Max_per_trait()
    {
        var player = TestPersonality.Of(25, 25, 10, 25, 25, 25);
        Assert.Single(SynergyCalculator.Evaluate(Settings, player, SynergyTestData.Companions(SynergyTestData.Lars)));
    }

    [Fact(DisplayName = "works with empty partyNpcPersonalities (player alone must cover all traits)")]
    public void Player_alone()
    {
        var player = TestPersonality.Of(25, 25, 25, 25, 25, 25);
        Assert.Single(SynergyCalculator.Evaluate(Settings, player, []));
    }

    [Fact(DisplayName = "returns null when single player cannot cover all traits alone")]
    public void Player_alone_cannot_cover()
    {
        var player = TestPersonality.Of(30, 10, 30, 30, 30, 30);
        Assert.Empty(SynergyCalculator.Evaluate(Settings, player, []));
    }
}

public class EvaluateBond
{
    private static readonly SynergySettings Settings = SynergyTestData.Only(SynergyTestData.Bond);

    [Fact(DisplayName = "returns null when partyNpcPersonalities is empty")]
    public void No_companions()
    {
        var player = TestPersonality.Of(20, 20, 10, 15, 20, 15);
        Assert.Empty(SynergyCalculator.Evaluate(Settings, player, []));
    }

    [Fact(DisplayName = "returns SynergyBonus when player aligns >= 80% with an NPC")]
    public void Aligned()
    {
        var player = TestPersonality.Of(20, 20, 10, 15, 20, 15);
        var bonus = Assert.Single(SynergyCalculator.Evaluate(Settings, player, SynergyTestData.Companions(SynergyTestData.Elena)));
        Assert.Equal(CombatStat.Speed, bonus.Stat);
        Assert.Equal(StatStep.Multiply, bonus.Step);
        Assert.Equal(1.10, bonus.Value);
    }

    [Fact(DisplayName = "returns null when player alignment is below 80% threshold")]
    public void Below_threshold()
    {
        var player = TestPersonality.Of(5, 5, 35, 35, 10, 10);
        Assert.Empty(SynergyCalculator.Evaluate(Settings, player, SynergyTestData.Companions(SynergyTestData.Elena)));
    }

    [Fact(DisplayName = "uses bestRatio across all NPCs (takes highest)")]
    public void Best_ratio()
    {
        var player = TestPersonality.Of(10, 8, 28, 15, 10, 29);
        Assert.Single(SynergyCalculator.Evaluate(
            Settings, player, SynergyTestData.Companions(SynergyTestData.Elena, SynergyTestData.Lars, SynergyTestData.Kade)));
    }

    [Fact(DisplayName = "division-by-zero guard: skips NPC when npcDominantSum === 0")]
    public void Division_by_zero_guard()
    {
        var player = TestPersonality.Of(20, 20, 10, 15, 20, 15);
        var zeroNpc = TestPersonality.Of(0, 0, 0, 0, 0, 0);
        Assert.Empty(SynergyCalculator.Evaluate(Settings, player, SynergyTestData.Companions(zeroNpc)));
    }

    [Fact(DisplayName = "returns correct matchQuality (bestRatio)")]
    public void Match_quality()
    {
        var bonus = Assert.Single(SynergyCalculator.Evaluate(
            Settings, SynergyTestData.Elena, SynergyTestData.Companions(SynergyTestData.Elena)));
        Assert.Equal(1.0, bonus.MatchQuality, 5);
    }
}

public class CalculateSynergy
{
    [Fact(DisplayName = "returns null when no paradigms are provided")]
    public void No_paradigms()
    {
        var player = TestPersonality.Of(25, 25, 25, 25, 25, 25);
        Assert.Empty(SynergyCalculator.Evaluate(SynergySettings.None, player, SynergyTestData.Companions(SynergyTestData.Elena)));
    }

    [Fact(DisplayName = "returns null when no paradigms are satisfied")]
    public void None_satisfied()
    {
        var player = TestPersonality.Of(10, 8, 10, 10, 10, 10);
        Assert.Empty(SynergyCalculator.Evaluate(
            TestContent.Synergy, player, SynergyTestData.Companions(SynergyTestData.Lars, SynergyTestData.Kade)));
    }

    [Fact(DisplayName = "returns Well Rounded bonus when only that paradigm is satisfied")]
    public void Only_well_rounded()
    {
        var player = TestPersonality.Of(25, 25, 25, 25, 25, 25);
        var bonus = Assert.Single(SynergyCalculator.Evaluate(SynergyTestData.Only(SynergyTestData.WellRounded), player, []));
        Assert.Equal("Well Rounded", bonus.Name);
        Assert.Equal(CombatStat.Power, bonus.Stat);
    }

    [Fact(DisplayName = "returns Bond bonus when only that paradigm is satisfied")]
    public void Only_bond()
    {
        var bonus = Assert.Single(SynergyCalculator.Evaluate(
            SynergyTestData.Only(SynergyTestData.Bond), SynergyTestData.Elena, SynergyTestData.Companions(SynergyTestData.Elena)));
        Assert.Equal("Bond", bonus.Name);
        Assert.Equal(CombatStat.Speed, bonus.Stat);
    }

    [Fact(DisplayName = "returns the higher-quality result when both paradigms are satisfied")]
    public void Higher_quality_wins()
    {
        var player = TestPersonality.Of(30, 30, 30, 30, 30, 30);
        var result = SynergyCalculator.Evaluate(
            TestContent.Synergy,
            player,
            SynergyTestData.Companions(SynergyTestData.Elena, SynergyTestData.Lars, SynergyTestData.Kade));
        var bonus = Assert.Single(result);
        // Both qualify here: Well Rounded scores 180/150 = 1.2, Bond scores 60/40 = 1.5 against Elena's top two traits.
        Assert.Equal("Bond", bonus.Name);
        Assert.Equal(60.0 / 40.0, bonus.MatchQuality);
    }

    [Fact(DisplayName = "tiebreak: well_rounded wins over bond on equal matchQuality")]
    public void Tie_goes_to_first_in_content_order()
    {
        var player = TestPersonality.Of(25, 25, 25, 25, 25, 25);
        var wrConfig = new SynergyBonusDefinition("Well Rounded Tie", SynergyRule.Coverage, 25, CombatStat.Power, StatStep.Multiply, 1.10);
        var bondConfig = new SynergyBonusDefinition("Bond Tie", SynergyRule.Alignment, 0, CombatStat.Speed, StatStep.Multiply, 1.10);

        var result = SynergyCalculator.Evaluate(
            new SynergySettings([wrConfig, bondConfig], SynergyResolution.BestOnly), player, SynergyTestData.Companions(player));
        var bonus = Assert.Single(result);
        Assert.Equal("Well Rounded Tie", bonus.Name);
    }

    [Fact(DisplayName = "uses DEFAULT_PARADIGMS correctly with standard party (Elena, Lars, Kade)")]
    public void Default_paradigms_with_standard_party()
    {
        var player = TestPersonality.Of(25, 25, 25, 25, 25, 25);
        var result = SynergyCalculator.Evaluate(
            TestContent.Synergy,
            player,
            SynergyTestData.Companions(SynergyTestData.Elena, SynergyTestData.Lars, SynergyTestData.Kade));
        Assert.Single(result);
    }
}
