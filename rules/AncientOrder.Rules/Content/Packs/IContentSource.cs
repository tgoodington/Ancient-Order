using AncientOrder.Rules.Common;

namespace AncientOrder.Rules.Content.Packs;

/// <summary>The only way the pack loader touches storage. Paths are relative to the pack folder, with forward slashes.</summary>
public interface IContentSource
{
    Result<string> ReadText(string relativePath);
}
