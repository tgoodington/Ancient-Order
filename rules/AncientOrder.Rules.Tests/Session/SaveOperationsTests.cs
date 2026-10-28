using System.Text;
using AncientOrder.Rules.Combat;
using AncientOrder.Rules.Common;
using AncientOrder.Rules.Saves;
using AncientOrder.Rules.Session;
using AncientOrder.Rules.State;
using AncientOrder.Rules.Tests.Fixtures;
using AncientOrder.Rules.Tests.Support;
using Microsoft.Extensions.Time.Testing;
using static AncientOrder.Rules.Tests.Support.SaveFixtures;

namespace AncientOrder.Rules.Tests.Session;

// Carried over from src/session/operations/saves.test.ts (27) and tests/session.saves.test.ts (14), plus restart survival with files
// and a mid-fight resume with the same dice.
// Dropped: slot values a typed int cannot hold (1.5, '1', null, undefined, NaN, 'x', 'abc'), and the mid-conversation resume
// (it depended on dialogue). Changed: the trust-persistence test sets trust through CharacterRules; the dialogue parts of the round-trip test are gone.
internal static class SaveSessions
{
    public static GameSession Over(MemorySaveBackend backend, GameContent? content = null) => SessionOver(backend, content);

    public static async Task Put(MemorySaveBackend backend, int slot, string text) =>
        await backend.WriteAsync(KeyFor(slot), Encoding.UTF8.GetBytes(text), Ct);

    public static ErrorCode? CodeOf<T>(Result<T> result) => result.IsOk ? null : result.Error.Code;

    public static GameState State(GameSession session) => SessionAssert.Ok(session.GetState());
}

public class SaveToSlot
{
    [Fact(DisplayName = "saves the active game and returns metadata")]
    public async Task Saves_and_returns_metadata()
    {
        var session = SessionWithGame();
        var res = await session.SaveToSlot(3);
        var meta = SessionAssert.Ok(res);
        Assert.Equal(3, meta.Slot);
        Assert.Equal(TestContent.Now, meta.SavedAt);
        Assert.Equal("Kael", meta.PlayerName);
    }

    [Fact(DisplayName = "writes the save to the backend")]
    public async Task Writes_to_the_backend()
    {
        var backend = new MemorySaveBackend();
        var session = SessionWithGame(backend);
        await session.SaveToSlot(1);
        Assert.NotNull(await backend.ReadAsync("manual-01", Ct));
    }

    [Fact(DisplayName = "returns GAME_NOT_FOUND with no active game")]
    public async Task No_game()
    {
        var res = await SessionOver(new MemorySaveBackend()).SaveToSlot(1);
        var error = SessionAssert.Refused(res, ErrorCode.GAME_NOT_FOUND);
        Assert.Equal("No active game", error.Message);
    }

    [Fact(DisplayName = "returns INVALID_SLOT for out-of-range and non-integer slots")]
    public async Task Invalid_slots()
    {
        var session = SessionWithGame();
        foreach (var slot in new[] { 0, 11, -1, int.MinValue, int.MaxValue })
        {
            SessionAssert.Refused(await session.SaveToSlot(slot), ErrorCode.INVALID_SLOT);
        }
    }

    [Fact(DisplayName = "reports INVALID_SLOT before GAME_NOT_FOUND")]
    public async Task Invalid_slot_comes_first()
    {
        SessionAssert.Refused(await SessionOver(new MemorySaveBackend()).SaveToSlot(0), ErrorCode.INVALID_SLOT);
    }

    [Fact(DisplayName = "uses the route message for an invalid slot")]
    public async Task Route_message()
    {
        var res = await SessionWithGame().SaveToSlot(11);
        Assert.Equal("Slot must be an integer between 1 and 10", SessionAssert.Refused(res, ErrorCode.INVALID_SLOT).Message);
    }

