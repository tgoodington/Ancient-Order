using AncientOrder.Rules.Common;
using AncientOrder.Rules.Content.Packs;
using AncientOrder.Rules.Conversations;
using AncientOrder.Rules.Reactions;
using AncientOrder.Rules.Session;
using AncientOrder.Rules.State;
using AncientOrder.Rules.Tests.Support;

namespace AncientOrder.Rules.Tests.ContentPacks;

public class ConversationTests
{
    private const string Pack = """{ "format": "ancient-order-content", "version": 1, "id": "t", "name": "Test", "standIn": false, "files": ["base.json", "talk.json"] }""";

    private const string Base = """
        {
          "settings": { "playerName": "Kael", "lineup": { "min": 1, "max": 1 } },
          "archetypes": [{ "id": "x" }],
          "characters": [
            { "id": "ann", "name": "Ann", "archetypeId": "x", "personality": { "patience": 20, "empathy": 20, "cunning": 10, "logic": 15, "kindness": 20, "charisma": 15 }, "startingAffection": 0, "startingTrust": 0 },
            { "id": "bo", "name": "Bo", "archetypeId": "x", "personality": { "patience": 20, "empathy": 20, "cunning": 10, "logic": 15, "kindness": 20, "charisma": 15 }, "startingAffection": 0, "startingTrust": 0 }
          ]
        }
        """;

    private const string Talk = """
        {
          "conversations": [
            {
              "id": "chat",
              "title": "A chat",
              "participants": ["ann"],
              "start": [{ "step": "hello" }],
              "steps": [
                { "id": "hello",
                  "lines": [ { "speaker": "ann", "text": "Hi." }, { "speaker": "player", "text": "Hello." }, { "speaker": "narrator", "text": "Wind." } ],
                  "choices": [
                    { "id": "kind", "text": "Be kind", "effects": [ { "type": "relationship", "character": "ann", "affection": 10, "trust": 5 } ], "next": [ { "step": "bye" } ] },
                    { "id": "scene", "text": "Go on", "next": [ { "conversation": "scene" } ] },
                    { "id": "leave", "text": "Leave" }
                  ] },
                { "id": "bye", "lines": [ { "speaker": "ann", "text": "Bye." } ] }
              ]
            },
            { "id": "scene", "participants": [], "steps": [ { "id": "s1", "lines": [ { "speaker": "narrator", "text": "Night." } ], "next": [ { "step": "s2" } ] }, { "id": "s2", "lines": [] } ] }
          ]
        }
        """;

    private static MemoryContentSource Memory(string talk) =>
        new([KeyValuePair.Create("pack.json", Pack), KeyValuePair.Create("base.json", Base), KeyValuePair.Create("talk.json", talk)]);

    private static GameContent Content(string talk = Talk)
    {
        var loaded = ContentPackLoader.Load(Memory(talk));
        Assert.True(loaded.IsOk, loaded.IsOk ? string.Empty : string.Join("\n", loaded.Error.Details));
        return loaded.Value;
    }

    private static GameSession Session(GameContent? content = null)
    {
        var session = TestSessions.Create(content ?? Content());
        Assert.True(session.NewGame().IsOk);
        return session;
    }

    private static string[] Problems(string talk)
    {
        var loaded = ContentPackLoader.Load(Memory(talk));
        Assert.False(loaded.IsOk);
        return [.. loaded.Error.Details.Select(d => d.Path)];
    }

