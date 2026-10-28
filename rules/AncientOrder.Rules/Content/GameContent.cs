using System.Collections.Immutable;
using System.Globalization;
using AncientOrder.Rules.Combat;
using AncientOrder.Rules.Combat.Ai;
using AncientOrder.Rules.Common;
using AncientOrder.Rules.Party;
using AncientOrder.Rules.Personality;

namespace AncientOrder.Rules.State;

/// <summary>The read-only content bundle the session plays from. T4, T6, and T8 add their parts here and to Create.</summary>
public sealed record GameContent
{
    private GameContent(GameSettings settings, CharacterCatalog characters, ArchetypeCatalog archetypes, SynergySettings synergy, EncounterCatalog encounters)
    {
        Settings = settings;
        Characters = characters;
        Archetypes = archetypes;
        Synergy = synergy;
        Encounters = encounters;
    }

    public GameSettings Settings { get; }

    public CharacterCatalog Characters { get; }

    public ArchetypeCatalog Archetypes { get; }

    public SynergySettings Synergy { get; }

    public EncounterCatalog Encounters { get; }

    public static Result<GameContent> Create(GameSettings settings, ImmutableArray<CharacterDefinition> characters, ImmutableArray<ArchetypeDefinition> archetypes, SynergySettings? synergy = null, ImmutableArray<EncounterDefinition> encounters = default)
    {
        if (settings is null)
        {
            return Result<GameContent>.Fail(new RuleError(
                ErrorCode.VALIDATION_ERROR,
                "Content is invalid: settings must not be null",
                [new RuleErrorDetail("settings", "settings must not be null")]));
        }

        var details = ImmutableArray.CreateBuilder<RuleErrorDetail>();

        // The spec lists no settings checks; these only refuse values that could never play.
        if (string.IsNullOrEmpty(settings.PlayerName))
        {
            details.Add(new RuleErrorDetail("settings/playerName", "playerName must not be empty"));
        }

        if (settings.LineupSize < 0)
        {
            details.Add(new RuleErrorDetail("settings/lineupSize", "lineupSize must be 0 or more"));
        }

        if (settings.StartingPersonality is not null)
        {
            var personality = PersonalityRules.Validate(settings.StartingPersonality);
            if (!personality.Valid)
            {
                details.Add(new RuleErrorDetail("settings/startingPersonality", string.Join("; ", personality.Errors)));
            }
        }

        var combat = settings.PlayerCombat?.Validate();
        if (combat is not null)
        {
            details.Add(new RuleErrorDetail("settings/playerCombat", combat));
        }

        var catalog = CharacterCatalog.Create(characters);
        if (!catalog.IsOk)
        {
            details.AddRange(catalog.Error.Details);
        }

        var archetypeCatalog = ArchetypeCatalog.Create(archetypes);
        if (!archetypeCatalog.IsOk)
        {
            details.AddRange(archetypeCatalog.Error.Details);
        }
        else if (catalog.IsOk)
        {
            for (var i = 0; i < catalog.Value.Definitions.Length; i++)
            {
                var archetypeId = catalog.Value.Definitions[i].ArchetypeId;
                if (!archetypeCatalog.Value.TryGet(archetypeId, out _))
                {
                    details.Add(new RuleErrorDetail(
                        "characters/" + i.ToString(CultureInfo.InvariantCulture) + "/archetypeId",
                        $"unknown archetype: {archetypeId}"));
                }
            }
        }

        var encounterCatalog = EncounterCatalog.Empty;
        if (archetypeCatalog.IsOk && catalog.IsOk)
        {
            var checkedEncounters = EncounterCatalog.Create(encounters, archetypeCatalog.Value, catalog.Value);
            if (checkedEncounters.IsOk)
            {
                encounterCatalog = checkedEncounters.Value;
            }
            else
            {
                details.AddRange(checkedEncounters.Error.Details);
            }
        }

        synergy ??= SynergySettings.None;
        details.AddRange(synergy.Validate());

        if (details.Count > 0)
        {
            return Result<GameContent>.Fail(
                new RuleError(ErrorCode.VALIDATION_ERROR, "Content is invalid: " + details[0].Message, details.ToImmutable()));
        }

        return Result<GameContent>.Ok(new GameContent(settings, catalog.Value, archetypeCatalog.Value, synergy, encounterCatalog));
    }
}