    [Fact(DisplayName = "does not change the session snapshot")]
    public async Task Does_not_change_the_snapshot()
    {
        var session = SessionWithGame();
        var snap = session.Snapshot;
        var calls = 0;
        using var subscription = session.Subscribe(_ => calls++);
        await session.SaveToSlot(1);
        Assert.Same(snap, session.Snapshot);
        Assert.Equal(snap.Version, session.Snapshot.Version);
        Assert.Equal(0, calls);
    }

    [Fact(DisplayName = "maps a backend write failure to STORAGE_ERROR")]
    public async Task Write_failure()
    {
        var backend = new FlakyBackend(new MemorySaveBackend()) { FailWrite = "Not enough storage space to save" };
        var session = SessionWithGame(backend);
        var error = SessionAssert.Refused(await session.SaveToSlot(1), ErrorCode.STORAGE_ERROR);
        Assert.Equal("Not enough storage space to save", error.Message);
    }

    [Fact(DisplayName = "saves the game as it is when called, leaving a mid-fight game saveable")]
    public async Task Saves_a_mid_fight_game()
    {
        var backend = new MemorySaveBackend();
        var session = FightSession(backend);
        var meta = SessionAssert.Ok(await session.SaveToSlot(2));
        Assert.Equal(2, meta.Slot);
        Assert.NotNull(await backend.ReadAsync("manual-02", Ct));
    }
}

public class LoadFromSlot
{
    [Fact(DisplayName = "loads a saved game into the session")]
    public async Task Loads_a_saved_game()
    {
        var session = SessionWithGame();
        session.AdjustPersonality(Adj.Of((AncientOrder.Rules.Personality.Trait.Patience, 8)));
        var before = SaveSessions.State(session);
        await session.SaveToSlot(1);
        session.NewGame();
        var res = await session.LoadFromSlot(1);
        Assert.True(res.IsOk);
        StateAssert.Equivalent(before, SaveSessions.State(session));
    }

    [Fact(DisplayName = "returns SAVE_NOT_FOUND for an empty slot and leaves the game alone")]
    public async Task Empty_slot()
    {
        var session = SessionWithGame();
        var snap = session.Snapshot;
        SessionAssert.Refused(await session.LoadFromSlot(9), ErrorCode.SAVE_NOT_FOUND);
        Assert.Same(snap, session.Snapshot);
    }

    [Fact(DisplayName = "works with no active game")]
    public async Task Works_with_no_game()
    {
        var backend = new MemorySaveBackend();
        var session = SessionWithGame(backend);
        await session.SaveToSlot(2);
        var fresh = SessionOver(backend);
        Assert.Null(fresh.Snapshot.Game);
        Assert.True((await fresh.LoadFromSlot(2)).IsOk);
        Assert.NotNull(fresh.Snapshot.Game);
    }

    [Fact(DisplayName = "returns INVALID_SLOT for bad slots")]
    public async Task Bad_slots()
    {
        var session = SessionWithGame();
        foreach (var slot in new[] { 0, 11, 25, -3 })
        {
            SessionAssert.Refused(await session.LoadFromSlot(slot), ErrorCode.INVALID_SLOT);
        }
    }

    [Fact(DisplayName = "returns VALIDATION_ERROR for a corrupted save and keeps the current game")]
    public async Task Corrupted_save()
    {
        var backend = new MemorySaveBackend();
        var session = SessionWithGame(backend);
        await SaveSessions.Put(backend, 4, "{not json");
        var snap = session.Snapshot;
        var error = SessionAssert.Refused(await session.LoadFromSlot(4), ErrorCode.VALIDATION_ERROR);
        Assert.StartsWith("Save file in slot 4 is corrupted: ", error.Message, StringComparison.Ordinal);
        Assert.Same(snap, session.Snapshot);
    }

    [Fact(DisplayName = "notifies subscribers when the load commits")]
    public async Task Notifies_once()
    {
        var session = SessionWithGame();
        await session.SaveToSlot(1);
        var calls = 0;
        using var subscription = session.Subscribe(_ => calls++);
        await session.LoadFromSlot(1);
        Assert.Equal(1, calls);
    }

