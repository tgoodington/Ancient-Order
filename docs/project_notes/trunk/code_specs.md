# Code Specs

**Date:** 2026-02-21
**Plan Reference:** `docs/project_notes/trunk/plan.md`
**Design Specs:** `design_spec_behavior_tree_ai_system.md`, `design_spec_group_action_type.md`

---

## Cross-Cutting Concerns

### Module System: ESM
- `package.json`: `"type": "module"`
- `tsconfig.json`: `"module": "NodeNext"`, `"moduleResolution": "NodeNext"`
- All imports use explicit `.js` extensions in compiled output (TypeScript source uses `.js` in import paths)
- Vitest natively supports ESM — no special config needed

### Immutability: Spread + Readonly<T>
- All domain state types (`GameState`, `CombatState`, `Combatant`) exported as `Readonly<T>` or with `readonly` field modifiers
- All state update functions use spread operator: `{ ...state, field: newValue }`
- Nested updates spread at each level: `{ ...state, player: { ...state.player, personality: newPersonality } }`
- No Immer — keep it simple and explicit (matches archived prototype pattern)

### Error Handling
- Reuse archived pattern: `ApiResponse<T>` envelope with `success`, `data?`, `error?` fields
- `ErrorCodes` as const object (not enum — better ESM compatibility)
- Fastify `setErrorHandler()` for global error handling
- Domain functions throw typed errors; API layer catches and wraps in `ApiResponse`

### Naming Conventions (from archived prototype)
- Files: `camelCase.ts` matching primary export
- Functions: `createXxx()`, `getXxx()`, `updateXxx()`, `validateXxx()`, `resolveXxx()`
- Constants: `UPPER_SNAKE_CASE`
- IDs: `npc_[descriptor]_[name]` for NPCs, `[name]_[node]` for dialogue nodes
- Private helpers: `_helperName()` prefix

### Session State: Fastify Decorate
- `fastify.decorate('gameState', null)` in app factory
- Type-safe via Fastify declaration merging:
  ```typescript
  declare module 'fastify' {
    interface FastifyInstance {
      gameState: GameState | null;
    }
  }
  ```
- All plugins access via `fastify.gameState`
- Set via `fastify.gameState = newState` (mutable reference, immutable object)

### Fastify Plugin Pattern
- Each API module exports an async function: `async function gamePlugin(fastify: FastifyInstance): Promise<void>`
- Registered via `fastify.register(gamePlugin, { prefix: '/api/game' })`
- JSON Schema validation on routes via Fastify's built-in `schema` option
- No Express routers — use `fastify.get()`, `fastify.post()`, etc.

### Test Strategy
- Co-located test files: `personalitySystem.test.ts` next to `personalitySystem.ts`
- Integration tests: `tests/` directory at project root if co-location is awkward
- Vitest with `describe`/`it`/`expect` API (Jest-compatible)
- TDD for all combat formulas (Task 11): test with Excel value first, then implement

### Random Roll Injection
- Per-call-site injection: functions that need randomness accept a `rollFn` parameter
- Default: `rollFn: () => Math.random() * 20` (produces 0-20 roll)
- Tests pass deterministic roll functions: `() => 15.0`
- Applied to: `resolvePerAttack()`, `resolvePriority()` (speed tie-breaking), GROUP tie-breaking

### Linting: ESLint + Prettier
- ESLint with `@typescript-eslint` plugin, flat config format (`eslint.config.js`)
- Prettier for formatting (`.prettierrc`)
- Scripts: `"lint": "eslint src/"`, `"format": "prettier --write src/"`

---

## Task Specs

### Task 1: Project Foundation & Tooling

- **Approach**: Clean scaffolding with Fastify, Vitest, TypeScript ESM, ESLint/Prettier. Create directory structure, configure all build tools, implement minimal health check.
- **Rationale**: Fresh setup avoids carrying forward archived Express dependencies. ESM aligns with Fastify and Vitest native support.
- **Files to Modify**: `package.json`, `tsconfig.json`
- **Files to Create**:
  - `vitest.config.ts` — include `src/**/*.test.ts` and `tests/**/*.test.ts`
  - `eslint.config.js` — flat config with `@typescript-eslint`
  - `.prettierrc` — default Prettier config (singleQuote, semi, printWidth 100)
  - `src/api/index.ts` — Fastify app factory with health check (`GET /health → { status: 'ok' }`) and `fastify.decorate('gameState', null)`
  - Directory structure: `src/types/`, `src/personality/`, `src/state/`, `src/dialogue/`, `src/persistence/`, `src/api/`, `src/combat/`, `src/combat/behaviorTree/`, `src/combat/behaviorTree/factors/`, `src/combat/behaviorTree/profiles/`, `src/fixtures/`
- **Patterns to Follow**: ESM imports with `.js` extensions. Fastify app factory pattern.
- **Key Implementation Details**:
  - Dependencies to install: `fastify`, `uuid`
  - Dev dependencies: `typescript`, `vitest`, `@types/node`, `@types/uuid`, `tsx` (ESM-compatible dev runner), `eslint`, `@typescript-eslint/eslint-plugin`, `@typescript-eslint/parser`, `prettier`
  - Remove: `express`, `@types/express`, `jest`, `ts-jest`, `ts-node` (archived dependencies)
  - Scripts: `"dev": "tsx watch src/api/index.ts"`, `"build": "tsc"`, `"start": "node dist/api/index.js"`, `"test": "vitest run"`, `"test:watch": "vitest"`, `"lint": "eslint src/"`, `"format": "prettier --write src/"`
  - tsconfig: `"target": "ES2022"`, `"module": "NodeNext"`, `"moduleResolution": "NodeNext"`, `"outDir": "./dist"`, `"rootDir": "./src"`, `"strict": true`, `"declaration": true`, `"sourceMap": true`
- **Acceptance Criteria**:
  1. `npm run dev` starts Fastify on port 3000, `GET /health` → `{ status: 'ok' }`
  2. `npm test` runs Vitest with zero failures
  3. `npm run build` compiles to `dist/` with zero errors
  4. Directory structure matches Section 4 of plan
- **Dependencies**: None

---

### Task 2: Sprint 1 Type System

