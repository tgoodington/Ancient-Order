using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;

namespace AncientOrder.Rules.Common;

[SuppressMessage("Naming", "CA1707", Justification = "Names mirror the TypeScript ErrorCodes exactly.")]
public enum ErrorCode
{
    GAME_NOT_FOUND,
    INVALID_SLOT,
    SAVE_NOT_FOUND,
    NPC_NOT_FOUND,
    TEAM_COMPOSITION_INVALID,
    VALIDATION_ERROR,
    INTERNAL_ERROR,
    NO_ACTIVE_COMBAT,
    COMBAT_INVALID_DECLARATION,
    ENCOUNTER_NOT_FOUND,
    COMBAT_ALREADY_ACTIVE,
    STORAGE_ERROR,
}

public sealed record RuleErrorDetail(string Path, string Message);

public sealed record RuleError(ErrorCode Code, string Message, ImmutableArray<RuleErrorDetail> Details)
{
    public RuleError(ErrorCode code, string message)
        : this(code, message, ImmutableArray<RuleErrorDetail>.Empty)
    {
    }
}
