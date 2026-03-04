# Blueprint: Game Backend Engine -- Sprint 3 Narrative & State Machine

**Specialist:** Game Backend Engine
**Sprint:** 3
**Tasks:** T1, T2, T3, T4, T5, T6, T7, T8, T9, T12
**Date:** 2026-03-02
**Status:** Approved for execution

---

## 1. Task Reference

| Task | Title | Depth | Dependencies | Acceptance Criteria Count |
|------|-------|-------|--------------|--------------------------|
| T1 | Narrative Type System | Light | None | 4 |
| T2 | Scene Graph Engine | Standard | T1 | 5 |
| T3 | Choice & Consequence Engine | Standard | T1, T2 | 5 |
| T4 | Narrative State Machine | Standard | T2, T3 | 5 |
| T5 | Narrative State Updaters | Light | T4 | 4 |
| T6 | Persistence Integration | Light | T5 | 5 |
| T7 | Team Synergy Calculator | Light | T1 | 7 |
| T8 | Combat Synergy Integration | Light | T7 | 4 |
| T9 | Narrative REST API | Standard | T5, T6 | 5 |
| T12 | Integration Validation | Light | T8, T9 | 5 |

**Dependency chains:**
- Chain A: T1 -> T2 -> T3 -> T4 -> T5 -> T6 -> T9 -> T12
- Chain B: T1 -> T7 -> T8 -> T12
- Chains converge at T12

---

## 2. Research Findings

### Codebase Architecture (confirmed via Stage 1 exploration)

**Type files:**
- `src/types/index.ts` -- Root type barrel. GameState (7 fields), Personality, PersonalityTrait, PersonalityAdjustment, PersonalityGate, PlayerCharacter, NPC, DialogueNode, DialogueOption, DialogueResult, ConversationEntry, ApiResponse, ApiError, ErrorCodes. [RF-1]
- `src/types/combat.ts` -- Standalone leaf file, zero imports from index.ts. CombatState, Combatant, CombatantConfig, EncounterConfig, all combat types. One-way dependency: index.ts imports CombatState from combat.ts. [RF-1]

**Engine modules:**
- `src/dialogue/dialogueEngine.ts` -- Graph traversal pattern template. Functions: `evaluatePersonalityGate`, `getAvailableOptions`, `getStartingNode`, `findNode`, `processDialogueSelection`, `validateDialogueTree`. Stateless tree passed as parameter. [RF-3]
- `src/personality/personalitySystem.ts` -- `adjustPersonality()`, `createDefaultPersonality()`, `validatePersonality()`. Pure functions, 3-pass normalization algorithm. [RF-2]

**State updaters:**
- `src/state/stateUpdaters.ts` -- Canonical pattern: `(state: Readonly<GameState>, ...) => GameState`. Every updater wraps with `updateTimestamp()`. Functions: `updateTimestamp`, `updatePlayerPersonality`, `applyPersonalityAdjustment`, `updateNPCAffection`, `updateNPCTrust`, `updateNPCRelationship`, `addConversationEntry`, `processDialogueChoice`, `updateCombatState`. [RF-2]
- `src/state/gameState.ts` -- `createNewGameState()` factory. Sets all nullable subsystems to null. [RF-7]
- `src/state/npcs.ts` -- NPC_TEMPLATES (Elena, Lars, Kade), `getNPC()`, `getAllNPCs()`. Object.freeze'd. [RF-7]

**Combat sync:**
- `src/combat/sync.ts` -- `initCombatState(_gameState, encounter)` where `_gameState` is unused placeholder. `_configToCombatant()` internal helper. `syncToGameState()`, `endCombat()`. [RF-4]

**Persistence:**
- `src/persistence/saveLoad.ts` -- `validateGameState()` runtime type guard. `saveGame()`, `loadGame()`, `listSaves()`, `deleteSave()`. combatState accepts any value (no deep validation). No schema versioning. [RF-5]

**API layer:**
- `src/api/index.ts` -- `buildApp()` factory. 5 plugins registered: game, player, npc, dialogue, combat. GameStateContainer decorated onto Fastify. `globalErrorHandler` registered before plugins. [RF-6]
- `src/api/dialogue.ts` -- Plugin pattern reference: `async function dialoguePlugin(fastify: FastifyInstance): Promise<void>`. Route generics for Params/Body, JSON Schema validation, ApiResponse<T> return. [RF-6]

**Fixtures:**
- `src/fixtures/encounter.json` -- EncounterConfig JSON with playerParty/enemyParty arrays. [RF-10]
- `src/fixtures/.gitkeep` -- Directory exists, ready for new files.

**Test files (24 total, co-located .test.ts pattern):**
- `src/dialogue/dialogueEngine.test.ts` -- Uses fixtures.js imports, `createNewGameState()`, `makePersonality()` helper. [RF-8]
- `src/state/stateUpdaters.test.ts`, `src/persistence/saveLoad.test.ts`, `src/combat/sync.test.ts` -- All relevant for integration. [RF-8]

### Existing GameState Shape (confirmed)

```typescript
interface GameState {
  readonly player: PlayerCharacter;
  readonly npcs: Record<string, NPC>;
  readonly currentDialogueNode: string | null;
  readonly saveSlot: number | null;
  readonly combatState: CombatState | null;
  readonly conversationLog: ConversationEntry[];
  readonly timestamp: number;
}
```

### Updater Signature Pattern (confirmed)

```typescript
function updaterName(state: Readonly<GameState>, ...args): GameState {
  return updateTimestamp({ ...state, /* changes */ });
}
```

### Fastify Plugin Pattern (confirmed)

```typescript
export async function pluginName(fastify: FastifyInstance): Promise<void> {
  fastify.get<{ Params: { ... } }>('/path', { schema: { ... } }, async (request, reply) => {
    // Access state: fastify.gameStateContainer.state
    // Return: ApiResponse<T>
  });
}
```

### NPC Personality Data (confirmed from npcs.ts)

| NPC | patience | empathy | cunning | logic | kindness | charisma |
|-----|----------|---------|---------|-------|----------|----------|
| Elena | 20 | 20 | 10 | 15 | 20 | 15 |
| Lars | 10 | 8 | 28 | 25 | 12 | 17 |
| Kade | 12 | 8 | 25 | 18 | 10 | 27 |

---

## 3. Approach

The approved direction integrates user decisions D1-D6 with all seven accepted assumptions (A1-A7).

**Type Hierarchy Strategy [A1, A7, D6]:**
Create `src/types/narrative.ts` as a standalone leaf file (zero imports from index.ts), mirroring the `types/combat.ts` pattern. All narrative types (Scene, SceneChoice, ScenePrerequisite, ChoiceConsequence, NarrativeState) and synergy types (SynergyBonus, SynergyResult, ParadigmConfig, SynergyStatType) reside here. `types/index.ts` imports `NarrativeState` and re-exports it, extending GameState with `narrativeState: NarrativeState | null`. Narrative error codes are added to the existing ErrorCodes const object in `types/index.ts`.

**Engine Architecture [A2, A5, D1, D2, D4]:**
Three engine modules in `src/narrative/`:
- `sceneEngine.ts` -- Scene graph loading, prerequisite evaluation (flat array, implicit AND), available choice filtering, dead-end validation. Scene graphs passed as parameters (stateless), independent from dialogue engine.
- `choiceEngine.ts` -- Choice validation, effect application by reusing existing state updaters directly (`applyPersonalityAdjustment`, `updateNPCRelationship`, etc.), flag setting.
- `narrativeStateMachine.ts` -- Thin orchestrator managing scene transitions, NarrativeState bookkeeping. Returns discriminated union result type (`NarrativeTransitionResult`).
Plus `synergyCalculator.ts` -- Pure paradigm-based synergy calculation per approved design spec.

**State Management [A3, A4, A6, D2]:**
- `NarrativeState.visitedSceneIds` stored as `readonly string[]` (JSON-safe).
- `NarrativeState.choiceFlags` stored as `Readonly<Record<string, boolean>>` (simple flag map).
- Narrative updaters in `stateUpdaters.ts` follow canonical pattern. Silent return on null narrativeState.
- Choice processing reuses existing updaters: personality adjustment -> NPC relationship -> flags -> log.

