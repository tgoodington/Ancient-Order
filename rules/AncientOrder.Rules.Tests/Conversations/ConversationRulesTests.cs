using AncientOrder.Rules.Common;
using AncientOrder.Rules.Content.Packs;
using AncientOrder.Rules.Conversations;
using AncientOrder.Rules.Session;
using AncientOrder.Rules.State;
using AncientOrder.Rules.Tests.Support;

namespace AncientOrder.Rules.Tests.ConversationTesting;

/// <summary>
/// Playing conversations (ES-4): openings, chaining, endings, Continue, refusals that change nothing, and immutability. The basic play-through,
/// the pack format, and the load-time reference checks are in ContentPacks/ConversationTests.cs and ConversationLoadTests.cs; this adds what they do not.
/// </summary>
public class ConversationRulesTests
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
              "id": "talk",
              "title": "A talk",
              "participants": ["ann"],
              "start": [ { "step": "back", "when": [ { "type": "seen", "conversation": "talk" } ] }, { "step": "first" } ],
              "steps": [
                { "id": "first",
                  "lines": [ { "speaker": "ann", "text": "Hello." }, { "speaker": "player", "text": "Hi." }, { "speaker": "narrator", "text": "Wind." } ],
                  "choices": [
                    { "id": "warm", "text": "Be warm", "effects": [ { "type": "setFlag", "flag": "warm" }, { "type": "relationship", "character": "ann", "affection": 5, "trust": 3 } ], "next": [ { "step": "after" } ] },
                    { "id": "leave", "text": "Leave" },
                    { "id": "onward", "text": "Onward", "next": [ { "conversation": "scene" } ] }
                  ] },
                { "id": "back", "lines": [ { "speaker": "ann", "text": "Again." } ], "next": [] },
                { "id": "after",
                  "lines": [ { "speaker": "ann", "text": "Thanks." } ],
                  "choices": [ { "id": "secret", "text": "Whisper", "when": [ { "type": "flag", "flag": "never" } ] } ],
                  "next": [ { "step": "first", "when": [ { "type": "flag", "flag": "never" } ] } ] }
              ]
            },
            {
              "id": "scene",
              "title": "A scene",
              "participants": ["ann", "bo"],
              "start": [ { "step": "late", "when": [ { "type": "flag", "flag": "never" } ] } ],
              "steps": [
                { "id": "s1", "lines": [ { "speaker": "narrator", "text": "Night falls." }, { "speaker": "ann", "text": "Cold." }, { "speaker": "bo", "text": "Very." } ], "next": [ { "step": "s2" } ] },
                { "id": "s2", "lines": [ { "speaker": "narrator", "text": "Dawn." } ] },
                { "id": "late", "lines": [ { "speaker": "narrator", "text": "Never." } ] }
              ]
            }
          ]
        }
        """;

    private static GameContent Content()
    {
        var loaded = ContentPackLoader.Load(new MemoryContentSource(
            [KeyValuePair.Create("pack.json", Pack), KeyValuePair.Create("base.json", Base), KeyValuePair.Create("talk.json", Talk)]));
        Assert.True(loaded.IsOk, loaded.IsOk ? string.Empty : string.Join("\n", loaded.Error.Details));
        return loaded.Value;
    }

    private static GameSession Session()
    {
        var session = TestSessions.Create(Content());
        Assert.True(session.NewGame().IsOk);
        return session;
    }

    [Fact]
    public void The_opening_is_the_first_start_entry_that_holds_and_a_return_visit_sees_the_earlier_visit()
    {
        var session = Session();

        Assert.Equal("first", session.StartConversation("talk").Value.StepId);
        Assert.True(session.Choose("leave").Value.Ended);
        Assert.True(session.Snapshot.Game!.Memory.HasSeen("talk"));

        Assert.Equal("back", session.StartConversation("talk").Value.StepId);
    }

    [Fact]
    public void When_no_start_entry_holds_the_first_step_opens()
    {
        var session = Session();
        session.StartConversation("talk");
        session.Choose("onward");

        Assert.Equal("s1", session.Snapshot.Conversation!.StepId);
    }

    [Fact]
    public void A_chained_conversation_opens_like_a_start_and_counts_as_seen()
    {
        var session = Session();
        session.StartConversation("talk");
        Assert.False(session.Snapshot.Game!.Memory.HasSeen("scene"));

        var report = session.Choose("onward").Value;

        Assert.True(report.Ended);
        Assert.Equal("scene", report.NextConversationId);
        Assert.Equal(new ActiveConversation("scene", "s1"), session.Snapshot.Game.Conversation);
        Assert.True(session.Snapshot.Game.Memory.HasSeen("scene"));
        Assert.True(session.Snapshot.Game.Memory.HasChosen("talk", "onward"));
    }

    [Fact]
    public void A_narrated_scene_with_three_speakers_steps_through_with_Continue_and_ends()
    {
        var session = Session();
        session.StartConversation("talk");
        session.Choose("onward");

        var view = session.Snapshot.Conversation!;
        Assert.Equal(["Ann", "Bo"], view.Participants.Select(p => p.Name));
        Assert.Equal([SpeakerKind.Narrator, SpeakerKind.Character, SpeakerKind.Character], view.Lines.Select(l => l.SpeakerKind));
        Assert.Equal([null, "Ann", "Bo"], view.Lines.Select(l => l.SpeakerName));
        Assert.True(view.IsContinue);
        Assert.Empty(view.Choices);

        var toNext = session.Continue().Value;
        Assert.False(toNext.Ended);
        Assert.Null(toNext.ChoiceId);
        Assert.Equal("s2", session.Snapshot.Conversation!.StepId);

        var done = session.Continue().Value;
        Assert.True(done.Ended);
        Assert.Null(done.NextConversationId);
        Assert.Null(session.Snapshot.Game!.Conversation);
        Assert.Null(session.Snapshot.Conversation);
    }

    [Fact]
    public void An_empty_destination_list_ends_the_conversation_on_Continue()
    {
        var session = Session();
        session.StartConversation("talk");
        session.Choose("leave");
        session.StartConversation("talk");
        Assert.Equal("back", session.Snapshot.Conversation!.StepId);

        Assert.True(session.Continue().Value.Ended);
        Assert.Null(session.Snapshot.Game!.Conversation);
    }

    [Fact]
    public void A_step_whose_choices_are_all_hidden_offers_Continue_and_an_unmatched_destination_list_ends_it()
    {
        var session = Session();
        session.StartConversation("talk");
        session.Choose("warm");

        var view = session.Snapshot.Conversation!;
        Assert.Equal("after", view.StepId);
        Assert.True(view.IsContinue);
        Assert.Empty(view.Choices);

        Assert.Equal(ErrorCode.CHOICE_UNAVAILABLE, session.Choose("secret").Error.Code);
        Assert.Equal("after", session.Snapshot.Conversation!.StepId);

        var done = session.Continue().Value;
        Assert.True(done.Ended);
        Assert.Null(done.NextConversationId);
        Assert.Null(session.Snapshot.Game!.Conversation);
    }

    [Fact]
    public void Each_refusal_leaves_the_game_and_version_unchanged()
    {
        var session = Session();

        void Refused(ErrorCode code, Func<RuleError> act)
        {
            var before = session.Snapshot;
            var game = before.Game;
            var error = act();
            Assert.Equal(code, error.Code);
            Assert.Same(before, session.Snapshot);
            Assert.Same(game, session.Snapshot.Game);
            Assert.Equal(before.Version, session.Snapshot.Version);
        }

        Refused(ErrorCode.NO_ACTIVE_CONVERSATION, () => session.Choose("warm").Error);
        Refused(ErrorCode.NO_ACTIVE_CONVERSATION, () => session.Continue().Error);
        Refused(ErrorCode.CONVERSATION_NOT_FOUND, () => session.StartConversation("nope").Error);

        Assert.True(session.StartConversation("talk").IsOk);
        Refused(ErrorCode.CONVERSATION_ALREADY_ACTIVE, () => session.StartConversation("scene").Error);
        Refused(ErrorCode.CHOICE_UNAVAILABLE, () => session.Choose("nope").Error);
        Refused(ErrorCode.CHOICE_UNAVAILABLE, () => session.Continue().Error);

        Assert.True(session.Choose("warm").IsOk);
        Refused(ErrorCode.CHOICE_UNAVAILABLE, () => session.Choose("secret").Error);
        Refused(ErrorCode.CHOICE_UNAVAILABLE, () => session.Choose("leave").Error);
    }

    [Fact]
    public void Starting_during_combat_is_refused_and_the_game_is_left_as_it_was()
    {
        var content = Content();
        var session = Session();
        var game = session.Snapshot.Game! with { Combat = Combat() };
        var copy = game with { };

        var refused = ConversationRules.Start(content, game, "talk", 1);

        Assert.Equal(ErrorCode.COMBAT_ALREADY_ACTIVE, refused.Error.Code);
        Assert.Equal(copy, game);
        Assert.Null(game.Conversation);
        Assert.False(game.Memory.HasSeen("talk"));
    }

    [Fact]
    public void A_choice_leaves_every_earlier_game_state_as_it_was()
    {
        var session = Session();
        session.StartConversation("talk");
        var before = session.Snapshot.Game!;
        var beforeVersion = session.Snapshot.Version;

        var report = session.Choose("warm").Value;
        var after = session.Snapshot.Game!;

        Assert.NotSame(before, after);
        Assert.True(session.Snapshot.Version > beforeVersion);
        Assert.Equal(new ActiveConversation("talk", "first"), before.Conversation);
        Assert.Equal(0, before.Characters["ann"].Affection);
        Assert.Equal(0, before.Characters["ann"].Trust);
        Assert.False(before.Memory.HasFlag("warm"));
        Assert.False(before.Memory.HasChosen("talk", "warm"));

        Assert.Equal(new ActiveConversation("talk", "after"), after.Conversation);
        Assert.Equal(5, after.Characters["ann"].Affection);
        Assert.Equal(3, after.Characters["ann"].Trust);
        Assert.True(after.Memory.HasFlag("warm"));
        Assert.True(after.Memory.HasChosen("talk", "warm"));
        Assert.Equal(["warm"], report.FlagsSet);
    }

    [Fact]
    public void LastChoice_is_set_by_a_committed_choice_and_by_nothing_else()
    {
        var session = Session();
        Assert.Null(session.Snapshot.LastChoice);

        session.StartConversation("talk");
        Assert.Null(session.Snapshot.LastChoice);

        var report = session.Choose("warm").Value;
        Assert.Same(report, session.Snapshot.LastChoice);
        Assert.Equal("warm", session.Snapshot.LastChoice!.ChoiceId);

        Assert.False(session.Choose("nope").IsOk);
        Assert.Same(report, session.Snapshot.LastChoice);

        Assert.True(session.Continue().IsOk);
        Assert.Null(session.Snapshot.LastChoice!.ChoiceId);
        Assert.NotSame(report, session.Snapshot.LastChoice);

        session.StartConversation("talk");
        Assert.Null(session.Snapshot.LastChoice);
    }

    // Start only asks whether a fight exists, so an empty shell stands in for one.
    private static AncientOrder.Rules.Combat.CombatState Combat() =>
        (AncientOrder.Rules.Combat.CombatState)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(AncientOrder.Rules.Combat.CombatState));
}