    [Fact(DisplayName = "clears pending combat orders")]
    public async Task Clears_pending_orders()
    {
        var session = SessionOver(new MemorySaveBackend());
        session.NewGame();
        session.SetLineup(TestSessions.Pair);
        await session.SaveToSlot(1);
        Assert.True(session.StartEncounter(SessionEncounters.Test).IsOk);
        Assert.True(session.DeclareOrders([TestSessions.Attack(TestCast.ElenaId, "enemy_brute"), TestSessions.Attack(TestCast.LarsId, "enemy_archer")]).IsOk);
        Assert.NotEmpty(session.Snapshot.PendingOrders);

        Assert.True((await session.LoadFromSlot(1)).IsOk);
        Assert.Empty(session.Snapshot.PendingOrders);
        Assert.Null(session.Snapshot.Game!.Combat);
    }

    [Fact(DisplayName = "clears the last fight result")]
    public async Task Clears_the_last_fight_result()
    {
        var session = SessionOver(new MemorySaveBackend(), QuickFight.WinContent);
        session.NewGame();
        session.SetLineup(TestSessions.Pair);
        await session.SaveToSlot(1);
        Assert.True(session.StartEncounter(QuickFight.WinEncounterId).IsOk);
        Assert.Equal(CombatStatus.Victory, QuickFight.FightToEnd(session));
        Assert.NotNull(session.Snapshot.LastCombatOutcome);

        Assert.True((await session.LoadFromSlot(1)).IsOk);
        Assert.Null(session.Snapshot.LastCombatOutcome);
    }

    [Fact(DisplayName = "starts as a fresh snapshot: version up by one, one notification, no pending orders")]
    public async Task Fresh_snapshot()
    {
        var session = FightSession();
        await session.SaveToSlot(5);
        QuickFight.FightToEnd(session, 1);
        session.DeclareOrders([TestSessions.Attack(TestCast.ElenaId, "enemy_brute")]);
        var before = session.Snapshot;
        var seen = new List<SessionSnapshot>();
        using var subscription = session.Subscribe(seen.Add);

        Assert.True((await session.LoadFromSlot(5)).IsOk);
        Assert.Equal(before.Version + 1, session.Snapshot.Version);
        Assert.Single(seen);
        Assert.Same(session.Snapshot, seen[0]);
        Assert.Empty(session.Snapshot.PendingOrders);
        Assert.Null(session.Snapshot.LastCombatOutcome);
        Assert.Equal(1, session.Snapshot.Game!.Combat!.Round);
    }

    [Fact(DisplayName = "succeeds with a report of what the content change adjusted")]
    public async Task Reports_adjustments()
    {
        var backend = new MemorySaveBackend();
        var first = SessionWithGame(backend);
        first.SetLineup(TestSessions.Pair);
        await first.SaveToSlot(1);

        var changed = TestSessions.Content(characters: [TestCast.Elena, TestCast.Kade]);
        var second = SessionOver(backend, changed);
        var report = SessionAssert.Ok(await second.LoadFromSlot(1));
        Assert.Equal(
            [LoadAdjustmentKind.CharacterDropped, LoadAdjustmentKind.LineupCleared],
            report.Adjustments.Select(a => a.Kind));
        Assert.Empty(second.Snapshot.Game!.Lineup);
        Assert.False(second.Snapshot.Game.Characters.ContainsKey(TestCast.LarsId));

        var unchanged = SessionAssert.Ok(await SessionOver(backend).LoadFromSlot(1));
        Assert.True(unchanged.IsEmpty);
    }

    [Fact(DisplayName = "a failing read leaves the current game and reports STORAGE_ERROR")]
    public async Task Read_failure()
    {
        var backend = new FlakyBackend(new MemorySaveBackend()) { FailRead = "Save storage is unavailable" };
        var session = SessionWithGame(backend);
        var snap = session.Snapshot;
        SessionAssert.Refused(await session.LoadFromSlot(1), ErrorCode.STORAGE_ERROR);
        Assert.Same(snap, session.Snapshot);
    }
}

