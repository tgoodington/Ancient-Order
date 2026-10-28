using System.Collections.Immutable;
using System.Text;
using System.Text.Json.Nodes;
using AncientOrder.Rules.Combat;
using AncientOrder.Rules.Common;
using AncientOrder.Rules.Saves;
using AncientOrder.Rules.Session;
using AncientOrder.Rules.State;
using AncientOrder.Rules.Tests.Fixtures;
using AncientOrder.Rules.Tests.Support;
using static AncientOrder.Rules.Tests.Support.SaveFixtures;

namespace AncientOrder.Rules.Tests.Saves;

// Carried over from src/persistence/saveStore.test.ts and saveStore.smoke.test.ts, over the in-memory backend.
// Keys are manual-01 to manual-10 (bytes, not strings). Dropped: invalid slot values a typed int cannot hold
// (1.5, NaN, a string, undefined), the narrativeState and dialogue parts of the populated round trip, and the
// older-save normalization tests, which became upgrade-step tests in SaveFormatTests.
internal static class StoreEnv
{
    public static (MemorySaveBackend Backend, SaveStore Store) Create()
    {
        var backend = new MemorySaveBackend();
        return (backend, new SaveStore(backend, Content));
    }

    public static async Task Put(MemorySaveBackend backend, int slot, string text) =>
        await backend.WriteAsync(KeyFor(slot), Encoding.UTF8.GetBytes(text), Ct);

    public static async Task Put(MemorySaveBackend backend, int slot, JsonNode doc) =>
        await backend.WriteAsync(KeyFor(slot), Utf8(doc), Ct);

    public static JsonObject Doc(Action<JsonObject>? editEnvelope = null, Action<JsonObject>? editGame = null, string name = "Rin")
    {
        var doc = SaveFixtures.Doc(MakeGame(name));
        editGame?.Invoke((JsonObject)doc["game"]!);
        editEnvelope?.Invoke(doc);
        return doc;
    }

    public static async Task<RuleError> LoadFailure(SaveStore store, int slot)
    {
        var result = await store.LoadAsync(slot, Ct);
        Assert.False(result.IsOk, "expected a refusal");
        return result.Error;
    }
}

public class Constants
{
    [Fact(DisplayName = "exports MIN_SLOT as 1")]
    public void Min_slot() => Assert.Equal(1, SaveConstants.MinSlot);

    [Fact(DisplayName = "exports MAX_SLOT as 10")]
    public void Max_slot() => Assert.Equal(10, SaveConstants.MaxSlot);
}

public class SaveGameLoadGameRoundTrip
{
    [Fact(DisplayName = "saves and loads state with deep equality")]
    public async Task Deep_equality()
    {
        var (_, store) = StoreEnv.Create();
        var state = MakeGame();
        Assert.True((await store.SaveAsync(1, state, TestContent.Now, Ct)).IsOk);
        var loaded = (await store.LoadAsync(1, Ct)).Value;
        StateAssert.Equivalent(state, loaded.Game);
        Assert.True(loaded.Report.IsEmpty);
    }

    [Fact(DisplayName = "returned SaveMetadata contains correct slot and playerName")]
    public async Task Metadata()
    {
        var (_, store) = StoreEnv.Create();
        var meta = (await store.SaveAsync(3, MakeGame("Kira"), 1234, Ct)).Value;
        Assert.Equal(3, meta.Slot);
        Assert.Equal("Kira", meta.PlayerName);
        Assert.Equal(1234, meta.SavedAt);
    }

    [Fact(DisplayName = "writes valid JSON under the slot key")]
    public async Task Writes_json_under_the_key()
    {
        var (backend, store) = StoreEnv.Create();
        await store.SaveAsync(2, MakeGame(), TestContent.Now, Ct);
        Assert.Equal("manual-02", KeyFor(2));
        var raw = await backend.ReadAsync("manual-02", Ct);
        Assert.NotNull(raw);
        Assert.NotNull(JsonNode.Parse(raw));
    }

