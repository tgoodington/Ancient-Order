using System.Text;
using AncientOrder.Rules.Saves;
using Godot;

namespace AncientOrder.DevTools;

/// <summary>Dismissible list of what a load adjusted against changed content (kind and detail). Shown only when there are adjustments.</summary>
public partial class LoadReportNotice : PanelContainer
{
    private Label _list = null!;

    public override void _Ready()
    {
        _list = GetNode<Label>("Box/Scroll/List");
        GetNode<Button>("Box/Dismiss").Pressed += Hide;
        Visible = false;
    }

    public void Show(LoadReport report)
    {
        if (report.IsEmpty)
        {
            return;
        }

        var text = new StringBuilder();
        foreach (var adjustment in report.Adjustments)
        {
            text.Append(adjustment.Kind).Append(adjustment.Id is null ? string.Empty : $" [{adjustment.Id}]").Append(": ").Append(adjustment.Message).Append('\n');
        }

        _list.Text = text.ToString().TrimEnd('\n');
        Visible = true;
    }
}
