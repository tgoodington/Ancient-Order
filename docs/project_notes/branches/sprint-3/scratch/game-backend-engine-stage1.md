# Game Backend Engine - Stage 1 Exploration Analysis

**Specialist:** Game Backend Engine
**Sprint:** 3 - Narrative & State Machine
**Tasks:** T1-T9, T12 (code/backend domain)
**Date:** 2026-03-02

---

## Research Findings

### RF-1: Type System Architecture
- GameState has 7 fields; `combatState: CombatState | null` is the precedent for nullable subsystem state.
- `types/combat.ts` is a standalone leaf file with zero imports from `types/index.ts`. One-way dependency: `index.ts` imports `CombatState` from `combat.ts`.
- ErrorCodes is a const object (not enum) for ESM compatibility. Existing codes cover game, save, NPC, dialogue, and combat domains.
- DialogueOption already has `gate?: PersonalityGate`, `personalityAdjustment?`, `npcAdjustment?` — the exact same effect types needed for scene choices.

### RF-2: State Updater Patterns (Canonical)
- Signature: `(state: Readonly<GameState>, ...) => GameState`
- Every updater wraps with `updateTimestamp()` as the outermost call.
- Immutability via shallow spread at each nesting level. No deep clone.
- Silent return of input state on missing NPCs (no throws). Clamping for NPC values.
- `processDialogueChoice` chains: personality adjustment -> NPC relationship -> conversation log. This is the orchestration pattern for choice effects.

### RF-3: Dialogue Engine as Pattern Template
- Graph traversal: stateless trees passed as parameters (not stored on state).
- Gate evaluation: single trait comparison via `evaluatePersonalityGate`.
- Dead-end detection: checks every node has at least one ungated option.
- Result type bundles `{ state, nextNode, selectedOption }`.
- Validation is a standalone function, not a runtime constraint.
- Fixtures file provides test data separately from production data.

### RF-4: Combat Sync Layer Integration Point
- `initCombatState(_gameState, encounter)` — `_gameState` is currently unused (underscore-prefixed placeholder).
- Combatant creation: `_configToCombatant()` maps config to live combatant.
- Synergy integration confirmed: apply multiplier to `power` or `speed` on player party combatants after `_configToCombatant` mapping, before returning CombatState.
- The function already imports from `types/index.js` and `types/combat.js`.

### RF-5: Persistence Constraints
- `validateGameState()` performs structural checks. `combatState` accepts any value (no deep validation).
- No schema versioning exists. No migration layer.
- Adding `narrativeState` requires nullable field + validation update for backward compat.
- JSON.stringify/parse round-trip means: no Sets, no Maps, no functions, no circular refs.

### RF-6: Fastify Plugin Architecture
- Plugin signature: `async function [name]Plugin(fastify: FastifyInstance): Promise<void>`
- State access: `fastify.gameStateContainer.state` — shared mutable container across all plugins.
- Route pattern: generics for Params/Body, JSON Schema validation in route options, ApiResponse<T> return.
- 5 existing plugins: game, player, npc, dialogue, combat. Narrative will be the 6th.

### RF-7: GameState Initialization
- `createNewGameState()` sets all nullable subsystems to `null` (combatState, currentDialogueNode, saveSlot).
- 3 NPC templates (Elena, Lars, Kade) frozen via `Object.freeze()`. Fixed personalities.
- Player starts with `createDefaultPersonality()` — equal distribution across 6 traits.

### RF-8: Test Infrastructure
- Co-located `.test.ts` files, Vitest framework.
- Two-tier fixtures: shared fixture files + local factory functions with `Partial<T>` overrides.
- Immutability verified via reference inequality: `expect(updated).not.toBe(baseState)`.
- ESM imports use `.js` extensions throughout.

### RF-9: Synergy Design Spec (Complete)
- Two paradigms: Well Rounded (power x1.10) and Bond (speed x1.10).
- Well Rounded: every trait maxed across party >= 25%, matchQuality = sum(maxes) / 150.
- Bond: player's top-2 trait alignment with NPC >= 0.80, matchQuality = bestAlignmentRatio.
- Highest matchQuality wins; Well Rounded wins ties.
- Config in `src/fixtures/synergyConfig.ts`.

