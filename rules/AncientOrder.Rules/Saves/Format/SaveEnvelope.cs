using System.Collections.Immutable;

namespace AncientOrder.Rules.Saves.Format;

/// <summary>The file's outer shape. Every collection is an array in a defined order; the rules records are never written directly.</summary>
public sealed record SaveEnvelope(
    string Format,
    int Version,
    string Kind,
    int Slot,
    long SavedAt,
    string PlayerName,
    SaveSummary Summary,
    SavedGame Game);

/// <summary>What the player was doing when the save was made.</summary>
public enum SaveSituation
{
    None,
    Town,
    Conversation,
    HandOff,
}

/// <summary>Written at save time so the slot list can describe the save without current content. TownName is null when there was no known town.</summary>
public sealed record SaveSummary(SaveSituation Situation, string? TownName = null);

/// <summary>
/// What changes in play. Characters are in catalog order, ReactionProgress sorted by id (ordinal). Party is in join order and Lineup in lineup order.
/// Memory is sorted (ordinal). Pending orders and the last fight outcome are not saved.
/// </summary>
public sealed record SavedGame(
    long Timestamp,
    SavedPlayer Player,
    ImmutableArray<SavedCharacter> Characters,
    ImmutableArray<string> Lineup,
    ImmutableArray<SavedReactionProgress> ReactionProgress,
    SavedCombat? Combat,
    ImmutableArray<string> Party,
    SavedTown? Town,
    SavedConversation? Conversation,
    SavedMemory Memory,
    SavedHandOff? HandOff);

public sealed record SavedTown(string Town, double X, double Z);

public sealed record SavedConversation(string Conversation, string Step);

/// <summary>Flags and seen conversations sorted ordinally; chosen sorted by conversation, then choice (ordinal).</summary>
public sealed record SavedMemory(ImmutableArray<string> Flags, ImmutableArray<SavedChoice> Chosen, ImmutableArray<string> Seen);

public sealed record SavedChoice(string Conversation, string Choice);

public sealed record SavedHandOff(string Fight, ImmutableArray<string> Lineup, string? Town);

public sealed record SavedPlayer(string Id, string Name, SavedPersonality Personality);

public sealed record SavedPersonality(
    double Patience,
    double Empathy,
    double Cunning,
    double Logic,
    double Kindness,
    double Charisma);

public sealed record SavedCharacter(string Id, double Affection, double Trust);

/// <summary>XP values are written as numbers and checked on load (finite, whole, 0 up to the int range) so a bad value gives a clear reason.</summary>
public sealed record SavedReactionProgress(string Id, double Block, double Dodge, double Parry);
