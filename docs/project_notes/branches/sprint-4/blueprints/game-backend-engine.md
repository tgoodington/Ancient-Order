# Game Backend Engine -- Design Blueprint

Specialist: game-backend-engine
Date: 2026-03-07
Status: APPROVED -- Ready for Producer Handoff

---

## 1. Task Reference

### T1: Wire saves management endpoints
- **Depth**: Light
- **Dependencies**: None (existing `listSaves` / `deleteSave` functions in persistence layer)
- **Acceptance Criteria**:
  1. GET `/api/game/saves` returns `ApiResponse<SaveSlotInfo[]>` with list of saved game slots and their metadata
  2. DELETE `/api/game/saves/:slot` returns `ApiResponse<T>` confirming deletion, or error for invalid/nonexistent slot
  3. Both endpoints use consistent error codes from `ErrorCodes`
  4. Existing save/load endpoints remain unchanged

### T2: Wire player and NPC state endpoints
- **Depth**: Standard
- **Dependencies**: T1 (backward compat in loadGame)
- **Acceptance Criteria**:
  1. GET `/api/player/personality` returns `ApiResponse<Personality>` with current personality trait values
  2. POST `/api/player/team` accepts NPC selection and returns updated team composition
  3. Team composition respects party size constraint (player + 2 NPCs from the 3 available)
  4. GET `/api/npc/:id` response includes affection and trust relationship data alongside base NPC info
  5. New state updater follows `(state: Readonly<GameState>, ...args) => GameState` pattern

### T3: Deepen state validation
- **Depth**: Light
- **Dependencies**: T2 (team field must exist on GameState before validation can reference it)
- **Acceptance Criteria**:
  1. Personality validation checks: 6 traits present, each in 5-35% range, sum equals 100%
  2. Combat state validation checks required shape when `combatState` is not null
  3. Narrative state validation checks scene graph references and flag consistency when `narrativeState` is not null
  4. Rejection returns typed error with specific validation failure details (which field, what was wrong)
  5. Existing Sprint 3 narrative validation logic preserved, not overwritten

---

## 2. Research Findings

### Confirmed File Paths

**Type Files:**
- `src/types/index.ts` -- GameState, Personality, NPC, PlayerCharacter, ApiResponse, ErrorCodes, ConversationEntry
- `src/types/combat.ts` -- CombatState, CombatPhase, Combatant, CombatAction, RoundResult, ActionType
- `src/types/narrative.ts` -- NarrativeState, SceneHistoryEntry, Scene, SceneChoice, ChoiceConsequence, SynergyBonus

**Engine Modules:**
- `src/state/stateUpdaters.ts` -- all immutable state updater functions
- `src/state/gameState.ts` -- `createNewGameState()` factory
- `src/state/npcs.ts` -- `NPC_TEMPLATES`, `getNPC()`, `getAllNPCs()`
- `src/personality/personalitySystem.ts` -- `adjustPersonality()`, `createDefaultPersonality()`

**API Plugins:**
- `src/api/index.ts` -- app factory, `buildApp()`, `GameStateContainer`, `globalErrorHandler`, plugin registration
- `src/api/game.ts` -- `gamePlugin` (POST /new, GET /state, POST /save/:slot, POST /load/:slot)
- `src/api/player.ts` -- `playerPlugin` (GET /, POST /personality)
- `src/api/npc.ts` -- `npcPlugin` (GET /, GET /:id)
- `src/api/dialogue.ts` -- `dialoguePlugin`
- `src/api/combat.ts` -- `combatPlugin`
- `src/api/narrative.ts` -- `narrativePlugin`

**Persistence:**
- `src/persistence/saveLoad.ts` -- `saveGame()`, `loadGame()`, `listSaves()`, `deleteSave()`, `validateGameState()`, `SaveMetadata`, `SaveSlotInfo`

### Confirmed Patterns

**GameState shape** (from `src/types/index.ts` lines 95-104):
```typescript
export interface GameState {
  readonly player: PlayerCharacter;
  readonly npcs: Record<string, NPC>;
  readonly currentDialogueNode: string | null;
  readonly saveSlot: number | null;
  readonly combatState: CombatState | null;
  readonly narrativeState: NarrativeState | null;
  readonly conversationLog: ConversationEntry[];
  readonly timestamp: number;
}
```

**State updater signature pattern** (from `src/state/stateUpdaters.ts` line 8):
```
(state: Readonly<GameState>, ...args) => GameState
```
All updaters wrap their return with `updateTimestamp()`.

