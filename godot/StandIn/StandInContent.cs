// STAND-IN CONTENT. Test material that only proves the Godot project is wired to the rules (ADR-062).
// It is not a creative draft. Sprint 7's content packs replace it.
using System.Collections.Immutable;
using AncientOrder.Rules.Combat;
using AncientOrder.Rules.Combat.Ai;
using AncientOrder.Rules.Party;
using AncientOrder.Rules.State;

namespace AncientOrder;

public static class StandInContent
{
    public const string CompanionAId = "standin_companion_a";
    public const string CompanionBId = "standin_companion_b";
    public const string ScoredArchetypeId = "standin_archetype_scored";
    public const string BasicArchetypeId = "standin_archetype_basic";
    public const string EncounterId = "standin_encounter";
    public const string EnemyId = "standin_enemy";

    public static GameContent Create()
    {
        var settings = new GameSettings(
            "STAND-IN Player",
            2,
            PlayerCombat: new CombatProfile(3, 120, 80, 80, ElementalPath.Air));

        ImmutableArray<CharacterDefinition> characters =
        [
            new(CompanionAId, "STAND-IN Companion A", ScoredArchetypeId, StandInPersonality(20, 20, 10, 15, 20, 15), 0, 0,
                new CombatProfile(3, 120, 85, 90, ElementalPath.Light)),
            new(CompanionBId, "STAND-IN Companion B", BasicArchetypeId, StandInPersonality(12, 8, 25, 18, 10, 27), 0, 0,
                new CombatProfile(3, 110, 100, 100, ElementalPath.Fire)),
        ];

        ImmutableArray<ArchetypeDefinition> archetypes =
        [
            new(ScoredArchetypeId, new ArchetypeBehavior.Scored(Profile())),
            new(BasicArchetypeId, new ArchetypeBehavior.Basic()),
        ];

        var synergy = new SynergySettings(
            [
                new SynergyBonusDefinition("STAND-IN Well Rounded", SynergyRule.Coverage, 25, CombatStat.Power, StatStep.Multiply, 1.10),
                new SynergyBonusDefinition("STAND-IN Bond", SynergyRule.Alignment, 80, CombatStat.Speed, StatStep.Multiply, 1.10),
            ],
            SynergyResolution.BestOnly);

        ImmutableArray<EncounterDefinition> encounters =
        [
            new(EncounterId, "STAND-IN Encounter", [
                new CombatantConfig(
                    EnemyId,
                    "STAND-IN Enemy",
                    BasicArchetypeId,
                    3,
                    400,
                    40,
                    60,
                    ElementalPath.Earth,
                    new ReactionSkills(new DefenseSkill(0.5, 0.3, 0.1), new DefenseSkill(0.4, 0.8, 0.15), new DefenseSkill(0.3, 0.9, 0.1))),
            ]),
        ];

        var result = GameContent.Create(settings, characters, archetypes, synergy, encounters);
        return result.IsOk
            ? result.Value
            : throw new InvalidOperationException("STAND-IN content is invalid: " + result.Error.Message);
    }

    private static AncientOrder.Rules.Personality.Personality StandInPersonality(double a, double b, double c, double d, double e, double f) =>
        new()
        {
            Patience = a,
            Empathy = b,
            Cunning = c,
            Logic = d,
            Kindness = e,
            Charisma = f,
        };

    private static DecisionProfile Profile() =>
        new(
            new Dictionary<ActionType, double>
            {
                [ActionType.Attack] = 0.3,
                [ActionType.Defend] = 0.5,
                [ActionType.Evade] = 0.3,
                [ActionType.Special] = 0.4,
                [ActionType.Group] = 0.2,
            }.ToImmutableDictionary(),
            new Dictionary<string, double>
            {
                ["ownStamina"] = 1.0,
                ["allyInDanger"] = 1.8,
                ["targetVulnerability"] = 0.5,
                ["energyAvailability"] = 1.2,
                ["speedAdvantage"] = 0.3,
                ["roundPhase"] = 0.8,
                ["teamBalance"] = 1.4,
            }.ToImmutableDictionary(StringComparer.Ordinal));
}