    [Fact]
    public void A_conversation_plays_from_start_to_finish_and_reports_each_step()
    {
        var session = Session();
        var view = session.StartConversation("chat").Value;

        Assert.Equal("hello", view.StepId);
        Assert.Equal("A chat", view.Title);
        Assert.Equal([new ParticipantView("ann", "Ann", Demeanor.Neutral)], view.Participants);
        Assert.Equal(
            [SpeakerKind.Character, SpeakerKind.Player, SpeakerKind.Narrator],
            view.Lines.Select(l => l.SpeakerKind));
        Assert.Equal("Ann", view.Lines[0].SpeakerName);
        Assert.Equal("Kael", view.Lines[1].SpeakerName);
        Assert.Null(view.Lines[2].SpeakerName);
        Assert.Equal(["kind", "scene", "leave"], view.Choices.Select(c => c.Id));
        Assert.False(view.IsContinue);
        Assert.Equal(new ActiveConversation("chat", "hello"), session.Snapshot.Game!.Conversation);
        Assert.Equal(view.StepId, session.Snapshot.Conversation!.StepId);
        Assert.Equal(view.Lines.Length, session.Snapshot.Conversation.Lines.Length);
        Assert.Null(session.Snapshot.LastChoice);

        var report = session.Choose("kind").Value;
        Assert.False(report.Ended);
        Assert.Equal("kind", report.ChoiceId);
        Assert.Equal(new RelationshipChange("ann", 10, 5), Assert.Single(report.RelationshipChanges));
        Assert.Same(report, session.Snapshot.LastChoice);
        Assert.Equal("bye", session.Snapshot.Conversation!.StepId);
        Assert.True(session.Snapshot.Conversation.IsContinue);
        Assert.Empty(session.Snapshot.Conversation.Choices);
        Assert.Equal(10, session.Snapshot.Game.Characters["ann"].Affection);

        var done = session.Continue().Value;
        Assert.True(done.Ended);
        Assert.Null(done.ChoiceId);
        Assert.Null(session.Snapshot.Game.Conversation);
        Assert.Null(session.Snapshot.Conversation);
    }

    [Fact]
    public void A_conversation_destination_ends_this_one_and_opens_the_other_and_a_later_change_clears_LastChoice()
    {
        var session = Session();
        session.StartConversation("chat");
        var report = session.Choose("scene").Value;

        Assert.True(report.Ended);
        Assert.Equal("scene", report.NextConversationId);
        Assert.Equal("scene", session.Snapshot.Conversation!.Id);
        Assert.Equal("s1", session.Snapshot.Conversation.StepId);

        Assert.True(session.Continue().IsOk);
        Assert.Equal("s2", session.Snapshot.Conversation!.StepId);
        Assert.NotNull(session.Snapshot.LastChoice);
        Assert.True(session.AdjustPersonality(new AncientOrder.Rules.Personality.PersonalityAdjustment(System.Collections.Immutable.ImmutableDictionary<AncientOrder.Rules.Personality.Trait, double>.Empty.Add(AncientOrder.Rules.Personality.Trait.Logic, 1))).IsOk);
        Assert.Null(session.Snapshot.LastChoice);

        Assert.True(session.Continue().Value.Ended);
    }

    [Fact]
    public void A_choice_with_no_destination_ends_the_conversation()
    {
        var session = Session();
        session.StartConversation("chat");
        Assert.True(session.Choose("leave").Value.Ended);
        Assert.Null(session.Snapshot.Game!.Conversation);
    }

    [Fact]
    public void Refusals_leave_the_game_unchanged_and_notify_no_one()
    {
        var session = Session();
        var notified = 0;
        using var _ = session.Subscribe(_ => notified++);

        Assert.Equal(ErrorCode.NO_ACTIVE_CONVERSATION, session.Choose("kind").Error.Code);
        Assert.Equal(ErrorCode.NO_ACTIVE_CONVERSATION, session.Continue().Error.Code);
        Assert.Equal(ErrorCode.CONVERSATION_NOT_FOUND, session.StartConversation("nope").Error.Code);

        session.StartConversation("chat");
        var before = session.Snapshot;
        notified = 0;
        Assert.Equal(ErrorCode.CONVERSATION_ALREADY_ACTIVE, session.StartConversation("scene").Error.Code);
        Assert.Equal(ErrorCode.CHOICE_UNAVAILABLE, session.Choose("bogus").Error.Code);
        Assert.Equal(ErrorCode.CHOICE_UNAVAILABLE, session.Continue().Error.Code);

        Assert.Same(before, session.Snapshot);
        Assert.Equal(0, notified);
    }