**Fastify plugin pattern** (from `src/api/game.ts`):
```typescript
export async function pluginName(fastify: FastifyInstance): Promise<void> { ... }
```
Registered via `fastify.register(plugin, { prefix: '/api/prefix' })` in `src/api/index.ts`.

**Session state access**: `fastify.gameStateContainer.state` (shared mutable container across all plugins).

**Error throwing pattern** (from `src/persistence/saveLoad.ts`):
```typescript
const err = new Error(message);
(err as NodeJS.ErrnoException & { code: string }).code = ErrorCodes.CODE_NAME;
throw err;
```

**Error handler mapping** (from `src/api/index.ts`): Checks `error.code` against `ErrorCodes` constants, maps to HTTP status codes (400/404/500).

**Existing ErrorCodes** confirmed at `src/types/index.ts` lines 188-206:
- `TEAM_COMPOSITION_INVALID` already defined (line 197)
- `VALIDATION_ERROR` already defined (line 198)

**NPC IDs** confirmed at `src/state/npcs.ts`:
- `npc_scout_elena`
- `npc_merchant_lars`
- `npc_outlaw_kade`

**CombatState shape** confirmed at `src/types/combat.ts` lines 189-197:
```typescript
export interface CombatState {
  readonly round: number;
  readonly phase: CombatPhase;
  readonly playerParty: readonly Combatant[];
  readonly enemyParty: readonly Combatant[];
  readonly actionQueue: readonly CombatAction[];
  readonly roundHistory: readonly RoundResult[];
  readonly status: 'active' | 'victory' | 'defeat';
}
```

**CombatPhase values**: `'AI_DECISION' | 'VISUAL_INFO' | 'PC_DECLARATION' | 'ACTION_RESOLUTION' | 'PER_ATTACK'`

**NarrativeState shape** confirmed at `src/types/narrative.ts` lines 157-162:
```typescript
export interface NarrativeState {
  readonly currentSceneId: string;
  readonly visitedSceneIds: readonly string[];
  readonly choiceFlags: Readonly<Record<string, boolean>>;
  readonly sceneHistory: readonly SceneHistoryEntry[];
}
```

**SceneHistoryEntry** at `src/types/narrative.ts` lines 167-171:
```typescript
export interface SceneHistoryEntry {
  readonly sceneId: string;
  readonly choiceId: string;
  readonly timestamp: number;
}
```

**validateGameState current return type**: `data is GameState` (boolean type predicate, lines 85-141 of `src/persistence/saveLoad.ts`).

**Call sites for validateGameState**:
1. `loadGame()` at line 207: `if (!validateGameState(data))` -- throws on false
2. `listSaves()` at line 240: `if (validateGameState(data))` -- skips slot on false

**loadGame backward compat**: Already normalizes missing `narrativeState` to null (lines 214-216). Same pattern needed for `team`.

---

## 3. Approach

### Type Hierarchy Strategy
Add `readonly team: readonly string[]` directly to `GameState` in `src/types/index.ts`. Add `ValidationResult` type alias in `src/persistence/saveLoad.ts` as a local type (not exported from the main types barrel -- it is persistence-internal). No new type files are created.

### Engine Architecture
- **Team updater**: New `updateTeamComposition()` in `src/state/stateUpdaters.ts` following the existing `(state: Readonly<GameState>, ...args) => GameState` pattern with `updateTimestamp()` wrapper.
- **Team validation**: Performed inside the updater -- exactly 2 valid NPC IDs from the 3 available. Throws `TEAM_COMPOSITION_INVALID` on invalid input.
- **Lock check**: POST `/api/player/team` checks `combatState !== null || narrativeState !== null` at the API layer and returns 400 if locked.

### State Management Approach
- All state updates remain immutable (spread + new reference).
- `team` field initialized as `[]` in `createNewGameState()`.
- `loadGame()` normalizes missing `team` to `[]` for backward compatibility.
- `validateGameState()` returns `ValidationResult` instead of boolean type predicate.

### API Integration Pattern
- T1 routes added to existing `gamePlugin` in `src/api/game.ts`.
- T2 routes added to existing `playerPlugin` in `src/api/player.ts` and modified in `npcPlugin` in `src/api/npc.ts`.
- `TEAM_COMPOSITION_INVALID` error handler mapping added to `globalErrorHandler` in `src/api/index.ts`.

---

## 4. Decisions Made

### D1: Team composition rules
- **Options**: (a) Changeable anytime, (b) Locked during combat/narrative, (c) Locked after first combat
- **Chosen**: (b) Locked during combat/narrative
- **User Input**: "POST /api/player/team returns error if combatState !== null or narrativeState !== null."
- **Rationale**: Prevents mid-scene party swaps. Future-proofed for when combat/narrative scenes exist. Simple null-check at API layer.

