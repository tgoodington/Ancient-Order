using AncientOrder.Rules.Combat;
using AncientOrder.Rules.Tests.Fixtures;

namespace AncientOrder.Rules.Tests.Combat;

// Carried over from src/combat/reactionProgression.test.ts. Reactions are the ReactionKind enum; a combatant with no reaction progress
// has ReactionProgress null (the TypeScript field was undefined). toEqual on a rates object becomes record equality.
public class XpCurveConstants
{
    [Fact(DisplayName = "matches the decoded Reaction Progression & Log thresholds")]
    public void Matches_thresholds()
    {
        Assert.Equal([0, 100, 215, 345, 490, 650, 825, 1015, 1220, 1440, 1675], CombatConstants.XpThresholds);
        Assert.Equal(11, CombatConstants.MaxReactionRank);
        Assert.Equal(1675, CombatConstants.MaxReactionXp);
    }

    [Fact(DisplayName = "has a step that grows by +15 each rank")]
    public void Step_grows()
    {
        var t = CombatConstants.XpThresholds;
        var steps = t.Skip(1).Select((v, i) => v - t[i]).ToArray();
        Assert.Equal([100, 115, 130, 145, 160, 175, 190, 205, 220, 235], steps);
    }
}

public class RankFromXp
{
    [Fact(DisplayName = "returns rank 1 at and below the first threshold")]
    public void Rank_1()
    {
        Assert.Equal(1, ReactionProgression.RankFromXp(0));
        Assert.Equal(1, ReactionProgression.RankFromXp(99));
    }

    [Fact(DisplayName = "advances exactly at each cumulative threshold")]
    public void Advances_at_thresholds()
    {
        Assert.Equal(2, ReactionProgression.RankFromXp(100));
        Assert.Equal(2, ReactionProgression.RankFromXp(214));
        Assert.Equal(3, ReactionProgression.RankFromXp(215));
        Assert.Equal(10, ReactionProgression.RankFromXp(1674));
        Assert.Equal(11, ReactionProgression.RankFromXp(1675));
    }

    [Fact(DisplayName = "caps at rank 11 for XP beyond the table")]
    public void Caps_at_11() => Assert.Equal(11, ReactionProgression.RankFromXp(99_999));
}

public class RatesForRank
{
    [Fact(DisplayName = "returns the band floors at rank 1")]
    public void Band_floors()
    {
        Assert.Equal(new DefenseSkill(0.4, 0.55, 0.4), ReactionProgression.RatesForRank(ReactionKind.Block, 1));
        Assert.Equal(new DefenseSkill(0.3, 0.8, 0.1), ReactionProgression.RatesForRank(ReactionKind.Dodge, 1));
        Assert.Equal(new DefenseSkill(0.1, 0.9, 0.0), ReactionProgression.RatesForRank(ReactionKind.Parry, 1));
    }

    [Fact(DisplayName = "reaches the band ceilings at rank 11 (matches ADR-054 SMR column)")]
    public void Band_ceilings()
    {
        Assert.Equal(0.8, ReactionProgression.RatesForRank(ReactionKind.Block, 11).SMR, 5);
        Assert.Equal(0.9, ReactionProgression.RatesForRank(ReactionKind.Dodge, 11).SMR, 5);
        Assert.Equal(1.0, ReactionProgression.RatesForRank(ReactionKind.Parry, 11).SMR, 5);
        Assert.Equal(0.9, ReactionProgression.RatesForRank(ReactionKind.Block, 11).SR, 5);
        Assert.Equal(0.55, ReactionProgression.RatesForRank(ReactionKind.Dodge, 11).SR, 5);
        Assert.Equal(0.35, ReactionProgression.RatesForRank(ReactionKind.Parry, 11).SR, 5);
    }

    [Fact(DisplayName = "interpolates a mid rank by base + step·(rank−1)")]
    public void Mid_rank()
    {
        var r = ReactionProgression.RatesForRank(ReactionKind.Block, 3);
        Assert.Equal(0.5, r.SR, 5);
        Assert.Equal(0.6, r.SMR, 5);
        Assert.Equal(0.45, r.FMR, 5);
    }

    [Fact(DisplayName = "clamps out-of-range ranks to [1, 11]")]
    public void Clamps()
    {
        Assert.Equal(ReactionProgression.RatesForRank(ReactionKind.Parry, 1), ReactionProgression.RatesForRank(ReactionKind.Parry, 0));
        Assert.Equal(ReactionProgression.RatesForRank(ReactionKind.Parry, 11), ReactionProgression.RatesForRank(ReactionKind.Parry, 99));
    }
}

