# Outline: Sprint 5 — Frontend React Phase 1

## 1. Objective

Bootstrap the React frontend and deliver the first player-facing component — the Equinox HUD — fully functional over live backend data. This establishes the frontend architecture, state management patterns, and visual design language that Sprints 6 (town scene) and 7 (combat UI) will build upon.

**Success criteria:** React app builds and runs on Vite dev server with proxy to Fastify backend on port 3000; GameStateProvider fetches and manages state from API endpoints; Equinox HUD renders all specified elements (stamina bar with 5 color states, energy segments, stance indicator, expandable personality breakdown, path selector) with interactive behavior; visual aesthetic matches the armor/leather/metal design language.

## 2. Discovery Summary

- **Problem:** Backend is complete (4 sprints, 1019 tests, full REST API) but has no frontend. Need to bootstrap React and deliver a polished HUD for the investor/publisher pitch demo.
- **Goals:** Working React app consuming real API data, complete Equinox HUD with all interactive elements, foundation architecture for Sprints 6-7.
- **Target users:** Player/demo audience (first visual impression), investor/publisher (pitch polish), downstream sprints (pattern foundation).
- **Constraints:** No backend modifications; immutable state pattern (ADR-005); ESM with .js extensions (ADR-016); Vercel deployment target; Fastify backend on port 3000.
- **Key findings:** All HUD data is available via existing API endpoints (game state, player, personality, narrative). No authentication needed for Phase 1 single-player dev environment.

### 2.5. Parent Context

**Parent:** sprint-4
**Parent Objective:** Complete backend API surface and harden state validation for frontend readiness.

**Shared Components:**
- API endpoints (game, player, personality, NPC, narrative, combat) — Sprint 5 consumes what Sprint 4 wired
- `ApiResponse<T>` envelope — Sprint 5 frontend parses this standardized contract
- `src/types/index.ts` — Sprint 5 may reference backend types for TypeScript alignment

**Inherited Decisions:**
- D2: Immutable state pattern — frontend mirrors with immutable React context updates
- ADR-016: ESM modules with .js extensions — Vite's ESM output aligns
- Party size = 3 (player + 2 NPCs), personality 6-trait/5-35%/sum=100% enforced by backend

**Intersection Points:**
- API response shapes — frontend GameStateProvider depends on Sprint 4's `ApiResponse<T>` contract
- Path switching — uses POST `/api/player/team` endpoint wired in Sprint 4

**Divergence:**
- Module resolution: backend uses NodeNext, frontend uses bundler (Vite) — separate tsconfig required

## 3. Technology Decisions

| Decision | Choice | Status | Rationale |
|----------|--------|--------|-----------|
| React toolchain | Vite + React in `client/` subfolder | Locked | ESM-native, fast HMR, Vitest config sharing, no SSR overhead for single-player game |
| Styling approach | CSS Modules + CSS custom properties | Locked | Game aesthetic requires hand-crafted styles; zero runtime cost; scoping built into Vite |
| State management | On-demand fetch via React Context | Locked | Single-player — no external state changes; fetch on mount + re-fetch after user actions |

## 6. Task Sequence

### Task 1: Initialize Vite + React project in client/
- **Domain**: code/frontend
- **Depth**: Light
- **Component**: Project scaffolding
- **Description**: Create the `client/` directory with its own `package.json`, `tsconfig.json` (extending root with DOM libs and bundler module resolution), and `vite.config.ts` (with proxy to `http://localhost:3000/api`). Install React, ReactDOM, Vite, and `@vitejs/plugin-react`. Set up the entry point (`main.tsx`, `App.tsx`) rendering a placeholder. Add a root-level `dev:all` script using `concurrently` to run both backend and frontend dev servers.
- **Acceptance Criteria**:
  1. `npm run dev` in `client/` starts Vite dev server and renders a React component in the browser
  2. API requests to `/api/*` from the frontend are proxied to `http://localhost:3000` without CORS issues
  3. TypeScript compilation succeeds with strict mode and DOM type support
  4. Root-level script can start both backend and frontend dev servers concurrently
- **Dependencies**: None
- **Files**: `client/package.json`, `client/tsconfig.json`, `client/vite.config.ts`, `client/src/main.tsx`, `client/src/App.tsx`, `client/index.html`

### Task 2: Create design token system and base styles
- **Domain**: code/frontend
- **Depth**: Light
- **Component**: Styling foundation
- **Description**: Define CSS custom properties for the armor/leather/metal color palette, typography, and spacing. Create a base stylesheet that resets defaults and establishes the game's dark, textured visual foundation. Include stamina color tokens for all 5 states.
- **Acceptance Criteria**:
  1. CSS custom properties define the complete color palette (leather browns, metal grays, stamina Green/Yellow/Orange/Red/Black)
  2. Base styles reset browser defaults and set the dark game-world background
  3. All downstream components can reference design tokens via `var(--token-name)`
