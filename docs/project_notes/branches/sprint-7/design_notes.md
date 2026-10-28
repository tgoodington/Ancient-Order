# Sprint 7 Design Notes (paused 2026-10-26)

Design paused mid-way: the designer decided to move the game to **Godot** (2026-10-26) after the 3D isometric direction came up (Divinity: Original Sin 2-style overworld, Final Fantasy 7-style 2D-ish combat). The engine move comes first; this sprint's design resumes afterwards, re-aimed at Godot. No tasks in `tasks.json` were enriched yet.

The decisions below are engine-agnostic and carry over.

## Settled

### Group 1 — Content foundation and characters (T1)
- Game content is a **content pack**: JSON files plus a small pack index listing them. The stand-in pack is the default; tests can pass their own pack.
- A pack holds: game settings (player name, starting personality, starting party, starting town, lineup size), characters, archetypes, towns, conversations (a file may hold several), fights.
- Characters: id, name, archetype id, fixed personality, starting affection/trust. A new game builds the roster, `party`, and `team` (fight lineup) from the pack. Every character lookup reads the game's roster; the hard-coded NPC templates are removed.
- Content is authoritative for fixed character traits (name, archetype, personality); saves own only what changes in play (affection, trust, party, lineup).
- Elena, Lars, and Kade move to a test-only cast so tests don't depend on stand-in or real content. Game content lives apart from test fixtures (supersedes ADR-032's location).
- Format: JSON (designer accepted; no new tooling, typos caught by the content check).

### Group 2 — Conversations and the five mechanics (T2–T6)
- **Designer decision:** one conversation format and one conversation player for one-on-one talks and narrated scenes; **retire** the Sprint 3 scene engine and the dialogue-tree engine (with their session operations and tests). `act1_demo.json` stays as an untouched file.
- Conversation = participants + steps; each step has lines (speaker: character, narrator, or player) and choices; a step with no choices shows Continue.
- Choices: `requires` a condition → shown locked with its requirement; `when` a condition → hidden unless true. Effects: personality shift, set/clear flag, relationship change, join/leave party, fight hand-off.
- Each step reports speaker, lines, all choices with locked state and requirements; each choice reports personality shifts, reactions, party joins/leaves.
- Story memory: flags, every choice taken ("conversation/choice"), conversations seen. Conditions: flag, earlier choice, seen conversation, personality trait threshold, relationship threshold, party membership, lineup ready; a list = all must hold (ADR-030), plus `any` and `not`. Lines, choices, choice destinations, conversation starts, and what people/spots open can all be conditioned.
- Party: choices add/remove members; lineup picked only from the party, size from content (min/max); removal also removes from the lineup; lineup changes refused during conversations and combat.
- **Designer decision (reactions, option C):** reactions computed by default from archetype trait leanings (any choice that shifts personality draws reactions, with matching affection/trust changes, from the characters present), with per-choice authored overrides (replace or silence a character's reaction with written text/values). Archetype definitions themselves are creative content, decided outside the pipeline; stand-in archetypes are clearly marked stand-ins.

### Group 3 — Town and fight hand-off (T7, T8)
- **Designer decision:** **free walking**, not grid steps. Positions are continuous world coordinates on a ground plane (so content keeps its meaning in a 3D overworld); people stand at points; spots are areas; "in reach" is a distance.
- Click-to-move with pathfinding around obstacles; clicking a person walks into reach and starts what they open. Walk-over spots fire when crossed; interact spots fire on interaction (set per spot in content).
- Who is present, where they stand, and which spots exist follow content conditions and are re-derived from state on every change (nothing extra saved).
- A person or spot opens the first entry whose conditions hold: a conversation or the fight hand-off. Hand-off can also be a conversation-choice effect (confirmed exit); a "lineup ready" condition shows it locked until valid. Invalid lineup → refused with a reason, player stays. Valid → records fight, encounter, lineup; town ends; Sprint 8 starts from it.
- Walkability: authored per town in content for now; with real 3D levels it comes from the level's navigation data behind the same interface.

## Open
- **Group 4 (saves):** the "pre-sprint saves" question is moot after ADR-063 — browser saves don't carry into the Godot desktop build, which starts its own save format. Save rules still proposed for the Godot version: version bump; mid-conversation saves resume at the same step; loading against changed content reconciles (fixed traits from content, vanished characters/towns/conversations/fights fall back safely, flags and remembered choices kept).
- Groups 5–8 not yet presented (content check, stand-in content, content guide; town on stage; screens and wiring; test suites). Their render/UI parts will be redesigned for Godot.
- ~~Godot language choice~~ — decided: **C#** (ADR-063). The rules engine becomes a plain C# library tested with `dotnet test`.
