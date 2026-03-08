# Game Backend Engine - Specialist Stage 1

## Research Findings

### T1: Saves Management Endpoints

- `listSaves(savesDir?)` returns `Promise<SaveSlotInfo[]>` -- always 10 entries (slots 1-10), each with `{ slot, exists, metadata? }`. `SaveMetadata` = `{ slot, timestamp, playerName }`.
- `deleteSave(slot, savesDir?)` returns `Promise<void>`. Validates slot range (1-10), throws `INVALID_SLOT` for out-of-range, `SAVE_NOT_FOUND` if file doesn't exist.
- Both functions throw errors with `.code` property matching `ErrorCodes` constants -- the global error handler in `src/api/index.ts` already maps `INVALID_SLOT` to 400 and `SAVE_NOT_FOUND` to 404.
- Existing game plugin (`src/api/game.ts`) handles save/load at POST `/save/:slot` and POST `/load/:slot`. New saves management routes (GET `/saves`, DELETE `/saves/:slot`) should logically live in the same game plugin since they share the `/api/game` prefix.
- Route pattern: typed params via `<{ Params: { slot: string } }>`, parse slot with `parseInt`, return `ApiResponse<T>` envelope.

### T2: Player/NPC State Endpoints

- **GET `/api/player/personality`**: Player plugin (`src/api/player.ts`) already has GET `/` returning full `PlayerCharacter`. A new `/personality` route returns just `state.player.personality` (type `Personality`).
- **POST `/api/player/team`**: No team composition exists anywhere in the codebase. `GameState` has no `team` or `party` field. Combat uses `EncounterConfig` with explicit `playerParty`/`enemyParty` arrays at combat init -- party is NOT persisted on GameState.
- NPC plugin (`src/api/npc.ts`) GET `/:id` currently returns NPC templates from `getNPC()` (frozen objects from `src/state/npcs.ts`). These templates have base affection/trust values (e.g., Lars starts at trust: -20). The **live** NPC state (with relationship changes from dialogue) is on `GameState.npcs[id]`. Current endpoint does NOT read from GameState -- it reads from the frozen template registry.
- State updater pattern: `function name(state: Readonly<GameState>, ...args): GameState` with `updateTimestamp()` wrapper. All updaters in `src/state/stateUpdaters.ts`.
- NPC interface: `{ id, archetype, personality, affection, trust }` -- affection and trust are already on the NPC type. No additional relationship data exists beyond these two numbers.
- The 3 NPCs: Elena (`npc_scout_elena`), Lars (`npc_merchant_lars`), Kade (`npc_outlaw_kade`).

### T3: State Validation

- Current `validateGameState()` in `src/persistence/saveLoad.ts` lines 85-141:
  - Checks basic types for top-level fields (timestamp, currentDialogueNode, saveSlot, conversationLog)
  - Validates player: id (string), name (string), personality (object with 6 number traits)
  - Validates NPCs: id, archetype, affection, trust (all numbers), personality (6 traits)
  - **Missing**: personality range (5-35%), personality sum (100%), affection/trust range validation
  - combatState: currently accepts ANY value including null (no structural validation)
  - narrativeState: checks top-level shape (currentSceneId string, visitedSceneIds array, choiceFlags object, sceneHistory array) but no deep field type checks
- CombatState shape (from `src/types/combat.ts`): `{ round: number, phase: CombatPhase, playerParty: Combatant[], enemyParty: Combatant[], actionQueue: CombatAction[], roundHistory: RoundResult[], status: 'active'|'victory'|'defeat' }`
- CombatPhase valid values: `'AI_DECISION' | 'VISUAL_INFO' | 'PC_DECLARATION' | 'ACTION_RESOLUTION' | 'PER_ATTACK'`
- NarrativeState fields needing deep validation: `SceneHistoryEntry` has `{ sceneId: string, choiceId: string, timestamp: number }`.

### Cross-Cutting Patterns

- No API-level integration tests exist (no `*.test.ts` files under `src/api/`). All existing tests are unit tests co-located with their source module.
- Global error handler maps error `.code` to HTTP status. New domain errors (e.g., `TEAM_COMPOSITION_INVALID`) already have an `ErrorCodes` entry but no handler mapping.
- Plugin registration order in `src/api/index.ts`: game, player, npc, dialogue, combat, narrative. New routes added to existing plugins don't affect registration order.

