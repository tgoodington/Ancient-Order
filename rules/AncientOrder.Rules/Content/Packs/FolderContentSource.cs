using System.Security;
using AncientOrder.Rules.Common;

namespace AncientOrder.Rules.Content.Packs;

/// <summary>A pack in a folder on disk. A path that would leave the folder is refused.</summary>
public sealed class FolderContentSource : IContentSource
{
    private readonly string _root;

    public FolderContentSource(string rootFolder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootFolder);
        _root = Path.GetFullPath(rootFolder);
    }

    public Result<string> ReadText(string relativePath)
    {
        if (string.IsNullOrEmpty(relativePath))
        {
            return Fail("file path is empty");
        }

        var full = Path.GetFullPath(Path.Combine(_root, relativePath));
        var prefix = Path.TrimEndingDirectorySeparator(_root) + Path.DirectorySeparatorChar;
        if (!full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return Fail("file path leaves the pack folder");
        }

        try
        {
            return Result<string>.Ok(File.ReadAllText(full));
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            return Fail("file not found");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException or ArgumentException or NotSupportedException)
        {
            return Fail("file could not be read: " + ex.Message);
        }
    }

    private static Result<string> Fail(string message) =>
        Result<string>.Fail(new RuleError(ErrorCode.STORAGE_ERROR, message));
}
