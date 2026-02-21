# Plan: Ancient Order Sprint 1 + Sprint 2 Rebuild

**Plan-Execute Contract v1.0 | Tier: Comprehensive**
**Date:** 2026-02-21

---

## 1. Objective

Rebuild the Ancient Order backend from an empty `src/` directory, implementing Sprint 1 (personality-driven narrative systems) and Sprint 2 (turn-based combat engine), producing a working backend sufficient for an investor/publisher pitch demo.

Success is measured by a single criterion: a 3v3 combat encounter that initializes, resolves through all 5 pipeline phases across multiple rounds, and produces per-attack formula outputs that exactly match the Excel source of truth. Sprint 1 is a prerequisite foundation, not the demo deliverable — Sprint 2 is the pitch.

---

## 2. Discovery Summary

- **Problem:** Complete backend rebuild from empty codebase. Prior Sprint 1 prototype is archived as reference — patterns are reusable but code is not to be copied. Sprint 2 combat engine was never implemented.
- **Goals:** Sprint 1 narrative stack operational; Sprint 2 combat engine operational with all Excel formulas ported exactly; 3v3 demo encounter resolves correctly.
- **Target users:** Solo developer (you); publisher/investor demo audience.
- **Constraints (game design — fixed):**
  - Personality: 6 traits, each 5-35%, sum always = 100%
  - NPCs: Fixed archetypes (only player personality changes)
  - Dialogue: No dead ends (all gates have ungated fallback)
  - Combat formulas: Exact replicas from `GM Combat Tracker.xlsx` — no modifications during porting
- **Key findings from orientation:**
  - `src/` is empty; archived prototype at `docs/project_notes/archive/src-sprint1-prototype/` provides pattern reference
  - No tests exist anywhere; Jest is listed in `package.json` but not installed — clean slate for tooling decisions
  - Archived code uses layered REST architecture with pure functions and spread-operator immutability
  - No ESLint/Prettier config exists; TypeScript strict mode is configured

---

## 3. Technology Decisions

| Decision | Choice | Status | Rationale |
|----------|--------|--------|-----------|
| HTTP Framework | Fastify | Locked | TypeScript-native, built-in JSON Schema validation. Archived Express patterns will need adapting to Fastify plugin architecture. |
| Test Framework | Vitest | Locked | Native TypeScript (no ts-jest config needed), fast watch mode critical for TDD formula porting, Jest-compatible API. |
| Build Sequencing | Linear: Sprint 1 → Sprint 2 | Locked | Sprint 1 establishes the state management foundation Sprint 2 extends; tested base before combat engine build begins. |
| Persistence | JSON files, `saves/` directory, 10 slots | Locked (ADR-005) | Established decision from prior sprint. |
| API Style | REST | Locked (ADR-006) | Established decision; Fastify replaces Express as the framework. |
| Combat Formulas | Exact Excel replicas | Locked (ADR-007) | Non-negotiable game design constraint. |
| Information Asymmetry | AI decides before player sees | Locked (ADR-008) | Core tactical mechanic; Phase 1 AI decisions hidden from player until Phase 2. |
| Combat Architecture | 5-phase pipeline, pure functions | Locked (ADR-012) | Each phase is a pure function. |
| CombatState | Independent from GameState, sync at round boundary | Locked (ADR-013) | Clean separation; GameState ↔ CombatState sync as explicit step. |
| Behavior Tree AI | Required; design session before implementation | Locked (ADR-014) | NPC decision model needs collaborative design. |
| Formula Porting Method | Test-driven (write test with Excel value, then implement) | Locked (ADR-015) | Prevents silent formula errors; Excel is source of truth. |
| Immutability Mechanism | Deferred to engineering | Open | Planning constraint: all state transitions produce new objects. Mechanism (Readonly<>, Immer, or spread convention) is engineering decision. User preference: Spread + Readonly<> if engineer needs direction. |
| Combat Encounter Config | Static JSON fixture | Recommended | Demo needs one fixed 3v3 encounter. Dynamic enemy configuration adds complexity for no demo value. Source: Excel's "Battle Scenarios" sheet. |

---

## 4. Component Architecture

Two sprint layers sharing a type foundation. Sprint 2 components extend (not replace) Sprint 1 state and API.

### Sprint 1: Narrative Stack

```
src/
├── types/
│   └── index.ts              [LEAF] All Sprint 1 interfaces and constants — zero deps
├── personality/
│   └── personalitySystem.ts  Trait enforcement, normalization, adjustment — imports: types
├── state/
│   ├── gameState.ts          Initial state creation — imports: types, personality, npcs
│   ├── npcs.ts               NPC templates (Elena, Lars, Kade) — imports: types
│   └── stateUpdaters.ts      Immutable state transition library — imports: types, personality
├── dialogue/
│   └── dialogueEngine.ts     Tree traversal, gate evaluation, dead-end validation — imports: types, stateUpdaters
├── persistence/
│   └── saveLoad.ts           JSON file save/load, 10 slots — imports: types
└── api/
    ├── index.ts              Fastify app factory, registers all plugins — imports: api/*
    ├── game.ts               Session state singleton, /api/game routes — imports: types, state/*
    ├── player.ts             /api/player routes — imports: types, state/*, game
    ├── npc.ts                /api/npc routes — imports: types, state/npcs, game
    └── dialogue.ts           /api/dialogue routes — imports: types, dialogue/, game
```

