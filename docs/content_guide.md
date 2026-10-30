# Content Guide

How to turn the designer's creative decisions into content-pack files. Written for Claude as the converter (ADR-062): the designer decides what the story, people, towns, and fights are; Claude writes the files; the content check proves them; the designer plays them.

This guide describes the format as the C# builds it. The records in `rules/AncientOrder.Rules/Content/Packs/Format/` are authoritative. Every example is copied from the stand-in pack in `godot/content/standin/`, which passes the check with 0 problems and 0 warnings. Examples carry STAND-IN wording on purpose; real content replaces it.

Commands:

- Check one or all packs: `dotnet run --project tools/AncientOrder.ContentCheck` (no arguments checks every `godot/content/*/` folder that holds a `pack.json`; an argument is a pack folder, e.g. `dotnet run --project tools/AncientOrder.ContentCheck -- godot/content/standin`).
- Everything (format, build, release build, all tests, content check, headless smoke): `pwsh -File ./verify.ps1`.

---

## 1. The hand-off loop

1. The designer hands over decisions in any form (notes, a list, a rough scene, a spoken outline).
2. Claude writes or edits pack files under `godot/content/<pack>/`. Systems code is not touched for a content change.
3. Claude runs the content check and fixes every problem (section 6). Warnings are read and either fixed or reported to the designer as deliberate.
4. The designer plays the result in Godot (open `godot/project.godot` in the Godot 4.7.2 (.NET) editor and press F5).
5. The two refine it: the designer reacts, Claude edits, the check runs again, and so on.

Creative questions that come up while converting are logged as open creative questions, not settled by Claude.

## 2. Iterating deep in a playthrough

To return to a moment far into a playthrough after a content edit:

1. Before the moment, save a game (the save screen, 10 slots).
2. Edit the pack and run the content check.
3. Run the game again and load that save. The save keeps the player, relationships, party, lineup, flags, and chosen and seen history; the new content applies from there.

Loading never refuses over changed content. It adjusts the save to fit, one step at a time in this order (characters, party, lineup, town, conversation, hand-off), and returns a load report. Each adjustment has a kind:

| Kind | Happens when | Result |
| --- | --- | --- |
| `CharacterDropped` | A saved character id is no longer in the content | Dropped from the save (their reaction progress is kept in case they return) |
| `CharacterAdded` | A content character is not in the save | Added at its `startingAffection` and `startingTrust` |
| `PartyMemberDropped` | A saved party member is no longer in the content | Leaves the party |
| `LineupMemberDropped` | A lineup member is gone from the content, not in the party, has no `combat`, or appears twice | Removed from the lineup |
| `LineupCleared` | The kept lineup is larger than `settings.lineup.max` | The whole lineup is cleared |
| `TownReset` | The saved town is gone, or the save has no town | Player placed at the start of `startingTown` (no town if the content has none) |
| `PositionReset` | The saved position is not walkable in the town now | Player moved to the town's `start` |
| `ConversationEnded` | The saved conversation is gone, or a fight or hand-off is also active | Conversation ended |
| `ConversationRestarted` | The saved step no longer exists in its conversation | Restarts at the conversation's opening step. This is what a removed or renamed step does to a save sitting on it |
| `HandOffCleared` | The saved hand-off fight is gone, or its lineup is not ready under the current limits | Hand-off cleared; the player is back in the town |
| `HandOffTownCleared` | The hand-off named a town that is gone | The hand-off stays but names no town |

Flags, chosen choices, and seen conversations are kept whole, even ids the content no longer uses. Practical consequences when iterating: a save keeps the old flag, choice, and conversation ids, so renaming one means conditions on the new id do not see the old history (set it again by playing, or keep the old id); adding a character is safe; removing the step a save sits on restarts that conversation.

## 3. Pack structure

A pack is a folder with a `pack.json` and the content files it lists. The stand-in pack:

```json
{
  "format": "ancient-order-content",
  "version": 1,
  "id": "standin",
  "name": "STAND-IN Pack",
  "standIn": true,
  "files": [
    "settings.json",
    "cast.json",
    "town.json",
    "conversations/townsfolk.json",
    "conversations/recruits.json",
    "conversations/scenes.json",
    "conversations/exit.json",
    "fights.json"
  ]
}
```

| Field | Required | Meaning |
| --- | --- | --- |
| `format` | yes | Must be exactly `ancient-order-content` |
| `version` | yes | Must be `1` |
| `id` | yes | Pack id, not empty |
| `name` | yes | Pack name, not empty |
| `standIn` | yes | `true` shows the STAND-IN CONTENT marker in the game; real packs set `false` |
| `files` | yes | Content files in load order |

File paths in `files`: relative to the pack folder, forward slashes only, each part non-empty and not `.` or `..`, must end in `.json`, no absolute paths or drive letters, `pack.json` cannot list itself, and a file cannot be listed twice.

**Sections and merge order.** Any file may hold any of these top-level sections, in any mix: `settings`, `synergy`, `characters`, `archetypes`, `fights`, `conversations`, `towns`. List sections (`characters`, `archetypes`, `fights`, `conversations`, `towns`) merge across files in `files` order. `settings` must appear exactly once in the whole pack; `synergy` at most once. A duplicate id of any kind is a problem, naming where the first one is.