### RF-10: Configuration Patterns
- TypeScript typed exports for compile-time safety (NPC templates, archetype profiles).
- JSON with `import ... with { type: 'json' }` for data fixtures (encounter.json).
- Registry pattern: Record or ReadonlyMap with lookup functions.

---

## ECD Analysis

### Elements (Data Structures & Modules)

| Element | Type | Location | New/Existing |
|---------|------|----------|--------------|
| NarrativeState | Interface | `types/narrative.ts` | New |
| Scene | Interface | `types/narrative.ts` | New |
| SceneChoice | Interface | `types/narrative.ts` | New |
| ScenePrerequisite | Interface | `types/narrative.ts` | New |
| ChoiceConsequence | Interface | `types/narrative.ts` | New |
| SynergyBonus / SynergyResult | Interface | `types/narrative.ts` | New |
| ParadigmConfig | Interface | `types/narrative.ts` | New |
| GameState.narrativeState | Field | `types/index.ts` | Extended |
| sceneEngine | Module | `narrative/sceneEngine.ts` | New |
| choiceEngine | Module | `narrative/choiceEngine.ts` | New |
| narrativeStateMachine | Module | `narrative/narrativeStateMachine.ts` | New |
| synergyCalculator | Module | `narrative/synergyCalculator.ts` | New |
| stateUpdaters (narrative) | Functions | `state/stateUpdaters.ts` | Extended |
| saveLoad validation | Function | `persistence/saveLoad.ts` | Extended |
| initCombatState | Function | `combat/sync.ts` | Extended |
| narrativePlugin | Plugin | `api/narrative.ts` | New |
| Scene JSON data | Fixtures | `fixtures/scenes/` | New |
| synergyConfig | Config | `fixtures/synergyConfig.ts` | New |

### Connections (Data Flow & Dependencies)

```
types/narrative.ts ──────────────────────────────────────┐
    │ (leaf file, no imports from index.ts)               │
    │                                                      │
types/index.ts ← import NarrativeState ───────────────────┘
    │
    ├──→ narrative/sceneEngine.ts (reads Scene[], evaluates prerequisites)
    │         │
    │         └──→ narrative/choiceEngine.ts (validates + processes choices)
    │                   │
    │                   └──→ narrative/narrativeStateMachine.ts (integrates both)
    │                             │
    ├──→ state/stateUpdaters.ts ←─┘ (narrative updaters call state machine)
    │         │
    │         ├──→ persistence/saveLoad.ts (serializes NarrativeState)
    │         │
    │         └──→ api/narrative.ts (exposes via REST)
    │
    ├──→ narrative/synergyCalculator.ts (pure calculator)
    │         │
    │         └──→ combat/sync.ts (applies synergy at combat init)
    │
    └──→ fixtures/scenes/*.json (scene data)
         fixtures/synergyConfig.ts (paradigm config)
```

**Key dependency chains:**
1. Types (T1) -> Scene Engine (T2) -> Choice Engine (T3) -> State Machine (T4) -> State Updaters (T5) -> Persistence (T6) -> API (T9)
2. Types (T1) -> Synergy Calculator (T7) -> Combat Integration (T8)
3. Both chains converge at Integration Validation (T12)

### Dynamics (Runtime Behavior)

**Scene Traversal Flow:**
1. API receives "get current scene" request
2. Plugin reads `gameState.narrativeState.currentSceneId`
3. Scene engine loads scene, evaluates prerequisites against NarrativeState flags + player personality
4. Returns scene content with filtered available choices (ungated always included)

**Choice Processing Flow:**
1. API receives choice submission (sceneId, choiceId)
2. Choice engine validates choice is available (gate evaluation against current personality)
3. Applies consequences: personality adjustment (via `adjustPersonality`), NPC state changes (via existing `updateNPCAffection`/`updateNPCTrust`), flag setting
4. State machine advances: updates currentSceneId, adds to visitedScenes, updates flags
5. GameState updater wraps all changes with `updateTimestamp()`
6. Returns updated GameState + next scene info

