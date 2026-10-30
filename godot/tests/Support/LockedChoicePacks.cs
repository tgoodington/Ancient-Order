namespace AncientOrder.Tests;

/// <summary>
/// A pack for the locked-choice screen tests: the Town pack's shape plus one conversation ("talk") with an open shift choice, a choice locked on cunning
/// with its own line, and one locked with the default line. All player-readable text is free of digits so the no-numbers guard means something.
/// </summary>
public static class LockedChoicePacks
{
    public const string OwnLockedLine = "You lack the nerve.";
    public const string DefaultLockedLine = "That is not open to you yet.";

    /// <summary>Cunning starts at 10 and the open choice shifts it up 8 (kindness down 8), so the gates at 34 and 35 stay shut after it.</summary>
    public static Dictionary<string, string> Talk() => new()
    {
        ["pack.json"] = """{ "format": "ancient-order-content", "version": 1, "id": "lockedtests", "name": "Locked Pack", "standIn": false, "files": ["all.json"] }""",
        ["all.json"] = """
            {
              "settings": {
                "playerName": "Rowan",
                "lineup": { "min": 1, "max": 2 },
                "startingParty": ["ann"],
                "startingPersonality": { "patience": 20, "empathy": 20, "cunning": 10, "logic": 15, "kindness": 20, "charisma": 15 },
                "playerCombat": { "rank": 3, "stamina": 100, "power": 80, "speed": 80, "elementalPath": "air" },
                "startingTown": "square"
              },
              "archetypes": [{ "id": "plain" }],
              "characters": [
                { "id": "ann", "name": "Ann", "archetypeId": "plain", "personality": { "patience": 20, "empathy": 20, "cunning": 10, "logic": 15, "kindness": 20, "charisma": 15 }, "startingAffection": 0, "startingTrust": 0, "combat": { "rank": 3, "stamina": 100, "power": 80, "speed": 80, "elementalPath": "fire" } }
              ],
              "towns": [
                { "id": "square", "name": "Market Square", "ground": [ [[0, 0], [20, 0], [20, 20], [0, 20]] ], "start": [3, 4], "people": [ { "character": "ann", "placements": [ { "at": [10, 10], "facing": 90 } ] } ] }
              ],
              "conversations": [
                { "id": "talk", "title": "A Hard Bargain", "participants": [], "start": [ { "step": "bargain" } ],
                  "steps": [
                    { "id": "bargain",
                      "lines": [ { "speaker": "narrator", "text": "The merchant folds his arms." } ],
                      "choices": [
                        { "id": "bold", "text": "Press hard", "effects": [ { "type": "shift", "cunning": 8, "kindness": -8 } ], "next": [ { "step": "after" } ] },
                        { "id": "outwit", "text": "Outwit him", "lockedText": "You lack the nerve.", "requires": [ { "type": "trait", "trait": "cunning", "atLeast": 35 } ] },
                        { "id": "charm", "text": "Charm him", "requires": [ { "type": "trait", "trait": "cunning", "atLeast": 34 } ] }
                      ] },
                    { "id": "after",
                      "lines": [ { "speaker": "narrator", "text": "He lowers his price." } ],
                      "choices": [ { "id": "leave", "text": "Leave" } ] }
                  ] }
              ]
            }
            """,
    };

    /// <summary>Talk with the third choice open too (its gate is met from the start), so the locked one sits between two open ones.</summary>
    public static Dictionary<string, string> TalkLockedMiddle()
    {
        var pack = Talk();
        pack["all.json"] = pack["all.json"].Replace("\"atLeast\": 34", "\"atLeast\": 10", StringComparison.Ordinal);
        return pack;
    }
}
