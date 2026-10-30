using System.Collections.Immutable;
using AncientOrder.Rules.Common;
using AncientOrder.Rules.State;

namespace AncientOrder.Rules.Content.Check;

/// <summary>
/// What the content check found. Problems fail the pack and Warnings never do; each detail's Path is "file:/json/pointer" as in the loader.
/// Content is the pack's content, present only when the pack has no problems.
/// </summary>
public sealed record ContentCheckReport(GameContent? Content, ImmutableArray<RuleErrorDetail> Problems, ImmutableArray<RuleErrorDetail> Warnings)
{
    public bool Passed => Problems.IsDefaultOrEmpty;
}
