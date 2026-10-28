using System.Collections.Immutable;
using AncientOrder.Rules.Common;
using AncientOrder.Rules.Saves;

namespace AncientOrder.Rules.Session;

/// <summary>
/// The save operations. Each awaits the store without ConfigureAwait(false) so it resumes on the caller's context
/// (Godot's main thread), which is where the session commits.
/// </summary>
internal static class SaveOperations
{
    private static Outcome<T> InvalidSlot<T>(SessionSnapshot s) =>
        Outcome.Refuse<T>(s, ErrorCode.INVALID_SLOT, "Slot must be an integer between 1 and 10");

    /// <summary>Saves the game as it is in the snapshot when called.</summary>
    public static async ValueTask<Outcome<SaveMetadata>> SaveToSlot(SessionContext context, SessionSnapshot s, SaveStore store, int slot)
    {
        if (!SaveConstants.IsValidSlot(slot))
        {
            return InvalidSlot<SaveMetadata>(s);
        }

        if (s.Game is null)
        {
            return Outcome.NoActiveGame<SaveMetadata>(s);
        }

        var saved = await store.SaveAsync(slot, s.Game, context.Now());
        return saved.IsOk ? Outcome.Read(s, saved.Value) : Outcome.Refuse<SaveMetadata>(s, saved.Error);
    }

    /// <summary>On success the next snapshot is a fresh one holding the loaded game (no pending orders, no last outcome); the session fixes up the version.</summary>
    public static async ValueTask<Outcome<LoadReport>> LoadFromSlot(SessionContext context, SessionSnapshot s, SaveStore store, int slot)
    {
        if (!SaveConstants.IsValidSlot(slot))
        {
            return InvalidSlot<LoadReport>(s);
        }

        var loaded = await store.LoadAsync(slot);
        return loaded.IsOk
            ? Outcome.Change(SessionSnapshot.Initial with { Game = loaded.Value.Game }, loaded.Value.Report)
            : Outcome.Refuse<LoadReport>(s, loaded.Error);
    }

    public static async ValueTask<Outcome<ImmutableArray<SlotInfo>>> ListSlots(SessionContext context, SessionSnapshot s, SaveStore store)
    {
        var listed = await store.ListAsync();
        return listed.IsOk ? Outcome.Read(s, listed.Value) : Outcome.Refuse<ImmutableArray<SlotInfo>>(s, listed.Error);
    }

    public static async ValueTask<Outcome<DeletedSlot>> DeleteSlot(SessionContext context, SessionSnapshot s, SaveStore store, int slot)
    {
        if (!SaveConstants.IsValidSlot(slot))
        {
            return InvalidSlot<DeletedSlot>(s);
        }

        var deleted = await store.DeleteAsync(slot);
        return deleted.IsOk ? Outcome.Read(s, new DeletedSlot(slot, true)) : Outcome.Refuse<DeletedSlot>(s, deleted.Error);
    }
}

/// <summary>Result of DeleteSlot: the slot and Deleted = true.</summary>
public sealed record DeletedSlot(int Slot, bool Deleted);
