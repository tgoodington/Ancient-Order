using AncientOrder.Rules.State;

namespace AncientOrder.Rules.Session;

internal static class GameOperations
{
    /// <summary>Starts a fresh game from content. Clears pending orders and the last combat outcome.</summary>
    public static Outcome<GameState> NewGame(SessionContext context, SessionSnapshot s)
    {
        var game = GameStateUpdates.NewGame(context.Content, "player_" + context.Ids.NextId(), context.Now());
        return Outcome.Change(SessionSnapshot.Initial with { Version = s.Version, Game = game }, game);
    }

    public static Outcome<GameState> GetState(SessionContext context, SessionSnapshot s) =>
        s.Game is null ? Outcome.NoActiveGame<GameState>(s) : Outcome.Read(s, s.Game);
}
