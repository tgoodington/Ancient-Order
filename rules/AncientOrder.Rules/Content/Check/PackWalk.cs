using System.Collections.Immutable;
using System.Globalization;
using AncientOrder.Rules.Conditions;
using AncientOrder.Rules.State;

namespace AncientOrder.Rules.Content.Check;

/// <summary>Helpers shared by the analyses.</summary>
internal static class PackWalk
{
    public static string Num(int i) => i.ToString(CultureInfo.InvariantCulture);

    /// <summary>Every condition in the list and below it (all, any, not), with its rules path ("when/0/conditions/1").</summary>
    public static IEnumerable<(Condition Condition, string Path)> Walk(ImmutableArray<Condition> list, string path)
    {
        for (var i = 0; !list.IsDefault && i < list.Length; i++)
        {
            foreach (var found in Walk(list[i], path + "/" + Num(i)))
            {
                yield return found;
            }
        }
    }

    private static IEnumerable<(Condition Condition, string Path)> Walk(Condition condition, string path)
    {
        yield return (condition, path);
        switch (condition)
        {
            case Condition.All a:
                foreach (var found in Walk(a.Conditions, path + "/conditions"))
                {
                    yield return found;
                }

                break;
            case Condition.Any a:
                foreach (var found in Walk(a.Conditions, path + "/conditions"))
                {
                    yield return found;
                }

                break;
            case Condition.Negation n:
                foreach (var found in Walk(n.Inner, path + "/condition"))
                {
                    yield return found;
                }

                break;
        }
    }

    /// <summary>Every condition list in conversations and towns, in content order, with its rules path.</summary>
    public static IEnumerable<(ImmutableArray<Condition> List, string Path)> ConditionLists(GameContent content)
    {
        for (var c = 0; c < content.Conversations.Definitions.Length; c++)
        {
            var conversation = content.Conversations.Definitions[c];
            var cp = "conversations/" + Num(c);
            for (var e = 0; !conversation.Start.IsDefault && e < conversation.Start.Length; e++)
            {
                yield return (conversation.Start[e].When, cp + "/start/" + Num(e) + "/when");
            }

            for (var s = 0; s < conversation.Steps.Length; s++)
            {
                var step = conversation.Steps[s];
                var sp = cp + "/steps/" + Num(s);
                for (var l = 0; !step.Lines.IsDefault && l < step.Lines.Length; l++)
                {
                    yield return (step.Lines[l].When, sp + "/lines/" + Num(l) + "/when");
                }

                for (var d = 0; !step.Next.IsDefault && d < step.Next.Length; d++)
                {
                    yield return (step.Next[d].When, sp + "/next/" + Num(d) + "/when");
                }

                for (var k = 0; !step.Choices.IsDefault && k < step.Choices.Length; k++)
                {
                    var choice = step.Choices[k];
                    var kp = sp + "/choices/" + Num(k);
                    yield return (choice.When, kp + "/when");
                    yield return (choice.Requires, kp + "/requires");
                    for (var d = 0; !choice.Next.IsDefault && d < choice.Next.Length; d++)
                    {
                        yield return (choice.Next[d].When, kp + "/next/" + Num(d) + "/when");
                    }
                }
            }
        }

        for (var t = 0; t < content.Towns.Definitions.Length; t++)
        {
            var town = content.Towns.Definitions[t];
            var tp = "towns/" + Num(t);
            for (var p = 0; !town.People.IsDefault && p < town.People.Length; p++)
            {
                var pp = tp + "/people/" + Num(p);
                for (var l = 0; l < town.People[p].Placements.Length; l++)
                {
                    yield return (town.People[p].Placements[l].When, pp + "/placements/" + Num(l) + "/when");
                }

                for (var o = 0; !town.People[p].Opens.IsDefault && o < town.People[p].Opens.Length; o++)
                {
                    yield return (town.People[p].Opens[o].When, pp + "/opens/" + Num(o) + "/when");
                }
            }

            for (var s = 0; !town.Spots.IsDefault && s < town.Spots.Length; s++)
            {
                var sp = tp + "/spots/" + Num(s);
                yield return (town.Spots[s].When, sp + "/when");
                for (var o = 0; !town.Spots[s].Opens.IsDefault && o < town.Spots[s].Opens.Length; o++)
                {
                    yield return (town.Spots[s].Opens[o].When, sp + "/opens/" + Num(o) + "/when");
                }
            }
        }
    }
}
