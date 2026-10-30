using AncientOrder.Rules.Content.Packs;

namespace AncientOrder.Rules.Tests.ConversationTesting;

/// <summary>
/// The conversation reference checks at content load, told with file places. The basic reference cases (unknown participant, speaker, step, character
/// in an effect, duplicate ids, null items, id lengths) are in ContentPacks/ConversationTests.cs and hand-off references in HandOff/HandOffTests.cs.
/// </summary>
public class ConversationLoadTests
{
    private const string Pack = """{ "format": "ancient-order-content", "version": 1, "id": "t", "name": "Test", "standIn": false, "files": ["base.json", "talk.json", "more.json"] }""";

    private const string Base = """
        {
          "settings": { "playerName": "Kael", "lineup": { "min": 1, "max": 1 } },
          "archetypes": [{ "id": "x" }],
          "characters": [
            { "id": "ann", "name": "Ann", "archetypeId": "x", "personality": { "patience": 20, "empathy": 20, "cunning": 10, "logic": 15, "kindness": 20, "charisma": 15 }, "startingAffection": 0, "startingTrust": 0 }
          ]
        }
        """;

    private static RuleErrorDetails Load(string talk, string more = "{}")
    {
        var loaded = ContentPackLoader.Load(new MemoryContentSource(
            [KeyValuePair.Create("pack.json", Pack), KeyValuePair.Create("base.json", Base), KeyValuePair.Create("talk.json", talk), KeyValuePair.Create("more.json", more)]));
        return new RuleErrorDetails(loaded.IsOk, loaded.IsOk ? [] : [.. loaded.Error.Details.Select(d => (d.Path, d.Message))]);
    }

    private sealed record RuleErrorDetails(bool Ok, (string Path, string Message)[] Problems);

    private static string One(string talk)
    {
        var result = Load(talk);
        Assert.False(result.Ok);
        return Assert.Single(result.Problems).Path;
    }

    [Fact]
    public void A_conversation_with_no_steps_is_refused_at_its_steps()
    {
        Assert.Equal("talk.json:/conversations/0/steps", One("""{ "conversations": [ { "id": "c", "participants": [], "steps": [] } ] }"""));
    }

    [Fact]
    public void A_conversation_id_defined_in_two_files_is_refused_naming_the_second_place_and_the_first()
    {
        const string one = """{ "conversations": [ { "id": "c", "participants": [], "steps": [ { "id": "a", "lines": [] } ] } ] }""";
        var result = Load(one, one);

        Assert.False(result.Ok);
        var problem = Assert.Single(result.Problems);
        Assert.Equal("more.json:/conversations/0/id", problem.Path);
        Assert.Contains("duplicate conversation id: c", problem.Message, StringComparison.Ordinal);
        Assert.Contains("talk.json:/conversations/0/id", problem.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_conversation_in_another_file_can_be_a_destination()
    {
        const string talk = """{ "conversations": [ { "id": "c", "participants": [], "steps": [ { "id": "a", "lines": [], "next": [ { "conversation": "d" } ] } ] } ] }""";
        const string more = """{ "conversations": [ { "id": "d", "participants": [], "steps": [ { "id": "b", "lines": [] } ] } ] }""";

        Assert.True(Load(talk, more).Ok);
    }

    [Theory]
    [InlineData("""{ "type": "seen", "conversation": "ghost" }""", "/conversation", "unknown conversation: ghost")]
    [InlineData("""{ "type": "chose", "conversation": "c", "choice": "ghost" }""", "/choice", "unknown choice in conversation c: ghost")]
    [InlineData("""{ "type": "inParty", "character": "ghost" }""", "/character", "unknown character: ghost")]
    [InlineData("""{ "type": "not", "condition": { "type": "inParty", "character": "ghost" } }""", "/condition/character", "unknown character: ghost")]
    public void Conditions_in_every_gate_of_a_conversation_are_checked_against_content(string condition, string tail, string message)
    {
        var gated = new (string Where, string Path)[]
        {
            ("""{ "id": "c", "participants": [], "start": [ { "step": "a", "when": [ COND ] } ], "steps": [ { "id": "a", "lines": [] } ] }""", "start/0/when/0"),
            ("""{ "id": "c", "participants": [], "steps": [ { "id": "a", "lines": [ { "speaker": "narrator", "text": "x", "when": [ COND ] } ] } ] }""", "steps/0/lines/0/when/0"),
            ("""{ "id": "c", "participants": [], "steps": [ { "id": "a", "lines": [], "choices": [ { "id": "k", "text": "t", "when": [ COND ] } ] } ] }""", "steps/0/choices/0/when/0"),
            ("""{ "id": "c", "participants": [], "steps": [ { "id": "a", "lines": [], "choices": [ { "id": "k", "text": "t", "requires": [ COND ] } ] } ] }""", "steps/0/choices/0/requires/0"),
            ("""{ "id": "c", "participants": [], "steps": [ { "id": "a", "lines": [], "next": [ { "step": "a", "when": [ COND ] } ] } ] }""", "steps/0/next/0/when/0"),
            ("""{ "id": "c", "participants": [], "steps": [ { "id": "a", "lines": [], "choices": [ { "id": "k", "text": "t", "next": [ { "step": "a", "when": [ COND ] } ] } ] } ] }""", "steps/0/choices/0/next/0/when/0"),
        };

        foreach (var (where, path) in gated)
        {
            var talk = "{ \"conversations\": [ " + where.Replace("COND", condition, StringComparison.Ordinal) + " ] }";
            var result = Load(talk);
            Assert.False(result.Ok);
            var problem = Assert.Single(result.Problems);
            Assert.Equal("talk.json:/conversations/0/" + path + tail, problem.Path);
            Assert.Contains(message, problem.Message, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void A_reaction_must_name_a_participant_and_say_something_or_stay_silent()
    {
        const string talk = """
            { "conversations": [ { "id": "c", "participants": ["ann"], "steps": [ { "id": "a", "lines": [], "choices": [
              { "id": "k", "text": "t", "reactions": [ { "character": "ghost", "text": "Hm." }, { "character": "ann" }, { "character": "ann", "silence": true } ] }
            ] } ] } ] }
            """;

        var paths = Load(talk).Problems.Select(p => p.Path).ToArray();

        Assert.Equal(
            [
                "talk.json:/conversations/0/steps/0/choices/0/reactions/0/character",
                "talk.json:/conversations/0/steps/0/choices/0/reactions/1/text",
                "talk.json:/conversations/0/steps/0/choices/0/reactions/2/character",
            ],
            paths);
    }
}
