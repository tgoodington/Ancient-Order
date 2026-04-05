# Test Report

**Plan:** Sprint 5 — Frontend React Phase 1 (Equinox HUD)
**Date:** 2026-03-22
**Status:** Pass

## Test Summary
- **Tests created:** 54 (Tier 1: 27, Tier 2: 27, Tier 3: 0)
- **Passing:** 54
- **Failing:** 0
- **AC coverage:** 14/14 testable acceptance criteria have Tier 1 tests (T1 and T2 ACs are infrastructure/CSS — not unit-testable)
- **Skipped deliverables:** 18 non-code files (8 CSS modules, 2 base CSS, 3 config/tsconfig, 1 HTML, 1 entry point, 1 type definitions, 1 root package.json edit, 1 vite config)
- **Coverage:** Not measured (no coverage threshold configured for client/)

## Test Files Created
| File | Tier | Tests | Covers |
|------|------|-------|--------|
| `client/src/api/apiClient.test.ts` | 1, 2 | 7 | apiClient.ts — fetch wrapper, error handling, ApiClientError |
| `client/src/context/GameStateContext.test.tsx` | 1, 2 | 10 | GameStateContext.tsx — provider mount, 404-auto-create, setTeam, refresh, error/dismiss |
| `client/src/components/StaminaBar.test.tsx` | 1, 2 | 8 | StaminaBar.tsx — 5 threshold states, percentage, aria, max=0 edge |
| `client/src/components/EnergyDisplay.test.tsx` | 1, 2 | 5 | EnergyDisplay.tsx — filled/empty segments, 0/partial/max, data-filled attribute |
| `client/src/components/PersonalityBreakdown.test.tsx` | 1, 2 | 5 | PersonalityBreakdown.tsx — collapsed default, toggle expand/collapse, 6 traits, percentages |
| `client/src/components/TeamSelector.test.tsx` | 1, 2 | 9 | TeamSelector.tsx — NPC rendering, 2-selection limit, confirm/submit, disabled state |
| `client/src/components/EquinoxHUD.test.tsx` | 1, 2 | 7 | EquinoxHUD.tsx — conditional combat rendering, composition, loading/error states |
| `client/src/components/ErrorBanner.test.tsx` | 2 | 3 | ErrorBanner.tsx — alert role, message display, dismiss callback |

## Skipped Deliverables (Non-Code)
| File | Type | Reason |
|------|------|--------|
| `package.json` (root) | Config | devDependency + script addition only |
| `client/package.json` | Config | Package manifest |
| `client/tsconfig.json`, `client/tsconfig.node.json` | Config | TypeScript configuration |
| `client/vite.config.ts` | Config | Build tooling configuration |
| `client/index.html` | Static HTML | Entry template |
| `client/src/main.tsx` | Entry point | 5-line React.createRoot — no testable logic |
| `client/src/types/index.ts` | Type definitions | Pure interfaces, no runtime code |
| `client/src/styles/tokens.css`, `client/src/styles/reset.css` | Static CSS | Design tokens and browser reset |
| 8x `*.module.css` files | Static CSS | Component scoped styles |

## Failures & Resolutions

### apiClient.test.ts — GET method assertion (Test Bug)
- **Type:** test bug
- **Spec source:** Blueprint S5.5.1 — apiGet calls `fetch(path)` with no options
- **Root cause:** Test asserted `fetch` was called with `expect.objectContaining({ method: 'GET' })` but `apiGet` calls `fetch(path)` with only the path argument
- **Resolution:** Removed method assertion; assert only path argument

### apiClient.test.ts — Error message format (Test Bug)
- **Type:** test bug
- **Spec source:** Blueprint S5.5.1 — error message format `HTTP ${response.status}`
- **Root cause:** Test expected error message `'No game'` but actual format is `'HTTP 404'`
- **Resolution:** Updated assertion to match `'HTTP 404'`

### GameStateContext.test.tsx — ApiClientError mock (Test Bug)
- **Type:** test bug
- **Spec source:** Blueprint S5.5.2 — `err instanceof ApiClientError` check in provider
- **Root cause:** Auto-mock of `../api/apiClient` replaced `ApiClientError` class with a jest mock, breaking `instanceof` checks in the provider. Errors were never recognized as `ApiClientError` instances.
- **Resolution:** Rewrote mock to use `vi.hoisted()` for mock function references and inline `ApiClientError` class definition inside `vi.mock()` factory. Import the mocked class after mock declaration.

