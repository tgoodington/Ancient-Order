using System.Collections.Immutable;
using AncientOrder.Rules.Common;

namespace AncientOrder.Rules.Combat;

/// <summary>TargetId is null for Evade; EnergySegments applies to Special only (fractional when an enemy spends all its energy).</summary>
public sealed record CombatAction(string CombatantId, ActionType Type, string? TargetId, double? EnergySegments);

public sealed record DefenseResult(DefenseType Type, bool Success, double DamageMultiplier);

public sealed record AttackResult(
    string AttackerId,
    string TargetId,
    double Damage,
    DefenseType DefenseType,
    DefenseResult DefenseOutcome,
    bool RankKO,
    bool Blindside,
    bool CrushingBlow,
    bool CounterChain);

/// <summary>One counter-attack in a Parry counter chain: the attacker's blow, the defender's reaction, and the damage it took.</summary>
public sealed record CounterExchange(string AttackerId, string DefenderId, DefenseType Defense, DefenseResult Outcome, double Damage);

/// <summary>CounterExchanges lists every counter-attack the action set off, in order. EvadeRecovery is the stamina an Evade actually restored (after the cap at max stamina); null for other actions.</summary>
public sealed record ActionResult(string CombatantId, ActionType Type, AttackResult? AttackResult)
{
    public ImmutableArray<CounterExchange> CounterExchanges { get; init; } = [];

    public double? EvadeRecovery { get; init; }
}

public sealed record GroupActionDeclaration(string LeaderId, string TargetId);

public enum GroupEnergyRequirement
{
    Full,
}

public sealed record GroupActionConfig(double DamageMultiplier, GroupEnergyRequirement EnergyRequirement);

public enum BlockDefenseKind
{
    BlockSuccess,
    BlockFailure,
}

public sealed record BlockDefenseResult(BlockDefenseKind Type, double DamageMultiplier);

/// <summary>One round: its actions and the end-of-round state, whose RoundHistory is empty (earlier rounds are not nested inside).</summary>
public sealed record RoundResult(int Round, ImmutableArray<ActionResult> Actions, CombatState StateSnapshot);

/// <summary>The fight's root state. Dice is the fight's own generator state, saved with the fight so a reload replays the same luck.</summary>
public sealed record CombatState(
    int Round,
    CombatPhase Phase,
    ImmutableArray<Combatant> PlayerParty,
    ImmutableArray<Combatant> EnemyParty,
    ImmutableArray<CombatAction> ActionQueue,
    ImmutableArray<RoundResult> RoundHistory,
    CombatStatus Status,
    DiceState Dice);