### D2: validateGameState return type change
- **Options**: (a) Keep boolean + separate `getValidationErrors()`, (b) Change to `ValidationResult` object, (c) Keep boolean + throw on first failure
- **Chosen**: (b) `ValidationResult` -- `{ valid: true } | { valid: false; errors: string[] }`
- **Rationale**: Accumulates all errors for debugging. Simple discriminated union. Call sites update trivially.

### D3: NPC endpoint data source
- **Options**: (a) Always return template, (b) Return live state when game active / template otherwise, (c) Merge template + overlay
- **Chosen**: (b) Return live GameState NPC when game active, template otherwise
- **Rationale**: NPC type is identical for both sources. No merging needed. Consumers get live affection/trust values.

### D4: Error code for corrupt save validation failure
- **Options**: (a) Continue using `SAVE_NOT_FOUND`, (b) Add new `VALIDATION_FAILED` code, (c) Reuse existing `VALIDATION_ERROR`
- **Chosen**: (c) Reuse `VALIDATION_ERROR` with descriptive message from validation result
- **Rationale**: `VALIDATION_ERROR` already exists and maps to 400 in the global error handler. No new error code needed.

### D5: Where to add team field to GameState
- **Options**: (a) Add to GameState in `src/types/index.ts`, (b) Create separate team type file
- **Chosen**: (a) Add `readonly team: readonly string[]` directly to GameState
- **Rationale**: Simple string array does not warrant a separate type file. Consistent with other top-level fields.

### D6: Combat state validation depth
- **Options**: (a) Full recursive validation, (b) Top-level shape only, (c) Top-level + first-level Combatant fields
- **Chosen**: (b) Top-level shape only -- check round, phase, status enums, and that arrays exist
- **Rationale**: Combat state is transient and created by the engine. Deep corruption of engine-created state is unlikely. Top-level checks catch serialization issues.

### A1: Team stored as NPC ID array on GameState -- accepted
### A2: Saves list returns slot metadata only -- accepted
### A3: NPC endpoint returns live GameState data when game active -- accepted
### A4: New routes added to existing plugins -- accepted
### A5: Change validateGameState return to result object -- accepted
### A6: Floating-point tolerance for personality sum -- accepted (0.01 tolerance)

---

## 5. Deliverable Specification

### 5.1 Type Definitions

#### 5.1.1 GameState Extension
**File**: `src/types/index.ts`
**Change**: Add `team` field to `GameState` interface

```typescript
export interface GameState {
  readonly player: PlayerCharacter;
  readonly npcs: Record<string, NPC>;
  readonly team: readonly string[];  // NEW: NPC IDs of active party members (0 or 2)
  readonly currentDialogueNode: string | null;
  readonly saveSlot: number | null;
  readonly combatState: CombatState | null;
  readonly narrativeState: NarrativeState | null;
  readonly conversationLog: ConversationEntry[];
  readonly timestamp: number;
}
```

- **team**: `readonly string[]` -- array of NPC ID strings. Empty `[]` when no team selected; exactly 2 entries when team is set. IDs must be valid keys in `GameState.npcs`.
- Placed after `npcs` for logical grouping (NPC data, then NPC selection).

#### 5.1.2 ValidationResult Type
**File**: `src/persistence/saveLoad.ts`
**Change**: Add local type above `validateGameState`

```typescript
export type ValidationResult =
  | { valid: true }
  | { valid: false; errors: string[] };
```

- Exported for test access.
- Discriminated union on `valid` boolean.
- `errors` array contains human-readable strings describing each validation failure (e.g., `"player.personality.patience: value 3 is below minimum 5"`, `"player.personality: sum 98.5 does not equal 100 (tolerance 0.01)"`).

### 5.2 Engine Logic

#### 5.2.1 updateTeamComposition
**File**: `src/state/stateUpdaters.ts`
**Signature**:
```typescript
export function updateTeamComposition(
  state: Readonly<GameState>,
  npcIds: readonly string[]
): GameState
```

**Behavior**:
1. Validate `npcIds` has exactly 2 elements. If not, throw error with code `TEAM_COMPOSITION_INVALID` and message `"Team must contain exactly 2 NPCs"`.
2. Validate each ID in `npcIds` exists as a key in `state.npcs`. If any ID is not found, throw error with code `TEAM_COMPOSITION_INVALID` and message `"Invalid NPC ID: <id>"`.
3. Validate no duplicate IDs. If duplicates found, throw error with code `TEAM_COMPOSITION_INVALID` and message `"Duplicate NPC ID: <id>"`.
4. Return `updateTimestamp({ ...state, team: npcIds })`.