public class ListSlots
{
    [Fact(DisplayName = "lists 10 empty slots on a fresh store")]
    public async Task Ten_empty_slots()
    {
        var slots = SessionAssert.Ok(await SessionOver(new MemorySaveBackend()).ListSlots());
        Assert.Equal(10, slots.Length);
        Assert.All(slots, s => Assert.Equal(SlotStatus.Empty, s.Status));
    }

    [Fact(DisplayName = "works without an active game")]
    public async Task Works_without_a_game()
    {
        Assert.True((await SessionOver(new MemorySaveBackend()).ListSlots()).IsOk);
    }

    [Fact(DisplayName = "reflects a saved slot with metadata")]
    public async Task Reflects_a_saved_slot()
    {
        var session = SessionWithGame();
        await session.SaveToSlot(1);
        var slots = SessionAssert.Ok(await session.ListSlots());
        var slot1 = slots.Single(s => s.Slot == 1);
        Assert.True(slot1.Exists);
        Assert.Equal(SlotStatus.Saved, slot1.Status);
        Assert.Equal(1, slot1.Metadata!.Slot);
        Assert.Equal(TestContent.Now, slot1.Metadata.SavedAt);
        Assert.Equal("Kael", slot1.Metadata.PlayerName);
    }

    [Fact(DisplayName = "shows a corrupted slot as corrupted without failing the list")]
    public async Task Corrupted_slot()
    {
        var backend = new MemorySaveBackend();
        var session = SessionOver(backend);
        await SaveSessions.Put(backend, 3, "garbage");
        var slots = SessionAssert.Ok(await session.ListSlots());
        Assert.Equal(SlotStatus.Corrupted, slots[2].Status);
        Assert.Equal(SlotStatus.Empty, slots[0].Status);
    }

    [Fact(DisplayName = "maps a backend read failure to STORAGE_ERROR")]
    public async Task Read_failure()
    {
        var backend = new FlakyBackend(new MemorySaveBackend()) { FailRead = "Save storage is unavailable" };
        SessionAssert.Refused(await SessionOver(backend).ListSlots(), ErrorCode.STORAGE_ERROR);
    }

    [Fact(DisplayName = "never commits or notifies")]
    public async Task Does_not_commit()
    {
        var session = SessionWithGame();
        var snap = session.Snapshot;
        var calls = 0;
        using var subscription = session.Subscribe(_ => calls++);
        await session.ListSlots();
        Assert.Same(snap, session.Snapshot);
        Assert.Equal(0, calls);
    }
}

public class DeleteSlot
{
    [Fact(DisplayName = "deletes an existing save")]
    public async Task Deletes()
    {
        var session = SessionWithGame();
        await session.SaveToSlot(1);
        var res = await session.DeleteSlot(1);
        Assert.Equal(new DeletedSlot(1, true), SessionAssert.Ok(res));
        SessionAssert.Refused(await session.LoadFromSlot(1), ErrorCode.SAVE_NOT_FOUND);
    }

    [Fact(DisplayName = "returns SAVE_NOT_FOUND for an empty slot")]
    public async Task Empty_slot()
    {
        SessionAssert.Refused(await SessionOver(new MemorySaveBackend()).DeleteSlot(5), ErrorCode.SAVE_NOT_FOUND);
    }

    [Fact(DisplayName = "allows deleting a corrupted slot")]
    public async Task Deletes_a_corrupted_slot()
    {
        var backend = new MemorySaveBackend();
        var session = SessionOver(backend);
        await SaveSessions.Put(backend, 2, "{not json");
        Assert.True((await session.DeleteSlot(2)).IsOk);
        Assert.Null(await backend.ReadAsync(KeyFor(2), Ct));
    }

