using AncientOrder.Rules.Common;
using AncientOrder.Rules.State;
using AncientOrder.Rules.Tests.Fixtures;
using AncientOrder.Rules.Tests.Support;

namespace AncientOrder.Rules.Tests.Characters;

// New: replaces src/state/npcs.test.ts, whose 24 tests checked the hard-coded roster data and Object.freeze behavior (see the port record).
public class CharacterCatalogTests
{
    private static Result<CharacterCatalog> Create(params CharacterDefinition[] definitions) =>
        CharacterCatalog.Create([.. definitions]);

    private static void AssertRefused(Result<CharacterCatalog> result, string detailPath)
    {
        Assert.False(result.IsOk);
        Assert.Equal(ErrorCode.VALIDATION_ERROR, result.Error.Code);
        Assert.Contains(result.Error.Details, d => d.Path == detailPath);
    }

    [Fact(DisplayName = "the test cast is a valid catalog")]
    public void Test_cast_is_valid() => Assert.True(CharacterCatalog.Create(TestCast.All).IsOk);

    [Fact(DisplayName = "an empty catalog is valid")]
    public void Empty_catalog_valid() => Assert.True(CharacterCatalog.Create([]).IsOk);

    [Fact(DisplayName = "content order is kept")]
    public void Content_order_kept()
    {
        var catalog = Create(TestCast.Kade, TestCast.Elena, TestCast.Lars).Value;
        Assert.Equal([TestCast.KadeId, TestCast.ElenaId, TestCast.LarsId], catalog.Definitions.Select(d => d.Id));
    }

    [Fact(DisplayName = "lookup by id finds each character and nothing else")]
    public void Lookup_by_id()
    {
        var catalog = Create(TestCast.Elena, TestCast.Lars, TestCast.Kade).Value;
        Assert.True(catalog.TryGet(TestCast.LarsId, out var lars));
        Assert.Equal("Lars", lars.Name);
        Assert.False(catalog.TryGet("npc_unknown_xyz", out _));
        Assert.False(catalog.TryGet(string.Empty, out _));
    }

    [Fact(DisplayName = "returns the cast as given, with no built-in characters")]
    public void No_builtin_characters()
    {
        var catalog = Create(TestCast.Elena).Value;
        Assert.Single(catalog.Definitions);
        Assert.False(catalog.TryGet(TestCast.LarsId, out _));
    }

    [Fact(DisplayName = "refuses duplicate character ids")]
    public void Refuses_duplicate_ids() =>
        AssertRefused(Create(TestCast.Elena, TestCast.Elena), "characters/1/id");

    [Fact(DisplayName = "refuses an empty id")]
    public void Refuses_empty_id() =>
        AssertRefused(Create(TestCast.Elena with { Id = string.Empty }), "characters/0/id");

    [Fact(DisplayName = "refuses an id over 64 characters and accepts exactly 64")]
    public void Refuses_long_id()
    {
        AssertRefused(Create(TestCast.Elena with { Id = new string('x', 65) }), "characters/0/id");
        Assert.True(Create(TestCast.Elena with { Id = new string('x', 64) }).IsOk);
    }

    [Fact(DisplayName = "refuses an empty name")]
    public void Refuses_empty_name() =>
        AssertRefused(Create(TestCast.Elena with { Name = string.Empty }), "characters/0/name");

    [Fact(DisplayName = "refuses an invalid fixed personality")]
    public void Refuses_invalid_personality() =>
        AssertRefused(Create(TestCast.Elena with { Personality = TestPersonality.Of(17, 17, 17, 17, 17, 17) }), "characters/0/personality");

    [Fact(DisplayName = "refuses starting affection outside -100 to 100")]
    public void Refuses_affection_out_of_range()
    {
        AssertRefused(Create(TestCast.Elena with { StartingAffection = 101 }), "characters/0/startingAffection");
        AssertRefused(Create(TestCast.Elena with { StartingAffection = -100.5 }), "characters/0/startingAffection");
        AssertRefused(Create(TestCast.Elena with { StartingAffection = double.NaN }), "characters/0/startingAffection");
        Assert.True(Create(TestCast.Elena with { StartingAffection = 100 }).IsOk);
        Assert.True(Create(TestCast.Elena with { StartingAffection = -100 }).IsOk);
    }