**Error throwing pattern** (matches codebase convention from `src/persistence/saveLoad.ts`):
```typescript
const err = new Error(message);
(err as NodeJS.ErrnoException & { code: string }).code = ErrorCodes.TEAM_COMPOSITION_INVALID;
throw err;
```

**Edge cases**:
- Empty array: rejected (not exactly 2)
- Array with 1 element: rejected (not exactly 2)
- Array with 3 elements: rejected (not exactly 2)
- Array with unknown NPC ID: rejected with specific ID in message
- Array with same ID twice: rejected as duplicate

**Pure function contract**: No mutations. Returns new state or throws. Deterministic for same inputs.

#### 5.2.2 validateGameState (deepened)
**File**: `src/persistence/saveLoad.ts`
**New Signature**:
```typescript
export function validateGameState(data: unknown): ValidationResult
```

**Behavior -- accumulates errors into `string[]`, returns all at once**:

**Phase 1: Structural checks (existing, preserved)**
1. Check `data` is non-null object. Error: `"GameState must be a non-null object"`
2. Check `timestamp` is number. Error: `"timestamp: expected number, got <typeof>"`
3. Check `currentDialogueNode` is null or string. Error: `"currentDialogueNode: expected string or null"`
4. Check `saveSlot` is null or number. Error: `"saveSlot: expected number or null"`
5. Check `conversationLog` is array. Error: `"conversationLog: expected array"`
6. Check `player` is non-null object. Error: `"player: expected object"`
7. Check `player.id` is string. Error: `"player.id: expected string"`
8. Check `player.name` is string. Error: `"player.name: expected string"`
9. Check `player.personality` is non-null object. Error: `"player.personality: expected object"`

**Phase 2: Personality deep validation (NEW) [AC T3.1]**
10. For each of the 6 traits (`patience`, `empathy`, `cunning`, `logic`, `kindness`, `charisma`):
    - Check trait is number. Error: `"player.personality.<trait>: expected number, got <typeof>"`
    - Check trait >= 5. Error: `"player.personality.<trait>: value <val> is below minimum 5"`
    - Check trait <= 35. Error: `"player.personality.<trait>: value <val> is above maximum 35"`
11. If all 6 traits are valid numbers, check sum equals 100 with tolerance 0.01:
    - `Math.abs(sum - 100) > 0.01` => Error: `"player.personality: sum <sum> does not equal 100 (tolerance 0.01)"`

**Phase 3: NPC validation (existing + enhanced)**
12. Check `npcs` is non-null, non-array object. Error: `"npcs: expected object"`
13. For each NPC entry:
    - Check `id` is string. Error: `"npcs.<key>.id: expected string"`
    - Check `archetype` is string. Error: `"npcs.<key>.archetype: expected string"`
    - Check `affection` is number. Error: `"npcs.<key>.affection: expected number"`
    - Check `trust` is number. Error: `"npcs.<key>.trust: expected number"`
    - Check `personality` is non-null object with 6 number traits (same as player personality checks but scoped to `npcs.<key>.personality.<trait>`)

**Phase 4: Team validation (NEW)**
14. If `team` field is present and not undefined:
    - Check `team` is array. Error: `"team: expected array"`
    - If array, check length is 0 or 2. Error: `"team: must contain exactly 0 or 2 NPC IDs, got <length>"`
    - If length 2, check each element is a string. Error: `"team[<i>]: expected string"`
15. If `team` is undefined, this is acceptable (backward compat -- will be normalized to `[]` by `loadGame`).

**Phase 5: Combat state validation (NEW) [AC T3.2] -- top-level shape only [D6]**
16. If `combatState` is not null and not undefined:
    - Check `combatState` is object. Error: `"combatState: expected object or null"`
    - Check `round` is number. Error: `"combatState.round: expected number"`
    - Check `phase` is one of the 5 valid CombatPhase values. Error: `"combatState.phase: invalid value '<val>', expected one of AI_DECISION, VISUAL_INFO, PC_DECLARATION, ACTION_RESOLUTION, PER_ATTACK"`
    - Check `status` is one of `'active' | 'victory' | 'defeat'`. Error: `"combatState.status: invalid value '<val>', expected one of active, victory, defeat"`
    - Check `playerParty` is array. Error: `"combatState.playerParty: expected array"`
    - Check `enemyParty` is array. Error: `"combatState.enemyParty: expected array"`
    - Check `actionQueue` is array. Error: `"combatState.actionQueue: expected array"`
    - Check `roundHistory` is array. Error: `"combatState.roundHistory: expected array"`

