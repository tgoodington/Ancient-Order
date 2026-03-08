# Sprint 4 Orientation Research

## Codebase Topology

### Directory Structure
- `src/` — All TypeScript source, organized by domain module
- `src/api/` — Fastify route plugins: game, player, npc, dialogue, combat, narrative
- `src/combat/` — Full combat engine with behavior tree AI
- `src/narrative/` — Scene engine, choice engine, synergy calculator, state machine
- `src/dialogue/` — Personality-gated dialogue branching
- `src/personality/` — 6-trait system (5-35% range, sum=100%)
- `src/state/` — Immutable state management, NPC templates, state updaters
- `src/persistence/` — JSON file save/load (listSaves, deleteSave already exist)
- `src/types/` — Shared TypeScript interfaces (leaf module, zero cross-deps)
- `src/fixtures/` — Test data and scene JSON
- `tests/` — Integration tests
- `docs/` — Reference docs and project notes

### Entry Points
- Server: `src/api/index.ts` (Fastify, dev via `tsx watch`)
- Build: `tsc` → `dist/`

### Test Infrastructure
- Vitest, ~35 test files co-located with source, 969 passing tests
- Pattern: `src/**/*.test.ts`

### Large Data Files
- `docs/Reference Documents/GM Combat Tracker.xlsx` — 769 KB (under 1 MB threshold)
- No files over 1 MB

## Codebase Patterns

### Architecture
- Plugin-based REST API (Fastify plugins per domain, registered in index.ts)
- Immutable state: all transitions produce new objects via spread
- Pure function modules with injectable dependencies (rollFn for determinism)
- Discriminated union results (narrative), thrown errors (combat exceptional cases)
- `ApiResponse<T>` generic wrapper: `{ success, data?, error? }`

### Conventions
- camelCase functions, PascalCase types, SCREAMING_SNAKE_CASE constants
- Named exports only, barrel index.ts files for types
- ESM with `.js` extensions on imports
- `ErrorCodes` const object (not enum, for ESM compat)
- Updater signature: `(state: Readonly<GameState>, ...args) => GameState`

### Dependency Flow
```
src/types/ ← leaf
  ↑
src/personality/, src/state/, src/persistence/, src/combat/, src/dialogue/, src/narrative/
  ↑
src/api/ ← sole integrator, no cross-domain imports
```

## Parent Intersection (Sprint 3 → Sprint 4)

### Shared Files
- `src/types/index.ts` — Sprint 3 added narrativeState to GameState, Sprint 4 reads it
- `src/persistence/saveLoad.ts` — Sprint 3 added listSaves/deleteSave + narrative validation; Sprint 4 wires API routes + deepens validation
- `src/state/stateUpdaters.ts` — Sprint 3 added narrative updaters; Sprint 4 adds team CRUD updaters
- `src/api/index.ts` — Sprint 3 registered narrativePlugin; Sprint 4 registers more plugins
- `src/state/npcs.ts` — NPC_TEMPLATES for Elena, Kade, Lars

### Binding Decisions from Sprint 3
- D2: All state mutations via canonical updater pattern in stateUpdaters.ts
- D5: Synergy reads from GameState at combat init; party size 3 hardcoded
- D6: All errors extend existing ErrorCodes const
- NarrativeState defaults to null on load (backward compat)

### Conflict Risks
- validateGameState() double-modification: Sprint 4 must extend, not overwrite Sprint 3's narrative validation
- Party size assumption: Sprint 3 synergy assumes 3 NPCs; Sprint 4 team CRUD must respect this
- Plugin registration order is load-bearing; Sprint 4 must not disrupt existing routes
