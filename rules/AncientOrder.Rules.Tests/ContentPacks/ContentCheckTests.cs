using AncientOrder.Rules.Content.Check;
using AncientOrder.Rules.Content.Packs;

namespace AncientOrder.Rules.Tests.ContentPacks;

public class ContentCheckTests
{
    private const string Pack = """{ "format": "ancient-order-content", "version": 1, "id": "t", "name": "Test", "standIn": false, "files": ["all.json"] }""";

    private const string Good = """
        {
          "settings": { "playerName": "Kael", "lineup": { "min": 1, "max": 1 }, "startingTown": "square" },
          "archetypes": [{ "id": "x" }],
          "characters": [
            { "id": "ann", "name": "Ann", "archetypeId": "x", "personality": { "patience": 20, "empathy": 20, "cunning": 10, "logic": 15, "kindness": 20, "charisma": 15 }, "startingAffection": 0, "startingTrust": 0 }
          ],
          "conversations": [
            { "id": "chat", "participants": ["ann"], "start": [{ "step": "hello" }],
              "steps": [
                { "id": "hello", "lines": [ { "speaker": "ann", "text": "Hi." } ], "choices": [
                  { "id": "mark", "text": "Mark", "effects": [ { "type": "setFlag", "flag": "met" } ], "next": [ { "step": "bye" } ] },
                  { "id": "secret", "text": "Whisper", "lockedText": "Not yet.", "requires": [ { "type": "flag", "flag": "met" } ] }
                ] },
                { "id": "bye", "lines": [ { "speaker": "ann", "text": "Bye." } ] }
              ] }
          ],
          "towns": [
            { "id": "square", "name": "Square", "ground": [ [[0, 0], [20, 0], [20, 20], [0, 20]] ],
              "obstacles": [ { "id": "fountain", "footprint": [[8, 8], [12, 8], [12, 12], [8, 12]], "height": 1.5 } ],
              "start": [1, 1],
              "people": [ { "character": "ann", "placements": [ { "at": [15, 15] } ], "opens": [ { "conversation": "chat" } ] } ],
              "spots": [ { "id": "well", "area": { "circle": { "center": [2, 2], "radius": 1 } }, "trigger": "interact" } ] }
          ]
        }
        """;

    private static ContentCheckReport Check(string all) =>
        ContentCheck.Run(new MemoryContentSource([KeyValuePair.Create("pack.json", Pack), KeyValuePair.Create("all.json", all)]));

    private static ContentCheckReport CheckEdited(string from, string to)
    {
        Assert.Contains(from, Good, StringComparison.Ordinal);
        return Check(Good.Replace(from, to, StringComparison.Ordinal));
    }

    private static string[] Problems(ContentCheckReport report) => [.. report.Problems.Select(p => p.Path + ": " + p.Message)];

    [Fact]
    public void A_good_pack_passes_with_its_content()
    {
        var report = Check(Good);
        Assert.True(report.Passed, string.Join("\n", Problems(report)));
        Assert.NotNull(report.Content);
        Assert.Empty(report.Warnings);
    }

    [Fact]
    public void A_step_whose_choices_are_all_gated_is_a_dead_end_naming_conversation_and_step()
    {
        var report = CheckEdited("""{ "id": "mark", "text": "Mark",""", """{ "id": "mark", "text": "Mark", "when": [ { "type": "flag", "flag": "met" } ],""");
        var problem = Assert.Single(report.Problems, p => p.Message.Contains("dead end", StringComparison.Ordinal));
        Assert.StartsWith("all.json:/conversations/0/steps/0", problem.Path, StringComparison.Ordinal);
        Assert.Contains("'chat'", problem.Message, StringComparison.Ordinal);
        Assert.Contains("'hello'", problem.Message, StringComparison.Ordinal);
        Assert.Null(report.Content);
    }

    [Fact]
    public void A_hand_off_choice_does_not_count_as_the_fallback()
    {
        var report = CheckEdited(
            """{ "id": "mark", "text": "Mark", "effects": [ { "type": "setFlag", "flag": "met" } ], "next": [ { "step": "bye" } ] }""",
            """{ "id": "mark", "text": "Mark", "effects": [ { "type": "setFlag", "flag": "met" } ], "requires": [ { "type": "flag", "flag": "met" } ], "lockedText": "No.", "next": [ { "step": "bye" } ] }""");
        Assert.Contains(report.Problems, p => p.Message.Contains("dead end", StringComparison.Ordinal));
    }

