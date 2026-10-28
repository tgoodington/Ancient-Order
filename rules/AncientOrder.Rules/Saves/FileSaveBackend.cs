using System.Security;

namespace AncientOrder.Rules.Saves;

/// <summary>
/// Stores each key as {folder}/{key}.json. The folder is handed in by whatever runs the rules. A write goes to
/// {key}.json.tmp in the same folder, is flushed to disk, then replaces the old file in one move, so a crash or
/// failure mid-write leaves the existing save untouched. A leftover .tmp is ignored and overwritten next time.
/// Disk-full failures become "Not enough storage space to save"; every other I/O or permission failure becomes "Save storage is unavailable".
/// </summary>
public sealed class FileSaveBackend : ISaveBackend
{
    public const string DiskFullMessage = "Not enough storage space to save";
    public const string UnavailableMessage = "Save storage is unavailable";

    private const int ErrorDiskFull = unchecked((int)0x80070070);
    private const int ErrorHandleDiskFull = unchecked((int)0x80070027);
    private const int Enospc = 28;

    private readonly string _folder;

    public FileSaveBackend(string folder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folder);
        _folder = folder;
    }

    // All disk work runs inside Task.Run so nothing synchronous happens on the caller's thread before the first await.
    public async ValueTask<byte[]?> ReadAsync(string key, CancellationToken cancellationToken = default)
    {
        var path = FinalPath(key);
        try
        {
            return await Task.Run(() => ReadBoundedAsync(path, cancellationToken), cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            return null;
        }
        catch (Exception ex) when (IsStorageFailure(ex))
        {
            throw Map(ex);
        }
    }

    // Never holds more than MaxSaveBytes + 1 bytes: a longer file comes back truncated to that and is refused as too large.
    private static async Task<byte[]?> ReadBoundedAsync(string path, CancellationToken cancellationToken)
    {
        var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous);
        await using (stream.ConfigureAwait(false))
        {
            var limit = SaveConstants.MaxSaveBytes + 1;
            var buffer = new byte[(int)Math.Min(stream.Length, limit)];
            var filled = 0;
            while (filled < buffer.Length)
            {
                var read = await stream.ReadAsync(buffer.AsMemory(filled), cancellationToken).ConfigureAwait(false);
                if (read == 0)
                {
                    break;
                }

                filled += read;
            }

            return filled == buffer.Length ? buffer : buffer[..filled];
        }
    }

    public async ValueTask<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        var path = FinalPath(key);
        try
        {
            return await Task.Run(() => File.Exists(path), cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (IsStorageFailure(ex))
        {
            throw Map(ex);
        }
    }

    public async ValueTask WriteAsync(string key, byte[] bytes, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        var path = FinalPath(key);
        var temp = path + ".tmp";
        try
        {
            await Task.Run(() => WriteCoreAsync(temp, path, bytes, cancellationToken), cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (IsStorageFailure(ex))
        {
            DeleteQuietly(temp);
            throw Map(ex);
        }
    }

    private async Task WriteCoreAsync(string temp, string path, byte[] bytes, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_folder);
        var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.Asynchronous);
        await using (stream.ConfigureAwait(false))
        {
            await stream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
            await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            stream.Flush(true);
        }

        File.Move(temp, path, overwrite: true);
    }

    public async ValueTask RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        var path = FinalPath(key);
        try
        {
            await Task.Run(
                () =>
                {
                    try
                    {
                        File.Delete(path);
                    }
                    catch (DirectoryNotFoundException)
                    {
                        // No folder means nothing to remove.
                    }
                },
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (IsStorageFailure(ex))
        {
            throw Map(ex);
        }
    }

    // Keys come from SaveStore (manual-01 and so on); anything else is a bug, never a path fragment.
    private string FinalPath(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (key.Length == 0 || !key.All(c => c is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '-'))
        {
            throw new ArgumentException("Save keys may contain only lowercase letters, digits, and '-'.", nameof(key));
        }

        return Path.Combine(_folder, key + ".json");
    }

    private static bool IsStorageFailure(Exception ex) =>
        ex is IOException or UnauthorizedAccessException or SecurityException or NotSupportedException;

    private static SaveStorageException Map(Exception ex) =>
        new(ex is IOException { HResult: ErrorDiskFull or ErrorHandleDiskFull or Enospc } ? DiskFullMessage : UnavailableMessage, ex);

    private static void DeleteQuietly(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (IsStorageFailure(ex))
        {
            // The old save is intact; a stray temp file is overwritten by the next write.
        }
    }
}
