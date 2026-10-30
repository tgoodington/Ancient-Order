using System.Collections.Immutable;
using AncientOrder.Rules.Party;

namespace AncientOrder.Rules.State;

/// <summary>What the party screen may show: PartyReadouts is the party members' personality and combat readouts, SynergyNumbers is each bonus's name, stat, step, and value.</summary>
public sealed record DisplaySettings(bool PartyReadouts = true, bool SynergyNumbers = true);

/// <summary>
/// StartingPersonality missing means the default personality. Without PlayerCombat the player character does not fight (T8).
/// StartingParty is who is in the party when a new game starts, in order (empty by default). Lineup is the fight lineup's size limits.
/// </summary>
/// <summary>LockedText is the in-world line shown for a locked choice that has none of its own.</summary>
public sealed record ConversationSettings(string LockedText = ConversationSettings.DefaultLockedText)
{
    public const string DefaultLockedText = "That is not open to you yet.";
}

/// <summary>Cutoffs on a reaction's size (the larger of its intended affection and trust change): below ClearAt is slight, at or above StrongAt is strong.</summary>
public sealed record TellSettings(double ClearAt = 3, double StrongAt = 8);

/// <summary>Cutoffs on affection plus trust: Warm at or above WarmAt, Cold at or below ColdAt, Wary at or below WaryAt. ColdAt &lt; WaryAt &lt; WarmAt.</summary>
public sealed record DemeanorSettings(double WarmAt = 50, double WaryAt = -20, double ColdAt = -60);

public sealed record GameSettings(
    string PlayerName,
    LineupLimits Lineup,
    AncientOrder.Rules.Personality.Personality? StartingPersonality = null,
    CombatProfile? PlayerCombat = null)
{
    public ImmutableArray<string> StartingParty { get; init; } = [];

    public DisplaySettings Display { get; init; } = new();

    public ConversationSettings Conversations { get; init; } = new();

    public TellSettings Tells { get; init; } = new();

    public DemeanorSettings Demeanor { get; init; } = new();

    /// <summary>The town a new game starts in (its start point); null means no town.</summary>
    public string? StartingTown { get; init; }

    /// <summary>How close, in meters, the player must be to a person or spot to use it. Positive. A town may set its own.</summary>
    public double Reach { get; init; } = DefaultReach;

    public const double DefaultReach = 2.0;
}
