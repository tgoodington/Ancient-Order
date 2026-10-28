using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Globalization;
using AncientOrder.Rules.Common;

namespace AncientOrder.Rules.Combat.Ai;

/// <summary>The archetype definitions in content order plus a lookup by id.</summary>
public sealed record ArchetypeCatalog
{
    private ArchetypeCatalog(ImmutableArray<ArchetypeDefinition> definitions)
    {
        Definitions = definitions;
        Index = definitions.ToFrozenDictionary(d => d.Id, StringComparer.Ordinal);
    }

    public ImmutableArray<ArchetypeDefinition> Definitions { get; }

    public FrozenDictionary<string, ArchetypeDefinition> Index { get; }

    public bool TryGet(string? id, out ArchetypeDefinition definition)
    {
        if (id is not null && Index.TryGetValue(id, out var found))
        {
            definition = found;
            return true;
        }

        definition = null!;
        return false;
    }

    public static Result<ArchetypeCatalog> Create(ImmutableArray<ArchetypeDefinition> definitions)
    {
        if (definitions.IsDefault)
        {
            return Result<ArchetypeCatalog>.Fail(new RuleError(
                ErrorCode.VALIDATION_ERROR,
                "Archetype catalog is invalid: archetypes must not be a default array",
                [new RuleErrorDetail("archetypes", "archetypes must not be a default array")]));
        }

        var details = ImmutableArray.CreateBuilder<RuleErrorDetail>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        for (var i = 0; i < definitions.Length; i++)
        {
            var d = definitions[i];
            var path = "archetypes/" + i.ToString(CultureInfo.InvariantCulture);

            if (d is null)
            {
                details.Add(new RuleErrorDetail(path, "archetype must not be null"));
                continue;
            }

            if (string.IsNullOrEmpty(d.Id))
            {
                details.Add(new RuleErrorDetail(path + "/id", "id must not be empty"));
            }
            else if (!seen.Add(d.Id))
            {
                details.Add(new RuleErrorDetail(path + "/id", $"duplicate archetype id: {d.Id}"));
            }

            switch (d.Behavior)
            {
                case ArchetypeBehavior.Basic:
                    break;
                case ArchetypeBehavior.Scored scored:
                    ValidateProfile(scored.Profile, path + "/profile", details);
                    break;
                default:
                    details.Add(new RuleErrorDetail(path + "/behavior", "behavior must be Basic or Scored"));
                    break;
            }
        }

        if (details.Count > 0)
        {
            return Result<ArchetypeCatalog>.Fail(
                new RuleError(ErrorCode.VALIDATION_ERROR, "Archetype catalog is invalid: " + details[0].Message, details.ToImmutable()));
        }

        return Result<ArchetypeCatalog>.Ok(new ArchetypeCatalog(definitions));
    }

    private static void ValidateProfile(DecisionProfile? profile, string path, ImmutableArray<RuleErrorDetail>.Builder details)
    {
        if (profile is null)
        {
            details.Add(new RuleErrorDetail(path, "a scored archetype needs a decision profile"));
            return;
        }

        foreach (var type in Enum.GetValues<ActionType>())
        {
            if (profile.BaseScores is null || !profile.BaseScores.TryGetValue(type, out var score))
            {
                details.Add(new RuleErrorDetail(path + "/baseScores/" + type, $"missing base score for {type}"));
            }
            else if (!double.IsFinite(score))
            {
                details.Add(new RuleErrorDetail(path + "/baseScores/" + type, $"base score for {type} must be a finite number"));
            }
        }

        foreach (var name in ScoringFactors.Names)
        {
            if (profile.FactorWeights is null || !profile.FactorWeights.TryGetValue(name, out var weight))
            {
                details.Add(new RuleErrorDetail(path + "/factorWeights/" + name, $"missing weight for factor {name}"));
            }
            else if (!double.IsFinite(weight))
            {
                details.Add(new RuleErrorDetail(path + "/factorWeights/" + name, $"weight for factor {name} must be a finite number"));
            }
        }
    }
}
