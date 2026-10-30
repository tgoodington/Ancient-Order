namespace AncientOrder.Tests;

/// <summary>A pack for the hand-off screen tests: ann (talks, her final choice hands off), bo, and an exit spot that hands off. Texts hold no digits.</summary>
public static class HandOffPacks
{
    public const string FightName = "The Brawl";
    public const double AnnX = 10;
    public const double AnnZ = 10;
    public const double ExitX = 15;
    public const double ExitZ = 15;

    public static Dictionary<string, string> Town() => new()
    {
        ["pack.json"] = """{ "format": "ancient-order-content", "version": 1, "id": "handofftests", "name": "Hand Off Pack", "standIn": false, "files": ["all.json"] }""",
        ["all.json"] = $$"""
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
                { "id": "talk", "participants": ["ann"], "start": [{ "step": "hello" }],
                  "steps": [ { "id": "hello", "lines": [ { "speaker": "ann", "text": "Ready to go?" } ], "choices": [
                    { "id": "go", "text": "Lead the way", "effects": [ { "type": "handOff", "fight": "brawl" } ] },
                    { "id": "later", "text": "Not now" }
                  ] } ] }
              ],
              "fights": [
                { "id": "brawl", "name": "{{FightName}}", "enemies": [ { "id": "rat", "name": "Rat", "archetype": "plain", "rank": 1, "stamina": 10, "power": 5, "speed": 5, "elementalPath": "fire", "reactionSkills": { "block": { "sr": 1, "smr": 1, "fmr": 1 }, "dodge": { "sr": 1, "smr": 1, "fmr": 1 }, "parry": { "sr": 1, "smr": 1, "fmr": 1 } } } ] }
              ],
              "towns": [
                {
                  "id": "square", "name": "Market Square",
                  "ground": [ [[0, 0], [20, 0], [20, 20], [0, 20]] ],
                  "start": [3, 4],
                  "people": [ { "character": "ann", "placements": [ { "at": [10, 10], "facing": 90 } ], "opens": [ { "conversation": "talk" } ] } ],
                  "spots": [
                    { "id": "exit", "area": { "circle": { "center": [15, 15], "radius": 1 } }, "trigger": "interact", "opens": [ { "handOff": "brawl" } ] }
                  ]
                }
              ]
            }
            """,
    };
}
