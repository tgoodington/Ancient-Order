using AncientOrder.Rules.Common;

namespace AncientOrder.Rules.Combat;

/// <summary>The three trainable reactions; Defenseless earns no XP.</summary>
public enum ReactionKind
{
    Block,
    Dodge,
    Parry,
}

/// <summary>A rate ramp is base + step * (rank - 1).</summary>
public sealed record RateRamp(double Base, double Step);

public sealed record ReactionRamp(RateRamp SR, RateRamp SMR, RateRamp FMR);

public static class ReactionProgression
{
    public static readonly ReactionProgress Zero = new(0, 0, 0);

    public static ReactionRamp Ramp(ReactionKind reaction) => reaction switch
    {
        ReactionKind.Block => new ReactionRamp(new RateRamp(0.4, 0.05), new RateRamp(0.55, 0.025), new RateRamp(0.4, 0.025)),
        ReactionKind.Dodge => new ReactionRamp(new RateRamp(0.3, 0.025), new RateRamp(0.8, 0.01), new RateRamp(0.1, 0.025)),
        ReactionKind.Parry => new ReactionRamp(new RateRamp(0.1, 0.025), new RateRamp(0.9, 0.01), new RateRamp(0.0, 0.025)),
        _ => throw new ArgumentOutOfRangeException(nameof(reaction)),
    };

    /// <summary>The highest rank (1 to 11) whose cumulative XP threshold has been met.</summary>
    public static int RankFromXp(double xp)
    {
        var rank = 1;
        for (var r = 0; r < CombatConstants.XpThresholds.Length; r++)
        {
            if (xp >= CombatConstants.XpThresholds[r])
            {
                rank = r + 1;
            }
            else
            {
                break;
            }
        }

        return rank;
    }

    public static DefenseSkill RatesForRank(ReactionKind reaction, int rank)
    {
        var clampedRank = Math.Max(1, Math.Min(CombatConstants.MaxReactionRank, rank));
        var step = clampedRank - 1;
        var ramp = Ramp(reaction);
        return new DefenseSkill(RatesAt(ramp.SR, step), RatesAt(ramp.SMR, step), RatesAt(ramp.FMR, step));
    }

    public static ReactionSkills ReactionSkillsFromProgress(ReactionProgress progress) => new(
        RatesForRank(ReactionKind.Block, RankFromXp(progress.Block)),
        RatesForRank(ReactionKind.Dodge, RankFromXp(progress.Dodge)),
        RatesForRank(ReactionKind.Parry, RankFromXp(progress.Parry)));

    /// <summary>Awards XP for using a reaction; re-derives that one reaction's rates if it ranked up. Returns the same combatant when it has no progress or is already capped.</summary>
    public static Combatant AwardReactionXp(Combatant combatant, ReactionKind reaction, bool success)
    {
        var progress = combatant.ReactionProgress;
        if (progress is null)
        {
            return combatant;
        }

        var gain = success ? CombatConstants.XpOnSuccess : CombatConstants.XpOnFailure;
        var oldXp = XpOf(progress, reaction);
        var newXp = Math.Min(CombatConstants.MaxReactionXp, oldXp + gain);
        if (newXp == oldXp)
        {
            return combatant;
        }

        var newProgress = reaction switch
        {
            ReactionKind.Block => progress with { Block = newXp },
            ReactionKind.Dodge => progress with { Dodge = newXp },
            _ => progress with { Parry = newXp },
        };

        var skills = combatant.ReactionSkills;
        if (RankFromXp(newXp) != RankFromXp(oldXp))
        {
            var rates = RatesForRank(reaction, RankFromXp(newXp));
            skills = reaction switch
            {
                ReactionKind.Block => skills with { Block = rates },
                ReactionKind.Dodge => skills with { Dodge = rates },
                _ => skills with { Parry = rates },
            };
        }

        return combatant with { ReactionProgress = newProgress, ReactionSkills = skills };
    }

    private static int XpOf(ReactionProgress progress, ReactionKind reaction) => reaction switch
    {
        ReactionKind.Block => progress.Block,
        ReactionKind.Dodge => progress.Dodge,
        _ => progress.Parry,
    };

    private static double RatesAt(RateRamp ramp, int step) => JsMath.Round((ramp.Base + (ramp.Step * step)) * 10000) / 10000;
}