    [Fact(DisplayName = "returns INVALID_SLOT for bad slots")]
    public async Task Bad_slots()
    {
        var session = SessionWithGame();
        foreach (var slot in new[] { 0, 11, -5 })
        {
            SessionAssert.Refused(await session.DeleteSlot(slot), ErrorCode.INVALID_SLOT);
        }
    }

    [Fact(DisplayName = "does not touch the active game")]
    public async Task Does_not_touch_the_game()
    {
        var session = SessionWithGame();
        await session.SaveToSlot(1);
        var snap = session.Snapshot;
        var calls = 0;
        using var subscription = session.Subscribe(_ => calls++);
        await session.DeleteSlot(1);
        Assert.Same(snap, session.Snapshot);
        Assert.Equal(0, calls);
    }

    [Fact(DisplayName = "maps a backend remove failure to STORAGE_ERROR")]
    public async Task Remove_failure()
    {
        var inner = new MemorySaveBackend();
        var session = SessionWithGame(new FlakyBackend(inner) { FailRemove = "Save storage is unavailable" });
        await session.SaveToSlot(1);
        SessionAssert.Refused(await session.DeleteSlot(1), ErrorCode.STORAGE_ERROR);
        Assert.NotNull(await inner.ReadAsync(KeyFor(1), Ct));
    }
}

// Port of the save/load portions of tests/sprint1.integration.test.ts and tests/sprint4.integration.test.ts (tests/session.saves.test.ts).
public class FullSessionFlowSaves
{
    [Fact(DisplayName = "save → load round-trip preserves state")]
    public async Task Round_trip_preserves_state()
    {
        var session = SessionWithGame();
        session.AdjustPersonality(Adj.Of((AncientOrder.Rules.Personality.Trait.Patience, 8)));
        var preSave = SaveSessions.State(session);

        var saved = await session.SaveToSlot(1);
        Assert.Equal(1, SessionAssert.Ok(saved).Slot);

        session.AdjustPersonality(Adj.Of((AncientOrder.Rules.Personality.Trait.Cunning, 5)));
        session.NewGame();
        Assert.NotEqual(preSave.Player.Personality, SaveSessions.State(session).Player.Personality);

        var loadRes = await session.LoadFromSlot(1);
        Assert.True(loadRes.IsOk);
        var loaded = SaveSessions.State(session);
        Assert.Equal(preSave.Player.Personality, loaded.Player.Personality);
        Assert.Null(loaded.Combat);
        Assert.True(Math.Abs(AncientOrder.Rules.Tests.Support.TestPersonality.Values(loaded.Player.Personality).Sum() - 100) < 0.02);
    }

    [Fact(DisplayName = "NPC trust changes persist across save/load")]
    public async Task Trust_persists()
    {
        var backend = new MemorySaveBackend();
        var session = SessionOver(backend);
        session.NewGame();
        var changed = CharacterRulesTrust(SaveSessions.State(session));
        Assert.Equal(3, changed.Characters[TestCast.ElenaId].Trust);

        Assert.True((await new SaveStore(backend, Content).SaveAsync(2, changed, TestContent.Now, Ct)).IsOk);
        session.NewGame();
        Assert.Equal(0, SaveSessions.State(session).Characters[TestCast.ElenaId].Trust);

        Assert.True((await session.LoadFromSlot(2)).IsOk);
        Assert.Equal(3, SaveSessions.State(session).Characters[TestCast.ElenaId].Trust);

        await session.SaveToSlot(3);
        session.NewGame();
        await session.LoadFromSlot(3);
        Assert.Equal(3, SaveSessions.State(session).Characters[TestCast.ElenaId].Trust);
    }

    private static GameState CharacterRulesTrust(GameState game)
    {
        var changed = CharacterRules.ChangeTrust(Content, game, TestCast.ElenaId, 3, TestContent.Now);
        return SessionAssert.Ok(changed);
    }
}

