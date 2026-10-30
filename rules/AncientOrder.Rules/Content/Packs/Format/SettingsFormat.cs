using System.Collections.Immutable;
using AncientOrder.Rules.Combat;

namespace AncientOrder.Rules.Content.Packs.Format;

public sealed record SettingsFormat(
    string PlayerName,
    LineupLimitsFormat Lineup,
    PersonalityFormat? StartingPersonality = null,
    CombatProfileFormat? PlayerCombat = null,
    ImmutableArray<string> StartingParty = default,
    DisplayFormat? Display = null,
    ConversationSettingsFormat? Conversations = null,
    TellsFormat? Tells = null,
    DemeanorFormat? Demeanor = null,
    string? StartingTown = null,
    double? Reach = null);

public sealed record TellsFormat(double ClearAt = 3, double StrongAt = 8);

public sealed record DemeanorFormat(double WarmAt = 50, double WaryAt = -20, double ColdAt = -60);

public sealed record ConversationSettingsFormat(string? LockedText = null);

public sealed record LineupLimitsFormat(int Min, int Max);

public sealed record DisplayFormat(bool PartyReadouts = true, bool SynergyNumbers = true);

public sealed record PersonalityFormat(
    double Patience,
    double Empathy,
    double Cunning,
    double Logic,
    double Kindness,
    double Charisma);

public sealed record CombatProfileFormat(
    double Rank,
    double Stamina,
    double Power,
    double Speed,
    ElementalPath ElementalPath,
    AscensionLevel? AscensionLevel = null);
