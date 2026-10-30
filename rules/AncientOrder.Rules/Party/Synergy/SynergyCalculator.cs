using System.Collections.Immutable;
using AncientOrder.Rules.Combat;
using AncientOrder.Rules.Personality;

namespace AncientOrder.Rules.Party;

public sealed record SynergyResult(string Name, CombatStat Stat, StatStep Step, double Value, double MatchQuality, string? Feel = null);

public static class SynergyCalculator
{
    /// <summary>Scores each bonus for the player and the companions (in the order given) and resolves per the settings.</summary>
    public static ImmutableArray<SynergyResult> Evaluate(
        SynergySettings settings,
        AncientOrder.Rules.Personality.Personality player,
        ImmutableArray<AncientOrder.Rules.Personality.Personality> companions)
    {
        var results = ImmutableArray.CreateBuilder<SynergyResult>();
        foreach (var bonus in settings.Bonuses)
        {
            var quality = bonus.Rule == SynergyRule.Coverage
                ? Coverage(player, companions, bonus.Threshold)
                : Alignment(player, companions, bonus.Threshold);
            if (quality is not null)
            {
                results.Add(new SynergyResult(bonus.Name, bonus.Stat, bonus.Step, bonus.Value, quality.Value, bonus.Feel));
            }
        }

        if (settings.Resolution == SynergyResolution.AllThatQualify || results.Count <= 1)
        {
            return results.ToImmutable();
        }

        var best = results[0];
        foreach (var result in results)
        {
            if (result.MatchQuality > best.MatchQuality)
            {
                best = result;
            }
        }

        return [best];
    }

    private static double? Coverage(
        AncientOrder.Rules.Personality.Personality player,
        ImmutableArray<AncientOrder.Rules.Personality.Personality> companions,
        double threshold)
    {
        double sum = 0;
        foreach (var trait in PersonalityRules.Traits)
        {
            var max = player[trait];
            foreach (var companion in companions)
            {
                max = Math.Max(max, companion[trait]);
            }

            if (max < threshold)
            {
                return null;
            }

            sum += max;
        }

        return sum / (6 * threshold);
    }

    private static double? Alignment(
        AncientOrder.Rules.Personality.Personality player,
        ImmutableArray<AncientOrder.Rules.Personality.Personality> companions,
        double threshold)
    {
        double? best = null;
        foreach (var companion in companions)
        {
            var top = PersonalityRules.Traits.OrderByDescending(t => companion[t]).Take(2).ToImmutableArray();
            var companionSum = companion[top[0]] + companion[top[1]];
            if (companionSum == 0)
            {
                continue;
            }

            var ratio = (player[top[0]] + player[top[1]]) / companionSum;
            if (best is null || ratio > best.Value)
            {
                best = ratio;
            }
        }

        if (best is null || best.Value < threshold / 100)
        {
            return null;
        }

        return best;
    }
}