**Phase 6: Narrative state validation (preserved + deepened) [AC T3.3, T3.5]**
17. If `narrativeState` is not null and not undefined:
    - Check `narrativeState` is object. Error: `"narrativeState: expected object or null"`
    - Check `currentSceneId` is string. Error: `"narrativeState.currentSceneId: expected string"`
    - Check `visitedSceneIds` is array. Error: `"narrativeState.visitedSceneIds: expected array"`
    - If array, check each element is string. Error: `"narrativeState.visitedSceneIds[<i>]: expected string"`
    - Check `choiceFlags` is non-null, non-array object. Error: `"narrativeState.choiceFlags: expected object"`
    - If object, check each value is boolean. Error: `"narrativeState.choiceFlags.<key>: expected boolean, got <typeof>"`
    - Check `sceneHistory` is array. Error: `"narrativeState.sceneHistory: expected array"`
    - If array, for each entry:
      - Check `sceneId` is string. Error: `"narrativeState.sceneHistory[<i>].sceneId: expected string"`
      - Check `choiceId` is string. Error: `"narrativeState.sceneHistory[<i>].choiceId: expected string"`
      - Check `timestamp` is number. Error: `"narrativeState.sceneHistory[<i>].timestamp: expected number"`

**Return**: If `errors.length === 0`, return `{ valid: true }`. Otherwise return `{ valid: false, errors }`.

**Important**: The existing Sprint 3 narrative validation (lines 131-138 of current `saveLoad.ts`) is **preserved and deepened** -- the same checks exist plus new deep field type checks. No existing check is removed.

### 5.3 State Management

#### 5.3.1 createNewGameState Update
**File**: `src/state/gameState.ts`
**Change**: Add `team: []` to the returned object.

```typescript
export function createNewGameState(): GameState {
  return {
    player: { ... },
    npcs: { ...NPC_TEMPLATES },
    team: [],  // NEW
    currentDialogueNode: null,
    saveSlot: null,
    combatState: null,
    narrativeState: null,
    conversationLog: [],
    timestamp: Date.now(),
  };
}
```

#### 5.3.2 loadGame Backward Compatibility
**File**: `src/persistence/saveLoad.ts`
**Change**: After validation passes in `loadGame()`, normalize missing `team` to `[]`.

Add after the existing `narrativeState` normalization (line 216):
```typescript
if ((data as Record<string, unknown>).team === undefined) {
  (data as Record<string, unknown>).team = [];
}
```

#### 5.3.3 loadGame Validation Update
**File**: `src/persistence/saveLoad.ts`
**Change**: Update `loadGame()` to use new `ValidationResult` return type.

Current code (line 207):
```typescript
if (!validateGameState(data)) {
  const err = new Error(`Save file in slot ${slot} is corrupted or has an invalid format.`);
  (err as NodeJS.ErrnoException & { code: string }).code = ErrorCodes.SAVE_NOT_FOUND;
  throw err;
}
```

New code:
```typescript
const validation = validateGameState(data);
if (!validation.valid) {
  const err = new Error(`Save file in slot ${slot} is corrupted: ${validation.errors.join('; ')}`);
  (err as NodeJS.ErrnoException & { code: string }).code = ErrorCodes.VALIDATION_ERROR;
  throw err;
}
```

Note: Error code changes from `SAVE_NOT_FOUND` to `VALIDATION_ERROR` per decision D4.

#### 5.3.4 listSaves Validation Update
**File**: `src/persistence/saveLoad.ts`
**Change**: Update `listSaves()` to use new `ValidationResult` return type.

Current code (line 240):
```typescript
if (validateGameState(data)) {
```

New code:
```typescript
const validation = validateGameState(data);
if (validation.valid) {
```

#### 5.3.5 State Invariants
After every `updateTeamComposition` call:
- `state.team.length === 0 || state.team.length === 2`
- Each ID in `state.team` exists as a key in `state.npcs`
- No duplicate IDs in `state.team`

After every `validateGameState` call on valid data:
- All 6 personality traits are numbers in [5, 35]
- Personality sum is within 0.01 of 100
- If `combatState` is non-null, it has valid top-level shape
- If `narrativeState` is non-null, it has valid deep shape

### 5.4 Configuration & Data

