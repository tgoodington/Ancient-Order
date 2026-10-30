namespace AncientOrder.Tests;

/// <summary>
/// The pack for TownTriggerTests: one open 30 by 30 town with the avatar at 2,2, a person (ann) at 12,2 on the straight path, an interact spot
/// (bench) at 2,16 and a walk-over spot (mat) at 20 to 24 by 10 to 14. One small crate far from every route, so every walk is a short straight line.
/// Each opens its own conversation, which ends on "bye". The bench lists a never-holding entry first, so the second must be the one opened.
/// </summary>
public static class TownTriggerPacks
{
    public const string PersonConversation = "chat";
    public const string SpotConversation = "bench_talk";
    public const string WalkOverConversation = "mat_scene";

    public static Dictionary<string, string> Town() => new()
    {
        ["pack.json"] = """{ "format": "ancient-order-content", "version": 1, "id": "triggertests", "name": "Trigger Pack", "standIn": false, "files": ["all.json"] }""",
        ["all.json"] = """
            {
              "settings": {
                "playerName": "Rowan",
                "lineup": { "min": 1, "max": 2 },
                "startingParty": ["ann", "bo"],
                "playerCombat": { "rank": 3, "stamina": 100, "power": 80, "speed": 80, "elementalPath": "air" },
                "startingTown": "square"
              },
              "archetypes": [{ "id": "plain" }],
              "characters": [
                { "id": "ann", "name": "Ann", "archetypeId": "plain", "personality": { "patience": 20, "empathy": 20, "cunning": 10, "logic": 15, "kindness": 20, "charisma": 15 }, "startingAffection": 0, "startingTrust": 0, "combat": { "rank": 3, "stamina": 100, "power": 80, "speed": 80, "elementalPath": "fire" } },
                { "id": "bo", "name": "Bo", "archetypeId": "plain", "personality": { "patience": 20, "empathy": 20, "cunning": 10, "logic": 15, "kindness": 20, "charisma": 15 }, "startingAffection": 0, "startingTrust": 0, "combat": { "rank": 3, "stamina": 100, "power": 80, "speed": 80, "elementalPath": "earth" } }
              ],
              "conversations": [
                { "id": "chat", "participants": ["ann"], "start": [{ "step": "s" }], "steps": [ { "id": "s", "lines": [], "choices": [ { "id": "bye", "text": "Goodbye" } ] } ] },
                { "id": "bench_locked", "participants": ["ann"], "start": [{ "step": "s" }], "steps": [ { "id": "s", "lines": [], "choices": [ { "id": "bye", "text": "Goodbye", "effects": [ { "type": "setFlag", "flag": "never" } ]} ] } ] },
                { "id": "bench_talk", "participants": ["ann"], "start": [{ "step": "s" }], "steps": [ { "id": "s", "lines": [], "choices": [ { "id": "bye", "text": "Goodbye" } ] } ] },
                { "id": "mat_scene", "participants": ["ann"], "start": [{ "step": "s" }], "steps": [ { "id": "s", "lines": [], "choices": [ { "id": "bye", "text": "Goodbye" } ] } ] }
              ],
              "towns": [
                {
                  "id": "square", "name": "Market Square",
                  "ground": [ [[0, 0], [30, 0], [30, 30], [0, 30]] ],
                  "start": [2, 2],
                  "obstacles": [ { "id": "crate", "footprint": [[26, 24], [28, 24], [28, 26], [26, 26]], "height": 1 } ],
                  "people": [ { "character": "ann", "placements": [ { "at": [12, 2], "facing": 90 } ], "opens": [ { "conversation": "chat" } ] } ],
                  "spots": [
                    { "id": "bench", "area": { "circle": { "center": [2, 16], "radius": 1 } }, "trigger": "interact",
                      "opens": [ { "conversation": "bench_locked", "when": [ { "type": "flag", "flag": "never" } ] }, { "conversation": "bench_talk" } ] },
                    { "id": "mat", "area": { "polygon": [[20, 10], [24, 10], [24, 14], [20, 14]] }, "trigger": "walkOver",
                      "opens": [ { "conversation": "mat_scene" } ] }
                  ]
                }
              ]
            }
            """,
    };
}
