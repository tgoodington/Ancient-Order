namespace AncientOrder.Rules.Saves;

/// <summary>Keeps saves in memory. Serves tests and is the session's default; nothing survives the process.</summary>
public sealed class MemorySaveBackend : ISaveBackend
{
    private readonly Dictionary<string, byte[]> _entries = new(StringComparer.Ordinal);
    private readonly object _gate = new();

    public ValueTask<byte[]?> ReadAsync(string key, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            return ValueTask.FromResult(_entries.TryGetValue(key, out var bytes) ? (byte[]?)bytes.ToArray() : null);
        }
    }

    public ValueTask<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            return ValueTask.FromResult(_entries.ContainsKey(key));
        }
    }

    public ValueTask WriteAsync(string key, byte[] bytes, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            _entries[key] = bytes.ToArray();
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            _entries.Remove(key);
        }

        return ValueTask.CompletedTask;
    }
}