    [Fact(DisplayName = "overwrites an existing save in the same slot")]
    public async Task Overwrites()
    {
        var (_, store) = StoreEnv.Create();
        await store.SaveAsync(1, MakeGame("First"), 1, Ct);
        await store.SaveAsync(1, MakeGame("Second"), 2, Ct);
        Assert.Equal("Second", (await store.LoadAsync(1, Ct)).Value.Game.Player.Name);
    }

    [Fact(DisplayName = "returns a timestamp matching the one listed for the slot")]
    public async Task Timestamp_matches_listing()
    {
        var (backend, store) = StoreEnv.Create();
        var meta = (await store.SaveAsync(4, MakeGame(), 987_654, Ct)).Value;
        var info = (await store.ListAsync(Ct)).Value[3];
        Assert.Equal(meta.SavedAt, info.Metadata!.SavedAt);
        var envelope = JsonNode.Parse((await backend.ReadAsync("manual-04", Ct))!)!;
        Assert.Equal(meta.SavedAt, (long)envelope["savedAt"]!);
    }

    [Fact(DisplayName = "round-trips a populated reactionProgress, team, and narrativeState")]
    public async Task Populated_round_trip()
    {
        var (_, store) = StoreEnv.Create();
        var state = MakeGame() with
        {
            Lineup = [TestCast.ElenaId, TestCast.LarsId],
            ReactionProgress = ImmutableDictionary<string, ReactionProgress>.Empty
                .WithComparers(StringComparer.Ordinal)
                .Add("player_1", new ReactionProgress(215, 4, 0)),
        };
        await store.SaveAsync(5, state, TestContent.Now, Ct);
        StateAssert.Equivalent(state, (await store.LoadAsync(5, Ct)).Value.Game);
    }
}

public class SlotIndependence
{
    [Fact(DisplayName = "saves to different slots independently")]
    public async Task Different_slots()
    {
        var (_, store) = StoreEnv.Create();
        await store.SaveAsync(1, MakeGame("Alice"), 1, Ct);
        await store.SaveAsync(2, MakeGame("Bob"), 1, Ct);
        Assert.Equal("Alice", (await store.LoadAsync(1, Ct)).Value.Game.Player.Name);
        Assert.Equal("Bob", (await store.LoadAsync(2, Ct)).Value.Game.Player.Name);
    }

    [Fact(DisplayName = "supports all 10 slots independently")]
    public async Task All_ten_slots()
    {
        var (_, store) = StoreEnv.Create();
        for (var slot = 1; slot <= 10; slot++)
        {
            await store.SaveAsync(slot, MakeGame("Player" + slot), 1, Ct);
        }

        for (var slot = 1; slot <= 10; slot++)
        {
            Assert.Equal("Player" + slot, (await store.LoadAsync(slot, Ct)).Value.Game.Player.Name);
        }
    }
}

public class LoadGameMissingSlotError
{
    [Fact(DisplayName = "rejects with SAVE_NOT_FOUND for an empty slot")]
    public async Task Save_not_found()
    {
        var (_, store) = StoreEnv.Create();
        Assert.Equal(ErrorCode.SAVE_NOT_FOUND, (await StoreEnv.LoadFailure(store, 1)).Code);
        var seven = await StoreEnv.LoadFailure(store, 7);
        Assert.Equal(ErrorCode.SAVE_NOT_FOUND, seven.Code);
        Assert.Equal("No save found in slot 7.", seven.Message);
    }
}

public class InvalidSlotErrors
{
    private static async Task AllThreeRefuse(int slot)
    {
        var (_, store) = StoreEnv.Create();
        Assert.Equal(ErrorCode.INVALID_SLOT, Refused((await store.SaveAsync(slot, MakeGame(), 1, Ct)), ErrorCode.INVALID_SLOT).Code);
        Assert.Equal(ErrorCode.INVALID_SLOT, Refused(await store.LoadAsync(slot, Ct), ErrorCode.INVALID_SLOT).Code);
        Assert.Equal(ErrorCode.INVALID_SLOT, Refused(await store.DeleteAsync(slot, Ct), ErrorCode.INVALID_SLOT).Code);
    }

