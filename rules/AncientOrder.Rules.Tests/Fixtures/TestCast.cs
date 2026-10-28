using System.Collections.Immutable;
using AncientOrder.Rules.Combat;
using AncientOrder.Rules.State;
using AncientOrder.Rules.Tests.Support;

namespace AncientOrder.Rules.Tests.Fixtures;

/// <summary>Test-only cast: Elena, Lars, Kade in that order, with today's ids, personalities, starting relationships, and encounter_demo player-party stats.</summary>
public static class TestCast
{
    public const string ElenaId = "npc_scout_elena";
    public const string LarsId = "npc_merchant_lars";
    public const string KadeId = "npc_outlaw_kade";

    public static CharacterDefinition Elena { get; } = new(
        ElenaId,
        "Elena",
        TestArchetypes.Elena,
        TestPersonality.Of(20, 20, 10, 15, 20, 15),
        0,
        0,
        new CombatProfile(3, 120, 85, 90, ElementalPath.Light));

    public static CharacterDefinition Lars { get; } = new(
        LarsId,
        "Lars",
        TestArchetypes.Lars,
        TestPersonality.Of(10, 8, 28, 25, 12, 17),
        0,
        -20,
        new CombatProfile(3, 140, 90, 70, ElementalPath.Earth));

    public static CharacterDefinition Kade { get; } = new(
        KadeId,
        "Kade",
        TestArchetypes.Kade,
        TestPersonality.Of(12, 8, 25, 18, 10, 27),
        0,
        0,
        new CombatProfile(3, 110, 100, 100, ElementalPath.Fire));

    public static ImmutableArray<CharacterDefinition> All { get; } = [Elena, Lars, Kade];
}
