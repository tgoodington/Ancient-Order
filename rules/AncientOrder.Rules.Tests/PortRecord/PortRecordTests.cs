using System.Reflection;
using System.Text;

namespace AncientOrder.Rules.Tests.PortRecord;

// Keeps docs/project_notes/port_record.md honest: every test in the in-scope TypeScript test files is either carried over under the
// same name (a C# display name) or listed in the record as "- <file> › <title>" (followed by " → reason" or the end of the line).
// The TypeScript files are read only.
public class PortRecordTests
{
    private static readonly string[] InScopeFiles =
    [
        "src/personality/personalitySystem.test.ts",
        "src/state/gameState.test.ts",
        "src/state/npcs.test.ts",
        "src/state/stateUpdaters.test.ts",
        "src/narrative/synergyCalculator.test.ts",
        "src/combat/formulas.test.ts",
        "src/combat/energy.test.ts",
        "src/combat/elementalPaths.test.ts",
        "src/combat/defense.test.ts",
        "src/combat/reactionProgression.test.ts",
        "src/combat/behaviorTree/evaluator.test.ts",
        "src/combat/behaviorTree/perception.test.ts",
        "src/combat/behaviorTree/factors/factors.test.ts",
        "src/combat/behaviorTree/profiles/profiles.test.ts",
        "src/combat/behaviorTree/rankCoefficient.test.ts",
        "src/combat/behaviorTree/tieBreaking.test.ts",
        "src/combat/declaration.test.ts",
        "src/combat/counterChain.test.ts",
        "src/combat/groupAction.test.ts",
        "src/combat/pipeline.test.ts",
        "src/combat/roundManager.test.ts",
        "src/combat/sync.test.ts",
        "src/combat/integration.test.ts",
        "src/session/combat.test.ts",
        "src/session/gameSession.test.ts",
        "src/session/validation.test.ts",
        "src/session/operations/combat.test.ts",
        "src/session/operations/player.test.ts",
        "src/session/operations/protoKeys.test.ts",
        "src/session/narrativeDialogue.test.ts",
        "src/session/operations/dialogue.test.ts",
        "src/session/operations/narrative.test.ts",
        "src/session/contentFreeze.test.ts",
        "tests/session.combat-e2e.test.ts",
        "tests/session.sprint1.test.ts",
        "tests/session.sprint4.test.ts",
        "src/persistence/backends.test.ts",
        "src/persistence/saveStore.smoke.test.ts",
        "src/persistence/saveStore.test.ts",
        "src/persistence/validation.test.ts",
        "src/session/operations/saves.test.ts",
        "tests/session.saves.test.ts",
    ];