    [Fact]
    public void Every_change_makes_a_new_game_and_leaves_earlier_ones_alone()
    {
        var session = Session();
        var first = session.Snapshot.Game!;
        session.StartConversation("chat");
        var started = session.Snapshot.Game!;
        session.Choose("kind");

        Assert.Null(first.Conversation);
        Assert.Equal(new ActiveConversation("chat", "hello"), started.Conversation);
        Assert.Equal(0, started.Characters["ann"].Affection);
        Assert.NotSame(started, session.Snapshot.Game);
    }

    [Fact]
    public void Starting_during_combat_is_refused()
    {
        var session = Session();
        var game = session.Snapshot.Game! with { Combat = TestCombat() };
        var refused = ConversationRules.Start(Content(), game, "chat", 1);
        Assert.Equal(ErrorCode.COMBAT_ALREADY_ACTIVE, refused.Error.Code);
    }

    [Fact]
    public void A_start_list_picks_the_opening_step_else_the_first_step()
    {
        var content = Content(Talk.Replace("\"start\": [{ \"step\": \"hello\" }],", "\"start\": [{ \"step\": \"bye\" }],", StringComparison.Ordinal));
        var session = Session(content);
        Assert.Equal("bye", session.StartConversation("chat").Value.StepId);

        var noStart = Content(Talk.Replace("\"start\": [{ \"step\": \"hello\" }],", string.Empty, StringComparison.Ordinal));
        Assert.Equal("hello", Session(noStart).StartConversation("chat").Value.StepId);
    }

    [Fact]
    public void Content_problems_are_told_with_their_file_places()
    {
        const string bad = """
            { "conversations": [
              { "id": "c", "participants": ["ghost"], "steps": [
                { "id": "a", "lines": [ { "speaker": "ghost", "text": "x" } ],
                  "choices": [ { "id": "k", "text": "t", "next": [ { "step": "zzz" } ] }, { "id": "k", "text": "t", "effects": [ { "type": "relationship", "character": "ghost" } ] } ] },
                { "id": "a", "lines": [], "next": [ { "conversation": "nowhere" } ] } ] },
              { "id": "c", "participants": [], "steps": [ { "id": "q", "lines": [] } ] }
            ] }
            """;

        var paths = Problems(bad);
        Assert.Contains("talk.json:/conversations/0/participants/0", paths);
        Assert.Contains("talk.json:/conversations/0/steps/0/lines/0/speaker", paths);
        Assert.Contains("talk.json:/conversations/0/steps/0/choices/0/next/0/step", paths);
        Assert.Contains("talk.json:/conversations/0/steps/0/choices/1/id", paths);
        Assert.Contains("talk.json:/conversations/0/steps/0/choices/1/effects/0/character", paths);
        Assert.Contains("talk.json:/conversations/0/steps/1/id", paths);
        Assert.Contains("talk.json:/conversations/0/steps/1/next/0/conversation", paths);
        Assert.Contains("talk.json:/conversations/1/id", paths);
    }

    [Fact]
    public void A_start_entry_naming_a_missing_step_and_a_malformed_destination_are_refused()
    {
        var start = Problems("""{ "conversations": [ { "id": "c", "participants": [], "start": [ { "step": "no" } ], "steps": [ { "id": "a", "lines": [] } ] } ] }""");
        Assert.Equal(["talk.json:/conversations/0/start/0/step"], start);

        var both = Problems("""{ "conversations": [ { "id": "c", "participants": [], "steps": [ { "id": "a", "lines": [], "next": [ { "step": "a", "conversation": "c" }, {} ] } ] } ] }""");
        Assert.Equal(["talk.json:/conversations/0/steps/0/next/0", "talk.json:/conversations/0/steps/0/next/1"], both);
    }