## ECD Analysis

### Entity: GameState.team (new)
Team composition is the only genuinely new data structure needed. Everything else is wiring existing functions or deepening existing validation.

**What needs to exist for team to work?**
1. A field on GameState representing which 2 of 3 NPCs are in the active party
2. A state updater to set/change team composition
3. An API endpoint to accept team selection
4. Validation that exactly 2 NPCs are selected from the 3 available
5. Compatibility with Sprint 3's synergy system (reads party at combat init)

**Representation options:**
- **Array of NPC IDs**: `team: readonly string[]` -- simple, explicit. Store `['npc_scout_elena', 'npc_outlaw_kade']`.
- **Set-like record**: `team: Record<string, boolean>` -- over-engineered for 2-of-3 selection.
- **Nullable tuple**: `team: [string, string] | null` -- enforces exactly 2 at type level but arrays don't enforce length at runtime.

Array of IDs is the clear winner: simple, serializable, easy to validate length.

### Entity: NPC endpoint enhancement
The current GET `/api/npc/:id` reads from frozen templates (`getNPC()`), NOT from GameState. This means it returns the *initial* NPC data, not the live relationship values. To include current affection/trust, the endpoint must read from `GameState.npcs[id]` when a game is active, falling back to the template when no game exists.

**Trade-off**: Merging template + live state in one endpoint vs. separate endpoints.
- Single endpoint is simpler for consumers (one GET for all NPC info).
- Template data (id, archetype, personality) never changes; only affection/trust are live.
- Recommendation: When game is active, return `GameState.npcs[id]`. When no game, return template.

### Entity: validateGameState deepening
Must validate across 3 subsystem states (personality, combat, narrative) without breaking existing Sprint 3 validation.

**Approach**: Add validation checks inline within the existing function, inserting after each subsystem's structural check. Personality range/sum checks go right after the trait number checks. Combat shape validation goes where the current "any value acceptable" comment is. Narrative deep checks go after the existing structural checks.

**Tolerance question for personality sum**: Should sum === 100 exactly, or allow floating-point tolerance? The `adjustPersonality()` function uses redistribution that should maintain 100.0 exactly, but saved data could have rounding artifacts. A tight epsilon (e.g., 0.01) is safer than exact equality.

### Entity: Error handling for new endpoints
- `TEAM_COMPOSITION_INVALID` already exists in ErrorCodes -- just needs a handler mapping in the global error handler.
- `VALIDATION_ERROR` already exists and is mapped to 400.
- For the validation failures in T3, the function returns boolean -- it doesn't throw. The caller (loadGame) throws with `SAVE_NOT_FOUND` code when validation fails. This is misleading (save exists but is invalid). Should use a more specific error code.

## Assumptions

### A1: Team composition stored as NPC ID array on GameState
- **Default**: Add `readonly team: readonly string[]` to `GameState` interface, defaulting to empty array in `createNewGameState()`.
- **Rationale**: Array of NPC IDs is the simplest serializable representation for a 2-of-3 selection. Empty array means "team not yet chosen" which is a valid initial state for a new game. This is the most common pattern for party composition in game engines.

### A2: Saves list returns slot metadata, not full GameState
- **Default**: GET `/api/game/saves` returns `ApiResponse<SaveSlotInfo[]>` directly from `listSaves()` -- slot number, exists flag, and metadata (timestamp + playerName) for occupied slots.
- **Rationale**: Full GameState per slot would be expensive (10 full state objects) and unnecessary. Metadata is sufficient for a save slot picker UI. The existing `listSaves()` already returns exactly this shape.

### A3: NPC endpoint returns live GameState data when game is active
- **Default**: GET `/api/npc/:id` returns `GameState.npcs[id]` when a game is active, falls back to `getNPC(id)` template when no game exists.
- **Rationale**: The NPC type already includes affection and trust fields. Returning the live state gives the frontend current relationship data without requiring a separate endpoint. The existing NPC type shape serves both purposes.

