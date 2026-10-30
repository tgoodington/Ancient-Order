using System.Collections.Immutable;
using AncientOrder.Rules.Common;
using AncientOrder.Rules.Conditions;
using AncientOrder.Rules.Content.Packs;
using AncientOrder.Rules.Conversations;
using AncientOrder.Rules.Session;
using AncientOrder.Rules.State;
using AncientOrder.Rules.Tests.Support;

namespace AncientOrder.Rules.Tests.ContentPacks;

public class ConditionTests
{
    private const string Pack = """{ "format": "ancient-order-content", "version": 1, "id": "t", "name": "Test", "standIn": false, "files": ["base.json", "talk.json"] }""";

    private const string Base = """
        {
          "settings": { "playerName": "Kael", "lineup": { "min": 1, "max": 1 } },
          "archetypes": [{ "id": "x" }],
          "characters": [
            { "id": "ann", "name": "Ann", "archetypeId": "x", "personality": { "patience": 20, "empathy": 20, "cunning": 10, "logic": 15, "kindness": 20, "charisma": 15 }, "startingAffection": 10, "startingTrust": -5 }
          ],
          "fights": [ { "id": "brawl", "name": "Brawl", "enemies": [ { "id": "rat", "name": "Rat", "archetype": "x", "rank": 1, "stamina": 10, "power": 5, "speed": 5, "elementalPath": "fire", "reactionSkills": { "block": { "sr": 1, "smr": 1, "fmr": 1 }, "dodge": { "sr": 1, "smr": 1, "fmr": 1 }, "parry": { "sr": 1, "smr": 1, "fmr": 1 } } } ] } ]
        }
        """;

    private const string Talk = """
        {
          "conversations": [
            {
              "id": "chat",
              "participants": ["ann"],
              "start": [ { "step": "again", "when": [ { "type": "seen", "conversation": "chat" } ] }, { "step": "hello" } ],
              "steps": [
                { "id": "hello",
                  "lines": [
                    { "speaker": "ann", "text": "Hi." },
                    { "speaker": "ann", "text": "Flagged.", "when": [ { "type": "flag", "flag": "met" } ] }
                  ],
                  "choices": [
                    { "id": "mark", "text": "Mark", "effects": [ { "type": "setFlag", "flag": "met" }, { "type": "setFlag", "flag": "met" } ], "next": [ { "step": "end", "when": [ { "type": "flag", "flag": "met" } ] }, { "step": "hello" } ] },
                    { "id": "unmark", "text": "Unmark", "effects": [ { "type": "clearFlag", "flag": "met" }, { "type": "clearFlag", "flag": "never" } ] },
                    { "id": "clever", "text": "Clever", "lockedText": "You cannot think of it.", "requires": [ { "type": "trait", "trait": "cunning", "atLeast": 30 } ] },
                    { "id": "hidden", "text": "Hidden", "when": [ { "type": "flag", "flag": "secret" } ] },
                    { "id": "friend", "text": "Friend", "lockedText": "Not yet.", "requires": [
                      { "type": "relationship", "character": "ann", "measure": "affection", "atLeast": 50 },
                      { "type": "any", "conditions": [ { "type": "inParty", "character": "ann" }, { "type": "lineupReady" } ] },
                      { "type": "not", "condition": { "type": "chose", "conversation": "chat", "choice": "unmark" } } ] }
                  ] },
                { "id": "again", "lines": [ { "speaker": "narrator", "text": "Again." } ] },
                { "id": "end", "lines": [ { "speaker": "narrator", "text": "End." } ] }
              ]
            }
          ]
        }
        """;

    private static GameContent Content(string talk = Talk)
    {
        var loaded = ContentPackLoader.Load(new MemoryContentSource([KeyValuePair.Create("pack.json", Pack), KeyValuePair.Create("base.json", Base), KeyValuePair.Create("talk.json", talk)]));
        Assert.True(loaded.IsOk, loaded.IsOk ? string.Empty : string.Join("\n", loaded.Error.Details));
        return loaded.Value;
    }

    private static string[] Problems(string talk)
    {
        var loaded = ContentPackLoader.Load(new MemoryContentSource([KeyValuePair.Create("pack.json", Pack), KeyValuePair.Create("base.json", Base), KeyValuePair.Create("talk.json", talk)]));
        Assert.False(loaded.IsOk);
        return [.. loaded.Error.Details.Select(d => d.Path)];
    }

    private static GameSession Session()
    {
        var session = TestSessions.Create(Content());
        Assert.True(session.NewGame().IsOk);
        return session;
    }

    [Fact]
    public void Choices_set_and_clear_flags_and_the_report_lists_only_real_changes()
    {
        var session = Session();
        session.StartConversation("chat");

        var marked = session.Choose("mark").Value;
        Assert.Equal(["met"], marked.FlagsSet);
        Assert.True(session.Snapshot.Game!.Memory.HasFlag("met"));
        Assert.Equal("end", session.Snapshot.Conversation!.StepId);
    }

