using AncientOrder.Rules.Content.Packs;
using AncientOrder.Rules.Reactions;
using AncientOrder.Rules.State;

namespace AncientOrder.Rules.Tests.ReactionTesting;

public class DemeanorTests
{
    private const string Pack = """{ "format": "ancient-order-content", "version": 1, "id": "t", "name": "Test", "standIn": false, "files": ["base.json"] }""";

    private const string Even = """{ "patience": 17, "empathy": 17, "cunning": 17, "logic": 17, "kindness": 15, "charisma": 17 }""";

    private static string Base(string settings) => $$"""
        {
          "settings": { "playerName": "Kael", "lineup": { "min": 1, "max": 1 }, "startingPersonality": {{Even}}{{settings}} },
          "archetypes": [ { "id": "plain" } ],
          "characters": [ { "id": "ann", "name": "Ann", "archetypeId": "plain", "personality": {{Even}}, "startingAffection": 0, "startingTrust": 0 } ],
          "fights": []
        }
        """;

    private static Rules.Common.Result<GameContent> Load(string settings) =>
        ContentPackLoader.Load(new MemoryContentSource([KeyValuePair.Create("pack.json", Pack), KeyValuePair.Create("base.json", Base(settings))]));

    private static GameContent Content(string settings = "")
    {
        var loaded = Load(settings);
        Assert.True(loaded.IsOk, loaded.IsOk ? string.Empty : string.Join("\n", loaded.Error.Details));
        return loaded.Value;
    }

    [Theory]
    [InlineData(50, Demeanor.Warm)]
    [InlineData(50.5, Demeanor.Warm)]
    [InlineData(49.5, Demeanor.Neutral)]
    [InlineData(0, Demeanor.Neutral)]
    [InlineData(-19.5, Demeanor.Neutral)]
    [InlineData(-20, Demeanor.Wary)]
    [InlineData(-20.5, Demeanor.Wary)]
    [InlineData(-59.5, Demeanor.Wary)]
    [InlineData(-60, Demeanor.Cold)]
    [InlineData(-60.5, Demeanor.Cold)]
    [InlineData(200, Demeanor.Warm)]
    [InlineData(-200, Demeanor.Cold)]
    public void The_default_bands_hold_at_and_around_each_cutoff(double total, Demeanor expected)
    {
        var settings = Content().Settings;
        Assert.Equal(expected, DemeanorRules.Of(settings, new CharacterState(total / 2, total / 2)));
    }

    [Fact]
    public void Only_the_sum_of_affection_and_trust_counts()
    {
        var settings = Content().Settings;
        Assert.Equal(Demeanor.Warm, DemeanorRules.Of(settings, new CharacterState(100, -50)));
        Assert.Equal(Demeanor.Warm, DemeanorRules.Of(settings, new CharacterState(-50, 100)));
        Assert.Equal(Demeanor.Cold, DemeanorRules.Of(settings, new CharacterState(-100, 40)));
        Assert.Equal(Demeanor.Neutral, DemeanorRules.Of(settings, new CharacterState(100, -100)));
    }

    [Fact]
    public void The_extremes_of_the_range_are_the_warmest_and_coldest()
    {
        var settings = Content().Settings;
        Assert.Equal(Demeanor.Warm, DemeanorRules.Of(settings, new CharacterState(100, 100)));
        Assert.Equal(Demeanor.Cold, DemeanorRules.Of(settings, new CharacterState(-100, -100)));
    }

    [Theory]
    [InlineData(10, Demeanor.Warm)]
    [InlineData(9, Demeanor.Neutral)]
    [InlineData(-5, Demeanor.Wary)]
    [InlineData(-4, Demeanor.Neutral)]
    [InlineData(-10, Demeanor.Cold)]
    [InlineData(-9, Demeanor.Wary)]
    public void Content_cutoffs_replace_the_defaults(double total, Demeanor expected)
    {
        var settings = Content(""", "demeanor": { "warmAt": 10, "waryAt": -5, "coldAt": -10 }""").Settings;
        Assert.Equal(expected, DemeanorRules.Of(settings, new CharacterState(total, 0)));
    }

    [Theory]
    [InlineData(""", "demeanor": { "warmAt": 10, "waryAt": 20, "coldAt": -10 }""")]
    [InlineData(""", "demeanor": { "warmAt": 10, "waryAt": 10, "coldAt": -10 }""")]
    [InlineData(""", "demeanor": { "warmAt": 10, "waryAt": -10, "coldAt": -10 }""")]
    [InlineData(""", "demeanor": { "warmAt": -50, "waryAt": -20, "coldAt": -60 }""")]
    [InlineData(""", "demeanor": { "warmAt": 50, "waryAt": -60, "coldAt": -20 }""")]
    [InlineData(""", "demeanor": { "warmAt": 0, "waryAt": 0, "coldAt": 0 }""")]
    public void The_loader_refuses_cutoffs_out_of_order(string settings)
    {
        var loaded = Load(settings);
        Assert.False(loaded.IsOk);
        Assert.Contains(loaded.Error.Details, d => d.Message.Contains("coldAt < waryAt < warmAt", StringComparison.Ordinal));
    }

    [Fact]
    public void Cutoffs_that_are_in_order_are_accepted_even_when_close_together()
    {
        var settings = Content(""", "demeanor": { "warmAt": 1, "waryAt": 0, "coldAt": -1 }""").Settings;
        Assert.Equal(
            [Demeanor.Warm, Demeanor.Wary, Demeanor.Cold],
            [DemeanorRules.Of(settings, new CharacterState(1, 0)), DemeanorRules.Of(settings, new CharacterState(0, 0)), DemeanorRules.Of(settings, new CharacterState(-1, 0))]);
    }
}
