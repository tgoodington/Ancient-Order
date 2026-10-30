using AncientOrder.Rules.Content.Packs;

namespace AncientOrder.Tools.Tests;

public sealed class ContentCheckCliTests : IDisposable
{
    private const string Index = """{ "format": "ancient-order-content", "version": 1, "id": "t", "name": "Test", "standIn": false, "files": ["all.json"] }""";

    private const string Clean = """
        {
          "settings": { "playerName": "P", "lineup": { "min": 1, "max": 1 }, "startingTown": "square" },
          "archetypes": [{ "id": "x" }],
          "characters": [
            { "id": "ann", "name": "Ann", "archetypeId": "x", "personality": { "patience": 20, "empathy": 20, "cunning": 10, "logic": 15, "kindness": 20, "charisma": 15 }, "startingAffection": 0, "startingTrust": 0 }
          ],
          "conversations": [
            { "id": "chat", "participants": ["ann"], "start": [{ "step": "hello" }],
              "steps": [ { "id": "hello", "lines": [ { "speaker": "ann", "text": "Hi." } ] } ] }
          ],
          "towns": [
            { "id": "square", "name": "Square", "ground": [ [[0, 0], [20, 0], [20, 20], [0, 20]] ], "start": [1, 1],
              "people": [ { "character": "ann", "placements": [ { "at": [15, 15] } ], "opens": [ { "conversation": "chat" } ] } ] }
          ]
        }
        """;

    private readonly string _root = Path.Combine(Path.GetTempPath(), "ao-cli-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private static string Broken => Clean.Replace("\"archetypeId\": \"x\"", "\"archetypeId\": \"nope\"", StringComparison.Ordinal);