    [Fact(DisplayName = "save, load, and remove throw INVALID_SLOT for 0")]
    public Task Zero() => AllThreeRefuse(0);

    [Fact(DisplayName = "save, load, and remove throw INVALID_SLOT for 11")]
    public Task Eleven() => AllThreeRefuse(11);

    [Fact(DisplayName = "save, load, and remove throw INVALID_SLOT for -1")]
    public Task Minus_one() => AllThreeRefuse(-1);

    [Fact(DisplayName = "writes nothing for an invalid slot")]
    public async Task Writes_nothing()
    {
        var (_, store) = StoreEnv.Create();
        await store.SaveAsync(0, MakeGame(), 1, Ct);
        await store.SaveAsync(11, MakeGame(), 1, Ct);
        Assert.All((await store.ListAsync(Ct)).Value, i => Assert.Equal(SlotStatus.Empty, i.Status));
    }

    [Fact(DisplayName = "accepts the boundary slots 1 and 10")]
    public async Task Boundary_slots()
    {
        var (_, store) = StoreEnv.Create();
        await store.SaveAsync(1, MakeGame("Low"), 1, Ct);
        await store.SaveAsync(10, MakeGame("High"), 1, Ct);
        Assert.Equal("Low", (await store.LoadAsync(1, Ct)).Value.Game.Player.Name);
        Assert.Equal("High", (await store.LoadAsync(10, Ct)).Value.Game.Player.Name);
    }

    [Fact(DisplayName = "the store's INVALID_SLOT message names the slot and range")]
    public async Task Store_message()
    {
        var (_, store) = StoreEnv.Create();
        Assert.Equal("Invalid slot number: 11. Must be between 1 and 10.", (await StoreEnv.LoadFailure(store, 11)).Message);
    }

    [Fact(DisplayName = "differs from the session route's message")]
    public async Task Differs_from_the_session_message()
    {
        var (_, store) = StoreEnv.Create();
        var session = new GameSession(Content, new GameSessionOptions { Saves = store });
        var res = await session.LoadFromSlot(11);
        var sessionMessage = Refused(res, ErrorCode.INVALID_SLOT).Message;
        Assert.Equal("Slot must be an integer between 1 and 10", sessionMessage);
        Assert.NotEqual((await StoreEnv.LoadFailure(store, 11)).Message, sessionMessage);
    }
}

public class ListSaves
{
    [Fact(DisplayName = "returns 10 entries ordered 1-10")]
    public async Task Ten_entries()
    {
        var (_, store) = StoreEnv.Create();
        var infos = (await store.ListAsync(Ct)).Value;
        Assert.Equal(10, infos.Length);
        Assert.Equal([1, 2, 3, 4, 5, 6, 7, 8, 9, 10], infos.Select(i => i.Slot));
    }

    [Fact(DisplayName = "all slots are empty on a fresh backend")]
    public async Task All_empty()
    {
        var (_, store) = StoreEnv.Create();
        Assert.All((await store.ListAsync(Ct)).Value, i =>
        {
            Assert.False(i.Exists);
            Assert.Equal(SlotStatus.Empty, i.Status);
        });
    }

    [Fact(DisplayName = "reflects saved slots with status and metadata")]
    public async Task Reflects_saved_slots()
    {
        var (_, store) = StoreEnv.Create();
        await store.SaveAsync(1, MakeGame("Player1"), 111, Ct);
        await store.SaveAsync(5, MakeGame("Player5"), 555, Ct);
        var infos = (await store.ListAsync(Ct)).Value;
        Assert.Equal(new SlotInfo(1, true, SlotStatus.Saved, new SaveMetadata(1, 111, "Player1")), infos[0]);
        Assert.Equal("Player5", infos[4].Metadata!.PlayerName);
        Assert.False(infos[2].Exists);
        Assert.Equal(SlotStatus.Empty, infos[2].Status);
        Assert.Null(infos[2].Metadata);
    }