No new configuration files or data-driven configs. The valid NPC IDs are derived from `state.npcs` at runtime (not hard-coded in the updater). The 3 NPC templates are defined in `src/state/npcs.ts` and loaded into GameState at creation time.

The personality trait names are hard-coded as `['patience', 'empathy', 'cunning', 'logic', 'kindness', 'charisma']` in the validation function (same as current code at line 105 of `saveLoad.ts`).

CombatPhase valid values are hard-coded in validation as `['AI_DECISION', 'VISUAL_INFO', 'PC_DECLARATION', 'ACTION_RESOLUTION', 'PER_ATTACK']`.

Combat status valid values: `['active', 'victory', 'defeat']`.

### 5.5 Integration Points

#### 5.5.1 API Endpoints

##### GET /api/game/saves [T1]
**File**: `src/api/game.ts`
**Handler logic**:
1. Call `listSaves()` (imported from `../persistence/saveLoad.js`)
2. Return `{ success: true, data: result }` with status 200
3. No game-active check needed (listing saves is independent of active session)

**Response schema**: `ApiResponse<SaveSlotInfo[]>`

##### DELETE /api/game/saves/:slot [T1]
**File**: `src/api/game.ts`
**Handler logic**:
1. Parse `request.params.slot` with `parseInt(value, 10)`
2. Call `deleteSave(slot)` -- this throws `INVALID_SLOT` or `SAVE_NOT_FOUND` which the global error handler already maps
3. Return `{ success: true, data: { slot, deleted: true } }` with status 200

**Request schema**: Params `{ slot: string }` (same pattern as save/load routes)
**Response schema**: `ApiResponse<{ slot: number; deleted: boolean }>`

**Typed params**: `fastify.delete<{ Params: { slot: string } }>('/saves/:slot', ...)`

##### GET /api/player/personality [T2]
**File**: `src/api/player.ts`
**Handler logic**:
1. Check `fastify.gameStateContainer.state === null` -- return 404 with `GAME_NOT_FOUND` if no active game
2. Return `{ success: true, data: fastify.gameStateContainer.state.player.personality }` with status 200

**Response schema**: `ApiResponse<Personality>`

##### POST /api/player/team [T2]
**File**: `src/api/player.ts`
**Handler logic**:
1. Check `fastify.gameStateContainer.state === null` -- return 404 with `GAME_NOT_FOUND`
2. Check `fastify.gameStateContainer.state.combatState !== null` -- return 400 with `TEAM_COMPOSITION_INVALID` and message `"Cannot change team during combat"` [D1]
3. Check `fastify.gameStateContainer.state.narrativeState !== null` -- return 400 with `TEAM_COMPOSITION_INVALID` and message `"Cannot change team during narrative scene"` [D1]
4. Extract `npcIds` from `request.body` (expected: `{ npcIds: string[] }`)
5. Call `updateTeamComposition(state, npcIds)` -- throws `TEAM_COMPOSITION_INVALID` on invalid input (caught by global error handler)
6. Set `fastify.gameStateContainer.state = newState`
7. Return `{ success: true, data: { team: newState.team } }` with status 200

**Request body schema**:
```json
{
  "type": "object",
  "properties": {
    "npcIds": {
      "type": "array",
      "items": { "type": "string" }
    }
  },
  "required": ["npcIds"],
  "additionalProperties": false
}
```

**Response schema**: `ApiResponse<{ team: readonly string[] }>`

##### GET /api/npc/:id (modified) [T2]
**File**: `src/api/npc.ts`
**Changed handler logic**:
1. If `fastify.gameStateContainer.state !== null` AND `fastify.gameStateContainer.state.npcs[id]` exists:
   - Return the live NPC from `fastify.gameStateContainer.state.npcs[id]`
2. Else fall back to `getNPC(id)` from template registry
3. If neither source has the NPC, return 404 with `NPC_NOT_FOUND`

This requires adding access to `fastify.gameStateContainer` in the NPC plugin. Since `gameStateContainer` is decorated on the root Fastify instance and the NPC plugin is registered with `fastify.register()`, the container is already accessible via `fastify.gameStateContainer` inside the plugin scope (confirmed by existing pattern in `game.ts` and `player.ts`).

**Import change**: No new imports needed -- `gameStateContainer` is already available via the Fastify instance decoration.

#### 5.5.2 Error Handler Addition
**File**: `src/api/index.ts`
**Change**: Add `TEAM_COMPOSITION_INVALID` mapping to `globalErrorHandler`