- **Dependencies**: Task 1
- **Files**: `client/src/styles/tokens.css`, `client/src/styles/reset.css`

### Task 3: Build GameStateProvider context
- **Domain**: code/frontend
- **Depth**: Standard
- **Component**: State management
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

### Task 4: Implement Equinox HUD — stamina bar and energy display
- **Domain**: code/frontend
- **Depth**: Standard
- **Component**: Equinox HUD
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

### Task 5: Implement Equinox HUD — stance, personality, and path selector
- **Domain**: code/frontend
- **Depth**: Standard
- **Component**: Equinox HUD
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

### Task 6: Compose Equinox HUD shell and integrate all elements
- **Domain**: code/frontend
- **Depth**: Light
- **Component**: Equinox HUD
- **Description**: Assemble the individual HUD components (stamina, energy, stance, personality, path) into the complete Equinox HUD shell component. Apply the armor/leather aesthetic to the outer frame. Wire into App.tsx with GameStateProvider wrapping.
- **Acceptance Criteria**:
  1. All HUD sub-components render together in a cohesive layout matching the minimal armor-like aesthetic
  2. HUD shell has a visually distinct frame/border treatment evoking in-world armor technology
  3. All interactive elements (expand personality, switch path) function within the composed HUD
  4. App.tsx renders the complete HUD wrapped in GameStateProvider, consuming live backend data
- **Dependencies**: Tasks 4, 5
- **Files**: `client/src/components/EquinoxHUD.tsx`, `client/src/components/EquinoxHUD.module.css`, `client/src/App.tsx`

### 6.5 Detail Assessment

| Task(s) | Domain | Depth | Rationale |
|---------|--------|-------|-----------|
| Task 1 | code/frontend | Light — autonomous | Standard Vite scaffolding, well-documented setup |
| Task 2 | code/frontend | Light — autonomous | Design token definition from spec, mechanical CSS work |
| Task 3 | code/frontend | Standard — confirmation needed | API integration patterns, response parsing strategy |
| Task 4 | code/frontend | Standard — confirmation needed | Stamina bar visual treatment is a user design decision |
| Task 5 | code/frontend | Standard — confirmation needed | Multiple user-facing interaction decisions (personality expansion, path selector UX) |
| Task 6 | code/frontend | Light — autonomous | Composition of existing components, layout assembly |

## 8. Risks & Mitigations

| Risk | Likelihood | Impact | Mitigation |
|------|-----------|--------|------------|
| API response shape doesn't match frontend expectations | Low | Medium | Task 3 specialist reads actual API responses to build provider; Sprint 4 standardized `ApiResponse<T>` |
| Two dev servers add friction to development workflow | Low | Low | Root-level `concurrently` script in Task 1; single `npm run dev:all` command |
| HUD aesthetic feels prototypy without designer input | Medium | Medium | Design tokens established early (Task 2); user is designer and reviews during build |
| GameStateProvider becomes a monolith as sprints progress | Low | Medium | On-demand pattern keeps it thin; Sprint 6-7 can split into domain-specific providers |

## 10. Outline Context for Detail Phase

- **Domain-Specific Considerations**: All work is code/frontend (React + TypeScript + CSS Modules). Follow Vite conventions for project structure. CSS Modules use `*.module.css` naming. React components use named exports. State management via React Context + `useContext` hook — no Redux or external state library.
- **Cross-Domain Dependencies**: GameStateProvider (Task 3) is the bridge between backend API and all HUD components (Tasks 4-6). Design tokens (Task 2) are consumed by all component CSS. Tasks 4 and 5 are independent of each other but both depend on Tasks 2 and 3.
- **Sequencing Considerations**: Task 1 (scaffold) must complete first. Tasks 2 and 3 can run in parallel. Tasks 4 and 5 can run in parallel after 2+3. Task 6 composes everything last. Dependency DAG: 1 → [2, 3] → [4, 5] → 6.
- **Open Questions**:
  - [code/frontend] Which specific API endpoints should GameStateProvider call on initial mount? Specialist determines by examining available routes.
  - [code/frontend] How should `ApiResponse<T>` error states surface in the UI? Specialist determines appropriate UX pattern.
  - [code/frontend] Exact animation/transition behavior for personality expand/collapse — specialist proposes, user approves.
- **Constraints**: No backend modifications. No routing library needed (single view). No external state management library (React Context only). CSS Modules only — no global styles except reset and tokens.
- **Decision Policy**: Sole developer, hands-on. Visual design decisions (stamina bar treatment, personality expansion UX, path selector layout) are `[USER]`. API integration details are `[SPEC]`. Straightforward scaffolding and composition are `[SILENT]`.
