namespace AncientOrder.Rules.Combat;

public enum PathType
{
    Action,
    Reaction,
}

public sealed record PathConfig(
    PathType Type,
    DefenseType DefenseBoost,
    DefenseType SpecialForces,
    double BuffModifier,
    double DebuffModifier);

public static class ElementalPaths
{
    public static PathConfig Config(ElementalPath path) => path switch
    {
        ElementalPath.Fire => Make(PathType.Reaction, DefenseType.Parry),
        ElementalPath.Water => Make(PathType.Action, DefenseType.Dodge),
        ElementalPath.Air => Make(PathType.Reaction, DefenseType.Dodge),
        ElementalPath.Earth => Make(PathType.Action, DefenseType.Block),
        ElementalPath.Shadow => Make(PathType.Action, DefenseType.Parry),
        ElementalPath.Light => Make(PathType.Reaction, DefenseType.Block),
        _ => throw new ArgumentOutOfRangeException(nameof(path)),
    };

    private static PathConfig Make(PathType type, DefenseType defense) =>
        new(type, defense, defense, CombatConstants.PathBuffModifier, CombatConstants.PathDebuffModifier);

    /// <summary>The SR stat of a defense: block, dodge or parry.</summary>
    private static BuffStat SrStat(DefenseType defense) => defense switch
    {
        DefenseType.Block => BuffStat.BlockSR,
        DefenseType.Dodge => BuffStat.DodgeSR,
        DefenseType.Parry => BuffStat.ParrySR,
        _ => throw new ArgumentOutOfRangeException(nameof(defense)),
    };

    /// <summary>Reaction-path self buff on the path's defense SR; applies once per (stat, source).</summary>
    public static Combatant ApplyPathBuff(Combatant combatant, ElementalPath path)
    {
        var config = Config(path);
        var stat = SrStat(config.DefenseBoost);
        var source = path.ToString();
        if (HasBuff(combatant, stat, source))
        {
            return combatant;
        }

        var buff = new Buff(stat, source, config.BuffModifier);
        return combatant with { ActiveBuffs = combatant.ActiveBuffs.Add(buff) };
    }

    /// <summary>Action-path debuff (negative modifier) on the target's defense SR; applies once per (stat, source).</summary>
    public static Combatant ApplyPathDebuff(Combatant target, ElementalPath attackerPath)
    {
        var config = Config(attackerPath);
        var stat = SrStat(config.DefenseBoost);
        var source = attackerPath.ToString();
        if (HasBuff(target, stat, source))
        {
            return target;
        }

        var buff = new Buff(stat, source, -config.DebuffModifier);
        return target with { ActiveBuffs = target.ActiveBuffs.Add(buff) };
    }

    public static DefenseType GetSpecialForceDefense(ElementalPath attackerPath) => Config(attackerPath).SpecialForces;

    public static DefenseType GetPreferredDefense(ElementalPath defenderPath)
    {
        var config = Config(defenderPath);
        return config.Type == PathType.Reaction ? config.DefenseBoost : DefenseType.Block;
    }

    private static bool HasBuff(Combatant combatant, BuffStat stat, string source)
    {
        foreach (var b in combatant.ActiveBuffs)
        {
            if (b.Stat == stat && string.Equals(b.Source, source, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
