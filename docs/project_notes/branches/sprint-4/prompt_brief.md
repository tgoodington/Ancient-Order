# Prompt Brief: Sprint 4 — Persistence & API Completion

## Problem Statement
Sprint 4 is the final backend sprint before React frontend work begins (Sprints 5-7). The codebase already has ~20 of ~28 endpoints and all core persistence functions implemented across Sprints 1-3, but ~8 API endpoints are missing, 2 persistence functions lack API exposure, and state validation on save/load is shallow. The backend API surface must be complete and hardened so frontend developers encounter predictable contracts and bulletproof state integrity.

## Commander's Intent
**Desired end state:** A frontend developer can hit any endpoint and get predictable, well-shaped responses. Any saved game loads back into a valid, uncorrupted state. No gaps in the API surface.

**Non-negotiables:** Consistent API contracts (same `ApiResponse<T>` envelope, clear error codes) AND bulletproof state integrity (deep validation on load, no personality math drift, no orphaned state)

**Boundaries:** Tight scope — wire missing endpoints, harden validation. No new features like auto-save, metadata enrichment, or session management.

## Success Criteria
- All spec'd endpoints implemented and returning consistent `ApiResponse<T>` shapes
- `listSaves()` and `deleteSave()` exposed via API routes
- Player team, personality GET, and NPC state endpoints wired up
- Save/load validates personality constraints (5-35% range, sum=100%), combat state, and narrative state deeply
- Existing endpoint behavior unchanged — no regressions

## Scope
**In scope:**
- Wire ~8 missing API endpoints (saves list/delete, player team CRUD, personality GET, NPC state)
- Expose existing `listSaves()` and `deleteSave()` persistence functions as API routes
- Deepen `validateGameState()` — personality math verification, combat state shape validation, narrative state integrity
- Maintain existing `ApiResponse<T>` envelope and error code patterns across all new endpoints

**Out of scope:**
- Auto-save functionality
- Save metadata enrichment (playtime, location, character level)
- Save file versioning or migration system
- Session management or multi-game support
- API documentation generation
- Integration test infrastructure (test phase determines coverage)
- Frontend work
- Redesigning existing endpoint patterns

## Constraints
- Fastify + TypeScript + ESM stack (established across 3 sprints)
- Immutable state management pattern (all state updates create new objects)
- Personality system: 6 traits, 5-35% range, sum always = 100%
- Save slots: 1-10 (hardcoded, file-based JSON persistence)
- NPC archetypes are fixed (Elena, Kade, Lars)

## Key Assumptions
| Assumption | Confidence | Basis |
|-----------|-----------|-------|
| Existing endpoint patterns are correct and should be extended, not redesigned | High | 3 sprints of consistent patterns, 969 passing tests |
| Sprint 1 API Reference spec is still the target for missing endpoints | Medium | May need updates for Sprint 2-3 system additions |
| Combat and narrative state shapes are stable enough to validate deeply | High | Sprint 2-3 types are well-tested |
| Party size is 3 (player + 2 NPCs) based on combat system | High | Combat is 3v3, established in Sprint 2 |

## Open Questions for Planning
- Are there Sprint 2/3 endpoints that should have been in the spec but aren't? (combat/narrative may have expanded beyond Sprint 1 API Reference)
- Should `validateGameState()` attempt repair on load (normalize invalid state) or reject outright?
- What NPC state data should the NPC state endpoint return beyond affection/trust?

## Decision Posture
| Area | Posture | Notes |
|------|---------|-------|
| Team composition rules | I decide | How player team endpoint works — max party size, NPC selection constraints |
| Endpoint shapes & contracts | Show me options | Specialist recommends request/response formats, user approves |
| Validation behavior on load | Show me options | Specialist recommends reject vs. repair strategy, user approves |