### A4: New routes added to existing plugins (no new plugin files)
- **Default**: Saves routes go in `src/api/game.ts` (under `/api/game` prefix). Player team route goes in `src/api/player.ts` (under `/api/player` prefix). Personality read-only route also in `src/api/player.ts`.
- **Rationale**: Routes share their plugin's URL prefix. Creating new plugin files would add registration complexity for no benefit. The existing plugins are small enough to absorb 1-2 additional routes.

### A5: Validation rejects with boolean, caller provides error context
- **Default**: `validateGameState()` continues to return `boolean`. When it returns false, the caller (loadGame) throws a typed error. For T3's "specific validation failure details" requirement, we change the return type to a result object.
- **Rationale**: A boolean return cannot communicate *what* failed. Returning `{ valid: true } | { valid: false, errors: string[] }` satisfies the acceptance criterion for specific failure details while remaining a pure function.

### A6: Floating-point tolerance for personality sum validation
- **Default**: Personality sum validated with tolerance of 0.01 (i.e., `Math.abs(sum - 100) < 0.01`).
- **Rationale**: The personality system uses redistribution math that theoretically maintains sum = 100.0, but JSON serialization round-trips and floating-point arithmetic can introduce tiny drift. A tight epsilon prevents false rejections while catching genuinely corrupt data.

## Key Decisions

### D1: Team composition rules -- max party size, selectability, changeability
- **Tier**: [USER]
- **Options**:
  - **(A) Player + exactly 2 NPCs, all 3 selectable, changeable anytime**: Simplest. POST `/api/player/team` accepts any 2 NPC IDs from the 3 available. Can be called repeatedly to swap team members. No restriction on when team can change.
  - **(B) Player + exactly 2 NPCs, all 3 selectable, locked during combat/narrative**: Team can be changed freely outside of combat and narrative, but POST returns error if `combatState !== null` or `narrativeState !== null`.
  - **(C) Player + exactly 2 NPCs, all 3 selectable, locked after first combat**: Team is permanent once combat begins for the first time. This is the most restrictive and creates permanent consequences.
- **Recommendation**: Option (A) for the POC/pitch build. Simplest implementation, maximum flexibility for testing. Locking can be added later when game flow is more defined. The acceptance criteria say "team composition respects party size constraint (player + 2 NPCs)" -- no mention of locking.
- **Risk if wrong**: If Option A is chosen but the game design needs locking, it's easy to add a guard later. If Option C is chosen prematurely, it makes testing harder and may need to be relaxed.

### D2: validateGameState return type change
- **Tier**: [SPEC]
- **Options**:
  - **(A) Keep boolean, add separate `getValidationErrors()` function**: Two functions -- one for quick check, one for detailed errors. Risk: callers must know to use the right one.
  - **(B) Change return to validation result object**: `{ valid: true } | { valid: false, errors: string[] }`. Single function, always returns details. Breaking change for existing callers that check `if (!validateGameState(data))`.
  - **(C) Keep boolean + throw on first failure with descriptive message**: Simple but loses ability to report multiple issues.
- **Recommendation**: Option (B). Change return type to `ValidationResult`. Update the two call sites (loadGame and listSaves) to use `result.valid`. This satisfies AC4 ("specific validation failure details") cleanly. The function is only called in 2 places, making the migration trivial.
- **Risk if wrong**: Breaking change to the function signature. But with only 2 call sites and co-located tests, the blast radius is tiny.

### D3: NPC endpoint data source (template vs. live state)
- **Tier**: [SPEC]
- **Options**:
  - **(A) Always return template data**: No change to current behavior. Affection/trust always show initial values.
  - **(B) Return live GameState NPC when game active, template otherwise**: GET `/api/npc/:id` checks `gameStateContainer.state`, returns live data if available.
  - **(C) Return template + overlay live relationship data**: Merge template base with live affection/trust from GameState.
- **Recommendation**: Option (B). The NPC type is identical for templates and live state -- no merging needed. The live state already contains all template fields (id, archetype, personality) plus the current affection/trust values. This gives the frontend a single source of truth.
- **Risk if wrong**: If the frontend expects template data specifically (e.g., to show "base personality"), it won't be available separately. However, NPC personality is fixed (only player personality changes), so template and live personality are always identical.