Insert before the fallback 500 block (before line 186):
```typescript
if (domainCode === ErrorCodes.TEAM_COMPOSITION_INVALID) {
  const response: ApiResponse<never> = {
    success: false,
    error: { code: ErrorCodes.TEAM_COMPOSITION_INVALID, message: error.message },
  };
  await reply.code(400).send(response);
  return;
}
```

#### 5.5.3 Persistence

**New fields that serialize/deserialize**: `team: readonly string[]` on GameState.

**Backward compatibility**: `loadGame()` normalizes missing `team` to `[]` (see 5.3.2). Old save files without `team` will load successfully.

**Validation on load**: `validateGameState()` accepts `team` as undefined (backward compat) or as an array of 0 or 2 string elements.

#### 5.5.4 Import Updates

**`src/api/game.ts`**: Add `listSaves, deleteSave` to the import from `../persistence/saveLoad.js`. Add `SaveSlotInfo` to the import from `../persistence/saveLoad.js` (for type annotation).

**`src/api/player.ts`**: Add `Personality` to the import from `../types/index.js`. Add `updateTeamComposition` to the import from `../state/stateUpdaters.js`.

**`src/state/stateUpdaters.ts`**: Add `ErrorCodes` to the import from `../types/index.js`.

---

## 6. Acceptance Mapping

### T1: Wire saves management endpoints

| AC | Satisfied By |
|----|-------------|
| T1.1: GET `/api/game/saves` returns `ApiResponse<SaveSlotInfo[]>` | GET /saves route in `src/api/game.ts` calling `listSaves()` |
| T1.2: DELETE `/api/game/saves/:slot` returns confirmation or error | DELETE /saves/:slot route in `src/api/game.ts` calling `deleteSave(slot)` |
| T1.3: Both endpoints use consistent `ErrorCodes` | `deleteSave()` throws `INVALID_SLOT` / `SAVE_NOT_FOUND`, mapped by existing global error handler |
| T1.4: Existing save/load endpoints unchanged | No modifications to POST /save/:slot or POST /load/:slot routes |

### T2: Wire player and NPC state endpoints

| AC | Satisfied By |
|----|-------------|
| T2.1: GET `/api/player/personality` returns personality | GET /personality route in `src/api/player.ts` returning `state.player.personality` |
| T2.2: POST `/api/player/team` accepts NPC selection | POST /team route in `src/api/player.ts` with `{ npcIds: string[] }` body |
| T2.3: Party size constraint (player + 2 NPCs) | `updateTeamComposition()` validates `npcIds.length === 2` |
| T2.4: GET `/api/npc/:id` includes affection/trust | Modified GET /:id in `src/api/npc.ts` reads from `GameState.npcs[id]` when game active |
| T2.5: State updater follows pattern | `updateTeamComposition(state: Readonly<GameState>, npcIds: readonly string[]): GameState` with `updateTimestamp()` |

### T3: Deepen state validation

| AC | Satisfied By |
|----|-------------|
| T3.1: Personality validation (6 traits, 5-35%, sum=100%) | Phase 2 of `validateGameState()`: per-trait range checks + sum check with 0.01 tolerance |
| T3.2: Combat state shape validation | Phase 5 of `validateGameState()`: round, phase, status, 4 arrays |
| T3.3: Narrative state deep validation | Phase 6 of `validateGameState()`: deep field type checks on visitedSceneIds, choiceFlags values, sceneHistory entries |
| T3.4: Typed error with specific failure details | `ValidationResult` type: `{ valid: false; errors: string[] }` with per-field messages; `loadGame()` includes errors in thrown error message |
| T3.5: Sprint 3 narrative validation preserved | Phase 6 preserves all existing checks (currentSceneId, visitedSceneIds, choiceFlags, sceneHistory) and adds deeper type checks |

---

## 7. Integration Points

### Files Modified (with specific identifiers)

| File | Changes |
|------|---------|
| `src/types/index.ts` | Add `team: readonly string[]` to `GameState` interface (after `npcs` field) |
| `src/state/gameState.ts` | Add `team: []` to `createNewGameState()` return object |
| `src/state/stateUpdaters.ts` | Add `updateTeamComposition()` function; add `ErrorCodes` import |
| `src/api/game.ts` | Add GET `/saves` and DELETE `/saves/:slot` routes; add `listSaves`, `deleteSave`, `SaveSlotInfo` imports |
| `src/api/player.ts` | Add GET `/personality` and POST `/team` routes; add `Personality` and `updateTeamComposition` imports |
| `src/api/npc.ts` | Modify GET `/:id` handler to check `gameStateContainer.state.npcs[id]` first |
| `src/api/index.ts` | Add `TEAM_COMPOSITION_INVALID` handler mapping in `globalErrorHandler` |
| `src/persistence/saveLoad.ts` | Change `validateGameState()` return type and implementation; add `ValidationResult` type; update `loadGame()` validation check + error code + team normalization; update `listSaves()` validation check |

