# Detail Brief

## Current Specialist
- **Name**: frontend-component
- **Display Name**: Frontend Component
- **Domain**: frontend/ui
- **Profile Path**: C:\Users\taylo\.claude\specialists\frontend-component\frontend-component.specialist.md

## Assigned Tasks

### Task T1: Initialize Vite + React project in client/
- **Depth**: Light
- **Description**: Create the `client/` directory with its own `package.json`, `tsconfig.json` (extending root with DOM libs and bundler module resolution), and `vite.config.ts` (with proxy to `http://localhost:3000/api`). Install React, ReactDOM, Vite, and `@vitejs/plugin-react`. Set up the entry point (`main.tsx`, `App.tsx`) rendering a placeholder. Add a root-level `dev:all` script using `concurrently` to run both backend and frontend dev servers.
- **Acceptance Criteria**:
  1. `npm run dev` in `client/` starts Vite dev server and renders a React component in the browser
  2. API requests to `/api/*` from the frontend are proxied to `http://localhost:3000` without CORS issues
  3. TypeScript compilation succeeds with strict mode and DOM type support
  4. Root-level script can start both backend and frontend dev servers concurrently
- **Dependencies**: None
- **Decisions**: None flagged (Light — autonomous)
- **Files**: `client/package.json`, `client/tsconfig.json`, `client/vite.config.ts`, `client/src/main.tsx`, `client/src/App.tsx`, `client/index.html`

### Task T2: Create design token system and base styles
- **Depth**: Light
- **Description**: Define CSS custom properties for the armor/leather/metal color palette, typography, and spacing. Create a base stylesheet that resets defaults and establishes the game's dark, textured visual foundation. Include stamina color tokens for all 5 states.
- **Acceptance Criteria**:
  1. CSS custom properties define the complete color palette (leather browns, metal grays, stamina Green/Yellow/Orange/Red/Black)
  2. Base styles reset browser defaults and set the dark game-world background
  3. All downstream components can reference design tokens via `var(--token-name)`
- **Dependencies**: Task 1
- **Decisions**: None flagged (Light — autonomous)
- **Files**: `client/src/styles/tokens.css`, `client/src/styles/reset.css`

### Task T3: Build GameStateProvider context
- **Depth**: Standard
- **Description**: Create a React context provider that fetches game state from the backend API on mount and exposes it to child components. Provide action methods that call API endpoints and re-fetch state on completion. Handle loading and error states. Parse `ApiResponse<T>` envelope consistently.
- **Acceptance Criteria**:
  1. Provider fetches initial game state from relevant API endpoints on mount
  2. Child components can access current game state (stamina, energy, stance, personality traits, active path) via context hook
  3. Provider exposes action methods (e.g., switch path) that call backend API and refresh state on success
  4. Loading and error states are exposed for consumer components to handle
- **Dependencies**: Task 1
- **Decisions**:
  - `[SPEC]` Which API endpoints to call on initial mount and how to combine responses — specialist determines based on available endpoints
- **Files**: `client/src/context/GameStateProvider.tsx`, `client/src/context/useGameState.ts`

### Task T4: Implement Equinox HUD — stamina bar and energy display
- **Depth**: Standard
- **Description**: Build the stamina bar component with 5-state color coding (Green 100-75%, Yellow 74-50%, Orange 49-25%, Red 24-1%, Black 0% KO) driven by CSS data-attributes. Build the energy segment display showing current segments out of 6 max. Both consume live data from GameStateProvider.
- **Acceptance Criteria**:
  1. Stamina bar displays current percentage with correct color state based on thresholds
  2. Color transitions match the 5 defined states without JavaScript style injection
  3. Energy display shows filled vs empty segments (up to 6 max)
  4. Both components update when game state changes via the provider
- **Dependencies**: Tasks 2, 3
- **Decisions**:
  - `[USER]` Stamina bar visual treatment — solid fill, segmented bar, or gradient within each color state
- **Files**: `client/src/components/StaminaBar.module.css`, `client/src/components/StaminaBar.tsx`, `client/src/components/EnergyDisplay.module.css`, `client/src/components/EnergyDisplay.tsx`

### Task T5: Implement Equinox HUD — stance, personality, and path selector
- **Depth**: Standard
- **Description**: Build the stance indicator showing current stance (A/D/E/S/G), the expandable personality breakdown showing all 6 traits with percentages, and the path selector allowing switching between two paths. Path switching calls the backend API via GameStateProvider and refreshes state.
- **Acceptance Criteria**:
  1. Stance indicator displays current stance letter with visual differentiation per stance
  2. Personality section expands/collapses to show all 6 traits with their percentage values
  3. Path selector displays current active path and allows switching between two available paths
  4. Path switch triggers API call and HUD updates to reflect new path on success
