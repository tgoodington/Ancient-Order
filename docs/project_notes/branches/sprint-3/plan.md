# Plan: Ancient Order Sprint 3 — Narrative & State Machine

**Plan-Execute Contract v1.0 | Tier: Standard**
**Date:** 2026-02-23

---

## 1. Objective

Build the narrative progression layer for Act 1: a scene graph engine with personality-gated choices, a flag-based consequence system, team synergy bonuses applied to combat, and a REST API for all narrative operations. This connects Sprint 1 (personality) and Sprint 2 (combat) into a cohesive story experience with 2-3 functional starter scenes proving the system end-to-end.

**Success criteria:**
- Scene graph traversal works with prerequisite evaluation (trait gates, choice flags)
- Player choices set flags that affect future scene availability and dialogue variants
- Team synergy bonuses apply to combat initialization through the existing sync layer
- Narrative state persists through save/load without breaking Sprint 1+2 data
- All narrative operations accessible via REST API
- 2-3 starter scenes demonstrate a playable Act 1 narrative arc

---

## 2. Discovery Summary

- **Problem:** Sprint 1+2 systems (personality, combat) operate in isolation. The backend needs narrative scaffolding to create a unified game experience for the investor demo.
- **Goals:** Act 1 scene progression, choice consequence tracking, team synergy bonuses, persistence integration, narrative REST API.
- **Target users:** Solo developer building toward investor/publisher pitch.
- **Constraints:** TypeScript + Fastify (inherited), immutable state, JSON persistence, 3-person party, 3 NPCs (Elena/Lars/Kade), no dead ends, static JSON scene definitions.
- **Key findings:** Existing codebase has 24 test files, layered plugin architecture, pure-function state transitions, utility-scoring combat AI. GameState needs extension for narrative fields. Synergy system is distinct from GROUP action's 1.5x multiplier.

---

## 2.5. Parent Context

**Parent:** trunk (Sprint 1 + Sprint 2)
**Parent Objective:** Rebuild backend with personality system and combat engine for investor pitch demo.

**Shared Components:**
- `src/types/index.ts`: Sprint 3 extends GameState with narrative fields (scene state, choice history, synergy bonuses)
- `src/state/stateUpdaters.ts`: Sprint 3 adds narrative updaters following the existing pure-function pattern
- `src/persistence/saveLoad.ts`: Narrative state must serialize through existing JSON save/load
- `src/combat/sync.ts`: Synergy bonuses injected via `initCombatState` at combat start
- `src/api/`: New narrative plugin follows established Fastify plugin pattern

**Inherited Decisions:**
- Fastify + Vitest + TypeScript (Locked)
- Immutable state via spread + Readonly (ADR-012)
- CombatState independent from GameState, synced at boundary (ADR-013)
- JSON persistence in saves/, 10 slots (ADR-005)
- REST API (ADR-006)
- Roll injection pattern: `rollFn` parameter for testability
- Fastify session state: `GameStateContainer` is decorated onto the Fastify instance via `fastify.decorate`. All plugins access session state via `fastify.gameState`. Sprint 3's narrative plugin follows this same established pattern. (Was an open question in parent plan — confirmed resolved in Sprint 1+2 implementation.)

**Intersection Points:**
- `types/index.ts` — additive extension, must not break Sprint 1+2 type contracts
- `combat/sync.ts` — synergy bonuses feed into `initCombatState`
- `persistence/saveLoad.ts` — narrative state must be JSON-serializable

**Divergence:**
- Sprint 3 introduces graph traversal (scene graph) as a new architectural pattern not present in Sprint 1+2
- Scene content is authored data (JSON fixtures) rather than computed logic

---

## 3. Technology Decisions

| Decision | Choice | Status | Rationale |
|----------|--------|--------|-----------|
| Scene Architecture | Directed graph with prerequisites | Locked | JSON nodes with IDs, content, choices (personality-gated), prerequisites (trait checks, choice flags). Mirrors dialogue engine's graph traversal pattern. |
| Consequence Model | Local effects + named flags | Locked | Choices have immediate effects (personality shift, NPC state change) plus set flags in a choice history map. Future scenes check flags as prerequisites. 1-hop, multiple readers. |
| Synergy-Combat Boundary | Synergy applied at initCombatState | Locked | Pre-combat party composition bonuses via sync.ts. Distinct from GROUP action's 1.5x resolution-time multiplier. ADR-013 defines this boundary. |
| Synergy Calculation | Deferred to design | DESIGN REQUIRED | Formula, pair vs team model, stat bonus types need game design exploration. |
| Branching Strategy | Deferred to design | DESIGN REQUIRED | Linear spine + variants vs true branching vs minimal. Affects content volume and test paths. |
| Content Scope | Engine + 2-3 starter scenes | Locked | Full engine with functional test scenes proving the narrative arc. Complete Act 1 content can expand later. |
| Scene Storage | Static JSON fixtures | Locked (inherited) | Scene definitions are data files, not generated. Matches existing pattern (encounter.json, NPC templates). |