**API Integration [D3, RF-6]:**
- New `narrativePlugin` in `src/api/narrative.ts` registered as 6th plugin.
- Scene JSON fixtures in `src/fixtures/scenes/`.
- Synergy config in `src/fixtures/synergyConfig.ts`.

**Combat Integration [D5, RF-4, RF-9]:**
- `initCombatState` reads from the previously-unused `_gameState` parameter.
- Calls `calculateSynergy()` with player personality + all NPC personalities (all 3 NPCs = party for Sprint 3).
- Applies stat multiplier to player party combatants only. Guarded: null/missing data = no synergy = backward-compatible.

---

## 4. Decisions Made

### D1: Scene Prerequisite Model -- Flat Array, Implicit AND
- **Options presented:** (A) Flat array with implicit AND, (B) Flat list with operator field (all/any), (C) Compound AND/OR nesting
- **Chosen:** Option A
- **Why:** Sufficient for Sprint 3 (2-3 scenes). OR logic can be simulated via separate scene variants. Mirrors dialogue engine's single-condition PersonalityGate pattern. Non-breaking extension to add `prerequisiteMode` later.
- **Rejected:** B adds unnecessary complexity for Sprint 3 scope. C is over-engineered for 2-3 scenes.

### D2: SceneChoice Effect Reuse -- Direct Reuse of Existing Updaters
- **Options presented:** (A) Reuse existing updaters directly, (B) New narrative-specific effect updaters, (C) Shared effect types with generic applier
- **Chosen:** Option A
- **Why:** Plan explicitly requires reuse of `updateNPCAffection`, `updateNPCTrust`, `updateNPCRelationship`. `processDialogueChoice` demonstrates the chaining pattern. Same GameState, same effect semantics.
- **Rejected:** B duplicates logic unnecessarily. C adds abstraction without Sprint 3 benefit.

### D3: Scene JSON Location -- src/fixtures/scenes/
- **Options presented:** (A) `src/fixtures/scenes/`, (B) `src/narrative/scenes/`, (C) `src/data/scenes/`
- **Chosen:** Option A
- **Why:** Established pattern: `encounter.json` lives in `src/fixtures/`. Scene JSON is authored data, not engine logic. Fixtures directory is the canonical home for data files.
- **Rejected:** B mixes data with code. C creates a new convention without precedent.

### D4: NarrativeState Result Type -- Discriminated Union
- **Options presented:** (A) Discriminated union (success | error), (B) Bundled result like DialogueResult, (C) State-only return
- **Chosen:** Option A
- **Why:** Plan requires typed errors for invalid transitions. Discriminated union cleanly separates success from failure without exceptions. API layer maps error variants to HTTP responses.
- **Rejected:** B lacks error typing required by plan. C provides no error information.

### D5: initCombatState Synergy Integration -- Read from GameState
- **Options presented:** (A) Read from GameState parameter, (B) Synergy as extra parameter, (C) Post-processing by caller
- **Chosen:** Option A
- **Why:** `_gameState` parameter exists as placeholder for exactly this extension. Encapsulates synergy within combat init. Design spec confirms this integration point.
- **Rejected:** B exposes synergy mechanics to callers unnecessarily. C breaks encapsulation.

### D6: Narrative Error Codes -- Extend Existing ErrorCodes
- **Options presented:** (A) Extend existing ErrorCodes in types/index.ts, (B) Separate NarrativeErrorCodes in types/narrative.ts
- **Chosen:** Option A
- **Why:** Single source of truth for all error codes. API error responses use `ApiError.code` referencing these constants. Additive change, won't break existing code.
- **Rejected:** B fragments error code definitions across files.

### Assumptions Accepted (all defaults confirmed)

- **A1:** Narrative types in separate `types/narrative.ts` file (leaf pattern)
- **A2:** Scene graphs are stateless parameters (not stored on GameState)
- **A3:** Visited scenes stored as `readonly string[]` (not Set)
- **A4:** Choice flags stored as `Readonly<Record<string, boolean>>`
- **A5:** Scene engine and dialogue engine remain independent (no shared utilities)
- **A6:** Silent error handling for missing narrative state (return input state unchanged)
- **A7:** Synergy types co-located in `types/narrative.ts`

---

## 5. Deliverable Specification

### 5.1 Type Definitions

#### File: `src/types/narrative.ts` (NEW) [A1, A7]

This is a standalone leaf file with zero imports from `types/index.ts`. The one-way dependency is `index.ts` -> `narrative.ts`.

```typescript
// ============================================================================
// Synergy Types
// ============================================================================

/**
 * The stat types synergy can modify on combatants.
 */
export type SynergyStatType = 'power' | 'speed';

/**
 * Output of a satisfied paradigm -- the bonus to apply.
 */
export interface SynergyBonus {
  readonly paradigmName: string;    // e.g., "Well Rounded", "Bond"
  readonly stat: SynergyStatType;   // which combatant stat gets modified
  readonly multiplier: number;      // e.g., 1.10 for +10%
  readonly matchQuality: number;    // 0.0-1.0+, used for highest-only comparison
}

/**
 * Result of the synergy calculation. null = no paradigm satisfied.
 */
export type SynergyResult = SynergyBonus | null;

/**
 * Data-driven configuration for a single synergy paradigm.
 */
export interface ParadigmConfig {
  readonly name: string;                          // display name
  readonly type: 'well_rounded' | 'bond';         // evaluation algorithm selector
  readonly threshold: number;                      // activation threshold (integer: 25 = 25%)
  readonly stat: SynergyStatType;                  // stat to modify
  readonly multiplier: number;                     // stat multiplier (e.g., 1.10)
}

// ============================================================================
// Scene Prerequisite Types
// ============================================================================

/**
 * The kind of prerequisite condition to evaluate.
 */
export type PrerequisiteType = 'trait' | 'flag' | 'visited_scene';

/**
 * Comparison operators for trait-based prerequisites.
 */
export type PrerequisiteOperator = 'gte' | 'lte' | 'eq';

/**
 * A single prerequisite condition for a scene.
 * Scenes have an array of these; ALL must pass (implicit AND). [D1]
 *
 * Discriminated by `type` field:
 * - trait: checks player personality trait against threshold
 * - flag: checks if a choice flag is set (true) in NarrativeState.choiceFlags
 * - visited_scene: checks if a scene ID is in NarrativeState.visitedSceneIds
 */
export interface ScenePrerequisite {
  readonly type: PrerequisiteType;
  readonly trait?: string;                // required when type === 'trait'
  readonly operator?: PrerequisiteOperator; // required when type === 'trait'
  readonly value?: number;                 // required when type === 'trait' (integer, e.g., 25)
  readonly flag?: string;                  // required when type === 'flag'
  readonly sceneId?: string;               // required when type === 'visited_scene'
}

// ============================================================================
// Choice & Consequence Types
// ============================================================================

/**
 * NPC relationship adjustment triggered by a choice.
 */
export interface NpcEffect {
  readonly npcId: string;
  readonly affectionChange?: number;  // delta applied to NPC affection (-100 to +100 range)
  readonly trustChange?: number;      // delta applied to NPC trust (-100 to +100 range)
}

/**
 * Personality adjustment triggered by a choice.
 * Keys are PersonalityTrait names; values are integer deltas.
 */
export type PersonalityEffect = Readonly<Record<string, number>>;

/**
 * The full consequence specification for a choice.
 */
export interface ChoiceConsequence {
  readonly personalityEffect?: PersonalityEffect;  // trait adjustments
  readonly npcEffects?: readonly NpcEffect[];       // NPC relationship changes
  readonly setFlags?: readonly string[];            // flag names to set to true
  readonly clearFlags?: readonly string[];          // flag names to set to false (remove)
}

/**
 * Personality gate for a scene choice -- mirrors the dialogue system's PersonalityGate.
 * Defined here to avoid importing from index.ts (leaf file constraint).
 */
export interface ScenePersonalityGate {
  readonly trait: string;                   // PersonalityTrait name
  readonly operator: 'gte' | 'lte' | 'eq'; // comparison operator
  readonly value: number;                   // threshold (integer, 5-35 range)
}

/**
 * A single choice available within a scene.
 */
export interface SceneChoice {
  readonly id: string;                      // unique within the scene
  readonly text: string;                    // display text for the choice
  readonly gate?: ScenePersonalityGate;     // undefined = always available (ungated)
  readonly consequence?: ChoiceConsequence;  // effects applied when chosen
  readonly nextSceneId: string | null;      // scene to transition to (null = end narrative)
}

// ============================================================================
// Scene Types
// ============================================================================

/**
 * A single node in the scene graph.
 */
export interface Scene {
  readonly id: string;                              // unique scene identifier
  readonly title: string;                           // scene title for display
  readonly text: string;                            // narrative text content
  readonly choices: readonly SceneChoice[];          // available player choices
  readonly prerequisites: readonly ScenePrerequisite[]; // ALL must pass for scene access [D1]
}

/**
 * A complete scene graph: an array of Scene nodes.
 * Passed as a parameter to engine functions (not stored on state). [A2]
 */
export type SceneGraph = readonly Scene[];

// ============================================================================
// Narrative State
// ============================================================================

/**
 * Runtime narrative state tracked on GameState.
 * Stored on GameState as `narrativeState: NarrativeState | null`.
 */
export interface NarrativeState {
  readonly currentSceneId: string;                              // ID of the active scene
  readonly visitedSceneIds: readonly string[];                   // scenes the player has visited [A3]
  readonly choiceFlags: Readonly<Record<string, boolean>>;       // consequence flags [A4]
  readonly sceneHistory: readonly SceneHistoryEntry[];            // ordered log of scene transitions
}

/**
 * A single entry in the scene transition history log.
 */
export interface SceneHistoryEntry {
  readonly sceneId: string;
  readonly choiceId: string;
  readonly timestamp: number;
}

// ============================================================================
// State Machine Result Types [D4]
// ============================================================================

/**
 * Successful scene transition result.
 */
export interface NarrativeTransitionSuccess {
  readonly type: 'success';
  readonly state: NarrativeState;         // updated NarrativeState
  readonly nextScene: Scene | null;       // the scene transitioned to (null = narrative ended)
  readonly choiceId: string;              // the choice that was made
}

/**
 * Failed scene transition result with typed error.
 */
export interface NarrativeTransitionError {
  readonly type: 'error';
  readonly code: string;                  // ErrorCodes constant value
  readonly message: string;               // human-readable error description
}

/**
 * Discriminated union for state machine transition results.
 */
export type NarrativeTransitionResult = NarrativeTransitionSuccess | NarrativeTransitionError;

// ============================================================================
// Scene Engine Result Types
// ============================================================================

/**
 * Result of getting the current scene with available choices filtered.
 */
export interface CurrentSceneResult {
  readonly scene: Scene;
  readonly availableChoices: readonly SceneChoice[];  // filtered by gates + prerequisites
}
```

