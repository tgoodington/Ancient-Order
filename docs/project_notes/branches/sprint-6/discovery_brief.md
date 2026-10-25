# Discovery Brief: Sprint 6 — Self-Contained Game Foundation

## Parent Context
- **Parent**: sprint-5 (Frontend React Phase 1 — HUD)
- **Inherited dimensions**: None unchanged. ADR-055 (2026-10-23) reframed the project from pitch prototype to shipping game.
- **Changed dimensions**:
  - Who: players are now the primary audience. Investors and publishers are no longer the target.
  - Where: the game runs on the player's machine with no server, instead of a React app calling Fastify on port 3000 and deploying to Vercel.
  - What: this sprint is the platform move. The town and dialogue were rescoped to Sprint 7, and combat UI moved to Sprint 8.
  - Why: new Branch Goal.
- **Project North Star**: Amended this session

## Who — Stakeholders
- Players (primary): should notice nothing different. Everything they can do today works the same way.
- Designer/director: a simpler setup to run, with one dev server instead of two.

## Where — Delivery
- Runs entirely in the browser on the developer's machine, with no server.
- No hosted or shareable link this sprint. The desktop (Steam) build comes in a later sprint.

## What — The Idea
**Goals:**
- Run the game engine (combat, narrative, personality, dialogue, state) inside the client.
- Retire the Fastify server from the game's runtime.
- Move saves from server disk files to local storage.
- Keep the Equinox HUD working exactly as it does now.
- Set up the scene renderer with an empty scene, ready for the Sprint 7 town.

**Requirements:**
- No gameplay changes.
- The engine's tests keep passing.
- Save and load still work.

**Constraints:**
- Follows ADR-055.
- Immutable state is preserved.
- The renderer choice (Phaser vs PixiJS) is made in the design phase.

**Out of scope:**
- Town scene and dialogue UI (Sprint 7).
- Combat UI (Sprint 8).
- Art.
- Desktop/Steam packaging.
- A hosted web link.

## Why — Branch Goal
Ancient Order runs as a self-contained game on the player's own machine. Each player owns their world and their saves, everything that worked before still works the same, and the game stands on a foundation built to ship.

## Decision Posture
Both: the designer wants to see all major creative and technical decisions.

## Open Questions
- None
