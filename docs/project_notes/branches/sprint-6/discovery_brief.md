# Discovery Brief: Sprint 6 — Frontend React Phase 2 (Town Scene & Dialogue)

## Parent Context
- **Parent**: sprint-5 (Frontend React Phase 1 — HUD)
- **Inherited dimensions**: Who, Where
- **Changed dimensions**: What (new town scene + dialogue surface), Why (branch-scoped goal)
- **Project North Star**: Holds as-is (authored to map this session — section was previously blank scaffold)

## Who — Stakeholders
- The single player (primary).
- Indirectly, the investor/publisher pitch audience, who must experience the prototype as a real game world.

## Where — Delivery
- React frontend, consuming the existing Fastify backend on port 3000.
- No backend modifications — frontend surface only.

## What — The Idea
**Goals:**
- Deliver a visual town scene: a rendered location where NPCs appear as clickable figures.
- Let the player click an NPC to open a dialogue UI driven by the backend's existing dialogue engine.
- Continue the minimal armor/leather design language established by the Equinox HUD.

**Requirements:**
- Town scene renders as a visual location, not a menu or list.
- NPCs are clickable figures within the scene.
- Dialogue UI presents available choices and plays out conversations from the backend dialogue engine.
- Personality-gated choices are handled invisibly — no "locked" markers, no visible gate indicators. Gating surfaces only through context clues and subtlety; the player should never perceive a mechanical lock.

**Constraints:**
- Consume existing REST API without backend changes.
- Immutable state pattern (ADR-005); ESM modules with .js extensions (ADR-016).
- Aesthetic must match the armor/leather HUD design language from Sprint 5.

**Out of scope:**
- Combat UI / action declaration (Sprint 7).
- Backend code changes (dialogue engine and narrative systems already exist from Sprints 1 and 3).
- Deployment / CI-CD.

## Why — Branch Goal
The player can walk into town, approach the people in it, and have conversations that feel authored to who their character is — where their personality shifts the texture of an exchange without ever being announced.

## Decision Posture
Creative choices — the user weighs in on how it looks, feels, and reads (town scene visuals, NPC presentation, dialogue UX, the subtlety of personality cues). Technical choices delegated unless they affect the creative experience.

## Open Questions
- NPC roster for the town and how the town scene connects to Act 1 scene progression (Sprint 3 narrative state machine) — to be settled in compose/design.
