using System.Text.Json.Nodes;
using AncientOrder.Rules.Saves;
using AncientOrder.Rules.Saves.Format;
using AncientOrder.Rules.State;
using AncientOrder.Rules.Tests.Fixtures;
using static AncientOrder.Rules.Tests.Support.SaveFixtures;

namespace AncientOrder.Rules.Tests.Saves;

// Fixtures/v1-foundation.json is a real version 1 file, written by the godot-foundation build (commit 6a5f948) before the version moved to 2.
public class UpgradeTests
{
    private static byte[] V1Bytes()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "AncientOrder.sln")))
        {
            dir = dir.Parent;
        }

        Assert.NotNull(dir);
        return File.ReadAllBytes(Path.Combine(dir.FullName, "rules", "AncientOrder.Rules.Tests", "Saves", "Fixtures", "v1-foundation.json"));
    }

    [Fact(DisplayName = "the fixture really is a version 1 file with none of the version 2 fields")]
    public void Fixture_is_version_one()
    {
        var doc = (JsonObject)JsonNode.Parse(V1Bytes())!;
        Assert.Equal(1, (int)doc["version"]!);
        Assert.Null(doc["summary"]);
        var game = (JsonObject)doc["game"]!;
        foreach (var key in new[] { "party", "town", "conversation", "memory", "handOff" })
        {
            Assert.False(game.ContainsKey(key), key);
        }
    }

    [Fact(DisplayName = "a version 1 save loads: its party is its lineup, the new fields are empty, and its own data is kept")]
    public void Upgrades_on_load()
    {
        var loaded = ReadOk(V1Bytes());
        Assert.Equal(3, loaded.Slot);
        Assert.Equal("Rin", loaded.PlayerName);
        Assert.Equal([TestCast.ElenaId, TestCast.LarsId], loaded.Game.Lineup);
        Assert.Equal(loaded.Game.Lineup, loaded.Game.Party);
        Assert.Equal(new CharacterState(33.5, -12), loaded.Game.Characters[TestCast.ElenaId]);
        Assert.Null(loaded.Game.Town);
        Assert.Null(loaded.Game.Conversation);
        Assert.Null(loaded.Game.HandOff);
        Assert.Empty(loaded.Game.Memory.Flags);
        Assert.Empty(loaded.Game.Memory.Chosen);
        Assert.Empty(loaded.Game.Memory.Seen);
        Assert.Equal(SaveSituation.None, loaded.Summary.Situation);
        Assert.Null(loaded.Summary.TownName);
        Assert.True(loaded.Report.IsEmpty);
    }

    [Fact(DisplayName = "an upgraded save writes back as version 2 and then reads as the same game")]
    public void Upgraded_save_rewrites_as_version_two()
    {
        var loaded = ReadOk(V1Bytes());
        var rewritten = WriteOk(loaded.Game, slot: loaded.Slot);
        Assert.Equal(SaveConstants.CurrentVersion, (int)JsonNode.Parse(rewritten)!["version"]!);
        var again = ReadOk(rewritten);
        Assert.Equal(loaded.Game.Party, again.Game.Party);
        Assert.Equal(loaded.Game.Lineup, again.Game.Lineup);
        Assert.Equal(loaded.Game.Characters[TestCast.ElenaId], again.Game.Characters[TestCast.ElenaId]);
        Assert.Equal(rewritten, WriteOk(again.Game, slot: again.Slot));
    }

    [Fact(DisplayName = "a version 1 save in a slot lists as a saved slot and loads through the session")]
    public async Task Loads_from_a_slot()
    {
        var backend = new MemorySaveBackend();
        await backend.WriteAsync(KeyFor(3), V1Bytes(), Ct);

        var session = SessionOver(backend);
        var listed = Assert.Single((await session.ListSlots()).Value, s => s.Slot == 3);
        Assert.Equal(SlotStatus.Saved, listed.Status);
        Assert.Equal("Rin", listed.Metadata!.PlayerName);
        Assert.Equal(SaveSituation.None, listed.Metadata.Situation);

        var report = (await session.LoadFromSlot(3)).Value;
        Assert.True(report.IsEmpty);
        Assert.Equal([TestCast.ElenaId, TestCast.LarsId], session.Snapshot.Game!.Party);
    }

    [Fact(DisplayName = "an upgrade step for every version below the current one exists, and a version with no step is refused")]
    public void Every_old_version_has_a_step()
    {
        for (var version = 1; version < SaveConstants.CurrentVersion; version++)
        {
            Assert.Contains(SaveUpgrades.Steps, s => s.FromVersion == version);
        }

        var doc = (JsonObject)JsonNode.Parse(V1Bytes())!;
        doc["version"] = 0;
        Assert.Contains("unsupported version", Refused(Read(doc)).Message, StringComparison.Ordinal);
    }
}
