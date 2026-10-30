using System.Collections.Immutable;
using AncientOrder.Rules.Common;
using AncientOrder.Rules.Content.Check;
using AncientOrder.Rules.Saves;
using AncientOrder.Rules.Session;
using AncientOrder.Rules.State;
using Godot;

namespace AncientOrder;

/// <summary>
/// The composition root: the one place that loads the content pack, builds the save store on the player's machine, and builds the session.
/// Future screens get the session from here. The pack is the folder named by the project setting ancient_order/content/pack.
/// When the pack cannot be loaded, Content and Session are null and ContentProblems lists every problem; the host never throws over bad content.
/// </summary>
public sealed class GameHost
{
    public const string PackSetting = "ancient_order/content/pack";
    public const string DefaultPack = "res://content/standin";

    public GameHost()
        : this(PackFolderFromSettings())
    {
    }

    public GameHost(string packFolder)
    {
        var report = ContentCheck.Run(new GodotContentSource(packFolder));
        foreach (var warning in report.Warnings)
        {
            GD.Print($"Content pack {packFolder}: warning {warning.Path}: {warning.Message}");
        }

        Report = report;
        if (report.Content is null)
        {
            ContentProblems = report.Problems;
            foreach (var problem in ContentProblems)
            {
                GD.PushError($"Content pack {packFolder}: {problem.Path}: {problem.Message}");
            }

            return;
        }

        Content = report.Content;
        var backend = new FileSaveBackend(ProjectSettings.GlobalizePath("user://saves"));
        Session = new GameSession(Content, new GameSessionOptions
        {
            Saves = new SaveStore(backend, Content),
            OnListenerError = error => GD.PushError(error.ToString()),
        });
    }

    public ContentCheckReport? Report { get; }

    public GameContent? Content { get; }

    public GameSession? Session { get; }

    public ImmutableArray<RuleErrorDetail> ContentProblems { get; } = ImmutableArray<RuleErrorDetail>.Empty;

    private static string PackFolderFromSettings() =>
        ProjectSettings.HasSetting(PackSetting) ? ProjectSettings.GetSetting(PackSetting).AsString() : DefaultPack;
}
