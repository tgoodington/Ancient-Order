using AncientOrder.Rules.Common;
using AncientOrder.Rules.Content.Packs;
using AncientOrder.Rules.Conversations;
using AncientOrder.Rules.State;

namespace AncientOrder.Rules.Content.Check;

/// <summary>
/// Warnings only. A step is reachable from its conversation's first step, its start entries, and the step destinations of reachable steps.
/// A conversation is used when some opens entry or some destination starts it. Conditions are not evaluated: every branch counts.
/// </summary>
internal static class ReachabilityCheck
{
    public static void Run(GameContent content, ContentProvenance provenance, List<RuleErrorDetail> warnings)
    {
        var started = new HashSet<string>(StringComparer.Ordinal);
        foreach (var conversation in content.Conversations.Definitions)
        {
            foreach (var step in conversation.Steps)
            {
                AddConversations(step.Next, started);
                foreach (var choice in step.Choices.IsDefault ? [] : step.Choices)
                {
                    AddConversations(choice.Next, started);
                }
            }
        }

        foreach (var town in content.Towns.Definitions)
        {
            foreach (var person in town.People.IsDefault ? [] : town.People)
            {
                AddOpens(person.Opens, started);
            }

            foreach (var spot in town.Spots.IsDefault ? [] : town.Spots)
            {
                AddOpens(spot.Opens, started);
            }
        }

        for (var c = 0; c < content.Conversations.Definitions.Length; c++)
        {
            var conversation = content.Conversations.Definitions[c];
            var cp = "conversations/" + PackWalk.Num(c);
            if (!started.Contains(conversation.Id))
            {
                warnings.Add(new RuleErrorDetail(provenance.Translate(cp), $"conversation '{conversation.Id}' is never started by any person, spot, or destination"));
            }

            var reached = ReachableSteps(conversation);
            for (var s = 0; s < conversation.Steps.Length; s++)
            {
                if (!reached.Contains(conversation.Steps[s].Id))
                {
                    warnings.Add(new RuleErrorDetail(
                        provenance.Translate(cp + "/steps/" + PackWalk.Num(s)),
                        $"conversation '{conversation.Id}' step '{conversation.Steps[s].Id}' cannot be reached from the conversation's start or any other step"));
                }
            }
        }
    }

    private static HashSet<string> ReachableSteps(ConversationDefinition conversation)
    {
        var byId = new Dictionary<string, StepDefinition>(StringComparer.Ordinal);
        foreach (var step in conversation.Steps)
        {
            byId[step.Id] = step;
        }

        var reached = new HashSet<string>(StringComparer.Ordinal);
        var pending = new Stack<string>();
        pending.Push(conversation.Steps[0].Id);
        foreach (var entry in conversation.Start.IsDefault ? [] : conversation.Start)
        {
            pending.Push(entry.StepId);
        }

        while (pending.Count > 0)
        {
            var id = pending.Pop();
            if (!reached.Add(id) || !byId.TryGetValue(id, out var step))
            {
                continue;
            }

            foreach (var destination in step.Next.IsDefault ? [] : step.Next)
            {
                Push(destination, pending);
            }

            foreach (var choice in step.Choices.IsDefault ? [] : step.Choices)
            {
                foreach (var destination in choice.Next.IsDefault ? [] : choice.Next)
                {
                    Push(destination, pending);
                }
            }
        }

        return reached;
    }

    private static void Push(Destination destination, Stack<string> pending)
    {
        if (destination is Destination.ToStep to)
        {
            pending.Push(to.StepId);
        }
    }

    private static void AddConversations(System.Collections.Immutable.ImmutableArray<Destination> next, HashSet<string> started)
    {
        foreach (var destination in next.IsDefault ? [] : next)
        {
            if (destination is Destination.ToConversation to)
            {
                started.Add(to.ConversationId);
            }
        }
    }

    private static void AddOpens(System.Collections.Immutable.ImmutableArray<Town.OpensEntry> opens, HashSet<string> started)
    {
        foreach (var entry in opens.IsDefault ? [] : opens)
        {
            if (entry.ConversationId is not null)
            {
                started.Add(entry.ConversationId);
            }
        }
    }
}