- **Approach**: Transcribe all interfaces from `Sprint1_API_Reference.md` and `Sprint1_Technical_Reference.md` into `src/types/index.ts`. Use the archived `types/index.ts` as pattern reference for structure and naming. Add `Readonly` modifiers on state-bearing interfaces.
- **Rationale**: Mechanical transcription from existing specs. Archived types provide naming conventions.
- **Files to Create**: `src/types/index.ts`
- **Patterns to Follow**: Archived `types/index.ts` (see research report for complete interface list)
- **Key Implementation Details**:
  - Interfaces to define: `Personality` (6 traits as number fields), `PersonalityTrait` (union type of 6 trait names), `PersonalityGate` (trait, operator: 'gte'|'lte'|'eq', value), `PersonalityAdjustment` (Partial<Personality>), `PlayerCharacter`, `NPC`, `GameState`, `DialogueNode`, `DialogueOption`, `DialogueResult`, `ConversationEntry`, `ApiResponse<T>`, `ApiError`, `ErrorCodes`
  - `GameState.combatState: CombatState | null` — typed as `import('./combat.js').CombatState | null` or use forward declaration. Simplest: make it `unknown | null` in Sprint 1 types, refine in Task 10.
  - `ErrorCodes` as `const` object (not enum): `export const ErrorCodes = { GAME_NOT_FOUND: 'GAME_NOT_FOUND', ... } as const`
  - Export all types as named exports from barrel file
  - `Personality` type: `{ patience: number; empathy: number; cunning: number; logic: number; kindness: number; charisma: number }`
  - `PersonalityTrait` type: `'patience' | 'empathy' | 'cunning' | 'logic' | 'kindness' | 'charisma'`
  - Add `readonly` modifier on `GameState`, `PlayerCharacter`, `NPC` fields
- **Acceptance Criteria**:
  1. All Sprint 1 interfaces defined
  2. TypeScript strict mode compilation succeeds
  3. No circular imports
- **Dependencies**: Task 1

---

### Task 3: Personality System

- **Approach**: Port the personality adjustment algorithm from the archived `personalitySystem.ts`. The algorithm is a multi-step process: apply direct adjustments → clamp to 5-35% → redistribute excess/deficit proportionally → normalize to 100% sum. All functions pure.
- **Rationale**: Archived implementation is the reference pattern. Algorithm is non-trivial (3-pass normalization).
- **Files to Create**: `src/personality/personalitySystem.ts`, `src/personality/personalitySystem.test.ts`
- **Patterns to Follow**: Archived `personality/personalitySystem.ts` — `adjustPersonality()`, `validatePersonality()`, `clampTrait()`, `createDefaultPersonality()`
- **Key Implementation Details**:
  - Constants: `MIN_TRAIT_VALUE = 5`, `MAX_TRAIT_VALUE = 35`, `TOTAL_PERCENTAGE = 100`, `FLOAT_TOLERANCE = 0.01`
  - Core functions:
    - `createDefaultPersonality(): Personality` — all traits equal (~16.67%, normalized to sum=100)
    - `adjustPersonality(current: Readonly<Personality>, adjustments: PersonalityAdjustment): Personality` — apply → clamp → redistribute → normalize
    - `validatePersonality(p: Personality): { valid: boolean; errors: string[] }` — check ranges and sum
    - `clampTrait(value: number): number` — `Math.max(MIN, Math.min(MAX, value))`
    - `getPersonalitySum(p: Personality): number` — sum all 6 traits
  - Normalization algorithm (3 passes from archived code):
    1. Apply direct adjustments, clamp each trait to [5, 35]
    2. Calculate sum. If sum ≠ 100, distribute difference proportionally across unadjusted traits (or evenly if all were adjusted)
    3. Final pass: distribute remaining rounding error equally, re-clamp, round to 2 decimal places
  - Test cases: boundary (4.9→5, 35.1→35), valid adjustment, multi-trait interaction, sum invariant
- **Acceptance Criteria**:
  1. Trait adjustment produces new object (no mutation)
  2. Sum-to-100% maintained after any adjustment
  3. Range enforcement: values clamped to [5, 35] with redistribution
  4. Unit tests cover boundaries, valid adjustments, multi-trait, sum invariant
- **Dependencies**: Task 2

---

### Task 4: Game State & NPC System

- **Approach**: Implement `createNewGameState()` and NPC template registry. NPCs stored as frozen template objects in a `Map` or record, retrievable by ID. Follow archived `state/gameState.ts` and `state/npcs.ts` patterns.
- **Rationale**: Direct port from archived patterns. NPC data comes from Sprint1_ClaudeCode_Prompt.md.
- **Files to Create**: `src/state/gameState.ts`, `src/state/npcs.ts`, `src/state/gameState.test.ts`, `src/state/npcs.test.ts`
- **Patterns to Follow**: Archived `state/gameState.ts` (factory function), `state/npcs.ts` (template record)
- **Key Implementation Details**:
  - `createNewGameState(): GameState` — returns complete initial state with:
    - UUID for `id`
    - Default personality from `createDefaultPersonality()`
    - All 3 NPCs loaded from templates
    - `combatState: null`
    - `currentDialogueNode: null`
    - `timestamp: Date.now()`
  - NPC templates: `NPC_TEMPLATES: Record<string, NPC>` — Elena (`npc_scout_elena`), Lars (`npc_merchant_lars`), Kade (`npc_outlaw_kade`)
  - NPC IDs and archetypes from Sprint1_ClaudeCode_Prompt.md
  - `getNPC(id: string): NPC | undefined` — lookup from templates
  - `getAllNPCs(): NPC[]` — return all templates
  - Templates are `Object.freeze()`'d at module level — immutable at runtime
- **Acceptance Criteria**:
  1. `createNewGameState()` returns valid state with personality summing to 100%
  2. 3 NPC templates retrievable by ID
  3. NPC personalities immutable
  4. Tests: initial state, NPC retrieval, unknown ID handling
- **Dependencies**: Task 2, Task 3

---

### Task 5: State Updaters Library

- **Approach**: Port the updater pattern from archived `state/stateUpdaters.ts`. Every function is `(state: Readonly<GameState>, ...) => GameState`. Composition pattern: higher-level updaters call lower-level ones.
- **Rationale**: Direct port from archived patterns. Core architectural pattern for all state changes.
- **Files to Create**: `src/state/stateUpdaters.ts`, `src/state/stateUpdaters.test.ts`
- **Patterns to Follow**: Archived `state/stateUpdaters.ts` — spread-based immutable updates, `updateTimestamp()` wrapper
- **Key Implementation Details**:
  - Core updaters (from archived code):
    - `updateTimestamp(state): GameState` — `{ ...state, timestamp: Date.now() }`
    - `updatePlayerPersonality(state, newPersonality): GameState` — nested spread on player
    - `applyPersonalityAdjustment(state, adjustment): GameState` — calls `adjustPersonality()` then `updatePlayerPersonality()`
    - `updateNPCAffection(state, npcId, change): GameState` — clamp to [-100, +100]
    - `updateNPCTrust(state, npcId, change): GameState` — clamp to [-100, +100]
    - `updateNPCRelationship(state, npcId, affection, trust): GameState` — compound updater
    - `addConversationEntry(state, entry): GameState` — append to log via spread
    - `processDialogueChoice(state, npcId, nodeId, optionId, option): GameState` — master function composing personality + NPC relationship + conversation log updates
    - `updateCombatState(state, combatState): GameState` — set/clear combatState field
  - Every updater wraps result with `updateTimestamp()`
  - Tests verify: reference inequality (new object), value correctness, composition
- **Acceptance Criteria**:
  1. All updaters return new GameState (input unchanged)
  2. Pure functions (identical inputs → identical outputs)
  3. All Sprint 1 mutations represented as updaters
  4. Tests verify reference inequality and value correctness
