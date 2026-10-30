using System.Collections.Immutable;

namespace AncientOrder.Ui;

/// <summary>
/// What the previous choice or Continue did, as the conversation panel shows it: the player's line (none for Continue), the characters' body-language lines
/// in report order, and plain party notices. Presentation only; it holds no numbers (ADR-075).
/// </summary>
public sealed record ConversationOutcome(
    string? PlayerName,
    string? PlayerLine,
    ImmutableArray<string> BodyLines,
    ImmutableArray<string> Notices)
{
    public bool IsEmpty => string.IsNullOrEmpty(PlayerLine) && BodyLines.IsDefaultOrEmpty && Notices.IsDefaultOrEmpty;
}