---

## 6. Task Sequence

### Task 1: Narrative Type System
- **Component**: `types/`
- **Description**: Extend the type system with all interfaces for narrative operations: scene graph nodes, choice definitions, consequence flags, narrative state (current scene, visited scenes, choice history), synergy bonus types. GameState gains narrative fields without breaking Sprint 1+2 contracts.
- **Acceptance Criteria**:
  1. All narrative interfaces defined: `Scene`, `SceneChoice`, `ScenePrerequisite`, `ChoiceFlag`, `NarrativeState`, `SynergyBonus`, and any supporting types
  2. `GameState` extended with `narrativeState: NarrativeState | null` — Sprint 1+2 code compiles without changes
  3. TypeScript strict mode compilation succeeds with all new types imported
  4. No circular imports between narrative types and existing Sprint 1+2 types
- **Dependencies**: None
- **Files**: `src/types/index.ts` (extended), possibly `src/types/narrative.ts` if separation is cleaner

---

### Task 2: Scene Graph Engine
- **Component**: `narrative/` (new)
- **Description**: Implement scene graph loading and traversal. Scenes are JSON nodes in a directed graph. Each node has an ID, text content, available choices, and prerequisite conditions. The engine evaluates prerequisites (personality trait checks, choice flag checks) to determine which scenes and choices are available. Must enforce the "no dead ends" constraint: every reachable scene has at least one choice with no prerequisites or met prerequisites.
- **Acceptance Criteria**:
  1. Scene graph loads from JSON data and is traversable by scene ID
  2. Prerequisite evaluation correctly gates scenes based on personality trait thresholds and choice flag presence
  3. Available choices for a scene are filtered by personality gates — ungated fallback always exists
  4. Dead-end detection: a validation function identifies any scene reachable through normal traversal where all choices are gated and none are satisfiable
  5. Unit tests cover: prerequisite pass/fail, personality gate filtering, dead-end validation on a test graph
- **Dependencies**: Task 1
- **Files**: `src/narrative/sceneEngine.ts`, test fixtures (JSON scene data), associated test file

---

### Task 3: Choice & Consequence Engine
- **Component**: `narrative/`
- **Description**: Implement the choice processing system. When a player selects a choice, the engine: (1) validates the choice is available given current state, (2) applies immediate effects (personality adjustments, NPC state changes), (3) sets named flags in the choice history map. The flag map is the mechanism for cross-scene consequences — future scene prerequisites read these flags. NPC state effects must reuse the existing `updateNPCAffection`, `updateNPCTrust`, and `updateNPCRelationship` updaters — do not invent parallel NPC state mechanisms. Reference `processDialogueChoice` in `stateUpdaters.ts` as the existing pattern for choice-driven state changes.
- **Acceptance Criteria**:
  1. Choice selection validates against current game state (choice must be available, not gated)
  2. Immediate effects apply correctly: personality adjustments use existing personality system, NPC state changes use the existing `updateNPCAffection`/`updateNPCTrust`/`updateNPCRelationship` updaters
  3. Named flags are set in the choice history map after processing
  4. Flag map is a simple string→boolean (or string→value) map that serializes to JSON
  5. Unit tests cover: valid choice processing, gated choice rejection, flag setting, personality effect application
- **Dependencies**: Task 1, Task 2
- **Files**: `src/narrative/choiceEngine.ts`, associated test file

---

### Task 4: Narrative State Machine
- **Component**: `narrative/`
- **Description**: Implement the narrative state management that integrates scene graph traversal and choice processing into a coherent state machine. Tracks: current scene ID, set of visited scene IDs, choice flag map, and provides transition functions that advance the player through the scene graph. All transitions are pure functions returning new NarrativeState.
- **Acceptance Criteria**:
  1. State machine correctly tracks current scene, visited scenes, and choice flags across multiple transitions
  2. Scene transitions validate that the target scene's prerequisites are met before advancing
  3. Attempting an invalid transition (unmet prerequisites, non-existent scene) returns a typed error
  4. All transition functions are pure: same NarrativeState + action → same result
  5. Unit tests cover: multi-step traversal through a test graph, invalid transition handling, state accumulation across transitions
- **Dependencies**: Task 2, Task 3
- **Files**: `src/narrative/narrativeStateMachine.ts`, associated test file

