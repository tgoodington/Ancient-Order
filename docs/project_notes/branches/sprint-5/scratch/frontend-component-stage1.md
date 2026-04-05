# Frontend Component Exploration — Stage 1

## Research Findings

### F1: PlayerCharacter has no combat stats
The `PlayerCharacter` interface (`src/types/index.ts:65-69`) contains only `id`, `name`, and `personality`. It does NOT contain stamina, energy, stance, or elementalPath. These fields exist exclusively on the `Combatant` interface (`src/types/combat.ts:101-117`), which is only instantiated inside a `CombatState` during active combat encounters.

### F2: CombatState is nullable on GameState
`GameState.combatState` is typed `CombatState | null` (`src/types/index.ts:101`). Outside of active combat, it is `null`. This means stamina, energy, and stance data are only available when a combat encounter is active.

### F3: VisualInfo stance field is not action stance
The `VisualInfo` type (`src/combat/roundManager.ts:44-52`) exposes a `stance` field that is `"active"` or `"KO"` -- NOT the action type letters (A/D/E/S/G) from the outline. The action type (ATTACK/DEFEND/EVADE/SPECIAL/GROUP) is a per-round declaration, not a persistent combatant field visible in VisualInfo.

### F4: ElementalPath is combat-only
`ElementalPath` is defined in `src/types/combat.ts:20` and appears only on `Combatant` and `CombatantConfig`. It is not a player-level attribute. The "path selector" concept in the outline does not map to any non-combat API endpoint.

### F5: Team composition is the only player-level "selection"
The `POST /api/player/team` endpoint accepts `{ npcIds: [string, string] }` and updates `GameState.team`. This is the closest concept to a "selector" that exists outside of combat. It selects which 2 NPCs accompany the player.

### F6: Personality is always available
`GET /api/player/personality` returns the full `Personality` object (6 traits, 5-35%, sum=100%). This is always available regardless of combat state and is the most consistently displayable HUD data.

### F7: NPC data is always available
`GET /api/npc` returns all NPCs with their archetype, personality, affection, and trust values. This data is always available and could populate HUD elements showing party composition.

### F8: GameState is the single-fetch root
`GET /api/game/state` returns the full `GameState` including player, npcs, team, combatState, and narrativeState. This is the most efficient single endpoint for initial mount -- it provides everything in one call.

### F9: Backend is Fastify, not Express
Despite ADR-006 referencing Express.js, the actual implementation uses Fastify (`package.json` shows `"fastify": "^5.3.2"`). The root `tsconfig.json` uses `NodeNext` module resolution. The backend listens on port 3000 (from context).

### F10: No existing client directory
The `client/` directory does not yet exist. This is a greenfield frontend build.

### F11: Root package.json has no concurrently script
The root `package.json` currently has only backend scripts (`build`, `start`, `dev`, `test`, etc.). The `dev:all` concurrent script for running both servers needs to be added, along with `concurrently` as a devDependency.

### F12: ApiResponse envelope is consistent
All endpoints return `{ success: boolean; data?: T; error?: { code: string; message: string } }`. The frontend needs a single utility to unwrap this envelope and throw/surface errors consistently.

---

## ECD Analysis

### Elements (E) -- Building Blocks

**Components (6 new):**

| Component | Props | Local State | Notes |
|-----------|-------|-------------|-------|
| `GameStateProvider` | `children` | `gameState: GameState \| null`, `loading: boolean`, `error: string \| null` | Context provider; fetches from `GET /api/game/state` |
| `StaminaBar` | `current: number`, `max: number` | None | Only renders when combatState is non-null; derives color state from percentage |
| `EnergyDisplay` | `current: number`, `max: number` | None | Renders filled/empty segments; max is 6 at baseline |
| `StanceIndicator` | `actionType: string \| null` | None | **Problem:** action type is a per-round declaration, not persistent state (see F3) |
| `PersonalityBreakdown` | `personality: Personality` | `expanded: boolean` | Always available; click-to-toggle expand/collapse |
| `PathSelector` | `elementalPath: ElementalPath` | None | **Problem:** path is combat-only (see F4); no non-combat path concept exists |
| `EquinoxHUD` | None (consumes context) | None | Shell composing all sub-components |

**Hooks:**
| Hook | Purpose |
|------|---------|
| `useGameState()` | Accesses GameStateProvider context; returns state, loading, error, and action methods |

