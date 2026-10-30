using System.Globalization;
using AncientOrder.Rules.Common;
using AncientOrder.Rules.Content.Check;
using AncientOrder.Rules.Content.Packs;

namespace AncientOrder.Tools;

/// <summary>
/// Checks content packs and prints what it finds. With no arguments it checks every godot/content/*/ folder holding a pack.json
/// under the repo root; otherwise each argument is a pack folder (relative to the current folder). Returns 1 when any pack has a problem, else 0 (2 for a usage error).
/// </summary>
public static class ContentCheckCli
{
    public const string ContentFolder = "godot/content";

    public static int Run(string[] args, TextWriter output, string? repoRoot)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(output);

        var folders = new List<string>();
        if (args.Length > 0)
        {
            folders.AddRange(args.Select(Path.GetFullPath));
        }
        else
        {
            if (repoRoot is null)
            {
                output.WriteLine("ERROR: no pack folders given and the repo root (the folder holding AncientOrder.sln) was not found");
                return 2;
            }

            var root = Path.GetFullPath(Path.Combine(repoRoot, ContentFolder));
            if (Directory.Exists(root))
            {
                folders.AddRange(Directory.GetDirectories(root)
                    .Where(d => File.Exists(Path.Combine(d, ContentPackLoader.IndexFileName)))
                    .Order(StringComparer.Ordinal));
            }

            if (folders.Count == 0)
            {
                output.WriteLine($"ERROR: no content packs (folders with a {ContentPackLoader.IndexFileName}) under {root}");
                return 2;
            }
        }

        var failed = false;
        foreach (var folder in folders)
        {
            var report = ContentCheck.Run(new FolderContentSource(folder));
            output.WriteLine($"== Pack {folder}");
            foreach (var problem in report.Problems)
            {
                output.WriteLine(Line("PROBLEM", problem));
            }

            foreach (var warning in report.Warnings)
            {
                output.WriteLine(Line("WARNING", warning));
            }

            output.WriteLine(string.Create(
                CultureInfo.InvariantCulture,
                $"{(report.Passed ? "OK" : "FAILED")}: {Path.GetFileName(folder)}: {report.Problems.Length} problem(s), {report.Warnings.Length} warning(s)"));
            failed |= !report.Passed;
        }

        return failed ? 1 : 0;
    }

    /// <summary>Walks up from the folder to the one holding AncientOrder.sln; null when there is none.</summary>
    public static string? FindRepoRoot(string startFolder)
    {
        var folder = new DirectoryInfo(startFolder);
        while (folder is not null)
        {
            if (File.Exists(Path.Combine(folder.FullName, "AncientOrder.sln")))
            {
                return folder.FullName;
            }

            folder = folder.Parent;
        }

        return null;
    }

    private static string Line(string kind, RuleErrorDetail detail) => $"{kind} {detail.Path}: {detail.Message}";
}
