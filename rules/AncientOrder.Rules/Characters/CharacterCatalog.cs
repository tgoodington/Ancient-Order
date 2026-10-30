using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Globalization;
using AncientOrder.Rules.Common;
using AncientOrder.Rules.Conversations;
using AncientOrder.Rules.Personality;

namespace AncientOrder.Rules.State;

/// <summary>The character definitions in content order, which is the order every listing and walk uses, plus a lookup by id.</summary>
public sealed record CharacterCatalog
{
    public const int MaxIdLength = 64;

    private CharacterCatalog(ImmutableArray<CharacterDefinition> definitions)
    {
        Definitions = definitions;
        Index = definitions.ToFrozenDictionary(d => d.Id, StringComparer.Ordinal);
    }

    public ImmutableArray<CharacterDefinition> Definitions { get; }

    public FrozenDictionary<string, CharacterDefinition> Index { get; }

    public bool TryGet(string id, out CharacterDefinition definition)
    {
        if (id is not null && Index.TryGetValue(id, out var found))
        {
            definition = found;
            return true;
        }

        definition = null!;
        return false;
    }

    public static Result<CharacterCatalog> Create(ImmutableArray<CharacterDefinition> definitions)
    {
        if (definitions.IsDefault)
        {
            return Result<CharacterCatalog>.Fail(new RuleError(
                ErrorCode.VALIDATION_ERROR,
                "Character catalog is invalid: characters must not be a default array",
                [new RuleErrorDetail("characters", "characters must not be a default array")]));
        }

        var details = ImmutableArray.CreateBuilder<RuleErrorDetail>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        for (var i = 0; i < definitions.Length; i++)
        {
            var d = definitions[i];
            var path = "characters/" + i.ToString(CultureInfo.InvariantCulture);

            if (d is null)
            {
                details.Add(new RuleErrorDetail(path, "character must not be null"));
                continue;
            }

            if (string.IsNullOrEmpty(d.ArchetypeId))
            {
                details.Add(new RuleErrorDetail(path + "/archetypeId", "archetypeId must not be empty"));
            }

            if (string.IsNullOrEmpty(d.Id))
            {
                details.Add(new RuleErrorDetail(path + "/id", "id must not be empty"));
            }
            else
            {
                if (d.Id.Length > MaxIdLength)
                {
                    details.Add(new RuleErrorDetail(path + "/id", $"id must be at most {MaxIdLength} characters"));
                }

                if (Speaker.IsReserved(d.Id))
                {
                    details.Add(new RuleErrorDetail(path + "/id", $"id {d.Id} is reserved for conversation speakers"));
                }

                if (!seen.Add(d.Id))
                {
                    details.Add(new RuleErrorDetail(path + "/id", $"duplicate character id: {d.Id}"));
                }
            }

            if (string.IsNullOrEmpty(d.Name))
            {
                details.Add(new RuleErrorDetail(path + "/name", "name must not be empty"));
            }

            if (d.Personality is null)
            {
                details.Add(new RuleErrorDetail(path + "/personality", "personality must not be null"));
            }
            else
            {
                var personality = PersonalityRules.Validate(d.Personality);
                if (!personality.Valid)
                {
                    details.Add(new RuleErrorDetail(path + "/personality", string.Join("; ", personality.Errors)));
                }
            }

            if (!InRange(d.StartingAffection))
            {
                details.Add(new RuleErrorDetail(path + "/startingAffection", "startingAffection must be from -100 to 100"));
            }

            if (!InRange(d.StartingTrust))
            {
                details.Add(new RuleErrorDetail(path + "/startingTrust", "startingTrust must be from -100 to 100"));
            }

            var combat = d.Combat?.Validate();
            if (combat is not null)
            {
                details.Add(new RuleErrorDetail(path + "/combat", combat));
            }
        }

        if (details.Count > 0)
        {
            return Result<CharacterCatalog>.Fail(
                new RuleError(ErrorCode.VALIDATION_ERROR, "Character catalog is invalid: " + details[0].Message, details.ToImmutable()));
        }

        return Result<CharacterCatalog>.Ok(new CharacterCatalog(definitions));
    }

    private static bool InRange(double value) => double.IsFinite(value) && value >= -100 && value <= 100;
}