**Dependency flow (Sprint 1):**
```
types (leaf)
  ↑
personality, state/npcs
  ↑
state/gameState, state/stateUpdaters
  ↑
dialogue, persistence
  ↑
api/* (thin Fastify plugins)
```

### Sprint 2: Combat Engine

```
src/
├── types/
│   └── combat.ts             All combat interfaces extending Sprint 1 types — imports: types/index
└── combat/
    ├── formulas.ts           All Excel formula implementations (pure functions) — imports: combat types
    ├── defense.ts            Block/Dodge/Parry resolution with SR/SMR/FMR — imports: formulas
    ├── counterChain.ts       Parry counter chain detection and resolution — imports: defense
    ├── elementalPaths.ts     6 elemental paths, buff/debuff application — imports: formulas
    ├── energy.ts             Energy segment tracking, ascension level management — imports: formulas
    ├── declaration.ts        Player action declaration validation — imports: combat types
    ├── pipeline.ts           Per-attack resolution pipeline (5 sub-steps) — imports: defense, counterChain, elementalPaths, energy
    ├── roundManager.ts       5-phase round orchestrator — imports: pipeline, declaration, behaviorTree (stub → real)
    ├── behaviorTree/         [DESIGN REQUIRED] NPC decision making by archetype
    ├── groupAction.ts        [DESIGN REQUIRED] Group action type resolution
    ├── integration.ts        Wires behaviorTree + groupAction into roundManager pipeline
    └── sync.ts               GameState ↔ CombatState round-boundary synchronization
```
```
src/api/
└── combat.ts                 Fastify combat plugin: /api/combat routes
```

**Dependency flow (Sprint 2):**
```
types/combat (leaf, imports types/index)
  ↑
combat/formulas
  ↑
combat/defense, combat/counterChain, combat/elementalPaths, combat/energy
  ↑
combat/declaration, combat/pipeline
  ↑
combat/roundManager (uses stubs initially)
  ↑
combat/behaviorTree [DESIGN], combat/groupAction [DESIGN]
  ↑
combat/integration (wires real implementations)
  ↑
combat/sync
  ↑
api/combat
```

---

## 5. Interface Contracts

Public contracts between major components. Internal implementation details are engineering decisions.

### Root State Types

```typescript
// types/index.ts (core contracts)
interface Personality { /* 6 traits: patience, empathy, cunning, logic, kindness, charisma */ }
interface PlayerCharacter { id: string; name: string; personality: Personality; /* ... */ }
interface NPC { id: string; archetype: string; personality: Personality; /* fixed */ }
interface GameState {
  player: PlayerCharacter;
  npcs: NPC[];
  currentDialogueNode: string | null;
  saveSlot: number | null;
  combatState: CombatState | null;  // null when not in combat
}
```

### Combat State Types

```typescript
// types/combat.ts (combat contracts)
interface CombatState {
  round: number;
  phase: CombatPhase;
  playerParty: Combatant[];
  enemyParty: Combatant[];
  actionQueue: CombatAction[];
  roundHistory: RoundResult[];
  status: 'active' | 'victory' | 'defeat';
}
interface Combatant { id: string; stamina: number; maxStamina: number; energy: number; ascensionLevel: number; activeBuffs: Buff[]; /* ... */ }
interface CombatAction { combatantId: string; type: ActionType; targetId: string | null; energySegments?: number; }
type ActionType = 'ATTACK' | 'DEFEND' | 'EVADE' | 'SPECIAL' | 'GROUP';
type CombatPhase = 'AI_DECISION' | 'VISUAL_INFO' | 'PC_DECLARATION' | 'ACTION_RESOLUTION' | 'PER_ATTACK';
```

### Pipeline Interfaces

```typescript
// combat/pipeline.ts — pure function, each phase in/out
function resolvePerAttack(state: CombatState, action: CombatAction): CombatState
function resolvePriority(actions: CombatAction[], state: CombatState): CombatAction[]

// combat/roundManager.ts — round orchestrator
function runRound(state: CombatState, playerDeclarations: CombatAction[]): CombatState

// combat/behaviorTree/ [DESIGN REQUIRED — interface TBD in design spec]
// Must produce: CombatAction[] for all enemy combatants given CombatState
interface BehaviorTreeEvaluator {
  evaluate(combatant: Combatant, state: CombatState): CombatAction;
}

// combat/sync.ts — bidirectional sync
function syncToGameState(gameState: GameState, combatState: CombatState): GameState
function initCombatState(gameState: GameState, encounter: EncounterConfig): CombatState
```

### Persistence Interface

```typescript
// persistence/saveLoad.ts
function saveGame(state: GameState, slot: number): Promise<void>
function loadGame(slot: number): Promise<GameState>
function listSaves(): Promise<SaveSlotInfo[]>
function deleteSave(slot: number): Promise<void>
```

### Fastify Plugin Pattern

```typescript
// Each api/*.ts exports a Fastify plugin (not an Express router factory)
async function gamePlugin(fastify: FastifyInstance, opts: object): Promise<void>
// Registered via: fastify.register(gamePlugin, { prefix: '/api/game' })
```

---

## 6. Task Sequence

### Task 1: Project Foundation & Tooling
- **Component**: Root configuration
- **Description**: Set up the complete project scaffolding — Fastify, Vitest, TypeScript, directory structure, npm scripts — so all subsequent tasks have a working build environment.
- **Acceptance Criteria**:
  1. `npm run dev` starts a Fastify server on port 3000 that responds to a health check endpoint (`GET /health` → `{ status: 'ok' }`)
  2. `npm test` runs Vitest with zero failures
  3. `npm run build` compiles TypeScript to `dist/` with zero errors
  4. `src/` directory structure mirrors the component architecture in Section 4 (directories created, no source files yet)