**Synergy-Combat Bridge:**
1. `initCombatState` called with GameState + EncounterConfig
2. Extracts player personality + party NPC personalities from GameState
3. Calls `calculateSynergy()` with extracted data + paradigm config
4. If SynergyBonus returned: `Math.round(stat * multiplier)` on player party combatants
5. Returns CombatState with synergy-modified stats

**Save/Load with Narrative:**
1. Save: `JSON.stringify(gameState)` — NarrativeState serializes as-is (arrays, not Sets)
2. Load: `JSON.parse()` -> `validateGameState()` checks for narrativeState presence
3. Backward compat: missing narrativeState treated as `null` (pre-Sprint-3 saves)

---

## Assumptions

### A1: Narrative Types in Separate File (types/narrative.ts)
- **Default:** Create `types/narrative.ts` as a standalone leaf file, mirroring the `types/combat.ts` pattern. `types/index.ts` imports `NarrativeState` from it and adds the nullable field to GameState.
- **Rationale:** Sprint 2 established this exact pattern — `combat.ts` is a leaf with zero imports from `index.ts`. Keeps narrative types self-contained, avoids bloating `index.ts`, prevents circular imports. The one-way dependency (`index.ts` -> `narrative.ts`) is proven safe.

### A2: Scene Graphs Are Stateless Parameters (Like Dialogue Trees)
- **Default:** Scene graph data is loaded from JSON and passed as a parameter to engine functions, not stored on GameState or NarrativeState.
- **Rationale:** The dialogue engine established this pattern — `dialogueTree` is a parameter to every function, never stored on GameState. Scene graphs are authored data (read-only at runtime), so they belong in fixtures/config, not in mutable state. This keeps NarrativeState small and serializable.

### A3: Visited Scenes Stored as Array (Not Set)
- **Default:** `NarrativeState.visitedSceneIds` is `readonly string[]` — an array, not a Set.
- **Rationale:** JSON serialization constraint. Sets don't survive `JSON.stringify`/`JSON.parse` round-trip. Array lookup is O(n) but scene counts are small (2-3 in Sprint 3, maybe 20-30 at Act 1 scale). If performance matters later, convert to Set at runtime and back to array for persistence.

### A4: Choice Flag Map Is Record<string, boolean>
- **Default:** `NarrativeState.choiceFlags` is `Readonly<Record<string, boolean>>` — simple string-to-boolean map.
- **Rationale:** Plan specifies "simple string->boolean (or string->value) map that serializes to JSON." Boolean is sufficient for Sprint 3 — flags are either set or not. If richer values are needed later, the type can be widened to `Record<string, string | number | boolean>` without breaking existing flags.

### A5: Scene Engine and Dialogue Engine Remain Independent
- **Default:** The scene engine (`narrative/sceneEngine.ts`) does not import from or share utilities with the dialogue engine (`dialogue/dialogueEngine.ts`).
- **Rationale:** Despite structural similarity (graph traversal + gates), the two systems serve different purposes: dialogue is intra-NPC conversation flow; scenes are narrative progression with prerequisites, flags, and multi-effect consequences. The dialogue engine evaluates single-trait `PersonalityGate` objects; scenes need compound prerequisites (trait checks + flag checks + visited-scene checks). Coupling them would create fragile cross-dependencies. Parallel implementation is explicitly noted as acceptable in the plan's risk table.

### A6: Silent Error Handling for Missing Narrative State
- **Default:** Narrative updaters return input state unchanged when `narrativeState` is null, matching the existing pattern where `updateNPCAffection` silently returns state for missing NPCs.
- **Rationale:** Consistent with established error handling philosophy — state updaters are forgiving, API layer handles validation and error responses. This prevents runtime crashes when narrative operations are called before narrative initialization.

### A7: Synergy Types Co-located in types/narrative.ts
- **Default:** SynergyBonus, SynergyResult, and ParadigmConfig interfaces live in `types/narrative.ts` alongside scene/choice types.
- **Rationale:** Synergy is a narrative-layer concept (party composition bonuses based on personality alignment). It doesn't belong in `types/combat.ts` (which is combat-engine-internal) or `types/index.ts` (which is the shared root). Grouping it with narrative types keeps the type separation clean: `index.ts` (core), `combat.ts` (combat engine), `narrative.ts` (Sprint 3 narrative + synergy).

