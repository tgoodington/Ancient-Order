using System.Collections.Immutable;

namespace AncientOrder.Rules.Combat;

/// <summary>An active buff (positive Modifier) or debuff (negative); permanent for the fight. Applies once per (Stat, Source).</summary>
public sealed record Buff(BuffStat Stat, string Source, double Modifier);

/// <summary>One fighter. ArchetypeId is null for the player's own fighter. ReactionProgress is null for enemies. A knocked-out fighter has IsKO set and Stamina 0.</summary>
public sealed record Combatant(
    string Id,
    string Name,
    string? ArchetypeId,
    double Rank,
    double Stamina,
    double MaxStamina,
    double Power,
    double Speed,
    double Energy,
    double MaxEnergy,
    AscensionLevel AscensionLevel,
    ImmutableArray<Buff> ActiveBuffs,
    ElementalPath ElementalPath,
    ReactionSkills ReactionSkills,
    ReactionProgress? ReactionProgress,
    bool IsKO);