public class ErrorPathsSaves
{
    [Fact(DisplayName = "saveToSlot returns GAME_NOT_FOUND when no active game exists")]
    public async Task Save_without_a_game()
    {
        var res = await SessionOver(new MemorySaveBackend()).SaveToSlot(1);
        Assert.False(res.IsOk);
        Assert.Equal(ErrorCode.GAME_NOT_FOUND, res.Error.Code);
    }

    [Fact(DisplayName = "saveToSlot(0) returns INVALID_SLOT for slot 0 (below minimum)")]
    public async Task Save_zero() =>
        SessionAssert.Refused(await SessionWithGame().SaveToSlot(0), ErrorCode.INVALID_SLOT);

    [Fact(DisplayName = "saveToSlot(11) returns INVALID_SLOT for slot 11 (above maximum)")]
    public async Task Save_eleven() =>
        SessionAssert.Refused(await SessionWithGame().SaveToSlot(11), ErrorCode.INVALID_SLOT);

    [Fact(DisplayName = "loadFromSlot(0) returns INVALID_SLOT for slot 0")]
    public async Task Load_zero() =>
        SessionAssert.Refused(await SessionOver(new MemorySaveBackend()).LoadFromSlot(0), ErrorCode.INVALID_SLOT);

    [Fact(DisplayName = "loadFromSlot(11) returns INVALID_SLOT for slot 11")]
    public async Task Load_eleven() =>
        SessionAssert.Refused(await SessionOver(new MemorySaveBackend()).LoadFromSlot(11), ErrorCode.INVALID_SLOT);

    [Fact(DisplayName = "loadFromSlot(9) returns SAVE_NOT_FOUND for a slot with no save")]
    public async Task Load_empty_slot()
    {
        var res = await SessionOver(new MemorySaveBackend()).LoadFromSlot(9);
        Assert.False(res.IsOk);
        Assert.Equal(ErrorCode.SAVE_NOT_FOUND, res.Error.Code);
    }
}

public class SavesList
{
    [Fact(DisplayName = "returns 10 save slot entries")]
    public async Task Ten_entries()
    {
        var res = await SessionOver(new MemorySaveBackend()).ListSlots();
        Assert.True(res.IsOk);
        Assert.Equal(10, res.Value.Length);
    }

    [Fact(DisplayName = "all slots empty on fresh session")]
    public async Task All_empty()
    {
        var res = await SessionOver(new MemorySaveBackend()).ListSlots();
        Assert.All(res.Value, s => Assert.False(s.Exists));
    }

    [Fact(DisplayName = "reflects saved slot after save")]
    public async Task Reflects_the_save()
    {
        var session = SessionWithGame();
        Assert.True((await session.SaveToSlot(1)).IsOk);
        var res = await session.ListSlots();
        var slot1 = res.Value.Single(s => s.Slot == 1);
        Assert.True(slot1.Exists);
        Assert.NotNull(slot1.Metadata);
        Assert.Equal(1, slot1.Metadata.Slot);
        Assert.False(string.IsNullOrEmpty(slot1.Metadata.PlayerName));
        Assert.True(slot1.Metadata.SavedAt > 0);
    }
}

public class SavesDelete
{
    [Fact(DisplayName = "returns deleted: true for existing save")]
    public async Task Deleted_true()
    {
        var session = SessionWithGame();
        await session.SaveToSlot(1);
        var res = await session.DeleteSlot(1);
        Assert.True(res.IsOk);
        Assert.True(res.Value.Deleted);
        Assert.Equal(1, res.Value.Slot);
    }

    [Fact(DisplayName = "returns INVALID_SLOT for invalid slot (slot 0 is below minimum)")]
    public async Task Invalid_slot() =>
        SessionAssert.Refused(await SessionOver(new MemorySaveBackend()).DeleteSlot(0), ErrorCode.INVALID_SLOT);