    [Fact]
    public void Clearing_removes_a_flag_and_an_unset_flag_is_not_reported()
    {
        var content = Content();
        var game = GameStateUpdates.NewGame(content, "p", 1);
        game = game with { Memory = game.Memory.WithFlag("met") };
        var started = ConversationRules.Start(content, game, "chat", 2).Value.Game;
        var report = ConversationRules.Choose(content, started, "unmark", 3).Value;

        Assert.Equal(["met"], report.Report.FlagsCleared);
        Assert.False(report.Game.Memory.HasFlag("met"));
        Assert.True(game.Memory.HasFlag("met"));
    }

    [Fact]
    public void Every_choice_taken_and_conversation_seen_is_remembered()
    {
        var session = Session();
        var before = session.Snapshot.Game!;
        Assert.False(before.Memory.HasSeen("chat"));

        session.StartConversation("chat");
        Assert.True(session.Snapshot.Game!.Memory.HasSeen("chat"));
        Assert.False(session.Snapshot.Game.Memory.HasChosen("chat", "mark"));
        session.Choose("unmark");

        Assert.True(session.Snapshot.Game.Memory.HasChosen("chat", "unmark"));
        Assert.False(before.Memory.HasSeen("chat"));
    }

    [Fact]
    public void A_start_entry_that_holds_wins_and_seen_is_recorded_after_the_opening_is_chosen()
    {
        var session = Session();
        Assert.Equal("hello", session.StartConversation("chat").Value.StepId);
        session.Choose("unmark");
        Assert.Equal("again", session.StartConversation("chat").Value.StepId);
    }

    [Fact]
    public void Lines_and_choices_follow_their_when_and_a_locked_choice_carries_its_explained_requirement()
    {
        var session = Session();
        var view = session.StartConversation("chat").Value;
        Assert.Single(view.Lines);
        Assert.Equal(["mark", "unmark", "clever", "friend"], view.Choices.Select(c => c.Id));
        Assert.True(view.Choices[0].Available);
        Assert.Null(view.Choices[0].Requirement);

        var clever = view.Choices[2];
        Assert.False(clever.Available);
        Assert.Equal("You cannot think of it.", clever.LockedText);
        var tree = Assert.IsType<RequirementView.All>(clever.Requirement);
        Assert.False(tree.Met);
        var trait = Assert.IsType<RequirementView.TraitRange>(Assert.Single(tree.Children));
        Assert.False(trait.Met);
        Assert.Equal(30, trait.AtLeast);
        Assert.Equal(session.Snapshot.Game!.Player.Personality.Cunning, trait.Current);

        Assert.Equal(ErrorCode.CHOICE_UNAVAILABLE, session.Choose("clever").Error.Code);
        Assert.Equal(ErrorCode.CHOICE_UNAVAILABLE, session.Choose("hidden").Error.Code);
    }

    [Fact]
    public void Explain_mirrors_all_any_and_not_with_current_values_and_names()
    {
        var session = Session();
        var friend = session.StartConversation("chat").Value.Choices.Single(c => c.Id == "friend");
        var all = Assert.IsType<RequirementView.All>(friend.Requirement);
        Assert.False(all.Met);
        Assert.Equal(3, all.Children.Length);

        var relationship = Assert.IsType<RequirementView.RelationshipRange>(all.Children[0]);
        Assert.Equal("Ann", relationship.CharacterName);
        Assert.Equal(RelationshipMeasure.Affection, relationship.Measure);
        Assert.Equal(10, relationship.Current);
        Assert.False(relationship.Met);

        var any = Assert.IsType<RequirementView.Any>(all.Children[1]);
        Assert.False(any.Met);
        var party = Assert.IsType<RequirementView.InParty>(any.Children[0]);
        Assert.Equal("Ann", party.CharacterName);
        Assert.False(party.Met);
        var lineup = Assert.IsType<RequirementView.LineupReady>(any.Children[1]);
        Assert.False(lineup.Met);
        Assert.NotNull(lineup.Reason);

        var not = Assert.IsType<RequirementView.Negation>(all.Children[2]);
        Assert.True(not.Met);
        Assert.False(Assert.IsType<RequirementView.Chose>(not.Child).Met);
    }

    [Fact]
    public void Holds_agrees_with_Explain_and_thresholds_are_inclusive()
    {
        var content = Content();
        var game = GameStateUpdates.NewGame(content, "p", 1);

        Assert.True(Conditions.Conditions.Holds(content, game, default));
        Assert.True(Conditions.Conditions.Holds(content, game, []));
        Assert.True(Conditions.Conditions.Holds(content, game, [new Condition.RelationshipRange("ann", RelationshipMeasure.Affection, 10, 10)]));
        Assert.True(Conditions.Conditions.Holds(content, game, [new Condition.RelationshipRange("ann", RelationshipMeasure.Trust, null, -5)]));
        Assert.False(Conditions.Conditions.Holds(content, game, [new Condition.RelationshipRange("ann", RelationshipMeasure.Trust, -4, null)]));
        Assert.True(Conditions.Conditions.Holds(content, game, [new Condition.Any([new Condition.Flag("a"), new Condition.Negation(new Condition.Flag("b"))])]));
        Assert.False(Conditions.Conditions.Holds(content, game, [new Condition.Flag("a")]));

        ImmutableArray<Condition> list = [new Condition.Flag("a"), new Condition.Negation(new Condition.Seen("chat"))];
        Assert.Equal(Conditions.Conditions.Holds(content, game, list), Conditions.Conditions.Explain(content, game, list).Met);
    }