**Ids.** Every id is 1 to 64 characters. This covers characters, conversations, steps, choices, fights, enemies, towns, and flags. Obstacle and spot ids and archetype ids must be non-empty and unique. Character ids cannot be `narrator` or `player` (reserved for speakers). An enemy id cannot reuse a character id. Step ids are unique within a conversation; choice ids are unique within a conversation (across all its steps). Ids are exact-match, case-sensitive text; the stand-in prefixes everything with `standin_`.

**Strict reading.** A file with a wrong name, an unknown name, a duplicate name, a missing required name, a `null` where a value is required, or an enum that is not an exact camelCase name is refused. Names are camelCase. Comments and trailing commas are tolerated. Enum values are written as camelCase names only: `elementalPath` is `fire`, `water`, `air`, `earth`, `shadow`, `light`; `ascensionLevel` is `level0` to `level3`; trait names are `patience`, `empathy`, `cunning`, `logic`, `kindness`, `charisma`.

**Choosing the pack.** The Godot project setting `ancient_order/content/pack` names the pack folder. It is set in `godot/project.godot` under `[ancient_order]`:

```
content/pack="res://content/standin"
```

To play another pack, change that path (e.g. `res://content/mypack`) and restart the game. If the pack fails the check, the game does not start the session; the problems are printed to the Godot output and shown in debug builds (section 8).

## 4. Content types

Required fields are marked yes. A field marked "no" may be omitted.

### 4.1 Settings

One `settings` section per pack (`godot/content/standin/settings.json`):

```json
"settings": {
  "playerName": "STAND-IN Player",
  "lineup": { "min": 2, "max": 3 },
  "startingParty": ["standin_companion_a", "standin_companion_b"],
  "playerCombat": { "rank": 3, "stamina": 120, "power": 80, "speed": 80, "elementalPath": "air" },
  "startingTown": "standin_town",
  "reach": 2.5,
  "display": { "partyReadouts": true, "synergyNumbers": true },
  "conversations": { "lockedText": "STAND-IN: this choice is locked right now." }
}
```

| Field | Required | Meaning |
| --- | --- | --- |
| `playerName` | yes | The player's name; 1 to 200 characters, no control characters |
| `lineup` | yes | `{ "min", "max" }`: the fight lineup size limits; needs `0 <= min <= max` |
| `startingPersonality` | no | Six traits (below); default is an even spread |
| `playerCombat` | no | The player's combat stats (below); without it the player does not fight |
| `startingParty` | no | Character ids in the party at a new game, in order; each must exist, no repeats |
| `startingTown` | no | Town id where a new game starts; must exist; without it there is no town |
| `reach` | no | Meters within which the player can use a person or spot; positive; default 2.0 (a town may override) |
| `display` | no | `{ "partyReadouts", "synergyNumbers" }`, both default `true` (section 8) |
| `conversations` | no | `{ "lockedText" }`: the in-world line for a locked choice with none of its own; default "That is not open to you yet." |
| `tells` | no | `{ "clearAt", "strongAt" }`, defaults 3 and 8; needs `0 <= clearAt <= strongAt` |
| `demeanor` | no | `{ "warmAt", "waryAt", "coldAt" }`, defaults 50, -20, -60; needs `coldAt < waryAt < warmAt` |

