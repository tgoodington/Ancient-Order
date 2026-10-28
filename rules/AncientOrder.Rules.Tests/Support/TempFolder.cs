namespace AncientOrder.Rules.Tests.Support;

/// <summary>A unique temporary folder path for one test. The folder itself is not created (a missing folder is a case under test); Dispose deletes it and anything in it.</summary>
public sealed class TempFolder : IDisposable
{
    public TempFolder()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ancient-order-tests-" + Guid.NewGuid().ToString("N"));
    }

    public string Path { get; }

    public string File(string name) => System.IO.Path.Combine(Path, name);

    public string[] FileNames() =>
        Directory.Exists(Path)
            ? [.. Directory.GetFileSystemEntries(Path).Select(p => System.IO.Path.GetFileName(p)).Order(StringComparer.Ordinal)]
            : [];

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
        catch (IOException)
        {
            // A leftover temp folder is harmless; the OS clears its temp area.
        }
        catch (UnauthorizedAccessException)
        {
            // Same: never fail a test over cleanup.
        }
    }
}