- **Dependencies**: Task 3, Task 4

---

### Task 6: Dialogue Engine

- **Approach**: Port dialogue traversal from archived `dialogue/dialogueEngine.ts`. Gate evaluation checks trait against threshold using operator (gte/lte/eq). Dead-end validation ensures every reachable node has at least one ungated option.
- **Rationale**: Archived implementation is the reference. Personality gate logic is straightforward.
- **Files to Create**: `src/dialogue/dialogueEngine.ts`, `src/dialogue/dialogueEngine.test.ts`, `src/dialogue/fixtures.ts` (test dialogue tree)
- **Patterns to Follow**: Archived `dialogue/dialogueEngine.ts`
- **Key Implementation Details**:
  - Core functions:
    - `evaluatePersonalityGate(gate: PersonalityGate, personality: Personality): boolean` — compare `personality[gate.trait]` against `gate.value` using `gate.operator`
    - `getAvailableOptions(node: DialogueNode, personality: Personality): DialogueOption[]` — filter options by gate evaluation (ungated options always available)
    - `getStartingNode(npc: NPC, dialogueTree: DialogueNode[]): DialogueNode` — convention: `${npcId}_greet`
    - `processDialogueSelection(state: GameState, npcId: string, nodeId: string, optionId: string, dialogueTree: DialogueNode[]): DialogueResult` — returns new state via stateUpdaters + next node info
    - `validateDialogueTree(nodes: DialogueNode[]): { valid: boolean; problematicNodes: string[] }` — find nodes with zero ungated options
  - `DialogueResult` type: `{ state: GameState; nextNode: DialogueNode | null; selectedOption: DialogueOption }`
  - Dialogue tree data passed as parameter (not hardcoded) — enables fixture-based testing
  - Test fixtures: sample dialogue tree with gated and ungated options, dead-end node for validation testing
- **Acceptance Criteria**:
  1. Gate pass/fail works correctly
  2. Sufficient trait → access gated options
  3. Dead-end detection identifies problematic nodes
  4. Traversal uses stateUpdaters (immutable)
  5. Tests: gate pass, gate fail, fallback, dead-end validation
- **Dependencies**: Task 3, Task 5

---

### Task 7: Persistence Layer

- **Approach**: Port JSON file-based persistence from archived `persistence/saveLoad.ts`. Async file I/O with `fs/promises`. Slots 1-10 in `saves/` directory.
- **Rationale**: Direct port. Simple file I/O.
- **Files to Create**: `src/persistence/saveLoad.ts`, `src/persistence/saveLoad.test.ts`
- **Patterns to Follow**: Archived `persistence/saveLoad.ts`
- **Key Implementation Details**:
  - Constants: `SAVES_DIRECTORY = 'saves'`, `MIN_SLOT = 1`, `MAX_SLOT = 10`
  - Functions:
    - `saveGame(state: GameState, slot: number): Promise<SaveMetadata>` — validate slot, serialize to `saves/slot_${slot}.json`, return metadata
    - `loadGame(slot: number): Promise<GameState>` — read file, parse, validate with type guard
    - `listSaves(): Promise<SaveSlotInfo[]>` — scan saves directory for existing files
    - `deleteSave(slot: number): Promise<void>` — remove file
    - `validateGameState(data: unknown): data is GameState` — runtime type guard for loaded JSON
  - `SaveMetadata`: `{ slot: number; timestamp: number; playerName: string }`
  - `SaveSlotInfo`: `{ slot: number; exists: boolean; metadata?: SaveMetadata }`
  - Error handling: slot out of range → throw with `ErrorCodes.INVALID_SLOT`; file not found → throw with `ErrorCodes.SAVE_NOT_FOUND`
  - Ensure `saves/` directory exists (create if missing via `fs.mkdir({ recursive: true })`)
  - Tests: use temp directory (Vitest `beforeEach`/`afterEach` with `fs.mkdtemp`), round-trip fidelity, slot independence, missing-slot error
- **Acceptance Criteria**:
  1. Save/load round-trip: loaded state deeply equals original
  2. 10 slots independently addressable
  3. Missing slot returns typed error
  4. Files are valid JSON
  5. Tests: round-trip, independence, missing-slot
- **Dependencies**: Task 2

---

### Task 8: Sprint 1 REST API (Fastify)

- **Approach**: Implement 4 Fastify plugins (game, player, npc, dialogue) registered in the app factory. Each plugin is a thin handler delegating to domain functions. Use Fastify JSON Schema validation for request bodies.
- **Rationale**: Fastify plugin architecture replaces Express router factories. Thin handlers keep business logic in domain modules.
- **Files to Modify**: `src/api/index.ts` (add plugin registrations)
- **Files to Create**: `src/api/game.ts`, `src/api/player.ts`, `src/api/npc.ts`, `src/api/dialogue.ts`
- **Patterns to Follow**: Fastify plugin pattern (see Cross-Cutting Concerns). Archived `api/*.ts` for route structure.
- **Key Implementation Details**:
  - `src/api/index.ts` (app factory):
    ```typescript
    export async function buildApp(): Promise<FastifyInstance> {
      const fastify = Fastify({ logger: true });
      fastify.decorate('gameState', null as GameState | null);
      fastify.get('/health', async () => ({ status: 'ok' }));
      await fastify.register(gamePlugin, { prefix: '/api/game' });
      await fastify.register(playerPlugin, { prefix: '/api/player' });
      await fastify.register(npcPlugin, { prefix: '/api/npc' });
      await fastify.register(dialoguePlugin, { prefix: '/api/dialogue' });
      fastify.setErrorHandler(globalErrorHandler);
      return fastify;
    }
    ```
  - `src/api/game.ts` — gamePlugin:
    - `POST /new` → `createNewGameState()`, set `fastify.gameState`
    - `GET /state` → return current `fastify.gameState`
    - `POST /save/:slot` → `saveGame(fastify.gameState, slot)`
    - `POST /load/:slot` → `loadGame(slot)`, set `fastify.gameState`
    - Middleware check: if `fastify.gameState === null` and route requires it → 404 with `GAME_NOT_FOUND`
  - `src/api/player.ts` — playerPlugin:
    - `GET /` → return player data
    - `POST /personality` → `applyPersonalityAdjustment()`, update `fastify.gameState`
  - `src/api/npc.ts` — npcPlugin:
    - `GET /` → list all NPCs
    - `GET /:id` → get NPC by ID
  - `src/api/dialogue.ts` — dialoguePlugin:
    - `GET /:npcId/start` → get starting dialogue node
    - `POST /:npcId/choose` → process dialogue choice, update state
  - All responses wrapped in `ApiResponse<T>` envelope
  - Request validation via Fastify `schema` option with JSON Schema objects
  - Fastify declaration merging for type-safe `fastify.gameState` access
- **Acceptance Criteria**:
  1. All Sprint 1 endpoints respond with correct status codes and ApiResponse envelopes
  2. Invalid requests return structured ApiError (not unhandled exceptions)
  3. Session state accessible across plugins via `fastify.gameState`
  4. Clean startup and shutdown
