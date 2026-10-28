using System.Collections.Immutable;
using System.Globalization;
using AncientOrder.Rules.Common;
using AncientOrder.Rules.State;

namespace AncientOrder.Rules.Saves;

public sealed record SaveMetadata(int Slot, long SavedAt, string PlayerName);

public enum SlotStatus
{
    Empty,
    Saved,
    Corrupted,
}

/// <summary>Exists is true only for loadable saves; a damaged slot has Exists = false and Status = Corrupted.</summary>
public sealed record SlotInfo(int Slot, bool Exists, SlotStatus Status, SaveMetadata? Metadata);

/// <summary>
/// Ten manual slots over a backend (port of saveStore.ts). Keys are manual-01 to manual-10; "auto-" keys are reserved.
/// Every refusal is a Result; a backend's SaveStorageException becomes STORAGE_ERROR.
/// Awaits here deliberately keep the caller's context (no ConfigureAwait(false)).
/// </summary>
public sealed class SaveStore
{
    private readonly ISaveBackend _backend;
    private readonly GameContent _content;

    public SaveStore(ISaveBackend backend, GameContent content)
    {
        ArgumentNullException.ThrowIfNull(backend);
        ArgumentNullException.ThrowIfNull(content);
        _backend = backend;
        _content = content;
    }

    public static string KeyFor(int slot) =>
        SaveConstants.KindManual + "-" + slot.ToString("00", CultureInfo.InvariantCulture);

    public async ValueTask<Result<SaveMetadata>> SaveAsync(int slot, GameState game, long now, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(game);
        if (InvalidSlot(slot) is { } invalid)
        {
            return Result<SaveMetadata>.Fail(invalid);
        }

        var bytes = SaveFormat.Write(slot, SaveConstants.KindManual, game, _content, now);
        if (!bytes.IsOk)
        {
            return Result<SaveMetadata>.Fail(bytes.Error);
        }

        try
        {
            await _backend.WriteAsync(KeyFor(slot), bytes.Value, cancellationToken);
        }
        catch (SaveStorageException ex)
        {
            return StorageFailure<SaveMetadata>(ex);
        }

        return Result<SaveMetadata>.Ok(new SaveMetadata(slot, now, game.Player.Name));
    }

    public async ValueTask<Result<LoadedSave>> LoadAsync(int slot, CancellationToken cancellationToken = default)
    {
        if (InvalidSlot(slot) is { } invalid)
        {
            return Result<LoadedSave>.Fail(invalid);
        }

        byte[]? raw;
        try
        {
            raw = await _backend.ReadAsync(KeyFor(slot), cancellationToken);
        }
        catch (SaveStorageException ex)
        {
            return StorageFailure<LoadedSave>(ex);
        }

        if (raw is null)
        {
            return Result<LoadedSave>.Fail(NotFound(slot));
        }

        var parsed = SaveFormat.Read(raw, _content);
        if (parsed.IsOk)
        {
            return parsed;
        }

        var error = parsed.Error;
        return Result<LoadedSave>.Fail(error with { Message = Corrupted(slot) + error.Message });
    }

    public async ValueTask<Result<ImmutableArray<SlotInfo>>> ListAsync(CancellationToken cancellationToken = default)
    {
        var slots = ImmutableArray.CreateBuilder<SlotInfo>();
        for (var slot = SaveConstants.MinSlot; slot <= SaveConstants.MaxSlot; slot++)
        {
            byte[]? raw;
            try
            {
                raw = await _backend.ReadAsync(KeyFor(slot), cancellationToken);
            }
            catch (SaveStorageException ex)
            {
                return StorageFailure<ImmutableArray<SlotInfo>>(ex);
            }

            if (raw is null)
            {
                slots.Add(new SlotInfo(slot, false, SlotStatus.Empty, null));
                continue;
            }

            var parsed = SaveFormat.Read(raw, _content);
            slots.Add(parsed.IsOk
                ? new SlotInfo(slot, true, SlotStatus.Saved, new SaveMetadata(slot, parsed.Value.SavedAt, parsed.Value.PlayerName))
                : new SlotInfo(slot, false, SlotStatus.Corrupted, null));
        }

        return Result<ImmutableArray<SlotInfo>>.Ok(slots.ToImmutable());
    }

    /// <summary>Deletes the slot; a damaged slot can be deleted too.</summary>
    public async ValueTask<Result<int>> DeleteAsync(int slot, CancellationToken cancellationToken = default)
    {
        if (InvalidSlot(slot) is { } invalid)
        {
            return Result<int>.Fail(invalid);
        }

        var key = KeyFor(slot);
        try
        {
            if (!await _backend.ExistsAsync(key, cancellationToken))
            {
                return Result<int>.Fail(NotFound(slot));
            }

            await _backend.RemoveAsync(key, cancellationToken);
        }
        catch (SaveStorageException ex)
        {
            return StorageFailure<int>(ex);
        }

        return Result<int>.Ok(slot);
    }

    private static RuleError? InvalidSlot(int slot) =>
        SaveConstants.IsValidSlot(slot)
            ? null
            : new RuleError(
                ErrorCode.INVALID_SLOT,
                $"Invalid slot number: {slot.ToString(CultureInfo.InvariantCulture)}. Must be between {SaveConstants.MinSlot.ToString(CultureInfo.InvariantCulture)} and {SaveConstants.MaxSlot.ToString(CultureInfo.InvariantCulture)}.");

    private static RuleError NotFound(int slot) =>
        new(ErrorCode.SAVE_NOT_FOUND, $"No save found in slot {slot.ToString(CultureInfo.InvariantCulture)}.");

    private static string Corrupted(int slot) =>
        $"Save file in slot {slot.ToString(CultureInfo.InvariantCulture)} is corrupted: ";

    private static Result<T> StorageFailure<T>(SaveStorageException ex) =>
        Result<T>.Fail(new RuleError(ErrorCode.STORAGE_ERROR, ex.Message));
}