### D4: Error code for corrupt save validation failure
- **Tier**: [SILENT]
- **Options**:
  - **(A) Continue using SAVE_NOT_FOUND for corrupt saves**: Current behavior. Misleading but functional.
  - **(B) Add new VALIDATION_FAILED error code**: Specific to corrupt save data, maps to 422 (Unprocessable Entity).
  - **(C) Use existing VALIDATION_ERROR code**: Already in ErrorCodes, already mapped to 400.
- **Recommendation**: Option (C). Reuse `VALIDATION_ERROR` with a descriptive message listing what failed. No new error code needed. The message from the validation result provides specificity.
- **Risk if wrong**: If 400 vs 422 semantics matter to the frontend, we'd need to add a code. For a POC, this is negligible.

### D5: Where to add team field to GameState
- **Tier**: [SILENT]
- **Options**:
  - **(A) Add to GameState interface in `src/types/index.ts`**: `readonly team: readonly string[]`
  - **(B) Create a separate team type file**: Over-engineered for a single array field.
- **Recommendation**: Option (A). Add the field directly to GameState. Initialize to `[]` in `createNewGameState()`. Validate in `validateGameState()`.
- **Risk if wrong**: None -- this is the obvious location following existing patterns.

### D6: Combat state validation depth
- **Tier**: [SPEC]
- **Options**:
  - **(A) Validate full CombatState recursively** (round, phase, playerParty with Combatant fields, enemyParty, etc.): Comprehensive but complex. CombatState has deeply nested types (Combatant -> ReactionSkills, Buff arrays, etc.).
  - **(B) Validate top-level CombatState shape only**: Check round is number, phase is valid enum string, playerParty/enemyParty are arrays, actionQueue is array, roundHistory is array, status is valid enum. Don't recurse into Combatant fields.
  - **(C) Validate CombatState + first-level Combatant fields**: Middle ground -- check top-level CombatState shape plus each Combatant has id, name, stamina, etc. Don't recurse into ReactionSkills/Buff.
- **Recommendation**: Option (B). Top-level shape validation catches the most common corruption scenarios (missing fields, wrong types) without the complexity of deep Combatant validation. Combat state is transient (null between encounters) and created by the combat engine (not user input), so deep corruption is unlikely. If a save has combat state, the shape check ensures it's structurally recognizable.
- **Risk if wrong**: A deeply corrupted Combatant object could pass validation but crash the combat engine. Mitigation: combat engine already does its own input validation at encounter init.

## Risks Identified

### R1: Team field addition is a GameState schema change
Adding `team: readonly string[]` to GameState means all existing save files lack this field. `loadGame()` must handle backward compatibility -- either normalize missing `team` to `[]` (like the existing `narrativeState` normalization) or reject old saves.
- **Mitigation**: Add normalization in `loadGame()` after validation, mirroring the existing `narrativeState === undefined -> null` pattern. Update `validateGameState()` to accept missing `team` field (treat as `[]`).

### R2: Changing validateGameState return type breaks 2 call sites
The function is called in `loadGame()` (line 207) and `listSaves()` (line 240). Both currently check `if (!validateGameState(data))`. Changing to a result object requires updating both.
- **Mitigation**: Both call sites are in the same file (`saveLoad.ts`). Update is trivial and can be done atomically. Tests will catch any missed updates.

### R3: NPC endpoint behavior change (template -> live state)
Changing GET `/api/npc/:id` to return live state when a game is active changes observable behavior. Any tests asserting template values will need updating.
- **Mitigation**: Current NPC tests are in `src/state/npcs.test.ts` (testing the registry, not the API). No API integration tests exist. Risk is minimal.

### R4: 969 existing tests must not break
All changes must maintain backward compatibility with the existing test suite.
- **Mitigation**: Run full test suite after each task. Additive changes (new routes, new fields with defaults) should not affect existing tests. The validateGameState change (R2) is the highest risk.

### R5: Personality sum tolerance edge case
If tolerance is too loose, genuinely corrupt personality data passes validation. If too strict, valid saves with floating-point drift are rejected.
- **Mitigation**: Use 0.01 tolerance. The personality system's redistribution algorithm works with integers internally (5-35 range), so drift should be well under 0.01. Test with known edge cases.

## Recommended Approach

