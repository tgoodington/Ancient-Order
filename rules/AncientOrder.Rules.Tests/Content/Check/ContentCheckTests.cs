using AncientOrder.Rules.Content.Check;
using AncientOrder.Rules.Content.Packs;

namespace AncientOrder.Rules.Tests.CheckKinds;

public class ContentCheckTests
{
    private const string Index = """{ "format": "ancient-order-content", "version": 1, "id": "t", "name": "Test", "standIn": false, "files": ["all.json"] }""";

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
              "start": [1, 1],
              "people": [ { "character": "ann", "placements": [ { "at": [15, 15] } ], "opens": [ { "conversation": "chat" } ] } ],
              "spots": [ { "id": "well", "area": { "circle": { "center": [2, 2], "radius": 1 } }, "trigger": "interact" } ] }
          ]
        }
        """;

    private static ContentCheckReport Check(string all) =>
        ContentCheck.Run(new MemoryContentSource([KeyValuePair.Create("pack.json", Index), KeyValuePair.Create("all.json", all)]));

    private static ContentCheckReport CheckEdited(string from, string to)
    {
        Assert.Contains(from, Good, StringComparison.Ordinal);
        return Check(Good.Replace(from, to, StringComparison.Ordinal));
    }

    [Fact]
    public void The_untouched_pack_passes_with_no_problems_and_no_warnings()
    {
        var report = Check(Good);
        Assert.True(report.Passed, string.Join("\n", report.Problems.Select(p => p.Path + ": " + p.Message)));
        Assert.NotNull(report.Content);
        Assert.Empty(report.Warnings);
    }

    [Fact]
    public void Problem_dead_end_when_every_choice_of_a_step_is_gated()
    {
        var report = CheckEdited("""{ "id": "mark", "text": "Mark",""", """{ "id": "mark", "text": "Mark", "when": [ { "type": "flag", "flag": "met" } ],""");
        Assert.False(report.Passed);
        var problem = Assert.Single(report.Problems, p => p.Message.Contains("dead end", StringComparison.Ordinal));
        Assert.StartsWith("all.json:/conversations/0/steps/0", problem.Path, StringComparison.Ordinal);
    }

    [Fact]
    public void Problem_start_off_the_ground()
    {
        var report = CheckEdited("\"start\": [1, 1]", "\"start\": [10, 30]");
        Assert.False(report.Passed);
        Assert.Contains(report.Problems, p => p.Path == "all.json:/towns/0/start");
    }

    [Fact]
    public void Problem_person_placed_off_the_ground()
    {
        var report = CheckEdited("\"at\": [15, 15]", "\"at\": [30, 30]");
        Assert.False(report.Passed);
        Assert.Contains(report.Problems, p => p.Path == "all.json:/towns/0/people/0/placements/0/at");
    }

    [Fact]
    public void Problem_spot_area_off_the_ground()
    {
        var report = CheckEdited("\"center\": [2, 2]", "\"center\": [50, 2]");
        Assert.False(report.Passed);
        Assert.Contains(report.Problems, p => p.Path == "all.json:/towns/0/spots/0/area");
    }

    [Fact]
    public void Problem_flag_that_nothing_sets()
    {
        var report = CheckEdited("""{ "type": "flag", "flag": "met" }""", """{ "type": "flag", "flag": "mett" }""");
        Assert.False(report.Passed);
        var problem = Assert.Single(report.Problems);
        Assert.Equal("all.json:/conversations/0/steps/0/choices/1/requires/0", problem.Path);
        Assert.Contains("'mett'", problem.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Problem_locked_choice_with_no_line_to_show()
    {
        var report = Check(Good
            .Replace("\"startingTown\": \"square\"", "\"startingTown\": \"square\", \"conversations\": { \"lockedText\": \"\" }", StringComparison.Ordinal)
            .Replace(""" "lockedText": "Not yet.",""", string.Empty, StringComparison.Ordinal));
        Assert.False(report.Passed);
        Assert.Contains(report.Problems, p => p.Path == "all.json:/conversations/0/steps/0/choices/1" && p.Message.Contains("lockedText", StringComparison.Ordinal));
    }

    [Fact]
    public void A_locked_choice_is_fine_when_the_pack_supplies_a_default_line()
    {
        var report = CheckEdited(""" "lockedText": "Not yet.",""", string.Empty);
        Assert.True(report.Passed, string.Join("\n", report.Problems.Select(p => p.Message)));
    }

    [Fact]
    public void Problem_empty_player_facing_text()
    {
        var report = CheckEdited("\"text\": \"Hi.\"", "\"text\": \"  \"");
        Assert.False(report.Passed);
        Assert.Contains(report.Problems, p => p.Path == "all.json:/conversations/0/steps/0/lines/0/text");
    }

    [Fact]
    public void Problem_empty_choice_text()
    {
        var report = CheckEdited("\"text\": \"Mark\"", "\"text\": \"\"");
        Assert.False(report.Passed);
        Assert.Contains(report.Problems, p => p.Path == "all.json:/conversations/0/steps/0/choices/0/text");
    }

    [Fact]
    public void Warning_step_that_cannot_be_reached()
    {
        var report = CheckEdited("""{ "id": "bye", "lines": [ { "speaker": "ann", "text": "Bye." } ] }""", """{ "id": "bye", "lines": [ { "speaker": "ann", "text": "Bye." } ] }, { "id": "orphan", "lines": [] }""");
        Assert.True(report.Passed);
        var warning = Assert.Single(report.Warnings);
        Assert.Equal("all.json:/conversations/0/steps/2", warning.Path);
        Assert.Contains("step 'orphan'", warning.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Warning_conversation_that_nothing_starts()
    {
        var report = CheckEdited(""", "opens": [ { "conversation": "chat" } ]""", string.Empty);
        Assert.True(report.Passed);
        var warning = Assert.Single(report.Warnings);
        Assert.Equal("all.json:/conversations/0", warning.Path);
        Assert.Contains("conversation 'chat' is never started", warning.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Warning_flag_that_is_set_but_never_read()
    {
        var report = CheckEdited("""{ "type": "flag", "flag": "met" }""", """{ "type": "seen", "conversation": "chat" }""");
        Assert.True(report.Passed, string.Join("\n", report.Problems.Select(p => p.Message)));
        var warning = Assert.Single(report.Warnings);
        Assert.Contains("'met' is set here but nothing reads it", warning.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Warning_synergy_bonus_the_player_cannot_see()
    {
        var report = Check(Good
            .Replace("\"startingTown\": \"square\"", "\"startingTown\": \"square\", \"display\": { \"synergyNumbers\": false }", StringComparison.Ordinal)
            .Replace("\"conversations\": [", "\"synergy\": { \"resolution\": \"bestOnly\", \"bonuses\": [ { \"name\": \"Bond\", \"rule\": \"alignment\", \"threshold\": 80, \"stat\": \"speed\", \"step\": \"multiply\", \"value\": 1.1 } ] }, \"conversations\": [", StringComparison.Ordinal));
        Assert.True(report.Passed, string.Join("\n", report.Problems.Select(p => p.Message)));
        Assert.Contains(report.Warnings, w => w.Message.Contains("feel line", StringComparison.Ordinal));
    }

    [Fact]
    public void Warning_digit_in_conversation_text()
    {
        var report = CheckEdited("\"text\": \"Hi.\"", "\"text\": \"Hi 2.\"");
        Assert.True(report.Passed);
        Assert.Equal("all.json:/conversations/0/steps/0/lines/0/text", Assert.Single(report.Warnings).Path);
    }

    [Fact]
    public void A_pack_the_loader_refuses_fails_the_check_with_no_content_and_the_loaders_locations()
    {
        var report = Check("{ \"settings\": \n }");
        Assert.False(report.Passed);
        Assert.Null(report.Content);
        Assert.Contains(report.Problems, p => p.Path.StartsWith("all.json:", StringComparison.Ordinal));
    }
}