    [Fact]
    public void A_flag_nothing_sets_fails_and_an_unread_flag_only_warns()
    {
        var typo = CheckEdited("""{ "type": "flag", "flag": "met" }""", """{ "type": "flag", "flag": "mett" }""");
        var problem = Assert.Single(typo.Problems);
        Assert.Contains("'mett'", problem.Message, StringComparison.Ordinal);
        Assert.StartsWith("all.json:/conversations/0/steps/0/choices/1/requires/0", problem.Path, StringComparison.Ordinal);
        Assert.Contains(typo.Warnings, w => w.Message.Contains("'met' is set here but nothing reads it", StringComparison.Ordinal));
    }

    [Fact]
    public void Unreachable_steps_and_unused_conversations_warn_without_failing()
    {
        var report = CheckEdited(
            """{ "id": "bye", "lines": [ { "speaker": "ann", "text": "Bye." } ] }""",
            """{ "id": "bye", "lines": [ { "speaker": "ann", "text": "Bye." } ] }, { "id": "orphan", "lines": [] }""");
        Assert.True(report.Passed);
        Assert.Contains(report.Warnings, w => w.Message.Contains("step 'orphan'", StringComparison.Ordinal));

        var unused = CheckEdited(""", "opens": [ { "conversation": "chat" } ]""", string.Empty);
        Assert.True(unused.Passed);
        Assert.Contains(unused.Warnings, w => w.Message.Contains("conversation 'chat' is never started", StringComparison.Ordinal));
    }

    [Fact]
    public void Starts_people_and_spots_off_the_ground_fail_with_their_location()
    {
        var start = CheckEdited("\"start\": [1, 1]", "\"start\": [10, 10]");
        Assert.Contains(start.Problems, p => p.Path == "all.json:/towns/0/start");

        var person = CheckEdited("\"at\": [15, 15]", "\"at\": [30, 30]");
        Assert.Contains(person.Problems, p => p.Path == "all.json:/towns/0/people/0/placements/0/at");

        var spot = CheckEdited("\"center\": [2, 2]", "\"center\": [50, 2]");
        Assert.Contains(spot.Problems, p => p.Path == "all.json:/towns/0/spots/0/area");
    }

    [Fact]
    public void Loader_problems_come_back_as_problems_with_no_content()
    {
        var report = CheckEdited("\"character\": \"ann\", \"placements\"", "\"character\": \"nobody\", \"placements\"");
        Assert.False(report.Passed);
        Assert.Null(report.Content);
        Assert.Contains(report.Problems, p => p.Message.Contains("nobody", StringComparison.Ordinal));
    }

    [Fact]
    public void A_locked_choice_with_nothing_to_show_and_empty_text_fail()
    {
        Assert.Empty(CheckEdited(""" "lockedText": "Not yet.",""", string.Empty).Problems);

        var blankDefault = Check(Good.Replace("\"startingTown\": \"square\"", "\"startingTown\": \"square\", \"conversations\": { \"lockedText\": \"\" }", StringComparison.Ordinal)
            .Replace(""" "lockedText": "Not yet.",""", string.Empty, StringComparison.Ordinal));
        Assert.Contains(blankDefault.Problems, p => p.Path == "all.json:/conversations/0/steps/0/choices/1" && p.Message.Contains("lockedText", StringComparison.Ordinal));

        var emptyLine = CheckEdited("\"text\": \"Hi.\"", "\"text\": \"  \"");
        Assert.Contains(emptyLine.Problems, p => p.Path == "all.json:/conversations/0/steps/0/lines/0/text");
    }

    [Fact]
    public void A_digit_in_conversation_text_warns_and_a_hidden_synergy_without_a_feel_line_warns()
    {
        var digit = CheckEdited("\"text\": \"Hi.\"", "\"text\": \"Hi 2.\"");
        Assert.True(digit.Passed);
        Assert.Contains(digit.Warnings, w => w.Path == "all.json:/conversations/0/steps/0/lines/0/text");

        var synergy = Good
            .Replace("\"startingTown\": \"square\"", "\"startingTown\": \"square\", \"display\": { \"synergyNumbers\": false }", StringComparison.Ordinal)
            .Replace("\"conversations\": [", "\"synergy\": { \"resolution\": \"bestOnly\", \"bonuses\": [ { \"name\": \"Bond\", \"rule\": \"alignment\", \"threshold\": 80, \"stat\": \"speed\", \"step\": \"multiply\", \"value\": 1.1 } ] }, \"conversations\": [", StringComparison.Ordinal);
        var report = Check(synergy);
        Assert.True(report.Passed, string.Join("\n", Problems(report)));
        Assert.Contains(report.Warnings, w => w.Message.Contains("feel line", StringComparison.Ordinal));
    }
}
