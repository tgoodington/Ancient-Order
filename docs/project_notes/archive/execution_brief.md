# Execution Brief: Sprint 1 Closeout + Sprint 2 Combat Engine

**Status:** Ready for execution
**Date:** 2026-02-11

---

## Plan Summary

This execution cycle covers two closely integrated workstreams: closing Sprint 1 with unit test coverage for pure functions, and implementing the full Sprint 2 combat engine. The combat system is built on a pipeline architecture that mirrors the 5-phase turn structure, with each phase as an independently testable pure function. All formulas are ported from the GM Combat Tracker Excel using test-driven development to guarantee correctness.

## Objective

Deliver Sprint 1 closeout (unit tests for personality system, dialogue engine, state updaters) and a complete, testable combat engine that resolves 3v3 encounters through all 5 phases with formula accuracy matching Excel source. Success: passing unit tests + full combat round execution with correct damage, defense, and energy calculations.

## Discovery Context

Sprint 1 backend (~94% complete) lacks test coverage entirely. Sprint 2 adds the most complex system in the project — 12 interconnected subsystems with deep formula dependencies. The existing codebase follows clean patterns (immutable state, layered architecture, pure functions) that map perfectly to a pipeline combat design. Two subsystems require design exploration before execution (behavior tree AI, Group action type) — these must be designed separately via `/intuition-design` before implementation.

## Task Summary (19 total)

### Sprint 1 Closeout (5 execute-ready tasks)
1. **Set Up Test Infrastructure** — Jest, ts-jest, jest.config.ts. (Wave 1, no dependencies)
2. **Unit Tests — Personality System** — Validate trait bounds, redistribution, normalization. (Wave 2, dep: Task 1)
3. **Unit Tests — Dialogue Engine** — Gate evaluation, dead-end validation, option filtering. (Wave 2, dep: Task 1)
4. **Unit Tests — State Updaters** — Immutability verification, update chaining, team composition. (Wave 2, dep: Task 1)
5. **Verify and Fix DELETE Endpoint** — Audit /api/game/saves/:slot, complete if needed. (Wave 1, no dependencies)

### Sprint 2 Combat Engine (14 tasks: 12 execute-ready, 2 design-required)

#### Execute-Ready (implement directly from plan)
6. **Combat Type Definitions** — All interfaces: CombatState, Combatant, DeclaredAction, AttackResolution. (Wave 2, no dependencies)
7. **Dominance Checks** — Rank KO, Blindside, Crushing Blow. TDD from Excel. (Wave 2, dep: Task 6)
8. **Defense Resolution** — Block/Dodge/Parry with SR/SMR/FMR, Defenseless, forced defense. (Wave 2, dep: Task 6)
9. **Damage Calculation & Stamina** — Multi-stage damage, max calc, recovery, KO, color coding. (Wave 3, dep: Tasks 6, 8)
10. **Counter Chain System** — Parry cascading, chain termination conditions. (Wave 3, dep: Tasks 8, 9)
11. **Path/Elemental System** — 6 paths with buff/debuff mechanics, forced defenses. (Wave 2, dep: Task 6)
12. **Energy & Ascension System** — Segment accumulation, level thresholds, bonuses, special costs. (Wave 2, dep: Task 6)
13. **Action Priority & Resolution Pipeline** — Phase 4-5 orchestration, attack resolution chain. (Wave 4, dep: Tasks 7-12)
14. **Player Declaration Phase** — Action validation, target checks, energy constraints. (Wave 2, dep: Task 6)
15. **Round Manager** — Full pipeline orchestrator, phase transitions, combat end conditions. (Wave 5, dep: Tasks 13, 14, 16)
18. **Combat API Endpoints** — POST/GET for start, state, declare, resolve, history. (Wave 5, dep: Task 15)
19. **GameState Combat Integration** — Sync between independent CombatState and GameState. (Wave 5, dep: Tasks 6, 15)

#### Design-Required (run `/intuition-design` BEFORE execution)
16. **Behavior Tree AI System** — Tree framework, node types, evaluation model, NPC archetypes. START DESIGN NOW. (Wave 5 execution after design, dep: Tasks 6, 14)
17. **Group Action Type** — Mechanics, targeting, priority system, team synergy. START DESIGN NOW (lower priority, can stub). (Wave 5 execution after design, dep: Task 6, 13)

---

## Design Explorations (Parallel with Waves 1-3)

**IMPORTANT:** Before execution can begin on Tasks 16-17, their designs must be completed via `/intuition-design`. These subsystems have no existing specifications and contain architectural decisions you need to make.

