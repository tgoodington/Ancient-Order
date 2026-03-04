# Plan: Ancient Order Sprint 3 — Narrative & State Machine

**Plan-Execute Contract v1.0 | Tier: Standard | Mode: v9 (Domain Specialist)**
**Date:** 2026-03-01 (revised from 2026-02-23 v8 plan)

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
- Fastify session state: `GameStateContainer` is decorated onto the Fastify instance via `fastify.decorate`. All plugins access session state via `fastify.gameState`. Sprint 3's narrative plugin follows this same established pattern.

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
| Synergy Calculation | Paradigm-based model with highest-only selection | Locked | Two paradigms (Well Rounded, Bond), binary threshold activation, pure calculator function, data-driven config. Design spec approved 2026-02-24. |
| Branching Strategy | Deferred to detail phase | DETAIL REQUIRED | Linear spine + variants vs true branching vs minimal. Affects content volume and test paths. Explored via Task 10 (Deep). |
| Content Scope | Engine + 2-3 starter scenes | Locked (inherited) | Full engine with functional test scenes proving the narrative arc. Complete Act 1 content can expand later. |
| Scene Storage | Static JSON fixtures | Locked (inherited) | Scene definitions are data files, not generated. Matches existing pattern (encounter.json, NPC templates). |

---

## 6. Task Sequence

### Task 1: Narrative Type System
- **Domain**: code/backend
- **Depth**: Light
- **Component**: `types/`
- **Description**: Extend the type system with all interfaces for narrative operations: scene graph nodes, choice definitions, consequence flags, narrative state (current scene, visited scenes, choice history), synergy bonus types. GameState gains narrative fields without breaking Sprint 1+2 contracts.
- **Acceptance Criteria**:
  1. All narrative interfaces defined: `Scene`, `SceneChoice`, `ScenePrerequisite`, `ChoiceFlag`, `NarrativeState`, `SynergyBonus`, `SynergyResult`, `ParadigmConfig`, and any supporting types
  2. `GameState` extended with `narrativeState: NarrativeState | null` — Sprint 1+2 code compiles without changes
  3. TypeScript strict mode compilation succeeds with all new types imported
  4. No circular imports between narrative types and existing Sprint 1+2 types
- **Dependencies**: None
- **Files**: `src/types/index.ts` (extended), possibly `src/types/narrative.ts` if separation is cleaner

---

### Task 2: Scene Graph Engine
- **Domain**: code/backend
- **Depth**: Standard
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
- **Domain**: code/backend
- **Depth**: Standard
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
- **Domain**: code/backend
- **Depth**: Standard
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
- **Domain**: code/backend
- **Depth**: Light
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
- **Domain**: code/backend
- **Depth**: Light
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

### Task 7: Team Synergy Calculator
- **Domain**: code/backend
- **Depth**: Light
- **Component**: `narrative/`
- **Description**: Implement the team synergy bonus calculation system per the approved design spec. Paradigm-based model: two paradigms (Well Rounded, Bond), binary threshold activation, highest-only selection, pure calculator function with data-driven config. Well Rounded checks that every trait has a party member ≥25%. Bond checks player alignment ≥80% with an NPC's dominant traits. The best-matching satisfied paradigm applies its bonus (power ×1.10 or speed ×1.10) to all player combatants at combat init.
- **Acceptance Criteria**:
  1. `calculateSynergy(playerPersonality, partyNpcPersonalities, paradigms)` returns `SynergyResult` (SynergyBonus | null)
  2. Well Rounded paradigm: fires when max trait value across all party members ≥25 for each of the 6 traits; matchQuality = sum(maxTraitValues) / (6 × threshold)
  3. Bond paradigm: fires when player's sum of an NPC's top-2 traits / NPC's own sum of top-2 traits ≥0.80; matchQuality = bestAlignmentRatio
  4. Highest-only selection: when both paradigms satisfied, highest matchQuality wins; Well Rounded wins ties
  5. Division-by-zero guard: if NPC dominant trait sum is 0, that NPC is skipped in Bond evaluation
  6. Default paradigm config lives in `src/fixtures/synergyConfig.ts`, overridable for tests
  7. TDD: tests cover Well Rounded pass/fail, Bond pass/fail, both satisfied (higher wins), no synergy, default personality (no bonus)