- **Dependencies**: Tasks 2, 3
- **Decisions**:
  - `[USER]` Personality expansion interaction — click-to-toggle, hover-to-expand, or always-visible with compact/expanded modes
  - `[USER]` Path selector layout — two side-by-side buttons, a toggle switch, or the "press center to switch" interaction from the spec
- **Files**: `client/src/components/StanceIndicator.tsx`, `client/src/components/PersonalityBreakdown.tsx`, `client/src/components/PathSelector.tsx` (+ corresponding .module.css files)

### Task T6: Compose Equinox HUD shell and integrate all elements
- **Depth**: Light
- **Description**: Assemble the individual HUD components (stamina, energy, stance, personality, path) into the complete Equinox HUD shell component. Apply the armor/leather aesthetic to the outer frame. Wire into App.tsx with GameStateProvider wrapping.
- **Acceptance Criteria**:
  1. All HUD sub-components render together in a cohesive layout matching the minimal armor-like aesthetic
  2. HUD shell has a visually distinct frame/border treatment evoking in-world armor technology
  3. All interactive elements (expand personality, switch path) function within the composed HUD
  4. App.tsx renders the complete HUD wrapped in GameStateProvider, consuming live backend data
- **Dependencies**: Tasks 4, 5
- **Decisions**: None flagged (Light — autonomous)
- **Files**: `client/src/components/EquinoxHUD.tsx`, `client/src/components/EquinoxHUD.module.css`, `client/src/App.tsx`

## Decision Policy

Sole developer, hands-on. Visual design decisions (stamina bar treatment, personality expansion UX, path selector layout) are `[USER]`. API integration details are `[SPEC]`. Straightforward scaffolding and composition are `[SILENT]`.

## Known Research

### Discovery Summary
- Backend is complete (4 sprints, 1019 tests, full REST API) — no modifications allowed
- React app will live in `client/` subfolder consuming backend API endpoints (game state, player, personality, narrative)
- No authentication needed for Phase 1 single-player dev environment
- All HUD data available via existing API endpoints
- Fastify backend runs on port 3000; Vite dev server proxies `/api/*` to it

### Technology Decisions (Section 3)
- **Toolchain**: Vite + React in `client/` subfolder (ESM-native, fast HMR)
- **Styling**: CSS Modules + CSS custom properties (hand-crafted styles, zero runtime cost, scoped)
- **State**: On-demand fetch via React Context only — no Redux or external state library

### Inherited Decisions (from Sprint 4)
- Immutable state pattern (ADR-005) — frontend mirrors with immutable React context updates
- ESM modules (ADR-016) — Vite's ESM output aligns naturally
- Party size = 3 (player + 2 NPCs); personality = 6 traits, 5-35%, sum=100% enforced by backend
- `ApiResponse<T>` envelope — all backend endpoints return this standardized contract
- Module resolution divergence: backend uses NodeNext, frontend uses `bundler` — separate `tsconfig.json` required

### Relevant Risks
- API response shape vs frontend expectations: Low likelihood — Task 3 specialist reads actual API responses; Sprint 4 standardized `ApiResponse<T>`
- HUD aesthetic without designer: Medium — design tokens established early (Task 2); user is designer and reviews during build

## Prior Blueprints
None (first and only specialist in execution order)

## Outline Context
- **Domain-Specific**: All work is code/frontend (React + TypeScript + CSS Modules). Follow Vite conventions. CSS Modules use `*.module.css` naming. React components use named exports. State via React Context + `useContext` — no Redux.
- **Cross-Domain Dependencies**: GameStateProvider (T3) bridges backend API to all HUD components (T4-T6). Design tokens (T2) consumed by all component CSS. T4 and T5 are independent of each other but both depend on T2 and T3.
- **Sequencing**: T1 first → [T2, T3] parallel → [T4, T5] parallel → T6 last. DAG: 1 → [2, 3] → [4, 5] → 6.
- **Open Questions**: Which endpoints for initial mount (specialist determines), `ApiResponse<T>` error UX (specialist proposes), animation behavior for personality expand/collapse (specialist proposes, user approves).
- **Constraints**: No backend modifications. No routing library. No external state library. CSS Modules only — no global styles except reset and tokens.
- **Decision Policy**: Sole developer. `[USER]` = visual design decisions. `[SPEC]` = API integration details. `[SILENT]` = scaffolding and composition.

## Detail Queue
- [in_progress] Frontend Component
