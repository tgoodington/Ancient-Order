using AncientOrder.Rules.Session;
using AncientOrder.Rules.Story;
using AncientOrder.Rules.Tests.Support;

namespace AncientOrder.Rules.Tests.ConditionTesting;

public class StoryMemoryTests
{
    private const string Talk = """
        {
          "conversations": [
            {
              "id": "intro", "participants": ["ann"],
                            "steps": [
                { "id": "first", "lines": [], "choices": [
                  { "id": "raise", "text": "Raise", "effects": [ { "type": "setFlag", "flag": "alarm" } ] },
                  { "id": "calm", "text": "Calm", "effects": [ { "type": "clearFlag", "flag": "alarm" } ] },
                  { "id": "both", "text": "Both", "effects": [ { "type": "setFlag", "flag": "a" }, { "type": "setFlag", "flag": "b" } ] },
                  { "id": "idle", "text": "Idle", "effects": [ { "type": "clearFlag", "flag": "never" } ] },
                  { "id": "stay", "text": "Stay", "next": [ { "step": "first" } ] }
                ] }
              ]
            },
            {
              "id": "gate", "participants": [],
              "start": [ { "step": "again", "when": [ { "type": "seen", "conversation": "gate" } ] }, { "step": "first" } ],
              "steps": [ { "id": "first", "lines": [] }, { "id": "again", "lines": [] } ]
            },
            {
              "id": "later", "participants": ["ann"],
              "steps": [
                { "id": "check",
                  "lines": [
                    { "speaker": "narrator", "text": "Alarm is up.", "when": [ { "type": "flag", "flag": "alarm" } ] },
                    { "speaker": "narrator", "text": "You raised it.", "when": [ { "type": "chose", "conversation": "intro", "choice": "raise" } ] },
                    { "speaker": "narrator", "text": "You were there.", "when": [ { "type": "seen", "conversation": "intro" } ] }
                  ],
                  "choices": [ { "id": "raise", "text": "Raise again" }, { "id": "calm", "text": "Calm", "effects": [ { "type": "clearFlag", "flag": "alarm" } ] } ] }
              ]
            }
          ]
        }
        """;

    private static GameSession Session()
    {
        var session = TestSessions.Create(ConditionPack.Load(Talk));
        Assert.True(session.NewGame().IsOk);
        return session;
    }

    private static StoryMemory Memory(GameSession session) => session.Snapshot.Game!.Memory;

    [Fact]
    public void A_new_game_remembers_nothing()
    {
        var memory = Memory(Session());

        Assert.Empty(memory.Flags);
        Assert.Empty(memory.Chosen);
        Assert.Empty(memory.Seen);
    }

    [Fact]
    public void SetFlag_sets_the_flag_reports_it_and_leaves_the_earlier_game_alone()
    {
        var session = Session();
        session.StartConversation("intro");
        var before = session.Snapshot.Game!;

        var report = session.Choose("raise").Value;

        Assert.True(Memory(session).HasFlag("alarm"));
        Assert.Equal(["alarm"], report.FlagsSet);
        Assert.Empty(report.FlagsCleared);
        Assert.False(before.Memory.HasFlag("alarm"));
    }

    [Fact]
    public void SetFlag_on_a_flag_already_set_is_not_reported_again()
    {
        var session = Session();
        session.StartConversation("intro");
        session.Choose("raise");
        session.StartConversation("intro");
        var again = session.Choose("raise").Value;

        Assert.True(Memory(session).HasFlag("alarm"));
        Assert.Empty(again.FlagsSet);
    }

    [Fact]
    public void ClearFlag_removes_a_set_flag_and_a_cleared_flag_can_be_set_again()
    {
        var session = Session();
        session.StartConversation("intro");
        session.Choose("raise");
        session.StartConversation("intro");
        var calm = session.Choose("calm").Value;

        Assert.False(Memory(session).HasFlag("alarm"));
        Assert.Equal(["alarm"], calm.FlagsCleared);

        session.StartConversation("intro");
        session.Choose("raise");
        Assert.True(Memory(session).HasFlag("alarm"));
    }

    [Fact]
    public void ClearFlag_on_a_flag_that_is_not_set_changes_nothing_and_reports_nothing()
    {
        var session = Session();
        session.StartConversation("intro");
        var idle = session.Choose("idle").Value;

        Assert.Empty(idle.FlagsCleared);
        Assert.Empty(Memory(session).Flags);
    }

