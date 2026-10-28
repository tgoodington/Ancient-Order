using System.Collections.Immutable;
using AncientOrder.Rules.Party;
using AncientOrder.Rules.Personality;
using AncientOrder.Rules.State;

namespace AncientOrder.Rules.Session;

internal static class PlayerOperations
{
    public static Outcome<PlayerCharacter> GetPlayer(SessionContext context, SessionSnapshot s) =>
        s.Game is null ? Outcome.NoActiveGame<PlayerCharacter>(s) : Outcome.Read(s, s.Game.Player);

    public static Outcome<AncientOrder.Rules.Personality.Personality> GetPersonality(SessionContext context, SessionSnapshot s) =>
        s.Game is null
            ? Outcome.NoActiveGame<AncientOrder.Rules.Personality.Personality>(s)
            : Outcome.Read(s, s.Game.Player.Personality);

    public static Outcome<GameState> AdjustPersonality(SessionContext context, SessionSnapshot s, PersonalityAdjustment? adjustment)
    {
        var invalid = SessionValidation.PersonalityAdjustment(adjustment);
        if (invalid is not null)
        {
            return Outcome.Refuse<GameState>(s, invalid);
        }

        if (s.Game is null)
        {
            return Outcome.NoActiveGame<GameState>(s);
        }

        var game = GameStateUpdates.AdjustPlayerPersonality(s.Game, adjustment!, context.Now());
        return Outcome.Change(s with { Game = game }, game);
    }

    public static Outcome<ImmutableArray<string>> SetLineup(SessionContext context, SessionSnapshot s, ImmutableArray<string?> ids)
    {
        var invalid = SessionValidation.LineupIds(ids, context.Content.Settings.LineupSize);
        if (invalid is not null)
        {
            return Outcome.Refuse<ImmutableArray<string>>(s, invalid);
        }

        if (s.Game is null)
        {
            return Outcome.NoActiveGame<ImmutableArray<string>>(s);
        }

        var result = LineupRules.Set(context.Content, s.Game, ids.Select(id => id!).ToImmutableArray(), context.Now());
        return result.IsOk
            ? Outcome.Change(s with { Game = result.Value }, result.Value.Lineup)
            : Outcome.Refuse<ImmutableArray<string>>(s, result.Error);
    }
}