**Field-level notes:**
- `ScenePrerequisite.trait`, `.operator`, `.value` are optional because they're only used when `type === 'trait'`. Similarly `.flag` for `'flag'` type, `.sceneId` for `'visited_scene'` type. This avoids creating separate interfaces per prerequisite type while keeping a single flat structure suitable for JSON authoring.
- `ScenePersonalityGate` mirrors `PersonalityGate` from index.ts but is defined independently to maintain the leaf file constraint [A1]. The `trait` field is typed as `string` rather than `PersonalityTrait` to avoid importing from index.ts.
- `NarrativeState.sceneHistory` provides an audit trail similar to `conversationLog` for dialogues.
- `ChoiceConsequence.clearFlags` is included for completeness (resetting flags) but Sprint 3 scenes may only use `setFlags`.

#### File: `src/types/index.ts` (EXTENDED) [D6, RF-1]

**Add import at top (after CombatState import):**
```typescript
import type { NarrativeState } from './narrative.js';
```

**Add re-export (after the import):**
```typescript
export type { NarrativeState } from './narrative.js';
```

**Extend GameState interface -- add field after `combatState`:**
```typescript
readonly narrativeState: NarrativeState | null;
```

**Extend ErrorCodes const object -- add after existing entries:**
```typescript
// Narrative error codes (Sprint 3)
SCENE_NOT_FOUND: 'SCENE_NOT_FOUND',
CHOICE_NOT_AVAILABLE: 'CHOICE_NOT_AVAILABLE',
CHOICE_NOT_FOUND: 'CHOICE_NOT_FOUND',
NARRATIVE_NOT_STARTED: 'NARRATIVE_NOT_STARTED',
INVALID_TRANSITION: 'INVALID_TRANSITION',
PREREQUISITE_NOT_MET: 'PREREQUISITE_NOT_MET',
```

#### File: `src/state/gameState.ts` (EXTENDED) [RF-7]

**Update `createNewGameState()` to include:**
```typescript
narrativeState: null,
```

### 5.2 Engine Logic

#### File: `src/narrative/sceneEngine.ts` (NEW) [RF-3, A2, A5, D1]

**Imports:**
```typescript
import type { Personality } from '../types/index.js';
import type {
  Scene,
  SceneGraph,
  SceneChoice,
  ScenePrerequisite,
  ScenePersonalityGate,
  NarrativeState,
  CurrentSceneResult,
} from '../types/narrative.js';
```

**Function: `evaluateScenePersonalityGate`**
```typescript
export function evaluateScenePersonalityGate(
  gate: ScenePersonalityGate,
  personality: Readonly<Personality>
): boolean
```
- **Behavior:** Compares `personality[gate.trait as keyof Personality]` against `gate.value` using `gate.operator` (gte -> `>=`, lte -> `<=`, eq -> `Math.abs(diff) < 0.1`).
- **Edge cases:** Unknown operator returns false. Unknown trait key returns false (trait value is `undefined`, which fails all numeric comparisons).
- **Pure function:** No side effects, deterministic.
- **Cite:** Independent implementation [A5], mirrors dialogue engine's `evaluatePersonalityGate` logic [RF-3].

**Function: `evaluatePrerequisite`**
```typescript
export function evaluatePrerequisite(
  prerequisite: ScenePrerequisite,
  personality: Readonly<Personality>,
  narrativeState: Readonly<NarrativeState>
): boolean
```
- **Behavior by type:**
  - `'trait'`: Calls `evaluateScenePersonalityGate({ trait: prerequisite.trait!, operator: prerequisite.operator!, value: prerequisite.value! }, personality)`. Returns false if trait/operator/value are missing.
  - `'flag'`: Returns `narrativeState.choiceFlags[prerequisite.flag!] === true`. Returns false if flag is missing from prerequisite.
  - `'visited_scene'`: Returns `narrativeState.visitedSceneIds.includes(prerequisite.sceneId!)`. Returns false if sceneId is missing from prerequisite.
- **Unknown type:** Returns false.
- **Pure function.**

**Function: `evaluateAllPrerequisites`**
```typescript
export function evaluateAllPrerequisites(
  prerequisites: readonly ScenePrerequisite[],
  personality: Readonly<Personality>,
  narrativeState: Readonly<NarrativeState>
): boolean
```
- **Behavior:** Returns `prerequisites.every(p => evaluatePrerequisite(p, personality, narrativeState))`. Empty array returns true (no prerequisites = always accessible). [D1: implicit AND]
- **Pure function.**

**Function: `getAvailableChoices`**
```typescript
export function getAvailableChoices(
  scene: Scene,
  personality: Readonly<Personality>
): SceneChoice[]
```
- **Behavior:** Returns `scene.choices.filter(choice => choice.gate === undefined || evaluateScenePersonalityGate(choice.gate, personality))`.
- **Guarantee:** If scene passes dead-end validation, at least one choice is always returned.
- **Pure function.**

**Function: `findScene`**
```typescript
export function findScene(
  sceneId: string,
  sceneGraph: SceneGraph
): Scene | undefined
```
- **Behavior:** Returns `sceneGraph.find(s => s.id === sceneId)`.
- **Pure function.**

**Function: `getCurrentScene`**
```typescript
export function getCurrentScene(
  narrativeState: Readonly<NarrativeState>,
  personality: Readonly<Personality>,
  sceneGraph: SceneGraph
): CurrentSceneResult | null
```
- **Behavior:** Looks up `narrativeState.currentSceneId` in sceneGraph. If not found, returns null. Otherwise returns `{ scene, availableChoices: getAvailableChoices(scene, personality) }`.
- **Pure function.**