    [Fact(DisplayName = "returns INVALID_SLOT for non-numeric slot")]
    public async Task Non_numeric_slot()
    {
        // A typed slot cannot be non-numeric; the closest case is the far ends of the int range.
        var session = SessionOver(new MemorySaveBackend());
        SessionAssert.Refused(await session.DeleteSlot(int.MinValue), ErrorCode.INVALID_SLOT);
        SessionAssert.Refused(await session.DeleteSlot(int.MaxValue), ErrorCode.INVALID_SLOT);
    }
}

// New: saves kept as files survive a restart. A restart is a new FileSaveBackend, SaveStore, and GameSession over the same folder.
public class RestartSurvival
{
    [Fact(DisplayName = "a game saved to files loads back equivalent after a restart")]
    public async Task Game_survives_a_restart()
    {
        using var temp = new TempFolder();
        var first = SessionOver(new FileSaveBackend(temp.Path));
        first.NewGame();
        first.AdjustPersonality(Adj.Of((AncientOrder.Rules.Personality.Trait.Patience, 8)));
        first.SetLineup(TestSessions.Pair);
        var saved = SaveSessions.State(first);
        Assert.True((await first.SaveToSlot(4)).IsOk);
        Assert.Equal(["manual-04.json"], temp.FileNames());

        var second = SessionOver(new FileSaveBackend(temp.Path));
        Assert.Null(second.Snapshot.Game);
        var report = SessionAssert.Ok(await second.LoadFromSlot(4));
        Assert.True(report.IsEmpty);
        StateAssert.Equivalent(saved, SaveSessions.State(second));

        var slots = SessionAssert.Ok(await second.ListSlots());
        Assert.Equal(SlotStatus.Saved, slots[3].Status);
        Assert.Equal("Kael", slots[3].Metadata!.PlayerName);
        Assert.All(slots.Where(s => s.Slot != 4), s => Assert.Equal(SlotStatus.Empty, s.Status));
    }

    [Fact(DisplayName = "the bytes on disk are what the format wrote, and re-saving after a restart gives the same bytes")]
    public async Task Bytes_survive_a_restart()
    {
        using var temp = new TempFolder();
        var first = SessionOver(new FileSaveBackend(temp.Path));
        first.NewGame();
        await first.SaveToSlot(1);
        var onDisk = await File.ReadAllBytesAsync(temp.File("manual-01.json"), Ct);

        var second = SessionOver(new FileSaveBackend(temp.Path));
        await second.LoadFromSlot(1);
        await second.SaveToSlot(1);
        Assert.Equal(onDisk, await File.ReadAllBytesAsync(temp.File("manual-01.json"), Ct));
    }

    [Fact(DisplayName = "a fight saved to files resumes after a restart with the same dice")]
    public async Task Fight_survives_a_restart_with_the_same_dice()
    {
        using var temp = new TempFolder();
        var original = FightSession(new FileSaveBackend(temp.Path));
        Assert.Equal(CombatStatus.Active, QuickFight.FightToEnd(original, 2));
        Assert.True((await original.SaveToSlot(7)).IsOk);

        var restarted = SessionOver(new FileSaveBackend(temp.Path), LongFightContent, seeds: new FixedSeedSource(999));
        Assert.True((await restarted.LoadFromSlot(7)).IsOk);
        StateAssert.Equivalent(original.Snapshot.Game, restarted.Snapshot.Game);

        for (var round = 0; round < 4; round++)
        {
            Assert.Equal(CombatStatus.Active, QuickFight.FightToEnd(original, 1));
            Assert.Equal(CombatStatus.Active, QuickFight.FightToEnd(restarted, 1));
            StateAssert.Equivalent(original.Snapshot.Game, restarted.Snapshot.Game);
        }

        Assert.Equal(7, original.Snapshot.Game!.Combat!.Round);
        Assert.Equal(6, original.Snapshot.Game.Combat.RoundHistory.Length);
    }

