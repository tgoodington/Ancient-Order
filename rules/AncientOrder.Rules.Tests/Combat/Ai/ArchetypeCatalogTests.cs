using System.Collections.Immutable;
using AncientOrder.Rules.Combat;
using AncientOrder.Rules.Combat.Ai;
using AncientOrder.Rules.Common;
using AncientOrder.Rules.State;
using AncientOrder.Rules.Tests.Fixtures;

namespace AncientOrder.Rules.Tests.Combat.Ai;

// Replaces src/combat/behaviorTree/profiles/profiles.test.ts. The profiles are now the test archetypes (TestArchetypes), so the structure and
// value tests run against them. Dropped: every elementalPath assertion (the profile no longer carries a path; tie-breaks use the combatant's own).
internal static class ArchetypeFixtures
{
    public static DecisionProfile ProfileOf(ArchetypeDefinition definition) => ((ArchetypeBehavior.Scored)definition.Behavior).Profile;

    public static DecisionProfile Elena { get; } = ProfileOf(TestArchetypes.ElenaLoyalScout);

    public static DecisionProfile Lars { get; } = ProfileOf(TestArchetypes.LarsSchemingMerchant);

    public static DecisionProfile Kade { get; } = ProfileOf(TestArchetypes.KadeRogueOutlaw);

    public static readonly string[] ExpectedFactors =
        ["ownStamina", "allyInDanger", "targetVulnerability", "energyAvailability", "speedAdvantage", "roundPhase", "teamBalance"];

    public static DecisionProfile Full(double score = 0.5, double weight = 1.0) => new(
        Enum.GetValues<ActionType>().ToImmutableDictionary(t => t, _ => score),
        ExpectedFactors.ToImmutableDictionary(f => f, _ => weight, StringComparer.Ordinal));

    public static ArchetypeDefinition Scored(string id, DecisionProfile profile) => new(id, new ArchetypeBehavior.Scored(profile));
}

public class ArchetypeProfilesStructure
{
    private static void AssertAllActionTypes(DecisionProfile profile)
    {
        foreach (var type in Enum.GetValues<ActionType>())
        {
            Assert.True(profile.BaseScores.ContainsKey(type));
        }

        Assert.Equal(5, profile.BaseScores.Count);
    }

    private static void AssertAllFactors(DecisionProfile profile)
    {
        foreach (var factor in ArchetypeFixtures.ExpectedFactors)
        {
            Assert.True(profile.FactorWeights.ContainsKey(factor));
        }

        Assert.Equal(7, profile.FactorWeights.Count);
    }

    [Fact(DisplayName = "Elena has all 5 action types in baseScores")]
    public void Elena_base_scores() => AssertAllActionTypes(ArchetypeFixtures.Elena);

    [Fact(DisplayName = "Lars has all 5 action types in baseScores")]
    public void Lars_base_scores() => AssertAllActionTypes(ArchetypeFixtures.Lars);

    [Fact(DisplayName = "Kade has all 5 action types in baseScores")]
    public void Kade_base_scores() => AssertAllActionTypes(ArchetypeFixtures.Kade);

    [Fact(DisplayName = "Elena has weights for all 7 factors")]
    public void Elena_weights() => AssertAllFactors(ArchetypeFixtures.Elena);

    [Fact(DisplayName = "Lars has weights for all 7 factors")]
    public void Lars_weights() => AssertAllFactors(ArchetypeFixtures.Lars);

    [Fact(DisplayName = "Kade has weights for all 7 factors")]
    public void Kade_weights() => AssertAllFactors(ArchetypeFixtures.Kade);
}

public class ArchetypeProfileValues
{
    public class ElenaLoyalScout
    {
        private static readonly DecisionProfile P = ArchetypeFixtures.Elena;

        [Fact(DisplayName = "has high allyInDanger weight (1.8 — most protective)")]
        public void Ally_in_danger() => Assert.Equal(1.8, P.FactorWeights["allyInDanger"]);

