namespace AncientOrder.Tests;

/// <summary>
/// Small in-memory content packs for screen tests, as file name to JSON text, ready for MemoryContentSource. Everything the player can read
/// (names, titles) is free of digits so the no-numbers guards mean something. Ids and coordinates are not text and may hold digits.
/// Add a builder here when a test needs content these do not have; keep it passing the content check unless it is built to fail.
/// </summary>
public static class TestPacks
{
    public const string PackName = "Test Pack";
    public const string PlayerName = "Rowan";
    public const string TownName = "Market Square";
    public const double StartX = 3;
    public const double StartZ = 4;

    /// <summary>A passing pack: two party members (ann, bo), one town ("square", start 3,4, ground 0 to 20 on both axes, ann standing at 10,10).</summary>
    public static Dictionary<string, string> Town() => Build("\"startingTown\": \"square\"");

    /// <summary>A pack the content check refuses: the starting town does not exist.</summary>
    public static Dictionary<string, string> UnknownStartingTown() => Build("\"startingTown\": \"nowhere\"");

    private static Dictionary<string, string> Build(string startingTown) => new()
    {
        ["pack.json"] = $$"""{ "format": "ancient-order-content", "version": 1, "id": "screentests", "name": "{{PackName}}", "standIn": false, "files": ["all.json"] }""",
        ["all.json"] = $$"""
            {
              "settings": {
                "playerName": "{{PlayerName}}",
                "lineup": { "min": 1, "max": 2 },
                "startingParty": ["ann", "bo"],
                "playerCombat": { "rank": 3, "stamina": 100, "power": 80, "speed": 80, "elementalPath": "air" },
                {{startingTown}}
              },
              "archetypes": [{ "id": "plain" }],
              "characters": [
                { "id": "ann", "name": "Ann", "archetypeId": "plain", "personality": { "patience": 20, "empathy": 20, "cunning": 10, "logic": 15, "kindness": 20, "charisma": 15 }, "startingAffection": 0, "startingTrust": 0, "combat": { "rank": 3, "stamina": 100, "power": 80, "speed": 80, "elementalPath": "fire" } },
                { "id": "bo", "name": "Bo", "archetypeId": "plain", "personality": { "patience": 20, "empathy": 20, "cunning": 10, "logic": 15, "kindness": 20, "charisma": 15 }, "startingAffection": 0, "startingTrust": 0, "combat": { "rank": 3, "stamina": 100, "power": 80, "speed": 80, "elementalPath": "earth" } }
              ],
              "towns": [
                {
                  "id": "square", "name": "{{TownName}}",
                  "ground": [ [[0, 0], [20, 0], [20, 20], [0, 20]] ],
                  "start": [3, 4],
                  "people": [ { "character": "ann", "placements": [ { "at": [10, 10], "facing": 90 } ] } ]
                }
              ]
            }
            """,
    };
}