    [Fact(DisplayName = "lets a backend STORAGE_ERROR fail the whole call")]
    public async Task Storage_error_fails_the_call()
    {
        var backend = new FlakyBackend(new MemorySaveBackend()) { FailRead = "Save storage is unavailable" };
        var listed = await new SaveStore(backend, Content).ListAsync(Ct);
        Assert.Equal(ErrorCode.STORAGE_ERROR, Refused(listed, ErrorCode.STORAGE_ERROR).Code);
    }
}

public class DeleteSave
{
    [Fact(DisplayName = "deletes an existing save")]
    public async Task Deletes()
    {
        var (_, store) = StoreEnv.Create();
        await store.SaveAsync(1, MakeGame(), 1, Ct);
        Assert.Equal(1, (await store.DeleteAsync(1, Ct)).Value);
        Assert.Equal(ErrorCode.SAVE_NOT_FOUND, (await StoreEnv.LoadFailure(store, 1)).Code);
    }

    [Fact(DisplayName = "throws SAVE_NOT_FOUND when deleting an empty slot")]
    public async Task Empty_slot()
    {
        var (_, store) = StoreEnv.Create();
        Assert.Equal(ErrorCode.SAVE_NOT_FOUND, Refused(await store.DeleteAsync(1, Ct), ErrorCode.SAVE_NOT_FOUND).Code);
    }

    [Fact(DisplayName = "does not affect other slots when one is deleted")]
    public async Task Other_slots_untouched()
    {
        var (_, store) = StoreEnv.Create();
        await store.SaveAsync(1, MakeGame("A"), 1, Ct);
        await store.SaveAsync(2, MakeGame("B"), 1, Ct);
        await store.DeleteAsync(1, Ct);
        Assert.Equal("B", (await store.LoadAsync(2, Ct)).Value.Game.Player.Name);
    }

    [Fact(DisplayName = "deleted slot shows as empty in the listing")]
    public async Task Shows_empty()
    {
        var (_, store) = StoreEnv.Create();
        await store.SaveAsync(3, MakeGame(), 1, Ct);
        await store.DeleteAsync(3, Ct);
        Assert.Equal(SlotStatus.Empty, (await store.ListAsync(Ct)).Value[2].Status);
    }

    [Fact(DisplayName = "allows deleting a corrupted slot")]
    public async Task Deletes_corrupted()
    {
        var (backend, store) = StoreEnv.Create();
        await StoreEnv.Put(backend, 2, "{not json");
        Assert.True((await store.DeleteAsync(2, Ct)).IsOk);
        Assert.Equal(SlotStatus.Empty, (await store.ListAsync(Ct)).Value[1].Status);
    }
}

public class CorruptedSaves
{
    private static async Task AssertCorrupted(MemorySaveBackend backend, SaveStore store, int slot)
    {
        var error = await StoreEnv.LoadFailure(store, slot);
        Assert.Equal(ErrorCode.VALIDATION_ERROR, error.Code);
        Assert.StartsWith($"Save file in slot {slot} is corrupted: ", error.Message, StringComparison.Ordinal);
        Assert.Equal(SlotStatus.Corrupted, (await store.ListAsync(Ct)).Value[slot - 1].Status);
        Assert.NotNull(await backend.ReadAsync(KeyFor(slot), Ct));
    }

    [Fact(DisplayName = "invalid JSON: load is VALIDATION_ERROR, listing is corrupted")]
    public async Task Invalid_json()
    {
        var (backend, store) = StoreEnv.Create();
        await StoreEnv.Put(backend, 1, "{not json");
        var error = await StoreEnv.LoadFailure(store, 1);
        Assert.Equal(ErrorCode.VALIDATION_ERROR, error.Code);
        Assert.Contains("invalid JSON", error.Message, StringComparison.Ordinal);
        Assert.Equal("Save file in slot 1 is corrupted: invalid JSON", error.Message);
        Assert.Equal(new SlotInfo(1, false, SlotStatus.Corrupted, null), (await store.ListAsync(Ct)).Value[0]);
    }

