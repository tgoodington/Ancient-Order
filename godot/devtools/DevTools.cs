using AncientOrder.Rules.Content.Check;
using AncientOrder.Ui;
using Godot;

namespace AncientOrder.DevTools;

/// <summary>
/// Designer-only tools (never shipped). Install adds a layer above the game and wires the overlay, the load notice and the content-problem list to ScreenFlow's read-only events.
/// Nothing here calls a session method that changes state. Compiled only under AO_DEVTOOLS (see AncientOrder.csproj).
/// </summary>
public static class DevTools
{
    public const string OverlayScene = "res://devtools/DevOverlay.tscn";
    public const string LoadNoticeScene = "res://devtools/LoadReportNotice.tscn";
    public const string ProblemsScene = "res://devtools/ContentProblemsList.tscn";

    public static void Install(ScreenFlow flow)
    {
        var layer = new CanvasLayer { Name = "DevToolsLayer", Layer = 100 };
        flow.AddChild(layer);

        var overlay = GD.Load<PackedScene>(OverlayScene).Instantiate<DevOverlay>();
        layer.AddChild(overlay);
        overlay.Bind(flow);

        var notice = GD.Load<PackedScene>(LoadNoticeScene).Instantiate<LoadReportNotice>();
        layer.AddChild(notice);

        flow.LoadCompleted += report => notice.Show(report);
        flow.ContentChecked += report => ShowProblems(flow, report);
    }

    private static void ShowProblems(ScreenFlow flow, ContentCheckReport report)
    {
        if (report.Passed || FindErrorScreen(flow) is not { } screen)
        {
            return;
        }

        var list = GD.Load<PackedScene>(ProblemsScene).Instantiate<ContentProblemsList>();
        screen.DetailSlot.AddChild(list);
        screen.DetailSlot.GetParent()?.GetNodeOrNull<Control>("Message")?.Hide();
        list.Fill(report);
    }

    private static ContentErrorScreen? FindErrorScreen(Node node)
    {
        foreach (var child in node.GetChildren())
        {
            if (child is ContentErrorScreen screen)
            {
                return screen;
            }

            if (FindErrorScreen(child) is { } found)
            {
                return found;
            }
        }

        return null;
    }
}
