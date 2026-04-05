# Test Strategy — Sprint 5: Frontend React Phase 1

## Prerequisites

No test framework exists in `client/`. Must install:
- `vitest` (consistent with backend)
- `@testing-library/react` (React component testing)
- `@testing-library/jest-dom` (DOM matchers)
- `@testing-library/user-event` (user interaction simulation)
- `jsdom` (DOM environment for Vitest)

Configure `client/vitest.config.ts` with jsdom environment and setup file.

## Deliverable Classification

### Code (testable — 10 files)
| File | Category | Rationale |
|------|----------|-----------|
| `client/src/api/apiClient.ts` | Code | Fetch wrapper with error handling logic |
| `client/src/context/GameStateContext.tsx` | Code | State management provider with fetch flows |
| `client/src/components/ErrorBanner.tsx` | Code | Interactive component with dismiss |
| `client/src/components/LoadingScreen.tsx` | Code | Presentational component |
| `client/src/components/StaminaBar.tsx` | Code | Computed derived state + data attributes |
| `client/src/components/EnergyDisplay.tsx` | Code | Computed segments + data attributes |
| `client/src/components/PersonalityBreakdown.tsx` | Code | Stateful toggle + trait rendering |
| `client/src/components/TeamSelector.tsx` | Code | Complex selection logic + API interaction |
| `client/src/components/EquinoxHUD.tsx` | Code | Conditional rendering + composition |
| `client/src/App.tsx` | Code | Root composition (tested indirectly via EquinoxHUD tests) |

### Non-code (skipped — 18 files)
| File | Type | Reason |
|------|------|--------|
| `package.json` (root edit) | Config | devDependency + script addition |
| `client/package.json` | Config | Package manifest |
| `client/tsconfig.json` | Config | TypeScript configuration |
| `client/tsconfig.node.json` | Config | TypeScript configuration |
| `client/vite.config.ts` | Config | Build tooling configuration |
| `client/index.html` | Static HTML | Entry HTML template |
| `client/src/main.tsx` | Entry point | 5-line React.createRoot — no logic |
| `client/src/types/index.ts` | Type definitions | Pure interfaces, no runtime code |
| `client/src/styles/tokens.css` | Static CSS | Design tokens |
| `client/src/styles/reset.css` | Static CSS | Browser reset |
| 8x `*.module.css` | Static CSS | Component styles |

## AC Coverage Matrix

### T1: Initialize Vite + React project
| AC | Test | Tier | Notes |
|----|------|------|-------|
| AC1 (dev server renders) | N/A | — | Infrastructure verification, manual |
| AC2 (API proxy) | N/A | — | Infrastructure verification, manual |
| AC3 (TypeScript strict) | N/A | — | Build verification, manual |
| AC4 (concurrent servers) | N/A | — | Infrastructure verification, manual |

### T2: Design tokens
| AC | Test | Tier | Notes |
|----|------|------|-------|
| AC1-AC3 | N/A | — | Non-code (CSS), not testable |

### T3: GameStateProvider
| AC | Test(s) | Tier | Level |
|----|---------|------|-------|
| AC1 (fetch on mount) | `fetches game state on mount`, `auto-creates game on 404` | Tier 1 | Context provider |
| AC2 (child access) | `exposes gameState via hook`, `derives npcs from gameState` | Tier 1 | Context provider |
| AC3 (action methods) | `setTeam calls API and refreshes state` | Tier 1 | Context provider |
| AC4 (loading/error) | `shows loading during fetch`, `exposes error on failure`, `dismissError clears error` | Tier 1 | Context provider |

### T4: Stamina bar and energy display
| AC | Test(s) | Tier | Level |
|----|---------|------|-------|
| AC1 (stamina % + color) | `renders percentage`, `5 threshold state tests` | Tier 1/2 | Component |
| AC2 (CSS data-attr colors) | `sets data-state attribute per threshold` | Tier 2 | Component |
| AC3 (energy segments) | `renders filled/empty segments`, `0/partial/max` | Tier 1/2 | Component |
| AC4 (updates on change) | Covered by re-render with new props | Tier 1 | Component |

### T5: Stance, personality, path selector
| AC | Test(s) | Tier | Level |
|----|---------|------|-------|
| AC1 (stance) | DEFERRED per D2 | — | — |
| AC2 (personality toggle) | `collapsed by default`, `expands on click`, `collapses on second click`, `shows 6 traits` | Tier 1 | Component |
| AC3 (team selector) | `renders 3 NPCs`, `select exactly 2`, `disabled state` | Tier 1 | Component |
| AC4 (team API + update) | `confirm calls onSetTeam` | Tier 1 | Component |

