namespace AncientOrder.Tests;

/// <summary>
/// A pack for reaction tests: a kind (ann, warm), a sly (bo, cold) and a plain (cy, wary) person standing in the town, a kind di who is only a participant, and
/// an unmet kind person (ed) in the town at the neutral point. Names and text are free of digits.
/// </summary>
public static class ReactionPacks
{
    public static Dictionary<string, string> Town() => new()
    {
        ["pack.json"] = """{ "format": "ancient-order-content", "version": 1, "id": "reactiontests", "name": "Reaction Pack", "standIn": false, "files": ["all.json"] }""",
        ["all.json"] = """
            {
              "settings": {
                "playerName": "Rowan",
                "lineup": { "min": 1, "max": 2 },
                "startingParty": ["ann"],
                "playerCombat": { "rank": 3, "stamina": 100, "power": 80, "speed": 80, "elementalPath": "air" },
                "startingTown": "square"
              },
              "archetypes": [
                { "id": "kind", "reactions": { "leanings": { "kindness": 1 }, "minimum": 2, "affectionPerPoint": 0.5, "trustPerPoint": 0.25,
                  "approve": { "clear": "{name} smiles.", "slight": "{name} nods.", "strong": "{name} beams." },
                  "disapprove": { "clear": "{name} frowns." } } },
                { "id": "sly", "reactions": { "leanings": { "kindness": -1 }, "minimum": 2, "affectionPerPoint": 0.5, "trustPerPoint": 0.25,
                  "approve": { "clear": "{name} grins." },
                  "disapprove": { "clear": "{name} sneers.", "strong": "{name} recoils." } } },
                { "id": "plain" }
              ],
              "characters": [
                { "id": "ann", "name": "Ann", "archetypeId": "kind", "personality": { "patience": 17, "empathy": 17, "cunning": 17, "logic": 17, "kindness": 15, "charisma": 17 }, "startingAffection": 40, "startingTrust": 30, "combat": { "rank": 3, "stamina": 100, "power": 80, "speed": 80, "elementalPath": "fire" } },
                { "id": "bo", "name": "Bo", "archetypeId": "sly", "personality": { "patience": 17, "empathy": 17, "cunning": 17, "logic": 17, "kindness": 15, "charisma": 17 }, "startingAffection": -40, "startingTrust": -40, "combat": { "rank": 3, "stamina": 100, "power": 80, "speed": 80, "elementalPath": "earth" } },
                { "id": "cy", "name": "Cy", "archetypeId": "plain", "personality": { "patience": 17, "empathy": 17, "cunning": 17, "logic": 17, "kindness": 15, "charisma": 17 }, "startingAffection": -15, "startingTrust": -10, "combat": { "rank": 3, "stamina": 100, "power": 80, "speed": 80, "elementalPath": "water" } },
                { "id": "di", "name": "Di", "archetypeId": "kind", "personality": { "patience": 17, "empathy": 17, "cunning": 17, "logic": 17, "kindness": 15, "charisma": 17 }, "startingAffection": 0, "startingTrust": 0, "combat": { "rank": 3, "stamina": 100, "power": 80, "speed": 80, "elementalPath": "air" } },
                { "id": "ed", "name": "Ed", "archetypeId": "kind", "personality": { "patience": 17, "empathy": 17, "cunning": 17, "logic": 17, "kindness": 15, "charisma": 17 }, "startingAffection": 0, "startingTrust": 0, "combat": { "rank": 3, "stamina": 100, "power": 80, "speed": 80, "elementalPath": "fire" } }
              ],
              "conversations": [
                { "id": "chat", "participants": ["ann", "bo", "cy", "di"], "start": [ { "step": "s" } ],
                  "steps": [ { "id": "s", "lines": [],
                    "choices": [
                      { "id": "nice", "text": "Be kind", "effects": [ { "type": "shift", "kindness": 10 } ] },
                      { "id": "huge", "text": "Be very kind", "effects": [ { "type": "shift", "kindness": 20 } ] },
                      { "id": "small", "text": "Be a little kind", "effects": [ { "type": "shift", "kindness": 4 } ] },
                      { "id": "hush", "text": "Say nothing", "reactions": [ { "character": "cy", "text": "Cy watches you." }, { "character": "ann", "silence": true } ] }
                    ] } ] }
              ],
              "towns": [
                {
                  "id": "square", "name": "Market Square",
                  "ground": [ [[0, 0], [20, 0], [20, 20], [0, 20]] ],
                  "start": [3, 4],
                  "people": [
                    { "character": "ann", "placements": [ { "at": [10, 10], "facing": 90 } ] },
                    { "character": "bo", "placements": [ { "at": [12, 10], "facing": 90 } ] },
                    { "character": "cy", "placements": [ { "at": [14, 10], "facing": 90 } ] },
                    { "character": "ed", "placements": [ { "at": [16, 10], "facing": 90 } ] }
                  ]
                }
              ]
            }
            """,
    };
}