---

### Task 5: Narrative State Updaters
- **Component**: `state/`
- **Description**: Extend the state updaters library with functions for narrative operations. Each updater follows the existing pattern: `(state: GameState, ...) => GameState`. Updaters needed: start narrative (initialize NarrativeState), advance scene, process choice, set/clear flags. These bridge the narrative module into the existing immutable state management system.
- **Acceptance Criteria**:
  1. All narrative updaters return new GameState objects without mutating input
  2. Narrative updaters integrate correctly with existing updaters (e.g., a choice that adjusts personality uses both narrative and personality updaters)
  3. Starting a narrative initializes NarrativeState within GameState
  4. Unit tests verify reference inequality and value correctness for each updater
- **Dependencies**: Task 4
- **Files**: `src/state/stateUpdaters.ts` (extended), associated test updates

---

### Task 6: Persistence Integration
- **Component**: `persistence/`
- **Description**: Ensure the existing save/load system correctly handles GameState with narrative fields. NarrativeState (current scene, visited set, choice flags) must round-trip through JSON serialization without data loss. This may require no code changes if NarrativeState is already JSON-serializable, but must be explicitly validated.
- **Acceptance Criteria**:
  1. Save/load round-trip with active NarrativeState produces deeply equal state
  2. Loading a Sprint 1+2 save (no narrative fields) doesn't crash — graceful handling of missing NarrativeState
  3. Choice flag map serializes and deserializes correctly
  4. Visited scene set serializes and deserializes correctly
  5. Unit tests cover: round-trip with narrative state, backward compatibility with pre-narrative saves
- **Dependencies**: Task 5
- **Files**: `src/persistence/saveLoad.ts` (may need updates), associated test updates

---

### Task 7: Team Synergy Calculator *(DESIGN REQUIRED)*
- **Component**: `narrative/` or `combat/` (TBD by design)
- **Description**: Implement the team synergy bonus calculation system. Given a party composition, compute stat bonuses based on personality interactions between party members. The formula, pair vs team model, bonus types, and integration pattern must come from the design spec. Synergy bonuses are computed pre-combat and applied through `initCombatState`.
- **Acceptance Criteria** *(to be refined after design — minimum requirements)*:
  1. Synergy bonuses are computed correctly for the 3-member party
  2. Bonuses are stat-typed (e.g., ATK +15%, DEF +10%) and expressible as percentage modifiers
  3. Calculation is a pure function: same party composition → same bonuses
  4. Bonuses are data-driven (configuration, not hardcoded per-character if-statements)
  5. Unit tests verify correct bonus computation for known party compositions
- **Dependencies**: Task 1 + Team Synergy System design spec
- **Files**: TBD by design spec

---

