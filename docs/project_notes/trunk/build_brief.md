# Build Brief: Ancient Order Sprint 1 + Sprint 2 Rebuild

**Date:** 2026-02-21
**Status:** Ready for Implementation
**Planning Reference:** `docs/project_notes/trunk/plan.md`
**Code Specs Reference:** `docs/project_notes/trunk/code_specs.md`

---

## Objective

Implement the Ancient Order backend from empty `src/` directory across 22 sequential tasks:
- **Sprint 1 (Tasks 1-9):** Personality-driven narrative systems + REST API
- **Sprint 2 (Tasks 10-22):** Turn-based combat engine with Excel formula porting

Success criterion: a 3v3 combat encounter that resolves through all 5 phases and produces formula outputs matching Excel source of truth.

---

## Code Specs Summary

**22 tasks specified**

**Key engineering decisions:**
- ESM modules with NodeNext resolution (explicit `.js` import extensions)
- Fastify `decorate()` for type-safe session state
- Spread + `Readonly<T>` for immutability (compile-time + runtime enforcement)
- Per-call-site `rollFn` injection for deterministic testing
- Shared `calculateBaseDamage()` utility in formulas.ts (used by pipeline + GROUP)
- ESLint flat config + Prettier for linting
- Co-located test files (`*.test.ts` next to source); integration/E2E in `tests/`

**Cross-cutting patterns:**
- Fastify plugin architecture (replaces archived Express router factories)
- `ApiResponse<T>` envelope with `ErrorCodes` const object
- Pure function pipeline: `(state, action, rollFn) => newState`
- TDD for all combat formulas (Excel source of truth via ADR-015)
- Immutability via spread operator (ADR-001)

**Design integration:**
- Behavior Tree AI (Task 17): Utility-scoring system with 7 factors, rank coefficient, archetype profiles (from `design_spec_behavior_tree_ai_system.md`)
- GROUP Action Type (Task 18): Leader-initiated team action, priority 0, energy gate, 1.5x damage multiplier (from `design_spec_group_action_type.md`)

---

## Task Sequence

### Sprint 1: Narrative Stack (Tasks 1-9)
| Task | Component | Dependencies |
|------|-----------|--------------|
| 1 | Project Foundation & Tooling | None |
| 2 | Sprint 1 Type System | Task 1 |
| 3 | Personality System | Tasks 2, 3 |
| 4 | Game State & NPC System | Tasks 2, 3 |
| 5 | State Updaters Library | Tasks 3, 4 |
| 6 | Dialogue Engine | Tasks 3, 5 |
| 7 | Persistence Layer | Task 2 |
| 8 | Sprint 1 REST API (Fastify) | Tasks 5, 6, 7 |
| 9 | Sprint 1 Integration Validation | Task 8 |

### Sprint 2: Combat Engine (Tasks 10-22)
| Task | Component | Dependencies | Design Spec |
|------|-----------|--------------|------------|
| 10 | Combat Type System | Task 9 | None |
| 11 | Combat Formula Suite (TDD) | Task 10 | None |
| 12 | Defense Resolution & Counter Chain | Tasks 10, 11 | None |
| 13 | Elemental Path & Energy/Ascension | Tasks 10, 11 | None |
| 14 | Player Declaration Validation | Task 10 | None |
| 15 | Action Priority & Resolution Pipeline | Tasks 11-14 | None |
| 16 | Round Manager Orchestrator | Task 15 | None |
| 17 | Behavior Tree AI | Task 10 + design spec | `design_spec_behavior_tree_ai_system.md` |
| 18 | Group Action Type | Task 10 + design spec | `design_spec_group_action_type.md` |
| 19 | Combat Integration | Tasks 16, 17, 18 | None |
| 20 | GameState-CombatState Synchronization | Tasks 16, 19 | None |
| 21 | Combat REST API | Tasks 19, 20 | None |
| 22 | End-to-End 3v3 Demo Encounter | Task 21 | None |

---

## Required User Steps

- **Excel Verification (Task 11, 22):** During TDD formula porting, verify test values against `GM Combat Tracker.xlsx`. The build agent will need specific input/output pairs for each formula category.
- **Encounter Fixture Data (Task 22):** Provide 3v3 encounter config (character stats, party composition) from Excel "Battle Scenarios" sheet, or confirm the build agent can extract it.
- **No server commands needed:** All testing uses Vitest. All API testing uses Fastify injection (no HTTP server startup required).