        [Fact(DisplayName = "has low speedAdvantage weight (0.3 — not aggressive)")]
        public void Speed_advantage() => Assert.Equal(0.3, P.FactorWeights["speedAdvantage"]);

        [Fact(DisplayName = "DEFEND base score higher than ATTACK base score")]
        public void Defend_over_attack() => Assert.True(P.BaseScores[ActionType.Defend] > P.BaseScores[ActionType.Attack]);

        [Fact(DisplayName = "exact base scores from spec")]
        public void Base_scores()
        {
            Assert.Equal(0.3, P.BaseScores[ActionType.Attack]);
            Assert.Equal(0.5, P.BaseScores[ActionType.Defend]);
            Assert.Equal(0.3, P.BaseScores[ActionType.Evade]);
            Assert.Equal(0.4, P.BaseScores[ActionType.Special]);
            Assert.Equal(0.2, P.BaseScores[ActionType.Group]);
        }
    }

    public class LarsSchemingMerchant
    {
        private static readonly DecisionProfile P = ArchetypeFixtures.Lars;

        [Fact(DisplayName = "has high ownStamina weight (1.5 — self-preserving)")]
        public void Own_stamina() => Assert.Equal(1.5, P.FactorWeights["ownStamina"]);

        [Fact(DisplayName = "has high energyAvailability weight (1.4 — careful resource management)")]
        public void Energy_availability() => Assert.Equal(1.4, P.FactorWeights["energyAvailability"]);

        [Fact(DisplayName = "exact base scores from spec")]
        public void Base_scores()
        {
            Assert.Equal(0.4, P.BaseScores[ActionType.Attack]);
            Assert.Equal(0.5, P.BaseScores[ActionType.Defend]);
            Assert.Equal(0.4, P.BaseScores[ActionType.Evade]);
            Assert.Equal(0.3, P.BaseScores[ActionType.Special]);
            Assert.Equal(0.2, P.BaseScores[ActionType.Group]);
        }
    }

    public class KadeRogueOutlaw
    {
        private static readonly DecisionProfile P = ArchetypeFixtures.Kade;

        [Fact(DisplayName = "has high targetVulnerability weight (1.6 — opportunist)")]
        public void Target_vulnerability() => Assert.Equal(1.6, P.FactorWeights["targetVulnerability"]);

        [Fact(DisplayName = "has high speedAdvantage weight (1.5 — exploits slow targets)")]
        public void Speed_advantage() => Assert.Equal(1.5, P.FactorWeights["speedAdvantage"]);

        [Fact(DisplayName = "has low allyInDanger weight (0.4 — ignores team)")]
        public void Ally_in_danger() => Assert.Equal(0.4, P.FactorWeights["allyInDanger"]);

        [Fact(DisplayName = "ATTACK base score is highest of all 3 archetypes")]
        public void Attack_highest()
        {
            Assert.True(P.BaseScores[ActionType.Attack] > ArchetypeFixtures.Elena.BaseScores[ActionType.Attack]);
            Assert.True(P.BaseScores[ActionType.Attack] > ArchetypeFixtures.Lars.BaseScores[ActionType.Attack]);
        }

        [Fact(DisplayName = "DEFEND base score is lowest (aggressive, not defensive)")]
        public void Defend_lowest()
        {
            Assert.True(P.BaseScores[ActionType.Defend] < ArchetypeFixtures.Elena.BaseScores[ActionType.Defend]);
            Assert.True(P.BaseScores[ActionType.Defend] < ArchetypeFixtures.Lars.BaseScores[ActionType.Defend]);
        }

        [Fact(DisplayName = "exact base scores from spec")]
        public void Base_scores()
        {
            Assert.Equal(0.6, P.BaseScores[ActionType.Attack]);
            Assert.Equal(0.2, P.BaseScores[ActionType.Defend]);
            Assert.Equal(0.3, P.BaseScores[ActionType.Evade]);
            Assert.Equal(0.4, P.BaseScores[ActionType.Special]);
            Assert.Equal(0.1, P.BaseScores[ActionType.Group]);
        }
    }
}

