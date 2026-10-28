using System.Collections.Immutable;
using System.Globalization;
using AncientOrder.Rules.Common;

namespace AncientOrder.Rules.Party;

public enum SynergyResolution
{
    BestOnly,
    AllThatQualify,
}

/// <summary>The bonuses in priority (content) order and how their results are chosen.</summary>
public sealed record SynergySettings(ImmutableArray<SynergyBonusDefinition> Bonuses, SynergyResolution Resolution)
{
    public static SynergySettings None { get; } = new(ImmutableArray<SynergyBonusDefinition>.Empty, SynergyResolution.BestOnly);

    public ImmutableArray<RuleErrorDetail> Validate()
    {
        var details = ImmutableArray.CreateBuilder<RuleErrorDetail>();
        if (!Enum.IsDefined(Resolution))
        {
            details.Add(new RuleErrorDetail("synergy/resolution", "resolution is not a known setting"));
        }

        var names = new HashSet<string>(StringComparer.Ordinal);
        if (Bonuses.IsDefault)
        {
            details.Add(new RuleErrorDetail("synergy/bonuses", "bonuses must not be missing"));
            return details.ToImmutable();
        }

        var bonuses = Bonuses;
        for (var i = 0; i < bonuses.Length; i++)
        {
            var b = bonuses[i];
            var path = "synergy/bonuses/" + i.ToString(CultureInfo.InvariantCulture);
            if (b is null)
            {
                details.Add(new RuleErrorDetail(path, "bonus must not be null"));
                continue;
            }

            if (string.IsNullOrEmpty(b.Name))
            {
                details.Add(new RuleErrorDetail(path + "/name", "name must not be empty"));
            }
            else if (!names.Add(b.Name))
            {
                details.Add(new RuleErrorDetail(path + "/name", $"duplicate bonus name: {b.Name}"));
            }

            if (!double.IsFinite(b.Threshold) || b.Threshold <= 0)
            {
                details.Add(new RuleErrorDetail(path + "/threshold", "threshold must be a finite number above 0"));
            }

            if (!Enum.IsDefined(b.Rule))
            {
                details.Add(new RuleErrorDetail(path + "/rule", "rule is not a known rule"));
            }

            if (!Enum.IsDefined(b.Stat))
            {
                details.Add(new RuleErrorDetail(path + "/stat", "stat is not a known combat stat"));
            }

            if (!Enum.IsDefined(b.Step))
            {
                details.Add(new RuleErrorDetail(path + "/step", "step is not a known step"));
            }

            if (!double.IsFinite(b.Value))
            {
                details.Add(new RuleErrorDetail(path + "/value", "value must be a finite number"));
            }
        }

        return details.ToImmutable();
    }
}