    private string MakePack(string folder, string all, bool underContent = false)
    {
        var dir = underContent ? Path.Combine(_root, ContentCheckCli.ContentFolder, folder) : Path.Combine(_root, folder);
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, ContentPackLoader.IndexFileName), Index);
        File.WriteAllText(Path.Combine(dir, "all.json"), all);
        return dir;
    }

    private static (int Code, string[] Lines) Run(string[] args, string? repoRoot = null)
    {
        using var writer = new StringWriter();
        var code = ContentCheckCli.Run(args, writer, repoRoot);
        return (code, writer.ToString().Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries));
    }

    [Fact]
    public void A_clean_pack_exits_0_and_prints_ok()
    {
        var pack = MakePack("good", Clean);
        var (code, lines) = Run([pack]);
        Assert.Equal(0, code);
        Assert.Equal("== Pack " + pack, lines[0]);
        Assert.Equal("OK: good: 0 problem(s), 0 warning(s)", lines[^1]);
        Assert.DoesNotContain(lines, l => l.StartsWith("PROBLEM", StringComparison.Ordinal));
    }

    [Fact]
    public void A_pack_with_a_problem_exits_1_with_a_problem_line_naming_file_location_and_message()
    {
        var pack = MakePack("bad", Broken);
        var (code, lines) = Run([pack]);
        Assert.Equal(1, code);
        var problem = Assert.Single(lines, l => l.StartsWith("PROBLEM ", StringComparison.Ordinal));
        Assert.StartsWith("PROBLEM all.json:/characters/0/archetypeId: ", problem, StringComparison.Ordinal);
        Assert.Contains("nope", problem, StringComparison.Ordinal);
        Assert.Equal("FAILED: bad: 1 problem(s), 0 warning(s)", lines[^1]);
    }

    [Fact]
    public void A_syntax_error_is_a_problem_with_line_and_column()
    {
        var pack = MakePack("syntax", "{\n  \"settings\": }");
        var (code, lines) = Run([pack]);
        Assert.Equal(1, code);
        Assert.Contains(lines, l => l.StartsWith("PROBLEM all.json:line 2, column ", StringComparison.Ordinal));
    }

    [Fact]
    public void A_warning_prints_a_warning_line_and_still_exits_0()
    {
        var pack = MakePack("warn", Clean.Replace("\"text\": \"Hi.\" } ] }", "\"text\": \"Hi.\" } ] }, { \"id\": \"orphan\", \"lines\": [] }", StringComparison.Ordinal));
        var (code, lines) = Run([pack]);
        Assert.Equal(0, code);
        var warning = Assert.Single(lines, l => l.StartsWith("WARNING ", StringComparison.Ordinal));
        Assert.StartsWith("WARNING all.json:/conversations/0/steps/1: ", warning, StringComparison.Ordinal);
        Assert.Contains("'orphan'", warning, StringComparison.Ordinal);
        Assert.Equal("OK: warn: 0 problem(s), 1 warning(s)", lines[^1]);
    }

    [Fact]
    public void Several_pack_arguments_are_each_checked_and_one_failure_fails_the_run()
    {
        var good = MakePack("good", Clean);
        var bad = MakePack("bad", Broken);
        var (code, lines) = Run([good, bad]);
        Assert.Equal(1, code);
        Assert.Equal(2, lines.Count(l => l.StartsWith("== Pack ", StringComparison.Ordinal)));
        Assert.Contains(lines, l => l.StartsWith("OK: good", StringComparison.Ordinal));
        Assert.Contains(lines, l => l.StartsWith("FAILED: bad", StringComparison.Ordinal));
    }

    [Fact]
    public void A_folder_with_no_pack_json_exits_1_naming_pack_json()
    {
        var empty = Path.Combine(_root, "empty");
        Directory.CreateDirectory(empty);
        var (code, lines) = Run([empty]);
        Assert.Equal(1, code);
        Assert.Contains(lines, l => l.StartsWith("PROBLEM pack.json: ", StringComparison.Ordinal));
    }

    [Fact]
    public void With_no_arguments_every_content_pack_folder_is_checked_in_name_order_and_non_pack_folders_are_skipped()
    {
        MakePack("beta", Clean, underContent: true);
        MakePack("alpha", Clean, underContent: true);
        Directory.CreateDirectory(Path.Combine(_root, ContentCheckCli.ContentFolder, "notes"));
        var (code, lines) = Run([], _root);
        Assert.Equal(0, code);
        var packs = lines.Where(l => l.StartsWith("== Pack ", StringComparison.Ordinal)).ToArray();
        Assert.Equal(2, packs.Length);
        Assert.EndsWith("alpha", packs[0], StringComparison.Ordinal);
        Assert.EndsWith("beta", packs[1], StringComparison.Ordinal);
    }

    [Fact]
    public void With_no_arguments_one_failing_pack_fails_the_run_but_the_others_are_still_checked()
    {
        MakePack("alpha", Broken, underContent: true);
        MakePack("beta", Clean, underContent: true);
        var (code, lines) = Run([], _root);
        Assert.Equal(1, code);
        Assert.Contains(lines, l => l.StartsWith("FAILED: alpha", StringComparison.Ordinal));
        Assert.Contains(lines, l => l.StartsWith("OK: beta", StringComparison.Ordinal));
    }

    [Fact]
    public void With_no_arguments_and_no_repo_root_or_no_packs_exits_2_with_an_error_line()
    {
        var (noRoot, noRootLines) = Run([], null);
        Assert.Equal(2, noRoot);
        Assert.StartsWith("ERROR: ", noRootLines[0], StringComparison.Ordinal);

        Directory.CreateDirectory(_root);
        var (noPacks, noPackLines) = Run([], _root);
        Assert.Equal(2, noPacks);
        Assert.StartsWith("ERROR: ", noPackLines[0], StringComparison.Ordinal);
    }

    [Fact]
    public void The_repo_root_is_the_nearest_folder_above_holding_the_solution_file()
    {
        var deep = Path.Combine(_root, "a", "b");
        Directory.CreateDirectory(deep);
        File.WriteAllText(Path.Combine(_root, "AncientOrder.sln"), string.Empty);
        Assert.Equal(_root, ContentCheckCli.FindRepoRoot(deep));
    }
}
