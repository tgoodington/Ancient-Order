namespace AncientOrder.Tests;

/// <summary>
/// A passing in-memory pack for conversation screen tests: TestPacks.Town plus two conversations, "chat" (one-on-one with Ann, three choices, a Continue step), "five" (a line and five choices), "long" (many choices) and
/// "scene" (a narrated scene with the narrator, Ann and Bo, played with Continue). Text is free of digits. One line holds BBCode-like markup that must show literally.
/// </summary>
public static class ConversationPacks
{
    public const string MarkupText = "Rain taps the roof [b]softly[/b].";

    private static readonly string ManyChoices = string.Join(", ", Enumerable.Range(0, 30).Select(i => $$"""{ "id": "o{{(char)('a' + i % 26)}}{{(char)('a' + i / 26)}}", "text": "Option {{(char)('A' + i % 26)}}{{(char)('A' + i / 26)}}" }"""));

    public static Dictionary<string, string> Talks()
    {
        var pack = TestPacks.Town();
        pack["pack.json"] = pack["pack.json"].Replace("\"files\": [\"all.json\"]", "\"files\": [\"all.json\", \"talk.json\"]", StringComparison.Ordinal);
        pack["talk.json"] = $$"""
            {
              "conversations": [
                {
                  "id": "chat",
                  "title": "A Quiet Word",
                  "participants": ["ann"],
                  "steps": [
                    { "id": "hello",
                      "lines": [
                        { "speaker": "ann", "text": "Welcome, traveler." },
                        { "speaker": "player", "text": "Thank you." },
                        { "speaker": "narrator", "text": "{{MarkupText}}" }
                      ],
                      "choices": [
                        { "id": "kind", "text": "Be kind", "next": [ { "step": "more" } ] },
                        { "id": "ask", "text": "Ask a question", "next": [ { "step": "more" } ] },
                        { "id": "bye", "text": "Say goodbye" }
                      ] },
                    { "id": "more", "lines": [ { "speaker": "ann", "text": "Of course." } ] }
                  ]
                },
                {
                  "id": "long",
                  "participants": ["ann"],
                  "steps": [
                    { "id": "start", "lines": [ { "speaker": "ann", "text": "Ask away." } ], "choices": [ { "id": "go", "text": "Go on", "next": [ { "step": "top" } ] } ] },
                    { "id": "top",
                      "lines": [ { "speaker": "ann", "text": "Where do I begin." } ],
                      "choices": [ {{ManyChoices}} ] }
                  ]
                },
                {
                  "id": "five",
                  "participants": ["ann"],
                  "steps": [
                    { "id": "ask",
                      "lines": [ { "speaker": "ann", "text": "Ask away." } ],
                      "choices": [
                        { "id": "a", "text": "First option" }, { "id": "b", "text": "Second option" },
                        { "id": "c", "text": "Third option" }, { "id": "d", "text": "Fourth option" },
                        { "id": "e", "text": "Fifth option" }
                      ] }
                  ]
                },
                {
                  "id": "scene",
                  "title": "Nightfall",
                  "participants": ["ann", "bo"],
                  "steps": [
                    { "id": "s1",
                      "lines": [
                        { "speaker": "narrator", "text": "Night falls on the square." },
                        { "speaker": "ann", "text": "It is cold." },
                        { "speaker": "bo", "text": "Very." }
                      ],
                      "next": [ { "step": "s2" } ] },
                    { "id": "s2", "lines": [ { "speaker": "narrator", "text": "Dawn comes." } ] }
                  ]
                }
              ]
            }
            """;
        return pack;
    }
}
