using AncientOrder.Rules.Common;
using AncientOrder.Rules.State;

namespace AncientOrder.Rules.Session;

/// <summary>What the operations know: content, clock, ids, and seeds. Rules functions get these as parameters.</summary>
public sealed record SessionContext(GameContent Content, TimeProvider Clock, IIdSource Ids, ISeedSource Seeds)
{
    /// <summary>Unix ms, as TypeScript Date.now().</summary>
    public long Now() => Clock.GetUtcNow().ToUnixTimeMilliseconds();
}

/// <summary>
/// The next snapshot plus the result. Returning the same snapshot instance means nothing changed,
/// so the session commits and notifies no one.
/// </summary>
public sealed record Outcome<T>(SessionSnapshot Next, Result<T> Result);

public static class Outcome
{
    public static Outcome<T> Read<T>(SessionSnapshot current, T value) =>
        new(current, Result<T>.Ok(value));

    public static Outcome<T> Change<T>(SessionSnapshot next, T value) =>
        new(next, Result<T>.Ok(value));

    public static Outcome<T> Refuse<T>(SessionSnapshot current, RuleError error) =>
        new(current, Result<T>.Fail(error));

    public static Outcome<T> Refuse<T>(SessionSnapshot current, ErrorCode code, string message) =>
        Refuse<T>(current, new RuleError(code, message));

    public static Outcome<T> NoActiveGame<T>(SessionSnapshot current) =>
        Refuse<T>(current, ErrorCode.GAME_NOT_FOUND, "No active game");
}
