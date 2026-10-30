using AncientOrder.Rules.Common;
using AncientOrder.Rules.Content.Packs;
using Godot;

namespace AncientOrder;

/// <summary>
/// A content pack in a Godot folder, usually res://content/something. Reads through Godot.FileAccess, which is the way to read res://
/// in an exported build too (the files are packed into the PCK there).
/// </summary>
public sealed class GodotContentSource : IContentSource
{
    private readonly string _root;

    public GodotContentSource(string rootFolder)
    {
        _root = rootFolder.TrimEnd('/');
    }

    public Result<string> ReadText(string relativePath)
    {
        if (string.IsNullOrEmpty(relativePath) || relativePath.StartsWith('/') || relativePath.Contains("..", StringComparison.Ordinal))
        {
            return Fail("file path leaves the pack folder");
        }

        var path = _root + "/" + relativePath;
        if (!Godot.FileAccess.FileExists(path))
        {
            return Fail("file not found");
        }

        var text = Godot.FileAccess.GetFileAsString(path);
        var error = Godot.FileAccess.GetOpenError();
        return error == Error.Ok ? Result<string>.Ok(text) : Fail("file could not be read: " + error);
    }

    private static Result<string> Fail(string message) =>
        Result<string>.Fail(new RuleError(ErrorCode.STORAGE_ERROR, message));
}