// getProfile(id) becomes ArchetypeCatalog.TryGet; "undefined" becomes false.
public class ProfileRegistry
{
    private static readonly ArchetypeCatalog Catalog = ArchetypeCatalog.Create(TestArchetypes.All).Value;

    [Fact(DisplayName = "retrieves Elena by archetype ID")]
    public void Elena()
    {
        Assert.True(Catalog.TryGet("elena_loyal_scout", out var found));
        Assert.IsType<ArchetypeBehavior.Scored>(found.Behavior);
    }

    [Fact(DisplayName = "retrieves Lars by archetype ID")]
    public void Lars()
    {
        Assert.True(Catalog.TryGet("lars_scheming_merchant", out var found));
        Assert.IsType<ArchetypeBehavior.Scored>(found.Behavior);
    }

    [Fact(DisplayName = "retrieves Kade by archetype ID")]
    public void Kade()
    {
        Assert.True(Catalog.TryGet("kade_rogue_outlaw", out var found));
        Assert.IsType<ArchetypeBehavior.Scored>(found.Behavior);
    }

    [Fact(DisplayName = "returns undefined for unknown archetype ID")]
    public void Unknown() => Assert.False(Catalog.TryGet("unknown_archetype", out _));

    [Fact(DisplayName = "all 3 profiles retrievable")]
    public void All_three()
    {
        ImmutableArray<ArchetypeDefinition> three = [TestArchetypes.ElenaLoyalScout, TestArchetypes.LarsSchemingMerchant, TestArchetypes.KadeRogueOutlaw];
        Assert.Equal(3, three.Length);
        foreach (var definition in three)
        {
            Assert.True(Catalog.TryGet(definition.Id, out var found));
            Assert.Same(definition, found);
        }
    }
}

// New tests (T15): the archetype content rules (validation refusals), the Basic archetypes in the test content, and lookup.
public class ArchetypeContentRules
{
    private static Result<ArchetypeCatalog> Create(params ArchetypeDefinition[] definitions) => ArchetypeCatalog.Create([.. definitions]);

    private static void AssertRefused(Result<ArchetypeCatalog> result, string path)
    {
        Assert.False(result.IsOk);
        Assert.Equal(ErrorCode.VALIDATION_ERROR, result.Error.Code);
        Assert.Contains(result.Error.Details, d => d.Path == path);
    }

    [Fact(DisplayName = "the test archetypes make a valid catalog in content order")]
    public void Test_archetypes_valid()
    {
        var result = ArchetypeCatalog.Create(TestArchetypes.All);
        Assert.True(result.IsOk);
        Assert.Equal(TestArchetypes.All, result.Value.Definitions);
    }

    [Fact(DisplayName = "the test archetypes keep today's three scored profiles and declare the unmatched ids basic")]
    public void Test_archetype_kinds()
    {
        var catalog = ArchetypeCatalog.Create(TestArchetypes.All).Value;
        foreach (var id in new[] { TestArchetypes.Elena, TestArchetypes.Lars, TestArchetypes.Kade })
        {
            Assert.True(catalog.TryGet(id, out var scored));
            Assert.IsType<ArchetypeBehavior.Scored>(scored.Behavior);
        }

        foreach (var id in new[] { "enemy", "kade_bold_outlaw", "lars_cautious_merchant", "warrior", "scout", "mage" })
        {
            Assert.True(catalog.TryGet(id, out var basic));
            Assert.IsType<ArchetypeBehavior.Basic>(basic.Behavior);
        }
    }

    [Fact(DisplayName = "an empty catalog is valid")]
    public void Empty_valid() => Assert.True(Create().IsOk);

    [Fact(DisplayName = "a basic archetype needs no profile")]
    public void Basic_needs_no_profile() => Assert.True(Create(TestArchetypes.Basic("grunt")).IsOk);