**Function: `validateSceneGraph`**
```typescript
export function validateSceneGraph(sceneGraph: SceneGraph): {
  valid: boolean;
  problematicScenes: string[];
}
```
- **Behavior:** For each scene in sceneGraph, checks that at least one choice has `gate === undefined` (ungated fallback). Scenes where ALL choices are gated are flagged as problematic. Mirrors `validateDialogueTree` pattern [RF-3].
- **Returns:** `{ valid: true/false, problematicScenes: string[] }`.
- **Pure function.**

**Function: `getAccessibleScenes`**
```typescript
export function getAccessibleScenes(
  sceneGraph: SceneGraph,
  personality: Readonly<Personality>,
  narrativeState: Readonly<NarrativeState>
): Scene[]
```
- **Behavior:** Returns all scenes from sceneGraph where `evaluateAllPrerequisites(scene.prerequisites, personality, narrativeState)` is true.
- **Pure function.**

#### File: `src/narrative/choiceEngine.ts` (NEW) [D2, RF-2, RF-3]

**Imports:**
```typescript
import type { GameState } from '../types/index.js';
import type {
  Scene,
  SceneChoice,
  ChoiceConsequence,
  NarrativeState,
} from '../types/narrative.js';
import {
  applyPersonalityAdjustment,
  updateNPCRelationship,
} from '../state/stateUpdaters.js';
import { evaluateScenePersonalityGate } from './sceneEngine.js';
```

**Function: `validateChoice`**
```typescript
export function validateChoice(
  scene: Scene,
  choiceId: string,
  personality: Readonly<import('../types/index.js').Personality>
): { valid: true; choice: SceneChoice } | { valid: false; code: string; message: string }
```
- **Behavior:**
  1. Find choice in `scene.choices` by `choiceId`. If not found: `{ valid: false, code: 'CHOICE_NOT_FOUND', message: ... }`.
  2. If choice has a gate, evaluate it via `evaluateScenePersonalityGate`. If gate fails: `{ valid: false, code: 'CHOICE_NOT_AVAILABLE', message: ... }`.
  3. Otherwise: `{ valid: true, choice }`.
- **Pure function.**

**Function: `applyConsequence`**
```typescript
export function applyConsequence(
  state: Readonly<GameState>,
  consequence: ChoiceConsequence
): GameState
```
- **Behavior (chained in order, mirroring `processDialogueChoice` pattern [RF-2]):**
  1. **Personality effect:** If `consequence.personalityEffect` exists and has keys, call `applyPersonalityAdjustment(state, consequence.personalityEffect as import('../types/index.js').PersonalityAdjustment)`. [D2: reuse existing updater]
  2. **NPC effects:** For each entry in `consequence.npcEffects` (if present), call `updateNPCRelationship(state, npcEffect.npcId, npcEffect.affectionChange ?? 0, npcEffect.trustChange ?? 0)`. [D2: reuse existing updater]
  3. **Flags:** Handled separately by the state machine (flag updates go on NarrativeState, not GameState directly). This function only handles GameState-level effects.
- **Returns:** Updated GameState with personality and NPC relationship changes applied.
- **Silent handling:** If `consequence` has no effects, returns input state unchanged (spread copy). [A6]
- **Pure function, no mutations.**

**Function: `processSceneChoice`**
```typescript
export function processSceneChoice(
  state: Readonly<GameState>,
  scene: Scene,
  choiceId: string
): { type: 'success'; state: GameState; choice: SceneChoice }
 | { type: 'error'; code: string; message: string }
```
- **Behavior:**
  1. Call `validateChoice(scene, choiceId, state.player.personality)`.
  2. If invalid: return the error.
  3. If valid: call `applyConsequence(state, choice.consequence ?? {})`.
  4. Return `{ type: 'success', state: updatedState, choice }`.
- **Pure function.**

#### File: `src/narrative/narrativeStateMachine.ts` (NEW) [D4, RF-2, RF-3]

**Imports:**
```typescript
import type { GameState } from '../types/index.js';
import type {
  Scene,
  SceneGraph,
  NarrativeState,
  NarrativeTransitionResult,
  SceneHistoryEntry,
} from '../types/narrative.js';
import { findScene, evaluateAllPrerequisites, getAvailableChoices } from './sceneEngine.js';
import { processSceneChoice } from './choiceEngine.js';
```

**Function: `createInitialNarrativeState`**
```typescript
export function createInitialNarrativeState(startingSceneId: string): NarrativeState
```
- **Behavior:** Returns:
  ```typescript
  {
    currentSceneId: startingSceneId,
    visitedSceneIds: [startingSceneId],
    choiceFlags: {},
    sceneHistory: [],
  }
  ```
- **Pure function.**

**Function: `advanceNarrative`**
```typescript
export function advanceNarrative(
  gameState: Readonly<GameState>,
  choiceId: string,
  sceneGraph: SceneGraph
): NarrativeTransitionResult
```
- **Behavior:**
  1. **Guard:** If `gameState.narrativeState === null`: return `{ type: 'error', code: 'NARRATIVE_NOT_STARTED', message: 'Narrative has not been started' }`.
  2. **Find current scene:** `findScene(gameState.narrativeState.currentSceneId, sceneGraph)`. If null: return `{ type: 'error', code: 'SCENE_NOT_FOUND', message: ... }`.
  3. **Process choice:** Call `processSceneChoice(gameState, currentScene, choiceId)`. If error: return the error as `NarrativeTransitionError`.
  4. **Resolve next scene:** Read `choice.nextSceneId` from the processed choice. If null (narrative ends): update NarrativeState with final bookkeeping, return success with `nextScene: null`.
  5. **Validate next scene prerequisites:** Find next scene in graph. If not found: return error `SCENE_NOT_FOUND`. Evaluate prerequisites using `evaluateAllPrerequisites` against the *updated* state (post-consequence). If not met: return `{ type: 'error', code: 'PREREQUISITE_NOT_MET', message: ... }`.
  6. **Build updated NarrativeState:**
     ```typescript
     const updatedNarrativeState: NarrativeState = {
       currentSceneId: nextScene.id,
       visitedSceneIds: [...gameState.narrativeState.visitedSceneIds, nextScene.id],
       choiceFlags: applyFlags(gameState.narrativeState.choiceFlags, choice.consequence),
       sceneHistory: [...gameState.narrativeState.sceneHistory, historyEntry],
     };
     ```
  7. **Return:** `{ type: 'success', state: updatedNarrativeState, nextScene, choiceId }`.
- **Note:** This function returns `NarrativeTransitionResult` which includes `NarrativeState` (not full GameState). The state updater layer (T5) is responsible for integrating the NarrativeState back into GameState.
- **Pure function.**

**Function: `applyFlags` (internal helper)**
```typescript
function applyFlags(
  currentFlags: Readonly<Record<string, boolean>>,
  consequence?: import('../types/narrative.js').ChoiceConsequence
): Readonly<Record<string, boolean>>
```
- **Behavior:**
  1. Start with spread of `currentFlags`.
  2. If `consequence?.setFlags`: set each flag name to `true`.
  3. If `consequence?.clearFlags`: delete each flag name from the result.
  4. Return new object.
- **Pure function.**

#### File: `src/narrative/synergyCalculator.ts` (NEW) [RF-9, design spec]

**Imports:**
```typescript
import type { Personality, PersonalityTrait } from '../types/index.js';
import type { SynergyBonus, SynergyResult, ParadigmConfig } from '../types/narrative.js';
```

**Constant:**
```typescript
const PERSONALITY_TRAITS: PersonalityTrait[] = [
  'patience', 'empathy', 'cunning', 'logic', 'kindness', 'charisma',
];
```

**Function: `evaluateWellRounded`**
```typescript
export function evaluateWellRounded(
  playerPersonality: Readonly<Personality>,
  partyNpcPersonalities: ReadonlyArray<Readonly<Personality>>,
  config: ParadigmConfig
): SynergyBonus | null
```
- **Algorithm (per design spec Section 4):**
  1. Collect all personalities: `[playerPersonality, ...partyNpcPersonalities]`.
  2. For each of 6 traits: find max value across all personalities.
  3. If any max < `config.threshold` (integer, e.g., 25): return null.
  4. `matchQuality = sum(maxValues) / (6 * config.threshold)`.
  5. Return `{ paradigmName: config.name, stat: config.stat, multiplier: config.multiplier, matchQuality }`.
