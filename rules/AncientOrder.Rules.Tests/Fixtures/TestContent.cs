using System.Collections.Immutable;
using AncientOrder.Rules.Combat;
using AncientOrder.Rules.Party;
using AncientOrder.Rules.State;

namespace AncientOrder.Rules.Tests.Fixtures;

/// <summary>Test GameContent: player Kael, default personality, lineup size 2, the test cast, the test archetypes, today's two synergy bonuses (best only). encounter_demo and encounter_test (T16).</summary>
public static class TestContent
{
    public const string PlayerId = "player-1";
    public const long Now = 1_700_000_000_000;

    public static GameSettings Settings { get; } = new("Kael", 2);

    public static SynergySettings Synergy { get; } = new(
        [
            new SynergyBonusDefinition("Well Rounded", SynergyRule.Coverage, 25, CombatStat.Power, StatStep.Multiply, 1.10),
            new SynergyBonusDefinition("Bond", SynergyRule.Alignment, 80, CombatStat.Speed, StatStep.Multiply, 1.10),
        ],
        SynergyResolution.BestOnly);

    public static GameContent Create(
        GameSettings? settings = null,
        ImmutableArray<CharacterDefinition>? characters = null,
        SynergySettings? synergy = null,
        ImmutableArray<EncounterDefinition>? encounters = null)
    {
        var result = GameContent.Create(
            settings ?? Settings,
            characters ?? TestCast.All,
            TestArchetypes.All,
            synergy ?? Synergy,
            encounters ?? TestEncounters.All);
        return result.IsOk ? result.Value : throw new InvalidOperationException("Test content is invalid: " + result.Error.Message);
    }

    public static GameState NewGame(GameContent content, long now = Now) => GameStateUpdates.NewGame(content, PlayerId, now);

    public static GameState NewGame(long now = Now) => NewGame(Create(), now);
}
