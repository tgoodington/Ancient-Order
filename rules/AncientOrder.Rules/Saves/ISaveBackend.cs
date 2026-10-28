namespace AncientOrder.Rules.Saves;

/// <summary>
/// Raw storage for save files, swappable without touching the save rules. A key is a short name such as manual-01.
/// A failing backend throws <see cref="SaveStorageException"/>; the save store turns that into STORAGE_ERROR.
/// </summary>
public interface ISaveBackend
{
    /// <summary>The stored bytes, or null when nothing is stored under the key. A backend may stop after SaveConstants.MaxSaveBytes + 1 bytes; SaveFormat.Read refuses such a file as too large.</summary>
    ValueTask<byte[]?> ReadAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>True when something is stored under the key; does not read its content.</summary>
    ValueTask<bool> ExistsAsync(string key, CancellationToken cancellationToken = default);

    ValueTask WriteAsync(string key, byte[] bytes, CancellationToken cancellationToken = default);

    /// <summary>Removes the key; removing a missing key is not an error.</summary>
    ValueTask RemoveAsync(string key, CancellationToken cancellationToken = default);
}

/// <summary>A storage failure with a player-readable message ("Not enough storage space to save" or "Save storage is unavailable").</summary>
public sealed class SaveStorageException : Exception
{
    public SaveStorageException()
    {
    }

    public SaveStorageException(string message)
        : base(message)
    {
    }

    public SaveStorageException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
