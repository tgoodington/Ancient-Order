namespace AncientOrder.Rules.Content.Packs.Format;

public sealed record CharacterFormat(
    string Id,
    string Name,
    string ArchetypeId,
    PersonalityFormat Personality,
    double StartingAffection,
    double StartingTrust,
    CombatProfileFormat? Combat = null);