- **Edge cases:** Empty `partyNpcPersonalities` -- only player personality is evaluated. All traits from a single person must meet threshold.
- **Pure function.**

**Function: `evaluateBond`**
```typescript
export function evaluateBond(
  playerPersonality: Readonly<Personality>,
  partyNpcPersonalities: ReadonlyArray<Readonly<Personality>>,
  config: ParadigmConfig
): SynergyBonus | null
```
- **Algorithm (per design spec Section 4):**
  1. For each NPC personality:
     a. Sort the 6 traits by value descending. Top 2 are dominant traits.
     b. `npcDominantSum = npc[trait1] + npc[trait2]`.
     c. **Division-by-zero guard:** If `npcDominantSum === 0`, skip this NPC.
     d. `playerAlignmentSum = player[trait1] + player[trait2]` (using NPC's top-2 trait keys).
     e. `alignmentRatio = playerAlignmentSum / npcDominantSum`.
  2. `bestRatio = max(all alignmentRatios)`. If no NPCs evaluated (all skipped): return null.
  3. If `bestRatio < config.threshold / 100` (Note: threshold is integer 80, compare as 0.80): return null. **IMPORTANT: The design spec uses 0.80 as the comparison value. The threshold in config is stored as integer 80. Convert: `config.threshold / 100`.**
  4. Return `{ paradigmName: config.name, stat: config.stat, multiplier: config.multiplier, matchQuality: bestRatio }`.
- **Edge cases:** Empty partyNpcPersonalities returns null. NPC with all traits equal -- top-2 selected by trait key iteration order (deterministic since `Object.entries` is used on a fixed-shape object).
- **Pure function.**

**CORRECTION on threshold comparison for Well Rounded vs Bond:**
The design spec notes: "Trait values are stored as integers (e.g., 20 = 20%). The threshold of 0.25 means 25. Normalise accordingly." This means:
- **Well Rounded threshold:** Config stores 25 (integer). Compare trait value (integer) directly: `max >= config.threshold`. No division needed.
- **Bond threshold:** Config stores 80 (integer). The alignment ratio is a decimal (0.0-1.0+). Compare: `bestRatio >= config.threshold / 100` (i.e., `>= 0.80`).

**Function: `calculateSynergy`**
```typescript
export function calculateSynergy(
  playerPersonality: Readonly<Personality>,
  partyNpcPersonalities: ReadonlyArray<Readonly<Personality>>,
  paradigms: ReadonlyArray<ParadigmConfig>
): SynergyResult
```
- **Algorithm (per design spec Section 4):**
  1. For each paradigm in `paradigms`:
     - If `paradigm.type === 'well_rounded'`: call `evaluateWellRounded`.
     - If `paradigm.type === 'bond'`: call `evaluateBond`.
     - Collect non-null results.
  2. If no results: return null.
  3. Sort by `matchQuality` descending.
  4. **Tiebreak (equal matchQuality):** `well_rounded` type wins over `bond` type. Implemented by stable sort with secondary key: paradigm type priority map `{ well_rounded: 0, bond: 1 }`.
  5. Return `results[0]`.
- **Pure function, deterministic.**

### 5.3 State Management

#### File: `src/state/stateUpdaters.ts` (EXTENDED) [RF-2, A6]

**Add import at top:**
```typescript
import type { NarrativeState } from '../types/narrative.js';
```

**Function: `initializeNarrative`**
```typescript
export function initializeNarrative(
  state: Readonly<GameState>,
  startingSceneId: string
): GameState
```
- **Behavior:** Creates initial NarrativeState and sets it on GameState.
  ```typescript
  return updateTimestamp({
    ...state,
    narrativeState: {
      currentSceneId: startingSceneId,
      visitedSceneIds: [startingSceneId],
      choiceFlags: {},
      sceneHistory: [],
    },
  });
  ```
- **Immutability:** New GameState with new NarrativeState object. Original state untouched.

**Function: `updateNarrativeState`**
```typescript
export function updateNarrativeState(
  state: Readonly<GameState>,
  narrativeState: NarrativeState
): GameState
```
- **Behavior:** Replaces narrativeState field on GameState.
  ```typescript
  return updateTimestamp({
    ...state,
    narrativeState,
  });
  ```
- **Guard [A6]:** If `state.narrativeState === null` and `narrativeState` is provided, this still works (initializing). The caller is responsible for providing valid NarrativeState.

**Function: `clearNarrative`**
```typescript
export function clearNarrative(
  state: Readonly<GameState>
): GameState
```
- **Behavior:** Sets narrativeState to null.
  ```typescript
  return updateTimestamp({
    ...state,
    narrativeState: null,
  });
  ```

**All updaters follow the canonical pattern:**
- Signature: `(state: Readonly<GameState>, ...) => GameState`
- Wrapped with `updateTimestamp()` as outermost call
- Shallow spread at each nesting level
- No mutations to input state

**GameState spread backward compatibility:** Adding `narrativeState` to the spread pattern is safe because the spread operator copies all enumerable own properties. Existing code that spreads GameState without explicitly listing `narrativeState` will still include it via `...state`. Existing test fixtures that construct partial GameState objects will need `narrativeState: null` added.

### 5.4 Configuration & Data

#### File: `src/fixtures/synergyConfig.ts` (NEW) [RF-9, RF-10, design spec]

```typescript
import type { ParadigmConfig } from '../types/narrative.js';

/**
 * Default synergy paradigm configuration for the POC.
 * Well Rounded: power x1.10 when party covers all traits >= 25%
 * Bond: speed x1.10 when player aligns >= 80% with an NPC's dominant traits
 */
export const DEFAULT_PARADIGMS: readonly ParadigmConfig[] = [
  {
    name: 'Well Rounded',
    type: 'well_rounded',
    threshold: 25,
    stat: 'power',
    multiplier: 1.10,
  },
  {
    name: 'Bond',
    type: 'bond',
    threshold: 80,
    stat: 'speed',
    multiplier: 1.10,
  },
] as const;
```

#### File: `src/fixtures/scenes/` (NEW DIRECTORY) [D3]

Scene JSON files will be created by Task 11 (narrative content authoring, not in this blueprint's scope). The engine reads scene data as `SceneGraph` (array of `Scene` objects). Example structure for a test fixture:

```json
[
  {
    "id": "scene_opening",
    "title": "The Gathering",
    "text": "You stand at the crossroads...",
    "choices": [
      {
        "id": "choice_brave",
        "text": "Step forward boldly",
        "gate": { "trait": "charisma", "operator": "gte", "value": 20 },
        "consequence": {
          "personalityEffect": { "charisma": 2 },
          "npcEffects": [{ "npcId": "npc_scout_elena", "trustChange": 5 }],
          "setFlags": ["brave_opening"]
        },
        "nextSceneId": "scene_two"
      },
      {
        "id": "choice_cautious",
        "text": "Observe from the shadows",
        "consequence": {
          "personalityEffect": { "cunning": 2 },
          "setFlags": ["cautious_opening"]
        },
        "nextSceneId": "scene_two"
      }
    ],
    "prerequisites": []
  }
]
```

#### Test Fixtures

**File: `src/narrative/fixtures.ts` (NEW)**

Provides test scene graph data, mirroring `src/dialogue/fixtures.ts` pattern [RF-8]:

```typescript
import type { Scene, SceneGraph } from '../types/narrative.js';

/** Minimal valid scene graph for testing */
export const TEST_SCENE_GRAPH: SceneGraph = [ /* 3 test scenes */ ];

/** Scene graph with a dead-end (all choices gated) for validation testing */
export const DEAD_END_SCENE_GRAPH: SceneGraph = [ /* scene with all gated choices */ ];

/** Scene graph with prerequisite chains for multi-step traversal testing */
export const PREREQUISITE_SCENE_GRAPH: SceneGraph = [ /* scenes with flag/trait/visited prerequisites */ ];
```

Exact fixture data will be defined during implementation, following the two-tier fixture pattern: shared fixtures file + local factory functions with `Partial<T>` overrides in test files [RF-8].

### 5.5 Integration Points

#### File: `src/combat/sync.ts` (EXTENDED) [D5, RF-4, RF-9]

**Add imports:**
```typescript
import type { Personality } from '../types/index.js';
import type { SynergyResult } from '../types/narrative.js';
import { calculateSynergy } from '../narrative/synergyCalculator.js';
import { DEFAULT_PARADIGMS } from '../fixtures/synergyConfig.js';
```

**Modify `initCombatState` function:**

The existing function signature remains the same. The `_gameState` parameter is renamed to `gameState` (remove underscore prefix) and is now used:

```typescript
export function initCombatState(
  gameState: Readonly<GameState>,
  encounter: Readonly<EncounterConfig>
): CombatState {
  let playerParty: Combatant[] = encounter.playerParty.map(_configToCombatant);
  const enemyParty: Combatant[] = encounter.enemyParty.map(_configToCombatant);

  // --- Synergy bonus application [Sprint 3] ---
  const synergyResult = _calculatePartySynergy(gameState);
  if (synergyResult !== null) {
    playerParty = playerParty.map(combatant => ({
      ...combatant,
      [synergyResult.stat]: Math.round(
        combatant[synergyResult.stat] * synergyResult.multiplier
      ),
    }));
  }

  return {
    round: 1,
    phase: 'AI_DECISION',
    playerParty,
    enemyParty,
    actionQueue: [],
    roundHistory: [],
    status: 'active',
  };
}
```

**Add internal helper:**
```typescript
/**
 * Extracts party personality data from GameState and calculates synergy.
 * Returns null if GameState lacks personality data.
 * All 3 NPCs are treated as party members for Sprint 3 (no party selection mechanic). [R2 mitigation]
 */
function _calculatePartySynergy(gameState: Readonly<GameState>): SynergyResult {
  // Guard: if player or NPCs are missing, skip synergy
  if (!gameState.player?.personality) return null;

  const npcPersonalities: Personality[] = Object.values(gameState.npcs)
    .filter(npc => npc?.personality)
    .map(npc => npc.personality);

  if (npcPersonalities.length === 0) return null;

  return calculateSynergy(
    gameState.player.personality,
    npcPersonalities,
    DEFAULT_PARADIGMS
  );
}
```

**Backward compatibility [R6 mitigation]:** The guard `if (!gameState.player?.personality) return null` ensures existing tests that pass minimal/null-ish gameState objects still work -- synergy is silently skipped, producing the same CombatState as before.

#### File: `src/persistence/saveLoad.ts` (EXTENDED) [RF-5, R4]

**Modify `validateGameState` function -- add after combatState check (before final `return true`):**

```typescript
// narrativeState: NarrativeState | null -- accept presence or absence
// Deep validation of NarrativeState structure when present
if (s.narrativeState !== null && s.narrativeState !== undefined) {
  if (typeof s.narrativeState !== 'object') return false;
  const ns = s.narrativeState as Record<string, unknown>;
  if (typeof ns.currentSceneId !== 'string') return false;
  if (!Array.isArray(ns.visitedSceneIds)) return false;
  if (!ns.choiceFlags || typeof ns.choiceFlags !== 'object' || Array.isArray(ns.choiceFlags)) return false;
  if (!Array.isArray(ns.sceneHistory)) return false;
}
```

**Backward compatibility [R4 mitigation]:** Missing `narrativeState` (undefined or absent in old saves) is acceptable -- the validator does not require its presence. When loading old saves, `narrativeState` will be `undefined` in the parsed object. GameState consumers must handle both `null` and `undefined` for this field, or the load function should normalize undefined to null.

**Add normalization in `loadGame` after validation:**
```typescript
// Normalize missing narrativeState to null for backward compatibility
if ((data as Record<string, unknown>).narrativeState === undefined) {
  (data as Record<string, unknown>).narrativeState = null;
}
```

#### File: `src/api/narrative.ts` (NEW PLUGIN) [RF-6]

**Plugin signature:**
```typescript
export async function narrativePlugin(fastify: FastifyInstance): Promise<void>
```

**Endpoints:**

**1. GET /start/:startingSceneId**
- **Purpose:** Initialize narrative state with the given starting scene.
- **Params:** `startingSceneId: string`
- **Handler:** Calls `initializeNarrative(state, startingSceneId)` from stateUpdaters. Updates container.
- **Response 200:** `ApiResponse<{ narrativeState: NarrativeState }>`
- **Response 404:** `ApiResponse` with `GAME_NOT_FOUND` if no active game.

**2. GET /current**
- **Purpose:** Get current scene with available choices.
- **Handler:** Reads `narrativeState.currentSceneId`, loads scene from scene graph, calls `getCurrentScene()`.
- **Response 200:** `ApiResponse<CurrentSceneResult>`
- **Response 400:** `ApiResponse` with `NARRATIVE_NOT_STARTED` if narrativeState is null.
- **Response 404:** `ApiResponse` with `SCENE_NOT_FOUND` if current scene not in graph.
- **Note:** Scene graph is loaded from fixture JSON at plugin registration time (or lazily on first request). Stored as module-level constant. [A2]

**3. POST /choose**
- **Request body:** `{ choiceId: string }`
- **Purpose:** Process a scene choice and advance narrative.
- **Handler:**
  1. Calls `advanceNarrative(gameState, choiceId, sceneGraph)`.
  2. If error result: maps error code to HTTP status and returns ApiError.
  3. If success: calls `applyConsequence` for GameState-level effects, calls `updateNarrativeState` to set the new NarrativeState, updates container.
- **Response 200:** `ApiResponse<{ narrativeState: NarrativeState; nextScene: Scene | null }>`
- **Response 400:** `ApiResponse` with appropriate error code.

**4. GET /state**
- **Purpose:** Get current narrative state.
- **Response 200:** `ApiResponse<{ narrativeState: NarrativeState | null }>`

**5. POST /reset**
- **Purpose:** Clear narrative state.
- **Handler:** Calls `clearNarrative(state)`. Updates container.
- **Response 200:** `ApiResponse<{ narrativeState: null }>`

**6. GET /synergy**
- **Purpose:** Get current synergy bonus for the party.
- **Handler:** Calls `calculateSynergy()` with player personality + NPC personalities + DEFAULT_PARADIGMS.
- **Response 200:** `ApiResponse<{ synergy: SynergyResult }>`

#### File: `src/api/index.ts` (EXTENDED) [RF-6]

**Add import:**
```typescript
import { narrativePlugin } from './narrative.js';
```

**Add plugin registration (after combatPlugin):**
```typescript
await fastify.register(narrativePlugin, { prefix: '/api/narrative' });
```

**Add error handling for narrative errors in `globalErrorHandler`:**
```typescript
// Narrative-specific error codes
if (domainCode === ErrorCodes.SCENE_NOT_FOUND) {
  const response: ApiResponse<never> = {
    success: false,
    error: { code: ErrorCodes.SCENE_NOT_FOUND, message: error.message },
  };
  await reply.code(404).send(response);
  return;
}

if (domainCode === ErrorCodes.NARRATIVE_NOT_STARTED) {
  const response: ApiResponse<never> = {
    success: false,
    error: { code: ErrorCodes.NARRATIVE_NOT_STARTED, message: error.message },
  };
  await reply.code(400).send(response);
  return;
}

if (domainCode === ErrorCodes.CHOICE_NOT_AVAILABLE) {
  const response: ApiResponse<never> = {
    success: false,
    error: { code: ErrorCodes.CHOICE_NOT_AVAILABLE, message: error.message },
  };
  await reply.code(400).send(response);
  return;
}

if (domainCode === ErrorCodes.CHOICE_NOT_FOUND) {
  const response: ApiResponse<never> = {
    success: false,
    error: { code: ErrorCodes.CHOICE_NOT_FOUND, message: error.message },
  };
  await reply.code(404).send(response);
  return;
}

if (domainCode === ErrorCodes.PREREQUISITE_NOT_MET) {
  const response: ApiResponse<never> = {
    success: false,
    error: { code: ErrorCodes.PREREQUISITE_NOT_MET, message: error.message },
  };
  await reply.code(400).send(response);
  return;
}

if (domainCode === ErrorCodes.INVALID_TRANSITION) {
  const response: ApiResponse<never> = {
    success: false,
    error: { code: ErrorCodes.INVALID_TRANSITION, message: error.message },
  };
  await reply.code(400).send(response);
  return;
}
```

---

## 6. Acceptance Mapping

### Task 1: Narrative Type System

| AC | Satisfied By |
|----|--------------|
| 1. All narrative interfaces defined | `src/types/narrative.ts`: Scene, SceneChoice, ScenePrerequisite, ChoiceConsequence, NarrativeState, SceneHistoryEntry, SynergyBonus, SynergyResult, ParadigmConfig, SynergyStatType, ScenePersonalityGate, NpcEffect, PersonalityEffect, SceneGraph, CurrentSceneResult, NarrativeTransitionSuccess, NarrativeTransitionError, NarrativeTransitionResult, PrerequisiteType, PrerequisiteOperator |
| 2. GameState extended, Sprint 1+2 compiles | `src/types/index.ts`: `narrativeState: NarrativeState \| null` added to GameState. `createNewGameState()` updated with `narrativeState: null`. |
| 3. TypeScript strict mode succeeds | All types use readonly modifiers, no `any`, proper nullability. Leaf file constraint prevents circular imports. |
| 4. No circular imports | `types/narrative.ts` has zero imports from `types/index.ts`. One-way: `index.ts` imports from `narrative.ts`. |

### Task 2: Scene Graph Engine

| AC | Satisfied By |
|----|--------------|
| 1. Scene graph loadable, traversable by ID | `findScene()`, `getCurrentScene()` in `sceneEngine.ts` |
| 2. Prerequisite evaluation | `evaluatePrerequisite()`, `evaluateAllPrerequisites()` in `sceneEngine.ts` |
| 3. Choices filtered by gates, ungated fallback | `getAvailableChoices()` in `sceneEngine.ts` |
| 4. Dead-end detection | `validateSceneGraph()` in `sceneEngine.ts` |
| 5. Unit tests | `sceneEngine.test.ts` with `TEST_SCENE_GRAPH`, `DEAD_END_SCENE_GRAPH`, `PREREQUISITE_SCENE_GRAPH` fixtures |

### Task 3: Choice & Consequence Engine

| AC | Satisfied By |
|----|--------------|
| 1. Choice validates against state | `validateChoice()` in `choiceEngine.ts` |
| 2. Effects use existing updaters | `applyConsequence()` calls `applyPersonalityAdjustment`, `updateNPCRelationship` from `stateUpdaters.ts` |
| 3. Named flags set in choice history | `applyFlags()` in `narrativeStateMachine.ts` (flag mutation is NarrativeState-level, orchestrated by state machine) |
| 4. Flag map is string->boolean, JSON-serializable | `NarrativeState.choiceFlags: Readonly<Record<string, boolean>>` |
| 5. Unit tests | `choiceEngine.test.ts` covering valid/gated/flag/personality scenarios |

### Task 4: Narrative State Machine

| AC | Satisfied By |
|----|--------------|
| 1. Tracks current scene, visited, flags | `advanceNarrative()` returns updated `NarrativeState` with all fields |
| 2. Validates prerequisites before advancing | Step 5 in `advanceNarrative()` checks `evaluateAllPrerequisites` |
| 3. Invalid transition returns typed error | `NarrativeTransitionError` with code/message [D4] |
| 4. Pure functions | All functions return new objects, no mutations |
| 5. Unit tests | `narrativeStateMachine.test.ts` with multi-step traversal, invalid transitions |

### Task 5: Narrative State Updaters

| AC | Satisfied By |
|----|--------------|
| 1. Returns new GameState without mutation | `initializeNarrative`, `updateNarrativeState`, `clearNarrative` use spread + `updateTimestamp` |
| 2. Integrates with existing updaters | Choice processing chains personality updaters via `applyConsequence` -> existing `applyPersonalityAdjustment` |
| 3. Starting narrative initializes NarrativeState | `initializeNarrative(state, startingSceneId)` |
| 4. Tests verify reference inequality | `stateUpdaters.test.ts` extended with `expect(updated).not.toBe(baseState)` checks |

### Task 6: Persistence Integration

| AC | Satisfied By |
|----|--------------|
| 1. Save/load round-trip | `validateGameState()` accepts NarrativeState structure |
| 2. Loading pre-narrative save doesn't crash | Missing `narrativeState` accepted (null/undefined), normalized to null |
| 3. Flag map serializes | `Record<string, boolean>` is JSON-safe |
| 4. Visited scenes serialize | `string[]` is JSON-safe [A3] |
| 5. Unit tests | `saveLoad.test.ts` extended with narrative state and backward compat tests |

### Task 7: Team Synergy Calculator

| AC | Satisfied By |
|----|--------------|
| 1. calculateSynergy signature | `calculateSynergy(playerPersonality, partyNpcPersonalities, paradigms): SynergyResult` |
| 2. Well Rounded logic | `evaluateWellRounded()` -- max per trait >= threshold, matchQuality = sum/divisor |
| 3. Bond logic | `evaluateBond()` -- top-2 alignment ratio >= threshold |
| 4. Highest-only with tiebreak | `calculateSynergy()` sorts by matchQuality, well_rounded wins ties |
| 5. Division-by-zero guard | `evaluateBond()` skips NPC when npcDominantSum === 0 |
| 6. Default config | `src/fixtures/synergyConfig.ts` with DEFAULT_PARADIGMS |
| 7. TDD tests | `synergyCalculator.test.ts` with all design spec test scenarios |

### Task 8: Combat Synergy Integration

| AC | Satisfied By |
|----|--------------|
| 1. initCombatState calls calculateSynergy | `_calculatePartySynergy()` helper called in `initCombatState` |
| 2. Correct stat modified with Math.round | `Math.round(combatant[synergyResult.stat] * synergyResult.multiplier)` |
| 3. No synergy = no regression | Guard returns null when personality data missing |
| 4. Unit tests | `sync.test.ts` extended with synergy/no-synergy scenarios |

### Task 9: Narrative REST API

| AC | Satisfied By |
|----|--------------|
| 1. Correct status codes and ApiResponse | All 6 endpoints return `ApiResponse<T>` with appropriate HTTP codes |
| 2. Get current scene with filtered choices | `GET /current` returns `CurrentSceneResult` |
| 3. Choice submission validates and processes | `POST /choose` calls `advanceNarrative` + `applyConsequence` |
| 4. Invalid requests return ApiError | Error codes mapped in handler and `globalErrorHandler` |
| 5. Plugin registers alongside existing | `narrativePlugin` registered in `buildApp()` |

### Task 12: Integration Validation

| AC | Satisfied By |
|----|--------------|
| 1. Complete narrative flow via API | Integration test: start -> choose -> flag-gate -> advance |
| 2. Synergy in combat init | Integration test: calculateSynergy -> initCombatState -> verify stats |
| 3. Save/load mid-narrative | Integration test: start narrative -> save -> load -> verify state |
| 4. Backward compatibility | Integration test: Sprint 1+2 operations unchanged |
| 5. No unhandled rejections | All error paths return structured responses |

---

## 7. Integration Points

### Type Definitions
- `src/types/narrative.ts` (NEW) -- exports: Scene, SceneGraph, SceneChoice, ScenePrerequisite, ScenePersonalityGate, PrerequisiteType, PrerequisiteOperator, ChoiceConsequence, NpcEffect, PersonalityEffect, NarrativeState, SceneHistoryEntry, SynergyBonus, SynergyResult, SynergyStatType, ParadigmConfig, NarrativeTransitionResult, NarrativeTransitionSuccess, NarrativeTransitionError, CurrentSceneResult
- `src/types/index.ts` (MODIFIED) -- adds: `narrativeState: NarrativeState | null` to GameState, 6 new ErrorCodes entries, re-exports NarrativeState

### Engine Modules
- `src/narrative/sceneEngine.ts` (NEW) -- exports: evaluateScenePersonalityGate, evaluatePrerequisite, evaluateAllPrerequisites, getAvailableChoices, findScene, getCurrentScene, validateSceneGraph, getAccessibleScenes
- `src/narrative/choiceEngine.ts` (NEW) -- exports: validateChoice, applyConsequence, processSceneChoice
- `src/narrative/narrativeStateMachine.ts` (NEW) -- exports: createInitialNarrativeState, advanceNarrative
- `src/narrative/synergyCalculator.ts` (NEW) -- exports: evaluateWellRounded, evaluateBond, calculateSynergy

### State Updaters
- `src/state/stateUpdaters.ts` (MODIFIED) -- adds: initializeNarrative, updateNarrativeState, clearNarrative
- `src/state/gameState.ts` (MODIFIED) -- adds: `narrativeState: null` to createNewGameState

### API Plugins
- `src/api/narrative.ts` (NEW) -- exports: narrativePlugin (6 routes: GET /start/:startingSceneId, GET /current, POST /choose, GET /state, POST /reset, GET /synergy)
- `src/api/index.ts` (MODIFIED) -- imports and registers narrativePlugin, extends globalErrorHandler

### Configuration
- `src/fixtures/synergyConfig.ts` (NEW) -- exports: DEFAULT_PARADIGMS
- `src/fixtures/scenes/` (NEW DIRECTORY) -- Scene JSON files (created by Task 11)

### Persistence
- `src/persistence/saveLoad.ts` (MODIFIED) -- validateGameState extended for NarrativeState, loadGame adds normalization

### Combat Integration
- `src/combat/sync.ts` (MODIFIED) -- initCombatState uses gameState parameter, adds _calculatePartySynergy helper

### Test Files (NEW)
- `src/narrative/sceneEngine.test.ts`
- `src/narrative/choiceEngine.test.ts`
- `src/narrative/narrativeStateMachine.test.ts`
- `src/narrative/synergyCalculator.test.ts`
- `src/narrative/fixtures.ts` (shared test fixtures)

### Test Files (MODIFIED)
- `src/state/stateUpdaters.test.ts` -- add narrative updater tests
- `src/persistence/saveLoad.test.ts` -- add narrative persistence tests
- `src/combat/sync.test.ts` -- add synergy integration tests

### Integration Test File (NEW)
- `src/narrative/integration.test.ts` -- end-to-end flow tests (T12)

---

## 8. Open Items

- [VERIFY] Confirm `createNewGameState()` spread in `gameState.ts` compiles after adding `narrativeState: null` -- run `npx tsc --noEmit` immediately after T1 type changes.
- [VERIFY] Confirm existing Sprint 1+2 test suite (793 tests) passes green after GameState extension -- run `npx vitest run` after T1 changes.
- [VERIFY] Confirm `sync.test.ts` existing tests still pass after `initCombatState` modification -- the `_gameState` parameter was unused, so existing test mock data (which may pass a minimal GameState) must still work with the synergy guard.
- [VERIFY] Confirm `encounter.json` import pattern (JSON import with assertion) works alongside new TypeScript config imports in `synergyConfig.ts`.
- [VERIFY] Confirm the `Personality` type's trait keys match the string keys used in `ScenePersonalityGate.trait` -- the gate uses `string` type to avoid importing from index.ts, so runtime access via `personality[gate.trait as keyof Personality]` must be validated.

No unresolved design questions remain. All architectural decisions are grounded in user decisions (D1-D6), accepted assumptions (A1-A7), research findings (RF-1 through RF-10), or the approved synergy design spec.

---

## 9. Producer Handoff

**Output format:** TypeScript source files (.ts)
**Producer:** code-writer
**Instruction tone:** Implement exact type definitions and function signatures as specified. All state updaters must return new objects without mutating inputs. Follow existing codebase conventions for imports (.js extensions), readonly modifiers, and updateTimestamp wrapping. Reference the existing dialogue engine and state updaters as pattern templates. Every pure function must be deterministic with no side effects.

### File Creation Order (dependency-respecting)

| Order | File | Task | Target Lines | Description |
|-------|------|------|-------------|-------------|
| 1 | `src/types/narrative.ts` | T1 | 150-180 | All narrative and synergy type definitions |
| 2 | `src/types/index.ts` | T1 | +15 lines (modification) | Import NarrativeState, extend GameState, add ErrorCodes |
| 3 | `src/state/gameState.ts` | T1 | +1 line (modification) | Add `narrativeState: null` |
| 4 | `src/narrative/fixtures.ts` | T2 | 80-120 | Shared test fixture data for scene graphs |
| 5 | `src/narrative/sceneEngine.ts` | T2 | 120-160 | Scene graph traversal, prerequisite evaluation, dead-end validation |
| 6 | `src/narrative/sceneEngine.test.ts` | T2 | 150-200 | Unit tests for all sceneEngine functions |
| 7 | `src/narrative/choiceEngine.ts` | T3 | 80-110 | Choice validation, consequence application |
| 8 | `src/narrative/choiceEngine.test.ts` | T3 | 120-160 | Unit tests for choice engine |
| 9 | `src/narrative/narrativeStateMachine.ts` | T4 | 100-140 | Thin orchestrator, transitions, flag management |
| 10 | `src/narrative/narrativeStateMachine.test.ts` | T4 | 150-200 | Multi-step traversal, invalid transitions |
| 11 | `src/state/stateUpdaters.ts` | T5 | +40 lines (modification) | initializeNarrative, updateNarrativeState, clearNarrative |
| 12 | `src/state/stateUpdaters.test.ts` | T5 | +60 lines (modification) | Narrative updater tests |
| 13 | `src/persistence/saveLoad.ts` | T6 | +20 lines (modification) | Validate NarrativeState, normalize on load |
| 14 | `src/persistence/saveLoad.test.ts` | T6 | +40 lines (modification) | Round-trip and backward compat tests |
| 15 | `src/fixtures/synergyConfig.ts` | T7 | 20-30 | DEFAULT_PARADIGMS configuration |
| 16 | `src/narrative/synergyCalculator.ts` | T7 | 100-130 | Pure synergy calculation functions |
| 17 | `src/narrative/synergyCalculator.test.ts` | T7 | 150-200 | TDD test scenarios from design spec |
| 18 | `src/combat/sync.ts` | T8 | +30 lines (modification) | Synergy integration in initCombatState |
| 19 | `src/combat/sync.test.ts` | T8 | +50 lines (modification) | Synergy/no-synergy combat init tests |
| 20 | `src/api/narrative.ts` | T9 | 150-200 | Narrative REST plugin with 6 endpoints |
| 21 | `src/api/index.ts` | T9 | +20 lines (modification) | Register narrative plugin, extend error handler |
| 22 | `src/narrative/integration.test.ts` | T12 | 150-250 | End-to-end integration tests |

**Total new files:** 12
**Total modified files:** 8
**Estimated new code:** ~1,200-1,700 lines (source) + ~900-1,300 lines (tests)

### Content Summary Per File

**Files 1-3 (T1 Foundation):** Type definitions and GameState extension. Must compile cleanly. Run full test suite after.

**Files 4-6 (T2 Scene Engine):** Fixtures first, then engine, then tests. Engine mirrors dialogueEngine pattern. Tests use fixture scene graphs.

**Files 7-8 (T3 Choice Engine):** Depends on sceneEngine for gate evaluation. Reuses stateUpdaters for effects. Tests mock GameState.

**Files 9-10 (T4 State Machine):** Integrates sceneEngine + choiceEngine. Thin orchestrator. Returns discriminated union. Tests do multi-step traversal.

**Files 11-14 (T5-T6 State/Persistence):** Extensions to existing files. Minimal new code. Focus on backward compatibility.

**Files 15-17 (T7 Synergy):** Config file, pure calculator, TDD tests. Independent from narrative chain. Tests use known NPC personalities from npcs.ts.

**Files 18-19 (T8 Combat Integration):** Modify initCombatState, add helper, extend tests. Must not regress existing combat tests.

**Files 20-21 (T9 API):** New plugin + registration. Follow dialoguePlugin as template. JSON Schema validation on all routes.

**File 22 (T12 Integration):** End-to-end tests spanning narrative -> combat -> persistence. Tests the full story: start narrative -> traverse scenes -> make choices -> see flag propagation -> compute synergy -> init combat with bonuses -> save/load mid-narrative.