    [Fact(DisplayName = "a damaged file is refused after a restart and a deleted slot stays deleted")]
    public async Task Damaged_and_deleted_files()
    {
        using var temp = new TempFolder();
        var first = SessionOver(new FileSaveBackend(temp.Path));
        first.NewGame();
        await first.SaveToSlot(1);
        await first.SaveToSlot(2);
        await first.DeleteSlot(1);
        var path = temp.File("manual-02.json");
        var bytes = await File.ReadAllBytesAsync(path, Ct);
        await File.WriteAllBytesAsync(path, bytes[..(bytes.Length / 2)], Ct);

        var second = SessionOver(new FileSaveBackend(temp.Path));
        second.NewGame();
        var snap = second.Snapshot;
        SessionAssert.Refused(await second.LoadFromSlot(2), ErrorCode.VALIDATION_ERROR);
        SessionAssert.Refused(await second.LoadFromSlot(1), ErrorCode.SAVE_NOT_FOUND);
        Assert.Same(snap, second.Snapshot);
        var slots = SessionAssert.Ok(await second.ListSlots());
        Assert.Equal(SlotStatus.Empty, slots[0].Status);
        Assert.Equal(SlotStatus.Corrupted, slots[1].Status);
    }

    [Fact(DisplayName = "an oversized file is refused as too large after a restart")]
    public async Task Oversized_file()
    {
        using var temp = new TempFolder();
        Directory.CreateDirectory(temp.Path);
        await File.WriteAllBytesAsync(temp.File("manual-03.json"), new byte[3_000_000], Ct);
        var session = SessionOver(new FileSaveBackend(temp.Path));
        var error = SessionAssert.Refused(await session.LoadFromSlot(3), ErrorCode.VALIDATION_ERROR);
        Assert.Contains("save too large", error.Message, StringComparison.Ordinal);
        Assert.Equal(SlotStatus.Corrupted, SessionAssert.Ok(await session.ListSlots())[2].Status);
    }
}

// New: a game saved mid-fight and loaded resolves the next rounds exactly as the game that was not saved.
public class MidFightResume
{
    [Fact(DisplayName = "the next rounds after a reload resolve exactly as without saving")]
    public async Task Same_dice_after_reload()
    {
        var backend = new MemorySaveBackend();
        var played = FightSession(backend);
        Assert.Equal(CombatStatus.Active, QuickFight.FightToEnd(played, 3));
        await played.SaveToSlot(1);

        var reloaded = SessionOver(backend, LongFightContent, seeds: new FixedSeedSource(12345));
        Assert.True((await reloaded.LoadFromSlot(1)).IsOk);
        StateAssert.Equivalent(played.Snapshot.Game, reloaded.Snapshot.Game);

        for (var round = 0; round < 6; round++)
        {
            QuickFight.FightToEnd(played, 1);
            QuickFight.FightToEnd(reloaded, 1);
            StateAssert.Equivalent(played.Snapshot.Game, reloaded.Snapshot.Game);
        }

        Assert.Equal(10, played.Snapshot.Game!.Combat!.Round);
    }

    [Fact(DisplayName = "a different seed would have resolved differently, so the saved dice are what carry over")]
    public async Task Saved_dice_not_the_session_seed()
    {
        var backend = new MemorySaveBackend();
        var played = FightSession(backend);
        QuickFight.FightToEnd(played, 1);
        await played.SaveToSlot(1);
        QuickFight.FightToEnd(played, 4);

        var other = FightSession(seeds: new FixedSeedSource(TestSessions.Seed + 1));
        QuickFight.FightToEnd(other, 5);
        Assert.NotEqual(played.Snapshot.Game!.Combat!.Dice, other.Snapshot.Game!.Combat!.Dice);

        var reloaded = SessionOver(backend, LongFightContent);
        await reloaded.LoadFromSlot(1);
        QuickFight.FightToEnd(reloaded, 4);
        Assert.Equal(played.Snapshot.Game.Combat.Dice, reloaded.Snapshot.Game!.Combat!.Dice);
    }
}
