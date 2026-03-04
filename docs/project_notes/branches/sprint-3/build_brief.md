# Build Brief: Sprint 3 Detail Phase Complete

**Date:** 2026-03-03
**Context:** Sprint 3 narrative & state machine implementation
**Status:** All blueprints approved, conflict check passed, ready for production

---

## Objective

Implement the narrative progression layer for Act 1: a complete scene graph engine with personality-gated choices, flag-based consequences, team synergy system, and REST API integration. Two specialist blueprints define both the technical engine (Tasks T1-T9, T12) and the narrative content (Tasks T10-T11).

---

## Team Summary

**Specialists (completed):**
1. **Game Backend Engine** — Architecture, types, engine modules, synergy system, API integration
2. **Narrative Designer** — Scene design, choice architecture, flag system, demo content

**Producer assignments:**
- Game Backend Engine tasks (T1-T9, T12) → code-writer producer
- Narrative Designer tasks (T10-T11) → document-writer (Task 10) + data-file-writer (Task 11)

**Total blueprints:** 2
**Total tasks:** 12 (T1-T12)
**Total acceptance criteria:** 56

---

## Blueprint Index

### 1. Game Backend Engine Blueprint
**Specialist:** Game Backend Engine
**Tasks covered:** T1-T9, T12 (10 tasks, 39 acceptance criteria)
**Producer:** code-writer
**Output format:** TypeScript implementation + tests
**File path:** `docs/project_notes/branches/sprint-3/blueprints/game-backend-engine.md`

**Summary:**
- New `src/types/narrative.ts` with all scene/choice/synergy types
- `src/narrative/` module with sceneEngine, choiceEngine, narrativeStateMachine, synergyCalculator
- Extend `src/types/index.ts` with `narrativeState: NarrativeState | null` field on GameState
- New `src/api/narrative.ts` plugin with 6 REST endpoints
- Extend `src/state/stateUpdaters.ts` with narrative-specific updaters
- Extend `src/persistence/saveLoad.ts` for NarrativeState validation
- Modify `src/combat/sync.ts` to calculate and apply synergy bonuses at combat init
- Add narrative-specific error codes to `ErrorCodes` const
- Integration tests validating end-to-end flow (T12)

**Key architectural decisions:**
- D1: Flat array prerequisites with implicit AND logic (no OR in POC)
- D2: Reuse existing state updaters directly (applyPersonalityAdjustment, updateNPCRelationship)
- D3: Scene JSON location `src/fixtures/scenes/`
- D4: NarrativeTransitionResult discriminated union (success | error)
- D5: Synergy integrated via existing `_gameState` parameter in initCombatState
- D6: Narrative errors extend existing ErrorCodes

### 2. Narrative Designer Blueprint
**Specialist:** Narrative Designer
**Tasks covered:** T10-T11 (2 tasks, 11 acceptance criteria)
**Producers:** document-writer (T10) + data-file-writer (T11)
**Output format:** Markdown design document (T10) + JSON scene graph (T11)
**File path:** `docs/project_notes/branches/sprint-3/blueprints/narrative-designer.md`

**Summary:**
- Linear spine narrative structure: 3 scenes in fixed sequence (Ironhold Arrival → Market Disturbance → Gym Registration)
- 12 total choices across 3 scenes (4 per scene)
- 3 personality gates (kindness≥19, cunning≥20, patience≥22) with ungated fallbacks
- 12 consequence flags with hybrid verb_object naming (defended_thief, gathered_intel, etc.)
- 2 flag-based prerequisites demonstrating cross-scene consequence
- Moderate prose (4-6 sentences), third person past tense, confident fantasy tone
- Indirect Rogue presence (thief with symbol, not named NPC)
- NPC relationship arcs for Elena (party), Kade (party), and Lars (encounter merchant)

**Key design decisions:**
- D1: Moderate gate thresholds (19-22), balanced between accessible and earned
- D2: Hybrid verb_object flag naming convention
- D3: 3-4 choices per scene
- D4: Indirect Rogue presence (adheres to ADR-029 "low-level background")
- D5: Town named "Ironhold" for world-building polish
- D6: Moderate prose with clear narrative voice

