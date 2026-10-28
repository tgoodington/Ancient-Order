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
    SavedGame Game);

/// <summary>What changes in play. Characters are in catalog order, ReactionProgress sorted by id (ordinal). Pending orders and the last fight outcome are not saved.</summary>
public sealed record SavedGame(
    long Timestamp,
    SavedPlayer Player,
    ImmutableArray<SavedCharacter> Characters,
    ImmutableArray<string> Lineup,
    ImmutableArray<SavedReactionProgress> ReactionProgress,
    SavedCombat? Combat);

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
