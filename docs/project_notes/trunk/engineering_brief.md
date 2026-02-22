# Engineering Brief: Ancient Order Sprint 1 + Sprint 2 Rebuild

**Date:** 2026-02-21
**Status:** Ready for Implementation
**Planning Reference:** `docs/project_notes/trunk/plan.md`
**Design Status:** All design items complete — 2 design specs ready for engineering

---

## Objective

Rebuild the Ancient Order backend from an empty `src/` directory, implementing Sprint 1 (personality-driven narrative systems) and Sprint 2 (turn-based combat engine), producing a working backend sufficient for an investor/publisher pitch demo.

Success is measured by a single criterion: a 3v3 combat encounter that initializes, resolves through all 5 pipeline phases across multiple rounds, and produces per-attack formula outputs that exactly match the Excel source of truth.

---

## Discovery Context

- **Problem:** Complete backend rebuild from empty codebase. Prior Sprint 1 prototype archived as reference — patterns reusable, code not to be copied. Sprint 2 combat engine never implemented.
- **Constraints:** Personality (6 traits, 5-35% range, sum=100%), Combat formulas (exact Excel replication), Dialogue (no dead ends), NPCs (fixed archetypes)
- **Platform:** Node.js + Fastify + TypeScript + Vitest + JSON file persistence
- **Build Sequencing:** Linear — Sprint 1 complete before Sprint 2 begins

---

## Design Specifications (Ready for Implementation)

Engineer **MUST** read these design specs before creating code specs for flagged tasks:

1. **`design_spec_behavior_tree_ai_system.md`** (Task 17)
   - Utility-scoring AI (7 multi-output factors, not classic behavior tree)
   - Rank-based decision quality coefficient (0.2-1.0)
   - 3 archetype profiles (Elena, Lars, Kade) with data-driven weights
   - Path-based tie-breaking (6 paths → 6 action priority orders)
   - CombatPerception layer for immutable state access
   - GROUP excluded via config flag (pending GROUP design)