**Task 16 — Behavior Tree AI:**
- Purpose: NPC combat decision-making (Phase 1)
- Questions to resolve: Node types, state access patterns, archetype profiles, integration method
- Design must specify: Interfaces, evaluation algorithm, example tree, archetype behaviors

**Task 17 — Group Action Type:**
- Purpose: Coordinated team action mechanic
- Questions to resolve: Mechanics (what does Group do?), resolution order, synergy effects
- Design must specify: Mechanics description, interaction with defense resolution, resolution algorithm

Both designs should be completed and saved as `design_spec_*.md` files before Wave 5 begins. If design reveals implementation complexity, execution can begin with stubbed versions (e.g., "Group not implemented in Sprint 2").

---

## Quality Gates (Mandatory)

- **All tests must pass** — Tasks 2-4, 7-14 have test suites that must be 100% passing
- **Formula accuracy** — All combat math verified against Excel using TDD; no optimizations or "improvements" to formulas
- **Immutability** — All combat functions follow spread-operator pattern; no mutations
- **Pure functions** — No side effects in combat pipeline; only gameState sync happens at round boundaries

---

## Known Risks & Mitigations

| Risk | Likelihood | Impact | Mitigation |
|------|-----------|--------|------------|
| Excel formula misinterpretation | Med | High | Test-driven porting with exact I/O pairs from spreadsheet. Verify against GM_Combat_Tracker_Documentation.md. |
| Behavior tree scope creep | Med | Med | Design exploration locks scope before execution. Defer sophistication to later sprints. |
| Counter chain infinite loop | Low | High | Implement chain depth limit (default 10). Stamina depletion is natural terminator. |
| Group action delays execution | Med | Low | Can be stubbed (placeholder) without blocking. Implement in Sprint 3 if time constrained. |
| Combat state complexity | Med | Med | Independent CombatState keeps pipeline focused. Sync only at round boundaries. Types enforce structure. |
| Phase 2 info asymmetry bug | Low | High | GET /api/combat/state must filter response based on phase. Test with both player and enemy perspectives. |

---

## Execution Strategy

### Wave 1 (Can start immediately, ~2 days)
- Task 1: Test infrastructure setup
- Task 5: DELETE endpoint audit/fix
- Task 6: Combat type definitions (all 3 in parallel)

### Wave 2 (After Wave 1, ~4 days)
- Tasks 2-4: Sprint 1 unit tests (parallel)
- Tasks 7-8, 11-12, 14: Combat subsystems (parallel)
  - Recommend starting Tasks 7-8 first (they gate Tasks 9-10)

### Wave 3 (After critical Wave 2 paths, ~2 days)
- Tasks 9-10: Damage calc + counter chains (depends on Tasks 8, 9)

### Design Exploration (Can start now, parallel with Waves 1-3)
- Task 16: `/intuition-design` for Behavior Tree AI (3-5 hours)
- Task 17: `/intuition-design` for Group action (2-3 hours, lower priority)

### Wave 4 (After Wave 3, ~2 days)
- Task 13: Resolution pipeline (depends on all subsystems)

### Wave 5 (After Task 15 design complete + Wave 4, ~2-3 days)
- Task 15: Round manager (depends on Tasks 13, 14, 16 design)
- Task 18: Combat API endpoints (depends on Task 15)
- Task 19: GameState integration (depends on Tasks 6, 15)

### Total Estimated Duration
- Sprint 1 tests: 2-3 days (can overlap with Sprint 2)
- Sprint 2 combat: 10-12 days
- Design explorations: 1 day (run in parallel)
- **Total: 10-14 days with recommended parallelization**

---

## References

- **Full Plan:** `docs/project_notes/plan.md`
- **Decisions Log:** `docs/project_notes/decisions.md` (5 locked decisions)
- **Codebase patterns:** `src/state/stateUpdaters.ts` (immutable pattern model), `src/dialogue/dialogueEngine.ts` (pure function model)
- **Formula source:** `docs/Reference Documents/GM_Combat_Tracker_Documentation.md`, `docs/Reference Documents/GM Combat Tracker.xlsx`
- **Type patterns:** `src/types/index.ts` (existing interfaces to match style)

---

## Next Steps

1. **Immediate:** Run `/intuition-design` for Tasks 16-17 (behavior tree, group action) in parallel with Wave 1 starting
2. **After design complete:** Begin Wave 1 implementation (Task 1, 5, 6)
3. **All design specs saved:** Proceed to Waves 2-5

Run `/intuition-execute` when ready to begin implementation.
