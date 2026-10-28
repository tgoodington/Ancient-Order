namespace AncientOrder.Rules.Combat;

public sealed record BlockOutcome(bool Success, double Damage, bool CrushingBlowEligible);

public sealed record DodgeOutcome(bool Success, double Damage);

public sealed record ParryOutcome(bool Success, double Damage, bool CounterTriggered);

/// <summary>Defense resolution. Success is a low roll: roll is at most SR * 20.</summary>
public static class Defense
{
    public static BlockOutcome ResolveBlock(double damage, double sr, double smr, double fmr, double roll)
    {
        var success = roll <= sr * 20;
        return new BlockOutcome(success, Formulas.CalculateBlockDamage(damage, smr, fmr, success), true);
    }

    public static DodgeOutcome ResolveDodge(double damage, double sr, double smr, double fmr, double roll)
    {
        var success = roll <= sr * 20;
        return new DodgeOutcome(success, Formulas.CalculateDodgeDamage(damage, smr, fmr, success));
    }

    public static ParryOutcome ResolveParry(double damage, double sr, double smr, double fmr, double roll)
    {
        var success = roll <= sr * 20;
        return new ParryOutcome(success, Formulas.CalculateParryDamage(damage, smr, fmr, success), success);
    }

    public static double ResolveDefenseless(double damage) => Formulas.CalculateDefenselessDamage(damage);

    public static DefenseResult ResolveDefense(DefenseType defenseType, double damage, ReactionSkills reactionSkills, double roll)
    {
        switch (defenseType)
        {
            case DefenseType.Block:
                {
                    var s = reactionSkills.Block;
                    var r = ResolveBlock(damage, s.SR, s.SMR, s.FMR, roll);
                    return new DefenseResult(DefenseType.Block, r.Success, Multiplier(damage, r.Damage, r.Success, s));
                }

            case DefenseType.Dodge:
                {
                    var s = reactionSkills.Dodge;
                    var r = ResolveDodge(damage, s.SR, s.SMR, s.FMR, roll);
                    return new DefenseResult(DefenseType.Dodge, r.Success, Multiplier(damage, r.Damage, r.Success, s));
                }

            case DefenseType.Parry:
                {
                    var s = reactionSkills.Parry;
                    var r = ResolveParry(damage, s.SR, s.SMR, s.FMR, roll);
                    return new DefenseResult(DefenseType.Parry, r.Success, Multiplier(damage, r.Damage, r.Success, s));
                }

            case DefenseType.Defenseless:
                return new DefenseResult(DefenseType.Defenseless, false, 1.0);

            default:
                throw new ArgumentOutOfRangeException(nameof(defenseType));
        }
    }

    private static double Multiplier(double damage, double finalDamage, bool success, DefenseSkill skill) =>
        damage > 0 ? finalDamage / damage : success ? 1 - skill.SMR : 1 - skill.FMR;

    /// <summary>Base reaction skills with all active buffs folded in, each rate clamped to [0, 1]. Crushing Blow is not folded here (it is written to the base block rates).</summary>
    public static ReactionSkills EffectiveReactionSkills(Combatant combatant)
    {
        var rs = combatant.ReactionSkills;
        var baseStats = new ModifiedStats(
            combatant.Power,
            combatant.Speed,
            rs.Block.SR,
            rs.Block.SMR,
            rs.Block.FMR,
            rs.Dodge.SR,
            rs.Dodge.SMR,
            rs.Dodge.FMR,
            rs.Parry.SR,
            rs.Parry.SMR,
            rs.Parry.FMR);
        var m = Formulas.ApplyDynamicModifiers(baseStats, combatant.ActiveBuffs);
        return new ReactionSkills(
            new DefenseSkill(Clamp01(m.BlockSR), Clamp01(m.BlockSMR), Clamp01(m.BlockFMR)),
            new DefenseSkill(Clamp01(m.DodgeSR), Clamp01(m.DodgeSMR), Clamp01(m.DodgeFMR)),
            new DefenseSkill(Clamp01(m.ParrySR), Clamp01(m.ParrySMR), Clamp01(m.ParryFMR)));
    }

    private static double Clamp01(double v) => Math.Min(1, Math.Max(0, v));
}