- **Dependencies**: Task 1
- **Files**: `src/narrative/synergyCalculator.ts`, `src/fixtures/synergyConfig.ts`, associated test file
- **Reference**: Approved design spec (git: `docs/project_notes/branches/sprint-3/design_spec_team_synergy_system.md`)

---

### Task 8: Combat Synergy Integration
- **Domain**: code/backend
- **Depth**: Light
- **Component**: `combat/sync.ts`
- **Description**: Wire team synergy bonuses into combat initialization. When `initCombatState` creates a CombatState from GameState and EncounterConfig, it calls `calculateSynergy()` with the player personality, party NPC personalities, and default paradigm config. If a synergy bonus is returned, apply the stat multiplier (power or speed) to all player party combatants via `Math.round(stat × multiplier)`. Enemy party is unaffected.
- **Acceptance Criteria**:
  1. `initCombatState` calls `calculateSynergy()` and applies non-null bonus to player party combatant stats
  2. Synergy bonuses modify the correct stat (`power` or `speed`) with `Math.round()` rounding
  3. Combat without synergy bonuses (no narrative state, or no paradigm satisfied) works identically to current behavior — no regressions
  4. Unit tests cover: combat init with synergy bonus applied, combat init without synergy (backward compatibility)
- **Dependencies**: Task 7
- **Files**: `src/combat/sync.ts` (extended), associated test updates

---

### Task 9: Narrative REST API
- **Domain**: code/backend
- **Depth**: Standard
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

### Task 10: Act 1 Narrative Design
- **Domain**: narrative/game-design
- **Depth**: Deep
- **Component**: Narrative content design
- **Description**: Design the Act 1 narrative arc for the investor demo: branching strategy (linear spine + variants vs true branching vs minimal), scene scope (which 2-3 scenes from the full Act 1 blueprint), choice design (personality gate placement, narrative weight, consequence flags), and scene pacing. This is creative/game-design work that produces a narrative design document specifying scene content, choice trees, flag names, prerequisite conditions, and personality gate thresholds for each scene.
- **Acceptance Criteria**:
  1. Branching strategy selected and documented with rationale
  2. 2-3 scenes defined with narrative text outlines, choice trees, and flag specifications
  3. At least one choice is personality-gated with an ungated fallback path
  4. At least one scene has a prerequisite checking a flag set by a prior scene's choice
  5. Flag naming convention established and all flags documented
  6. Scene pacing supports investor demo flow (opening hook → choice → consequence visible)
- **Dependencies**: None (can run in parallel with Tasks 1-9)
- **Files**: Design document output (blueprint from detail phase)

---

### Task 11: Act 1 Scene JSON Authoring
- **Domain**: narrative/content
- **Depth**: Standard
- **Component**: `src/fixtures/` or `src/narrative/scenes/`
- **Description**: Author the Act 1 starter scenes as JSON scene graph data, implementing the narrative design from Task 10. Convert the design document's scene outlines, choice trees, flag specs, and prerequisites into valid JSON loadable by the scene graph engine.
- **Acceptance Criteria**:
  1. 2-3 scenes authored as JSON matching the scene graph engine's expected format
  2. All personality gates, prerequisite conditions, and consequence flags match the Task 10 design
  3. All scenes pass dead-end validation (no unreachable dead ends)
  4. Scene data is valid JSON loadable by the scene graph engine (Task 2)
  5. Unit tests validate scene data structure and dead-end freedom
- **Dependencies**: Task 2, Task 3, Task 10
- **Files**: Scene JSON files (location determined during detail phase)

---

### Task 12: Integration Validation
- **Domain**: code/backend
- **Depth**: Light
- **Component**: Cross-system
- **Description**: End-to-end validation that all Sprint 3 systems work together through the API. Test the complete flow: start narrative → traverse scenes → make choices → observe flag propagation → compute synergy → initiate combat with synergy bonuses applied → save/load mid-narrative.
- **Acceptance Criteria**:
  1. Complete narrative flow via API: start → scene traversal → choice → flag-gated scene unlock → choice consequences visible
  2. Synergy bonuses computed from party composition are reflected in combat initialization
  3. Save/load during active narrative preserves all state (scene position, flags, visited scenes)
  4. Backward compatibility: Sprint 1+2 API operations work unchanged
  5. No unhandled promise rejections across all integration test scenarios