    [Fact]
    public void One_choice_can_set_several_flags()
    {
        var session = Session();
        session.StartConversation("intro");
        var report = session.Choose("both").Value;

        Assert.Equal(["a", "b"], report.FlagsSet.Order());
        Assert.True(Memory(session).HasFlag("a"));
        Assert.True(Memory(session).HasFlag("b"));
    }

    [Fact]
    public void A_choice_is_remembered_when_taken_and_only_for_its_own_conversation()
    {
        var session = Session();
        session.StartConversation("intro");
        Assert.False(Memory(session).HasChosen("intro", "raise"));

        session.Choose("stay");
        Assert.True(Memory(session).HasChosen("intro", "stay"));
        Assert.False(Memory(session).HasChosen("intro", "raise"));

        session.Choose("raise");
        Assert.True(Memory(session).HasChosen("intro", "raise"));
        Assert.False(Memory(session).HasChosen("later", "raise"));
        Assert.Equal(2, Memory(session).Chosen.Count);
    }

    [Fact]
    public void A_refused_choice_is_not_remembered()
    {
        var session = Session();
        session.StartConversation("intro");
        Assert.False(session.Choose("nope").IsOk);
        Assert.Empty(Memory(session).Chosen);
    }

    [Fact]
    public void A_conversation_is_remembered_as_seen_when_it_starts_not_when_it_ends()
    {
        var session = Session();
        Assert.False(Memory(session).HasSeen("intro"));

        session.StartConversation("intro");
        Assert.True(Memory(session).HasSeen("intro"));
        Assert.False(Memory(session).HasSeen("later"));
        Assert.Null(session.Snapshot.LastChoice);

        session.Choose("raise");
        session.StartConversation("later");
        Assert.True(Memory(session).HasSeen("later"));
    }

    [Fact]
    public void A_refused_start_is_not_remembered()
    {
        var session = Session();
        Assert.False(session.StartConversation("nowhere").IsOk);
        Assert.Empty(Memory(session).Seen);
    }

    [Fact]
    public void Memory_is_kept_across_later_conversations_and_shapes_what_they_show()
    {
        var fresh = Session();
        fresh.StartConversation("later");
        Assert.Empty(fresh.Snapshot.Conversation!.Lines);

        var session = Session();
        session.StartConversation("intro");
        session.Choose("raise");
        var view = session.StartConversation("later").Value;

        Assert.Equal(["Alarm is up.", "You raised it.", "You were there."], view.Lines.Select(l => l.Text));
        session.Choose("raise");
        Assert.True(Memory(session).HasFlag("alarm"));
        Assert.True(Memory(session).HasChosen("intro", "raise"));
        Assert.True(Memory(session).HasChosen("later", "raise"));
        Assert.True(Memory(session).HasSeen("intro"));
        Assert.True(Memory(session).HasSeen("later"));
    }

    [Fact]
    public void A_later_conversation_can_clear_what_an_earlier_one_set()
    {
        var session = Session();
        session.StartConversation("intro");
        session.Choose("raise");
        session.StartConversation("later");
        session.Choose("calm");

        Assert.False(Memory(session).HasFlag("alarm"));
        Assert.True(Memory(session).HasChosen("intro", "raise"));
    }

    [Fact]
    public void Seen_changes_which_start_entry_opens_the_next_time()
    {
        var session = Session();
        Assert.Equal("first", session.StartConversation("gate").Value.StepId);
        session.Continue();
        Assert.Equal("again", session.StartConversation("gate").Value.StepId);
    }

    [Fact]
    public void Memory_updates_return_new_memories_and_compare_ordinally()
    {
        var empty = StoryMemory.Empty;
        var flagged = empty.WithFlag("f");
        var chosen = flagged.WithChosen("c", "k");
        var seen = chosen.WithSeen("c");

        Assert.False(empty.HasFlag("f"));
        Assert.False(flagged.HasChosen("c", "k"));
        Assert.False(chosen.HasSeen("c"));
        Assert.True(seen.HasFlag("f") && seen.HasChosen("c", "k") && seen.HasSeen("c"));
        Assert.False(seen.WithoutFlag("f").HasFlag("f"));
        Assert.True(seen.HasFlag("f"));
        Assert.Single(seen.WithFlag("f").Flags);

        Assert.False(seen.HasFlag("F"));
        Assert.False(seen.HasChosen("C", "k"));
        Assert.False(seen.HasChosen("c", "K"));
        Assert.False(seen.HasSeen("C"));
        Assert.Equal(empty, StoryMemory.Empty);
    }
}