### T1: Wire saves management endpoints (Light)

1. **Add 2 routes to `src/api/game.ts`**:
   - `GET /saves` -- calls `listSaves()`, wraps result in `ApiResponse<SaveSlotInfo[]>`. No game-active check needed (listing saves is independent of active session).
   - `DELETE /saves/:slot` -- parses slot param, calls `deleteSave(slot)`. Errors propagate to global handler (INVALID_SLOT -> 400, SAVE_NOT_FOUND -> 404).
2. **Import `listSaves` and `deleteSave`** from `../persistence/saveLoad.js` in game plugin.
3. **No new error handler mappings needed** -- INVALID_SLOT and SAVE_NOT_FOUND are already handled.
4. **Tests**: Add route-level tests using `buildApp()` + `fastify.inject()` for both happy path and error cases.

### T2: Wire player and NPC state endpoints (Standard)

1. **Add `team` field to GameState** (`src/types/index.ts`): `readonly team: readonly string[]`
2. **Update `createNewGameState()`** (`src/state/gameState.ts`): initialize `team: []`
3. **Add `updateTeamComposition()` state updater** (`src/state/stateUpdaters.ts`):
   - Signature: `(state: Readonly<GameState>, npcIds: readonly string[]) => GameState`
   - Validates: exactly 2 IDs, all IDs exist in `state.npcs`, no duplicates
   - Throws `TEAM_COMPOSITION_INVALID` on validation failure
4. **Add routes to `src/api/player.ts`**:
   - `GET /personality` -- returns `ApiResponse<Personality>` with `state.player.personality`
   - `POST /team` -- accepts `{ npcIds: string[] }`, calls `updateTeamComposition()`, returns `ApiResponse<GameState>`
5. **Modify `src/api/npc.ts` GET `/:id`**: When `gameStateContainer.state` is not null, return `state.npcs[id]` instead of template. Fall back to `getNPC(id)` when no game active.
6. **Add error handler mapping** for `TEAM_COMPOSITION_INVALID` -> 400 in `src/api/index.ts`.
7. **Backward compatibility**: Add `team` normalization in `loadGame()` (missing -> `[]`).

### T3: Deepen state validation (Light)

1. **Change `validateGameState()` return type** to `ValidationResult`:
   ```typescript
   type ValidationResult = { valid: true } | { valid: false; errors: string[] };
   ```
2. **Add personality deep validation**:
   - Each trait in [5, 35] range
   - Sum within 0.01 of 100
   - Both player and NPC personalities
3. **Add combat state shape validation** (when not null):
   - `round` is number, `phase` is valid CombatPhase, `status` is valid enum
   - `playerParty`, `enemyParty`, `actionQueue`, `roundHistory` are arrays
4. **Deepen narrative state validation** (when not null):
   - `visitedSceneIds` entries are strings
   - `choiceFlags` values are booleans
   - `sceneHistory` entries have `sceneId` (string), `choiceId` (string), `timestamp` (number)
5. **Add team field validation**: array of strings, length 0 or 2, all entries exist as valid NPC IDs (optional -- may be too strict for validation without NPC registry access)
6. **Update call sites** in `loadGame()` and `listSaves()` to use new return type.
7. **Preserve existing Sprint 3 narrative validation** -- extend, don't replace.

### Execution Order
Tasks are independent and can execute in parallel. T2 and T3 both modify GameState and validateGameState, so if executed sequentially, T2 should go first (adds team field) then T3 (validates it). If parallel, each must be aware of the other's GameState changes.

### Files Modified (predicted)
- `src/types/index.ts` -- add `team` field to GameState, add `ValidationResult` type
- `src/state/gameState.ts` -- initialize `team: []`
- `src/state/stateUpdaters.ts` -- add `updateTeamComposition()`
- `src/api/game.ts` -- add GET `/saves`, DELETE `/saves/:slot`
- `src/api/player.ts` -- add GET `/personality`, POST `/team`
- `src/api/npc.ts` -- modify GET `/:id` to use live state
- `src/api/index.ts` -- add TEAM_COMPOSITION_INVALID error handler mapping
- `src/persistence/saveLoad.ts` -- deepen `validateGameState()`, update return type, add team normalization in `loadGame()`
