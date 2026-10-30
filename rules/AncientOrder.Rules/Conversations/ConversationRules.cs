using System.Collections.Immutable;
using AncientOrder.Rules.Common;
using AncientOrder.Rules.Conditions;
using AncientOrder.Rules.Effects;
using AncientOrder.Rules.Reactions;
using AncientOrder.Rules.State;

namespace AncientOrder.Rules.Conversations;

/// <summary>The new game after a conversation action, and what the action did. For Start the report has no ChoiceId.</summary>
public sealed record ConversationStep(GameState Game, ChoiceReport Report);

/// <summary>Where conversations ask the condition system: what is shown, locked, opened, and taken.</summary>
internal static class ConversationGates
{
    public static bool StartHolds(GameContent content, GameState game, StartEntry entry) =>
        Conditions.Conditions.Holds(content, game, entry.When);

    public static bool LineShows(GameContent content, GameState game, LineDefinition line) =>
        Conditions.Conditions.Holds(content, game, line.When);

    public static bool ChoiceShows(GameContent content, GameState game, ChoiceDefinition choice) =>
        Conditions.Conditions.Holds(content, game, choice.When);

    public static bool ChoiceAvailable(GameContent content, GameState game, ChoiceDefinition choice) =>
        ExplainRequirements(content, game, choice).Met;

    /// <summary>
    /// The choice requirements explained, plus a lineupReady for the fight of a hand-off effect, so content never writes that condition.
    /// The hand-off happens after the effects before it, so its lineup is judged on the game after a dry run of those effects; a dry run
    /// that refuses locks the choice with its reason. This is the one source for both availability and the locked explanation.
    /// </summary>
    public static RequirementView.All ExplainRequirements(GameContent content, GameState game, ChoiceDefinition choice)
    {
        var own = (RequirementView.All)Conditions.Conditions.Explain(content, game, choice.Requires);
        var handOff = choice.Effects.IsDefaultOrEmpty ? null : choice.Effects.OfType<Effect.HandOff>().FirstOrDefault();
        if (handOff is null)
        {
            return own;
        }

        var preceding = choice.Effects.TakeWhile(e => e is not Effect.HandOff).ToImmutableArray();
        var dryRun = EffectRules.Apply(content, game, preceding, game.Timestamp);
        var implied = dryRun.IsOk
            ? ((RequirementView.All)Conditions.Conditions.Explain(content, dryRun.Value.Game, [new Condition.LineupReady(handOff.FightId)])).Children
            : ImmutableArray.Create<RequirementView>(new RequirementView.LineupReady(handOff.FightId, dryRun.Error.Message, false));
        var children = own.Children.AddRange(implied);
        return new RequirementView.All(children, children.All(c => c.Met));
    }

    public static bool DestinationHolds(GameContent content, GameState game, Destination destination) =>
        Conditions.Conditions.Holds(content, game, destination.When);
}

/// <summary>
/// Plays conversations from content. Pure: each function takes content, the game, and the time, and returns a new game or a refusal;
/// a refusal leaves the given game untouched. A choice applies completely or not at all.
/// </summary>
public static class ConversationRules
{
    public static Result<ConversationStep> Start(GameContent content, GameState game, string conversationId, long now)
    {
        if (game.Conversation is not null)
        {
            return Fail(ErrorCode.CONVERSATION_ALREADY_ACTIVE, "A conversation is already running");
        }

        if (game.Combat is not null)
        {
            return Fail(ErrorCode.COMBAT_ALREADY_ACTIVE, "A conversation cannot start during combat");
        }

        if (!content.Conversations.TryGet(conversationId, out var conversation))
        {
            return Fail(ErrorCode.CONVERSATION_NOT_FOUND, $"Conversation not found: \"{conversationId}\"");
        }

        // The opening step is chosen before the conversation counts as seen, so a start entry can tell a first visit from a return.
        var opened = GameStateUpdates.WithConversation(
            WithSeen(game, conversation.Id), new ActiveConversation(conversation.Id, OpeningStep(content, game, conversation)), now);
        return Result<ConversationStep>.Ok(new ConversationStep(opened, new ChoiceReport(conversation.Id, null, false, null)));
    }

