using AncientOrder.Rules.Common;
using AncientOrder.Rules.Conditions;
using AncientOrder.Rules.Content.Packs;
using AncientOrder.Rules.Effects;
using AncientOrder.Rules.State;

namespace AncientOrder.Rules.Content.Check;

/// <summary>A flag read by a condition but set by no setFlag effect anywhere in the pack fails (a typo would silently lock content); a flag set but never read only warns.</summary>
internal static class FlagCheck
{
    public static void Run(GameContent content, ContentProvenance provenance, List<RuleErrorDetail> problems, List<RuleErrorDetail> warnings)
    {
        var sets = new List<(string Flag, string Path)>();
        for (var c = 0; c < content.Conversations.Definitions.Length; c++)
        {
            var conversation = content.Conversations.Definitions[c];
            for (var s = 0; s < conversation.Steps.Length; s++)
            {
                var choices = conversation.Steps[s].Choices;
                for (var k = 0; !choices.IsDefault && k < choices.Length; k++)
                {
                    var effects = choices[k].Effects;
                    for (var e = 0; !effects.IsDefault && e < effects.Length; e++)
                    {
                        if (effects[e] is Effect.SetFlag set)
                        {
                            sets.Add((set.FlagId, "conversations/" + PackWalk.Num(c) + "/steps/" + PackWalk.Num(s) + "/choices/" + PackWalk.Num(k) + "/effects/" + PackWalk.Num(e)));
                        }
                    }
                }
            }
        }

        var reads = new List<(string Flag, string Path)>();
        foreach (var (list, path) in PackWalk.ConditionLists(content))
        {
            foreach (var (condition, conditionPath) in PackWalk.Walk(list, path))
            {
                if (condition is Condition.Flag flag)
                {
                    reads.Add((flag.FlagId, conditionPath));
                }
            }
        }

        var setFlags = new HashSet<string>(sets.Select(s => s.Flag), StringComparer.Ordinal);
        var readFlags = new HashSet<string>(reads.Select(r => r.Flag), StringComparer.Ordinal);
        foreach (var (flag, path) in reads)
        {
            if (!setFlags.Contains(flag))
            {
                problems.Add(new RuleErrorDetail(provenance.Translate(path), $"flag '{flag}' is read here but nothing in the pack sets it"));
            }
        }

        foreach (var (flag, path) in sets)
        {
            if (!readFlags.Contains(flag))
            {
                warnings.Add(new RuleErrorDetail(provenance.Translate(path), $"flag '{flag}' is set here but nothing reads it"));
            }
        }
    }
}
