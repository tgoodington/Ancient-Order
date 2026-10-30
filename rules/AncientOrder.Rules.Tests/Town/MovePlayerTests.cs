using AncientOrder.Rules.Common;
using AncientOrder.Rules.Content.Packs;
using AncientOrder.Rules.Session;
using AncientOrder.Rules.Tests.Fixtures;
using AncientOrder.Rules.Tests.Support;
using AncientOrder.Rules.Town;

namespace AncientOrder.Rules.Tests.TownWalking;

public class MovePlayerTests
{
    private const string Pack = """{ "format": "ancient-order-content", "version": 1, "id": "t", "name": "Test", "standIn": false, "files": ["all.json"] }""";

    private const string All = """
        {
          "settings": { "playerName": "Kael", "lineup": { "min": 1, "max": 1 }, "startingTown": "yard" },
          "archetypes": [{ "id": "x" }],
          "characters": [],
          "towns": [
            {
              "id": "yard", "name": "Yard",
              "ground": [ [[0, 0], [20, 0], [20, 20], [0, 20]] ],
              "obstacles": [ { "id": "wall", "footprint": [[8, 0], [10, 0], [10, 15], [8, 15]], "height": 3 } ],
              "start": [2, 2]
            }
          ]
        }
        """;

    private static GameSession NewTownGame()
    {
        var loaded = ContentPackLoader.Load(new MemoryContentSource([KeyValuePair.Create("pack.json", Pack), KeyValuePair.Create("all.json", All)]));
        Assert.True(loaded.IsOk, loaded.IsOk ? string.Empty : string.Join("\n", loaded.Error.Details));
        var session = TestSessions.Create(loaded.Value);
        Assert.True(session.NewGame().IsOk);
        return session;
    }

    [Fact]
    public void A_walkable_report_is_accepted_and_recorded_in_the_game_and_the_view_with_the_version_up_by_one()
    {
        var session = NewTownGame();
        var versionBefore = session.Snapshot.Version;

        var moved = session.MovePlayer(15, 12);

        Assert.True(moved.IsOk);
        Assert.IsType<TownEvent.Nothing>(moved.Value);
        Assert.Equal(new Point(15, 12), session.Snapshot.Town!.Player);
        Assert.Equal(new Point(15, 12), session.Snapshot.Game!.Town!.Position);
        Assert.Equal(versionBefore + 1, session.Snapshot.Version);
    }

    [Fact]
    public void Each_accepted_report_replaces_the_last_one_and_leaves_the_game_it_came_from_unchanged()
    {
        var session = NewTownGame();
        var original = session.Snapshot.Game!;

        session.MovePlayer(5, 5);
        session.MovePlayer(15, 18);

        Assert.Equal(new Point(15, 18), session.Snapshot.Game!.Town!.Position);
        Assert.Equal(new Point(2, 2), original.Town!.Position);
    }

    [Fact]
    public void An_unwalkable_report_is_refused_with_not_walkable_and_leaves_the_game_and_version_unchanged()
    {
        var session = NewTownGame();
        var before = session.Snapshot;

        foreach (var (x, z) in new[] { (9.0, 5.0), (8.0, 5.0), (10.0, 15.0), (-1.0, 5.0), (25.0, 5.0), (5.0, 21.0) })
        {
            var refused = session.MovePlayer(x, z);

            Assert.False(refused.IsOk);
            Assert.Equal(ErrorCode.NOT_WALKABLE, refused.Error.Code);
            Assert.Same(before, session.Snapshot);
            Assert.Equal(before.Version, session.Snapshot.Version);
            Assert.Equal(new Point(2, 2), session.Snapshot.Game!.Town!.Position);
        }
    }

    [Fact]
    public void A_refused_report_does_not_disturb_a_position_already_reported()
    {
        var session = NewTownGame();
        session.MovePlayer(14, 14);
        var before = session.Snapshot;

        Assert.Equal(ErrorCode.NOT_WALKABLE, session.MovePlayer(9, 9).Error.Code);

        Assert.Same(before, session.Snapshot);
        Assert.Equal(new Point(14, 14), session.Snapshot.Town!.Player);
    }

    [Fact]
    public void A_report_with_no_town_is_refused_with_town_not_active_and_changes_nothing()
    {
        var session = TestSessions.Create(TestContent.Create());
        Assert.True(session.NewGame().IsOk);
        var before = session.Snapshot;

        var refused = session.MovePlayer(1, 1);

        Assert.Equal(ErrorCode.TOWN_NOT_ACTIVE, refused.Error.Code);
        Assert.Same(before, session.Snapshot);
        Assert.Equal(before.Version, session.Snapshot.Version);
    }
}