### Files NOT Modified
- `src/types/combat.ts` -- read-only reference for validation constants
- `src/types/narrative.ts` -- read-only reference for validation structure
- `src/state/npcs.ts` -- unchanged, templates still serve as fallback
- `src/api/dialogue.ts`, `src/api/combat.ts`, `src/api/narrative.ts` -- unchanged

---

## 8. Open Items

- [VERIFY] Confirm that `fastify.gameStateContainer` is accessible in the NPC plugin scope at runtime. Pattern is used in `game.ts` and `player.ts` already, so it should work, but verify during execution with a test.
- [VERIFY] Confirm that existing 969 tests still pass after adding `team` field to `GameState`. Tests that create GameState objects manually will need `team: []` added. Run full test suite during execution to identify any breakage.
- [VERIFY] Confirm that `listSaves()` behavior with the new `ValidationResult` return works correctly for corrupted save files (should continue to mark them as `exists: false`).

---

## 9. Producer Handoff

### Output Format
TypeScript source files (.ts) -- modifications to existing files only. No new files created.

### Producer Name
code-writer

### Files in Creation/Modification Order (respecting dependency)

1. **`src/types/index.ts`** (~210 lines, +2 lines changed)
   - Add `readonly team: readonly string[];` to `GameState` interface after the `npcs` field (after line 97)
   - This must be first because all other files import `GameState`

2. **`src/state/gameState.ts`** (~44 lines, +1 line changed)
   - Add `team: [],` to the `createNewGameState()` return object (after line 35 `npcs`)
   - Depends on: updated `GameState` type from step 1

3. **`src/state/stateUpdaters.ts`** (~330 lines, +40 lines added)
   - Add `ErrorCodes` to the import from `../types/index.js` (line 12)
   - Add `updateTeamComposition()` function at the end, before or after the Narrative State Updaters section
   - New section header: `// Team Composition`
   - Depends on: updated `GameState` type from step 1

4. **`src/persistence/saveLoad.ts`** (~320 lines, +80 lines changed)
   - Add `ValidationResult` type export above `validateGameState` (after line 74)
   - Rewrite `validateGameState()` to return `ValidationResult` with accumulated errors
   - Update `loadGame()`: change validation check to use `ValidationResult`, change error code to `VALIDATION_ERROR`, add `team` normalization
   - Update `listSaves()`: change validation check to use `ValidationResult`
   - Depends on: updated `GameState` type from step 1

5. **`src/api/index.ts`** (~260 lines, +10 lines added)
   - Add `TEAM_COMPOSITION_INVALID` error handler mapping in `globalErrorHandler`
   - Insert before the fallback 500 block
   - Depends on: nothing new, but logically pairs with step 6

6. **`src/api/game.ts`** (~175 lines, +30 lines added)
   - Add `listSaves`, `deleteSave` imports from `../persistence/saveLoad.js`
   - Add `SaveSlotInfo` import from `../persistence/saveLoad.js`
   - Add GET `/saves` route
   - Add DELETE `/saves/:slot` route
   - Depends on: nothing new

7. **`src/api/player.ts`** (~130 lines, +55 lines added)
   - Add `Personality` to the import from `../types/index.js`
   - Add `updateTeamComposition` to the import from `../state/stateUpdaters.js`
   - Add GET `/personality` route
   - Add POST `/team` route with lock checks
   - Depends on: `updateTeamComposition` from step 3

8. **`src/api/npc.ts`** (~62 lines, +10 lines changed)
   - Modify GET `/:id` handler to check `fastify.gameStateContainer.state?.npcs[id]` first
   - Fall back to `getNPC(id)` when no game active or NPC not in state
   - Depends on: nothing new

### Instruction Tone
Mechanical. Every function signature, field name, error message, and validation check is fully specified above. The code-writer should implement exactly what is described without making design decisions. Refer back to this blueprint for every detail. When in doubt, match the existing codebase patterns (error throwing, updateTimestamp wrapping, ApiResponse envelope, typed params).

### Test Strategy
No new test files are specified in this blueprint. The [VERIFY] items in Open Items section require running the existing test suite (`npm test` or equivalent) to confirm no regressions from the `GameState.team` field addition. Test files that manually construct `GameState` objects will need `team: []` added -- the code-writer should fix these as compilation errors surface during the build step.