### Task 8: Combat Synergy Integration
- **Component**: `combat/sync.ts`
- **Description**: Wire team synergy bonuses into combat initialization. When `initCombatState` creates a CombatState from GameState and EncounterConfig, it reads synergy bonuses from the narrative/synergy system and applies them as stat modifiers to the player party's combatants. This is the bridge between Sprint 3's synergy calculation and Sprint 2's combat engine.
- **Acceptance Criteria**:
  1. `initCombatState` applies synergy bonuses to player party combatant stats
  2. Synergy bonuses modify the correct stats (matching the synergy calculator's output types)
  3. Combat without synergy bonuses (no narrative state, or no synergy defined) works identically to current behavior — no regressions
  4. Unit tests cover: combat init with synergy, combat init without synergy (backward compatibility)
- **Dependencies**: Task 7
- **Files**: `src/combat/sync.ts` (extended), associated test updates

---

### Task 9: Narrative REST API
- **Component**: `api/`
- **Description**: Implement a Fastify narrative plugin exposing all narrative operations through REST endpoints. Endpoints needed: get current scene (with available choices), submit choice, get narrative state, start/reset narrative, get synergy bonuses for current party. Follows the existing Fastify plugin pattern with JSON Schema validation and ApiResponse<T> envelopes.
- **Acceptance Criteria**:
  1. All narrative endpoints respond with correct status codes and ApiResponse<T> envelopes
  2. Get current scene returns scene content with only available (non-gated) choices
  3. Choice submission validates and processes the choice, returning updated state
  4. Invalid requests return structured ApiError responses, not unhandled exceptions
  5. Plugin registers correctly alongside existing game/player/npc/dialogue/combat plugins
- **Dependencies**: Task 5, Task 6
- **Files**: `src/api/narrative.ts` (new plugin), `src/api/index.ts` (updated registration)

---

### Task 10: Act 1 Starter Scenes *(DESIGN REQUIRED)*
- **Component**: `src/fixtures/` or `src/narrative/scenes/`
- **Description**: Author 2-3 functional Act 1 scenes as JSON scene graph data. Scenes must include: narrative text, player choices with personality gates, consequence flags, and prerequisite conditions demonstrating cross-scene flag propagation. Scene content, branching model, and narrative arc must come from the design spec (which covers both branching strategy and scene content).
- **Acceptance Criteria** *(to be refined after design — minimum requirements)*:
  1. 2-3 scenes form a playable narrative sequence with at least one branching point
  2. At least one choice is personality-gated with an ungated fallback
  3. At least one scene has a prerequisite checking a flag set by a prior scene's choice
  4. All scenes pass dead-end validation (no unreachable dead ends)
  5. Scene data is valid JSON loadable by the scene graph engine
- **Dependencies**: Task 2, Task 3 + Act 1 Narrative Design spec
- **Files**: Scene JSON files (location TBD by design/engineering)

---

### Task 11: Integration Validation
- **Component**: Cross-system
- **Description**: End-to-end validation that all Sprint 3 systems work together through the API. Test the complete flow: start narrative → traverse scenes → make choices → observe flag propagation → compute synergy → initiate combat with synergy bonuses applied → save/load mid-narrative.
- **Acceptance Criteria**:
  1. Complete narrative flow via API: start → scene traversal → choice → flag-gated scene unlock → choice consequences visible
  2. Synergy bonuses computed from party composition are reflected in combat initialization
  3. Save/load during active narrative preserves all state (scene position, flags, visited scenes)
  4. Backward compatibility: Sprint 1+2 API operations work unchanged
  5. No unhandled promise rejections across all integration test scenarios
- **Dependencies**: Task 8, Task 9, Task 10
- **Files**: Integration test file(s) in `src/` or `tests/`

---

## 6.5 Design Recommendations

| Task(s) | Item Name | Recommendation | Rationale |
|---------|-----------|----------------|-----------|
| Task 7, 8 | Team Synergy System | **DESIGN REQUIRED** | Formula undefined. Pair-based vs team composition, stat bonus types, player transparency, and balance all need game design exploration. Clear boundary with GROUP action synergy (combat-time 1.5x is separate). |
| Task 10 | Act 1 Narrative Design | **DESIGN REQUIRED** | Branching strategy (linear spine + variants vs true branching) determines scene content structure. Scene pacing, choice meaning, personality gate placement, and flag design are creative decisions requiring user input. |
| Task 1 | Narrative Type System | Ready for execution | Additive type extension following established patterns. No design decisions — engineering determines field names and structure. |
| Tasks 2, 3, 4 | Scene & Choice Engine | Ready for execution | Architecture decided (directed graph + flag-based consequences). Implementation is mechanical given the scene graph and consequence model decisions. |
| Task 5 | Narrative State Updaters | Ready for execution | Follows established pure-function pattern in stateUpdaters.ts. No novel design. |
| Task 6 | Persistence Integration | Ready for execution | Validation and possible extension of existing save/load. Narrative state is JSON-serializable by design. |
| Task 9 | Narrative REST API | Ready for execution | Follows established Fastify plugin pattern. Endpoint design derives from the narrative operations. |
| Task 11 | Integration Validation | Ready for execution | Test scenarios derive from the implemented systems. No novel design. |

**Design sessions must complete before Task 7 and Task 10 begin.** Both design sessions can run concurrently with Tasks 1-6 implementation (they touch different surfaces). Task 8 (combat synergy integration) depends on Task 7's design being resolved.

---

## 7. Testing Strategy

**Framework:** Vitest (inherited, co-located test files)

**Test types required:**

| Type | What | Which Tasks |
|------|------|-------------|
| Unit | Scene graph traversal, prerequisite evaluation, choice processing, flag system, narrative state transitions, synergy calculation | Tasks 2, 3, 4, 5, 7 |
| Integration | Full API request flows, save/load with narrative state, cross-system narrative + combat | Tasks 6, 9, 11 |
| Validation | Dead-end detection on scene graphs, backward compatibility with pre-narrative saves | Tasks 2, 6 |
| TDD (synergy) | Known party composition → expected bonus values once formula is designed | Task 7 |

**Critical scenarios:**
- No dead ends: every scene reachable through normal traversal has at least one available choice (Task 2)
- Flag propagation: choice in Scene A sets flag → Scene C prerequisite checks that flag (Tasks 3, 4)
- Backward compatibility: loading a Sprint 1+2 save (no NarrativeState) doesn't crash (Task 6)
- Synergy-combat bridge: synergy bonuses correctly modify combatant stats in initCombatState (Task 8)
- Full narrative arc via API: start → traverse → choose → observe consequences (Task 11)

**Test file convention:** Co-located (`.test.ts` next to source), matching Sprint 1+2 pattern.

---

## 8. Risks & Mitigations

| Risk | Likelihood | Impact | Mitigation |
|------|-----------|--------|------------|
| GameState type extension breaks Sprint 1+2 code | Low | High | NarrativeState is nullable (`NarrativeState \| null`). Existing code never reads it. Run full Sprint 1+2 test suite after type changes. |
| Synergy design reveals scope larger than one task | Medium | Medium | Design session bounds the POC: simple formula, limited bonus types. Complex synergy deferred to future sprint. |
| Scene content authoring takes longer than engine code | Medium | Medium | Start with 2 scenes minimum. Third scene is stretch. Engine is proven with test fixtures regardless. |
| Branching strategy design produces more content than fits in sprint | Medium | Medium | Linear spine + variants is the recommended starting point in design — caps content volume. |
| Narrative state makes save files backward-incompatible | Low | Medium | Task 6 explicitly tests loading pre-narrative saves. NarrativeState defaults to null if missing. |
| Visited scene set implemented as JS Set — fails JSON serialization | Low | Medium | NarrativeState constraint: no Sets, Maps, or circular references. Visited scenes must be stored as an array or object, not a `Set`. Serialization constraint is confirmed — apply at type definition time (Task 1). |
| Scene graph engine duplicates dialogue engine patterns | Low | Low | Intentional parallel — both are graph traversal with gates. `src/dialogue/dialogueEngine.ts` is the reference. Consider shared utilities if overlap is significant (engineering decision). |

---

## 10. Planning Context for Engineer

**Sequencing Considerations:**
- Tasks 1-4 form a dependency chain (types → engine → choices → state machine). Task 5 extends state updaters after the state machine is stable.
- Task 6 (persistence) can proceed once Task 5 is done — it's validation work, possibly requiring minimal code changes.
- Task 9 (API) depends on Tasks 5 and 6 but is independent of synergy work.
- Tasks 7-8 (synergy) are blocked on design but independent of Tasks 2-6. Once design completes, they can be built in parallel with or after the engine tasks.
- Task 10 (scene content) is blocked on narrative design but the scene engine (Task 2) can be tested with fixture data.

**Parallelization Opportunities:**
- Design sessions (synergy + narrative) can run concurrently with Tasks 1-6 implementation
- Task 6 (persistence) and Task 9 (API) touch independent surfaces once Task 5 is complete
- Tasks 7-8 (synergy) and Task 10 (scenes) are independent of each other after their respective design specs

**Engineering Questions:**
- Should narrative types live in `types/index.ts` (extending the existing file) or `types/narrative.ts` (new file importing from index)? Sprint 2 used a separate `types/combat.ts` — same pattern may apply.
- How much overlap exists between the scene graph engine and the existing dialogue engine? `src/dialogue/dialogueEngine.ts` and `src/dialogue/fixtures.ts` are direct reference patterns — both use personality-gated graph traversal. Should they share graph traversal utilities or remain independent?
- Where does the synergy calculator live: `narrative/` (it's a narrative-layer concept) or `combat/` (it's consumed by combat)? Design spec may inform this.
- Scene JSON files: `src/fixtures/scenes/` (alongside encounter.json) or `src/narrative/scenes/` (co-located with engine)? Reference: `src/dialogue/fixtures.ts` shows the existing fixture co-location pattern.
- The choice consequence engine should reuse `updateNPCAffection`, `updateNPCTrust`, `updateNPCRelationship`, and `processDialogueChoice` from `stateUpdaters.ts` — engineer should read these before designing the consequence updater API to avoid duplication or inconsistency.

**Constraints:**
- All narrative state transitions must produce new objects (ADR-012)
- NarrativeState must be JSON-serializable — no Sets, Maps, or circular references
- Scene prerequisites must always have an ungated fallback path (no dead ends constraint)
- Synergy bonuses integrate through `initCombatState` in sync.ts, not by modifying CombatState directly (ADR-013)
- Sprint 1+2 test suite must remain green after all Sprint 3 changes

**Risk Context:**
- The synergy system is the highest design risk — formula undefined, integration point is sensitive (combat init). Keep the initial implementation simple and bounded.
- Scene content authoring is the highest effort risk — real narrative text is slower to produce than code. The 2-3 scene target is intentionally conservative.
- GameState extension is the highest regression risk — changes to the root type affect every module. The additive nullable field approach minimizes blast radius.