2. **`design_spec_group_action_type.md`** (Task 18)
   - Leader-initiated team action (conscripts all allies)
   - Priority 0 (highest, before DEFEND)
   - Energy gate (all participants require full segments)
   - Block-only defense suppression (no Dodge/Parry/counters)
   - 1.5x damage multiplier on sum of participant damage
   - Flexible participant count (fires with non-KO'd allies)
   - Extending priority sort: GROUP=0, DEFEND=1, ATTACK/SPECIAL=2, EVADE=3

---

## Task Summary

### Sprint 1 (Tasks 1-9): Narrative Stack Foundation

| Task | Component | Status | Role in Demo |
|------|-----------|--------|-------------|
| 1 | Project Foundation & Tooling | Ready | Build infrastructure (Fastify, Vitest, TypeScript) |
| 2 | Sprint 1 Type System | Ready | Core interfaces (Personality, PlayerCharacter, NPC, GameState) |
| 3 | Personality System | Ready | 6 traits, 5-35% range, redistribution algorithm |
| 4 | Game State & NPC System | Ready | Initial state creation, 3 test NPCs (Elena, Lars, Kade) |
| 5 | State Updaters Library | Ready | Pure immutable state transition functions |
| 6 | Dialogue Engine | Ready | Tree traversal, personality gates, dead-end validation |
| 7 | Persistence Layer | Ready | JSON save/load, 10 addressable slots |
| 8 | Sprint 1 REST API (Fastify) | Ready | All Sprint 1 endpoints (game, player, npc, dialogue) |
| 9 | Sprint 1 Integration Validation | Ready | End-to-end session flow testing |

### Sprint 2 (Tasks 10-22): Combat Engine

| Task | Component | Status | Dependencies | Design Spec |
|------|-----------|--------|--------------|------------|
| 10 | Combat Type System | Ready | Task 9 | None — interfaces derived from GM_Combat_Tracker_Documentation.md |
| 11 | Combat Formula Suite (TDD) | Ready | Task 10 | None — formulas sourced from Excel |
| 12 | Defense Resolution & Counter Chain | Ready | Tasks 10, 11 | None — mechanics documented |
| 13 | Elemental Path & Energy/Ascension | Ready | Tasks 10, 11 | None — mechanics documented |
| 14 | Player Declaration Validation | Ready | Task 10 | None — straightforward validation |
| 15 | Action Priority & Resolution Pipeline | Ready | Tasks 11-14 | None — **MUST UPDATE PRIORITY TABLE**: GROUP=0 (was 2), DEFEND=1 (was 1), ATTACK/SPECIAL=2 (was 3), EVADE=3 (was 4) — see GROUP spec |
| 16 | Round Manager Orchestrator | Ready | Task 15 | None — 5-phase orchestration (with stubs initially) |
| 17 | Behavior Tree AI | **DESIGN READY** | Task 10 + design spec | `design_spec_behavior_tree_ai_system.md` — implements utility scoring evaluator |
| 18 | Group Action Type | **DESIGN READY** | Task 10 + design spec | `design_spec_group_action_type.md` — validates at declaration, resolves at priority 0 |
| 19 | Combat Integration | Ready (pending T17/T18) | Tasks 16, 17, 18 | None — mechanical wiring (replaces stubs) |
| 20 | GameState-CombatState Synchronization | Ready | Tasks 16, 19 | None — bidirectional sync at round boundaries |
| 21 | Combat REST API | Ready | Tasks 19, 20 | None — Fastify combat plugin |
| 22 | End-to-End 3v3 Demo Encounter | Ready | Task 21 | None — static fixture + multi-round validation |

---

## Critical Implementation Notes

### Priority System Change (Task 15)
- **Before:** `DEFEND: 1, GROUP: 2, ATTACK: 3, SPECIAL: 3, EVADE: 4`
- **After:** `GROUP: 0, DEFEND: 1, ATTACK: 2, SPECIAL: 2, EVADE: 3`
- GROUP moves to priority 0 (highest) per design spec ADR-020
- This change is internal to `combat/pipeline.ts` — no other tasks affected

### GROUP Action Type Integration Points (Task 18)
- **declaration.ts:** Validate GROUP eligibility (all allies have full energy)
- **roundManager.ts Phase 3:** Mark ally declarations as overridden when GROUP accepted
- **pipeline.ts:** Priority sort uses new GROUP=0 slot
- **defense.ts:** Called by GROUP resolver with forced `defenseType: 'block'`
- **behavior tree evaluator:** Check `groupActionsEnabled` flag before adding GROUP to candidates

### Behavior Tree AI Integration (Task 17)
- Evaluator interface: `evaluate(combatant: Combatant, state: CombatState): CombatAction`
- Wraps internal utility scoring system
- Factors call `perception.buildPerception()` on each evaluation
- Archetype profiles are pure data (JSON-like structures)
- GROUP initially disabled via `EvaluatorConfig.groupActionsEnabled = false`

### Formula Porting (Task 11)
- Use test-driven development: write test with Excel value, then implement
- All 5 action types, 5 dominance checks, 4 event/result combinations covered
- Critical: at least 2 input/output test pairs per formula category
- Spot-check against Excel during Task 22 E2E validation

### Immutability Pattern (Throughout)
- All state updates produce new objects (spread operator + Readonly<>)
- Pipeline functions are pure: `(state, action) => newState`
- No mutations to input state
- `GroupActionConfig` and profiles can be constants (frozen objects)

---

## Known Risks & Mitigations

| Risk | Likelihood | Impact | Mitigation |
|------|-----------|--------|-----------|
| Behavior tree AI scope expands mid-implementation | Medium | High | Design spec is complete with clear scope. If scope expands, stub AI can power limited demo while full BT deferred to later sprint. |
| GROUP mechanics incompatible with priority system after implementation starts | Low | High | Design specs were created collaboratively with priority system in mind. GROUP spec explicitly references priority table changes. |
| Excel formula porting produces silent errors | Medium | High | ADR-015 TDD approach is primary mitigation — tests must be written with Excel values first. Engineer should verify test inputs against Excel during T11. |
| Fastify plugin architecture differs from archived Express patterns | Medium | Medium | Code specs will provide clear examples of Fastify plugin pattern. Engineer should read Fastify docs (esp. plugin scoping, `fastify.decorate()`) before writing specs. |
| CombatState ↔ GameState sync edge cases (concurrent save during combat) | Low | Medium | Task 20 acceptance criteria explicitly cover save/load-during-combat scenario. Mark as test priority. |

---

## Quality Gates

Before marking any task complete:

1. **Unit tests pass:** All pure functions tested with relevant input scenarios
2. **Integration tested:** Multi-step workflows tested end-to-end (e.g., full round with all action types)
3. **Formula accuracy (T11, T12, T13):** Spot-checked against Excel source of truth
4. **Determinism (T16-T18):** Same state → same result (no hidden randomness)
5. **Immutability enforced:** All state updates produce new objects; original unchanged
6. **API contracts honored:** Fastify endpoints match Sprint 1 REST API reference

---

## References

- **Plan:** `docs/project_notes/trunk/plan.md` (Section 6: Task Sequence with full acceptance criteria)
- **Discovery:** `docs/project_notes/trunk/discovery_brief.md`
- **Combat System:** `docs/Reference Documents/GM_Combat_Tracker_Documentation.md`
- **Reference Docs:** All 8 documents in `docs/Reference Documents/`
- **Design Specs:** `docs/project_notes/trunk/design_spec_*.md` (behavior tree AI, GROUP action type)
- **Architecture Decisions:** `docs/project_notes/decisions.md` (ADR-001 through ADR-020)
- **Key Facts:** `docs/project_notes/key_facts.md` (system mechanics, archetype profiles, API endpoints)