    private const int MinimumPlausibleTitles = 500;

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "AncientOrder.sln")))
            {
                return dir.FullName;
            }
            dir = dir.Parent;
        }
        throw new InvalidOperationException("AncientOrder.sln was not found above " + AppContext.BaseDirectory);
    }

    private static bool IsQuote(char c) => c is '\'' or '"' or '`';

    private static bool IsIdentChar(char c) => char.IsLetterOrDigit(c) || c == '_' || c == '$' || c == '.';

    // Index just past the bracket that closes the one at 'open' (string and template literals are skipped).
    private static int SkipBalanced(string s, int open)
    {
        var depth = 0;
        for (var i = open; i < s.Length; i++)
        {
            var c = s[i];
            if (IsQuote(c))
            {
                i = ReadString(s, i, out _);
                continue;
            }
            if (c is '(' or '[' or '{')
            {
                depth++;
            }
            else if (c is ')' or ']' or '}')
            {
                depth--;
                if (depth == 0)
                {
                    return i + 1;
                }
            }
        }
        return s.Length;
    }

    // Reads the string literal starting at 'start'; returns the index of its closing quote.
    private static int ReadString(string s, int start, out string value)
    {
        var quote = s[start];
        var sb = new StringBuilder();
        var i = start + 1;
        while (i < s.Length && s[i] != quote)
        {
            if (s[i] == '\\' && i + 1 < s.Length)
            {
                i++;
                sb.Append(s[i] switch { 'n' => '\n', 't' => '\t', 'r' => '\r', _ => s[i] });
            }
            else
            {
                sb.Append(s[i]);
            }
            i++;
        }
        value = sb.ToString();
        return i;
    }

    private static int SkipSpaces(string s, int p)
    {
        while (p < s.Length && char.IsWhiteSpace(s[p]))
        {
            p++;
        }
        return p;
    }

    public static IReadOnlyList<string> ExtractTitles(string source)
    {
        var titles = new List<string>();
        for (var i = 0; i < source.Length; i++)
        {
            var c = source[i];
            if (IsQuote(c))
            {
                i = ReadString(source, i, out _);
                continue;
            }
            if (c == '/' && i + 1 < source.Length && source[i + 1] == '/')
            {
                while (i < source.Length && source[i] != '\n')
                {
                    i++;
                }
                continue;
            }
            if (c == '/' && i + 1 < source.Length && source[i + 1] == '*')
            {
                var end = source.IndexOf("*/", i + 2, StringComparison.Ordinal);
                i = end < 0 ? source.Length : end + 1;
                continue;
            }
            if ((c != 'i' && c != 't') || (i > 0 && IsIdentChar(source[i - 1])))
            {
                continue;
            }
            var keyword = source.AsSpan(i).StartsWith("it", StringComparison.Ordinal) ? "it"
                : source.AsSpan(i).StartsWith("test", StringComparison.Ordinal) ? "test" : null;
            if (keyword is null)
            {
                continue;
            }
            var p = i + keyword.Length;
            if (source.AsSpan(p).StartsWith(".each", StringComparison.Ordinal))
            {
                p = SkipSpaces(source, p + ".each".Length);
                if (p >= source.Length || source[p] != '(')
                {
                    continue;
                }
                p = SkipBalanced(source, p);
            }
            p = SkipSpaces(source, p);
            if (p >= source.Length || source[p] != '(')
            {
                continue;
            }
            p = SkipSpaces(source, p + 1);
            if (p >= source.Length || !IsQuote(source[p]))
            {
                continue;
            }
            var close = ReadString(source, p, out var title);
            titles.Add(title.Trim());
            i = close;
        }
        return titles;
    }

    private static HashSet<string> CSharpDisplayNames()
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
        foreach (var type in typeof(PortRecordTests).Assembly.GetTypes())
        {
            foreach (var method in type.GetMethods(all))
            {
                foreach (var fact in method.GetCustomAttributes<FactAttribute>(true))
                {
                    if (!string.IsNullOrEmpty(fact.DisplayName))
                    {
                        names.Add(fact.DisplayName.Trim());
                    }
                }
            }
        }
        return names;
    }

    private static bool IsListed(string[] recordLines, string file, string title)
    {
        var prefix = "- " + file + " › " + title;
        foreach (var line in recordLines)
        {
            if (line.StartsWith(prefix, StringComparison.Ordinal)
                && (line.Length == prefix.Length || line.AsSpan(prefix.Length).StartsWith(" →", StringComparison.Ordinal)))
            {
                return true;
            }
        }
        return false;
    }

    [Fact(DisplayName = "The title extractor handles quotes, escapes, it.each and comments")]
    public void Extractor_reads_titles()
    {
        const string source = """
            describe('x', () => {
              it('single \'quoted\' title', () => {});
              it("double \"quoted\" title", () => {});
              it(`backtick title ${1}`, () => {});
              it.each([1, [2, 3], 'a)'])('each %s', (n) => {});
              test('a test', () => {});
              // it('in a comment', () => {});
              const s = "it('in a string')";
              submit('not a test');
            });
            """;
        Assert.Equal(
            ["single 'quoted' title", "double \"quoted\" title", "backtick title ${1}", "each %s", "a test"],
            ExtractTitles(source));
    }

    [Fact(DisplayName = "EveryTypeScriptTestIsAccounted")]
    public void EveryTypeScriptTestIsAccounted()
    {
        var root = FindRepoRoot();
        var recordPath = Path.Combine(root, "docs", "project_notes", "port_record.md");
        Assert.True(File.Exists(recordPath), "port_record.md is missing: " + recordPath);
        var recordLines = File.ReadAllText(recordPath).Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var names = CSharpDisplayNames();

        var total = 0;
        var byName = 0;
        var listed = 0;
        var missing = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var file in InScopeFiles)
        {
            var path = Path.Combine(root, file.Replace('/', Path.DirectorySeparatorChar));
            Assert.True(File.Exists(path), "In-scope TypeScript test file is missing: " + file);
            foreach (var title in ExtractTitles(File.ReadAllText(path)))
            {
                if (!seen.Add(file + "\0" + title))
                {
                    continue;
                }
                total++;
                if (names.Contains(title))
                {
                    byName++;
                }
                else if (IsListed(recordLines, file, title))
                {
                    listed++;
                }
                else
                {
                    missing.Add(file + " › " + title);
                }
            }
        }

        Assert.True(total > MinimumPlausibleTitles, $"Found only {total} TypeScript titles; the extractor or the file list is broken.");
        Assert.True(missing.Count == 0, $"{missing.Count} TypeScript test(s) are neither a C# display name nor listed in port_record.md "
            + $"({total} checked, {byName} by name, {listed} listed):\n" + string.Join("\n", missing));
    }

    [Fact(DisplayName = "Every test entry in the port record names a real TypeScript test")]
    public void Record_entries_name_real_tests()
    {
        var root = FindRepoRoot();
        var recordLines = File.ReadAllText(Path.Combine(root, "docs", "project_notes", "port_record.md"))
            .Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var known = new List<(string File, string Title)>();
        foreach (var file in InScopeFiles)
        {
            foreach (var title in ExtractTitles(File.ReadAllText(Path.Combine(root, file.Replace('/', Path.DirectorySeparatorChar)))))
            {
                known.Add((file, title));
            }
        }

        var entries = 0;
        var unknown = new List<string>();
        foreach (var line in recordLines)
        {
            if (!line.StartsWith("- ", StringComparison.Ordinal) || !line.Contains(".test.ts › ", StringComparison.Ordinal))
            {
                continue;
            }
            entries++;
            var real = known.Any(k =>
            {
                var prefix = "- " + k.File + " › " + k.Title;
                return line.StartsWith(prefix, StringComparison.Ordinal)
                    && (line.Length == prefix.Length || line.AsSpan(prefix.Length).StartsWith(" →", StringComparison.Ordinal));
            });
            if (!real)
            {
                unknown.Add(line);
            }
        }

        Assert.True(entries > 0, "The port record lists no tests.");
        Assert.True(unknown.Count == 0, "These port record entries do not name a TypeScript test in the in-scope files:\n" + string.Join("\n", unknown));
    }
}
