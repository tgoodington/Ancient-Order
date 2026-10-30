using System.Collections.Immutable;
using AncientOrder.Rules.Common;
using Godot;

namespace AncientOrder.Ui;

/// <summary>
/// Shown when the content pack fails its check. Release builds say only that the content failed to load, with Quit.
/// Problems holds the full list and DetailSlot is an empty container under the message; the dev tools (T37) fill it in debug builds.
/// </summary>
public partial class ContentErrorScreen : Control
{
    private Button _quit = null!;

    public ImmutableArray<RuleErrorDetail> Problems { get; private set; } = ImmutableArray<RuleErrorDetail>.Empty;

    public Control DetailSlot { get; private set; } = null!;

    public override void _Ready()
    {
        DetailSlot = GetNode<Control>("Center/Panel/Content/DetailSlot");
        _quit = GetNode<Button>("Center/Panel/Content/Quit");
        _quit.Pressed += () => GetTree().Quit();
    }

    public void ShowProblems(ImmutableArray<RuleErrorDetail> problems) => Problems = problems.IsDefault ? ImmutableArray<RuleErrorDetail>.Empty : problems;

    public void FocusFirst() => _quit.GrabFocus();
}