**Types (frontend-side):**
| Type | Fields | Notes |
|------|--------|-------|
| `FrontendGameState` | Mirror of backend `GameState` | Possibly a subset; avoid duplicating backend types |
| `ApiResponse<T>` | `success`, `data?`, `error?` | Utility type for API calls |

**Utilities:**
| Utility | Purpose |
|---------|---------|
| `apiClient.ts` | Fetch wrapper that unwraps `ApiResponse<T>`, handles errors |
| `staminaState(pct: number)` | Returns color state name: green/yellow/orange/red/black |

**CSS / Tokens:**
| File | Contents |
|------|----------|
| `tokens.css` | Color palette (leather browns, metal grays), stamina colors (5 states), typography scale, spacing scale |
| `reset.css` | Browser reset, dark background, base font |
| `*.module.css` per component | Scoped component styles |

### Connections (C) -- Relationships

**Component Hierarchy:**
```
App.tsx
  GameStateProvider
    EquinoxHUD
      StaminaBar        (combat-only data)
      EnergyDisplay     (combat-only data)
      StanceIndicator   (combat-only data -- problematic)
      PersonalityBreakdown (always available)
      PathSelector      (combat-only data -- problematic)
```

**Data Flow:**
1. `GameStateProvider` calls `GET /api/game/state` on mount.
2. If no active game, it calls `POST /api/game/new` to create one.
3. State is exposed via `useGameState()` hook.
4. HUD components read from context. Combat-dependent components check for `gameState.combatState !== null`.
5. Action methods (e.g., personality adjustment) call the API, then re-fetch `GET /api/game/state`.

**Style Connections:**
- All components import from `tokens.css` via CSS custom properties (no direct import needed -- properties cascade from `:root`).
- Stamina bar color states driven by `data-state` attribute on the container, styled in CSS via `[data-state="green"]` selectors.

### Dynamics (D) -- Behavior Over Time

**User Workflows:**

1. **App Load (no game):** Provider fetches state -> 404 -> creates new game -> fetches again -> renders HUD with personality data only (no combat).
2. **App Load (existing game, no combat):** Provider fetches state -> renders HUD with personality + team data. Stamina/energy/stance sections hidden or show defaults.
3. **App Load (existing game, active combat):** Provider fetches state -> renders full HUD with all combat stats from `combatState.combatants`.
4. **Personality expand/collapse:** User clicks personality section -> local `expanded` state toggles -> CSS transition slides traits in/out.
5. **Path switch (combat only):** This is a combat-only concept. Outside combat, there is no path to switch.

**Loading States:**
- `GameStateProvider`: full-screen loading indicator on initial mount.
- Action methods: optimistic is unnecessary for prototype; show loading on the triggering element.

**Error States:**
- API unreachable: show error banner with retry button.
- `ApiResponse` with `success: false`: surface error message.

**Transition/Animation:**
- Stamina bar width change: CSS `transition: width 0.3s ease`.
- Stamina color change: CSS `transition: background-color 0.3s ease`.
- Personality expand/collapse: CSS `max-height` transition or CSS grid row animation.

---

## Assumptions

### A1: Single GameState fetch on mount
- **Default:** Call `GET /api/game/state` once on mount to hydrate all HUD data, rather than multiple parallel endpoint calls.
- **Rationale:** GameState is the root object containing player, npcs, team, combatState, and narrativeState. One call gives everything. Additional targeted fetches (e.g., `GET /api/player`) would be redundant since their data is a subset of GameState.

### A2: Auto-create game if none exists
- **Default:** If `GET /api/game/state` returns 404, automatically call `POST /api/game/new` and re-fetch.
- **Rationale:** Single-player prototype with no auth. There is no "game selection" screen in Sprint 5. The HUD needs data to render.

### A3: CSS data-attributes for stamina color states
- **Default:** Use `data-state="green|yellow|orange|red|black"` on the stamina bar container, styled with CSS attribute selectors.
- **Rationale:** Outline explicitly requires "CSS data-attributes" for color states "without JavaScript style injection." This is the standard pattern.

### A4: Named exports for all React components
- **Default:** Use `export function ComponentName()` (named exports, not default exports).
- **Rationale:** Outline context explicitly states "React components use named exports."

### A5: Vite proxy targets all /api/* paths
- **Default:** Vite dev server proxy rewrites `/api` to `http://localhost:3000/api`.
- **Rationale:** Standard Vite proxy config. Backend registers all routes under `/api/*` prefix.

