using AncientOrder.Rules.Common;
using AncientOrder.Rules.Content.Packs;
using AncientOrder.Rules.Conversations;
using AncientOrder.Rules.Effects;
using AncientOrder.Rules.State;

namespace AncientOrder.Rules.Content.Check;

/// <summary>
/// A step with choices needs one that is always available: no when, no requires, and not a hand-off (a hand-off is gated by the lineup).
/// Conditions cannot be proven always true without play, so an unconditioned choice is the ungated fallback.
/// </summary>
internal static class DeadEndCheck
{
    public static void Run(GameContent content, ContentProvenance provenance, List<RuleErrorDetail> problems)
    {
        for (var c = 0; c < content.Conversations.Definitions.Length; c++)
        {
            var conversation = content.Conversations.Definitions[c];
            for (var s = 0; s < conversation.Steps.Length; s++)
            {
                var step = conversation.Steps[s];
                if (step.Choices.IsDefaultOrEmpty || step.Choices.Any(IsFallback))
                {
                    continue;
                }

                problems.Add(new RuleErrorDetail(
                    provenance.Translate("conversations/" + PackWalk.Num(c) + "/steps/" + PackWalk.Num(s) + "/choices"),
                    $"dead end: conversation '{conversation.Id}' step '{step.Id}' has no choice that is always available (add a choice with no when and no requires)"));
            }
        }
    }

    private static bool IsFallback(ChoiceDefinition choice) =>
        choice.When.IsDefaultOrEmpty
        && choice.Requires.IsDefaultOrEmpty
        && (choice.Effects.IsDefault || !choice.Effects.Any(e => e is Effect.HandOff));
}
