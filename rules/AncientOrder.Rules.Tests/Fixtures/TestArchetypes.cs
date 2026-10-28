using System.Collections.Immutable;
using AncientOrder.Rules.Combat;
using AncientOrder.Rules.Combat.Ai;

namespace AncientOrder.Rules.Tests.Fixtures;

/// <summary>Test-only archetypes: today's three scored profiles plus today's unmatched archetypes declared basic.</summary>
public static class TestArchetypes
{
    public const string Elena = "elena_loyal_scout";
    public const string Lars = "lars_scheming_merchant";
    public const string Kade = "kade_rogue_outlaw";

    public static ArchetypeDefinition ElenaLoyalScout { get; } = new(Elena, new ArchetypeBehavior.Scored(Profile(
        0.3, 0.5, 0.3, 0.4, 0.2,
        1.0, 1.8, 0.5, 1.2, 0.3, 0.8, 1.4)));

    public static ArchetypeDefinition LarsSchemingMerchant { get; } = new(Lars, new ArchetypeBehavior.Scored(Profile(
        0.4, 0.5, 0.4, 0.3, 0.2,
        1.5, 1.0, 0.8, 1.4, 0.6, 1.2, 1.0)));

    public static ArchetypeDefinition KadeRogueOutlaw { get; } = new(Kade, new ArchetypeBehavior.Scored(Profile(
        0.6, 0.2, 0.3, 0.4, 0.1,
        0.6, 0.4, 1.6, 1.0, 1.5, 0.5, 0.6)));

    public static ImmutableArray<ArchetypeDefinition> All { get; } =
    [
        ElenaLoyalScout,
        LarsSchemingMerchant,
        KadeRogueOutlaw,
        Basic("enemy"),
        Basic("kade_bold_outlaw"),
        Basic("lars_cautious_merchant"),
        Basic("warrior"),
        Basic("scout"),
        Basic("mage"),
    ];

    public static ArchetypeDefinition Basic(string id) => new(id, new ArchetypeBehavior.Basic());

    private static DecisionProfile Profile(
        double attack, double defend, double evade, double special, double group,
        double ownStamina, double allyInDanger, double targetVulnerability, double energyAvailability,
        double speedAdvantage, double roundPhase, double teamBalance) =>
        new(
            new Dictionary<ActionType, double>
            {
                [ActionType.Attack] = attack,
                [ActionType.Defend] = defend,
                [ActionType.Evade] = evade,
                [ActionType.Special] = special,
                [ActionType.Group] = group,
            }.ToImmutableDictionary(),
            new Dictionary<string, double>
            {
                ["ownStamina"] = ownStamina,
                ["allyInDanger"] = allyInDanger,
                ["targetVulnerability"] = targetVulnerability,
                ["energyAvailability"] = energyAvailability,
                ["speedAdvantage"] = speedAdvantage,
                ["roundPhase"] = roundPhase,
                ["teamBalance"] = teamBalance,
            }.ToImmutableDictionary(StringComparer.Ordinal));
}
