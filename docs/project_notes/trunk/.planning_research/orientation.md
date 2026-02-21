# Orientation Research — Ancient Order Sprint 1+2 Rebuild

## Codebase State

- `src/` is **currently empty** — all Sprint 1 prototype code has been archived to `docs/project_notes/archive/src-sprint1-prototype/`
- This is a true rebuild from an empty codebase
- `dist/` does not exist (no prior build output)
- `saves/` directory exists (runtime JSON saves)

## Directory Structure

```
.claude/          - Claude Code settings
docs/
  Reference Documents/  - Sprint specs, combat tracker docs + Excel, handoff doc
  project_notes/
    trunk/        - Workflow memory (discovery brief, planning artifacts)
    archive/      - src-sprint1-prototype/ (archived reference patterns)
    branches/     - (empty)
src/              - EMPTY (target for rebuild)
saves/            - Runtime JSON save slots
package.json, tsconfig.json, CLAUDE.md, AGENTS.md
```

## Archived Prototype (Reference Patterns, Not Code to Copy)

Archived at `docs/project_notes/archive/src-sprint1-prototype/`:
- `types/index.ts` — All interfaces: Personality, PlayerCharacter, NPC, DialogueNode/Option, GameState, CombatState, API types, ErrorCodes
- `personality/personalitySystem.ts` — 6-trait enforcement (5-35%, sum=100%)
- `state/gameState.ts` — createNewGameState, createPlayerCharacter, getGameVersion
- `state/npcs.ts` — NPC templates (Elena, Lars, Kade)
- `state/stateUpdaters.ts` — Immutable state transition library
- `dialogue/dialogueEngine.ts` — Personality gate evaluation, dead-end validation
- `persistence/saveLoad.ts` — JSON file save/load, slots 1-N
- `api/index.ts` — Express app factory, mounts 4 routers
- `api/game.ts`, `api/player.ts`, `api/npc.ts`, `api/dialogue.ts` — Route handlers

## Architecture Pattern (from archived prototype)

**Layered REST API architecture**, no MVC, no classes:
- `types/` — Zero-dependency pure type definitions
- `personality/`, `state/` — Domain logic layer (pure functions)
- `dialogue/` — Dialogue engine (pure functions on GameState)
- `persistence/` — File I/O layer
- `api/` — Thin Express router handlers

**State management:** Explicit immutable via spread operators (`{ ...state, ... }`). Convention-enforced, no library.
**Session state:** `api/game.ts` acts as in-memory singleton via `getActiveGameState`/`setActiveGameState`; all routers import from it.

## Dependency Flow

```
types/ (leaf, zero deps)
  ↑
personality/personalitySystem
  ↑
state/{gameState, npcs, stateUpdaters}, dialogue/, persistence/
  ↑
api/*
```

## Coding Conventions

- **Naming:** camelCase for files/functions/variables; PascalCase for types/interfaces; SCREAMING_SNAKE_CASE for error code constants
- **Exports:** Named `export function` declarations only; no default exports; root `index.ts` re-exports all public surface
- **TypeScript:** Strict mode, ES2020 target, CommonJS modules
- **Comments:** Section dividers (`// ===...===`) within files
- **Generic envelope:** `ApiResponse<T>` wraps all REST responses

## Build & Tooling

- **Compiler:** TypeScript 5.3, `tsc` only (no bundler)
- **Dev runner:** `ts-node` 10.9 (`npm run dev` → `ts-node src/api/index.ts`)
- **Production:** `tsc` → `node dist/api/index.js`
- **Server:** Express 4.18, port 3000 (overridable via PORT env)
- **Tests:** Jest named in `package.json` scripts but NOT installed; no test files exist anywhere

## Key Gaps for Planning

1. **Test framework:** Jest listed but not installed; must decide and install fresh
2. **No linting/formatting config** — may want to add ESLint/Prettier for new build
3. **No error handling conventions** documented (archived code used try/catch in API layer)
4. **Sprint 2 combat types** are partially defined in archived `types/index.ts` (CombatState) but combat engine modules don't exist in archive — they were planned but not implemented in Sprint 1 prototype
