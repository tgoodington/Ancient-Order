using AncientOrder.Rules.Common;
using AncientOrder.Rules.Conversations;

namespace AncientOrder.Rules.Session;

internal static class ConversationOperations
{
    public static Outcome<ConversationView> Start(SessionContext context, SessionSnapshot s, string? id)
    {
        var invalid = SessionValidation.IdString("conversationId", id);
        if (invalid is not null)
        {
            return Outcome.Refuse<ConversationView>(s, invalid);
        }

        if (s.Game is null)
        {
            return Outcome.NoActiveGame<ConversationView>(s);
        }

        if (s.Game.HandOff is not null)
        {
            return Outcome.Refuse<ConversationView>(s, ErrorCode.TOWN_NOT_ACTIVE, "The town is complete");
        }

        var started = ConversationRules.Start(context.Content, s.Game, id!, context.Now());
        if (!started.IsOk)
        {
            return Outcome.Refuse<ConversationView>(s, started.Error);
        }

        var view = ConversationView.From(context.Content, started.Value.Game);
        return view is null
            ? Outcome.Refuse<ConversationView>(s, ErrorCode.CONVERSATION_NOT_FOUND, $"Conversation not found: \"{id}\"")
            : Outcome.Change(s with { Game = started.Value.Game, LastChoice = null }, view);
    }

    public static Outcome<ChoiceReport> Choose(SessionContext context, SessionSnapshot s, string? choiceId)
    {
        var invalid = SessionValidation.IdString("choiceId", choiceId);
        if (invalid is not null)
        {
            return Outcome.Refuse<ChoiceReport>(s, invalid);
        }

        return s.Game is null
            ? Outcome.NoActiveGame<ChoiceReport>(s)
            : Commit(s, ConversationRules.Choose(context.Content, s.Game, choiceId!, context.Now()));
    }

    public static Outcome<ChoiceReport> Continue(SessionContext context, SessionSnapshot s) =>
        s.Game is null
            ? Outcome.NoActiveGame<ChoiceReport>(s)
            : Commit(s, ConversationRules.Continue(context.Content, s.Game, context.Now()));

    private static Outcome<ChoiceReport> Commit(SessionSnapshot s, Result<ConversationStep> step) =>
        step.IsOk
            ? Outcome.Change(s with { Game = step.Value.Game, LastChoice = step.Value.Report }, step.Value.Report)
            : Outcome.Refuse<ChoiceReport>(s, step.Error);
}