### A6: Frontend tsconfig does NOT extend root tsconfig
- **Default:** Create an independent `client/tsconfig.json` with `bundler` moduleResolution, DOM libs, and JSX support.
- **Rationale:** Root tsconfig uses `NodeNext` module resolution and excludes DOM libs. These are incompatible with a Vite/React frontend. The outline notes this divergence explicitly.

### A7: Energy max is 6 segments
- **Default:** Display energy as 6 segments (filled vs empty).
- **Rationale:** `Combatant.maxEnergy` can vary by ascension level, but the base is 6. The outline says "current/6 max." The component should use the actual `maxEnergy` value from the combatant data, not hardcode 6.

### A8: API client uses native fetch
- **Default:** Use the browser's native `fetch()` API, no axios or other HTTP library.
- **Rationale:** Vite targets modern browsers. No need for an HTTP library in a prototype. Reduces dependencies.

---

## Key Decisions

### D1: HUD data availability outside combat
- **Tier:** [SPEC]
- **Options:**
  - **Option A: Combat-only HUD.** StaminaBar, EnergyDisplay, StanceIndicator, and PathSelector only render when `combatState` is non-null. Outside combat, the HUD shows only PersonalityBreakdown and team composition. This accurately reflects the data model.
  - **Option B: Mock/placeholder display.** Show stamina at 100%, energy full, no stance, no path when outside combat. Looks complete but displays fabricated data.
  - **Option C: Defer combat components to Sprint 7.** Sprint 5 builds only PersonalityBreakdown and team display (always-available data). Combat HUD elements (stamina, energy, stance) are built in Sprint 7 alongside the combat UI.
- **Recommendation:** Option A. The HUD shell renders with a "non-combat" layout showing personality and team info, and expands to show combat stats when an encounter is active. This is honest about data availability, avoids fabricated state, and still delivers a complete HUD architecture. The conditional rendering pattern will be needed regardless.
- **Risk if wrong:** Option B could confuse the investor demo if stamina never changes outside combat. Option C delays architectural decisions about the HUD but may be the pragmatic choice if the Sprint 5 goal is purely "foundation."

### D2: StanceIndicator data source
- **Tier:** [SPEC]
- **Options:**
  - **Option A: Show last declared action type.** Track the player's most recent combat declaration (ATTACK/DEFEND/EVADE/SPECIAL/GROUP) and display it. Requires storing this client-side since VisualInfo does not expose action types.
  - **Option B: Show active/KO status.** Use the `stance` field from VisualInfo (which is literally "active" or "KO"). Simple but not what the outline envisions.
  - **Option C: Drop StanceIndicator from Sprint 5.** The outline's vision of showing A/D/E/S/G cannot be fulfilled from current API data without client-side tracking of declarations. Defer to Sprint 7 when combat UI is built.
- **Recommendation:** Option C. The VisualInfo `stance` field does not contain action types, and adding client-side declaration tracking is premature in Sprint 5 (no combat UI to declare actions from). Build the component shell/interface but defer implementation.
- **Risk if wrong:** If the demo needs to show stance, Option A could work but requires wiring through the combat declaration flow which is Sprint 7 scope.

### D3: PathSelector scope and meaning
- **Tier:** [SPEC]
- **Options:**
  - **Option A: ElementalPath selector (combat-only).** Show and allow switching the player combatant's elemental path during combat. Requires a new API endpoint or using combat declaration. Currently no endpoint exists for changing path mid-combat.
  - **Option B: Team composition selector (always available).** Reinterpret "path selector" as the team composition selector -- choosing which 2 NPCs are in the party via `POST /api/player/team`. This uses an existing endpoint and is meaningful outside combat.
  - **Option C: Defer to Sprint 7.** Like StanceIndicator, the path concept is combat-scoped. Defer the selector to when combat UI is built.
- **Recommendation:** Option B. The outline says "path selector allowing switching between two paths" and "path switching calls the backend API." The only backend endpoint that matches a switching/selection action is `POST /api/player/team`. Reinterpreting this as a team/party selector provides real interactivity with an existing API endpoint. However, this is a meaningful reinterpretation that the user should confirm.
- **Risk if wrong:** If "path" literally means ElementalPath, then there is no API endpoint to support it outside combat, and this component must be deferred. If "path" means team composition, Option B works perfectly.