### T6: Compose HUD shell
| AC | Test(s) | Tier | Level |
|----|---------|------|-------|
| AC1 (cohesive layout) | `renders personality and team sections` | Tier 1 | Composed component |
| AC2 (armor frame) | Visual — not testable | — | — |
| AC3 (interactive elements) | `error banner dismissible`, `team selector functional` | Tier 1 | Composed component |
| AC4 (live backend data) | `renders with GameStateProvider` | Tier 1 | Composed component |

## Test Files

| # | File | Tier | Target | Tests |
|---|------|------|--------|-------|
| 1 | `client/src/api/apiClient.test.ts` | 1, 2 | apiClient.ts | 7 |
| 2 | `client/src/context/GameStateContext.test.tsx` | 1, 2 | GameStateContext.tsx | 10 |
| 3 | `client/src/components/StaminaBar.test.tsx` | 1, 2 | StaminaBar.tsx | 8 |
| 4 | `client/src/components/EnergyDisplay.test.tsx` | 1, 2 | EnergyDisplay.tsx | 5 |
| 5 | `client/src/components/PersonalityBreakdown.test.tsx` | 1, 2 | PersonalityBreakdown.tsx | 5 |
| 6 | `client/src/components/TeamSelector.test.tsx` | 1, 2 | TeamSelector.tsx | 9 |
| 7 | `client/src/components/EquinoxHUD.test.tsx` | 1, 2 | EquinoxHUD.tsx | 7 |
| 8 | `client/src/components/ErrorBanner.test.tsx` | 2 | ErrorBanner.tsx | 3 |

## Tier Distribution

| Tier | Count | % | Target |
|------|-------|---|--------|
| Tier 1 (AC tests) | 27 | 50% | ≥40% ✓ |
| Tier 2 (blueprint contracts) | 27 | 50% | — |
| Tier 3 (coverage) | 0 | 0% | ≤30% ✓ |
| **Total** | **54** | 100% | — |

## Negative Test Inventory

| Test File | Negative Tests | Description |
|-----------|---------------|-------------|
| apiClient.test.ts | 4 | non-ok HTTP, success:false, network error, non-JSON response |
| GameStateContext.test.tsx | 4 | fetch error, auto-create failure, setTeam error, refreshState error |
| StaminaBar.test.tsx | 2 | max=0 edge case, 0% KO state |
| EnergyDisplay.test.tsx | 1 | 0 energy (all empty) |
| TeamSelector.test.tsx | 3 | can't select 3rd, confirm disabled <2, disabled prop |
| EquinoxHUD.test.tsx | 2 | no combatState hides combat, error banner display |

**Total negative:** 16 of 54 Tier 1/2 = 30% ✓ (target ≥30%)

## Mock Strategy

| Module | Mock Target | Approach |
|--------|------------|----------|
| apiClient.ts | `global.fetch` | `vi.fn()` replacing window.fetch |
| GameStateContext.tsx | `apiClient` module | `vi.mock('../api/apiClient')` |
| Component tests | GameStateContext | Custom wrapper providing mock context values |
| TeamSelector.tsx | `onSetTeam` prop | `vi.fn()` returning Promise |

## Framework Command

```bash
cd client && npx vitest run
```

## Mutation Spot-Check Candidates

1. **StaminaBar.tsx** — Change threshold `60` to `70` (should break green/yellow boundary test)
2. **GameStateContext.tsx** — Change `GAME_NOT_FOUND` error code check (should break 404-auto-create test)
3. **TeamSelector.tsx** — Change `selectedIds.size === 2` to `=== 1` (should break canSubmit test)

## Specialist Recommendations Incorporated

| Recommendation | Incorporated | Notes |
|----------------|-------------|-------|
| apiClient: mock fetch, test network/non-JSON/error envelope | ✓ | Tests 1-7 in apiClient.test.ts |
| GameStateProvider: test 404→create, loading/error, context values | ✓ | Tests 1-10 in GameStateContext.test.tsx |
| StaminaBar: test 5 threshold boundaries, data-state changes | ✓ | Tests 1-8 in StaminaBar.test.tsx |
| EnergyDisplay: test 0/partial/max, maxEnergy from data | ✓ | Tests 1-5 in EnergyDisplay.test.tsx |
| PersonalityBreakdown: test toggle, 6 traits, percentages | ✓ | Tests 1-5 in PersonalityBreakdown.test.tsx |
| TeamSelector: test NPC listing, exactly-2, API confirm, disabled | ✓ | Tests 1-9 in TeamSelector.test.tsx |
| ErrorBanner: verify shows on failure, dismisses | ✓ | Tests 1-3 in ErrorBanner.test.tsx |

## Spec Ambiguities

- T1 ACs are all infrastructure verification — not unit-testable, noted as manual verification
- T2 ACs are CSS token definitions — non-code, skipped
- StaminaBar thresholds: outline says 75/50/25, build used 60/40/20 per user approval — test against 60/40/20