- **Dependencies**: None
- **Files**: `package.json`, `tsconfig.json`, `vitest.config.ts`, `src/` (directory tree), `src/api/index.ts` (minimal health check only)

---

### Task 2: Sprint 1 Type System
- **Component**: `types/`
- **Description**: Define all TypeScript interfaces and constants required for the Sprint 1 narrative systems. This is the zero-dependency leaf all other Sprint 1 modules import from. Reference: `Sprint1_API_Reference.md` and `Sprint1_Technical_Reference.md` in `docs/Reference Documents/`.
- **Acceptance Criteria**:
  1. All Sprint 1 interfaces are defined: `Personality`, `PersonalityTrait`, `PersonalityGate`, `PlayerCharacter`, `NPC`, `GameState`, `DialogueNode`, `DialogueOption`, `DialogueResult`, `ApiResponse<T>`, `ApiError`, and `ErrorCodes`
  2. TypeScript strict mode compilation succeeds with all types imported in a validation file
  3. No circular imports within the types module
- **Dependencies**: Task 1
- **Files**: `src/types/index.ts`

---

### Task 3: Personality System
- **Component**: `personality/`
- **Description**: Implement the personality trait management system: 6 traits, each 5-35%, sum always = 100%. Includes trait validation, normalization, and adjustment functions. All functions are pure. Reference: `Sprint1_Technical_Reference.md`.
- **Acceptance Criteria**:
  1. Trait adjustment functions produce new personality objects without mutating input
  2. Sum-to-100% constraint is maintained after any adjustment (normalization distributes excess/deficit across other traits)
  3. Individual trait range enforcement: values below 5% are clamped to 5%, values above 35% are clamped to 35%, with redistribution to maintain 100% sum
  4. Unit tests cover: boundary cases (4.9% → 5%, 35.1% → 35%), valid adjustments, multi-trait interactions, and sum invariant
- **Dependencies**: Task 2
- **Files**: `src/personality/personalitySystem.ts`, `src/personality/personalitySystem.test.ts`

---

### Task 4: Game State & NPC System
- **Component**: `state/`
- **Description**: Implement `createNewGameState()` for initial game state creation and the NPC template system for the 3 test NPCs (Elena, Lars, Kade). NPC personalities are fixed archetypes. Reference: `Sprint1_API_Reference.md` and `Sprint1_ClaudeCode_Prompt.md`.
- **Acceptance Criteria**:
  1. `createNewGameState()` returns a valid `GameState` with all required fields populated, personality traits summing to 100%, and `combatState: null`
  2. All 3 NPC templates are retrievable by ID with correct archetype data (name, archetype, fixed personality)
  3. NPC personalities are immutable — no function in the NPC module can modify a template's personality
  4. Unit tests cover: initial state structure, NPC retrieval by ID, unknown NPC ID error handling
- **Dependencies**: Task 2, Task 3
- **Files**: `src/state/gameState.ts`, `src/state/npcs.ts`, associated test files

---

### Task 5: State Updaters Library
- **Component**: `state/`
- **Description**: Implement the library of pure immutable state transition functions. Every function signature is `(state: GameState, ...) => GameState`. This is the standard mechanism for all game state changes. Reference: `Sprint1_Technical_Reference.md`.
- **Acceptance Criteria**:
  1. All updater functions return a new `GameState` object (input reference unchanged after update)
  2. Updater functions are pure: identical inputs always produce identical outputs
  3. All Sprint 1 state mutations are represented as updater functions (no ad-hoc state modification elsewhere)
  4. Unit tests verify reference inequality (new object returned) and value correctness for each updater
- **Dependencies**: Task 3, Task 4
- **Files**: `src/state/stateUpdaters.ts`, `src/state/stateUpdaters.test.ts`

---

### Task 6: Dialogue Engine
- **Component**: `dialogue/`
- **Description**: Implement the dialogue tree traversal engine with personality gate evaluation and dead-end validation. Personality gates check a character trait against a threshold; gated options require sufficient trait value. All dialogue nodes must have at least one ungated option (no dead ends). Reference: `Sprint1_Technical_Reference.md`.
- **Acceptance Criteria**:
  1. Personality gates evaluate correctly: a character with insufficient trait value for a gated option receives the ungated fallback
  2. A character with sufficient trait value can access gated options
  3. Dead-end detection: a validation function identifies any dialogue node reachable through normal traversal that has zero ungated options
  4. Dialogue traversal produces a `DialogueResult` using stateUpdaters to update game state (immutable)
  5. Unit tests cover: gate pass, gate fail, fallback selection, dead-end validation on sample dialogue tree
- **Dependencies**: Task 3, Task 5
- **Files**: `src/dialogue/dialogueEngine.ts`, test data fixtures, associated test file

---

### Task 7: Persistence Layer
- **Component**: `persistence/`
- **Description**: Implement JSON file-based save/load with 10 addressable slots in the `saves/` directory. All save operations are async. Reference: `Sprint1_API_Reference.md` (save/load endpoints define the required operations).
- **Acceptance Criteria**:
  1. Save and load round-trip: `saveGame(state, slot)` followed by `loadGame(slot)` produces a state that is deeply equal to the original
  2. All 10 save slots (1-10) are independently addressable
  3. Loading a non-existent slot returns a typed error without crashing
  4. Save files are valid JSON readable outside the application
  5. Unit tests cover: round-trip fidelity, slot independence, missing-slot error handling
