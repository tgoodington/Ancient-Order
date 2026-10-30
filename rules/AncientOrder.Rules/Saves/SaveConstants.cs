namespace AncientOrder.Rules.Saves;

public static class SaveConstants
{
    public const string FormatTag = "ancient-order-save";
    public const int CurrentVersion = 2;
    public const int MaxSaveBytes = 2_000_000;
    public const int MinSlot = 1;
    public const int MaxSlot = 10;

    /// <summary>'auto' is reserved for future autosaves; only 'manual' is used.</summary>
    public const string KindManual = "manual";

    /// <summary>The longest town name the summary keeps; a longer one is cut when written.</summary>
    public const int MaxTownNameLength = 200;

    /// <summary>The longest player name a save holds.</summary>
    public const int MaxPlayerNameLength = 200;

    public const int MaxReasons = 10;
    public const int MaxReasonLength = 200;

    /// <summary>The largest nesting a save may have. A fight's round history nests each round's snapshot, so the default of 64 is too small.</summary>
    public const int MaxJsonDepth = 512;

    public static bool IsValidSlot(int slot) => slot >= MinSlot && slot <= MaxSlot;
}
