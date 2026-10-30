using AncientOrder.Rules.Saves;

namespace AncientOrder.Tests;

/// <summary>
/// Wraps a save backend (a new MemorySaveBackend when none) and, once Hold is called, parks every call until Release, so a test can look at the screen
/// while an operation is still running. Counts the writes and removes that reached the inner backend, for "nothing was saved" checks.
/// </summary>
public sealed class GatedSaveBackend(ISaveBackend? inner = null) : ISaveBackend
{
    private readonly object _gate = new();
    private TaskCompletionSource? _held;
    private int _waiting;
    private int _writes;
    private int _removes;

    public ISaveBackend Inner { get; } = inner ?? new MemorySaveBackend();

    /// <summary>Calls parked right now.</summary>
    public int Waiting
    {
        get
        {
            lock (_gate)
            {
                return _waiting;
            }
        }
    }

    public int Writes => Volatile.Read(ref _writes);

    public int Removes => Volatile.Read(ref _removes);

    /// <summary>From now on, calls wait until Release.</summary>
    public void Hold()
    {
        lock (_gate)
        {
            _held ??= new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        }
    }

    /// <summary>Lets every parked call go, and stops parking new ones.</summary>
    public void Release()
    {
        TaskCompletionSource? held;
        lock (_gate)
        {
            held = _held;
            _held = null;
        }

        held?.TrySetResult();
    }

    public async ValueTask<byte[]?> ReadAsync(string key, CancellationToken cancellationToken = default)
    {
        await Wait();
        return await Inner.ReadAsync(key, cancellationToken);
    }

    public async ValueTask<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        await Wait();
        return await Inner.ExistsAsync(key, cancellationToken);
    }

    public async ValueTask WriteAsync(string key, byte[] bytes, CancellationToken cancellationToken = default)
    {
        await Wait();
        Interlocked.Increment(ref _writes);
        await Inner.WriteAsync(key, bytes, cancellationToken);
    }

    public async ValueTask RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        await Wait();
        Interlocked.Increment(ref _removes);
        await Inner.RemoveAsync(key, cancellationToken);
    }

    private async Task Wait()
    {
        Task? held;
        lock (_gate)
        {
            held = _held?.Task;
            if (held is not null)
            {
                _waiting++;
            }
        }

        if (held is null)
        {
            return;
        }

        try
        {
            await held;
        }
        finally
        {
            lock (_gate)
            {
                _waiting--;
            }
        }
    }
}
