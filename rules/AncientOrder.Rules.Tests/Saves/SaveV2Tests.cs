using AncientOrder.Rules.Saves;
using AncientOrder.Rules.Saves.Format;
using AncientOrder.Rules.Town;
using static AncientOrder.Rules.Tests.Support.SaveFixtures;

namespace AncientOrder.Rules.Tests.Saves;

// Version 2 saves end to end. The field-by-field format checks live in SaveWorldTests; this adds save, load, save again and the session loop.
public class SaveV2Tests
{
    [Fact(DisplayName = "saving, loading, and saving again gives identical bytes for a game in town, in a conversation, and handed off")]
    public void Save_load_save_is_byte_identical()
    {
        foreach (var game in new[] { SaveWorlds.Played(SaveWorlds.Full), SaveWorlds.Talking(SaveWorlds.Full, "hello"), SaveWorlds.HandedOff(SaveWorlds.Full) })
        {
            var first = WriteOk(game, SaveWorlds.Full, slot: 4);
            var loaded = ReadOk(first, SaveWorlds.Full);
            Assert.True(loaded.Report.IsEmpty);
            Assert.Equal(first, WriteOk(loaded.Game, SaveWorlds.Full, slot: 4));
        }
    }

    [Fact(DisplayName = "a game in the middle of a fight round-trips to identical bytes")]
    public void Fight_round_trip_is_byte_identical()
    {
        var game = FightGame(rounds: 2);
        var first = WriteOk(game, LongFightContent);
        var loaded = ReadOk(first, LongFightContent);
        Assert.NotNull(loaded.Game.Combat);
        Assert.Equal(first, WriteOk(loaded.Game, LongFightContent));
    }

    [Fact(DisplayName = "a conversation saved at its opening step resumes there and can be played on")]
    public async Task Mid_conversation_save_resumes_and_continues()
    {
        var backend = new MemorySaveBackend();
        var first = SessionOver(backend, SaveWorlds.Full);
        Assert.True(first.NewGame().IsOk);
        Assert.True(first.StartConversation("talk").IsOk);
        Assert.Equal("hello", first.Snapshot.Conversation!.StepId);
        Assert.True((await first.SaveToSlot(2)).IsOk);

        var second = SessionOver(backend, SaveWorlds.Full);
        Assert.True((await second.LoadFromSlot(2)).Value.IsEmpty);
        Assert.Equal("hello", second.Snapshot.Conversation!.StepId);
        Assert.False(second.Snapshot.Game!.Memory.HasFlag("met"));

        Assert.True(second.Choose("go").IsOk);
        Assert.Equal("bye", second.Snapshot.Conversation!.StepId);
        Assert.True(second.Snapshot.Game!.Memory.HasFlag("met"));
    }

    [Fact(DisplayName = "a session save and load keeps the saved bytes identical and the slot list reports the town name and situation")]
    public async Task Session_save_load_save_and_summary()
    {
        var backend = new MemorySaveBackend();
        var session = SessionOver(backend, SaveWorlds.Full);
        Assert.True(session.NewGame().IsOk);
        Assert.True(session.StartConversation("talk").IsOk);
        Assert.True((await session.SaveToSlot(1)).IsOk);
        var saved = (await backend.ReadAsync(KeyFor(1), Ct))!;

        var loadedSession = SessionOver(backend, SaveWorlds.Full);
        Assert.True((await loadedSession.LoadFromSlot(1)).IsOk);
        Assert.True((await loadedSession.SaveToSlot(1)).IsOk);
        Assert.Equal(saved, (await backend.ReadAsync(KeyFor(1), Ct))!);

        var slot = Assert.Single((await loadedSession.ListSlots()).Value, s => s.Slot == 1);
        Assert.Equal(SaveSituation.Conversation, slot.Metadata!.Situation);
        Assert.Equal("Square", slot.Metadata.TownName);
    }

    [Fact(DisplayName = "the player's town position survives a session save and load")]
    public async Task Position_survives_the_session()
    {
        var backend = new MemorySaveBackend();
        await new SaveStore(backend, SaveWorlds.Full).SaveAsync(5, SaveWorlds.Played(SaveWorlds.Full), 1, Ct);
        var session = SessionOver(backend, SaveWorlds.Full);
        Assert.True((await session.LoadFromSlot(5)).IsOk);
        Assert.Equal(new TownPresence("dock", new Point(110.5, 120.25)), session.Snapshot.Game!.Town);
        Assert.Equal("dock", session.Snapshot.Town!.Id);
    }
}
