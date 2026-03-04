# Assemble Brief: Sprint 3 — Narrative & State Machine

**Date:** 2026-03-02
**Plan Reference:** `docs/project_notes/branches/sprint-3/plan.md` (v9 mode, 12 tasks)

---

## Objective Summary

Build the narrative progression layer for Act 1: scene graph engine, choice consequence system, team synergy bonuses, and narrative REST API. Connects Sprint 1 (personality) and Sprint 2 (combat) into a cohesive story experience.

---

## Domain Breakdown

### code/backend (10 tasks: 1-9, 12)

**Tier breakdown:**
- **Light (5 tasks):** 1, 5, 6, 7, 8, 12 — Autonomous execution, standard patterns
- **Standard (4 tasks):** 2, 3, 4, 9 — Confirmation gates, new modules, multiple endpoints

**Key constraints:**
- All state transitions must produce new objects (ADR-012)
- NarrativeState must be JSON-serializable (no Sets/Maps/circular refs)
- Scene prerequisites must always have ungated fallback (no dead ends)
- Synergy bonuses integrate through `initCombatState`, not CombatState modifications (ADR-013)
- Sprint 1+2 test suite must remain green
- Synergy calculator has a complete approved design spec — reference it directly for Tasks 7-8

**Shared file modifications:**
- `src/types/index.ts` — extend with narrative types (Task 1)
- `src/state/stateUpdaters.ts` — add narrative updaters (Task 5)
- `src/combat/sync.ts` — integrate synergy bonuses (Task 8)
- `src/persistence/saveLoad.ts` — validate narrative serialization (Task 6)
- `src/api/index.ts` — register narrative plugin (Task 9)

### narrative/game-design (1 task: 10)

**Depth:** Deep — Design exploration required.

**Purpose:** Establish Act 1 narrative arc, branching strategy, scene scope, choice design, flag naming. Design document guides Task 11.

**Constraints:**
- Act 1 demo is a Gym Town mid-journey slice (not opening sequence)
- 2-3 scenes required
- Personality gates must be achievable (traits 5-35% range)
- Flag naming must be consistent and documented
- Branching strategy must cap content volume (linear spine + variants recommended)

### narrative/content (1 task: 11)

**Depth:** Standard — Guided by Task 10 design.

**Purpose:** Author 2-3 Act 1 scenes as JSON scene graph data, implementing the Task 10 design.

**Constraints:**
- Scene JSON must match engine schema (Task 2)
- All flags referenced in prerequisites must be settable
- Dead-end validation must pass

---

## Task Dependencies & Parallelization

**Code/Backend Chains (can run independently):**

- **Type + Synergy chain** (Tasks 1, 7, 8): No blocking on narrative engine
- **Engine chain** (Tasks 2, 3, 4, 5, 6): Sequence as written; Task 6 is validation-only
- **API task** (Task 9): Depends on Tasks 5, 6; independent of synergy work
- **Integration** (Task 12): Blocked on Tasks 8, 9, 11

**Narrative Chain:**

- **Task 10 design** (Deep): No code dependencies; can run in parallel with all code tasks
- **Task 11 authoring** (Standard): Blocked on Task 10 (design) + Tasks 2-3 (engine for JSON format)

**Execution Strategy:**

- **Phase 1** (parallel):
  - Code/Backend Specialist A: Tasks 1, 2, 3, 4, 5, 6, 9, 12 (sequenced per dependencies)
  - Narrative/Game-Design Specialist B: Task 10 (Deep exploration)

- **Phase 2** (after Task 10 design):
  - Narrative/Content Specialist C: Task 11 (authoring guided by Task 10)

---

## Known Technical Decisions

**Locked Decisions:**
- Scene Architecture: Directed graph with prerequisites (mirrors dialogue engine pattern)
- Consequence Model: Local effects + named flags (1-hop, multiple readers)
- Synergy-Combat Boundary: Applied at `initCombatState` (pre-combat, separate from GROUP's 1.5x)
- Synergy Calculation: Paradigm-based (Well Rounded, Bond) with highest-only selection, binary thresholds
- Content Scope: Engine + 2-3 starter scenes
- Scene Storage: Static JSON fixtures

**Detail-Required Decision:**
- Branching Strategy: Deferred to Task 10 (Deep). User will explore linear spine + variants vs true branching vs minimal during design.

---

## Critical Test Scenarios

- No dead ends: every reachable scene has ≥1 available choice (Task 2)
- Flag propagation: Scene A choice → flag → Scene C prerequisite check (Tasks 3, 4)
- Backward compatibility: loading pre-narrative Sprint 1+2 saves doesn't crash (Task 6)
- Synergy TDD targets: Well Rounded/Bond pass/fail, both satisfied, no synergy, default personality (Task 7)
- Synergy-combat bridge: bonuses correctly modify combatant stats (Task 8)
- Full narrative arc via API: start → traverse → choose → observe consequences (Task 12)

---

## Specialist Matching Guidance

**code/backend specialist** should have:
- Deep familiarity with the existing codebase (personality system, state updaters, combat sync, REST API patterns)
- Experience with immutable state management and TypeScript
- Ability to read and interpret design specs (synergy calculator design spec)
- Vitest TDD workflow experience

**narrative/game-design specialist** should have:
- Game narrative/design experience
- Understanding of personality-driven dialogue design
- Comfort with iterative dialogue and exploration
- User feedback integration skills

**narrative/content specialist** should have:
- Narrative writing / dialogue authoring experience
- Understanding of game narrative pacing
- JSON data structure experience
- Ability to implement based on design specifications

---

## References

- **Plan:** `docs/project_notes/branches/sprint-3/plan.md`
- **Synergy Design Spec:** Git history: `docs/project_notes/branches/sprint-3/design_spec_team_synergy_system.md` (complete, approved 2026-02-24)
- **Codebase reference:** `src/dialogue/dialogueEngine.ts` (pattern for graph traversal with personality gates)
- **State updater reference:** `src/state/stateUpdaters.ts` (pure-function pattern)
- **ADRs:** ADR-012 (immutable state), ADR-013 (independent CombatState), ADR-026 (team synergy paradigm model)