    [Fact]
    public void Narrator_and_player_cannot_be_character_ids_and_bad_effects_are_refused_not_thrown()
    {
        var withReserved = new MemoryContentSource([
            KeyValuePair.Create("pack.json", Pack),
            KeyValuePair.Create("base.json", Base.Replace("\"id\": \"bo\"", "\"id\": \"narrator\"", StringComparison.Ordinal)),
            KeyValuePair.Create("talk.json", "{}"),
        ]);
        var refused = ContentPackLoader.Load(withReserved);
        Assert.Equal(["base.json:/characters/1/id"], refused.Error.Details.Select(d => d.Path));

        Assert.NotEmpty(Problems("""{ "conversations": [ { "id": "c", "participants": [], "steps": [ { "id": "a", "lines": [], "choices": [ { "id": "k", "text": "t", "effects": [ { "type": "mystery" } ] } ] } ] } ] }"""));
        Assert.NotEmpty(Problems("""{ "conversations": [ { "id": "c", "participants": [], "steps": [ { "id": "a", "lines": [], "choices": [ { "id": "k", "text": "t", "effects": [ { "character": "ann" } ] } ] } ] } ] }"""));
    }

    [Fact]
    public void A_null_item_in_any_array_is_refused_with_its_place_not_thrown()
    {
        Assert.Equal(["talk.json:/conversations/0"], Problems("""{ "conversations": [ null ] }"""));
        Assert.Equal(["talk.json:/conversations/0/steps/0"], Problems("""{ "conversations": [ { "id": "c", "participants": [], "steps": [ null ] } ] }"""));
        Assert.Equal(["talk.json:/conversations/0/participants/0"], Problems("""{ "conversations": [ { "id": "c", "participants": [ null ], "steps": [] } ] }"""));
        Assert.Equal(
            ["talk.json:/conversations/0/steps/0/lines/0", "talk.json:/conversations/0/steps/0/choices/0", "talk.json:/conversations/0/steps/0/next/0"],
            Problems("""{ "conversations": [ { "id": "c", "participants": [], "steps": [ { "id": "a", "lines": [ null ], "choices": [ null ], "next": [ null ] } ] } ] }"""));
        Assert.Equal(
            ["talk.json:/conversations/0/steps/0/choices/0/effects/0"],
            Problems("""{ "conversations": [ { "id": "c", "participants": [], "steps": [ { "id": "a", "lines": [], "choices": [ { "id": "k", "text": "t", "effects": [ null ] } ] } ] } ] }"""));
        Assert.Equal(["talk.json:/characters/0", "talk.json:/fights/0"], Problems("""{ "characters": [ null ], "fights": [ null ] }"""));

        var index = ContentPackLoader.Load(new MemoryContentSource([KeyValuePair.Create("pack.json", Pack.Replace("\"base.json\"", "null", StringComparison.Ordinal))]));
        Assert.Equal(["pack.json:/files/0"], index.Error.Details.Select(d => d.Path));
    }

    [Theory]
    [InlineData("""{ "conversations": [ { "id": "c", "participants": [], "steps": [ { "id": "LONG", "lines": [] } ] } ] }""", "talk.json:/conversations/0/steps/0/id")]
    [InlineData("""{ "conversations": [ { "id": "c", "participants": [], "steps": [ { "id": "a", "lines": [], "choices": [ { "id": "LONG", "text": "t" } ] } ] } ] }""", "talk.json:/conversations/0/steps/0/choices/0/id")]
    [InlineData("""{ "conversations": [ { "id": "c", "participants": [], "steps": [ { "id": "a", "lines": [], "choices": [ { "id": "k", "text": "t", "effects": [ { "type": "setFlag", "flag": "LONG" } ] } ] } ] } ] }""", "talk.json:/conversations/0/steps/0/choices/0/effects/0/flag")]
    [InlineData("""{ "conversations": [ { "id": "c", "participants": [], "steps": [ { "id": "a", "lines": [], "choices": [ { "id": "k", "text": "t", "when": [ { "type": "flag", "flag": "LONG" } ] } ] } ] } ] }""", "talk.json:/conversations/0/steps/0/choices/0/when/0/flag")]
    public void Step_ids_choice_ids_and_flags_over_64_characters_are_refused_because_saves_could_not_hold_them(string talk, string path)
    {
        Assert.Equal([path], Problems(talk.Replace("LONG", new string('x', 65), StringComparison.Ordinal)));
    }

    // Start only asks whether a fight exists, so an empty shell stands in for one.
    private static AncientOrder.Rules.Combat.CombatState TestCombat() =>
        (AncientOrder.Rules.Combat.CombatState)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(AncientOrder.Rules.Combat.CombatState));
}