    [Fact(DisplayName = "non-object JSON (array, null, number) is corrupted")]
    public async Task Non_object_json()
    {
        var (backend, store) = StoreEnv.Create();
        foreach (var raw in new[] { "[]", "null", "42" })
        {
            await StoreEnv.Put(backend, 1, raw);
            Assert.Equal(ErrorCode.VALIDATION_ERROR, (await StoreEnv.LoadFailure(store, 1)).Code);
        }
    }

    [Fact(DisplayName = "wrong format tag is corrupted")]
    public async Task Wrong_format_tag()
    {
        var (backend, store) = StoreEnv.Create();
        await StoreEnv.Put(backend, 1, StoreEnv.Doc(e => e["format"] = "something-else"));
        await AssertCorrupted(backend, store, 1);
    }

    [Fact(DisplayName = "unsupported version is corrupted")]
    public async Task Unsupported_version()
    {
        var (backend, store) = StoreEnv.Create();
        await StoreEnv.Put(backend, 1, StoreEnv.Doc(e => e["version"] = 99));
        await AssertCorrupted(backend, store, 1);
        Assert.Contains("unsupported version 99", (await StoreEnv.LoadFailure(store, 1)).Message, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "invalid game is corrupted")]
    public async Task Invalid_game()
    {
        var (backend, store) = StoreEnv.Create();
        await StoreEnv.Put(backend, 1, StoreEnv.Doc(e => e["game"] = new JsonObject { ["player"] = null }));
        await AssertCorrupted(backend, store, 1);
    }

    [Fact(DisplayName = "a bad kind, slot, savedAt, or playerName is corrupted")]
    public async Task Bad_envelope_fields()
    {
        var (backend, store) = StoreEnv.Create();
        var cases = new (string Field, JsonNode? Value)[]
        {
            ("kind", "auto"),
            ("slot", 0),
            ("slot", 11),
            ("slot", 1.5),
            ("savedAt", "yesterday"),
            ("savedAt", null),
            ("playerName", 5),
        };
        foreach (var (field, value) in cases)
        {
            await StoreEnv.Put(backend, 1, StoreEnv.Doc(e => e[field] = value?.DeepClone()));
            await AssertCorrupted(backend, store, 1);
        }
    }

    [Fact(DisplayName = "one corrupted slot does not hide the others in the listing")]
    public async Task One_corrupted_slot()
    {
        var (backend, store) = StoreEnv.Create();
        await store.SaveAsync(1, MakeGame("Good"), 1, Ct);
        await StoreEnv.Put(backend, 2, "{not json");
        var infos = (await store.ListAsync(Ct)).Value;
        Assert.Equal(SlotStatus.Saved, infos[0].Status);
        Assert.Equal(SlotStatus.Corrupted, infos[1].Status);
        Assert.Equal(SlotStatus.Empty, infos[2].Status);
    }

    [Fact(DisplayName = "a game that makes validation throw is corrupted, not a crash")]
    public async Task Structurally_broken_game()
    {
        var (backend, store) = StoreEnv.Create();
        await StoreEnv.Put(backend, 1, StoreEnv.Doc(editGame: g => g["characters"] = new JsonArray(JsonValue.Create<string?>(null))));
        await store.SaveAsync(2, MakeGame("Fine"), 1, Ct);
        var error = await StoreEnv.LoadFailure(store, 1);
        Assert.Equal(ErrorCode.VALIDATION_ERROR, error.Code);
        var infos = (await store.ListAsync(Ct)).Value;
        Assert.Equal(SlotStatus.Corrupted, infos[0].Status);
        Assert.Equal(SlotStatus.Saved, infos[1].Status);
    }

    [Fact(DisplayName = "a raw save longer than 2,000,000 characters is corrupted as too large")]
    public async Task Too_large()
    {
        var (backend, store) = StoreEnv.Create();
        await StoreEnv.Put(backend, 1, StoreEnv.Doc(e => e["padding"] = new string('x', 2_000_001)));
        var error = await StoreEnv.LoadFailure(store, 1);
        Assert.Equal(ErrorCode.VALIDATION_ERROR, error.Code);
        Assert.Contains("save too large", error.Message, StringComparison.Ordinal);
        Assert.Equal(SlotStatus.Corrupted, (await store.ListAsync(Ct)).Value[0].Status);
    }

