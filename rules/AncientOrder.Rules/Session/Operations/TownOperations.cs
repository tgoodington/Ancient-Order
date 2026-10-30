using AncientOrder.Rules.Common;
using AncientOrder.Rules.Town;

namespace AncientOrder.Rules.Session;

internal static class TownOperations
{
    public static Outcome<TownEvent> MovePlayer(SessionContext context, SessionSnapshot s, double x, double z)
    {
        if (!double.IsFinite(x) || !double.IsFinite(z))
        {
            return Outcome.Refuse<TownEvent>(s, ErrorCode.VALIDATION_ERROR, "x and z must be finite numbers");
        }

        if (s.Game is null)
        {
            return Outcome.NoActiveGame<TownEvent>(s);
        }

        if (s.Game.HandOff is not null)
        {
            return TownComplete(s);
        }

        return Commit(s, TownRules.MovePlayer(context.Content, s.Game, new Point(x, z), context.Now()));
    }

    public static Outcome<TownEvent> TriggerPerson(SessionContext context, SessionSnapshot s, string? characterId)
    {
        var invalid = SessionValidation.IdString("characterId", characterId);
        if (invalid is not null)
        {
            return Outcome.Refuse<TownEvent>(s, invalid);
        }

        if (s.Game is { HandOff: not null })
        {
            return TownComplete(s);
        }

        return s.Game is null
            ? Outcome.NoActiveGame<TownEvent>(s)
            : Commit(s, TownTriggers.TriggerPerson(context.Content, s.Game, characterId!, context.Now()));
    }

    public static Outcome<TownEvent> TriggerSpot(SessionContext context, SessionSnapshot s, string? spotId)
    {
        var invalid = SessionValidation.IdString("spotId", spotId);
        if (invalid is not null)
        {
            return Outcome.Refuse<TownEvent>(s, invalid);
        }

        if (s.Game is { HandOff: not null })
        {
            return TownComplete(s);
        }

        return s.Game is null
            ? Outcome.NoActiveGame<TownEvent>(s)
            : Commit(s, TownTriggers.TriggerSpot(context.Content, s.Game, spotId!, context.Now()));
    }

    private static Outcome<TownEvent> TownComplete(SessionSnapshot s) =>
        Outcome.Refuse<TownEvent>(s, ErrorCode.TOWN_NOT_ACTIVE, "The town is complete");

    private static Outcome<TownEvent> Commit(SessionSnapshot s, Result<TownStep> step) =>
        step.IsOk
            ? Outcome.Change(s with { Game = step.Value.Game, LastChoice = null }, step.Value.Event)
            : Outcome.Refuse<TownEvent>(s, step.Error);
}