### D4: Stamina bar visual treatment
- **Tier:** [USER]
- **Options:**
  - **Option A: Solid fill.** Single-color bar that changes color at thresholds. Simple, readable, game-standard.
  - **Option B: Segmented bar.** Bar divided into discrete segments (e.g., 10 or 20 segments). More tactile, classic RPG feel.
  - **Option C: Gradient within color state.** Solid fill with a subtle gradient (lighter at top, darker at bottom) for depth. Slightly more polished.
- **Recommendation:** Option C (gradient). Adds visual depth consistent with the armor/metal aesthetic without complexity. The gradient is purely CSS (`linear-gradient`) and costs nothing in implementation.
- **Risk if wrong:** Purely visual; easy to swap. No architectural impact.

### D5: Personality expansion interaction
- **Tier:** [USER]
- **Options:**
  - **Option A: Click-to-toggle.** Collapsed by default showing summary (e.g., top 2 traits). Click expands to show all 6 traits with percentage bars. Mobile-friendly.
  - **Option B: Hover-to-expand.** Expands on hover, collapses on mouse leave. Desktop-only; inaccessible without mouse.
  - **Option C: Always visible, compact/expanded modes.** Traits always shown in a compact single-line format; click expands to full bar visualization.
- **Recommendation:** Option A. Click-to-toggle is accessible (keyboard and pointer), works on all devices, and keeps the HUD compact by default. The collapsed state could show a small radar/spider chart or just the trait with the highest value.
- **Risk if wrong:** Purely UX; easy to swap interaction pattern. Hover (Option B) would exclude keyboard/touch users.

### D6: Frontend type strategy
- **Tier:** [SILENT]
- **Options:**
  - **Option A: Duplicate types in frontend.** Define `Personality`, `PlayerCharacter`, `NPC`, `GameState` etc. in `client/src/types/`. Simple, no cross-project dependency.
  - **Option B: Shared types package.** Create a shared `types/` package imported by both backend and frontend. Ensures type alignment but adds build complexity.
  - **Option C: Generate types from API.** Use a tool like openapi-typescript. Backend doesn't have OpenAPI spec, so this is impractical.
- **Recommendation:** Option A. For a prototype with a sole developer, duplicating the subset of types needed by the frontend is the simplest approach. The types are small and stable (backend is locked). A shared package adds monorepo tooling overhead that is not justified for a pitch demo.
- **Risk if wrong:** Types could drift if backend changes, but backend is locked for Sprint 5. Low risk.

### D7: Error display pattern
- **Tier:** [SILENT]
- **Options:**
  - **Option A: Inline error states.** Each component handles its own error display (e.g., "Failed to load" message in place of content).
  - **Option B: Top-level error banner.** A single error banner at the app level that surfaces the most recent API error. Components show fallback/empty state.
  - **Option C: Both.** Top-level banner for global errors (network down), inline for component-specific errors.
- **Recommendation:** Option B for Sprint 5. The only error source is the GameStateProvider's initial fetch. A single banner with a retry button is sufficient. Component-level error handling adds complexity only needed when components make independent API calls (Sprint 7+).
- **Risk if wrong:** Low. Can layer in inline error states later.

---

## Risks Identified

### R1: HUD scope mismatch with data model (BLOCKING)
**What:** The outline assumes stamina, energy, stance (A/D/E/S/G), and elemental path are always available for HUD display. Research proves these are combat-only data that exist only when `GameState.combatState` is non-null. Outside combat, the HUD has no stamina/energy/stance/path data to display.
**Severity:** Blocking -- must resolve before execution. Tasks T4 and T5 acceptance criteria assume this data is always available.
**Resolution:** Decisions D1, D2, D3 above. The user must confirm whether the HUD shows conditional combat data, uses placeholders, or defers combat components.

### R2: VisualInfo stance is not action stance
**What:** The outline describes StanceIndicator showing "A/D/E/S/G" (action types). The actual `VisualInfo.stance` field is `"active" | "KO"` (combatant status). Action type is a per-round declaration, not exposed as persistent state in VisualInfo.
**Severity:** Blocking for T5 StanceIndicator. See Decision D2.
**Resolution:** Either track client-side declarations (Sprint 7 scope) or defer the component.

