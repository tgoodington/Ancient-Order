using System.Collections.Immutable;

namespace AncientOrder.Rules.Content.Packs.Format;

/// <summary>A content file. Every section is optional here; any file may hold any section, and sections merge in index order. Towns are listed under "towns".</summary>
public sealed record ContentFile(
    SettingsFormat? Settings = null,
    SynergyFormat? Synergy = null,
    ImmutableArray<CharacterFormat> Characters = default,
    ImmutableArray<ArchetypeFormat> Archetypes = default,
    ImmutableArray<FightFormat> Fights = default,
    ImmutableArray<ConversationFormat> Conversations = default,
    ImmutableArray<TownFormat> Towns = default);