- **Dependencies**: Task 5, Task 6, Task 7

---

### Task 9: Sprint 1 Integration Validation

- **Approach**: Integration tests using Fastify's `inject()` method (no actual HTTP server needed). Test complete session flow through API.
- **Rationale**: Fastify's `inject()` is purpose-built for integration testing without network overhead.
- **Files to Create**: `tests/sprint1.integration.test.ts`
- **Patterns to Follow**: Fastify injection testing pattern: `const response = await app.inject({ method: 'POST', url: '/api/game/new' })`
- **Key Implementation Details**:
  - Test scenarios:
    1. Full session flow: new game → modify personality → traverse dialogue → save → load → verify loaded state equals saved
    2. Error paths: personality adjustment violating constraints, invalid save slot, missing game state
    3. NPC data integrity: retrieve all NPCs, verify fixed personalities
    4. Dialogue gate evaluation via API: choose option requiring trait threshold
  - Each test creates a fresh Fastify instance via `buildApp()`
  - No persistent state between tests
  - Assert personality sum invariant after every personality-modifying API call
- **Acceptance Criteria**:
  1. Complete session flow works end-to-end
  2. All endpoints tested with realistic payloads
  3. No unhandled promise rejections
  4. Personality constraint maintained across all API operations
- **Dependencies**: Task 8

---

### Task 10: Combat Type System

- **Approach**: Define all combat interfaces in `src/types/combat.ts`, importing Sprint 1 types from `./index.js`. Extend `GameState` with `combatState: CombatState | null` (already declared in Sprint 1 types as forward reference).
- **Rationale**: Separate file keeps combat types focused. Plan specifies this split.
- **Files to Create**: `src/types/combat.ts`
- **Files to Modify**: `src/types/index.ts` (refine `combatState` field type to import from `combat.ts`)
- **Patterns to Follow**: Sprint 1 types structure. `readonly` field modifiers on state types.
- **Key Implementation Details**:
  - Interfaces to define:
    - `CombatState`: `{ round, phase, playerParty, enemyParty, actionQueue, roundHistory, status }`
    - `Combatant`: `{ id, name, archetype, rank, stamina, maxStamina, power, speed, energy, maxEnergy, ascensionLevel, activeBuffs, elementalPath, reactionSkills, isKO }`
    - `CombatAction`: `{ combatantId, type: ActionType, targetId: string | null, energySegments?: number }`
    - `ActionType`: `'ATTACK' | 'DEFEND' | 'EVADE' | 'SPECIAL' | 'GROUP'`
    - `CombatPhase`: `'AI_DECISION' | 'VISUAL_INFO' | 'PC_DECLARATION' | 'ACTION_RESOLUTION' | 'PER_ATTACK'`
    - `RoundResult`: `{ round, actions: ActionResult[], stateSnapshot }`
    - `AttackResult`: `{ attackerId, targetId, damage, defenseType, defenseOutcome, rankKO, blindside, crushingBlow, counterChain }`
    - `DefenseResult`: `{ type: DefenseType, success: boolean, damageMultiplier: number }`
    - `DefenseType`: `'block' | 'dodge' | 'parry' | 'defenseless'`
    - `Buff`: `{ type, source, duration, modifier }`
    - `DebuffEffect`: `{ stat, amount, source }`
    - `ElementalPath`: `'Fire' | 'Water' | 'Air' | 'Earth' | 'Shadow' | 'Light'`
    - `AscensionLevel`: `0 | 1 | 2 | 3`
    - `EncounterConfig`: `{ id, name, playerParty: CombatantConfig[], enemyParty: CombatantConfig[] }`
    - `CombatantConfig`: `{ id, name, archetype, rank, stamina, power, speed, elementalPath, ... }`
    - `ReactionSkills`: `{ block: { SR, SMR, FMR }, dodge: { SR, FMR }, parry: { SR, FMR } }`
  - Priority constants: `export const ACTION_PRIORITY: Record<ActionType, number> = { GROUP: 0, DEFEND: 1, ATTACK: 2, SPECIAL: 2, EVADE: 3 }` (updated per GROUP design spec)
  - Energy constants: `ENERGY_GAINS = { actionSuccess: 1.0, actionFailure: 0.5, reactionSuccess: 0.5, reactionFailure: 0.25 }`
  - Ascension constants: `ASCENSION_THRESHOLDS = [35, 95, 180]`, `ASCENSION_STARTING_SEGMENTS = [0, 0, 1, 2]`, `ASCENSION_ACCUMULATION_BONUS = [0, 0.25, 0.25, 0.50]`
  - GROUP types from design spec: `GroupActionDeclaration`, `GroupResolutionResult`, `BlockDefenseResult`, `GroupActionConfig`
  - Behavior tree types from design spec: `ActionScores`, `ScoringFactor`, `ArchetypeProfile`, `EvaluatorConfig`, `ScoredCandidate`, `CombatPerception`, `AllyPerception`, `EnemyPerception`, `TargetPerception`
- **Acceptance Criteria**:
  1. All combat interfaces defined
  2. `GameState` extended with typed `combatState`
  3. All 5 action types and 4 priority levels represented
  4. No circular imports
- **Dependencies**: Task 9

---

### Task 11: Combat Formula Suite (TDD)

- **Approach**: Test-driven development. For each formula: (1) write test with Excel-derived input/output, (2) implement function. All formulas are pure functions in a single file. Extract `calculateBaseDamage()` as shared utility for pipeline and GROUP action use.
- **Rationale**: ADR-015 mandates TDD for formula porting. Single file keeps formulas co-located and cross-referenceable. Shared damage utility prevents duplication (per engineering decision).
- **Files to Create**: `src/combat/formulas.ts`, `src/combat/formulas.test.ts`
- **Patterns to Follow**: Pure functions, no side effects, no randomness (roll values passed as parameters)
- **Key Implementation Details**:
  - Formula functions to implement:
    - `calculateRankKOThreshold(attackerRank, targetRank): number` — `((attackerRank - targetRank) * 3) / 10`
    - `checkRankKO(threshold, roll): boolean` — `(roll / 20) >= (1 - threshold)`, condition: attackerRank > targetRank by ≥0.5
    - `calculateBlindsideThreshold(attackerSpeed, targetSpeed): number` — `(attackerSpeed - targetSpeed) / targetSpeed`
    - `checkBlindside(threshold, roll): boolean` — `(roll / 20) >= (1 - threshold)`, condition: attackerSpeed > targetSpeed
    - `calculateCrushingBlowThreshold(actionPower, targetPower): number` — `(actionPower - targetPower) / targetPower`
    - `checkCrushingBlow(threshold, roll): boolean` — same roll check, condition: Block defense AND actionPower > targetPower
    - `calculateBlockDamage(damage, SMR, FMR, success): number` — success: `damage * (1 - SMR)`, fail: `damage * (1 - FMR)`
    - `calculateDodgeDamage(damage, FMR, success): number` — success: 0, fail: `damage * (1 - FMR)`
    - `calculateParryDamage(damage, FMR, success): number` — success: 0 (counter triggered), fail: `damage * (1 - FMR)`
    - `calculateDefenselessDamage(damage): number` — `damage` (100%)
    - `calculateBaseDamage(attackerPower, targetPower, ...modifiers): number` — **shared utility** used by both pipeline and GROUP. Exact formula from Excel.
    - `calculateSpecialDamageBonus(baseDamage, energySegments): number` — `baseDamage * (1 + 0.10 * energySegments)`
    - `calculateEvadeRegen(maxStamina): number` — `maxStamina * 0.30`
    - `calculateEnergyGain(eventType, result, ascensionLevel): number` — lookup from energy table × accumulation bonus
    - `calculateAscensionLevel(totalSegments): AscensionLevel` — check against thresholds [35, 95, 180]
    - `applyDynamicModifiers(baseStats, buffs, debuffs): ModifiedStats` — stack all active buffs/debuffs
  - Tests:
    - At least 2 input/output pairs per formula category from Excel
    - Full range coverage for threshold formulas (not just one case)
    - Energy gain for all 4 event/result combos × 4 ascension levels
    - Edge cases: equal ranks (no KO possible), zero power, maximum values