- **Dependencies**: Task 8, Task 9, Task 11
- **Files**: Integration test file(s) in `src/` or `tests/`

---

## 6.5 Detail Assessment

| Task(s) | Domain | Depth | Rationale |
|---------|--------|-------|-----------|
| Task 1 | code/backend | Light — autonomous | Additive type extension following established patterns (Sprint 2 used types/combat.ts). No design decisions. |
| Task 2 | code/backend | Standard — confirmation needed | New module but mirrors existing dialogue engine pattern. Key decisions: graph data structure, prerequisite evaluation API. |
| Task 3 | code/backend | Standard — confirmation needed | Follows existing stateUpdaters pattern but interfaces between scene engine and state system need definition. |
| Task 4 | code/backend | Standard — confirmation needed | Integrates Tasks 2+3 into coherent state machine. Transition function API and error handling model need confirmation. |
| Task 5 | code/backend | Light — autonomous | Straightforward pattern application — extends stateUpdaters.ts following established conventions. |
| Task 6 | code/backend | Light — autonomous | Validation work, possibly requiring minimal code changes. JSON serialization constraints already defined. |
| Task 7 | code/backend | Light — autonomous | Approved design spec provides complete types, algorithms, edge cases, and test scenarios. Implementation is mechanical. |
| Task 8 | code/backend | Light — autonomous | Well-defined integration point. Design spec specifies exact `initCombatState` modification. |
| Task 9 | code/backend | Standard — confirmation needed | Follows Fastify plugin pattern but has multiple endpoints. Request/response shapes and error handling need confirmation. |
| Task 10 | narrative/game-design | Deep — design exploration required | Creative decisions: branching strategy, scene pacing, choice meaning, personality gate placement, flag design. No existing spec. |
| Task 11 | narrative/content | Standard — confirmation needed | Mechanical authoring guided by Task 10 design, but JSON structure and scene data format need confirmation against engine expectations. |
| Task 12 | code/backend | Light — autonomous | Test scenarios derive from implemented systems. No novel design. |

**Cross-domain note:** Task 10 (narrative/game-design) produces the design that Task 11 (narrative/content) implements. Tasks 1-9 and 12 are all code/backend and can be handled by a single specialist. Task 10 requires a different specialist with game design / narrative expertise.

---

## 7. Testing Strategy

**Framework:** Vitest (inherited, co-located test files)

**Test types required:**

| Type | What | Which Tasks |
|------|------|-------------|
| Unit | Scene graph traversal, prerequisite evaluation, choice processing, flag system, narrative state transitions, synergy calculation | Tasks 2, 3, 4, 5, 7 |
| Integration | Full API request flows, save/load with narrative state, cross-system narrative + combat | Tasks 6, 9, 12 |
| Validation | Dead-end detection on scene graphs, backward compatibility with pre-narrative saves | Tasks 2, 6 |
| TDD (synergy) | Known party composition → expected bonus values per design spec test scenarios | Task 7 |

**Critical scenarios:**
- No dead ends: every scene reachable through normal traversal has at least one available choice (Task 2)
- Flag propagation: choice in Scene A sets flag → Scene C prerequisite checks that flag (Tasks 3, 4)
- Backward compatibility: loading a Sprint 1+2 save (no NarrativeState) doesn't crash (Task 6)
- Synergy-combat bridge: synergy bonuses correctly modify combatant stats in initCombatState (Task 8)
- Full narrative arc via API: start → traverse → choose → observe consequences (Task 12)
- Synergy TDD targets: Well Rounded pass/fail, Bond pass/fail, both satisfied, no synergy, default personality (Task 7)

**Test file convention:** Co-located (`.test.ts` next to source), matching Sprint 1+2 pattern.

---

## 8. Risks & Mitigations