    [Fact(DisplayName = "refuses a duplicate archetype id")]
    public void Refuses_duplicate() =>
        AssertRefused(Create(TestArchetypes.Basic("grunt"), TestArchetypes.Basic("grunt")), "archetypes/1/id");

    [Fact(DisplayName = "refuses an empty archetype id")]
    public void Refuses_empty_id() => AssertRefused(Create(TestArchetypes.Basic(string.Empty)), "archetypes/0/id");

    [Fact(DisplayName = "refuses a scored archetype with a missing base score")]
    public void Refuses_missing_base_score()
    {
        var profile = ArchetypeFixtures.Full();
        profile = profile with { BaseScores = profile.BaseScores.Remove(ActionType.Group) };
        AssertRefused(Create(ArchetypeFixtures.Scored("a", profile)), "archetypes/0/profile/baseScores/Group");
    }

    [Fact(DisplayName = "refuses a scored archetype with a missing factor weight")]
    public void Refuses_missing_weight()
    {
        var profile = ArchetypeFixtures.Full();
        profile = profile with { FactorWeights = profile.FactorWeights.Remove("teamBalance") };
        AssertRefused(Create(ArchetypeFixtures.Scored("a", profile)), "archetypes/0/profile/factorWeights/teamBalance");
    }

    [Fact(DisplayName = "refuses a scored archetype with no base scores or weights at all")]
    public void Refuses_empty_profile()
    {
        var empty = new DecisionProfile(ImmutableDictionary<ActionType, double>.Empty, ImmutableDictionary<string, double>.Empty);
        var result = Create(ArchetypeFixtures.Scored("a", empty));
        Assert.False(result.IsOk);
        Assert.Equal(12, result.Error.Details.Length);
    }

    [Fact(DisplayName = "refuses a scored archetype with a non-finite base score or weight")]
    public void Refuses_non_finite()
    {
        var profile = ArchetypeFixtures.Full();
        profile = profile with
        {
            BaseScores = profile.BaseScores.SetItem(ActionType.Attack, double.NaN),
            FactorWeights = profile.FactorWeights.SetItem("ownStamina", double.PositiveInfinity),
        };
        var result = Create(ArchetypeFixtures.Scored("a", profile));
        AssertRefused(result, "archetypes/0/profile/baseScores/Attack");
        AssertRefused(result, "archetypes/0/profile/factorWeights/ownStamina");
    }

    [Fact(DisplayName = "refuses a scored archetype with no profile")]
    public void Refuses_null_profile() => AssertRefused(Create(ArchetypeFixtures.Scored("a", null!)), "archetypes/0/profile");

    [Fact(DisplayName = "refuses a default array of archetypes")]
    public void Refuses_default_array() => Assert.False(ArchetypeCatalog.Create(default).IsOk);

    [Fact(DisplayName = "reports every problem, not just the first")]
    public void Reports_every_problem()
    {
        var result = Create(TestArchetypes.Basic("x"), TestArchetypes.Basic("x"), TestArchetypes.Basic(string.Empty));
        Assert.Equal(2, result.Error.Details.Length);
    }

    [Fact(DisplayName = "TryGet returns false for a null id")]
    public void Try_get_null() => Assert.False(ArchetypeCatalog.Create(TestArchetypes.All).Value.TryGet(null, out _));

    [Fact(DisplayName = "game content refuses a character naming an unknown archetype")]
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

    [Fact(DisplayName = "game content refuses an invalid archetype list")]
    public void Content_refuses_invalid_archetypes()
    {
        var result = GameContent.Create(
            TestContent.Settings,
            TestCast.All,
            [TestArchetypes.Basic("dup"), TestArchetypes.Basic("dup")]);
        Assert.False(result.IsOk);
        Assert.Contains(result.Error.Details, d => d.Path == "archetypes/1/id");
    }
}
