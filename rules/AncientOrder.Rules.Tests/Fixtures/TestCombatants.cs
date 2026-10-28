using System.Collections.Immutable;
using AncientOrder.Rules.Combat;
using AncientOrder.Rules.Common;

namespace AncientOrder.Rules.Tests.Fixtures;

/// <summary>
/// Combatant and state builders mirroring the makeCombatant / makeState helpers in the TypeScript combat tests.
/// Overrides are done with a record <c>with</c> expression on the returned combatant.
/// </summary>
public static class TestCombatants
{
    /// <summary>The fixture of energy.test.ts and elementalPaths.test.ts: id test_combatant, rank 1, power 50, speed 50.</summary>
    public static Combatant Fighter() => new(
        "test_combatant",
        "Test Fighter",
        "test",
        1.0,
        100,
        100,
        50,
        50,
        0,
        5,
        AscensionLevel.Level0,
        [],
        ElementalPath.Fire,
        new ReactionSkills(new DefenseSkill(0.5, 0.4, 0.2), new DefenseSkill(0.5, 0.8, 0.2), new DefenseSkill(0.5, 0.9, 0.1)),
        null,
        false);

    /// <summary>The fixture of the behavior-tree tests: rank 3, power 20, speed 10, stamina 100/100, Fire. The name is the id.</summary>
    public static Combatant Make(string id, string? archetype = "test_archetype") => new(
        id,
        id,
        archetype,
        3.0,
        100,
        100,
        20,
        10,
        0,
        5,
        AscensionLevel.Level0,
        [],
        ElementalPath.Fire,
        new ReactionSkills(new DefenseSkill(0.5, 0.3, 0.1), new DefenseSkill(0.4, 0.8, 0.15), new DefenseSkill(0.3, 0.9, 0.1)),
        null,
        false);

    /// <summary>The player fixture of reactionProgression.test.ts: all reactions at zero XP, rank-1 rates.</summary>
    public static Combatant Progressing() => Fighter() with
    {
        Id = "player_1",
        Name = "Hero",
        ArchetypeId = "scout",
        Rank = 2,
        Power = 10,
        Speed = 10,
        MaxEnergy = 3,
        ReactionProgress = ReactionProgression.Zero,
        ReactionSkills = ReactionProgression.ReactionSkillsFromProgress(ReactionProgression.Zero),
    };

    public static Combatant WithProgress(Combatant combatant, ReactionProgress progress) => combatant with
    {
        ReactionProgress = progress,
        ReactionSkills = ReactionProgression.ReactionSkillsFromProgress(progress),
    };

    public static Combatant Ko(Combatant combatant) => combatant with { Stamina = 0, IsKO = true };

    public static CombatState State(Combatant[] playerParty, Combatant[] enemyParty, int round = 1) => new(
        round,
        CombatPhase.AiDecision,
        [.. playerParty],
        [.. enemyParty],
        [],
        [],
        CombatStatus.Active,
        DiceState.FromSeed(1));
}