- **Dependencies**: Task 2
- **Files**: `src/persistence/saveLoad.ts`, `src/persistence/saveLoad.test.ts`

---

### Task 8: Sprint 1 REST API (Fastify)
- **Component**: `api/`
- **Description**: Implement the Fastify plugin layer for all Sprint 1 API endpoints. Register four route plugins: game, player, npc, dialogue. Each plugin is a thin handler that delegates to domain functions. Use Fastify's JSON Schema validation for request/response types. Reference: `Sprint1_API_Reference.md` for all endpoint specs.
- **Acceptance Criteria**:
  1. All Sprint 1 endpoints documented in `Sprint1_API_Reference.md` respond with correct status codes and `ApiResponse<T>` envelopes
  2. Invalid requests (malformed JSON, wrong types) return structured `ApiError` responses via Fastify schema validation, not unhandled exceptions
  3. Session state (active `GameState`) is accessible across all route plugins without prop-drilling
  4. Server starts cleanly and handles shutdown signals without hanging connections
- **Dependencies**: Task 5, Task 6, Task 7
- **Files**: `src/api/index.ts` (Fastify app factory), `src/api/game.ts`, `src/api/player.ts`, `src/api/npc.ts`, `src/api/dialogue.ts`

---

### Task 9: Sprint 1 Integration Validation
- **Component**: Cross-system
- **Description**: Validate that all Sprint 1 subsystems work together end-to-end through the API. This is integration-level testing — verifying the complete request flow from HTTP endpoint through domain logic to persistence and back, not just unit-level correctness.
- **Acceptance Criteria**:
  1. Complete session flow works end-to-end via API: `POST /api/game/new` → modify personality → traverse dialogue → `POST /api/game/save` → `POST /api/game/load` → loaded state equals saved state
  2. All Sprint 1 API endpoints tested with realistic payloads (not just happy-path)
  3. No unhandled promise rejections in any API path under integration test scenarios
  4. Personality constraint invariant maintained across all API operations that touch personality
- **Dependencies**: Task 8
- **Files**: `src/api/*.integration.test.ts` or `tests/sprint1.integration.test.ts`

---

### Task 10: Combat Type System
- **Component**: `types/combat.ts`
- **Description**: Define all TypeScript interfaces required for the Sprint 2 combat engine. Extends `GameState` from Sprint 1 types with `combatState: CombatState | null`. Reference: `GM_Combat_Tracker_Documentation.md` and `Sprint1_API_Reference.md` (CombatState interface sketch).
- **Acceptance Criteria**:
  1. All combat interfaces defined: `CombatState`, `Combatant`, `CombatAction`, `ActionType`, `CombatPhase`, `RoundResult`, `AttackResult`, `DefenseResult`, `Buff`, `DebuffEffect`, `ElementalPath`, `AscensionLevel`, `EncounterConfig`
  2. `GameState` extended (or augmented) with `combatState: CombatState | null`; Sprint 1 code compiles without changes
  3. All 5 action types enumerated: `ATTACK`, `DEFEND`, `EVADE`, `SPECIAL`, `GROUP`; all 4 priority levels represented
  4. No circular imports between combat types and Sprint 1 types
- **Dependencies**: Task 9
- **Files**: `src/types/combat.ts`

---

### Task 11: Combat Formula Suite (TDD)
- **Component**: `combat/formulas.ts`
- **Description**: Port all mathematical formulas from the GM Combat Tracker Excel. Each formula must be written test-first: write the test with an input/output pair derived from the Excel, then implement. Formulas are pure functions. Reference: `GM_Combat_Tracker_Documentation.md` (Math sheet, Paths sheet) and `GM Combat Tracker.xlsx` for specific values.

  Formulas to port:
  - Rank KO threshold: `((attackerRank - targetRank) × 3) / 10`; check: `(roll/20) >= (1 - threshold)`
  - Blindside threshold: `(attackerSpeed - targetSpeed) / targetSpeed`; same roll check
  - Crushing Blow threshold: `(actionPower - targetPower) / targetPower`; same roll check
  - Block success damage: `damage × (1 - SMR)`; block failure: `damage × (1 - FMR)`
  - Dodge failure damage: `damage × (1 - FMR)` (success = 0 damage)
  - Parry failure damage: `damage × (1 - FMR)` (success = counter attack)
  - Energy gain: by event type (Action/Reaction) and result (Success/Failure) per the energy table
  - Special damage bonus: `baseDamage × (1 + 0.10 × energySegmentsUsed)`
  - Evade regen: `maxStamina × 0.30`
  - Dynamic stat modifiers (from buff/debuff stack)
- **Acceptance Criteria**:
  1. Each formula function has a test written before implementation using Excel-derived input/output values
  2. Rank KO, Blindside, Crushing Blow threshold formulas produce correct results across the full range of plausible inputs (not just one case)
  3. Damage calculation formulas match Excel for all defense types (Block success/fail, Dodge success/fail, Parry success/fail, Defenseless)
  4. Energy gain table matches Excel for all 4 event/result combinations
  5. All tests pass without mocking (pure functions, deterministic)
- **Dependencies**: Task 10
- **Files**: `src/combat/formulas.ts`, `src/combat/formulas.test.ts`

