# Test Strategy: Sprint 4 — Persistence & API Completion

**Date:** 2026-03-08
**Framework:** Vitest 3.0.8 (`vitest run`)
**Config:** `vitest.config.ts` — includes `src/**/*.test.ts`, `tests/**/*.test.ts`

## Test Files

### File 1: Unit — updateTeamComposition
- **Target:** `src/state/stateUpdaters.test.ts` (append to existing)
- **Type:** Unit
- **Source file:** `src/state/stateUpdaters.ts` (lines 300-328)
- **Tests:**
  1. `sets team to provided NPC IDs` — valid 2-ID array returns new state with team set
  2. `returns a new object (reference inequality)` — immutability check
  3. `does not mutate the input state` — original state unchanged
  4. `updates timestamp` — timestamp advances
  5. `preserves other fields (player, npcs, conversationLog)` — no side effects
  6. `rejects empty array` — throws TEAM_COMPOSITION_INVALID
  7. `rejects array with 1 element` — throws TEAM_COMPOSITION_INVALID
  8. `rejects array with 3 elements` — throws TEAM_COMPOSITION_INVALID
  9. `rejects unknown NPC ID` — throws TEAM_COMPOSITION_INVALID with ID in message
  10. `rejects duplicate NPC IDs` — throws TEAM_COMPOSITION_INVALID
  11. `thrown error has code TEAM_COMPOSITION_INVALID` — error.code check
- **Mocks:** None (pure function, uses createNewGameState())

### File 2: Unit — validateGameState deep validation
- **Target:** `src/persistence/saveLoad.test.ts` (append to existing)
- **Type:** Unit
- **Source file:** `src/persistence/saveLoad.ts`
- **Tests (personality depth — Phase 2):**
  1. `rejects personality trait below minimum 5` — trait at 3
  2. `rejects personality trait above maximum 35` — trait at 40
  3. `rejects personality sum not equal to 100` — sum at 90
  4. `returns specific error messages for personality failures` — check errors array content
  5. `accepts personality at boundary values (5 and 35)` — edge case pass
- **Tests (combat state — Phase 5):**
  6. `accepts valid combatState shape` (already exists, verify)
  7. `rejects combatState with invalid phase value` — bad enum
  8. `rejects combatState with invalid status value` — bad enum
  9. `rejects combatState with missing arrays` — no playerParty/enemyParty
  10. `rejects combatState with non-number round` — type mismatch
- **Tests (team validation — Phase 4):**
  11. `accepts team: []` (empty team)
  12. `accepts team with 2 string entries`
  13. `rejects team with 1 entry` — wrong length
  14. `rejects team with 3 entries` — wrong length
  15. `rejects team with non-string entries`
  16. `accepts missing team field (backward compat)`
- **Tests (narrative deep — Phase 6):**
  17. `rejects narrativeState with non-boolean choiceFlags value`
  18. `rejects narrativeState with non-string visitedSceneIds entry`
  19. `rejects narrativeState sceneHistory entry with non-string sceneId`
  20. `rejects narrativeState sceneHistory entry with non-number timestamp`
- **Tests (ValidationResult shape):**
  21. `returns { valid: true } for valid state`
  22. `returns { valid: false, errors: [...] } with accumulated errors`
- **Mocks:** None (pure function, uses makeGameState() fixture)

### File 3: Integration — Sprint 4 API endpoints
- **Target:** `tests/sprint4.integration.test.ts` (new file)
- **Type:** Integration
- **Source files:** `src/api/game.ts`, `src/api/player.ts`, `src/api/npc.ts`
- **Pattern:** `buildApp()` + Fastify `inject()` (same as `sprint1.integration.test.ts`)
- **Tests (GET /api/game/saves — T1):**
  1. `returns 200 with 10 save slot entries` — no game needed
  2. `all slots empty on fresh app` — each slot exists: false
  3. `reflects saved slot after POST /save` — save then check list
- **Tests (DELETE /api/game/saves/:slot — T1):**
  4. `returns 200 with deleted: true for existing save` — save, delete, verify
  5. `returns 400 for invalid slot (0, 11, abc)` — INVALID_SLOT
  6. `returns error for non-existent slot` — SAVE_NOT_FOUND
- **Tests (GET /api/player/personality — T2):**
  7. `returns 200 with personality when game active` — create game, get personality
  8. `returns 404 when no active game` — GAME_NOT_FOUND
  9. `personality has 6 traits summing to ~100` — constraint check
- **Tests (POST /api/player/team — T2):**
  10. `returns 200 with team when valid 2 NPC IDs provided` — happy path
  11. `returns 404 when no active game` — GAME_NOT_FOUND
  12. `returns 400 when combat is active (D1 USER decision)` — team lock during combat
  13. `returns 400 when narrative is active (D1 USER decision)` — team lock during narrative
  14. `returns 400 for invalid NPC IDs` — schema/business validation
  15. `returns 400 for wrong number of NPCs` — schema validation (minItems/maxItems)
- **Tests (GET /api/npc/:id — T2):**
  16. `returns live NPC data when game is active (D3)` — affection/trust from live state
  17. `returns template NPC data when no game active` — falls back to getNPC()
  18. `returns 404 for unknown NPC ID` — NPC_NOT_FOUND
- **Mocks:** None (uses buildApp() for real Fastify instance with inject())

## Mock Requirements

No external mocking needed. All tests use:
- `createNewGameState()` for unit test fixtures
- `makeGameState()` (existing test helper) for saveLoad tests
- `buildApp()` + `fastify.inject()` for integration tests
- `fs.mkdtemp()` for temp directories in saveLoad tests

## Framework Command

```bash
npx vitest run src/state/stateUpdaters.test.ts src/persistence/saveLoad.test.ts tests/sprint4.integration.test.ts
```

## Estimated Test Count

- updateTeamComposition: 11 unit tests
- validateGameState deep: 22 unit tests
- Sprint 4 integration: 18 integration tests
- **Total: ~51 tests** (11 unit + 22 unit + 18 integration)

## Coverage Target

No configured threshold. Target: 80%+ line coverage on Sprint 4 modified files.

## Specialist Recommendations

Blueprint Section 9 states: "No new test files are specified." No specialist test recommendations to incorporate. Test strategy designed independently based on acceptance criteria and code analysis.

## Acceptance Criteria Coverage

| AC | Covered By |
|----|-----------|
| T1.1: GET /saves returns list | Integration #1-3 |
| T1.2: DELETE /saves/:slot | Integration #4-6 |
| T1.3: Consistent ErrorCodes | Integration #5-6 |
| T1.4: Existing endpoints unchanged | Existing sprint1 tests (regression) |
| T2.1: GET /personality returns traits | Integration #7-9 |
| T2.2: POST /team accepts NPC selection | Integration #10 |
| T2.3: Party size constraint | Unit #6-8, Integration #15 |
| T2.4: GET /npc/:id includes affection/trust | Integration #16-17 |
| T2.5: State updater follows pattern | Unit #1-5 |
| T3.1: Personality validation | Unit (saveLoad) #1-5 |
| T3.2: Combat state validation | Unit (saveLoad) #6-10 |
| T3.3: Narrative state deep validation | Unit (saveLoad) #17-20 |
| T3.4: Typed error with details | Unit (saveLoad) #21-22 |
| T3.5: Sprint 3 narrative preserved | Existing saveLoad tests (regression) |
