# Build Report

**Plan:** Ancient Order Sprint 1 + Sprint 2 Rebuild
**Date:** 2026-02-23
**Status:** Success

---

## Tasks Completed

- [x] Task 10 — Combat Type System: 28 interfaces, 5 constants, zero-import leaf module
- [x] Task 11 — Combat Formula Suite (TDD): 16 formulas, 86 tests, Excel-formula TDD pattern
- [x] Task 12 — Defense Resolution & Counter Chain: 4 defense types, while-loop chain, 41 tests
- [x] Task 13 — Elemental Path & Energy/Ascension Systems: 6 paths data-driven, 73 tests
- [x] Task 14 — Player Declaration Validation: pure validation, 5 action types, GROUP KO exclusion
- [x] Task 15 — Action Priority & Resolution Pipeline: 7-step pipeline, GROUP stub, 30 tests
- [x] Task 16 — Round Manager Orchestrator: 5-phase orchestrator with stub AI, 33 tests
- [x] Task 17 — Behavior Tree AI: 22 files, 7 factors, 3 archetype profiles, 519 tests
- [x] Task 18 — Group Action Type: 1.5x multiplier, forced Block, energy gate, pipeline wired
- [x] Task 19 — Combat Integration: Real evaluator + GROUP wired, 5 integration scenarios
- [x] Task 20 — GameState-CombatState Synchronization: init/sync/endCombat pure functions, 31 tests
- [x] Task 21 — Combat REST API: 6 endpoints, Fastify plugin, ApiResponse envelope
- [x] Task 22 — End-to-End 3v3 Demo Encounter: fixture + 26 E2E API-driven tests

**Sprint 1 (Tasks 1-9) was already complete at build start.**

---

## Verification Results

- **Security Review:** PASS — No secrets, no injection, no path traversal. One Medium advisory fixed (maxItems: 10 on declare endpoint).
- **Code Review (Tasks 10, 14):** PASS
- **Code Review (Tasks 11, 17):** FAIL → corrections applied → resubmitted (dead code removed, test cases pinned)
- **Tests:** 793 passed / 793 total — 25 test files — zero failures
- **Build:** `npm run build` — zero TypeScript errors

---

## Files Modified / Created

**Sprint 2 type system:**
- `src/types/combat.ts` — created (28 interfaces, 5 constants)
- `src/types/index.ts` — refined `combatState` field type

**Sprint 2 combat engine (new files):**
- `src/combat/formulas.ts` + `.test.ts`
- `src/combat/defense.ts` + `.test.ts`
- `src/combat/counterChain.ts` + `.test.ts`
- `src/combat/elementalPaths.ts` + `.test.ts`
- `src/combat/energy.ts` + `.test.ts`
- `src/combat/declaration.ts` + `.test.ts`
- `src/combat/pipeline.ts` + `.test.ts`
- `src/combat/groupAction.ts` + `.test.ts`
- `src/combat/roundManager.ts` + `.test.ts`
- `src/combat/sync.ts` + `.test.ts`
- `src/combat/integration.test.ts`
- `src/combat/e2e.test.ts`

**Behavior tree (22 files):**
- `src/combat/behaviorTree/evaluator.ts`
- `src/combat/behaviorTree/perception.ts`
- `src/combat/behaviorTree/rankCoefficient.ts`
- `src/combat/behaviorTree/tieBreaking.ts`
- `src/combat/behaviorTree/factors/` (7 factor files + index + test)
- `src/combat/behaviorTree/profiles/` (3 profile files + index + test)

**API layer:**
- `src/api/combat.ts` — created (6 endpoints)
- `src/api/index.ts` — added combat plugin registration

**Fixtures:**
- `src/fixtures/encounter.json` — created (3v3 demo encounter)

**State layer:**
- `src/state/stateUpdaters.ts` — updated `updateCombatState` parameter type

---

## Issues & Resolutions

| Issue | Resolution |
|-------|-----------|
| `calculateBaseDamage` formula unverifiable (Excel cell references) | Proceeded with power-ratio model, added exact numeric tests, flagged for Excel verification at Task 22 |
| Task 11 Code Review FAIL — no exact test values for base damage | Added exact numeric assertions, added "pending Excel verification" note |
| Task 17 Code Review FAIL — dead `lerpScores` in targetVulnerability, undocumented DEFEND speedDelta behavior | Removed dead code, added documentation comments, added interior bracket tests |
| `POST /api/game/new` requires body (found during E2E) | E2E helper updated to send `payload: {}` |
| Security MEDIUM-1: unbounded actions array | Fixed with `maxItems: 10` in declare endpoint schema |
| `GameState` has no `combatHistory` field | `endCombat` records result in conversationLog with sentinel `npcId: '_combat_system'` |
| `_appendActionResult` in pipeline is a stub | Round Manager reconstructs result data from stamina deltas; full AttackResult surfacing deferred |

---

## Required User Steps

> **CRITICAL BEFORE PITCH:** Verify `src/fixtures/encounter.json` stats against the Excel "Battle Scenarios" sheet. Current stats are representative values derived from documentation — they have not been verified against the actual spreadsheet.

> **Formula verification:** The `calculateBaseDamage` formula uses a power-ratio model (`attackerPower * (attackerPower / targetPower)`). Verify this against `Math!A40:AM54` in `GM Combat Tracker.xlsx`. Test values in `src/combat/formulas.test.ts` are marked with a "pending Excel verification" comment.

> **No server commands needed:** All tests use Vitest. All API tests use Fastify injection (no HTTP server startup required).

---

## Deviations from Specs

| Deviation | Rationale |
|-----------|-----------|
| `calculateBaseDamage` formula inferred (power-ratio model) | Excel cell references not accessible programmatically. Formula is plausible and behaviorally consistent with docs. Needs user verification. |
| `endCombat` uses conversationLog sentinel instead of dedicated combatHistory field | `GameState` interface has no `combatHistory` field from Sprint 1 rebuild. Workaround preserves type safety without schema change. |
| `_appendActionResult` in pipeline is a stub (Round Manager reconstructs from deltas) | Pipeline spec defines the function as the owner; Round Manager owns round history assembly. No user-visible behavior difference. |
| Enemy archetypes use `"enemy"` (falls through to stub AI) | Behavior tree profiles exist for Elena/Lars/Kade only. Enemy AI uses stubBehaviorTree (ATTACK first live target). Appropriate for demo. |
