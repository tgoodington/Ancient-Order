using AncientOrder.Rules.Common;

namespace AncientOrder.Rules.Content.Packs;

/// <summary>A pack held in memory as a map of relative path to text, for tests. The map is only looked up, never walked.</summary>
public sealed class MemoryContentSource : IContentSource
{
    private readonly Dictionary<string, string> _files;

    public MemoryContentSource(IEnumerable<KeyValuePair<string, string>> files)
    {
        ArgumentNullException.ThrowIfNull(files);
        _files = new Dictionary<string, string>(files, StringComparer.Ordinal);
    }

    public Result<string> ReadText(string relativePath) =>
        relativePath is not null && _files.TryGetValue(relativePath, out var text)
            ? Result<string>.Ok(text)
            : Result<string>.Fail(new RuleError(ErrorCode.STORAGE_ERROR, "file not found"));
}