| Risk | Likelihood | Impact | Mitigation |
|------|-----------|--------|------------|
| GameState type extension breaks Sprint 1+2 code | Low | High | NarrativeState is nullable (`NarrativeState \| null`). Existing code never reads it. Run full Sprint 1+2 test suite after type changes. |
| Scene content authoring takes longer than engine code | Medium | Medium | Split into design (Task 10) and authoring (Task 11). Start with 2 scenes minimum. Engine is proven with test fixtures regardless. |
| Branching strategy design produces more content than fits in sprint | Medium | Medium | Linear spine + variants is the recommended starting point — caps content volume. Deep exploration in Task 10 resolves this before authoring begins. |
| Narrative state makes save files backward-incompatible | Low | Medium | Task 6 explicitly tests loading pre-narrative saves. NarrativeState defaults to null if missing. |
| Visited scene set implemented as JS Set — fails JSON serialization | Low | Medium | NarrativeState constraint: no Sets, Maps, or circular references. Visited scenes stored as array or object. Enforced at type definition time (Task 1). |
| Scene graph engine duplicates dialogue engine patterns | Low | Low | Intentional parallel — both are graph traversal with gates. Consider shared utilities if overlap is significant (detail phase decision). |
| Narrative design (Task 10) delays engine work | Low | Medium | Task 10 has no dependencies on engine tasks. Can run in parallel with Tasks 1-9. Only Task 11 is blocked on it. |

---

## 10. Planning Context for Detail Phase

### Domain-Specific Considerations

**code/backend (Tasks 1-9, 12):**
- All state transitions must produce new objects (ADR-012)
- NarrativeState must be JSON-serializable — no Sets, Maps, or circular references
- Scene prerequisites must always have an ungated fallback path (no dead ends constraint)
- Synergy bonuses integrate through `initCombatState` in sync.ts, not by modifying CombatState directly (ADR-013)
- Sprint 1+2 test suite must remain green after all Sprint 3 changes
- Reference `src/dialogue/dialogueEngine.ts` as the pattern for graph traversal with personality gates
- Reference `src/state/stateUpdaters.ts` for the existing pure-function state transition pattern
- Synergy calculator has a complete approved design spec (git history: `design_spec_team_synergy_system.md`) — Tasks 7-8 should reference it directly

**narrative/game-design (Task 10):**
- Act 1 is for investor demo — pacing must hook quickly and demonstrate choice consequence within 2-3 scenes
- 3 NPCs (Elena, Lars, Kade) with fixed personality archetypes — gates must be achievable through dialogue
- Branching strategy determines content volume and test complexity — favor bounded scope
- Flag naming must be consistent and documented for cross-scene prerequisite evaluation
- Personality gate thresholds must be achievable through normal gameplay (traits range 5-35%)

**narrative/content (Task 11):**
- Scene JSON must match the schema defined by the scene graph engine (Task 2)
- All flags referenced in prerequisites must be settable by a prior scene's choices
- Dead-end validation must pass — every scene needs at least one ungated choice

### Cross-Domain Dependencies

- Task 10 (narrative/game-design) → Task 11 (narrative/content): design document defines what scenes to author
- Task 11 (narrative/content) → Task 2 (code/backend): scene JSON must conform to engine's expected format
- Task 7 (code/backend) references approved design spec — no cross-domain dependency (spec is complete)

### Sequencing Considerations

- Tasks 1-4 form a dependency chain (types → engine → choices → state machine). Task 5 extends state updaters after the state machine is stable.
- Task 6 (persistence) can proceed once Task 5 is done — it's validation work, possibly requiring minimal code changes.
- Task 9 (API) depends on Tasks 5 and 6 but is independent of synergy work.
- Tasks 7-8 (synergy) are independent of Tasks 2-6. They depend only on Task 1 (types) and the completed design spec.
- Task 10 (narrative design) can run in parallel with all code tasks — it has no code dependencies.
- Task 11 (scene authoring) is blocked on both Task 10 (design) and Tasks 2-3 (engine, so JSON format is defined).
- Task 12 (integration) is the final task, blocked on Tasks 8, 9, and 11.

### Open Questions

- Should narrative types live in `types/index.ts` (extending the existing file) or `types/narrative.ts` (new file importing from index)? Sprint 2 used a separate `types/combat.ts` — same pattern may apply.
- How much overlap exists between the scene graph engine and the existing dialogue engine? Should they share graph traversal utilities or remain independent?
- Scene JSON files: `src/fixtures/scenes/` (alongside encounter.json) or `src/narrative/scenes/` (co-located with engine)?
- The choice consequence engine should reuse `updateNPCAffection`, `updateNPCTrust`, `updateNPCRelationship`, and `processDialogueChoice` from `stateUpdaters.ts` — detail phase should read these before designing the consequence API.