Personality object (`startingPersonality`, and each character's `personality`): all six of `patience`, `empathy`, `cunning`, `logic`, `kindness`, `charisma` are required; each is 5 to 35 and the six must sum to 100 (within 0.01).

Combat profile object (`playerCombat`, a character's `combat`): `rank` (above 0), `stamina` (above 0), `power` (0 or more), `speed` (0 or more), `elementalPath` are required; `ascensionLevel` (`level0` to `level3`) is optional.

### 4.2 Characters

Under `characters`, in `cast.json`:

```json
{
  "id": "standin_companion_a",
  "name": "STAND-IN Companion A",
  "archetypeId": "standin_archetype_scored",
  "personality": { "patience": 20, "empathy": 20, "cunning": 10, "logic": 15, "kindness": 20, "charisma": 15 },
  "startingAffection": 0,
  "startingTrust": 0,
  "combat": { "rank": 3, "stamina": 120, "power": 85, "speed": 90, "elementalPath": "light" }
}
```

| Field | Required | Meaning |
| --- | --- | --- |
| `id` | yes | Up to 64 characters, unique, not `narrator` or `player` |
| `name` | yes | Display name, not empty (also fills `{name}` in reaction lines) |
| `archetypeId` | yes | Must name an archetype in the pack |
| `personality` | yes | Six traits, 5 to 35 each, sum 100. Fixed for NPCs; only the player's personality changes |
| `startingAffection` | yes | -100 to 100 |
| `startingTrust` | yes | -100 to 100 |
| `combat` | no | Combat profile. Without it the character cannot fight and cannot be in a lineup (`standin_elder` has none) |

### 4.3 Archetypes

Under `archetypes`, in `cast.json`. An archetype does two jobs: how its characters (and enemies) decide in combat, and how its characters react to the player's choices.

| Field | Required | Meaning |
| --- | --- | --- |
| `id` | yes | Unique, not empty |
| `profile` | no | A decision profile makes the archetype score its combat options. Without one it attacks the first living opponent |
| `reactions` | no | How characters of this archetype react to a choice that shifts the player's personality. Without it they never react by archetype |

Decision profile (from `standin_archetype_scored`): `baseScores` needs all five of `attack`, `defend`, `evade`, `special`, `group`; `factorWeights` needs all seven of `ownStamina`, `allyInDanger`, `targetVulnerability`, `energyAvailability`, `speedAdvantage`, `roundPhase`, `teamBalance`. All values are finite numbers.

```json
"profile": {
  "baseScores": { "attack": 0.3, "defend": 0.5, "evade": 0.3, "special": 0.4, "group": 0.2 },
  "factorWeights": {
    "ownStamina": 1.0,
    "allyInDanger": 1.8,
    "targetVulnerability": 0.5,
    "energyAvailability": 1.2,
    "speedAdvantage": 0.3,
    "roundPhase": 0.8,
    "teamBalance": 1.4
  }
}
```

Reactions:

```json
"reactions": {
  "leanings": { "empathy": 1, "kindness": 1, "cunning": -1 },
  "minimum": 1,
  "affectionPerPoint": 1,
  "trustPerPoint": 0.5,
  "approve": {
    "slight": "STAND-IN reaction: {name} gives a faint nod (slight approval).",
    "clear": "STAND-IN reaction: {name} smiles (clear approval).",
    "strong": "STAND-IN reaction: {name} beams warmly (strong approval)."
  },
  "disapprove": {
    "slight": "STAND-IN reaction: {name} glances away (slight disapproval).",
    "clear": "STAND-IN reaction: {name} frowns (clear disapproval).",
    "strong": "STAND-IN reaction: {name} recoils sharply (strong disapproval)."
  }
}
```

| Field | Required | Meaning |
| --- | --- | --- |
| `leanings` | yes | Per trait, how much this archetype likes a rise (positive) or dislikes it (negative). Any of the six traits; unwritten ones weigh nothing |
| `minimum` | yes | 0 or more. A score smaller than this (in size) draws no reaction |
| `affectionPerPoint` | yes | Affection change per point of score |
| `trustPerPoint` | yes | Trust change per point of score |
| `approve` | yes | Lines for a positive score: `clear` required, `slight` and `strong` optional (fall back to `clear`) |
| `disapprove` | yes | Same, for a negative score |

**The reaction formula.** When a choice has a `shift` effect, each present participant of the conversation reacts, once, in participant order:

1. Score = the sum over traits of (leaning for the trait) x (the choice's written shift for that trait). Only the written shift counts, not the clamped result.
2. If the absolute score is below `minimum`, no reaction.
3. Affection change = round(score x `affectionPerPoint`); trust change = round(score x `trustPerPoint`). If both round to zero, no reaction.
4. Strength: the larger of the two absolute changes, below `tells.clearAt` is slight, at or above `tells.strongAt` is strong, otherwise clear.
5. A positive score uses `approve` lines, a negative one `disapprove`, picking the line for the strength. `{name}` becomes the character's name.
6. The changes apply to the character's relationship (kept within -100 to 100).

Worked example: Companion A (`standin_archetype_scored`) hears the Elder choice `warm`, `{ "type": "shift", "empathy": 4 }`. Score = 1 x 4 = 4. Affection +4, trust +2; the larger is 4, which is clear (3 to under 8). A approves with the `clear` line. Companion B (`standin_archetype_basic`, empathy leaning -1) scores -4: disapproves with its clear line.

### 4.4 Synergy

One optional `synergy` section, in `settings.json`. Without it there are no synergies.

```json
"synergy": {
  "resolution": "bestOnly",
  "bonuses": [
    {
      "name": "STAND-IN Well Rounded",
      "rule": "coverage",
      "threshold": 25,
      "stat": "power",
      "step": "multiply",
      "value": 1.1,
      "feel": "STAND-IN feel: the party covers every base and hits a little harder."
    }
  ]
}
```

(The stand-in pack has a second bonus, `STAND-IN Bond`, `rule: alignment`, `threshold: 80`, `stat: speed`.)

| Field | Required | Meaning |
| --- | --- | --- |
| `resolution` | yes | `bestOnly` (only the best-matching qualifying bonus applies) or `allThatQualify` |
| `bonuses` | yes | List of bonuses, in priority order |
| `name` | yes | Unique, not empty |
| `rule` | yes | `coverage` (every trait is at least `threshold` in someone in the party) or `alignment` (for some companion, the player's total on that companion's two strongest traits is at least `threshold` percent of the companion's own total on them) |
| `threshold` | yes | Above 0 |
| `stat` | yes | `rank`, `maxStamina`, `power`, or `speed` |
| `step` | yes | `flat` (added to base), `addPercent` (0.10 is +10%, one shared pool), or `multiply` (applied last) |
| `value` | yes | Finite number |
| `feel` | no | A line that describes the bonus without numbers (section 8) |

### 4.5 Fights

Under `fights`, in `fights.json`. A fight is a hand-off target: leaving the town for it needs a ready lineup.

```json
{
  "id": "standin_encounter",
  "name": "STAND-IN Encounter",
  "lineup": { "min": 2, "max": 3 },
  "enemies": [
    {
      "id": "standin_enemy",
      "name": "STAND-IN Enemy",
      "archetype": "standin_archetype_basic",
      "rank": 3,
      "stamina": 400,
      "power": 40,
      "speed": 60,
      "elementalPath": "earth",
      "reactionSkills": {
        "block": { "sr": 0.5, "smr": 0.3, "fmr": 0.1 },
        "dodge": { "sr": 0.4, "smr": 0.8, "fmr": 0.15 },
        "parry": { "sr": 0.3, "smr": 0.9, "fmr": 0.1 }
      }
    }
  ]
}
```

| Field | Required | Meaning |
| --- | --- | --- |
| `id`, `name` | yes | Unique id; name not empty |
| `lineup` | no | `{ "min", "max" }` this fight asks for instead of `settings.lineup`. Needs `0 <= min <= max`, and `min` cannot exceed `settings.lineup.max` (the lineup could never be ready) |
| `enemies` | yes | At least one enemy |

Enemy: `id` (unique among fighters, not a character id), `name`, `archetype` (must exist), `rank` (above 0), `stamina` (above 0), `power` and `speed` (0 or more), `elementalPath`, `reactionSkills` are required; `ascensionLevel` optional. `reactionSkills` needs `block`, `dodge`, and `parry`, each with `sr`, `smr`, and `fmr` as numbers from 0 to 1.

### 4.6 Towns

Under `towns`, in `town.json`. A town is the walkable place the player moves in, with people and spots.

**Coordinates and facing.** Every point is a `[x, z]` pair in meters on the ground plane: x runs east, z runs south, and Godot places a point at `Vector3(x, 0, z)` with Y up. A polygon is 3 or more points whose last joins the first. `facing` is degrees: 0 faces -Z (north), clockwise seen from above (90 faces +X, 180 faces +Z, 270 faces -X).

```json
{
  "id": "standin_town",
  "name": "STAND-IN Town",
  "ground": [[[0, 0], [40, 0], [40, 40], [0, 40]]],
  "start": [20, 20],
  "obstacles": [
    { "id": "standin_building_northwest", "footprint": [[6, 6], [14, 6], [14, 12], [6, 12]], "height": 4, "label": "STAND-IN Building" }
  ]
}
```

| Field | Required | Meaning |
| --- | --- | --- |
| `id`, `name` | yes | Unique id; name not empty |
| `ground` | yes | List of walkable polygons (their union is the ground); at least one |
| `start` | yes | Where a new game stands; must be on the ground (check problem otherwise) |
| `obstacles` | no | Footprints cut out of the ground; each has `id` (unique), `footprint` (polygon), `height` (above 0), `label` (optional) |
| `reach` | no | Overrides `settings.reach` for this town; positive |
| `people` | no | People standing in the town |
| `spots` | no | Places that trigger something |

**People and placements.** A person is a character standing in the town. They are present when one of their placements holds; the first holding placement is where they stand. A person with no holding placement is not there.

```json
{
  "character": "standin_smith",
  "placements": [
    { "at": [30, 18], "facing": 270, "when": [{ "type": "flag", "flag": "standin_smith_moved" }] },
    { "at": [20, 26], "facing": 0 }
  ],
  "opens": [{ "conversation": "standin_smith_talk" }]
}
```

| Field | Required | Meaning |
| --- | --- | --- |
| `character` | yes | Must exist |
| `placements` | yes | At least one: `at` (required, on the ground), `facing` (optional), `when` (optional conditions) |
| `opens` | no | What the person opens when the player uses them. The first entry whose `when` holds wins |

An `opens` entry has exactly one of `conversation` (a conversation id) or `handOff` (a fight id), plus an optional `when`. People use the player's reach and are opened by interacting.

**Spots.** A spot is an area that triggers:

```json
{
  "id": "standin_market_bell",
  "name": "STAND-IN Market Bell",
  "area": { "polygon": [[15, 2], [25, 2], [25, 5], [15, 5]] },
  "trigger": "walkOver",
  "opens": [{ "conversation": "standin_scene_walkover", "when": [{ "type": "not", "condition": { "type": "seen", "conversation": "standin_scene_walkover" } }] }]
}
```

| Field | Required | Meaning |
| --- | --- | --- |
| `id` | yes | Unique within the town |
| `area` | yes | Exactly one of `{ "circle": { "center": [x, z], "radius": r } }` (radius above 0) or `{ "polygon": [...] }` |
| `trigger` | yes | `interact` (the player uses it within reach) or `walkOver` (fires when the player steps into the area) |
| `name` | no | Label for a shown spot |
| `shown` | no | Whether the spot shows as a marker; default `true` for `interact`, `false` for `walkOver` |
| `when` | no | The spot exists only while these hold |
| `opens` | no | Same as for people; first holding entry wins |

A spot's anchor (a circle's center, or a polygon's first walkable corner else its corner average) must be on the ground.

A hand-off spot (the stand-in exit) falls back to a conversation when the first entry does not hold:

```json
"opens": [
  {
    "handOff": "standin_encounter",
    "when": [{ "type": "flag", "flag": "standin_exit_ready" }, { "type": "lineupReady" }]
  },
  { "conversation": "standin_exit" }
]
```

### 4.7 Conversations

Under `conversations`, in the files under `conversations/`. A conversation is a set of steps. A step shows lines and then either choices or Continue. A narrated scene is simply a conversation whose steps have `next` and no choices (`standin_scene_walkover`).

| Field | Required | Meaning |
| --- | --- | --- |
| `id` | yes | Unique, 1 to 64 characters |
| `participants` | yes | Character ids present (may be empty `[]`); each must exist. Only participants react to choices |
| `steps` | yes | At least one. The first step is the default opening |
| `title` | no | Display title |
| `start` | no | Openings: list of `{ "step", "when" }`. The first whose `when` holds opens the conversation; if none holds, the first step opens |

```json
"start": [{ "step": "again", "when": [{ "type": "seen", "conversation": "standin_elder_talk" }] }],
```

The opening is chosen before the conversation counts as seen, so a `seen` condition on its own id tells a first visit from a return.

**Steps.**

| Field | Required | Meaning |
| --- | --- | --- |
| `id` | yes | Unique within the conversation |
| `lines` | yes | Lines shown in order |
| `choices` | no | Choices offered |
| `next` | no | Where Continue goes when no choice is showing |

**Lines.** `speaker` is `narrator`, `player`, or a character id (must exist); `text` is what is shown; `when` (optional) shows the line only while it holds.

```json
{ "speaker": "narrator", "text": "STAND-IN: this extra line appears only because you answered warmly the first time.", "when": [{ "type": "chose", "conversation": "standin_elder_talk", "choice": "warm" }] }
```

**Choices.**

| Field | Required | Meaning |
| --- | --- | --- |
| `id` | yes | Unique within the conversation |
| `text` | yes | The choice as the player reads it |
| `when` | no | The choice is hidden unless these hold |
| `requires` | no | The choice is shown locked unless these hold |
| `lockedText` | no | The line shown while locked; falls back to `settings.conversations.lockedText` |
| `effects` | no | What the choice does (section 5) |
| `reactions` | no | Overrides of participants' reactions (below) |
| `next` | no | Destinations |

`when` hides; `requires` shows the choice but locked. Use `requires` when the player should see that something is closed to them, and `when` when it should not appear at all.

```json
{
  "id": "favor",
  "text": "STAND-IN: ask for a favor (trait-gated; opens the gate spot)",
  "lockedText": "STAND-IN: locked until you are more empathetic.",
  "requires": [{ "type": "trait", "trait": "empathy", "atLeast": 20 }],
  "effects": [{ "type": "setFlag", "flag": "standin_gate_open" }],
  "next": [{ "step": "favor_done" }]
}
```

**Destinations (`next`).** A list tried in order; the first whose `when` holds is taken. Each has exactly one of `step` (another step of the same conversation) or `conversation` (the opening of another conversation, which ends this one and counts it as seen), plus an optional `when`. If none holds or `next` is missing, the conversation ends. The same shape is used by steps and choices.

Conditional destination (the always-offered choice whose outcome depends on a hidden condition):

```json
"next": [
  { "step": "help_given", "when": [{ "type": "trait", "trait": "empathy", "atLeast": 20 }] },
  { "step": "help_refused" }
]
```

Chaining into another conversation:

```json
{ "id": "bell", "text": "STAND-IN: ask about the bell (chains into a narrated scene)", "next": [{ "conversation": "standin_scene_chained" }] }
```

**Reaction overrides (`reactions`).** One per participant at most, naming a participant of the conversation. Each has either `"silence": true` (no reaction, and nothing else written) or `text` with optional `affection` and `trust`, which replaces the archetype reaction. `{name}` works in the text. Overrides fire whether or not the choice shifts personality.

```json
"reactions": [
  { "character": "standin_companion_b", "text": "STAND-IN reaction: {name} gives a written line that replaces the archetype reaction.", "affection": 2, "trust": 2 },
  { "character": "standin_companion_a", "silence": true }
]
```

A written reaction's strength comes from the larger absolute of its affection and trust against the `tells` cutoffs; with no change it is a slight, neutral line.

## 5. Conditions and effects

### 5.1 Conditions

A condition is an object with a `type`. Every `when` and `requires` is a **list** of conditions, and all of them must hold (an empty or missing list always holds). Thresholds are inclusive: `atLeast: 20` holds at 20; `atMost: -21` holds at -21. Trait values are percent values (the player's six traits sum to 100).

| `type` | Fields | Holds when |
| --- | --- | --- |
| `flag` | `flag` | The flag is set |
| `chose` | `conversation`, `choice` | The player has taken that choice of that conversation (the choice must exist in it) |
| `seen` | `conversation` | The conversation has started at least once |
| `trait` | `trait`, `atLeast` and/or `atMost` | The player's trait is in range. At least one bound is required |
| `relationship` | `character`, `measure` (`affection` or `trust`), `atLeast` and/or `atMost` | The character's affection or trust is in range. At least one bound is required |
| `inParty` | `character` | The character is in the party |
| `lineupReady` | `fight` (optional) | The lineup is ready under the named fight's limits, or the settings limits when `fight` is omitted |
| `all` | `conditions` | Every nested condition holds |
| `any` | `conditions` | At least one nested condition holds |
| `not` | `condition` | The one nested condition does not hold |

Examples from the stand-in pack:

```json
{ "type": "flag", "flag": "standin_smith_moved" }
{ "type": "chose", "conversation": "standin_elder_talk", "choice": "warm" }
{ "type": "seen", "conversation": "standin_elder_talk" }
{ "type": "trait", "trait": "empathy", "atLeast": 20 }
{ "type": "relationship", "character": "standin_guard", "measure": "trust", "atMost": -21 }
{ "type": "inParty", "character": "standin_merchant" }
{ "type": "lineupReady" }
{ "type": "not", "condition": { "type": "inParty", "character": "standin_companion_b" } }
```

`all` and `any` are not used in the stand-in pack. Their shape is a `conditions` list of nested conditions:

```json
{ "type": "any", "conditions": [{ "type": "flag", "flag": "a" }, { "type": "flag", "flag": "b" }] }
```

(`a` and `b` here stand for real flag names; the check requires every flag read to be set somewhere.) Because top-level lists already mean "all", `all` is only needed inside `any` or `not`.

### 5.2 Effects

Effects belong to a choice's `effects` list. They apply in written order, as a unit: if any is refused, none apply.

| `type` | Fields | Does |
| --- | --- | --- |
| `relationship` | `character`, `affection` and/or `trust` | Adds to the character's affection and/or trust, kept within -100 to 100 |
| `join` | `character` | Adds the character to the party (already in: nothing) |
| `leave` | `character` | Removes the character from the party and lineup (not in: nothing) |
| `shift` | any of the six trait names | Shifts the player's personality by those amounts. Every trait stays 5 to 35 and the six keep summing to 100; the others rebalance. Drives archetype reactions |
| `setFlag` | `flag` | Sets the flag |
| `clearFlag` | `flag` | Clears the flag |
| `handOff` | `fight` | Leaves the town for that fight |

```json
{ "type": "shift", "empathy": 4 }
{ "type": "relationship", "character": "standin_guard", "affection": 10, "trust": 10 }
{ "type": "join", "character": "standin_recruit" }
{ "type": "leave", "character": "standin_companion_b" }
{ "type": "setFlag", "flag": "standin_gate_open" }
{ "type": "handOff", "fight": "standin_encounter" }
```

`clearFlag` has the same shape as `setFlag` (`{ "type": "clearFlag", "flag": "..." }`) and is not used in the stand-in pack.

**Order of operations in a choice.** (1) The effects apply in order. (2) Participants react (archetype reactions from the shift, or the overrides). (3) The choice is recorded as chosen. (4) The destination is taken.

**Hand-off choices.** A `handOff` effect must be the choice's last effect, and the choice cannot also have `next` (it ends the conversation and leaves for the fight). A hand-off choice is locked automatically while the lineup is not ready: the game adds the lineup requirement itself, judged after the effects before the hand-off run, so never write `lineupReady` for it. Give it a `lockedText`:

```json
{
  "id": "go",
  "text": "STAND-IN: go to the stand-in fight now (confirmed exit)",
  "lockedText": "STAND-IN: locked until your fight lineup is ready.",
  "effects": [{ "type": "handOff", "fight": "standin_encounter" }]
}
```

## 6. What the content check enforces

`dotnet run --project tools/AncientOrder.ContentCheck` prints one line per finding and a summary per pack:

```
== Pack C:\...\godot\content\standin
OK: standin: 0 problem(s), 0 warning(s)
```

A finding reads `PROBLEM <file>:<pointer>: <message>` or `WARNING <file>:<pointer>: <message>`. The pointer is a JSON pointer into the file: `cast.json:/characters/0/archetypeId` is the `archetypeId` of the first character in `cast.json`. A syntax error is `file:line L, column C`; a problem with a whole file is just the file name; a `pack.json` problem is `pack.json:/files/3`. Findings come in content order, so output is the same on every machine. The exit code is 1 when any pack has a problem, 0 otherwise (2 for a usage error).

**Problems fail the pack** (the game will not load it). **Warnings never fail** (the pack plays); read them and either fix or tell the designer they are deliberate.

The check runs in stages. If a file cannot be read (syntax, a wrong or unknown name, a missing required field, a bad enum), only those problems are reported; cross-file checks wait for the fix. Fix the first batch and re-run.

### 6.1 Format and loading problems

- `pack.json`: wrong `format`, wrong `version`, empty `id` or `name`, missing `files`; a file path that breaks the rules in section 3, is listed twice, or does not exist.
- Strict reading: unknown, duplicate, or missing names; `null` for a required value; an enum that is not an exact camelCase name; an effect or condition with a missing or unknown `type`; `null` list items.
- No `settings` section, or more than one `settings` or `synergy` section.
- A duplicate id: `duplicate character id: ... (first defined at cast.json:/characters/0/id)`.
- A point that is not `[x, z]`; an area without exactly one of `circle` or `polygon`; an `opens` entry without exactly one of `conversation` or `handOff`; a destination without exactly one of `step` or `conversation`; a non-finite `shift`.

### 6.2 Reference and value problems

- Settings: empty or over-long `playerName`; invalid `lineup`, `tells`, `demeanor`, `reach`, `startingPersonality`, `playerCombat`; unknown or duplicate `startingParty` character; unknown `startingTown`.
- Characters: id over 64 characters or reserved; invalid personality (range 5 to 35, sum 100); relationship values outside -100 to 100; invalid `combat`; unknown `archetypeId`.
- Archetypes: a scored profile missing a base score or factor weight; reactions without a `clear` line; non-finite leanings, `minimum` below 0, non-finite per-point values.
- Synergy: duplicate or empty bonus name; threshold not above 0; non-finite value.
- Fights: ids, ranges, and reaction-skill rates as in section 4.5; an unknown archetype; an enemy id reusing a character id; a fight `lineup.min` above the settings `lineup.max`.
- Conversations: unknown participant, speaker, step, conversation, fight, or character in an effect or condition; no steps; duplicate step or choice ids; a `handOff` that is not the last effect or that has `next`; a reaction naming a non-participant, naming one twice, silenced with extra values, or with neither silence nor text; a `trait` or `relationship` condition with no bound; a `chose` naming a choice the conversation does not have; flags and ids over 64 characters or empty.
- Towns: no ground; a polygon under 3 points; an obstacle height not above 0; a person with no placement or an unknown character; a spot radius not above 0; an `opens` naming an unknown conversation or fight.

### 6.3 Whole-pack analyses

These run only when the pack loads cleanly.

| Rule | Level | What it catches |
| --- | --- | --- |
| Dead end | Problem | A step with choices where none is always available. Add a choice with no `when`, no `requires`, and no `handOff` (the stand-in `bye` choices are such fallbacks) |
| Ground | Problem | A town `start`, a person placement, or a spot anchor off the walkable ground (outside the ground polygons or inside an obstacle) |
| Flag read, never set | Problem | A `flag` condition whose flag no `setFlag` anywhere in the pack sets (a typo would silently lock content) |
| Empty text | Problem | An empty or blank line, choice, locked line, reaction line, or feel line |
| Locked with nothing to show | Problem | A choice with `requires` and no `lockedText` while `settings.conversations.lockedText` is blank |
| Flag set, never read | Warning | A `setFlag` whose flag no condition reads |
| Digit in text | Warning | A digit in a line, choice, locked line, or reaction line (archetype or override). Players do not see numbers in conversations |
| Hidden synergy with no feel | Warning | A synergy bonus with no `feel` while `display.synergyNumbers` is false, so players would not notice it |
| Conversation never started | Warning | A conversation no person, spot, or destination opens |
| Step unreachable | Warning | A step no start entry, first step, or destination leads to. Conditions are not evaluated: every branch counts as reachable |

## 7. The stand-in playthrough route

The stand-in pack is test material that exercises every mechanic. The list below shows what each stop offers; it is not one sequence a single playthrough can follow. Three points:

- The Elder's `override` and `warm` cannot both be taken in one timeline. A conversation counts as seen when it starts, so every later visit opens at `again`. To see both, save before the Elder, take `override`, load the save (section 2), then take `warm`.
- The Elder's `ask` (the hidden empathy branch) is only on the first greeting, so a route that takes `warm` first never plays it. The Guard's `pass` demonstrates the hidden-outcome mechanic along the route.
- `favor` is never seen locked after `warm`, because empathy then reaches 20 or more. The locked choices met on the route are the Merchant's `join` and the Exit's `go`.

The proven route is `rules/AncientOrder.Rules.Tests/Playthrough/StandInRoute.cs`. Its order is: save, Elder `override`, load, Elder `warm`, Elder `favor`, Gate, Merchant `gift` twice then `join`, Smith `slight`, `strong`, `traveler`, `move`, `bell` (at the Smith's new spot), Recruit `invite`, Guard `pass` (refused), `apologize`, `pass` (granted), Companion B `part`, walk over the market bell, Exit `mark_ready`, set the lineup to Companion A, Recruit, and Merchant, then the Exit hands off to the fight. Edit that file together with the pack when stand-in content changes.

The stops, in the order the list reads:

1. **Start in town.** New game in `standin_town` with Companion A and Companion B in the party, lineup 2 to 3. Companion B stands in town to be asked to leave.
2. **Elder** (`standin_elder_talk`). First visit: the greeting line shows only now (a `start` entry on `seen`), plus a clue line about empathy. `warm` and `cool` shift personality and draw different reactions from Companion A and B (archetype reactions). `override` shows a written reaction and a silence. `ask` always appears; its destination depends on the hidden empathy condition. Returning shows the `again` step, an extra line because you chose `warm`, and `favor` (trait-gated, locked line, ungated `smalltalk` fallback). `favor` sets `standin_gate_open`.
3. **Gate spot** (`standin_gate`). Appears only after the favor (`when` on a flag), an `interact` spot opening `standin_gate_talk`.
4. **Smith** (`standin_smith_talk`). A line that shows because of an earlier choice (`chose`). `slight` and `strong` shifts show different reaction strengths. `move` sets `standin_smith_moved`, and the Smith stands at the other placement. `traveler` sets `standin_recruit_appears`. `bell` chains into `standin_scene_chained`.
5. **Recruit** (`standin_recruit_talk`). Appears after `traveler`, and only while not in the party; `invite` uses `join`.
6. **Guard** (`standin_guard_talk`). Starts distrustful; the cold and mild greetings switch on a trust threshold. `apologize` improves the relationship. `pass` is always offered with a hidden trust outcome.
7. **Merchant** (`standin_merchant_talk`). `gift` raises affection and trust; `join` is relationship-gated and uses the pack default locked line. Once in the party, a `start` entry opens the `already` step.
8. **Companion B** (`standin_farewell`). `leave` removes them from the party; returning, a `start` entry on `not inParty` shows `away` with a `join` choice to rejoin.
9. **Market bell spot** (`standin_market_bell`). A `walkOver` spot starting the narrated scene `standin_scene_walkover` once (a `not seen` condition).
10. **Exit spot** (`standin_exit`). Talks to `standin_exit`: `mark_ready` sets `standin_exit_ready` (and then vanishes via `not flag`). `go` is a hand-off choice, locked until the lineup is ready. Next time the spot hands off straight to `standin_encounter` (`flag` plus `lineupReady`).
11. **Party and lineup.** Synergy bonuses (`coverage` and `alignment`) with feel lines, the party readout, and lineup picking.
12. **Hand-off.** The placeholder fight screen.

## 8. What players see and what stays hidden

The design intuition: players read people, not numbers. The numbers live in the content files (this guide's Content types) and in the rules. Players see tells, demeanor, locked lines, and feel lines.

**Numbers players may see.** Only the party readout shows numbers: the player's and party members' personality and combat figures, subject to `display.partyReadouts`; and each synergy bonus's name, stat, step, and value, subject to `display.synergyNumbers`. Set either to `false` to hide it. Numbers are never shown for enemies, never in conversations, and never for affection or trust.

**Tells.** How a character reacts to a choice, shown as body language, never as a number. A tell has a direction (warmer, cooler, or neutral), a strength (slight, clear, strong), and a line:

- Direction comes from the sign of affection plus trust change.
- Strength comes from the larger absolute change against `settings.tells`: below `clearAt` is slight, at or above `strongAt` is strong, otherwise clear. Defaults: 3 and 8.
- Lines come from the archetype's `approve` and `disapprove` (slight and strong fall back to clear) or from a choice's `reactions` override. Write them as what a person visibly does: a nod, a glance away, a frown.

**Demeanor.** How a person carries themselves toward the player, from affection plus trust: warm at or above `warmAt`, cold at or below `coldAt`, wary at or below `waryAt`, otherwise neutral (defaults 50, -20, -60). Use it as the basis for how a person is written or shown. It is never printed as a number.

**Locked lines.** A choice with `requires` shows locked with its `lockedText`, or the pack default. Write locked lines in-world ("not until you are more empathetic"), never "needs empathy 20".

**Feel lines.** A synergy bonus's `feel` describes its effect in words ("the party moves as one and acts a little faster"). With `display.synergyNumbers` false it is the only way players learn the bonus exists, so write one for every bonus.

**Hidden-outcome choices.** A choice that is always offered but whose result depends on a hidden number (the Elder's `ask`, the Guard's `pass`). Write it with:

1. An ungated choice, so the player is never locked out (the check also requires a fallback).
2. Conditional `next` destinations: the success step first with its condition, the failure step last with none.
3. A clue, earlier in the same or another conversation, that points at the hidden quality without naming a number: `"STAND-IN clue: the Elder is known to help visitors who are empathetic."` The outcome lines then confirm it in-world: `"STAND-IN Elder: help refused, because your empathy was not high enough yet."` (the stand-in states the reason so the test is legible; real content decides how plain to be).

**Digits.** Players should not see numbers in conversations, so the check warns on any digit in a line, choice, locked line, or reaction line. Spell numbers out or rephrase. Feel lines are checked only for being empty.

**Checking the hidden numbers: dev tools.** Designer-only tools show what players never see: exact traits, what the last choice changed (written shift, trait changes, and each reaction's direction, strength, and affection and trust change), the relationships and demeanor of the people present, each locked choice's requirement against current values, story flags, and the synergy scores. In the editor and debug builds, F3 toggles the overlay. Debug builds also list the adjustments made when a save loads against changed content (section 2) and show the full problem list when a pack fails the check. Release builds leave the dev tools out entirely; they only read the session and never change the game. The code is in `godot/devtools/`.