---

## Quality Gates

Before marking any task complete, verify:

1. **Unit tests pass** — all pure functions tested with relevant scenarios
2. **Integration tested** — multi-step workflows tested end-to-end (e.g., full round with all action types)
3. **Formula accuracy (T11, T12, T13)** — spot-checked against Excel source of truth (per ADR-015)
4. **Determinism (T16-T22)** — same state → same result; no hidden randomness
5. **Immutability enforced** — all state updates produce new objects; originals unchanged (per ADR-001)
6. **API contracts honored** — Fastify endpoints match specifications
7. **No circular imports** — all modules compile with `npm run build`

---

## Known Risks & Mitigations

| Risk | Likelihood | Impact | Mitigation |
|------|-----------|--------|-----------|
| ESM import path `.js` extensions confuse build agent | Medium | Medium | Code specs explicitly document this requirement. Vitest and TypeScript handle resolution. |
| Formula TDD requires Excel values — build agent lacks Excel access | Medium | High | Spec includes formulas from `GM_Combat_Tracker_Documentation.md`. User verifies test pairs against Excel where docs insufficient. |
| Behavior tree has 20+ files with 7 factors + 3 profiles | Medium | Medium | Design spec provides exact scoring tables and data. Factors are independent; can be implemented/tested in isolation. |
| GROUP priority 0 change affects pipeline sorting logic | Low | Medium | Priority table defined in `types/combat.ts` constants (Task 10). Pipeline reads constant; change isolated. |
| Counter chain infinite loop on buggy termination condition | Low | Medium | Safety cap at 10 iterations. While-loop with explicit termination checks (Parry fail, KO, stamina). Tests cover all termination conditions. |
| Fastify plugin architecture differs from archived Express patterns | Medium | Medium | Code specs provide clear file-by-file Fastify plugin pattern. Build agent uses this as reference. |
| CombatState ↔ GameState sync edge cases (save during combat) | Low | Low | Task 20 acceptance criteria explicitly covers save/load-during-combat scenario. Tests validate this path. |

---

## Implementation Approach

**Linear execution:** Tasks 1-9 (Sprint 1) must complete before Tasks 10-22 (Sprint 2) begin. Task 9 integration validation is a hard gate.

**Parallelization within Sprint 2:**
- Tasks 11, 14 can proceed in parallel (both need only T10)
- Tasks 12, 13 can proceed in parallel after Task 11 complete
- Tasks 17, 18 (design-dependent) can proceed in parallel with Tasks 10-16 (independent phases)

**TDD workflow (Task 11 primary):**
- Write test with Excel-derived input/output
- Implement formula
- Verify test passes
- Repeat for each formula category

**Build output validation:**
- After each task completion, unit tests should pass
- Integration tests run at sprint gates (T9 for Sprint 1, T19 for Sprint 2 core, T22 for full E2E)
- Formula verification (T11, T22): spot-check against Excel to ensure ADR-015 compliance

---

## Files to Create / Modify

**Configuration files** (Task 1): `package.json`, `tsconfig.json`, `vitest.config.ts`, `eslint.config.js`, `.prettierrc`

**Sprint 1 sources** (Tasks 2-8): 30+ files in `src/types/`, `src/personality/`, `src/state/`, `src/dialogue/`, `src/persistence/`, `src/api/`

**Sprint 2 sources** (Tasks 10-21): 50+ files in `src/types/combat.ts`, `src/combat/`, `src/combat/behaviorTree/`, `src/fixtures/`

**Tests** (throughout): 35+ test files co-located with sources + `tests/` directory for integration/E2E

**Directory structure** (Task 1): Create empty directories matching component architecture from plan Section 4

---

## References

- **Plan:** `docs/project_notes/trunk/plan.md` (detailed task specs, acceptance criteria, dependencies)
- **Design Specs:** `design_spec_behavior_tree_ai_system.md`, `design_spec_group_action_type.md`
- **Engineering Decisions:** `docs/project_notes/decisions.md` (ADR-001 through ADR-020 established; ADR-021–025 from code specs)
- **Archived Patterns:** `docs/project_notes/archive/src-sprint1-prototype/` (reference for naming, structure, pure function patterns)
- **Combat Reference:** `docs/Reference Documents/GM_Combat_Tracker_Documentation.md` (formula source of truth)
- **API Contracts:** `docs/Reference Documents/Sprint1_API_Reference.md` (endpoint specs, type contracts)