---

### Task 12: Defense Resolution & Counter Chain System
- **Component**: `combat/defense.ts`, `combat/counterChain.ts`
- **Description**: Implement defense outcome resolution (Block/Dodge/Parry/Defenseless) using SR/SMR/FMR rates, and the Parry counter chain system. Defense resolution determines whether a defense succeeds and applies appropriate damage formula. Counter chain: a successful Parry inserts a counter attack into the action queue; the counter target may Parry again, creating an indefinite chain that terminates on Parry failure, KO, or stamina depletion.
- **Acceptance Criteria**:
  1. SR/SMR/FMR-based defense resolution produces correct outcomes for all four defense types (Block, Dodge, Parry, Defenseless)
  2. A successful Parry generates a counter `CombatAction` insertable into the resolution queue
  3. Counter chain terminates correctly in all three ending conditions: Parry failure, combatant KO, stamina depletion
  4. Defense outcomes are pure functions (given a defense type, rates, and a roll value, always produce the same result)
  5. Unit tests cover: each defense success case, each defense failure case, counter chain of length 2 and length 3, chain termination conditions
- **Dependencies**: Task 10, Task 11
- **Files**: `src/combat/defense.ts`, `src/combat/counterChain.ts`, associated test files

---

### Task 13: Elemental Path & Energy/Ascension Systems
- **Component**: `combat/elementalPaths.ts`, `combat/energy.ts`
- **Description**: Implement the 6 elemental paths (Fire, Water, Air, Earth, Shadow, Light) with their buff/debuff application logic, and the energy segment tracking + ascension level system. Reaction paths boost own defensive rates; Action paths debuff target defensive rates. Ascension advances at segment thresholds (35, 95, 180) and grants starting segments for next round.
- **Acceptance Criteria**:
  1. Each of the 6 elemental paths applies correct buff or debuff modifiers (Reaction: own rates up; Action: target rates down)
  2. Special technique defense constraint is enforced per path (each path forces a specific defense on the target)
  3. Energy segment accumulation matches the Excel table for all 4 event/result combinations
  4. Ascension level advances at correct thresholds; each level grants the correct starting segments for the next round
  5. Accumulation bonus (+25%/+25%/+50% at levels 1/2/3) is applied to energy gains
  6. Unit tests cover: each path's buff/debuff effect, ascension thresholds, energy accumulation with bonus applied
- **Dependencies**: Task 10, Task 11
- **Files**: `src/combat/elementalPaths.ts`, `src/combat/energy.ts`, associated test files

---