    [Fact(DisplayName = "caps corrupted reasons at 10, each at most 200 characters")]
    public async Task Caps_reasons()
    {
        var (backend, store) = StoreEnv.Create();
        var characters = new JsonArray();
        for (var i = 0; i < 30; i++)
        {
            characters.Add(new JsonObject { ["id"] = "character-" + i, ["affection"] = 500, ["trust"] = -500 });
        }

        await StoreEnv.Put(backend, 1, StoreEnv.Doc(e => { e["kind"] = "auto"; e["slot"] = 0; }, g => g["characters"] = characters));
        var error = await StoreEnv.LoadFailure(store, 1);
        var reasons = error.Message["Save file in slot 1 is corrupted: ".Length..].Split("; ");
        Assert.True(reasons.Length <= 10, "reasons: " + reasons.Length);
        Assert.All(reasons, r => Assert.True(r.Length <= 200));
        Assert.Equal(10, error.Details.Length);
        Assert.All(error.Details, d => Assert.True(d.Message.Length <= 200));
    }

    [Fact(DisplayName = "a crafted save with __proto__ / constructor keys does not pollute Object.prototype")]
    public async Task Proto_keys_are_ignored()
    {
        var (backend, store) = StoreEnv.Create();
        var doc = StoreEnv.Doc();
        doc["__proto__"] = new JsonObject { ["polluted"] = "envelope" };
        doc["constructor"] = new JsonObject { ["prototype"] = new JsonObject { ["polluted"] = "ctor" } };
        var game = (JsonObject)doc["game"]!;
        game["__proto__"] = new JsonObject { ["polluted"] = "game" };
        game["constructor"] = new JsonObject { ["prototype"] = new JsonObject { ["polluted"] = "gameCtor" } };
        await StoreEnv.Put(backend, 1, doc);

        var loaded = (await store.LoadAsync(1, Ct)).Value;
        await store.ListAsync(Ct);

        StateAssert.Equivalent(MakeGame(), loaded.Game);
        Assert.Equal(typeof(GameState), loaded.Game.GetType());
    }
}

public class ValidationOnSave
{
    [Fact(DisplayName = "rejects an invalid game with VALIDATION_ERROR and writes nothing")]
    public async Task Rejects_and_writes_nothing()
    {
        var (backend, store) = StoreEnv.Create();
        var game = MakeGame();
        var bad = game with { Characters = game.Characters.SetItem(TestCast.ElenaId, new CharacterState(500, 0)) };
        Assert.Equal(ErrorCode.VALIDATION_ERROR, Refused(await store.SaveAsync(1, bad, 1, Ct)).Code);
        Assert.Null(await backend.ReadAsync(KeyFor(1), Ct));
        Assert.Equal(SlotStatus.Empty, (await store.ListAsync(Ct)).Value[0].Status);
    }

    [Fact(DisplayName = "does not overwrite an existing save when the new game is invalid")]
    public async Task Keeps_the_existing_save()
    {
        var (_, store) = StoreEnv.Create();
        await store.SaveAsync(1, MakeGame("Keep"), 1, Ct);
        var game = MakeGame();
        var broken = game with { Player = game.Player with { Personality = TestPersonality.Of(50, 10, 10, 10, 10, 10) } };
        Assert.False((await store.SaveAsync(1, broken, 2, Ct)).IsOk);
        Assert.Equal("Keep", (await store.LoadAsync(1, Ct)).Value.Game.Player.Name);
    }
}

public class StorageFailures
{
    [Fact(DisplayName = "a write that throws surfaces as STORAGE_ERROR")]
    public async Task Write_throws()
    {
        var backend = new FlakyBackend(new MemorySaveBackend()) { FailWrite = "Not enough storage space to save" };
        var result = await new SaveStore(backend, Content).SaveAsync(1, MakeGame(), 1, Ct);
        var error = Refused(result, ErrorCode.STORAGE_ERROR);
        Assert.Equal("Not enough storage space to save", error.Message);
    }

