# Discovery Brief: Sprint 7 (Godot) — Town and Conversation Systems

## Parent Context
- **Parent**: godot-foundation (Rules engine moved to C# on Godot 4.7)
- **Inherited dimensions**: None unchanged. Most of what changes brings back the original Sprint 7 brief (`branches/sprint-7/discovery_brief.md`), re-aimed at Godot (ADR-063).
- **Changed dimensions**:
  - Who: players get a town and conversations, shown with stand-in content. The designer supplies creative content from outside the pipeline. (Before, players saw nothing new.)
  - Where: the first playable Godot build. The town is in 3D. (Before, there was no UI.)
  - What: town and conversation systems carrying all five demo mechanics, plus the screens needed to play them. The settled Sprint 7 design decisions carry over.
  - Why: new Branch Goal, adapted from the Sprint 7 goal.
- **Project North Star**: Holds as-is

## Who — Stakeholders
- Demo players walk a town and hold conversations. This sprint shows clearly marked stand-in content; later the real Ironhold content plays through the same systems.
- Designer/director makes the creative decisions (story, dialogue, characters, places, art) outside the pipeline and hands them over to become content files.

## Where — Delivery
- The first playable Godot build, run from the Godot 4.7.2 (.NET) editor on the developer's machine. Exported desktop builds and Steam come in a later sprint.
- The town is shown in 3D: an isometric camera over the ground, with placeholder shapes for buildings and people.
- The browser build stays frozen as the reference.

## What — The Idea
**Goals:**
- A walkable 3D town built from content: its layout, where people stand, and spots that start conversations or scenes. Free walking with click-to-move.
- One conversation system for one-on-one talks and narrated scenes. It carries all five mechanics:
  - personality shifts
  - personality-gated options
  - reactions driven by archetype
  - consequences that carry forward
  - party changes
- Every team-building direction stays open: winning someone over, new recruits, and choosing who fights.
- Room for about 30 minutes of town play, ending with the hand-off to the fight (Sprint 8).
- The screens needed to play it, in the Equinox look:
  - start (new game or load)
  - the town
  - conversations
  - a view of personality, party, and fight lineup
  - save and load
- Clearly marked stand-in content that exercises every mechanic.

**Requirements:**
- A new story, town, character, or conversation is a content change, not a code change.
- Every personality gate has an ungated fallback, so there are no dead ends.
- Players can save and load from inside the game.
- The settled Sprint 7 design decisions (groups 1–3 in `branches/sprint-7/design_notes.md`) carry over.
- Rules stay in the C# library, independent of Godot.

**Constraints:**
- ADR-062: systems only, with no creative questions.
- ADR-063: Godot 4.7 (.NET) and C#.
- Placeholder art.
- Immutable state.
- NPC archetypes stay fixed.
- 6 personality traits, each 5–35%, always summing to 100%.

**Out of scope:**
- Real story, dialogue, characters, and art.
- Combat UI and the fight itself (Sprint 8).
- Exported builds, Steam, and web.
- A custom authoring tool.
- Any work on the TypeScript build.
- Carrying browser saves over.

## Why — Branch Goal
Players can walk a town and talk with its people, and every mechanic the demo exists to show off comes through there. Who the player is shifts with what they choose, opens or closes options, changes how people respond, follows them into later moments, and shapes who fights beside them. When the designer settles a story, a character, or a place, it goes in as content. The town and conversations are built on the foundation the game will ship on, so they grow into the full game's world instead of being rebuilt for it.

## Decision Posture
Both: the designer wants to see all major creative and technical decisions.

## Open Questions
Carried over from Sprint 7, to be settled outside the pipeline:
- Ironhold's new beats, and whether any `act1_demo.json` beats survive.
- The team-building direction.
