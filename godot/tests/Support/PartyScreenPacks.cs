namespace AncientOrder.Tests;

/// <summary>
/// In-memory packs for the party screen tests (T32). Party in join order is bo, cy, ann (cy has no combat stats); dee exists but is not in the party.
/// Lineup limits are 2 to 2. Affection and trust are 37 and 53, values that no personality, combat, or limit number shares, so a guard can look for them.
/// Names and readable text carry no digits. One conversation, "talk", exists to lock the lineup.
/// </summary>
public static class PartyScreenPacks
{
    public const string AffectionText = "37";
    public const string TrustText = "53";
    public const string Feel = "You move as one.";
    public const string ConversationId = "talk";

    public static Dictionary<string, string> Standard(bool readouts = true, bool numbers = true, int lineupMin = 2, int lineupMax = 2) => Build("\"bo\", \"cy\", \"ann\"", readouts, numbers, lineupMin, lineupMax);

    public static Dictionary<string, string> EmptyParty() => Build(string.Empty, true, true, 2, 2);

    private static string Person(string id, string name, string combat) =>
        $$"""{ "id": "{{id}}", "name": "{{name}}", "archetypeId": "plain", "personality": { "patience": 20, "empathy": 20, "cunning": 10, "logic": 15, "kindness": 20, "charisma": 15 }, "startingAffection": {{AffectionText}}, "startingTrust": {{TrustText}}{{combat}} }""";

    private static string Combat(string path) =>
        $$""", "combat": { "rank": 3, "stamina": 110, "power": 80, "speed": 70, "elementalPath": "{{path}}" }""";

    private static Dictionary<string, string> Build(string party, bool readouts, bool numbers, int lineupMin, int lineupMax) => new()
    {
        ["pack.json"] = """{ "format": "ancient-order-content", "version": 1, "id": "partytests", "name": "Party Tests", "standIn": false, "files": ["all.json"] }""",
        ["all.json"] = $$"""
            {
              "settings": {
                "playerName": "Rowan",
                "lineup": { "min": {{lineupMin}}, "max": {{lineupMax}} },
                "startingParty": [ {{party}} ],
                "display": { "partyReadouts": {{(readouts ? "true" : "false")}}, "synergyNumbers": {{(numbers ? "true" : "false")}} },
                "playerCombat": { "rank": 3, "stamina": 100, "power": 60, "speed": 60, "elementalPath": "air" },
                "startingTown": "square"
              },
              "archetypes": [{ "id": "plain" }],
              "characters": [
                {{Person("ann", "Ann", Combat("fire"))}},
                {{Person("bo", "Bo", Combat("earth"))}},
                {{Person("cy", "Cy", string.Empty)}},
                {{Person("dee", "Dee", Combat("water"))}}
              ],
              "synergy": { "resolution": "allThatQualify", "bonuses": [ { "name": "Any", "rule": "coverage", "threshold": 1, "stat": "power", "step": "multiply", "value": 1.1, "feel": "{{Feel}}" } ] },
              "towns": [
                { "id": "square", "name": "Market Square", "ground": [ [[0, 0], [20, 0], [20, 20], [0, 20]] ], "start": [3, 4], "people": [] }
              ],
              "conversations": [
                { "id": "{{ConversationId}}", "title": "Talk", "participants": [], "steps": [ { "id": "s", "lines": [], "choices": [ { "id": "x", "text": "Go on" } ] } ] }
              ]
            }
            """,
    };
}