### Task 14: Player Declaration Validation
- **Component**: `combat/declaration.ts`
- **Description**: Implement the validation function that checks player-submitted action declarations before they enter the pipeline. Validates: action type is legal for the combatant's current state, target is valid (in-combat, not KO'd), stamina is sufficient, Special actions have available energy segments.
- **Acceptance Criteria**:
  1. Valid declarations for all 5 action types pass without error
  2. Invalid target (KO'd combatant, non-existent ID) is rejected with descriptive error
  3. Insufficient stamina for chosen action is rejected
  4. Special action with insufficient energy segments is rejected
  5. Validation is a pure function (same state + declaration → same result)
- **Dependencies**: Task 10
- **Files**: `src/combat/declaration.ts`, `src/combat/declaration.test.ts`

---

### Task 15: Action Priority & Resolution Pipeline
- **Component**: `combat/pipeline.ts`
- **Description**: Implement the per-attack resolution pipeline and action priority ordering. Priority order: 1=DEFEND, 2=GROUP, 3=SPECIAL/ATTACK, 4=EVADE. Within same priority, sort by Speed + random factor. Per-attack resolution steps (in order): (1) identify true target — check for DEFEND interceptors; (2) Rank KO roll; (3) Blindside roll; (4) reaction selection (constrained if Blindsided or KO'd); (5) defense roll and damage calculation; (6) counter chain resolution; (7) stamina/energy updates, buff/debuff application. GROUP actions are handled via stub in this task (real implementation in Task 18).
- **Acceptance Criteria**:
  1. Actions are sorted into correct priority order for all combinations of action types
  2. Speed-based tie-breaking operates correctly within a priority bracket
  3. DEFEND intercept correctly redirects attacks to the defending combatant
  4. Per-attack resolution executes all 7 steps in correct order for a standard ATTACK action
  5. Pipeline is a pure function: `resolvePerAttack(state, action) => CombatState` with no side effects
  6. GROUP action stub returns a no-op result without crashing
  7. Unit tests cover: priority ordering, DEFEND intercept, full per-attack resolution for at least 2 scenarios
- **Dependencies**: Task 11, Task 12, Task 13, Task 14
- **Files**: `src/combat/pipeline.ts`, `src/combat/pipeline.test.ts`

---

### Task 16: Round Manager Orchestrator
- **Component**: `combat/roundManager.ts`
- **Description**: Implement the 5-phase round orchestrator. Coordinates: Phase 1 (AI decisions via behavior tree stub), Phase 2 (assemble visual info payload), Phase 3 (receive and validate player declarations), Phase 4 (build and sort action queue), Phase 5 (execute per-attack pipeline for each action). Behavior tree is a stub in this task — it produces placeholder decisions. Real behavior tree wired in Task 19.
- **Acceptance Criteria**:
  1. `runRound(state, playerDeclarations)` executes all 5 phases in correct order and returns updated `CombatState`
  2. AI decision phase calls the behavior tree stub and incorporates stub output into the action queue
  3. Phase 2 assembles the correct visual info (stances, stamina levels, targeting) without revealing hidden Phase 1 details
  4. Phase 4 correctly sorts the unified action queue (player + AI actions) by priority
  5. A complete 3v3 round resolves without errors using all stubs (behavior tree stub + GROUP stub)
  6. Round result (all action outcomes) is recorded in `roundHistory`
- **Dependencies**: Task 15
- **Files**: `src/combat/roundManager.ts`, `src/combat/roundManager.test.ts`

---

### Task 17: Behavior Tree AI *(DESIGN REQUIRED)*
- **Component**: `combat/behaviorTree/`
- **Description**: Implement the NPC behavior tree AI that determines combat actions for each AI combatant. Node types, evaluation model, archetype-specific profiles, and integration interface must come from the design spec produced by `/intuition-design`. The design spec will define what to build; this task implements it.
- **Acceptance Criteria** *(to be refined after design — minimum requirements)*:
  1. Each of the 3 NPC archetypes (Elena, Lars, Kade) produces decisions that are distinct and archetype-appropriate
  2. AI decisions are deterministic for a given game state (no hidden randomness in the tree evaluation itself)
  3. Behavior tree evaluator matches the interface expected by Round Manager Phase 1: given a `Combatant` and `CombatState`, produces a `CombatAction`
  4. All archetype profiles are data-driven (not hardcoded per-archetype switch statements), following the design spec's pattern
- **Dependencies**: Task 10 + Behavior Tree AI design spec (from `/intuition-design`)
- **Files**: `src/combat/behaviorTree/` (structure TBD by design spec)

---

### Task 18: Group Action Type *(DESIGN REQUIRED)*
- **Component**: `combat/groupAction.ts`
- **Description**: Implement the GROUP action type mechanics. Targeting rules, synergy effects, resolution within the priority system, and interaction with defense/counter systems must come from the design spec produced by `/intuition-design`. This task implements that spec and replaces the GROUP stub from Task 15.
- **Acceptance Criteria** *(to be refined after design — minimum requirements)*:
  1. GROUP actions resolve at priority 2 per the established priority system
  2. Targeting rules are enforced (invalid GROUP configurations rejected)
  3. Synergy effects apply correctly as defined in the design spec
  4. GROUP resolution integrates with the existing per-attack pipeline without breaking ATTACK/DEFEND/EVADE/SPECIAL resolution
- **Dependencies**: Task 10 + Group Action Type design spec (from `/intuition-design`)
- **Files**: `src/combat/groupAction.ts` (or directory, TBD by design spec)

---

### Task 19: Combat Integration
- **Component**: `combat/integration.ts`
- **Description**: Wire the real behavior tree AI (Task 17) and Group action resolution (Task 18) into the Round Manager, replacing the stubs from Task 16. Validate the integrated system with a multi-round scenario covering all action types.
- **Acceptance Criteria**:
  1. Round Manager Phase 1 produces realistic, archetype-differentiated NPC decisions (not stubs) across all 3 enemy archetypes
  2. GROUP actions resolve correctly through the full pipeline (no stub fallback)
  3. A complete 3v3 round with diverse action types (including at least one SPECIAL and one GROUP) resolves without errors
  4. All integration tests pass at this task boundary — no regressions in Tasks 12-16 coverage
- **Dependencies**: Task 16, Task 17, Task 18
- **Files**: `src/combat/roundManager.ts` (updated), `src/combat/integration.test.ts`

---

### Task 20: GameState-CombatState Synchronization
- **Component**: `combat/sync.ts`
- **Description**: Implement the bidirectional sync between `GameState` and `CombatState` at round boundaries. `initCombatState` creates a `CombatState` from `GameState` for encounter start. `syncToGameState` applies round results (HP changes, buffs, stamina) back to `GameState`. This preserves the ADR-013 independence between the two state objects.
- **Acceptance Criteria**:
  1. `initCombatState(gameState, encounterConfig)` produces a valid `CombatState` reflecting GameState character stats and the encounter's enemy configuration
  2. `syncToGameState(gameState, combatState)` applies HP/stamina/buff changes from combat to GameState without modifying other GameState fields
  3. Sync is pure: same inputs always produce the same output; no mutations to input states
  4. A save/load round-trip during active combat preserves CombatState correctly through the sync layer
- **Dependencies**: Task 16, Task 19
- **Files**: `src/combat/sync.ts`, `src/combat/sync.test.ts`

---

### Task 21: Combat REST API
- **Component**: `api/combat.ts`
- **Description**: Implement the Fastify combat plugin exposing combat operations through the REST API. Endpoints to support: initialize encounter, submit player declarations, advance round, get combat state (visual info for Phase 2), get round history. Register with the main Fastify app.
- **Acceptance Criteria**:
  1. All combat API endpoints respond correctly for valid scenarios (encounter init, declaration submission, round advancement, state query)
  2. Player declarations submitted through the API are validated before processing (invalid declarations return error responses, not server errors)
  3. Combat state is correctly initialized through the API and persists across sequential API calls
  4. Visual info endpoint returns Phase 2 data (stances, stamina levels, targeting) without exposing Phase 1 AI decisions
- **Dependencies**: Task 19, Task 20
- **Files**: `src/api/combat.ts`, updated `src/api/index.ts`

---

### Task 22: End-to-End 3v3 Demo Encounter
- **Component**: Cross-system (validation)
- **Description**: Configure a static 3v3 demo encounter from the Excel's "Battle Scenarios" sheet and validate that it resolves correctly through all phases and multiple rounds. This is the primary demo deliverable — the fixture and test together prove the system works for the pitch. The encounter fixture is static JSON (not dynamic configuration).
- **Acceptance Criteria**:
  1. A defined 3v3 encounter (player party + enemy party from Excel Battle Scenarios) resolves through all 5 phases per round, running to completion (victory, defeat, or a fixed round count)
  2. All formula outputs during the encounter for deterministic inputs match Excel source of truth (specific: at least one Rank KO check, one Blindside check, one defense roll, one energy accumulation, one ascension threshold)
  3. The encounter produces identical results when run twice from the same initial state (deterministic end-to-end)
  4. The demo can be driven through the combat REST API (not just internal function calls)
- **Dependencies**: Task 21
- **Files**: `src/fixtures/encounter.json` (static encounter config), `src/combat/e2e.test.ts`

---

## 6.5 Design Recommendations

| Task(s) | Item Name | Recommendation | Rationale |
|---------|-----------|----------------|-----------|
| Task 17 | Behavior Tree AI System | **DESIGN REQUIRED** | No spec exists. Node types (Sequence, Selector, Condition, Action leaves), evaluation model (synchronous vs async), archetype profile structure, and the interface integration pattern with Round Manager Phase 1 all require design decisions with lasting consequences. Execution agents should not invent this architecture. |
| Task 18 | Group Action Type | **DESIGN REQUIRED** | Completely undefined in all reference documentation (the Excel notes it as "undefined — inspired by Skies of Arcadia crew specials"). Targeting rules, synergy mechanics, resolution within the priority system, and interaction with defense/counter chains need invention, not just implementation. |
| Task 1 | Project Foundation | Ready for execution | Standard Fastify + Vitest + TypeScript scaffolding. No design decisions — engineering selects exact package versions, config details. |
| Task 2 | Sprint 1 Type System | Ready for execution | Complete interface specs in `Sprint1_API_Reference.md`. Mechanical transcription + any gaps filled from `Sprint1_Technical_Reference.md`. |
| Tasks 3-8 | Sprint 1 Narrative Stack | Ready for execution | Complete specs in 4 reference documents + archived patterns. Personality math is explicit, dialogue mechanics are documented, persistence is straightforward. Fastify plugin pattern replaces Express router factory — engineering decision. |
| Task 9 | Sprint 1 Integration | Ready for execution | Integration test scenarios derive naturally from Sprint 1 API reference. No novel design. |
| Task 10 | Combat Type System | Ready for execution | Interfaces derive from `GM_Combat_Tracker_Documentation.md`. Engineering fills gaps (exact field names, optional fields). |
| Tasks 11-16 | Combat Engine Core | Ready for execution | All formulas sourced from Excel (ADR-015 TDD approach). Phase pipeline, priority system, defense system, counter chain, elemental paths, energy/ascension — all fully documented. |
| Task 19 | Combat Integration | Ready for execution | Mechanical wiring once T17/T18 design specs exist. |
| Tasks 20-22 | Sync, API, Demo | Ready for execution | Standard wiring and validation work once core components exist. |

**Design sessions must complete before Task 17 and Task 18 begin.** Both design sessions can run concurrently with Tasks 10-16 implementation (they touch different components and have no runtime dependency on each other).

---

## 7. Testing Strategy

**Framework:** Vitest (TDD workflow throughout Sprint 2 formula porting).

**Test types required:**

| Type | What | Which Tasks |
|------|------|------------|
| Unit | Pure functions: personality math, state updaters, dialogue gate evaluation, all combat formulas, defense resolution, counter chain, elemental paths, energy/ascension, declaration validation, pipeline stages | Tasks 3, 5, 6, 11, 12, 13, 14, 15 |
| Integration | Full API request flows, save/load round-trip, multi-step dialogue session | Tasks 9, 21 |
| Formula TDD | Excel-derived test cases: write test with known input/output value, then implement | Task 11 (strictly TDD), Tasks 12, 13 (formula verification) |
| End-to-End | Full 3v3 encounter through combat REST API | Task 22 |

**Critical scenarios:**
- Personality sum invariant under all adjustment operations (Task 3)
- Each Excel formula verified with at least 2 input/output pairs from the spreadsheet (Task 11)
- Counter chain of length 3+ terminates correctly (Task 12)
- Ascension level transitions at all three thresholds (Task 13)
- Complete round with all 5 action types in a single queue (Task 15)
- Full 3v3 end-to-end with formula output matching Excel (Task 22)

**Test file convention:** Co-locate test files with source (`personalitySystem.test.ts` next to `personalitySystem.ts`). Integration and E2E tests may live in a `tests/` directory at `src/` level if co-location is awkward.

**Infrastructure needed:** Vitest config to include `src/**/*.test.ts` and `tests/**/*.test.ts`. No special setup beyond TypeScript resolution.

---

## 8. Risks & Mitigations

| Risk | Likelihood | Impact | Mitigation |
|------|-----------|--------|------------|
| Behavior tree AI design reveals unexpected scope (node types, archetype profiles require more than one sprint) | Medium | High | Run design session early — concurrently with T10-T16. If scope expands, stub AI can power a limited demo (fixed decision per archetype) while full behavior tree is scoped to a future sprint. |
| Group action type design uncovers mechanics incompatible with existing priority system | Medium | High | Design session must examine priority system interface (T15) before finalizing Group action resolution. If incompatible, redesign priority system in T15 before T18 begins. |
| Excel formula porting produces silent errors (wrong output, no crash) | Medium | High | ADR-015 TDD approach is the primary mitigation — test must be written with Excel value before implementation. Engineer should verify test inputs against Excel directly during T11. |
| Fastify plugin architecture unfamiliar to execution agents (vs Express routes) | Medium | Medium | Engineering phase must produce clear code specs for the Fastify plugin pattern. Recommend engineer reads Fastify docs and archived Express patterns before writing specs. |
| Sprint 2 task count too large for a single execution session | High | Medium | Task sequence is designed for sequential build. If execution stalls, Tasks 1-16 produce a testable combat engine (without AI and Group); that's a viable partial demo. T17/T18/T19 are the incremental add. |
| CombatState ↔ GameState sync edge cases (concurrent save during combat) | Low | Medium | T20 acceptance criteria explicitly cover the save/load-during-combat scenario. Engineering phase should note this as a test priority. |
| Random factors in combat (roll/20 checks) complicate deterministic testing | Low | Medium | Seed-based randomness (pass a random seed or mock the roll function) makes tests deterministic. Engineering phase decides the injection mechanism. |

---

## 9. Open Questions

| Question | Why It Matters | Recommended Default |
|----------|---------------|-------------------|
| Should random roll functions be injectable (seeded) at the module level or per-call-site? | Deterministic testing of Rank KO, Blindside, Crushing Blow requires controllable randomness | Per-call-site injection: `resolvePerAttack(state, action, rollFn)` where `rollFn` defaults to `() => Math.random() * 20`. Tests pass a fixed rollFn. |
| Where does the session state singleton live in Fastify (vs Express's `api/game.ts` pattern)? | Fastify uses plugins, not middleware; the singleton pattern from the archived prototype doesn't translate directly | Use a Fastify plugin with `fastify.decorate('gameState', ...)` to attach session state to the Fastify instance. All plugins access via `fastify.gameState`. |
| Should Sprint 1 and Sprint 2 types live in one file (`types/index.ts`) or split (`types/index.ts` + `types/combat.ts`)? | File organization affects import clarity; combat types depend on Sprint 1 types | Split: `types/index.ts` for Sprint 1, `types/combat.ts` for Sprint 2 combat interfaces. `combat.ts` imports from `index.ts`. Keeps each file focused. |

---

## 10. Planning Context for Engineer

The engineer decides HOW to implement every task — internal patterns, file decomposition within components, error handling details. These are planning-level considerations, not instructions.

**Sequencing considerations:**
- Tasks 3, 4, 7 can proceed in parallel (all depend only on T2, touch independent surfaces). Task 6 depends on T3 and T5. Within Sprint 2, Tasks 11-14 can proceed in parallel once T10 is complete.
- Design sessions for T17 and T18 should start as early as possible — concurrently with Tasks 10-14. Design specs must be ready before Task 16 (Round Manager) is finalized, since the Round Manager's stub interface must match the real behavior tree interface.
- Task 22 (E2E) cannot meaningfully begin until T21 (Combat API) is complete.

**Parallelization opportunities:**
- Sprint 1: T3 + T4 + T7 in parallel (all need only T2)
- Sprint 2: T11 + T14 in parallel (both need only T10); T12 + T13 in parallel (both need T10 + T11)
- Design sessions (T17, T18) in parallel with T10-T16 implementation

**Engineering questions to resolve:**
- How to handle the Fastify plugin-based session state singleton (vs the archived `getActiveGameState/setActiveGameState` pattern from Express)
- What immutability mechanism to use: TypeScript `Readonly<>` types, Immer `produce()`, or spread-only by convention (user preference: Spread + Readonly if direction is needed)
- Random roll injection mechanism (per-function parameter vs module-level seeding) for testability of dominance checks
- Whether to type combat actions with discriminated unions (e.g., `type CombatAction = AttackAction | DefendAction | ...`) vs a single interface with optional fields
- Test file placement strategy (co-location vs `tests/` directory)
- ESLint/Prettier configuration — currently absent; engineer may add if desired for development experience

**Constraints:**
- All combat formulas must exactly replicate Excel output — no simplifications, approximations, or "improvements" (ADR-007)
- All state transitions must produce new objects; no mutations to input state (ADR-012)
- AI decisions are made and locked in Phase 1 before player sees Phase 2 visual information — this constraint must be preserved in the Round Manager implementation (ADR-008)
- The `saves/` directory is the persistence target (already exists in repo); save slot numbering is 1-10
- `docs/project_notes/archive/src-sprint1-prototype/` contains reference patterns (structure, naming conventions, module boundaries) but code is not to be copied verbatim

**Risk context:**
- The behavior tree AI and Group action type are the highest-risk tasks due to undefined specifications — do not begin implementation until design specs exist. If execution begins before design is complete, use the stubs from T15/T16 and mark T17/T18 as blocked.
- Formula porting (T11) is the highest-accuracy risk — a silent formula error only discovered at T22 would require backtracking through multiple tasks. TDD approach (ADR-015) is the primary safeguard; engineer should treat each formula test as a contract with the Excel source of truth.
- Fastify's plugin architecture differs meaningfully from Express middleware — if the engineer is unfamiliar, reading the Fastify documentation (especially plugin scoping and `fastify.decorate`) before writing code specs will prevent architectural mistakes that are expensive to unwind.
