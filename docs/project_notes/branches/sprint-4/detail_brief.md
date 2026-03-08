# Detail Brief

## Current Specialist
- **Name**: game-backend-engine
- **Display Name**: Game Backend Engine
- **Domain**: code/backend
- **Profile Path**: C:/Users/taylo/.claude/specialists/game-backend-engine/game-backend-engine.specialist.md

## Assigned Tasks

### Task T1: Wire saves management endpoints
- **Depth**: Light
- **Description**: Add GET `/api/game/saves` and DELETE `/api/game/saves/:slot` routes that call the existing `listSaves()` and `deleteSave()` functions from `src/persistence/saveLoad.ts`. Follow the established Fastify plugin pattern.
- **Acceptance Criteria**:
  1. GET `/api/game/saves` returns `ApiResponse<T>` with list of saved game slots and their metadata
  2. DELETE `/api/game/saves/:slot` returns `ApiResponse<T>` confirming deletion, or error for invalid/nonexistent slot
  3. Both endpoints use consistent error codes from `ErrorCodes`
  4. Existing save/load endpoints remain unchanged
- **Dependencies**: None
- **Files**: `src/api/game.ts`, `src/persistence/saveLoad.ts`

### Task T2: Wire player and NPC state endpoints
- **Depth**: Standard
- **Description**: Add GET `/api/player/personality` (read-only personality traits), POST `/api/player/team` (set team composition), and ensure GET `/api/npc/:id` returns complete NPC state including relationship data (affection/trust). Add a state updater for team composition following the canonical pattern.
- **Acceptance Criteria**:
  1. GET `/api/player/personality` returns `ApiResponse<T>` with current personality trait values
  2. POST `/api/player/team` accepts NPC selection and returns updated team composition
  3. Team composition respects party size constraint (player + 2 NPCs from the 3 available)
  4. GET `/api/npc/:id` response includes affection and trust relationship data alongside base NPC info
  5. New state updater follows `(state: Readonly<GameState>, ...args) => GameState` pattern
- **Dependencies**: None
- **Decisions**:
  - `[USER]` Team composition rules — max party size, whether all 3 NPCs must be selectable, whether team can be changed mid-game or only at certain points
- **Files**: `src/api/player.ts`, `src/api/npc.ts`, `src/state/stateUpdaters.ts`, `src/types/index.ts`

### Task T3: Deepen state validation
- **Depth**: Light
- **Description**: Extend `validateGameState()` to deeply validate personality constraints, combat state shape, and narrative state integrity. Invalid state on load is rejected with a typed error — no repair attempts. Preserve existing Sprint 3 narrative validation.
- **Acceptance Criteria**:
  1. Personality validation checks: 6 traits present, each in 5-35% range, sum equals 100%
  2. Combat state validation checks required shape when `combatState` is not null (valid combatants, round structure)
  3. Narrative state validation checks scene graph references and flag consistency when `narrativeState` is not null
  4. Rejection returns typed error with specific validation failure details (which field, what was wrong)
  5. Existing Sprint 3 narrative validation logic preserved, not overwritten
- **Dependencies**: None
- **Files**: `src/persistence/saveLoad.ts`, `src/types/index.ts`

## Decision Policy
Sole developer, hands-on. Team composition rules in T2 are `[USER]` — surface options during detail. All other decisions can be handled autonomously following established patterns.

## Known Research

### Discovery Summary (Section 2)
- **Problem**: ~4 API endpoints missing from spec'd 12 (saves list/delete, personality GET, team POST). State validation on load is shallow.
- **Goals**: Complete API surface, bulletproof save/load integrity, consistent error contracts.
- **Target users**: Frontend developers (Sprints 5-7), players (save integrity), demo audience.
- **Constraints**: Fastify + TypeScript + ESM, immutable state, personality 6-trait/5-35%/sum=100%, party size 3, NPC archetypes fixed (Elena, Kade, Lars).
- **Key finding**: `listSaves()` and `deleteSave()` already exist in `src/persistence/saveLoad.ts` — only API route wiring needed. Sprint 3 added narrative validation to `validateGameState()` — Sprint 4 deepens it without overwriting.

### Parent Context (Section 2.5)
- **Parent**: sprint-3 — narrative progression layer, scene graph engine, personality-gated choices, team synergy system
- **Shared Components**:
  - `src/persistence/saveLoad.ts`: Sprint 3 added `listSaves()`/`deleteSave()` + narrative validation
  - `src/state/stateUpdaters.ts`: Sprint 3 added narrative updaters; Sprint 4 adds team composition updater
  - `src/api/index.ts`: Sprint 3 registered narrativePlugin; Sprint 4 registers additional plugins
  - `src/types/index.ts`: Sprint 3 added `narrativeState` to `GameState`; Sprint 4 reads it for validation
- **Inherited Decisions**:
  - D2: All state mutations via canonical updater pattern `(state: Readonly<GameState>, ...args) => GameState`
  - D5: Synergy reads from GameState at combat init; party size 3 is assumed
  - D6: All errors extend existing `ErrorCodes` const object
- **Intersection Points**:
  - `validateGameState()` — Sprint 4 must extend Sprint 3's narrative validation, not overwrite it
  - Plugin registration in `src/api/index.ts` — must not disrupt existing route registration order

### Risks (Section 8 equivalent — from Outline Section 10)
- T3 validation must not overwrite Sprint 3 narrative validation already in `validateGameState()`
- T2 team updater may add fields to `GameState` that T3 should validate — coordinate if both run simultaneously
- Plugin registration order in `src/api/index.ts` must not disrupt existing routes
- Zero regressions on existing 969 tests

### Open Questions
- [code/backend] What NPC state data should GET `/api/npc/:id` return beyond base info + affection/trust? (specialist determines based on existing NPC type)
- [code/backend] Should saves list endpoint return full GameState summaries or just slot metadata? (specialist determines based on `listSaves()` return type)

## Prior Blueprints
None (first and only specialist in execution order)

## Outline Context (Section 10)
- **Domain-Specific Considerations**: All work is code/backend in the existing Fastify+TypeScript+ESM stack. Follow established patterns: Fastify plugin registration, `ApiResponse<T>` envelope, `ErrorCodes` const, named exports, `.js` import extensions, immutable state updaters.
- **Cross-Domain Dependencies**: Task 2's team composition updater must be compatible with Sprint 3's synergy system (which assumes party size 3 at combat init). Task 3's validation must account for all state added in Sprints 1-3.
- **Sequencing Considerations**: Tasks are independent and can execute in parallel. Task 3 does not depend on Tasks 1-2. However, Task 2's team updater may add fields to GameState that Task 3 should validate — coordinate if both run simultaneously.
- **Constraints**: No new features beyond endpoint wiring and validation. No auto-save, versioning, or session management. Must not break existing 969 tests.
- **Decision Policy**: Sole developer, hands-on. Team composition rules are `[USER]` — surface options during detail. All other decisions can be handled autonomously following established patterns.

## Detail Queue
- [in_progress] Game Backend Engine (T1, T2, T3)