---

## Key Decisions

### D1: Scene Prerequisite Model — Compound vs Single
- **Options:**
  - **(A) Single prerequisite per check** — each prerequisite is one condition (trait >= X, flag == true, scene visited). Scene has `prerequisites: ScenePrerequisite[]` where ALL must pass (implicit AND).
  - **(B) Compound prerequisites with AND/OR** — prerequisites support nested boolean logic (AND groups, OR groups).
  - **(C) Flat list with an `operator` field** — `prerequisites: ScenePrerequisite[]` with a scene-level `prerequisiteMode: 'all' | 'any'` for AND/OR.
- **Recommendation:** Option A — flat array with implicit AND. Sufficient for Sprint 3 (2-3 scenes). OR logic can be simulated by creating separate scene variants. Dialogue engine's `PersonalityGate` is single-condition, and the plan describes prerequisites as "trait checks, choice flag checks" without mentioning compound logic.
- **Risk if wrong:** If Act 1 expansion needs "trait >= 20 OR flag 'helped_elena' set", we'd need to restructure prerequisites. Low risk — can add `prerequisiteMode: 'all' | 'any'` later as a non-breaking extension, or nest into groups.

### D2: SceneChoice Effect Reuse — Wrap vs Inline
- **Options:**
  - **(A) Reuse existing updaters directly** — choice engine calls `applyPersonalityAdjustment()`, `updateNPCRelationship()`, etc. from `stateUpdaters.ts`.
  - **(B) New narrative-specific effect updaters** — duplicate the logic in narrative module for independence.
  - **(C) Shared effect types with a generic applier** — abstract effect application into a shared utility.
- **Recommendation:** Option A — direct reuse. The plan explicitly requires this: "NPC state effects must reuse the existing `updateNPCAffection`, `updateNPCTrust`, and `updateNPCRelationship` updaters." The `processDialogueChoice` orchestrator in `stateUpdaters.ts` already demonstrates the chaining pattern. The choice engine should follow the same chain: personality adjustment -> NPC state changes -> flag setting -> conversation log.
- **Risk if wrong:** Tight coupling between narrative and dialogue/state modules. Acceptable — they share the same GameState and the same effect semantics. If effect semantics diverge later, extract to a shared module.

### D3: Scene JSON Location — fixtures/ vs narrative/
- **Options:**
  - **(A) `src/fixtures/scenes/`** — alongside existing `encounter.json`.
  - **(B) `src/narrative/scenes/`** — co-located with engine code.
  - **(C) `src/data/scenes/`** — dedicated data directory.
- **Recommendation:** Option A — `src/fixtures/scenes/`. Existing pattern: `encounter.json` lives in `src/fixtures/`, NPC templates in `src/state/npcs.ts` (typed), archetype profiles in `src/combat/behaviorTree/profiles/`. The fixtures directory is established as the home for authored data files. Scene JSON is authored data, not engine logic.
- **Risk if wrong:** Organizational, not functional. Can move files later with import path updates. Low impact.

### D4: NarrativeState Result Type — Bundled vs Separate
- **Options:**
  - **(A) Bundled result** — scene transitions return `{ state: GameState; nextScene: Scene | null; selectedChoice: SceneChoice }` mirroring `DialogueResult`.
  - **(B) State-only return** — transitions return just `GameState`, caller looks up scene separately.
  - **(C) Discriminated union** — `NarrativeTransitionResult = { type: 'success', state, nextScene } | { type: 'error', code, message }`.
- **Recommendation:** Option C — discriminated union. The plan requires typed errors for invalid transitions ("Attempting an invalid transition returns a typed error"). A discriminated union cleanly separates success from failure without exceptions. The API layer can map `'error'` variants to HTTP error responses. This is more explicit than the dialogue engine's approach (which throws or returns undefined).
- **Risk if wrong:** More verbose than Option A. But the plan explicitly calls for typed errors on invalid transitions, so Option A is insufficient.

