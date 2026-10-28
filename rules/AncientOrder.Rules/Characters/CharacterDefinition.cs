
namespace AncientOrder.Rules.State;
/// <summary>A character's fixed definition from content; never changes during play (ADR-003). A character without Combat never fights.</summary>
public sealed record CharacterDefinition(
    string Id,
    string Name,
    string ArchetypeId,
    AncientOrder.Rules.Personality.Personality Personality,
    double StartingAffection,
    double StartingTrust,
    CombatProfile? Combat = null);
