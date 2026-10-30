using System.Collections.Immutable;
using AncientOrder.Rules.Party;

namespace AncientOrder.Rules.Combat;

/// <summary>One enemy as content lists it. A missing AscensionLevel means level 0. ReactionSkills are literal: enemies carry no reaction progress.</summary>
public sealed record CombatantConfig(
    string Id,
    string Name,
    string Archetype,
    double Rank,
    double Stamina,
    double Power,
    double Speed,
    ElementalPath ElementalPath,
    ReactionSkills ReactionSkills,
    AscensionLevel? AscensionLevel = null);

/// <summary>
/// An encounter is content and lists the enemies; the player's side comes from the player and the lineup. Lineup, when given, is the
/// lineup size this fight asks for, in place of the settings limits.
/// </summary>
public sealed record EncounterDefinition(string Id, string Name, ImmutableArray<CombatantConfig> Enemies)
{
    public LineupLimits? Lineup { get; init; }
}