- **Acceptance Criteria**:
  1. Each formula has test written before implementation (TDD)
  2. Threshold formulas correct across full range
  3. Damage formulas match Excel for all defense types
  4. Energy table matches Excel for all 4 combos
  5. All tests pass without mocking
- **Dependencies**: Task 10

---

### Task 12: Defense Resolution & Counter Chain System

- **Approach**: Implement defense outcome resolution using SR/SMR/FMR rates, and counter chain as recursive resolution. Defense resolution is a pure function taking a roll value (injected). Counter chain loops until termination condition.
- **Rationale**: Counter chain is inherently recursive but bounded by KO/stamina/parry-failure. Loop-based implementation avoids stack overflow for long chains.
- **Files to Create**: `src/combat/defense.ts`, `src/combat/counterChain.ts`, `src/combat/defense.test.ts`, `src/combat/counterChain.test.ts`
- **Patterns to Follow**: Per-call-site roll injection. Pure functions returning new state.
- **Key Implementation Details**:
  - `defense.ts`:
    - `resolveDefense(defenseType, damage, reactionSkills, roll): DefenseResult` — dispatches to Block/Dodge/Parry/Defenseless handlers
    - `resolveBlock(damage, SR, SMR, FMR, roll): { success: boolean; damage: number; crushingBlowEligible: boolean }`
    - `resolveDodge(damage, SR, FMR, roll): { success: boolean; damage: number }`
    - `resolveParry(damage, SR, FMR, roll): { success: boolean; damage: number; counterTriggered: boolean }`
    - `resolveDefenseless(damage): { success: false; damage: number }`
    - Roll check: `roll <= SR * 20` → success (SR is 0-1 probability)
  - `counterChain.ts`:
    - `resolveCounterChain(state, originalAttacker, parrier, rollFn): { state: CombatState; chainLength: number; actions: AttackResult[] }`
    - Chain logic:
      1. Parrier performs counter attack on original attacker
      2. Original attacker may Parry the counter → if success, they counter back → chain continues
      3. Terminates when: Parry fails, combatant KO'd, stamina depleted
    - Implementation: `while` loop (not recursion) with termination checks
    - Maximum chain depth safety: 10 (prevent infinite loops from bugs)
  - Tests: each defense success/failure, counter chain of length 2 and 3, all 3 termination conditions
- **Acceptance Criteria**:
  1. SR/SMR/FMR-based resolution correct for all 4 defense types
  2. Successful Parry generates counter CombatAction
  3. Counter chain terminates on all 3 conditions
  4. All functions pure
  5. Tests: each defense case, chain lengths 2+3, termination conditions
- **Dependencies**: Task 10, Task 11

---

### Task 13: Elemental Path & Energy/Ascension Systems

- **Approach**: Implement 6 paths as data-driven configuration (path → buff/debuff mapping). Energy tracking as pure accumulation functions. Ascension as threshold checks.
- **Rationale**: Data-driven path config avoids per-path switch statements. Energy/ascension are simple arithmetic with table lookups.
- **Files to Create**: `src/combat/elementalPaths.ts`, `src/combat/energy.ts`, `src/combat/elementalPaths.test.ts`, `src/combat/energy.test.ts`
- **Patterns to Follow**: Constant config objects for path data. Pure functions for all calculations.
- **Key Implementation Details**:
  - `elementalPaths.ts`:
    - Path config table: `ELEMENTAL_PATH_CONFIG: Record<ElementalPath, PathConfig>`
    - `PathConfig`: `{ type: 'action' | 'reaction', defenseBoost: DefenseType, specialForces: DefenseType, buffModifier: number, debuffModifier: number }`
    - Path effects:
      - Reaction paths (Fire, Air, Light): boost own defensive SR for their defense type
      - Action paths (Water, Earth, Shadow): debuff target's defensive SR for their defense type
    - `applyPathBuff(combatant, path): Combatant` — apply reaction path buff to own rates
    - `applyPathDebuff(target, attackerPath): Combatant` — apply action path debuff to target rates
    - `getSpecialForceDefense(attackerPath): DefenseType` — what defense the Special forces on target
  - `energy.ts`:
    - `addEnergySegments(combatant, eventType, result): Combatant` — lookup gain from table, apply ascension bonus, add to current energy
    - `checkAscensionAdvance(combatant): Combatant` — check total segments against thresholds, advance level
    - `getStartingSegments(ascensionLevel): number` — lookup from `ASCENSION_STARTING_SEGMENTS`
    - `resetRoundEnergy(combatant): Combatant` — reset to starting segments for ascension level
    - Accumulation bonus: `gain * (1 + ASCENSION_ACCUMULATION_BONUS[level])`
  - Tests: each path's buff/debuff effect, ascension at all 3 thresholds, energy accumulation with bonus
- **Acceptance Criteria**:
  1. Each path applies correct buff/debuff modifiers
  2. Special defense constraint enforced per path
  3. Energy accumulation matches Excel table
  4. Ascension advances at correct thresholds with correct starting segments
  5. Accumulation bonus applied correctly
  6. Tests: path effects, ascension, energy with bonus
- **Dependencies**: Task 10, Task 11

---

### Task 14: Player Declaration Validation

