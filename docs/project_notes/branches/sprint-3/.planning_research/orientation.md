# Sprint 3 Planning Orientation Research

## Codebase Topology

**Structure:** Layered plugin architecture — types (leaf) → domain logic (personality, state, dialogue, combat) → persistence → API (Fastify plugins).

**Key modules:**
- `src/types/` — index.ts (Sprint 1), combat.ts (Sprint 2)
- `src/personality/` — 6-trait system, pure functions
- `src/state/` — gameState, npcs (Elena/Lars/Kade), stateUpdaters (immutable transitions)
- `src/dialogue/` — personality-gated dialogue engine
- `src/persistence/` — JSON file save/load, 10 slots
- `src/combat/` — formulas, defense, counterChain, elementalPaths, energy, pipeline, roundManager, sync, groupAction, behaviorTree/ (utility-scoring AI)
- `src/api/` — Fastify plugins: game, player, npc, dialogue, combat
- `src/fixtures/` — encounter.json

**Entry point:** `src/api/index.ts` (Fastify buildApp)
**Tests:** 24 co-located .test.ts files, Vitest framework
**Build:** TypeScript 5.3, ES2022, NodeNext ESM, tsc → dist/

## Pattern Extraction

**Architecture:** Layered plugins, not MVC. Fastify uses `GameStateContainer` decorated on instance for shared session state.
**Combat AI:** Utility-scoring system (despite behaviorTree directory name). Evaluator generates (actionType, target) pairs, scores via weighted factors from archetype profiles.
**Conventions:** camelCase files, named exports only, `_` prefix for private helpers, `.js` extensions in imports (ESM), `const` objects (not enums), roll injection via optional `rollFn` parameter.
**Immutability:** All state transitions via spread operators, never mutating input.
**State updaters:** updateTimestamp, updatePlayerPersonality, applyPersonalityAdjustment, updateNPCAffection, updateNPCTrust, updateNPCRelationship, processDialogueChoice, updateCombatState.

## Parent Intersection Analysis

**Shared components:** npcs.ts, saveLoad.ts, types/index.ts (GameState), stateUpdaters.ts, api/ plugins, groupAction.ts (synergy overlap)
**Binding decisions from parent:**
- Fastify (not Express)
- Vitest for tests
- JSON persistence in saves/, 10 slots (ADR-005)
- REST API (ADR-006)
- Immutable state (ADR-012, ADR-013)
- CombatState independent from GameState, synced at boundary (ADR-013)
- Static JSON fixtures for game data

**Key conflicts/dependencies:**
- GameState needs extension for narrative fields (scenes, choice history) — additive but must not break Sprint 1+2 types
- Synergy overlap: parent Task 18 Group Action has "synergy effects" vs Sprint 3 team synergy bonuses — need explicit boundary
- Narrative state must serialize through existing saveGame/loadGame JSON interface
- Fastify session singleton pattern (GameStateContainer) already resolved in implementation

**Reusable patterns:** Pure function state transitions, Fastify plugin registration, co-located tests, static JSON fixtures, TDD for formula logic
