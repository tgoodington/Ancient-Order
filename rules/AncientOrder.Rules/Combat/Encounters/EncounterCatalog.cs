using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Globalization;
using AncientOrder.Rules.Combat.Ai;
using AncientOrder.Rules.Common;
using AncientOrder.Rules.State;

namespace AncientOrder.Rules.Combat;

/// <summary>The encounters in content order plus a lookup by id.</summary>
public sealed record EncounterCatalog
{
    private EncounterCatalog(ImmutableArray<EncounterDefinition> definitions)
    {
        Definitions = definitions;
        Index = definitions.ToFrozenDictionary(d => d.Id, StringComparer.Ordinal);
    }

    public static EncounterCatalog Empty { get; } = new(ImmutableArray<EncounterDefinition>.Empty);

    public ImmutableArray<EncounterDefinition> Definitions { get; }

    public FrozenDictionary<string, EncounterDefinition> Index { get; }

    public bool TryGet(string? id, out EncounterDefinition definition)
    {
        if (id is not null && Index.TryGetValue(id, out var found))
        {
            definition = found;
            return true;
        }

        definition = null!;
        return false;
    }

    /// <summary>Checks unique encounter ids, unique fighter ids within an encounter, no enemy id equal to a character id, and every enemy archetype defined. A default array means no encounters.</summary>
    public static Result<EncounterCatalog> Create(
        ImmutableArray<EncounterDefinition> definitions,
        ArchetypeCatalog archetypes,
        CharacterCatalog characters)
    {
        if (definitions.IsDefault)
        {
            return Result<EncounterCatalog>.Ok(Empty);
        }

        var details = ImmutableArray.CreateBuilder<RuleErrorDetail>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        for (var i = 0; i < definitions.Length; i++)
        {
            var d = definitions[i];
            var path = "encounters/" + i.ToString(CultureInfo.InvariantCulture);

            if (d is null)
            {
                details.Add(new RuleErrorDetail(path, "encounter must not be null"));
                continue;
            }

            if (string.IsNullOrEmpty(d.Id))
            {
                details.Add(new RuleErrorDetail(path + "/id", "id must not be empty"));
            }
            else
            {
                if (d.Id.Length > CharacterCatalog.MaxIdLength)
                {
                    details.Add(new RuleErrorDetail(path + "/id", $"id must be 1 to {CharacterCatalog.MaxIdLength} characters"));
                }

                if (!seen.Add(d.Id))
                {
                    details.Add(new RuleErrorDetail(path + "/id", $"duplicate encounter id: {d.Id}"));
                }
            }

            if (string.IsNullOrEmpty(d.Name))
            {
                details.Add(new RuleErrorDetail(path + "/name", "name must not be empty"));
            }

            if (d.Enemies.IsDefault)
            {
                details.Add(new RuleErrorDetail(path + "/enemies", "enemies must not be a default array"));
                continue;
            }

            if (d.Enemies.Length == 0)
            {
                details.Add(new RuleErrorDetail(path + "/enemies", "an encounter needs at least one enemy"));
            }

            var fighters = new HashSet<string>(StringComparer.Ordinal);
            for (var j = 0; j < d.Enemies.Length; j++)
            {
                var e = d.Enemies[j];
                var enemyPath = path + "/enemies/" + j.ToString(CultureInfo.InvariantCulture);

                if (e is null)
                {
                    details.Add(new RuleErrorDetail(enemyPath, "enemy must not be null"));
                    continue;
                }

                if (string.IsNullOrEmpty(e.Id))
                {
                    details.Add(new RuleErrorDetail(enemyPath + "/id", "id must not be empty"));
                }
                else
                {
                    if (e.Id.Length > CharacterCatalog.MaxIdLength)
                    {
                        details.Add(new RuleErrorDetail(enemyPath + "/id", $"id must be 1 to {CharacterCatalog.MaxIdLength} characters"));
                    }

                    if (!fighters.Add(e.Id))
                    {
                        details.Add(new RuleErrorDetail(enemyPath + "/id", $"duplicate fighter id: {e.Id}"));
                    }

                    if (characters.TryGet(e.Id, out _))
                    {
                        details.Add(new RuleErrorDetail(enemyPath + "/id", $"enemy id reuses a character id: {e.Id}"));
                    }
                }

                if (!archetypes.TryGet(e.Archetype, out _))
                {
                    details.Add(new RuleErrorDetail(enemyPath + "/archetype", $"unknown archetype: {e.Archetype}"));
                }

                ValidateStats(e, enemyPath, details);
            }
        }

        if (details.Count > 0)
        {
            return Result<EncounterCatalog>.Fail(
                new RuleError(ErrorCode.VALIDATION_ERROR, "Encounter catalog is invalid: " + details[0].Message, details.ToImmutable()));
        }

        return Result<EncounterCatalog>.Ok(new EncounterCatalog(definitions));
    }

    private static void ValidateStats(CombatantConfig e, string path, ImmutableArray<RuleErrorDetail>.Builder details)
    {
        if (string.IsNullOrEmpty(e.Name))
        {
            details.Add(new RuleErrorDetail(path + "/name", "name must not be empty"));
        }

        if (!double.IsFinite(e.Rank) || e.Rank <= 0)
        {
            details.Add(new RuleErrorDetail(path + "/rank", "rank must be a finite number above 0"));
        }

        if (!double.IsFinite(e.Stamina) || e.Stamina <= 0)
        {
            details.Add(new RuleErrorDetail(path + "/stamina", "stamina must be a finite number above 0"));
        }

        if (!double.IsFinite(e.Power) || e.Power < 0)
        {
            details.Add(new RuleErrorDetail(path + "/power", "power must be a finite number of 0 or more"));
        }

        if (!double.IsFinite(e.Speed) || e.Speed < 0)
        {
            details.Add(new RuleErrorDetail(path + "/speed", "speed must be a finite number of 0 or more"));
        }

        if (!Enum.IsDefined(e.ElementalPath))
        {
            details.Add(new RuleErrorDetail(path + "/elementalPath", "elementalPath is not a known path"));
        }

        if (e.AscensionLevel is { } level && !Enum.IsDefined(level))
        {
            details.Add(new RuleErrorDetail(path + "/ascensionLevel", "ascensionLevel must be 0 to 3"));
        }

        var skills = e.ReactionSkills;
        if (skills is null)
        {
            details.Add(new RuleErrorDetail(path + "/reactionSkills", "reactionSkills must be present"));
            return;
        }

        ValidateSkill(skills.Block, path + "/reactionSkills/block", details);
        ValidateSkill(skills.Dodge, path + "/reactionSkills/dodge", details);
        ValidateSkill(skills.Parry, path + "/reactionSkills/parry", details);
    }

    private static void ValidateSkill(DefenseSkill? skill, string path, ImmutableArray<RuleErrorDetail>.Builder details)
    {
        if (skill is null)
        {
            details.Add(new RuleErrorDetail(path, "reaction skill must be present"));
            return;
        }

        CheckRate(skill.SR, path + "/SR", details);
        CheckRate(skill.SMR, path + "/SMR", details);
        CheckRate(skill.FMR, path + "/FMR", details);
    }

    private static void CheckRate(double rate, string path, ImmutableArray<RuleErrorDetail>.Builder details)
    {
        if (!double.IsFinite(rate) || rate < 0 || rate > 1)
        {
            details.Add(new RuleErrorDetail(path, "rate must be a finite number from 0 to 1"));
        }
    }
}
