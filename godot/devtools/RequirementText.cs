using System.Globalization;
using System.Text;
using AncientOrder.Rules.Conditions;

namespace AncientOrder.DevTools;

/// <summary>Plain text for a RequirementView tree, with thresholds and current values: 'Empathy 25%+ (you: 22%) - unmet'. All, any and not nest by indent. Dev tools only.</summary>
public static class RequirementText
{
    public static string Format(RequirementView view)
    {
        var builder = new StringBuilder();
        Write(builder, view, 0);
        return builder.ToString().TrimEnd('\n');
    }

    public static string Num(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);

    private static void Write(StringBuilder builder, RequirementView view, int depth)
    {
        var pad = new string(' ', depth * 2);
        switch (view)
        {
            case RequirementView.All all:
                builder.Append(pad).Append("all of").Append(Mark(all.Met)).Append('\n');
                foreach (var child in all.Children)
                {
                    Write(builder, child, depth + 1);
                }

                break;
            case RequirementView.Any any:
                builder.Append(pad).Append("any of").Append(Mark(any.Met)).Append('\n');
                foreach (var child in any.Children)
                {
                    Write(builder, child, depth + 1);
                }

                break;
            case RequirementView.Negation not:
                builder.Append(pad).Append("not").Append(Mark(not.Met)).Append('\n');
                Write(builder, not.Child, depth + 1);
                break;
            default:
                builder.Append(pad).Append(Leaf(view)).Append(Mark(view.Met)).Append('\n');
                break;
        }
    }

    private static string Mark(bool met) => met ? " - met" : " - unmet";

    private static string Leaf(RequirementView view) => view switch
    {
        RequirementView.Flag f => $"flag {f.FlagId}",
        RequirementView.Chose c => $"chose {c.ConversationId}/{c.ChoiceId}",
        RequirementView.Seen s => $"seen {s.ConversationId}",
        RequirementView.TraitRange t => $"{t.Trait} {Range(t.AtLeast, t.AtMost, "%")} (you: {Num(t.Current)}%)",
        RequirementView.RelationshipRange r => $"{r.CharacterName} {r.Measure.ToString().ToLowerInvariant()} {Range(r.AtLeast, r.AtMost, string.Empty)} (now: {Num(r.Current)})",
        RequirementView.InParty p => $"{p.CharacterName} in party",
        RequirementView.LineupReady l => l.Reason is null ? "lineup ready" : $"lineup ready (not: {l.Reason})",
        _ => view.GetType().Name,
    };

    private static string Range(double? atLeast, double? atMost, string unit) => (atLeast, atMost) switch
    {
        ({ } lo, { } hi) => $"{Num(lo)}{unit} to {Num(hi)}{unit}",
        ({ } lo, null) => $"{Num(lo)}{unit}+",
        (null, { } hi) => $"up to {Num(hi)}{unit}",
        _ => "any",
    };
}
