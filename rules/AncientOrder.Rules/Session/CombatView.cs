using System.Collections.Immutable;
using AncientOrder.Rules.Combat;

namespace AncientOrder.Rules.Session;

public enum FighterStance
{
    Active,
    KO,
}

/// <summary>What a screen may show of one fighter. Targeting is the target of that fighter's pending player order, or null.</summary>
public sealed record FighterView(string Id, double Stamina, double StaminaPct, FighterStance Stance, string? Targeting);

/// <summary>The player-facing view of a fight, player side first. Enemy decisions are never part of it.</summary>
public sealed record CombatView(
    int Round,
    CombatPhase Phase,
    CombatStatus Status,
    ImmutableArray<FighterView> Fighters,
    ImmutableArray<CombatAction> PendingOrders);

public sealed record StartEncounterResult(string EncounterId, int Round, CombatStatus Status);

/// <summary>One order's verdict. Error is set only when Valid is false.</summary>
public sealed record DeclarationEntry(string CombatantId, ActionType Type, string? TargetId, bool Valid, string? Error);

public sealed record DeclarationResult(ImmutableArray<DeclarationEntry> Accepted, ImmutableArray<DeclarationEntry> Rejected);

public enum CombatResultStatus
{
    Active,
    Victory,
    Defeat,
}

public sealed record CombatResultView(CombatResultStatus Status, int Round);