### R3: PathSelector has no matching API endpoint outside combat
**What:** The outline describes a "path selector" that calls the backend API. `ElementalPath` is combat-only. No endpoint exists to change a player's path outside of combat encounter configuration.
**Severity:** Blocking for T5 PathSelector. See Decision D3.
**Resolution:** Reinterpret as team composition selector (existing `POST /api/player/team` endpoint) or defer.

### R4: No game may exist on first load
**What:** The backend requires `POST /api/game/new` before any state is available. If the frontend loads without an active game session, `GET /api/game/state` returns 404.
**Severity:** Advisory. Handled by Assumption A2 (auto-create game).
**Resolution:** GameStateProvider auto-creates a new game if none exists.

### R5: Backend uses Fastify, not Express
**What:** ADR-006 references Express.js, but `package.json` shows Fastify v5.3.2. No impact on frontend (HTTP is HTTP), but documentation references should be accurate.
**Severity:** Advisory. No frontend impact.

---

## Recommended Approach

### Architecture Summary

The Equinox HUD should be designed as a **context-aware adaptive component** that renders different content based on game phase (exploration vs. combat). This accurately reflects the backend data model where combat stats only exist during active encounters.

### Recommended Task Modifications

**T1 (unchanged):** Initialize Vite + React project. Independent `client/tsconfig.json` with `bundler` resolution (A6). Native fetch for API calls (A8).

**T2 (unchanged):** Design token system. Stamina color tokens for 5 states. Armor/leather/metal palette.

**T3 (minor adjustment):** GameStateProvider fetches `GET /api/game/state` on mount (A1). Auto-creates game if 404 (A2). Exposes typed context with explicit `hasCombat` derived flag for conditional rendering. Provides action methods: `refreshState()`, `updateTeam(npcIds)`, `adjustPersonality(deltas)`.

**T4 (scope adjustment per D1):** StaminaBar and EnergyDisplay are built as components but only render when combat is active. They read from `gameState.combatState.combatants` (specifically the player's combatant entry). CSS data-attribute color states (A3). Outside combat, a placeholder or "no active combat" state is shown.

**T5 (significant scope adjustment per D1, D2, D3):**
- **PersonalityBreakdown:** Fully functional always. Click-to-toggle (D5).
- **StanceIndicator:** Deferred to Sprint 7 or rendered as combat-phase-only with "active/KO" display. Cannot show A/D/E/S/G from current API (R2).
- **PathSelector:** Reinterpreted as TeamSelector using `POST /api/player/team` (D3 Option B), OR deferred. Requires user confirmation.

**T6 (adjusted):** EquinoxHUD shell composes available components. Uses conditional rendering: exploration mode shows personality + team; combat mode adds stamina, energy, and (eventually) stance.

### Key Resolve-Before-Execution Items

1. **D1 (HUD data availability):** User must confirm whether to build combat-conditional components now or defer combat HUD to Sprint 7.
2. **D3 (PathSelector meaning):** User must confirm whether "path selector" means team composition (existing API) or elemental path (no API, combat-only).
3. **D2 (StanceIndicator):** If D1 = build now, user must confirm deferral of A/D/E/S/G stance display to Sprint 7.

### File Structure

```
client/
  index.html
  package.json
  tsconfig.json
  vite.config.ts
  src/
    main.tsx
    App.tsx
    types/
      index.ts              -- Frontend type definitions (D6: duplicated subset)
    styles/
      tokens.css            -- Design tokens (T2)
      reset.css             -- Browser reset (T2)
    api/
      client.ts             -- Fetch wrapper, ApiResponse unwrapping
    context/
      GameStateProvider.tsx  -- Context provider (T3)
      useGameState.ts       -- Consumer hook (T3)
    components/
      StaminaBar.tsx         + .module.css (T4)
      EnergyDisplay.tsx      + .module.css (T4)
      PersonalityBreakdown.tsx + .module.css (T5)
      TeamSelector.tsx       + .module.css (T5, if D3 = team)
      EquinoxHUD.tsx         + .module.css (T6)
```

### Initial Mount API Strategy (resolves [SPEC] for T3)

```
1. GET /api/game/state
   - If 404: POST /api/game/new -> GET /api/game/state
   - Response provides: player (name, personality), npcs, team, combatState (nullable), narrativeState (nullable)
2. No additional endpoint calls needed on mount.
3. Action methods re-call GET /api/game/state after any mutation endpoint.
```

This single-endpoint strategy is efficient and provides all data the HUD needs. The GameState root object is the authoritative source.
