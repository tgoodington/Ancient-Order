# Build Report

**Plan:** Sprint 5 — Frontend React Phase 1 (Equinox HUD)
**Date:** 2026-03-22
**Status:** Success

---

## Task Results

### Task 1–6: Frontend Component Architecture (all tasks, single phase)
- **Domain**: frontend/ui
- **Specialist**: frontend-component
- **Producer**: code-writer (source)
- **Output**: 27 new files in `client/` + 1 root `package.json` edit (28 total)
- **Status**: PASS

#### Review Chain
1. **Specialist Review** (frontend-component): PASS — One issue found (T6-2: isInNarrative used an incorrect type cast bypassing the typed `GameState.narrativeState` field). Fixed by producer before Layer 2.
2. **Builder Verification**: PASS — All 6 tasks' acceptance criteria verified. One unanticipated human-facing decision escalated to user (stamina thresholds — see Decision Compliance). Two internal deviations logged below.
3. **Cross-Cutting Review** (N/A — no mandatory_reviewers on frontend-component): N/A
4. **Security Review**: PASS — No critical/high/medium findings. Two low-severity observations (dependency pinning, inline styles vs. future CSP).

#### Deviations from Blueprint

1. **isInCombat redundant guard** — `EquinoxHUD.tsx` line 14 added `&& gameState?.combatState !== undefined` to the blueprint's spec of `gameState?.combatState !== null`. The `CombatState | null` type can never be `undefined`. Internal/technical — no user impact. Fixed by specialist review feedback: was classified as minor by reviewer (no rewrite needed since runtime behavior is identical). Logged for completeness.

2. **EquinoxHUD title fallback** — `EquinoxHUD.tsx` line 26 uses `gameState?.player.name ?? 'Ancient Order'` instead of the blueprint's `{gameState.player.name}`. This prevents a null-reference error when `gameState` is null during initial loading (the `<h2>` is outside the `{!loading && gameState && ...}` guard). Internal technical improvement — correct behavior. Blueprint spec would throw on null access.

3. **tsconfig.json extra options** — `client/tsconfig.json` includes `moduleDetection: "force"`, `allowImportingTsExtensions: true`, `noEmit: true` not specified in the blueprint. Consistent with a Vite + TypeScript setup. No functional impact.

#### Decision Compliance

- **[USER] decisions honored**: 5 of 5
  - D1 (combat-conditional HUD): ✓ StaminaBar/EnergyDisplay render only when `playerCombatant` is non-null
  - D2 (StanceIndicator deferred): ✓ No StanceIndicator.tsx produced
  - D3 (Team selector reinterpretation): ✓ TeamSelector.tsx (not PathSelector)
  - D4 (gradient fill): ✓ CSS `linear-gradient` via `data-state` attribute selectors
  - D5 (click-to-toggle personality): ✓ `useState(false)` toggle on header button click

- **[SPEC] decisions applied**: 2
  - D6 (duplicate types in frontend): ✓ `client/src/types/index.ts` with subset of backend types
  - D7 (top-level error banner): ✓ `ErrorBanner.tsx` with dismissible alert

- **Unanticipated decisions**: 1 human-facing (escalated and resolved)
  - **Stamina thresholds**: Blueprint used >60%/>40%/>20%/>0% (symmetric 20% bands) instead of the outline's 75%/50%/25% thresholds. Escalated to user during Layer 2. **User accepted the specialist's version (60/40/20)**. No fix applied.

#### External Dependencies
- Run `npm install` in `client/` to install React 19, Vite 6, TypeScript
- Run `npm run dev` in `client/` to verify Vite dev server starts at `http://localhost:5173`
- Run `npm run dev:all` from root to start both backend (port 3000) and frontend (port 5173) concurrently
- [VERIFY] Confirm Vite 6 + React 19 peer dependency compatibility at install time
- [VERIFY] Confirm `player_` prefix convention in playerParty (based on `src/state/gameState.ts` player id pattern)
- [VERIFY] Confirm `concurrently@^9.0.0` is current major version

---

## Files Modified

**Edited:**
- `package.json` — added `"concurrently": "^9.0.0"` devDependency and `"dev:all"` script

