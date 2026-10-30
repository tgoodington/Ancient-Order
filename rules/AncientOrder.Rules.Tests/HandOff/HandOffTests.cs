using AncientOrder.Rules.Common;
using AncientOrder.Rules.Conditions;
using AncientOrder.Rules.Content.Packs;
using AncientOrder.Rules.Session;
using AncientOrder.Rules.Tests.Support;
using AncientOrder.Rules.Town;

namespace AncientOrder.Rules.Tests.HandOff;

public class HandOffTests
{
    private const string Pack = """{ "format": "ancient-order-content", "version": 1, "id": "t", "name": "Test", "standIn": false, "files": ["base.json"] }""";

    private const string Rat = """{ "id": "rat", "name": "Rat", "archetype": "x", "rank": 1, "stamina": 10, "power": 5, "speed": 5, "elementalPath": "fire", "reactionSkills": { "block": { "sr": 1, "smr": 1, "fmr": 1 }, "dodge": { "sr": 1, "smr": 1, "fmr": 1 }, "parry": { "sr": 1, "smr": 1, "fmr": 1 } } }""";

    private const string Combat = """{ "rank": 1, "stamina": 50, "power": 10, "speed": 10, "elementalPath": "fire" }""";

    private const string Person = "\"personality\": { \"patience\": 20, \"empathy\": 20, \"cunning\": 10, \"logic\": 15, \"kindness\": 20, \"charisma\": 15 }, \"startingAffection\": 0, \"startingTrust\": 0";

    private const string Template = """
        {
          "settings": { "playerName": "Kael", "lineup": { "min": 1, "max": 2 }, "startingTown": "square", "startingParty": ["ann", "bo"] },
          "archetypes": [{ "id": "x" }],
          "characters": [
            { "id": "ann", "name": "Ann", "archetypeId": "x", PERSON, "combat": COMBAT },
            { "id": "bo", "name": "Bo", "archetypeId": "x", PERSON, "combat": COMBAT }
          ],
          "conversations": [
            { "id": "talk", "participants": ["ann"], "start": [{ "step": "hello" }],
              "steps": [ { "id": "hello", "lines": [], "choices": [
                { "id": "go", "text": "Go", "effects": [ { "type": "setFlag", "flag": "left" }, { "type": "handOff", "fight": "brawl" } ] },
                { "id": "dropgo", "text": "Drop and go", "effects": [ { "type": "leave", "character": "bo" }, { "type": "handOff", "fight": "brawl" } ] },
                { "id": "gobig", "text": "Go big", "effects": [ { "type": "handOff", "fight": "melee" } ] }
              ] } ] }
          ],
          "fights": [
            { "id": "brawl", "name": "The Brawl", "enemies": [ RAT ] },
            { "id": "melee", "name": "The Melee", "lineup": { "min": 2, "max": 2 }, "enemies": [ RAT ] }
          ],
          "towns": [ {
            "id": "square", "name": "Square",
            "ground": [ [[0, 0], [40, 0], [40, 40], [0, 40]] ],
            "start": [1, 1],
            "people": [ { "character": "ann", "placements": [ { "at": [5, 5] } ], "opens": [ { "conversation": "talk" } ] } ],
            "spots": [
              { "id": "post", "area": { "circle": { "center": [5, 8], "radius": 1 } }, "trigger": "interact", "opens": [ { "handOff": "brawl" } ] },
              { "id": "arch", "area": { "circle": { "center": [8, 5], "radius": 1 } }, "trigger": "interact", "opens": [ { "handOff": "melee" } ] }
            ]
          } ]
        }
        """;

    private static string Build(string? replaceFrom = null, string? replaceTo = null)
    {
        var text = Template
            .Replace("PERSON", Person, StringComparison.Ordinal)
            .Replace("COMBAT", Combat, StringComparison.Ordinal)
            .Replace("RAT", Rat, StringComparison.Ordinal);
        return replaceFrom is null ? text : text.Replace(replaceFrom, replaceTo, StringComparison.Ordinal);
    }

    private static MemoryContentSource Memory(string text) =>
        new([KeyValuePair.Create("pack.json", Pack), KeyValuePair.Create("base.json", text)]);