    [Fact(DisplayName = "refuses starting trust outside -100 to 100")]
    public void Refuses_trust_out_of_range()
    {
        AssertRefused(Create(TestCast.Elena with { StartingTrust = 100.1 }), "characters/0/startingTrust");
        AssertRefused(Create(TestCast.Elena with { StartingTrust = -101 }), "characters/0/startingTrust");
        Assert.True(Create(TestCast.Elena with { StartingTrust = -100 }).IsOk);
    }

    [Fact(DisplayName = "refuses an empty archetype id")]
    public void Refuses_empty_archetype() =>
        AssertRefused(Create(TestCast.Elena with { ArchetypeId = string.Empty }), "characters/0/archetypeId");

    [Fact(DisplayName = "reports every problem, not just the first")]
    public void Reports_every_problem()
    {
        var result = Create(TestCast.Elena with { Name = string.Empty, StartingTrust = 500 });
        Assert.False(result.IsOk);
        Assert.Equal(2, result.Error.Details.Length);
    }

    [Fact(DisplayName = "game content refuses a character whose archetype is not in the archetypes")]
    public void Content_refuses_unknown_archetype()
    {
        var result = GameContent.Create(
            TestContent.Settings,
            [TestCast.Elena with { ArchetypeId = "no_such_archetype" }],
            TestArchetypes.All);
        Assert.False(result.IsOk);
        Assert.Equal(ErrorCode.VALIDATION_ERROR, result.Error.Code);
        Assert.Contains(result.Error.Details, d => d.Path == "characters/0/archetypeId");
    }

    [Fact(DisplayName = "game content refuses invalid characters with VALIDATION_ERROR")]
    public void Content_refuses_invalid_characters()
    {
        var result = GameContent.Create(
            TestContent.Settings,
            [TestCast.Elena, TestCast.Elena],
            TestArchetypes.All);
        Assert.False(result.IsOk);
        Assert.Equal(ErrorCode.VALIDATION_ERROR, result.Error.Code);
    }

    [Fact(DisplayName = "game content holds the characters it is given in content order")]
    public void Content_holds_characters_in_order()
    {
        var content = TestContent.Create(characters: [TestCast.Lars, TestCast.Elena]);
        Assert.Equal([TestCast.LarsId, TestCast.ElenaId], content.Characters.Definitions.Select(d => d.Id));
    }

    [Fact(DisplayName = "the test cast has today's ids, personalities, archetype ids, and starting relationships")]
    public void Test_cast_matches_today()
    {
        Assert.Equal(
            ["npc_scout_elena", "npc_merchant_lars", "npc_outlaw_kade"],
            TestCast.All.Select(d => d.Id));
        Assert.Equal(
            ["elena_loyal_scout", "lars_scheming_merchant", "kade_rogue_outlaw"],
            TestCast.All.Select(d => d.ArchetypeId));
        Assert.Equal(TestPersonality.Of(20, 20, 10, 15, 20, 15), TestCast.Elena.Personality);
        Assert.Equal(TestPersonality.Of(10, 8, 28, 25, 12, 17), TestCast.Lars.Personality);
        Assert.Equal(TestPersonality.Of(12, 8, 25, 18, 10, 27), TestCast.Kade.Personality);
        Assert.Equal(-20.0, TestCast.Lars.StartingTrust);
        Assert.All(TestCast.All, d => Assert.Equal(0.0, d.StartingAffection));
    }

    [Fact(DisplayName = "a default array of characters is refused, not thrown")]
    public void Default_array_refused()
    {
        var result = CharacterCatalog.Create(default);
        Assert.False(result.IsOk);
        Assert.Equal(ErrorCode.VALIDATION_ERROR, result.Error.Code);
    }
}