- **Approach**: Single validation function that checks action type legality, target validity, stamina sufficiency, and energy requirements. Returns typed validation result (valid or error with reason).
- **Rationale**: Straightforward validation. Single entry point for Phase 3.
- **Files to Create**: `src/combat/declaration.ts`, `src/combat/declaration.test.ts`
- **Patterns to Follow**: Pure validation function returning result object (not throwing)
- **Key Implementation Details**:
  - `validateDeclaration(state: CombatState, action: CombatAction): ValidationResult`
  - `ValidationResult`: `{ valid: true } | { valid: false; error: string; fallback?: CombatAction }`
  - Validation checks (in order):
    1. Combatant exists and is not KO'd
    2. Action type is valid (`ActionType` union)
    3. Target validation:
       - ATTACK/SPECIAL: target must be non-KO'd enemy
       - DEFEND: target must be non-KO'd ally
       - EVADE: no target needed (null)
       - GROUP: target must be non-KO'd enemy (leader picks target)
    4. Stamina check: combatant has sufficient stamina for action
    5. SPECIAL: combatant has energy segments > 0
    6. GROUP (from design spec): all non-KO'd allies must have full energy. Reject with fallback to ATTACK on same target.
  - GROUP validation uses `maxEnergyForAscensionLevel()` utility to check "full energy"
- **Acceptance Criteria**:
  1. Valid declarations for all 5 types pass
  2. Invalid target rejected with descriptive error
  3. Insufficient stamina rejected
  4. SPECIAL with no energy rejected
  5. GROUP with incomplete energy rejected, fallback to ATTACK
  6. Pure function
- **Dependencies**: Task 10

---

### Task 15: Action Priority & Resolution Pipeline

