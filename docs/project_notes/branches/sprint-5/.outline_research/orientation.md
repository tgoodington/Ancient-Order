# Orientation Research — Sprint 5

## Codebase Topology

**Top-Level Structure:**
- `src/` — TypeScript source (api/, state/, types/, personality/, dialogue/, narrative/, combat/, persistence/, fixtures/)
- `dist/` — Compiled JS output
- `docs/` — Reference docs and project notes/memory
- `tests/` — Integration/E2E tests
- `saves/` — JSON file-based game saves

**Key Modules:**
- src/api/ — Fastify REST plugins (game, player, npc, dialogue, combat, narrative)
- src/state/ — Immutable state management and updaters
- src/types/ — TypeScript interfaces (barrel pattern via index.ts)
- src/personality/ — 6-trait system with 5-35% range, sum=100%
- src/dialogue/ — Personality-driven dialogue engine
- src/narrative/ — Scene engine, state machine, choice system, synergy
- src/combat/ — Turn-based combat with behavior tree AI
- src/persistence/ — JSON file save/load

**Entry Points:**
- Main: src/api/index.ts (Fastify app factory)
- Start: npm start → dist/api/index.js
- Dev: npm run dev → tsx watch src/api/index.ts

**Test Infrastructure:**
- Vitest, 30+ test files, colocated (src/**/*.test.ts) + integration (tests/)
- 1019 tests passing as of Sprint 4

**Build System:**
- TypeScript 5.3, strict mode, ES2022 target, NodeNext modules
- ESLint + Prettier, tsx for dev hot-reload

**Large Data Files:**
- GM Combat Tracker.xlsx — 769 KB (under 1 MB threshold, no preprocessing needed)

## Codebase Patterns

**Architecture:** Pure functional + immutable state, Fastify plugin system, layered (API → Domain → Types)
- All state updates create new objects (spread operator, Readonly<T>)
- Zero side effects in domain logic
- Unidirectional state flow

**Conventions:**
- Named exports, .js ESM extensions, barrel pattern for types
- camelCase functions, UPPER_SNAKE_CASE constants, PascalCase types
- Colocated tests adjacent to implementation
- Global error handler → standardized ApiResponse<T> envelope

**Key Abstractions:**
- GameState root object, Personality (6 traits), CombatState, ActionResult
- validatePersonality(), adjustPersonality() (3-pass normalization)
- Combat formula suite (pure functions), behavior tree evaluator
- GameStateContainer decorator for shared session state across plugins

**Dependencies:**
- Types are leaf module (no domain imports)
- API plugins import domain logic
- No circular dependencies
- Plugin-to-plugin communication via shared GameStateContainer

## Parent Intersection (Sprint 4 → Sprint 5)

**Shared Components:**
- src/api/index.ts — Sprint 4 registered endpoint plugins; Sprint 5 calls all endpoints from React
- src/types/index.ts — Sprint 4 validated GameState shape; Sprint 5 GameStateProvider uses these types
- src/persistence/saveLoad.ts — Sprint 4 wired list/delete saves; Sprint 5 consumes these endpoints
- ApiResponse<T> envelope — Sprint 4 standardized; Sprint 5 frontend parses this contract

**Inherited Decisions:**
- D2: Immutable state pattern (state: Readonly<GameState>) → frontend must mirror
- ADR-016: ESM modules with .js extensions
- Backend on port 3000 → React needs dev proxy/CORS
- Party size = 3 locked, personality 6-trait/5-35%/sum=100% enforced by backend

**Conflicts:** None. Sprint 5 has explicit "No backend modifications" constraint.

**Patterns to Reuse:**
- ApiResponse<T> parsing in frontend
- Immutable state updates in React context
- Modular organization (separate context providers per domain)
