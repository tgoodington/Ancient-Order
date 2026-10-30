using System.Collections.Immutable;
using AncientOrder.Rules.Combat;

namespace AncientOrder.Rules.Content.Packs.Format;

/// <summary>A fight (encounter) lists the enemies and, optionally, the lineup size it asks for in place of the settings limits.</summary>
public sealed record FightFormat(string Id, string Name, ImmutableArray<EnemyFormat> Enemies, LineupLimitsFormat? Lineup = null);

public sealed record EnemyFormat(
    string Id,
    string Name,
    string Archetype,
    double Rank,
    double Stamina,
    double Power,
    double Speed,
    ElementalPath ElementalPath,
    ReactionSkillsFormat ReactionSkills,
    AscensionLevel? AscensionLevel = null);

public sealed record ReactionSkillsFormat(DefenseSkillFormat Block, DefenseSkillFormat Dodge, DefenseSkillFormat Parry);

public sealed record DefenseSkillFormat(double Sr, double Smr, double Fmr);