    private static GameSession Session()
    {
        var loaded = ContentPackLoader.Load(Memory(Build()));
        Assert.True(loaded.IsOk, loaded.IsOk ? string.Empty : string.Join("\n", loaded.Error.Details));
        var session = TestSessions.Create(loaded.Value);
        Assert.True(session.NewGame().IsOk);
        return session;
    }

    private static string[] Problems(string text) =>
        ContentPackLoader.Load(Memory(text)) is { IsOk: false } loaded ? [.. loaded.Error.Details.Select(d => d.Message)] : throw new InvalidOperationException("expected a refusal");

    [Fact]
    public void A_spot_that_hands_off_is_refused_without_a_ready_lineup_and_the_player_stays()
    {
        var session = Session();
        session.MovePlayer(5, 7);
        var before = session.Snapshot;

        var refused = session.TriggerSpot("post");

        Assert.Equal(ErrorCode.LINEUP_NOT_READY, refused.Error.Code);
        Assert.Equal("Pick at least 1 fighter", refused.Error.Message);
        Assert.Same(before, session.Snapshot);
        Assert.Null(session.Snapshot.HandOff);
    }

    [Fact]
    public void A_ready_lineup_records_the_hand_off_and_every_snapshot_after_it_carries_the_names()
    {
        var session = Session();
        session.SetLineup(["bo"]);
        session.MovePlayer(5, 7);

        var handed = Assert.IsType<TownEvent.HandedOff>(session.TriggerSpot("post").Value);

        Assert.Equal("brawl", handed.FightId);
        var recorded = session.Snapshot.Game!.HandOff!;
        Assert.Equal("brawl", recorded.FightId);
        Assert.Equal(["bo"], recorded.Lineup);
        Assert.Equal("square", recorded.TownId);
        var view = session.Snapshot.HandOff!;
        Assert.Equal("The Brawl", view.FightName);
        Assert.Equal([("bo", "Bo")], view.Lineup.Select(m => (m.Id, m.Name)));

        session.GetState();
        Assert.Equal("brawl", session.Snapshot.HandOff!.FightId);
    }

    [Fact]
    public void After_the_hand_off_moving_triggering_conversations_and_the_lineup_are_refused()
    {
        var session = Session();
        session.SetLineup(["bo"]);
        session.MovePlayer(5, 7);
        session.TriggerSpot("post");
        var after = session.Snapshot;

        Assert.Equal(ErrorCode.TOWN_NOT_ACTIVE, session.MovePlayer(5, 6).Error.Code);
        Assert.Equal(ErrorCode.TOWN_NOT_ACTIVE, session.TriggerPerson("ann").Error.Code);
        Assert.Equal(ErrorCode.TOWN_NOT_ACTIVE, session.TriggerSpot("post").Error.Code);
        Assert.Equal(ErrorCode.TOWN_NOT_ACTIVE, session.StartConversation("talk").Error.Code);
        Assert.Equal(ErrorCode.TOWN_NOT_ACTIVE, session.SetLineup(["ann"]).Error.Code);
        Assert.Same(after, session.Snapshot);
    }

    [Fact]
    public void A_fight_can_ask_for_its_own_lineup_size()
    {
        var session = Session();
        session.SetLineup(["bo"]);
        session.MovePlayer(8, 4);

        var refused = session.TriggerSpot("arch");

        Assert.Equal(ErrorCode.LINEUP_NOT_READY, refused.Error.Code);
        Assert.Equal("Pick at least 2 fighters", refused.Error.Message);
    }

    [Fact]
    public void A_choice_with_a_hand_off_is_locked_with_the_lineup_reason_until_ready_then_ends_the_conversation()
    {
        var session = Session();
        session.MovePlayer(5, 6);
        session.TriggerPerson("ann");

        var go = session.Snapshot.Conversation!.Choices.Single(c => c.Id == "go");
        Assert.False(go.Available);
        var all = Assert.IsType<RequirementView.All>(go.Requirement);
        var lineup = Assert.IsType<RequirementView.LineupReady>(Assert.Single(all.Children));
        Assert.Equal("brawl", lineup.FightId);
        Assert.Equal("Pick at least 1 fighter", lineup.Reason);
        Assert.Equal(ErrorCode.CHOICE_UNAVAILABLE, session.Choose("go").Error.Code);
        Assert.False(session.Snapshot.Game!.Memory.HasFlag("left"));

    }

