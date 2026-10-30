using AncientOrder.Rules.Common;
using AncientOrder.Rules.Conversations;

namespace AncientOrder.Rules.Session;

public sealed partial class GameSession
{
    /// <summary>Starts a conversation at its opening step. Refused while one is running or during combat.</summary>
    public Result<ConversationView> StartConversation(string? id) =>
        Run((c, s) => ConversationOperations.Start(c, s, id));

    /// <summary>Takes a choice on the current step; the snapshot's LastChoice carries what it did.</summary>
    public Result<ChoiceReport> Choose(string? choiceId) =>
        Run((c, s) => ConversationOperations.Choose(c, s, choiceId));

    /// <summary>Moves on from a step that offers Continue.</summary>
    public Result<ChoiceReport> Continue() => Run(ConversationOperations.Continue);
}