    public static Result<ConversationStep> Choose(GameContent content, GameState game, string choiceId, long now)
    {
        if (!TryCurrent(content, game, out var conversation, out var step, out var failure))
        {
            return failure!;
        }

        var choice = step.Choices.IsDefault
            ? null
            : step.Choices.FirstOrDefault(c => c.Id == choiceId && ConversationGates.ChoiceShows(content, game, c) && ConversationGates.ChoiceAvailable(content, game, c));
        if (choice is null)
        {
            return Fail(ErrorCode.CHOICE_UNAVAILABLE, $"Choice not available: \"{choiceId}\"");
        }

        // Order within a choice: effects, then reactions (T4), then the choice is recorded, then the destination.
        var applied = EffectRules.Apply(content, game, Items(choice.Effects), now);
        if (!applied.IsOk)
        {
            return Result<ConversationStep>.Fail(applied.Error);
        }

        var reacted = ReactionRules.React(content, applied.Value.Game, conversation, choice, applied.Value.WrittenShift, now);
        if (!reacted.IsOk)
        {
            return Result<ConversationStep>.Fail(reacted.Error);
        }

        var report = new ChoiceReport(conversation.Id, choice.Id, false, null)
        {
            Reactions = reacted.Value.Reactions,
            TraitChanges = applied.Value.TraitChanges,
            WrittenShift = applied.Value.WrittenShift,
            RelationshipChanges = applied.Value.RelationshipChanges,
            Joined = applied.Value.Joined,
            Left = applied.Value.Left,
            FlagsSet = applied.Value.FlagsSet,
            FlagsCleared = applied.Value.FlagsCleared,
            HandOff = applied.Value.HandOff,
        };
        var recorded = reacted.Value.Game with { Memory = reacted.Value.Game.Memory.WithChosen(conversation.Id, choice.Id) };
        if (applied.Value.HandOff is not null)
        {
            return Result<ConversationStep>.Ok(new ConversationStep(recorded, report with { Ended = true }));
        }

        return Move(content, recorded, conversation, choice.Next, report, now);
    }

    /// <summary>Moves on from a step that offers Continue: it has no choices showing. Follows the step's own destinations.</summary>
    public static Result<ConversationStep> Continue(GameContent content, GameState game, long now)
    {
        if (!TryCurrent(content, game, out var conversation, out var step, out var failure))
        {
            return failure!;
        }

        if (OffersChoices(content, game, step))
        {
            return Fail(ErrorCode.CHOICE_UNAVAILABLE, "This step has choices; pick one");
        }

        return Move(content, game, conversation, step.Next, new ChoiceReport(conversation.Id, null, false, null), now);
    }

    /// <summary>The first start entry whose condition holds, or else the first step.</summary>
    public static string OpeningStep(GameContent content, GameState game, ConversationDefinition conversation)
    {
        if (!conversation.Start.IsDefault)
        {
            foreach (var entry in conversation.Start)
            {
                if (ConversationGates.StartHolds(content, game, entry))
                {
                    return entry.StepId;
                }
            }
        }

        return conversation.Steps[0].Id;
    }

    /// <summary>True when the step has at least one choice showing; otherwise the step offers Continue.</summary>
    public static bool OffersChoices(GameContent content, GameState game, StepDefinition step) =>
        !step.Choices.IsDefault && step.Choices.Any(c => ConversationGates.ChoiceShows(content, game, c));

    private static Result<ConversationStep> Move(
        GameContent content, GameState game, ConversationDefinition conversation, ImmutableArray<Destination> next, ChoiceReport report, long now)
    {
        var destination = next.IsDefault ? null : next.FirstOrDefault(d => ConversationGates.DestinationHolds(content, game, d));
        switch (destination)
        {
            case Destination.ToStep to:
                return Result<ConversationStep>.Ok(new ConversationStep(
                    GameStateUpdates.WithConversation(game, new ActiveConversation(conversation.Id, to.StepId), now),
                    report));
            case Destination.ToConversation to:
                if (!content.Conversations.TryGet(to.ConversationId, out var target))
                {
                    return Fail(ErrorCode.CONVERSATION_NOT_FOUND, $"Conversation not found: \"{to.ConversationId}\"");
                }

                return Result<ConversationStep>.Ok(new ConversationStep(
                    GameStateUpdates.WithConversation(WithSeen(game, target.Id), new ActiveConversation(target.Id, OpeningStep(content, game, target)), now),
                    report with { Ended = true, NextConversationId = target.Id }));
            default:
                return Result<ConversationStep>.Ok(new ConversationStep(
                    GameStateUpdates.WithConversation(game, null, now),
                    report with { Ended = true }));
        }
    }

    private static bool TryCurrent(
        GameContent content,
        GameState game,
        out ConversationDefinition conversation,
        out StepDefinition step,
        out Result<ConversationStep>? failure)
    {
        conversation = null!;
        step = null!;
        failure = null;
        if (game.Conversation is not { } active)
        {
            failure = Fail(ErrorCode.NO_ACTIVE_CONVERSATION, "No active conversation");
            return false;
        }

        if (!content.Conversations.TryGet(active.ConversationId, out conversation))
        {
            failure = Fail(ErrorCode.CONVERSATION_NOT_FOUND, $"Conversation not found: \"{active.ConversationId}\"");
            return false;
        }

        var found = conversation.Steps.FirstOrDefault(s => s.Id == active.StepId);
        if (found is null)
        {
            failure = Fail(ErrorCode.CONVERSATION_NOT_FOUND, $"Step not found: \"{active.StepId}\"");
            return false;
        }

        step = found;
        return true;
    }

    private static GameState WithSeen(GameState game, string conversationId) =>
        game with { Memory = game.Memory.WithSeen(conversationId) };

    private static ImmutableArray<Effect> Items(ImmutableArray<Effect> effects) => effects.IsDefault ? [] : effects;

    private static Result<ConversationStep> Fail(ErrorCode code, string message) =>
        Result<ConversationStep>.Fail(new RuleError(code, message));
}