    [Fact]
    public void A_choice_unlocks_against_the_limits_of_its_own_fight()
    {
        var session = Session();
        session.SetLineup(["bo"]);
        session.MovePlayer(5, 6);
        session.TriggerPerson("ann");

        Assert.True(session.Snapshot.Conversation!.Choices.Single(c => c.Id == "go").Available);
        Assert.False(session.Snapshot.Conversation.Choices.Single(c => c.Id == "gobig").Available);
    }

    [Fact]
    public void Choosing_a_hand_off_records_it_in_the_report_and_keeps_the_effects_before_it()
    {
        var session = Session();
        session.SetLineup(["bo"]);
        session.MovePlayer(5, 6);
        session.TriggerPerson("ann");

        var report = session.Choose("go").Value;

        Assert.Equal("brawl", report.HandOff);
        Assert.True(report.Ended);
        Assert.Null(report.NextConversationId);
        var game = session.Snapshot.Game!;
        Assert.Null(game.Conversation);
        Assert.Null(session.Snapshot.Conversation);
        Assert.True(game.Memory.HasFlag("left"));
        Assert.Equal("brawl", game.HandOff!.FightId);
        Assert.Equal("The Brawl", session.Snapshot.HandOff!.FightName);
    }

    [Fact]
    public void Content_that_hands_off_to_an_unknown_fight_or_leads_on_after_a_hand_off_is_refused()
    {
        Assert.Contains(Problems(Build("\"handOff\": \"brawl\"", "\"handOff\": \"nope\"")), p => p.Contains("unknown fight: nope", StringComparison.Ordinal));
        Assert.Contains(Problems(Build("\"fight\": \"brawl\"", "\"fight\": \"nope\"")), p => p.Contains("unknown fight: nope", StringComparison.Ordinal));
        Assert.Contains(Problems(Build("\"effects\": [ { \"type\": \"handOff\", \"fight\": \"melee\" } ]", "\"effects\": [ { \"type\": \"handOff\", \"fight\": \"melee\" } ], \"next\": [ { \"step\": \"hello\" } ]")), p => p.Contains("cannot also lead on", StringComparison.Ordinal));
        Assert.Contains(Problems(Build("{ \"type\": \"setFlag\", \"flag\": \"left\" }, { \"type\": \"handOff\", \"fight\": \"brawl\" }", "{ \"type\": \"handOff\", \"fight\": \"brawl\" }, { \"type\": \"setFlag\", \"flag\": \"left\" }")), p => p.Contains("last effect", StringComparison.Ordinal));
    }

    [Fact]
    public void A_fight_lineup_must_be_a_valid_range()
    {
        var paths = ContentPackLoader.Load(Memory(Build("\"min\": 2, \"max\": 2", "\"min\": 2, \"max\": 1"))) is { IsOk: false } loaded
            ? loaded.Error.Details
            : throw new InvalidOperationException("expected a refusal");

        Assert.Contains(paths, d => d.Message.Contains("0 <= min <= max", StringComparison.Ordinal) && d.Path.EndsWith("/fights/1/lineup/min", StringComparison.Ordinal));
    }

    [Fact]
    public void A_fight_lineup_min_above_the_settings_max_is_refused_as_unsatisfiable()
    {
        var problems = Problems(Build("\"min\": 2, \"max\": 2", "\"min\": 3, \"max\": 3"));

        Assert.Contains(problems, p => p.Contains("could never be ready", StringComparison.Ordinal));
    }

