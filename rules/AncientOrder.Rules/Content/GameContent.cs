using System.Collections.Immutable;
using System.Globalization;
using AncientOrder.Rules.Combat;
using AncientOrder.Rules.Combat.Ai;
using AncientOrder.Rules.Common;
using AncientOrder.Rules.Conversations;
using AncientOrder.Rules.Party;
using AncientOrder.Rules.Personality;
using AncientOrder.Rules.Town;

namespace AncientOrder.Rules.State;

/// <summary>The read-only content bundle the session plays from. T4, T6, and T8 add their parts here and to Create.</summary>
public sealed record GameContent
{
    private GameContent(GameSettings settings, CharacterCatalog characters, ArchetypeCatalog archetypes, SynergySettings synergy, EncounterCatalog encounters, PackInfo pack, ConversationCatalog conversations, TownCatalog towns)
    {
        Settings = settings;
        Characters = characters;
        Archetypes = archetypes;
        Synergy = synergy;
        Encounters = encounters;
        Pack = pack;
        Conversations = conversations;
        Towns = towns;
    }

    public GameSettings Settings { get; }

    public CharacterCatalog Characters { get; }

    public ArchetypeCatalog Archetypes { get; }

    public SynergySettings Synergy { get; }

    public EncounterCatalog Encounters { get; }

    public ConversationCatalog Conversations { get; }

    public TownCatalog Towns { get; }

    /// <summary>Which content pack this came from; unnamed and not stand-in for content built in C#.</summary>
    public PackInfo Pack { get; }

    public static Result<GameContent> Create(GameSettings settings, ImmutableArray<CharacterDefinition> characters, ImmutableArray<ArchetypeDefinition> archetypes, SynergySettings? synergy = null, ImmutableArray<EncounterDefinition> encounters = default, PackInfo? pack = null, ImmutableArray<ConversationDefinition> conversations = default, ImmutableArray<TownDefinition> towns = default)
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
        else if (settings.PlayerName.Length > Saves.SaveConstants.MaxPlayerNameLength || settings.PlayerName.Any(char.IsControl))
        {
            details.Add(new RuleErrorDetail("settings/playerName", $"playerName must be at most {Saves.SaveConstants.MaxPlayerNameLength} characters with no control characters"));
        }

        if (settings.Lineup is null)
        {
            details.Add(new RuleErrorDetail("settings/lineup", "lineup must not be missing"));
        }
        else if (settings.Lineup.Min < 0 || settings.Lineup.Max < settings.Lineup.Min)
        {
            details.Add(new RuleErrorDetail("settings/lineup", "lineup needs 0 <= min <= max"));
        }

        if (settings.Display is null)
        {
            details.Add(new RuleErrorDetail("settings/display", "display must not be missing"));
        }

        if (settings.Tells is null)
        {
            details.Add(new RuleErrorDetail("settings/tells", "tells must not be missing"));
        }
        else if (!double.IsFinite(settings.Tells.ClearAt) || !double.IsFinite(settings.Tells.StrongAt) || settings.Tells.ClearAt < 0 || settings.Tells.StrongAt < settings.Tells.ClearAt)
        {
            details.Add(new RuleErrorDetail("settings/tells", "tells need 0 <= clearAt <= strongAt"));
        }

        if (settings.Demeanor is null)
        {
            details.Add(new RuleErrorDetail("settings/demeanor", "demeanor must not be missing"));
        }
        else if (!double.IsFinite(settings.Demeanor.WarmAt) || !double.IsFinite(settings.Demeanor.WaryAt) || !double.IsFinite(settings.Demeanor.ColdAt)
            || !(settings.Demeanor.ColdAt < settings.Demeanor.WaryAt && settings.Demeanor.WaryAt < settings.Demeanor.WarmAt))
        {
            details.Add(new RuleErrorDetail("settings/demeanor", "demeanor needs coldAt < waryAt < warmAt"));
        }

        if (!double.IsFinite(settings.Reach) || settings.Reach <= 0)
        {
            details.Add(new RuleErrorDetail("settings/reach", "reach must be a positive number"));
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

        if (catalog.IsOk && !settings.StartingParty.IsDefault)
        {
            var startingSeen = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < settings.StartingParty.Length; i++)
            {
                var id = settings.StartingParty[i];
                var path = "settings/startingParty/" + i.ToString(CultureInfo.InvariantCulture);
                if (id is null || !catalog.Value.TryGet(id, out _))
                {
                    details.Add(new RuleErrorDetail(path, $"unknown character: {id}"));
                }
                else if (!startingSeen.Add(id))
                {
                    details.Add(new RuleErrorDetail(path, $"duplicate character in startingParty: {id}"));
                }
            }
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
        EncounterCatalog? checkedEncounterCatalog = null;
        if (archetypeCatalog.IsOk && catalog.IsOk)
        {
            var checkedEncounters = EncounterCatalog.Create(encounters, archetypeCatalog.Value, catalog.Value);
            if (checkedEncounters.IsOk)
            {
                encounterCatalog = checkedEncounters.Value;
                checkedEncounterCatalog = checkedEncounters.Value;
                for (var i = 0; settings.Lineup is not null && !encounters.IsDefault && i < encounters.Length; i++)
                {
                    if (encounters[i].Lineup is not { } own)
                    {
                        continue;
                    }

                    var lineupPath = "encounters/" + i.ToString(CultureInfo.InvariantCulture) + "/lineup";
                    if (own.Min < 0 || own.Max < own.Min)
                    {
                        details.Add(new RuleErrorDetail(lineupPath + "/min", "a fight lineup needs 0 <= min <= max"));
                    }
                    else if (own.Min > settings.Lineup.Max)
                    {
                        details.Add(new RuleErrorDetail(lineupPath + "/min", $"a fight lineup min of {own.Min} is above the settings lineup max of {settings.Lineup.Max}, so the lineup could never be ready"));
                    }
                }
            }
            else
            {
                details.AddRange(checkedEncounters.Error.Details);
            }
        }

        var conversationCatalog = ConversationCatalog.Empty;
        if (catalog.IsOk)
        {
            var checkedConversations = ConversationCatalog.Create(conversations, catalog.Value, checkedEncounterCatalog);
            if (checkedConversations.IsOk)
            {
                conversationCatalog = checkedConversations.Value;
            }
            else
            {
                details.AddRange(checkedConversations.Error.Details);
            }
        }

        var townCatalog = TownCatalog.Empty;
        if (catalog.IsOk)
        {
            var checkedTowns = TownCatalog.Create(towns, catalog.Value, conversationCatalog, checkedEncounterCatalog);
            if (checkedTowns.IsOk)
            {
                townCatalog = checkedTowns.Value;
                if (settings.StartingTown is not null && !townCatalog.TryGet(settings.StartingTown, out _))
                {
                    details.Add(new RuleErrorDetail("settings/startingTown", $"unknown town: {settings.StartingTown}"));
                }
            }
            else
            {
                details.AddRange(checkedTowns.Error.Details);
            }
        }

        synergy ??= SynergySettings.None;
        details.AddRange(synergy.Validate());

        if (details.Count > 0)
        {
            return Result<GameContent>.Fail(
                new RuleError(ErrorCode.VALIDATION_ERROR, "Content is invalid: " + details[0].Message, details.ToImmutable()));
        }

        return Result<GameContent>.Ok(new GameContent(settings, catalog.Value, archetypeCatalog.Value, synergy, encounterCatalog, pack ?? PackInfo.Unnamed, conversationCatalog, townCatalog));
    }
}