- **Approach**: Priority sort as pure function using the `ACTION_PRIORITY` table from types. Per-attack resolution as a 7-step pipeline function. GROUP stub returns no-op.
- **Rationale**: Pure pipeline function matches ADR-012. Priority table from types/combat.ts (already includes GROUP=0 from Task 10).
- **Files to Create**: `src/combat/pipeline.ts`, `src/combat/pipeline.test.ts`
- **Patterns to Follow**: Pure function `(state, action, rollFn) => state`. Roll injection for testability.
- **Key Implementation Details**:
  - `sortByPriority(actions: CombatAction[], state: CombatState, rollFn): CombatAction[]`
    - Sort by `ACTION_PRIORITY[action.type]` ascending (GROUP=0 first, EVADE=3 last)
    - Within same priority: sort by combatant Speed descending + random factor (rollFn)
    - GROUP tie within priority 0: team average speed (from GROUP design spec)
  - `resolvePerAttack(state: CombatState, action: CombatAction, rollFn): CombatState`
    - 7-step pipeline:
      1. Identify true target (check for DEFEND interceptors — combatant who declared DEFEND targeting this attacker's target)
      2. Rank KO roll (from formulas)
      3. Blindside roll (from formulas)
      4. Reaction selection (constrained if Blindsided → Defenseless; if KO'd → skip)
      5. Defense roll and damage calculation (from defense.ts and formulas.ts)
      6. Counter chain resolution (from counterChain.ts)
      7. Stamina/energy updates, buff/debuff application (from energy.ts, elementalPaths.ts)
    - Returns new CombatState with updated combatants and action recorded in history
  - GROUP stub: `if (action.type === 'GROUP') return state` — no-op, replaced in Task 18
  - DEFEND intercept logic: scan action queue for DEFEND actions targeting the same ally as the current attack
- **Acceptance Criteria**:
  1. Correct priority ordering for all action type combinations
  2. Speed-based tie-breaking works within priority bracket
  3. DEFEND intercept redirects attacks correctly
  4. Full 7-step resolution for standard ATTACK
  5. Pure function
  6. GROUP stub returns no-op
  7. Tests: priority ordering, DEFEND intercept, full resolution for 2+ scenarios
- **Dependencies**: Task 11, Task 12, Task 13, Task 14

---

### Task 16: Round Manager Orchestrator

- **Approach**: 5-phase orchestrator calling subsystem functions in sequence. AI behavior tree is a stub producing placeholder actions. Each phase is a distinct function for testability.
- **Rationale**: Phase separation enables testing each phase independently. Stubs for AI and GROUP allow core pipeline testing without design-dependent components.
- **Files to Create**: `src/combat/roundManager.ts`, `src/combat/roundManager.test.ts`
- **Patterns to Follow**: Pure function: `runRound(state, playerDeclarations, rollFn) => CombatState`
- **Key Implementation Details**:
  - `runRound(state: CombatState, playerDeclarations: CombatAction[], rollFn?): CombatState`
    - Phase 1 — AI Decision: call behavior tree evaluator (stub) for each non-KO'd enemy → `CombatAction[]`
    - Phase 2 — Visual Info: assemble payload (stances, stamina, targeting) from state. No state mutation. Returns `VisualInfo` object.
    - Phase 3 — PC Declaration: validate player declarations via `validateDeclaration()`. Handle GROUP ally override per design spec.
    - Phase 4 — Action Queue: merge AI + player actions, sort via `sortByPriority()`
    - Phase 5 — Per-Attack: iterate sorted queue, call `resolvePerAttack()` for each. Track cumulative state changes.
  - AI stub: `function stubBehaviorTree(combatant, state): CombatAction` — returns `{ type: 'ATTACK', targetId: firstNonKOEnemy, combatantId: self.id }`
  - `VisualInfo` type: `{ combatants: { id, stamina, staminaPct, stance, targeting }[] }` — no Phase 1 AI decisions exposed
  - GROUP ally override (Phase 3): if a GROUP declaration passes validation, find all allies' declarations in playerDeclarations and mark them as overridden (remove from action queue)
  - Round result recorded in `state.roundHistory`
  - Victory/defeat check after round: all enemies KO'd → victory, all players KO'd → defeat
- **Acceptance Criteria**:
  1. `runRound()` executes all 5 phases in order, returns updated CombatState
  2. AI stub produces actions incorporated into queue
  3. Visual info doesn't reveal Phase 1 decisions
  4. Phase 4 sorts unified queue by priority
  5. 3v3 round resolves without errors using stubs
  6. Round result recorded in history
- **Dependencies**: Task 15

---

### Task 17: Behavior Tree AI

- **Approach**: Implement the utility-scoring AI system per `design_spec_behavior_tree_ai_system.md`. File structure as specified in the design spec. Each factor is a separate file. Archetype profiles are pure data objects.
- **Rationale**: Design spec is fully specified with types, algorithms, factor scoring tables, and archetype profiles.
- **Files to Create**:
  - `src/combat/behaviorTree/evaluator.ts` — main evaluate() function + scoring loop
  - `src/combat/behaviorTree/perception.ts` — buildPerception()
  - `src/combat/behaviorTree/factors/index.ts` — factor registry
  - `src/combat/behaviorTree/factors/ownStamina.ts`
  - `src/combat/behaviorTree/factors/allyInDanger.ts`
  - `src/combat/behaviorTree/factors/targetVulnerability.ts`
  - `src/combat/behaviorTree/factors/energyAvailability.ts`
  - `src/combat/behaviorTree/factors/speedAdvantage.ts`
  - `src/combat/behaviorTree/factors/roundPhase.ts`
  - `src/combat/behaviorTree/factors/teamBalance.ts`
  - `src/combat/behaviorTree/profiles/index.ts` — profile registry
  - `src/combat/behaviorTree/profiles/elena.ts`
  - `src/combat/behaviorTree/profiles/lars.ts`
  - `src/combat/behaviorTree/profiles/kade.ts`
  - `src/combat/behaviorTree/rankCoefficient.ts`
  - `src/combat/behaviorTree/tieBreaking.ts`
  - Test files: `evaluator.test.ts`, `perception.test.ts`, `factors/*.test.ts` (one per factor or grouped), `profiles/profiles.test.ts`
- **Patterns to Follow**: Design spec Section 4 algorithm. Factor scoring tables from design spec Section 4.
- **Key Implementation Details**:
  - Public interface: `evaluate(combatant: Combatant, state: CombatState, config?: EvaluatorConfig): CombatAction`
  - Internal algorithm (from design spec Section 4):
    1. Load archetype profile by `combatant.archetype`
    2. `buildPerception(combatant, state)` → `CombatPerception`
    3. `rankCoefficient(combatant.rank)` → coefficient (0.2–1.0)
    4. Filter valid action types (exclude GROUP if disabled, SPECIAL if no energy)
    5. For each valid (actionType, target) pair: compute score = baseScore + Σ(weight × factor.evaluate()) × rankCoefficient
    6. Sort candidates by score descending
    7. Tie-break: PATH_TIEBREAK then lowest target stamina
    8. Return CombatAction
  - Factor scoring: implement exact bracket tables from design spec (e.g., OwnStamina < 0.3 → specific scores)
  - Linear interpolation within brackets where spec says "scales linearly"
  - Profile data: copy exact `baseScores` and `factorWeights` from design spec Section 4
  - `config.groupActionsEnabled = false` as default (enabled after GROUP integration in Task 19)
  - SPECIAL energy segments: use all available segments (design spec edge case)
- **Acceptance Criteria**:
  1. Elena/Lars/Kade produce distinct, archetype-appropriate decisions
  2. Deterministic: same state → same action
  3. Interface matches Round Manager Phase 1 expectation
  4. Profiles are data-driven (no per-archetype switch statements)
  5. Tests: each factor, rank coefficient, tie-breaking, evaluator with known inputs, archetype differentiation, determinism, edge cases
- **Dependencies**: Task 10

---

### Task 18: Group Action Type

- **Approach**: Implement GROUP resolution per `design_spec_group_action_type.md`. Single file `groupAction.ts` with resolver function. Extends declaration validation (Task 14) and replaces pipeline GROUP stub (Task 15).
- **Rationale**: Design spec is fully specified. GROUP uses `calculateBaseDamage()` from formulas.ts (shared utility per engineering decision).
- **Files to Create**: `src/combat/groupAction.ts`, `src/combat/groupAction.test.ts`
- **Files to Modify**: `src/combat/declaration.ts` (GROUP validation already specified in Task 14), `src/combat/pipeline.ts` (replace GROUP stub)
- **Patterns to Follow**: Design spec Section 4 algorithm. Pure function returning new CombatState.
- **Key Implementation Details**:
  - `resolveGroup(state: CombatState, declaration: GroupActionDeclaration, config: GroupActionConfig, rollFn): CombatState`
    - Algorithm (from design spec):
      1. Identify participants: leader + non-KO'd allies
      2. Calculate individual damage per participant using `calculateBaseDamage()` from formulas.ts
      3. Sum damages → multiply by `config.damageMultiplier` (1.5)
      4. Resolve Block defense on target: call `resolveBlock()` from defense.ts with forced Block type
      5. Apply final damage to target stamina
      6. Set all participants' energy to 0
      7. Record result, return new CombatState
  - `GROUP_ACTION_CONFIG: GroupActionConfig = { damageMultiplier: 1.5, energyRequirement: 'full' }` — exported constant
  - Opposing GROUP tie-breaking: team average speed, then random factor
  - Edge cases from design spec: KO'd allies reduce participant count but multiplier stays, solo GROUP is valid
  - Pipeline integration: replace `if (action.type === 'GROUP') return state` stub with call to `resolveGroup()`
- **Acceptance Criteria**:
  1. GROUP resolves at priority 0
  2. Invalid GROUP configurations rejected (energy check)
  3. Synergy multiplier (1.5x) applied correctly
  4. GROUP integrates without breaking other action resolution
  5. Tests: rejection on incomplete energy, reduced participants, total damage calc, Block defense, energy consumption, priority sort placement
- **Dependencies**: Task 10, Task 11, Task 12, Task 14, Task 15

---

### Task 19: Combat Integration

- **Approach**: Replace stubs in roundManager with real behavior tree evaluator and GROUP resolver. Validate with multi-round scenario covering all 5 action types.
- **Rationale**: Mechanical wiring — all components exist. Integration test proves the system works end-to-end.
- **Files to Modify**: `src/combat/roundManager.ts` (replace `stubBehaviorTree` with real evaluator import, wire GROUP resolution)
- **Files to Create**: `src/combat/integration.test.ts`
- **Patterns to Follow**: Import real implementations, remove stubs
- **Key Implementation Details**:
  - Replace `stubBehaviorTree()` with `import { evaluate } from './behaviorTree/evaluator.js'`
  - Wire `EvaluatorConfig` with `groupActionsEnabled: true` now that GROUP is implemented
  - Integration test scenarios:
    1. Full 3v3 round with AI producing archetype-differentiated decisions
    2. Round with GROUP action (ensure energy gate, multiplier, Block defense all work through pipeline)
    3. Round with SPECIAL action (energy segments consumed, damage bonus applied)
    4. Round with counter chain (Parry → counter → resolution)
    5. Multi-round scenario: run 3+ rounds, verify state progression
  - Verify: no regressions in Tasks 12-16 unit tests (run full suite)
- **Acceptance Criteria**:
  1. Phase 1 produces archetype-differentiated NPC decisions (not stubs)
  2. GROUP resolves correctly through full pipeline
  3. 3v3 round with diverse action types resolves without errors
  4. No regressions in existing tests
- **Dependencies**: Task 16, Task 17, Task 18

---

### Task 20: GameState-CombatState Synchronization

- **Approach**: Two pure functions for bidirectional sync. `initCombatState` creates CombatState from GameState + EncounterConfig. `syncToGameState` applies combat results back.
- **Rationale**: ADR-013 requires clean separation with explicit sync boundaries.
- **Files to Create**: `src/combat/sync.ts`, `src/combat/sync.test.ts`
- **Patterns to Follow**: Pure functions, immutable state, spread operator
- **Key Implementation Details**:
  - `initCombatState(gameState: GameState, encounter: EncounterConfig): CombatState`
    - Create `Combatant` objects from player party (using GameState character stats) and enemy party (using EncounterConfig)
    - Set: round=1, phase='AI_DECISION', actionQueue=[], roundHistory=[], status='active'
    - Map player team members from GameState's team roster
  - `syncToGameState(gameState: GameState, combatState: CombatState): GameState`
    - Apply: stamina changes, buff/debuff updates from combat back to GameState characters
    - Set `gameState.combatState` to current CombatState
    - Don't modify non-combat GameState fields (dialogue, personality, NPC relationships)
  - `endCombat(gameState: GameState, result: 'victory' | 'defeat'): GameState`
    - Clear `combatState` to null
    - Record completed combat in combat history
  - Tests: init creates valid CombatState, sync preserves non-combat fields, save/load round-trip during combat
- **Acceptance Criteria**:
  1. `initCombatState` produces valid CombatState from GameState
  2. `syncToGameState` applies combat changes without modifying other fields
  3. Pure functions
  4. Save/load during combat preserves CombatState
- **Dependencies**: Task 16, Task 19

---

### Task 21: Combat REST API

- **Approach**: Fastify combat plugin with endpoints for encounter management, declarations, round advancement, and state queries. Registered in app factory alongside Sprint 1 plugins.
- **Rationale**: Thin handler layer like Sprint 1 API. Delegates to domain functions.
- **Files to Create**: `src/api/combat.ts`
- **Files to Modify**: `src/api/index.ts` (register combat plugin)
- **Patterns to Follow**: Sprint 1 Fastify plugin pattern. ApiResponse envelope. JSON Schema validation.
- **Key Implementation Details**:
  - Endpoints:
    - `POST /api/combat/encounter` — body: `{ encounterId }` → `initCombatState()`, set `fastify.gameState.combatState`
    - `POST /api/combat/declare` — body: `{ actions: CombatAction[] }` → validate each via `validateDeclaration()`, store for Phase 3
    - `POST /api/combat/round` — advance round via `runRound()`, sync to GameState
    - `GET /api/combat/state` — return `VisualInfo` (Phase 2 data, no AI decisions)
    - `GET /api/combat/history` — return `roundHistory` from CombatState
    - `GET /api/combat/result` — return combat status (active/victory/defeat)
  - Combat state lifecycle:
    - Encounter init → rounds (declare → advance) → victory/defeat → `endCombat()`
    - Phase tracking prevents out-of-order operations (can't declare before encounter init)
  - Error handling: no active combat → GAME_NOT_FOUND variant, invalid declarations → structured errors
- **Acceptance Criteria**:
  1. All combat endpoints respond correctly for valid scenarios
  2. Invalid declarations return error responses
  3. Combat state persists across sequential API calls
  4. Visual info endpoint doesn't expose AI decisions
- **Dependencies**: Task 19, Task 20

---

### Task 22: End-to-End 3v3 Demo Encounter

- **Approach**: Static JSON encounter fixture from Excel "Battle Scenarios" sheet. E2E test drives the encounter through the combat REST API. Deterministic: use seeded rollFn for reproducibility.
- **Rationale**: This is the pitch deliverable. Static fixture keeps it simple. Deterministic seeds enable exact verification against Excel.
- **Files to Create**: `src/fixtures/encounter.json`, `src/combat/e2e.test.ts`
- **Patterns to Follow**: Fastify injection for API-driven testing. Deterministic roll sequences.
- **Key Implementation Details**:
  - `encounter.json`: 3 player combatants + 3 enemy combatants with stats from Excel "Battle Scenarios" sheet
  - E2E test:
    1. Create new game via API
    2. Initialize encounter via `/api/combat/encounter`
    3. Submit player declarations via `/api/combat/declare`
    4. Advance round via `/api/combat/round`
    5. Verify formula outputs match Excel (at least: 1 Rank KO check, 1 Blindside check, 1 defense roll, 1 energy accumulation, 1 ascension threshold)
    6. Run multiple rounds to completion (or fixed round count)
    7. Verify determinism: run twice from same state, identical results
  - Seeded randomness: inject a deterministic `rollFn` that produces a fixed sequence (e.g., `[12, 5, 18, 3, 15, ...]`)
  - API-driven: all interactions through HTTP endpoints (via Fastify inject), not internal function calls
  - Victory/defeat check: encounter runs until status changes from 'active'
- **Acceptance Criteria**:
  1. 3v3 encounter resolves through all 5 phases per round to completion
  2. Formula outputs match Excel for deterministic inputs
  3. Identical results when run twice (deterministic)
  4. Driven through combat REST API
- **Dependencies**: Task 21

---

## Required User Steps

- **Excel verification (Task 11, 22)**: During TDD formula porting, the build agent will need specific input/output pairs from `GM Combat Tracker.xlsx`. The user should verify test values against the Excel file if automated extraction isn't feasible.
- **Encounter fixture data (Task 22)**: The 3v3 encounter config (character stats, party composition) needs to be sourced from the Excel "Battle Scenarios" sheet. User may need to provide specific values if the Excel file can't be read programmatically.
- **No server commands needed**: All testing uses Vitest (no manual server startup). All API tests use Fastify injection (no HTTP server required).

---

## Engineering Questions Resolved

| Question (from Plan Section 10) | Decision | Rationale |
|---|---|---|
| Random roll injection mechanism | Per-call-site: `rollFn` parameter with default `() => Math.random() * 20` | Clean, testable, no global state. Tests pass fixed functions. |
| Fastify session state singleton | `fastify.decorate('gameState', null)` with declaration merging | Idiomatic Fastify pattern. Type-safe. All plugins access via `fastify.gameState`. |
| Sprint 1 vs Sprint 2 type file split | `types/index.ts` (Sprint 1) + `types/combat.ts` (Sprint 2) | Plan specifies this split. `combat.ts` imports from `index.ts`. |
| Immutability mechanism | Spread operator + `Readonly<T>` type annotations | User preference. Matches archived prototype's spread pattern. Readonly adds compile-time enforcement. |
| CombatAction typing | Single interface with optional fields | Plan already defines: `{ combatantId, type, targetId, energySegments? }`. Design specs confirm no new fields needed for GROUP. |
| Test file placement | Co-located: `*.test.ts` next to source. Integration/E2E in `tests/` | Modern convention. Plan recommends this approach. |
| ESLint/Prettier | Add both (user preference) | Flat config ESLint + Prettier. Enforces consistency. |
| Module system | ESM (`"type": "module"`, `"module": "NodeNext"`) | Vitest and Fastify native support. Clean import/export. User approved. |
| Shared damage calculation | `calculateBaseDamage()` in `formulas.ts` | Single source of truth. Both pipeline and GROUP import from same function. User approved. |

---

## Risk Notes

| Risk | Mitigation |
|---|---|
| ESM import path `.js` extensions may confuse build agents | Code specs explicitly note this requirement. Vitest config handles `.ts` → `.js` resolution. |
| Formula TDD requires Excel values — build agent may not have Excel access | Spec the test values inline from `GM_Combat_Tracker_Documentation.md`. User verifies against Excel where docs are insufficient. |
| Behavior tree has 20+ files — large task surface | Clear file-by-file spec with exact scoring tables. Factors are independent and can be implemented/tested in isolation. |
| GROUP priority 0 change affects pipeline sorting | Priority table defined in `types/combat.ts` constants (Task 10). Pipeline reads from this table — change is isolated. |
| Counter chain infinite loop | Safety cap at 10 iterations. While-loop with explicit termination checks. |