### D5: initCombatState Synergy — Parameter Injection vs GameState Read
- **Options:**
  - **(A) Read from GameState** — `initCombatState` reads player personality + NPC personalities from the `_gameState` parameter (currently unused), calls `calculateSynergy()` internally.
  - **(B) Synergy as parameter** — caller pre-computes synergy and passes it: `initCombatState(gameState, encounter, synergyBonus?)`.
  - **(C) Post-processing** — caller gets CombatState, then applies synergy externally.
- **Recommendation:** Option A — read from GameState. The `_gameState` parameter already exists as a placeholder for exactly this kind of future extension. Using it fulfills the original design intent. The function already imports from `types/index.js`. Keeps the synergy calculation encapsulated within combat initialization — the caller doesn't need to know about synergy mechanics.
- **Risk if wrong:** Couples `initCombatState` to narrative/synergy knowledge. But the R4 finding explicitly identifies this as the intended integration point, and the design spec confirms "Integration: initCombatState calls calculateSynergy."

### D6: Narrative Error Codes — Extend Existing vs Separate
- **Options:**
  - **(A) Extend ErrorCodes** — add narrative codes (SCENE_NOT_FOUND, CHOICE_NOT_AVAILABLE, NARRATIVE_NOT_STARTED, etc.) to the existing const object in `types/index.ts`.
  - **(B) Separate NarrativeErrorCodes** — new const object in `types/narrative.ts`.
