# Discovery Brief: Sprint 7 — Town and Conversation Systems

## Parent Context
- **Parent**: sprint-6 (Self-contained game foundation)
- **Inherited dimensions**: Where. The game still runs in the browser on the developer's machine.
- **Changed dimensions**:
  - Who: Sprint 6 meant players to notice nothing new. Now they get a town and conversations. The designer becomes the source of creative content, made outside the pipeline.
  - What: This sprint builds the town and conversation systems that creative content plugs into. Story, dialogue, and character decisions moved out of the pipeline (2026-10-26).
  - Why: There's a new Branch Goal.
- **Project North Star**: The statement itself holds as-is. A "Current phase: the demo" line was added under it this session, in `project_map.md` and the trunk brief.

## Who — Stakeholders
- Demo players: They'll eventually experience these systems through the real Ironhold content. This sprint they see stand-in content.
- Designer/director: Makes the creative decisions (story, dialogue, characters, art) outside the pipeline and brings them back to be plugged in.

## Where — Delivery
- The game runs in the browser on the developer's machine. There's no server and no hosted link.
- Steam packaging comes in a later sprint.

## What — The Idea
**Goals:**
- A walkable town on the scene stage, built from content: the layout, where people stand, and the spots that trigger scenes or conversations.
- A conversation system that plays any authored conversation and can carry all five demo mechanics:
  - personality shifts
  - personality-gated options
  - NPC reactions driven by archetype
  - consequences that carry forward (flags and earlier choices)
  - changes to the party
- Support for every team-building direction still open: winning someone over, new recruits, and choosing who goes into the fight.
- Room for about 30 minutes of town play, ending with a hand-off to the gym fight (Sprint 8).
- Clearly marked stand-in content that exercises every mechanic.

**Requirements:**
- A new story, town, or conversation is a content change, not a code change.
- Creative hand-off: the designer hands over decisions, and Claude turns them into content files.
- Every personality gate has an ungated fallback, so dialogue has no dead ends.
- Save and load keep working.

**Constraints:**
- Placeholder art.
- Follows ADR-055.
- Immutable state.
- NPC archetypes stay fixed.
- Personality traits stay within 5–35% and always sum to 100%.

**Out of scope:**
- The real Ironhold/Act 1 story, dialogue, and character decisions. This creative work happens outside the pipeline.
- Art.
- Combat UI and the gym fight itself (Sprint 8).
- Steam packaging and a hosted link.
- An authoring tool.

## Why — Branch Goal
Players can walk a town and talk with its people, and every mechanic the demo exists to show off comes through there. Who the player is shifts with what they choose, opens or closes options, changes how people respond, follows them into later moments, and shapes who fights beside them. When the designer settles a story, a character, or a scene, it goes in as content, and the systems are ready for whatever the creative work decides.

## Decision Posture
Both: the designer wants to see all major creative and technical decisions.

## Open Questions
- The exact creative hand-off format. The designer leans toward handing over decisions and having Claude convert them into content files, to be refined together.
- Creative questions, to be settled outside the pipeline:
  - Ironhold's new beats, and whether any of the existing `act1_demo.json` beats survive.
  - The team-building direction.