**New files — client/ (27):**
- `client/package.json`
- `client/tsconfig.json`
- `client/tsconfig.node.json`
- `client/vite.config.ts`
- `client/index.html`
- `client/src/types/index.ts`
- `client/src/api/apiClient.ts`
- `client/src/styles/tokens.css`
- `client/src/styles/reset.css`
- `client/src/context/GameStateContext.tsx`
- `client/src/components/ErrorBanner.tsx`
- `client/src/components/ErrorBanner.module.css`
- `client/src/components/LoadingScreen.tsx`
- `client/src/components/LoadingScreen.module.css`
- `client/src/components/StaminaBar.tsx`
- `client/src/components/StaminaBar.module.css`
- `client/src/components/EnergyDisplay.tsx`
- `client/src/components/EnergyDisplay.module.css`
- `client/src/components/PersonalityBreakdown.tsx`
- `client/src/components/PersonalityBreakdown.module.css`
- `client/src/components/TeamSelector.tsx`
- `client/src/components/TeamSelector.module.css`
- `client/src/components/EquinoxHUD.tsx`
- `client/src/components/EquinoxHUD.module.css`
- `client/src/App.tsx`
- `client/src/App.module.css`
- `client/src/main.tsx`

---

## Test Deliverables Deferred

The blueprint's Section 3 (Testability Notes) documents test strategy intent per component, but no test files were specified as deliverables in Section 9 (Producer Handoff). The test phase will use these testability notes as advisory input for its own test strategy.

| Blueprint Source | Component | Testability Notes |
|-----------------|-----------|-------------------|
| frontend-component.md | apiClient.ts | Mock fetch; test network failure, non-JSON response, `success: false` envelope |
| frontend-component.md | GameStateProvider | Test 404-then-create flow, loading/error states, context value propagation |
| frontend-component.md | StaminaBar | Test all 5 color threshold boundaries; exact boundary values for `data-state` changes |
| frontend-component.md | EnergyDisplay | Test filled vs empty segments at 0, partial, max; verify maxEnergy from data |
| frontend-component.md | PersonalityBreakdown | Test expand/collapse toggle; all 6 traits render; percentage values display |
| frontend-component.md | TeamSelector | Test NPC listing, exactly-2 selection, API call on confirm, disabled state |
| frontend-component.md | ErrorBanner | Verify shows on API failure, dismisses correctly |

---

## Process Flow Deviations

N/A — no process_flow.md exists for this sprint.

---

## Vision Alignment

| Success Criterion | Addressed By | Status |
|---|---|---|
| React app builds, runs, connects to Fastify backend on port 3000 | `client/vite.config.ts` (proxy), `client/package.json` (scripts) | Covered |
| GameStateProvider fetches and manages state from all relevant API endpoints | `client/src/context/GameStateContext.tsx` (GET /api/game/state, POST /api/game/new, POST /api/player/team) | Covered |
| Equinox HUD renders stamina bar (5-state color coding) | `client/src/components/StaminaBar.tsx` + `StaminaBar.module.css` | Covered |
| Equinox HUD renders energy segments (6 max) | `client/src/components/EnergyDisplay.tsx` | Covered |
| Equinox HUD renders current stance indicator | Deferred to Sprint 7 per D2 (StanceIndicator) | Deferred |
| Expandable personality breakdown with 6 traits | `client/src/components/PersonalityBreakdown.tsx` | Covered |
| Path selector allows switching between two paths | `client/src/components/TeamSelector.tsx` (reinterpreted as team selector per D3) | Covered — reinterpreted |
| All HUD interactions functional | PersonalityBreakdown click-to-toggle + TeamSelector selection/confirm | Covered |
| Visual aesthetic matches armor/leather design spec | `client/src/styles/tokens.css` (leather browns, metal grays) + CSS modules | Covered |

**Non-negotiables check:**

| Non-negotiable | Assessment |
|---|---|
| HUD must connect to real backend API (not mock data) | Met — GameStateProvider uses live fetch to /api/game/state with no mock fallback |
| All HUD elements from spec must be implemented and interactive | Mostly met — StanceIndicator deferred to Sprint 7 per specialist decision D2 (no clean data source outside combat) |
| Visual aesthetic matches armor/leather design language | Met — CSS custom properties define leather browns (#1a1410, #2a2118), metal grays (#c0b8a8, #7a7060, #4a4238), armor frame borders |

---

## Issues & Resolutions

1. **T6-2: isInNarrative type cast** — EquinoxHUD used `(gameState as { narrativeState?: unknown } | null)?.narrativeState` bypassing the typed field. Fixed by producer on specialist reviewer feedback: replaced with `gameState?.narrativeState != null`.

2. **Stamina threshold deviation** — Specialist changed thresholds from outline's 75%/50%/25% to symmetric 60%/40%/20%. Escalated to user mid-build during Layer 2 verification. User accepted the specialist's version. No code change.

---

## Required User Steps

1. `cd client && npm install` — install dependencies (React 19, Vite 6, TypeScript 5.7)
2. Ensure backend is running: `npm run dev` from root (port 3000)
3. `npm run dev` from `client/` to start Vite at `http://localhost:5173`
4. Verify HUD renders in browser with live backend data
5. Alternatively: `npm run dev:all` from root to start both servers concurrently
6. [VERIFY] Check peer dependency compatibility output from `npm install` — Vite 6 + React 19