---

## Conflict Check Results

**Scan conducted:** 2026-03-03
**Conflicts found:** **None**

**Verification summary:**
✓ No contradictory decisions between blueprints
✓ No overlapping file modifications with conflicts
✓ No inconsistent interface assumptions
✓ No duplicated work

**Dependency validation:**
- Game Backend Engine (T1-T6) must complete before Narrative Designer T11 (T11 depends on T2-T3 engine implementation)
- Narrative Designer T10 can run in parallel with Game Backend T1-T9
- T11 JSON authoring depends on T10 design completion

Both blueprints are complementary with clear separation of concerns (engine vs. content).

---

## Quality Gates

All blueprints have passed:

✓ **Completeness gate:**
- All 9 mandatory sections present in both blueprints
- Acceptance Mapping section addresses every acceptance criterion
- Producer Handoff sections specify clear output formats and tone guidance
- Open Items contain only [VERIFY] execution-time validation tasks

✓ **Design quality:**
- All design choices grounded in user decisions (6 each in both blueprints)
- All assumptions documented and either accepted or overridden
- Research findings cited with file paths
- Decisions Made section provides audit trail with rationale for rejected options
- Integration Points specify exact file locations and format requirements

✓ **Architectural alignment:**
- Game Backend Engine defines types and engines
- Narrative Designer depends on those types (clear dependency direction)
- Both align on NPC data, personality system, and API response formats
- Both follow established patterns from Sprint 1+2 (immutable state, Fastify plugins, spread operator)

---

## Known Risks

**Aggregated from blueprints:**

1. **Gate reachability in short demo** (Narrative Designer R1)
   - Risk: With only 3 scenes, Scene 3 gates may be unreachable
   - Mitigation: Gates set at 22 max; test paths verified; ungated fallbacks mandatory

2. **Flag tracking complexity** (Narrative Designer R2)
   - Risk: 9-12 flags across 3 scenes could become hard to track
   - Mitigation: Flag-based prerequisites limited to 1-2 instances; prerequisite graph kept shallow

3. **Personality adjustment algorithm mismatch** (Game Backend [VERIFY] item)
   - Risk: Gate thresholds may be off by 1 point due to redistribution rounding
   - Mitigation: T11 must test all gates against actual `adjustPersonality()` output

4. **Synergy integration with existing tests** (Game Backend R5)
   - Risk: Modifying `initCombatState` might break existing Sprint 1+2 combat tests
   - Mitigation: Synergy calculation is guarded (null input = no synergy = backward compatible)

5. **Narrative state persistence** (Game Backend R6)
   - Risk: Loading Sprint 1+2 saves without NarrativeState field could cause errors
   - Mitigation: NarrativeState validation defaults missing field to null

---

## References

**Blueprint files:**
- `docs/project_notes/branches/sprint-3/blueprints/game-backend-engine.md`
- `docs/project_notes/branches/sprint-3/blueprints/narrative-designer.md`

**Plan context:**
- `docs/project_notes/branches/sprint-3/plan.md` (12 tasks, full specification)

**Team assignments:**
- `docs/project_notes/branches/sprint-3/team_assignment.json` (specialist profiles and execution order)

**Shared project memory:**
- `docs/project_notes/decisions.md` (ADRs 001-029, all architectural decisions)
- `docs/project_notes/key_facts.md` (project configuration, NPC archetypes, personality specs)

---

## Next Steps

1. **Run `/intuition-build`** to begin production with coordinated execution
2. Game Backend Engine code-writer will implement T1-T9 in dependency order
3. Narrative Designer document-writer will produce Task 10 design doc (can begin in parallel)
4. After T2-T3 completion, Narrative Designer data-file-writer produces Task 11 scene JSON
5. Game Backend code-writer produces integration tests (T12) after T8-T9 complete
6. Final integration validation combines all systems

**Estimated duration:** 2-3 days for full implementation with proper testing

---

**Status:** ✅ Ready for production. All blueprints approved.