    [Fact(DisplayName = "a read that throws surfaces as STORAGE_ERROR on load and remove")]
    public async Task Read_throws()
    {
        var backend = new FlakyBackend(new MemorySaveBackend()) { FailRead = "Save storage is unavailable" };
        var store = new SaveStore(backend, Content);
        var load = Refused(await store.LoadAsync(1, Ct), ErrorCode.STORAGE_ERROR);
        Assert.Equal("Save storage is unavailable", load.Message);
        Refused(await store.DeleteAsync(1, Ct), ErrorCode.STORAGE_ERROR);
    }

    [Fact(DisplayName = "a remove that throws surfaces as STORAGE_ERROR")]
    public async Task Remove_throws()
    {
        var inner = new MemorySaveBackend();
        var good = new SaveStore(inner, Content);
        await good.SaveAsync(1, MakeGame(), 1, Ct);
        var broken = new SaveStore(new FlakyBackend(inner) { FailRemove = "Save storage is unavailable" }, Content);
        Refused(await broken.DeleteAsync(1, Ct), ErrorCode.STORAGE_ERROR);
        Assert.Equal("Rin", (await good.LoadAsync(1, Ct)).Value.Game.Player.Name);
    }
}

public class SaveStoreSmoke
{
    [Fact(DisplayName = "round-trips a game through a slot and lists it")]
    public async Task Round_trips_and_lists()
    {
        var (_, store) = StoreEnv.Create();
        var game = MakeGame();
        var meta = (await store.SaveAsync(3, game, TestContent.Now, Ct)).Value;
        StateAssert.Equivalent(game, (await store.LoadAsync(3, Ct)).Value.Game);
        var list = (await store.ListAsync(Ct)).Value;
        Assert.Equal(10, list.Length);
        Assert.Equal(meta.SavedAt, list[2].Metadata!.SavedAt);
    }

    [Fact(DisplayName = "reports corrupted JSON as VALIDATION_ERROR and lists it as corrupted")]
    public async Task Corrupted_json()
    {
        var (backend, store) = StoreEnv.Create();
        await StoreEnv.Put(backend, 2, "{not json");
        Assert.Equal(ErrorCode.VALIDATION_ERROR, (await StoreEnv.LoadFailure(store, 2)).Code);
        Assert.Equal(SlotStatus.Corrupted, (await store.ListAsync(Ct)).Value[1].Status);
    }

    [Fact(DisplayName = "lists a save that makes validation throw as corrupted without failing the other slots")]
    public async Task Validation_throw_lists_as_corrupted()
    {
        var (backend, store) = StoreEnv.Create();
        await store.SaveAsync(1, MakeGame(), 1, Ct);
        var crafted = StoreEnv.Doc(e => e["slot"] = 2, g => g["lineup"] = new JsonArray(JsonValue.Create<string?>(null)));
        await StoreEnv.Put(backend, 2, crafted);
        var list = (await store.ListAsync(Ct)).Value;
        Assert.Equal(10, list.Length);
        Assert.Equal(SlotStatus.Saved, list[0].Status);
        Assert.Equal(SlotStatus.Corrupted, list[1].Status);
        Assert.Equal(ErrorCode.VALIDATION_ERROR, (await StoreEnv.LoadFailure(store, 2)).Code);
    }

    [Fact(DisplayName = "reports an oversized save as corrupted")]
    public async Task Oversized_is_corrupted()
    {
        var (backend, store) = StoreEnv.Create();
        await StoreEnv.Put(backend, 4, new string('x', 2_000_001));
        Assert.Equal(SlotStatus.Corrupted, (await store.ListAsync(Ct)).Value[3].Status);
        var error = await StoreEnv.LoadFailure(store, 4);
        Assert.Equal(ErrorCode.VALIDATION_ERROR, error.Code);
        Assert.Contains("save too large", error.Message, StringComparison.Ordinal);
    }
}
