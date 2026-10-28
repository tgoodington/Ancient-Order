using System.Collections.Immutable;
using AncientOrder.Rules.Common;
using AncientOrder.Rules.Party;
using AncientOrder.Rules.Saves;

namespace AncientOrder.Rules.Session;

public sealed partial class GameSession
{
    /// <summary>Saves the current game to a slot (1 to 10). Never changes the game or notifies listeners.</summary>
    public ValueTask<Result<SaveMetadata>> SaveToSlot(int slot) =>
        RunAsync((c, s) => SaveOperations.SaveToSlot(c, s, _saves, slot));

    /// <summary>Loads a slot as a fresh active game (no pending orders, no last outcome); succeeds with what the load adjusted.</summary>
    public ValueTask<Result<LoadReport>> LoadFromSlot(int slot) =>
        RunAsync((c, s) => SaveOperations.LoadFromSlot(c, s, _saves, slot));

    public ValueTask<Result<ImmutableArray<SlotInfo>>> ListSlots() =>
        RunAsync((c, s) => SaveOperations.ListSlots(c, s, _saves));

    public ValueTask<Result<DeletedSlot>> DeleteSlot(int slot) =>
        RunAsync((c, s) => SaveOperations.DeleteSlot(c, s, _saves, slot));

    /// <summary>
    /// Like Run, but the operation awaits storage. The await resumes on the caller's context (no ConfigureAwait(false)),
    /// and a change commits against the snapshot current when the operation finishes, so a load replaces the game
    /// even if other changes committed while it was reading.
    /// </summary>
    internal async ValueTask<Result<T>> RunAsync<T>(Func<SessionContext, SessionSnapshot, ValueTask<Outcome<T>>> operation)
    {
        var started = _snapshot;
        SessionSnapshot committed;
        Result<T> result;
        try
        {
            var outcome = await operation(_context, started);
            result = outcome.Result;
            if (ReferenceEquals(outcome.Next, started))
            {
                return result;
            }

            committed = outcome.Next with
            {
                Version = _snapshot.Version + 1,
                Synergy = outcome.Next.Game is null
                    ? ImmutableArray<SynergyResult>.Empty
                    : Synergy.Current(_context.Content, outcome.Next.Game),
            };
        }
        catch (Exception)
        {
            return Result<T>.Fail(new RuleError(ErrorCode.INTERNAL_ERROR, "An unexpected error occurred"));
        }

        _snapshot = committed;
        Notify(committed);
        return result;
    }
}
