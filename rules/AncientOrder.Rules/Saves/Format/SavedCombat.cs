using System.Collections.Immutable;
using AncientOrder.Rules.Combat;

namespace AncientOrder.Rules.Saves.Format;

/// <summary>A full mirror of CombatState. When the rules add a combat field, add it here and to the two combat methods in SaveMapper.</summary>
public sealed record SavedCombat(
    int Round,
    CombatPhase Phase,
    ImmutableArray<SavedCombatant> PlayerParty,
    ImmutableArray<SavedCombatant> EnemyParty,
    ImmutableArray<SavedCombatAction> ActionQueue,
    ImmutableArray<SavedRoundResult> RoundHistory,
    CombatStatus Status,
    SavedDice Dice);

/// <summary>The fight's generator state as four unsigned integers.</summary>
public sealed record SavedDice(ulong S0, ulong S1, ulong S2, ulong S3);

public sealed record SavedBuff(BuffStat Stat, string Source, double Modifier);

public sealed record SavedDefenseSkill(double SR, double SMR, double FMR);

public sealed record SavedReactionSkills(SavedDefenseSkill Block, SavedDefenseSkill Dodge, SavedDefenseSkill Parry);

public sealed record SavedCombatReactionProgress(double Block, double Dodge, double Parry);

public sealed record SavedCombatant(
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
    ImmutableArray<SavedBuff> ActiveBuffs,
    ElementalPath ElementalPath,
    SavedReactionSkills ReactionSkills,
    SavedCombatReactionProgress? ReactionProgress,
    bool IsKO);

public sealed record SavedCombatAction(string CombatantId, ActionType Type, string? TargetId, double? EnergySegments);

public sealed record SavedDefenseResult(DefenseType Type, bool Success, double DamageMultiplier);

public sealed record SavedAttackResult(
    string AttackerId,
    string TargetId,
    double Damage,
    DefenseType DefenseType,
    SavedDefenseResult DefenseOutcome,
    bool RankKO,
    bool Blindside,
    bool CrushingBlow,
    bool CounterChain);

public sealed record SavedCounterExchange(string AttackerId, string DefenderId, DefenseType Defense, SavedDefenseResult Outcome, double Damage);

public sealed record SavedActionResult(
    string CombatantId,
    ActionType Type,
    SavedAttackResult? AttackResult,
    ImmutableArray<SavedCounterExchange> CounterExchanges,
    double? EvadeRecovery);

public sealed record SavedRoundResult(int Round, ImmutableArray<SavedActionResult> Actions, SavedCombat StateSnapshot);
