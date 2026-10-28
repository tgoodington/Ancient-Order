using System.Collections.Immutable;
using AncientOrder.Rules.Common;

namespace AncientOrder.Rules.Combat;

/// <summary>The stats buffs can modify, as numbers. Output of ApplyDynamicModifiers.</summary>
public sealed record ModifiedStats(
    double Power,
    double Speed,
    double BlockSR,
    double BlockSMR,
    double BlockFMR,
    double DodgeSR,
    double DodgeSMR,
    double DodgeFMR,
    double ParrySR,
    double ParrySMR,
    double ParryFMR);

/// <summary>Pure combat formulas. Every roll is passed in (0 to 20), exactly where the TypeScript calls rollFn().</summary>
public static class Formulas
{
    public static double CalculateRankKOThreshold(double attackerRank, double targetRank) =>
        ((attackerRank - targetRank) * 3) / 10;

    public static bool CheckRankKO(double threshold, double roll) => roll / 20 >= 1 - threshold;

    public static double CalculateBlindsideThreshold(double attackerSpeed, double targetSpeed) =>
        (attackerSpeed - targetSpeed) / targetSpeed;

    public static bool CheckBlindside(double threshold, double roll) => roll / 20 >= 1 - threshold;

    public static double CalculateCrushingBlowThreshold(double actionPower, double targetPower) =>
        (actionPower - targetPower) / targetPower;

    public static bool CheckCrushingBlow(double threshold, double roll) => roll / 20 >= 1 - threshold;

    public static double CalculateBlockDamage(double damage, double smr, double fmr, bool success) =>
        success ? damage * (1 - smr) : damage * (1 - fmr);

    public static double CalculateDodgeDamage(double damage, double smr, double fmr, bool success) =>
        success ? damage * (1 - smr) : damage * (1 - fmr);

    public static double CalculateParryDamage(double damage, double smr, double fmr, bool success) =>
        success ? damage * (1 - smr) : damage * (1 - fmr);

    public static double CalculateDefenselessDamage(double damage) => damage;

    /// <summary>Spreadsheet Math!O: MROUND(L + M + N, 0.25). No target-power term.</summary>
    public static double CalculateBaseDamage(double attackerPower, double modifier = 0) =>
        JsMath.Round((attackerPower + modifier) / 0.25) * 0.25;

    /// <summary>Raw (unrounded) Special power; round once afterwards with CalculateBaseDamage.</summary>
    public static double CalculateSpecialDamageBonus(double power, double energySegments) =>
        power * (1 + 0.1 * energySegments);

    public static double CalculateEvadeRegen(double maxStamina) => maxStamina * 0.3;

    public static double CalculateEnergyGain(EnergyEvent eventType, bool success, AscensionLevel ascensionLevel)
    {
        var baseGain = CombatConstants.EnergyGain(eventType, success);
        var bonus = CombatConstants.AscensionAccumulationBonus[(int)ascensionLevel];
        return baseGain * (1 + bonus);
    }

    public static AscensionLevel CalculateAscensionLevel(double totalSegments)
    {
        var t = CombatConstants.AscensionThresholds;
        if (totalSegments >= t[2])
        {
            return AscensionLevel.Level3;
        }

        if (totalSegments >= t[1])
        {
            return AscensionLevel.Level2;
        }

        if (totalSegments >= t[0])
        {
            return AscensionLevel.Level1;
        }

        return AscensionLevel.Level0;
    }

    /// <summary>Adds every buff's modifier to its stat, in buff order. The input is never changed.</summary>
    public static ModifiedStats ApplyDynamicModifiers(ModifiedStats baseStats, ImmutableArray<Buff> buffs)
    {
        var stats = baseStats;
        foreach (var buff in buffs)
        {
            stats = buff.Stat switch
            {
                BuffStat.Power => stats with { Power = stats.Power + buff.Modifier },
                BuffStat.Speed => stats with { Speed = stats.Speed + buff.Modifier },
                BuffStat.BlockSR => stats with { BlockSR = stats.BlockSR + buff.Modifier },
                BuffStat.BlockSMR => stats with { BlockSMR = stats.BlockSMR + buff.Modifier },
                BuffStat.BlockFMR => stats with { BlockFMR = stats.BlockFMR + buff.Modifier },
                BuffStat.DodgeSR => stats with { DodgeSR = stats.DodgeSR + buff.Modifier },
                BuffStat.DodgeSMR => stats with { DodgeSMR = stats.DodgeSMR + buff.Modifier },
                BuffStat.DodgeFMR => stats with { DodgeFMR = stats.DodgeFMR + buff.Modifier },
                BuffStat.ParrySR => stats with { ParrySR = stats.ParrySR + buff.Modifier },
                BuffStat.ParrySMR => stats with { ParrySMR = stats.ParrySMR + buff.Modifier },
                BuffStat.ParryFMR => stats with { ParryFMR = stats.ParryFMR + buff.Modifier },
                _ => throw new ArgumentOutOfRangeException(nameof(buffs)),
            };
        }

        return stats;
    }
}