public class ReactionSkillsFromProgress
{
    [Fact(DisplayName = "derives each reaction independently from its own XP")]
    public void Independent()
    {
        var skills = ReactionProgression.ReactionSkillsFromProgress(new ReactionProgress(0, 100, 1675));
        Assert.Equal(ReactionProgression.RatesForRank(ReactionKind.Block, 1), skills.Block);
        Assert.Equal(ReactionProgression.RatesForRank(ReactionKind.Dodge, 2), skills.Dodge);
        Assert.Equal(ReactionProgression.RatesForRank(ReactionKind.Parry, 11), skills.Parry);
    }

    [Fact(DisplayName = "maps ZERO_REACTION_PROGRESS to all rank-1 rates")]
    public void Zero_progress()
    {
        Assert.Equal(
            new ReactionSkills(
                ReactionProgression.RatesForRank(ReactionKind.Block, 1),
                ReactionProgression.RatesForRank(ReactionKind.Dodge, 1),
                ReactionProgression.RatesForRank(ReactionKind.Parry, 1)),
            ReactionProgression.ReactionSkillsFromProgress(ReactionProgression.Zero));
    }
}

public class AwardReactionXp
{
    [Fact(DisplayName = "grants +4 on a successful reaction")]
    public void Plus_4()
    {
        var after = ReactionProgression.AwardReactionXp(TestCombatants.Progressing(), ReactionKind.Dodge, true);
        Assert.Equal(new ReactionProgress(0, 4, 0), after.ReactionProgress);
    }

    [Fact(DisplayName = "grants +2 on a failed reaction")]
    public void Plus_2()
    {
        var after = ReactionProgression.AwardReactionXp(TestCombatants.Progressing(), ReactionKind.Dodge, false);
        Assert.Equal(new ReactionProgress(0, 2, 0), after.ReactionProgress);
    }

    [Fact(DisplayName = "rederives only the leveled reaction on a rank-up")]
    public void Rank_up()
    {
        var start = TestCombatants.WithProgress(TestCombatants.Progressing(), new ReactionProgress(96, 0, 0));
        var after = ReactionProgression.AwardReactionXp(start, ReactionKind.Block, true);
        Assert.Equal(100, after.ReactionProgress!.Block);
        Assert.Equal(ReactionProgression.RatesForRank(ReactionKind.Block, 2), after.ReactionSkills.Block);
        Assert.Equal(ReactionProgression.RatesForRank(ReactionKind.Dodge, 1), after.ReactionSkills.Dodge);
        Assert.Equal(ReactionProgression.RatesForRank(ReactionKind.Parry, 1), after.ReactionSkills.Parry);
    }

    [Fact(DisplayName = "does not rederive rates when no rank threshold is crossed")]
    public void No_rank_up()
    {
        var start = TestCombatants.Progressing();
        var after = ReactionProgression.AwardReactionXp(start, ReactionKind.Parry, true);
        Assert.Equal(start.ReactionSkills, after.ReactionSkills);
    }

    [Fact(DisplayName = "caps XP at the rank-11 threshold")]
    public void Caps_xp()
    {
        var start = TestCombatants.WithProgress(TestCombatants.Progressing(), new ReactionProgress(0, 0, 1673));
        var after = ReactionProgression.AwardReactionXp(start, ReactionKind.Parry, true);
        Assert.Equal(CombatConstants.MaxReactionXp, after.ReactionProgress!.Parry);
        Assert.Equal(ReactionProgression.RatesForRank(ReactionKind.Parry, 11), after.ReactionSkills.Parry);
    }

    [Fact(DisplayName = "returns the combatant unchanged once a reaction is capped")]
    public void Capped_is_unchanged()
    {
        var start = TestCombatants.WithProgress(TestCombatants.Progressing(), new ReactionProgress(0, 0, CombatConstants.MaxReactionXp));
        Assert.Same(start, ReactionProgression.AwardReactionXp(start, ReactionKind.Parry, true));
    }

    [Fact(DisplayName = "is a no-op for combatants without reactionProgress (enemies/NPCs)")]
    public void Enemy_no_op()
    {
        var enemy = TestCombatants.Progressing() with { Id = "enemy_1", ReactionProgress = null };
        Assert.Same(enemy, ReactionProgression.AwardReactionXp(enemy, ReactionKind.Block, true));
    }
}
