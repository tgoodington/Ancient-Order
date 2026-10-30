namespace AncientOrder.Tests;

/// <summary>
/// The pack for the town stage tests: a 40 by 40 square with a long wall (x 18 to 22, z 0 to 30) and a crate, so a walk from the start
/// has to go around. Ann always stands at 10,10; Bo appears at 30,10 once the flag "met" is set (conversation "chat", choice "mark").
/// An interact spot "post" sits at 30,35. Names hold no digits.
/// </summary>
public static class TownStagePacks
{
    public const string TownId = "square";

    public static Dictionary<string, string> Walled() => new()
    {
        ["pack.json"] = """{ "format": "ancient-order-content", "version": 1, "id": "townstage", "name": "Stage Pack", "standIn": false, "files": ["all.json"] }""",
        ["all.json"] = """
            {
              "settings": { "playerName": "Rowan", "lineup": { "min": 1, "max": 1 }, "startingTown": "square" },
              "archetypes": [{ "id": "plain" }],
              "characters": [
                { "id": "ann", "name": "Ann", "archetypeId": "plain", "personality": { "patience": 20, "empathy": 20, "cunning": 10, "logic": 15, "kindness": 20, "charisma": 15 }, "startingAffection": 0, "startingTrust": 0 },
                { "id": "bo", "name": "Bo", "archetypeId": "plain", "personality": { "patience": 20, "empathy": 20, "cunning": 10, "logic": 15, "kindness": 20, "charisma": 15 }, "startingAffection": 0, "startingTrust": 0 }
              ],
              "conversations": [
                { "id": "chat", "participants": ["ann"], "start": [{ "step": "hello" }],
                  "steps": [ { "id": "hello", "lines": [], "choices": [
                    { "id": "mark", "text": "Mark", "effects": [ { "type": "setFlag", "flag": "met" } ] }
                  ] } ] }
              ],
              "towns": [
                {
                  "id": "square", "name": "Market Square",
                  "ground": [ [[0, 0], [40, 0], [40, 40], [0, 40]] ],
                  "obstacles": [
                    { "id": "wall", "footprint": [[18, 0], [22, 0], [22, 30], [18, 30]], "height": 3, "label": "Wall" },
                    { "id": "crate", "footprint": [[8, 20], [10, 20], [10, 22], [8, 22]], "height": 1 }
                  ],
                  "start": [2, 2],
                  "people": [
                    { "character": "ann", "placements": [ { "at": [10, 10], "facing": 90 } ] },
                    { "character": "bo", "placements": [ { "at": [30, 10], "when": [ { "type": "flag", "flag": "met" } ] } ] }
                  ],
                  "spots": [
                    { "id": "post", "name": "Post", "area": { "circle": { "center": [30, 35], "radius": 1 } }, "trigger": "interact" }
                  ]
                }
              ]
            }
            """,
    };
}