- **Recommendation:** Option A — extend existing ErrorCodes. The const object pattern is already established. API error responses use `ApiError.code` which references these constants. Adding narrative codes keeps a single source of truth for all error codes across the application. The pattern is additive (won't break existing code).
- **Risk if wrong:** ErrorCodes object grows large over time. Minimal risk — it's a flat const object, not a complex data structure.

---

## Risks Identified

### R1: GameState Extension Breaks Sprint 1+2 Compilation (Impact: High, Likelihood: Low)
Adding `narrativeState: NarrativeState | null` to GameState means every place that constructs a GameState must include the field. This includes `createNewGameState()`, test fixtures that create full GameState objects, and `processDialogueChoice` return types.
- **Mitigation:** Make field nullable and add it to `createNewGameState()` as `null`. Existing test fixtures using `Partial<GameState>` or spread patterns should work. Run full Sprint 1+2 test suite immediately after type changes.

### R2: Synergy Calculator Needs Party Composition Data Not Currently in GameState (Impact: Medium, Likelihood: Medium)
`calculateSynergy()` needs "party NPC personalities" — but GameState has `npcs: Record<string, NPC>` with ALL NPCs, not just current party members. There's no party composition field. `initCombatState` gets an `EncounterConfig` which has combatant configs but not personality data.
- **Mitigation:** Two options: (a) assume all 3 NPCs are always in the party for Sprint 3 (simplest, matches the 3-person party constraint), or (b) add a `partyNpcIds: string[]` field to NarrativeState. Recommend (a) for Sprint 3 since the plan says "3-person party, 3 NPCs" — there's no party selection mechanic yet.

### R3: Narrative State Machine Complexity Creep (Impact: Medium, Likelihood: Medium)
T4 (state machine) integrates T2 (scene engine) + T3 (choice engine) into a single orchestrator. Risk of becoming a "god module" that handles too many concerns.
- **Mitigation:** Keep the state machine thin — it delegates to scene engine for prerequisite evaluation and choice engine for effect processing. State machine only manages transitions between scenes and NarrativeState bookkeeping (currentScene, visitedScenes, flags).

### R4: Save File Backward Compatibility Without Schema Versioning (Impact: Medium, Likelihood: Low)
No schema versioning exists. `validateGameState()` does structural checks. Adding `narrativeState` to validation could reject old saves if not handled carefully.
- **Mitigation:** T6 explicitly handles this. `narrativeState` is nullable — validation should accept its absence. Test with pre-Sprint-3 save files. Consider adding a schema version field as a future-proofing measure (document as tech debt, don't block sprint).

### R5: Scene Prerequisite Evaluation Performance at Scale (Impact: Low, Likelihood: Low)
Array-based visited scenes and flat prerequisite evaluation are O(n). Not a concern for 2-3 scenes but could matter at 50+ scenes.
- **Mitigation:** Accept for Sprint 3. Document as a known scaling consideration. If Act 1 exceeds ~30 scenes, convert `visitedSceneIds` to a Set at runtime (deserialize array -> Set for engine use, Set -> array for persistence).

### R6: initCombatState Signature Change Could Break Sprint 2 Tests (Impact: Medium, Likelihood: Low)
If `initCombatState` starts reading from the previously-unused `_gameState` parameter, tests that pass a minimal/null gameState could fail.
- **Mitigation:** Synergy calculation should be guarded: if `_gameState` is null or has no NPCs, skip synergy (return CombatState as-is). Existing tests pass mock EncounterConfig but may pass null/minimal gameState — verify before modifying.

---

## Recommended Approach

### Architecture Summary

The Sprint 3 narrative system follows the established codebase conventions precisely:

1. **Type separation:** `types/narrative.ts` as a standalone leaf (mirroring `types/combat.ts`), imported by `types/index.ts` to extend GameState with `narrativeState: NarrativeState | null`.

2. **Module structure:** Three narrative engine files in `src/narrative/`:
   - `sceneEngine.ts` — scene loading, prerequisite evaluation, available choice filtering, dead-end validation
   - `choiceEngine.ts` — choice validation, effect application (reusing existing state updaters), flag setting
   - `narrativeStateMachine.ts` — thin orchestrator managing scene transitions, NarrativeState bookkeeping

3. **State updater integration:** Extend `stateUpdaters.ts` with narrative-specific updaters following the canonical `(state: Readonly<GameState>, ...) => GameState` + `updateTimestamp()` pattern.

4. **Synergy subsystem:** Pure calculator in `narrative/synergyCalculator.ts` with config in `fixtures/synergyConfig.ts`. Wired into `combat/sync.ts` via the existing unused `_gameState` parameter in `initCombatState`.

5. **Persistence:** Nullable `narrativeState` field enables backward compatibility with zero migration code. `validateGameState()` updated to accept presence or absence of the field.

6. **API:** New `narrativePlugin` in `api/narrative.ts` registered alongside existing 5 plugins. Follows Fastify plugin pattern with JSON Schema validation and ApiResponse<T> envelopes.

### Implementation Sequence

**Phase 1 — Foundation (T1):**
Define all narrative + synergy types in `types/narrative.ts`. Extend GameState. Extend ErrorCodes. Verify Sprint 1+2 compilation.

**Phase 2a — Narrative Engine (T2 -> T3 -> T4, sequential):**
Scene engine first (graph loading, prerequisites, dead-end validation). Then choice engine (validation, effects, flags). Then state machine (thin orchestrator integrating both).

**Phase 2b — Synergy (T7, parallel with 2a):**
Pure calculator implementation per approved design spec. Independent of narrative engine. Can proceed as soon as T1 types exist.

**Phase 3 — Integration Layer (T5 + T6 + T8, after Phase 2):**
State updaters for narrative ops (T5). Persistence validation (T6). Combat synergy wiring (T8).

**Phase 4 — API (T9, after Phase 3):**
Narrative REST endpoints. Depends on state updaters being stable.

**Phase 5 — Validation (T12, final):**
End-to-end integration tests spanning narrative -> combat -> persistence.

### Key Patterns to Follow

- **Graph traversal:** Mirror dialogue engine's stateless-parameter approach — scene graphs passed to functions, never stored on GameState.
- **Effect processing:** Reuse `processDialogueChoice` chaining pattern — personality -> NPC state -> flags -> conversation log.
- **Error handling:** Discriminated union results for state machine transitions. Silent return for state updaters on null narrative state. Structured ApiError for API layer.
- **Testing:** Co-located `.test.ts` files. Two-tier fixtures (shared + local factory). Reference inequality checks for immutability. Dead-end validation on test graphs.
- **JSON compatibility:** Arrays instead of Sets. Record<string, boolean> for flags. No circular references.
