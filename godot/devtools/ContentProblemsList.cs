using System.Text;
using AncientOrder.Rules.Common;
using AncientOrder.Rules.Content.Check;
using Godot;

namespace AncientOrder.DevTools;

/// <summary>Every content-check problem and warning as 'file:location: message', for the content error screen.</summary>
public partial class ContentProblemsList : VBoxContainer
{
    public void Fill(ContentCheckReport report)
    {
        GetNode<Label>("Heading").Text = $"Content failed the check: {report.Problems.Length} problem(s), {report.Warnings.Length} warning(s)";
        var text = new StringBuilder();
        foreach (var problem in report.Problems)
        {
            Append(text, "PROBLEM", problem);
        }

        foreach (var warning in report.Warnings)
        {
            Append(text, "warning", warning);
        }

        GetNode<Label>("Scroll/List").Text = text.ToString().TrimEnd('\n');
    }

    private static void Append(StringBuilder text, string kind, RuleErrorDetail detail) => text.Append(kind).Append("  ").Append(detail.Path).Append(": ").Append(detail.Message).Append('\n');
}