    [Fact]
    public void A_destination_whose_condition_fails_is_skipped_for_the_next()
    {
        var content = Content();
        var game = ConversationRules.Start(content, GameStateUpdates.NewGame(content, "p", 1), "chat", 2).Value.Game;
        var moved = ConversationRules.Choose(content, game, "mark", 3).Value.Game;
        Assert.Equal("end", moved.Conversation!.StepId);
    }

    [Fact]
    public void A_step_whose_choices_are_all_hidden_offers_Continue()
    {
        const string talk = """
            { "conversations": [ { "id": "c", "participants": [], "steps": [
              { "id": "a", "lines": [], "choices": [ { "id": "k", "text": "t", "when": [ { "type": "flag", "flag": "f" } ] } ], "next": [ { "step": "b" } ] },
              { "id": "b", "lines": [] } ] } ] }
            """;
        var content = Content(talk);
        var game = ConversationRules.Start(content, GameStateUpdates.NewGame(content, "p", 1), "c", 2).Value.Game;
        var view = ConversationView.From(content, game)!;
        Assert.True(view.IsContinue);
        Assert.Equal("b", ConversationRules.Continue(content, game, 3).Value.Game.Conversation!.StepId);
    }

    [Fact]
    public void Content_with_bad_conditions_is_refused_with_file_places()
    {
        const string bad = """
            { "conversations": [ { "id": "c", "participants": [], "steps": [
              { "id": "a", "lines": [ { "speaker": "narrator", "text": "x", "when": [ { "type": "chose", "conversation": "c", "choice": "nope" } ] } ],
                "choices": [
                  { "id": "k", "text": "t",
                    "when": [ { "type": "seen", "conversation": "ghost" }, { "type": "trait", "trait": "logic" } ],
                    "requires": [ { "type": "relationship", "character": "zed", "measure": "trust" }, { "type": "all", "conditions": [ { "type": "lineupReady", "fight": "none" } ] } ],
                    "effects": [ { "type": "setFlag", "flag": "" } ],
                    "next": [ { "step": "a", "when": [ { "type": "inParty", "character": "zed" } ] } ] } ] } ],
              "start": [ { "step": "a", "when": [ { "type": "flag", "flag": "" } ] } ] } ] }
            """;

        var paths = Problems(bad);
        Assert.Contains("talk.json:/conversations/0/steps/0/lines/0/when/0/choice", paths);
        Assert.Contains("talk.json:/conversations/0/steps/0/choices/0/when/0/conversation", paths);
        Assert.Contains("talk.json:/conversations/0/steps/0/choices/0/when/1", paths);
        Assert.Contains("talk.json:/conversations/0/steps/0/choices/0/requires/0/character", paths);
        Assert.Contains("talk.json:/conversations/0/steps/0/choices/0/requires/0", paths);
        Assert.Contains("talk.json:/conversations/0/steps/0/choices/0/requires/1/conditions/0/fight", paths);
        Assert.Contains("talk.json:/conversations/0/steps/0/choices/0/effects/0/flag", paths);
        Assert.Contains("talk.json:/conversations/0/steps/0/choices/0/next/0/when/0/character", paths);
        Assert.Contains("talk.json:/conversations/0/start/0/when/0/flag", paths);
    }

    [Fact]
    public void Malformed_condition_records_are_refused_not_thrown()
    {
        string Wrap(string condition) => "{ \"conversations\": [ { \"id\": \"c\", \"participants\": [], \"steps\": [ { \"id\": \"a\", \"lines\": [ { \"speaker\": \"narrator\", \"text\": \"x\", \"when\": [ " + condition + " ] } ] } ] } ] }";

        Assert.NotEmpty(Problems(Wrap("""{ "type": "mystery" }""")));
        Assert.NotEmpty(Problems(Wrap("""{ "flag": "x" }""")));
        Assert.NotEmpty(Problems(Wrap("""{ "type": "not" }""")));
        Assert.NotEmpty(Problems(Wrap("""{ "type": "trait", "trait": "luck", "atLeast": 1 }""")));
        Assert.NotEmpty(Problems(Wrap("""{ "type": "relationship", "character": "ann", "measure": "love", "atLeast": 1 }""")));
        Assert.NotEmpty(Problems(Wrap("null")));
    }

    [Fact]
    public void Lineup_ready_naming_a_real_fight_is_accepted()
    {
        const string talk = """
            { "conversations": [ { "id": "c", "participants": [], "steps": [
              { "id": "a", "lines": [ { "speaker": "narrator", "text": "x", "when": [ { "type": "lineupReady", "fight": "brawl" } ] } ] } ] } ] }
            """;
        Assert.NotNull(Content(talk));
    }
}