    [Fact]
    public void A_choice_is_judged_on_the_lineup_left_by_the_effects_before_its_hand_off()
    {
        var session = Session();
        session.SetLineup(["bo"]);
        session.MovePlayer(5, 6);
        session.TriggerPerson("ann");
        var before = session.Snapshot;

        var choices = session.Snapshot.Conversation!.Choices;
        Assert.True(choices.Single(c => c.Id == "go").Available);
        var dropgo = choices.Single(c => c.Id == "dropgo");
        Assert.False(dropgo.Available);
        var lineup = Assert.IsType<RequirementView.LineupReady>(Assert.Single(Assert.IsType<RequirementView.All>(dropgo.Requirement).Children));
        Assert.Equal("Pick at least 1 fighter", lineup.Reason);
        Assert.Equal(ErrorCode.CHOICE_UNAVAILABLE, session.Choose("dropgo").Error.Code);
        Assert.Same(before, session.Snapshot);
    }

    private const string PersonWithHandOff = "\"people\": [ { \"character\": \"bo\", \"placements\": [ { \"at\": [8, 8] } ], \"opens\": [ { \"handOff\": \"brawl\" } ] }, { \"character\": \"ann\"";

    private static GameSession SessionWithHandOffPerson()
    {
        var loaded = ContentPackLoader.Load(Memory(Build("\"people\": [ { \"character\": \"ann\"", PersonWithHandOff)));
        Assert.True(loaded.IsOk, loaded.IsOk ? string.Empty : string.Join(Environment.NewLine, loaded.Error.Details));
        var session = TestSessions.Create(loaded.Value);
        Assert.True(session.NewGame().IsOk);
        return session;
    }

    [Fact]
    public void A_person_that_hands_off_is_refused_without_a_ready_lineup_and_the_player_stays()
    {
        var session = SessionWithHandOffPerson();
        session.MovePlayer(8, 7);
        var before = session.Snapshot;

        var refused = session.TriggerPerson("bo");

        Assert.Equal(ErrorCode.LINEUP_NOT_READY, refused.Error.Code);
        Assert.Equal("Pick at least 1 fighter", refused.Error.Message);
        Assert.Same(before, session.Snapshot);
        Assert.Null(session.Snapshot.HandOff);
        Assert.NotNull(session.Snapshot.Town);
    }

    [Fact]
    public void A_person_hands_off_once_the_lineup_is_ready_and_the_fight_and_lineup_are_recorded()
    {
        var session = SessionWithHandOffPerson();
        session.MovePlayer(8, 7);
        Assert.Equal(ErrorCode.LINEUP_NOT_READY, session.TriggerPerson("bo").Error.Code);
        session.SetLineup(["bo"]);

        var handed = Assert.IsType<TownEvent.HandedOff>(session.TriggerPerson("bo").Value);

        Assert.Equal("brawl", handed.FightId);
        Assert.Equal("brawl", session.Snapshot.Game!.HandOff!.FightId);
        Assert.Equal(["bo"], session.Snapshot.Game.HandOff.Lineup);
        Assert.Equal("The Brawl", session.Snapshot.HandOff!.FightName);
    }

    [Fact]
    public void A_refused_spot_leaves_the_player_free_to_fix_the_lineup_and_try_again()
    {
        var session = Session();
        session.MovePlayer(5, 7);
        Assert.Equal(ErrorCode.LINEUP_NOT_READY, session.TriggerSpot("post").Error.Code);

        Assert.True(session.MovePlayer(6, 7).IsOk);
        Assert.True(session.SetLineup(["ann"]).IsOk);
        Assert.True(session.MovePlayer(5, 7).IsOk);

        Assert.True(session.TriggerSpot("post").IsOk);
        Assert.Equal(["ann"], session.Snapshot.Game!.HandOff!.Lineup);
    }

    [Fact]
    public void The_recorded_and_viewed_lineup_keeps_the_order_it_was_set_in()
    {
        var session = Session();
        session.SetLineup(["bo", "ann"]);
        session.MovePlayer(8, 4);

        Assert.True(session.TriggerSpot("arch").IsOk);

        Assert.Equal(["bo", "ann"], session.Snapshot.Game!.HandOff!.Lineup);
        Assert.Equal(["Bo", "Ann"], session.Snapshot.HandOff!.Lineup.Select(m => m.Name));
        Assert.Equal("The Melee", session.Snapshot.HandOff.FightName);
    }
}
