
namespace AncientOrder.Rules.State;
/// <summary>StartingPersonality missing means the default personality. Without PlayerCombat the player character does not fight (T8).</summary>
public sealed record GameSettings(
    string PlayerName,
    int LineupSize,
    AncientOrder.Rules.Personality.Personality? StartingPersonality = null,
    CombatProfile? PlayerCombat = null);