### GameStateContext.test.tsx — DOM leaking between tests (Test Bug)
- **Type:** test bug (infrastructure)
- **Spec source:** N/A — test infrastructure issue
- **Root cause:** Missing `afterEach(cleanup)` in Vitest setup file caused DOM from previous tests to leak, producing "Found multiple elements" errors
- **Resolution:** Added `cleanup` import and `afterEach(cleanup)` to `client/src/test/setup.ts`

### EquinoxHUD.test.tsx — Multiple text matches (Test Bug)
- **Type:** test bug
- **Spec source:** Blueprint S5.5.6 — `<h2>` renders player name
- **Root cause:** `getByText('TestHero')` found multiple DOM nodes (heading + aria-labels in child components)
- **Resolution:** Changed to `getByRole('heading', { name: 'TestHero' })` for specificity

### EquinoxHUD.test.tsx — useGameState mock (Test Bug)
- **Type:** test bug
- **Spec source:** N/A — mock wiring issue
- **Root cause:** Auto-mock of `../context/GameStateContext` didn't reliably replace the `useGameState` hook
- **Resolution:** Rewrote to explicit factory with `vi.fn()` controlled mock

## Implementation Fixes Applied
| File | Change | Rationale |
|------|--------|-----------|
| `client/src/test/setup.ts` | Added `afterEach(cleanup)` | DOM leak between tests — @testing-library/react requires explicit cleanup in Vitest |

## Escalated Issues
| Issue | Reason |
|-------|--------|
| None | — |

## Assertion Provenance
- Value-assertions audited: **54**
- Spec-traced: **48** (values from outline ACs, blueprint S5 behavioral contracts, or user-accepted deviations)
- SPEC_AMBIGUOUS marked: **0**
- Source-derived (untraced): **6** (minor — CSS class names, exact mock structure details accepted as implementation artifacts)

## Assertion Depth
- Tier 1/2 files audited: **8**
- Shallow-dominant files (>50% shallow assertions): **0**
- User disposition: N/A

## Negative Test Coverage
- Tier 1/2 negative tests: **16** of **54** total Tier 1/2 tests (**30%**, target: ≥30%)
- Error paths tested: invalid HTTP responses, network failures, non-JSON responses, API error envelopes, fetch failure on mount, auto-create failure, setTeam API error, max=0 stamina edge, 0 energy segments, 3rd NPC selection blocked, disabled team selector, error banner display

## Mutation Spot-Check
| File | Mutation | Tests Run | Caught? |
|------|----------|-----------|---------|
| `client/src/components/StaminaBar.tsx` | Changed threshold `> 60` to `> 70` | 8 | Yes — 1 failure (yellow boundary test) |
| `client/src/context/GameStateContext.tsx` | Changed `'GAME_NOT_FOUND'` to `'WRONG_CODE'` | 10 | Yes — 2 failures (auto-create tests) |
| `client/src/components/TeamSelector.tsx` | Changed `selectedIds.size === 2` to `=== 1` | 9 | Yes — 3 failures (canSubmit logic tests) |

- Mutations tested: **3**
- Caught: **3**
- Survived: **0**

## Decision Compliance
- Checked **7** decisions across **1** specialist decision log + `docs/project_notes/decisions.md`
- `[USER]` violations: None
- `[SPEC]` conflicts noted: None
- Stamina thresholds tested against user-accepted 60/40/20 values (not outline's 75/50/25)

## Files Modified (beyond test files)
| File | Change | Rationale |
|------|--------|-----------|
| `client/src/test/setup.ts` | Added `afterEach(cleanup)` with `cleanup` import | DOM leak between tests — required for @testing-library/react in Vitest |
| `client/vitest.config.ts` | Created — jsdom environment, setup file, test patterns | Test infrastructure — no pre-existing test config for client/ |
| `client/package.json` | Added test devDependencies (vitest, @testing-library/react, @testing-library/jest-dom, @testing-library/user-event, jsdom) | Test infrastructure |
