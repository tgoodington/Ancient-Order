# Blueprint: Frontend Component Architecture (Equinox HUD)

## 1. Task Reference

| Task | Title | Acceptance Criteria | Dependencies |
|------|-------|-------------------|-------------|
| T1 | Initialize Vite + React project in client/ | AC1-AC4 | None |
| T2 | Create design token system and base styles | AC1-AC3 | T1 |
| T3 | Build GameStateProvider context | AC1-AC4 | T1 |
| T4 | Equinox HUD -- stamina bar and energy display | AC1-AC4 | T2, T3 |
| T5 | Equinox HUD -- stance, personality, and path selector | AC1-AC4 (AC1 deferred) | T2, T3 |
| T6 | Compose Equinox HUD shell and integrate all | AC1-AC4 | T4, T5 |

**Deferred:** T5-AC1 (StanceIndicator) deferred to Sprint 7 per D2.
**Reinterpreted:** T5-AC3/AC4 "Path selector" reinterpreted as "Team selector" per D3.

---

## 2. Research Findings

### Framework & Tooling
- **Backend:** Fastify v5.3.2, port 3000 (`src/api/index.ts:256`)
- **Backend TypeScript:** `tsconfig.json` -- `ES2022`, `NodeNext` module, strict mode
- **Frontend:** Greenfield -- no `client/` directory exists (F10)
- **Root package.json:** Backend-only scripts, no `concurrently` (F11)

### Key Backend Files
- **Type definitions:** `src/types/index.ts` (GameState, PlayerCharacter, NPC, Personality, ApiResponse, ApiError)
- **Combat types:** `src/types/combat.ts` (Combatant, CombatState, ElementalPath, ActionType)
- **API entry:** `src/api/index.ts` (Fastify app factory, route registration)
- **Game routes:** `src/api/game.ts` (POST /api/game/new, GET /api/game/state)
- **Player routes:** `src/api/player.ts` (GET /api/player, GET /api/player/personality, POST /api/player/team)
- **NPC routes:** `src/api/npc.ts` (GET /api/npc, GET /api/npc/:id)
- **NPC templates:** `src/state/npcs.ts` (Elena, Lars, Kade -- 3 NPCs)
- **Game state factory:** `src/state/gameState.ts` (createNewGameState -- team starts as `[]`)

### Data Model Facts
- `PlayerCharacter` has only `id`, `name`, `personality` -- no combat stats (F1)
- `GameState.combatState` is `CombatState | null` -- null outside combat (F2)
- Stamina, energy, elementalPath exist only on `Combatant` inside `CombatState` (F1, F4)
- `GameState.team` is `readonly string[]` -- NPC IDs, exactly 2 when set (starts as `[]`)
- 3 NPCs available: `npc_scout_elena`, `npc_merchant_lars`, `npc_outlaw_kade`
- Team change is locked during combat and narrative scenes (backend enforced)
- `ApiResponse<T>` envelope: `{ success: boolean; data?: T; error?: { code, message } }` (F12)

### API Endpoints Verified
| Method | Path | Response Type | Notes |
|--------|------|--------------|-------|
| GET | /api/game/state | ApiResponse\<GameState\> | Returns 404 if no game |
| POST | /api/game/new | ApiResponse\<GameState\> | Body: `{ playerName?, difficulty? }` |
| GET | /api/player/personality | ApiResponse\<Personality\> | 6 traits |
| POST | /api/player/team | ApiResponse\<{ team: readonly string[] }\> | Body: `{ npcIds: [string, string] }` |
| GET | /api/npc | ApiResponse\<NPC[]\> | All 3 NPC templates |

---

## 3. Approach

### Component Architecture
A React 19 + Vite 6 SPA in `client/` with a Context-based state provider. The component tree is:

```
App.tsx
  GameStateProvider
    ErrorBanner (conditional)
    LoadingScreen (conditional)
    EquinoxHUD
      StaminaBar (combat-only, conditional)
      EnergyDisplay (combat-only, conditional)
      PersonalityBreakdown (always visible)
      TeamSelector (always visible, disabled during combat/narrative)
```

The HUD is **combat-conditional** (D1): stamina and energy sub-components only render when `combatState` is non-null. Personality and team are always visible since they live on `GameState.player` and `GameState.team` respectively.

### State Management Strategy
Single `GameStateContext` with a custom `useGameState()` hook. The provider:
1. Fetches `GET /api/game/state` on mount (A1)
2. If 404, auto-creates via `POST /api/game/new` then re-fetches (A2)
3. Exposes `gameState`, `loading`, `error`, and action methods (`setTeam`, `refreshState`)
4. All API calls go through a thin `apiClient.ts` wrapper using native `fetch` (A8)

### Styling Approach
CSS Modules (`*.module.css`) per component with a global design token layer (`tokens.css`) and browser reset (`reset.css`). Stamina bar color states use CSS `data-state` attribute selectors (A3) with gradient fills (D4). Dark game-world background.

### Accessibility Plan
- ARIA roles on all interactive elements
- Keyboard support: Enter/Space for toggle and selection
- `aria-expanded` on PersonalityBreakdown toggle
- `aria-valuenow`/`aria-valuemin`/`aria-valuemax` on StaminaBar progressbar
- `prefers-reduced-motion` media query to disable animations
- Minimum 4.5:1 contrast ratio on text against dark background

### Testability Notes
- **apiClient.ts**: Pure fetch wrapper -- test with mock fetch. Edge cases: network failure, non-JSON response, API error envelope with `success: false`.
- **GameStateProvider**: Test the 404-then-create flow, loading/error states, and that context values propagate. Mock apiClient.
- **StaminaBar**: Test all 5 color threshold boundaries (>60%, 40-60%, 20-40%, 1-20%, 0%). Test that `data-state` attribute changes correctly at exact boundaries.
- **EnergyDisplay**: Test filled vs empty segment count at 0, partial, and max energy. Verify maxEnergy from data (A7), not hardcoded.
- **PersonalityBreakdown**: Test expand/collapse toggle, all 6 traits render, percentage values sum display.
- **TeamSelector**: Test NPC listing, selection of exactly 2, API call on confirm, disabled state during combat/narrative.
- **Error boundary**: Verify ErrorBanner shows on API failure, dismisses correctly.

---

## 4. Decisions Made

### D1: HUD data availability outside combat
- **Options:** (a) Always show all HUD elements with placeholder/zero values, (b) Combat-conditional HUD showing only available data
- **Chosen:** (b) Combat-conditional HUD
- **Rationale:** Stamina/energy only exist on `Combatant` inside `CombatState`, which is null outside combat (F1, F2). Showing zeros would be misleading. Personality and team are always available.

### D2: StanceIndicator data source
- **Options:** (a) Use VisualInfo stance ("active"/"KO"), (b) Show per-round action type letter from declarations, (c) Defer to Sprint 7
- **Chosen:** (c) Defer to Sprint 7
- **Rationale:** VisualInfo stance is "active"/"KO" not action types (F3). Action type is per-round declaration, not persistent. No clean data source exists for a real-time stance display yet.

### D3: PathSelector scope and meaning
- **Options:** (a) ElementalPath switcher (combat-only), (b) Team selector using POST /api/player/team
- **Chosen:** (b) Team selector
- **Rationale:** ElementalPath is combat-only and fixed per combatant config (F4). Team composition is the only player-level selection available outside combat (F5). POST /api/player/team is the matching endpoint.

### D4: Stamina bar visual treatment
- **Options:** (a) Flat solid color, (b) Gradient fill
- **Chosen:** (b) Gradient fill
- **Rationale:** Gradient adds visual depth matching the "armor-like" aesthetic. Applied via CSS `linear-gradient` on the fill element, color derived from `data-state`.

### D5: Personality expansion interaction
- **Options:** (a) Always expanded, (b) Hover to expand, (c) Click-to-toggle
- **Chosen:** (c) Click-to-toggle
- **Rationale:** Click is accessible (keyboard and pointer), works on touch devices, and preserves compact HUD layout when collapsed.

### D6: Frontend type strategy
- **Options:** (a) Import from backend src/types, (b) Duplicate types in frontend, (c) Shared types package
- **Chosen:** (b) Duplicate types in frontend
- **Rationale:** Frontend tsconfig does NOT extend root tsconfig (A6). Separate `client/` directory with its own build. Avoids coupling frontend build to backend source tree. Types are small and stable.

### D7: Error display pattern
- **Options:** (a) Per-component inline errors, (b) Top-level error banner, (c) Toast notifications
- **Chosen:** (b) Top-level error banner
- **Rationale:** Single error surface simplifies the first sprint. ErrorBanner renders at the top of the HUD shell, dismissible. Future sprints can add per-component error handling.

### A1-A8: Accepted Assumptions
- **A1:** Single GameState fetch on mount (GET /api/game/state)
- **A2:** Auto-create game if 404 (POST /api/game/new then re-fetch)
- **A3:** CSS data-attributes for stamina color states
- **A4:** Named exports for all React components
- **A5:** Vite proxy targets all /api/* paths to localhost:3000
- **A6:** Frontend tsconfig does NOT extend root tsconfig
- **A7:** Energy max uses actual maxEnergy from Combatant data
- **A8:** API client uses native fetch (no axios/ky)

---

## 5. Deliverable Specification

### 5.1 Project Scaffolding (T1)

#### File: `client/package.json`
```json
{
  "name": "ancient-order-client",
  "private": true,
  "type": "module",
  "scripts": {
    "dev": "vite",
    "build": "tsc -b && vite build",
    "preview": "vite preview"
  }
}
```
**Dependencies:** `react@^19.0.0`, `react-dom@^19.0.0`
**Dev dependencies:** `@types/react@^19.0.0`, `@types/react-dom@^19.0.0`, `@vitejs/plugin-react@^4.4.0`, `typescript@^5.7.0`, `vite@^6.2.0`

#### File: `client/vite.config.ts`
Vite config with React plugin and proxy:
```typescript
server: {
  port: 5173,
  proxy: {
    '/api': {
      target: 'http://localhost:3000',
      changeOrigin: true
    }
  }
}
```
Per A5: all `/api/*` requests proxy to backend.

#### File: `client/tsconfig.json`
Standalone config (A6 -- does NOT extend root):
- `target`: `ES2022`
- `module`: `ESNext`
- `moduleResolution`: `bundler`
- `lib`: `["ES2022", "DOM", "DOM.Iterable"]`
- `jsx`: `react-jsx`
- `strict`: true
- `include`: `["src"]`

#### File: `client/tsconfig.node.json`
For vite.config.ts:
- `include`: `["vite.config.ts"]`
- `module`: `ESNext`, `moduleResolution`: `bundler`

#### File: `client/index.html`
Standard Vite SPA entry HTML with `<div id="root"></div>` and `<script type="module" src="/src/main.tsx"></script>`.

#### File: `client/src/main.tsx`
```typescript
import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { App } from './App';
import './styles/reset.css';
import './styles/tokens.css';

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <App />
  </StrictMode>
);
```

#### Root `package.json` addition
Add to root package.json:
- devDependency: `concurrently@^9.0.0`
- Script: `"dev:all": "concurrently \"npm run dev\" \"npm run dev --prefix client\""`

### 5.2 Type Definitions (D6 -- duplicated frontend types)

#### File: `client/src/types/index.ts`

```typescript
// Duplicated from backend -- kept minimal for HUD needs

export interface Personality {
  patience: number;
  empathy: number;
  cunning: number;
  logic: number;
  kindness: number;
  charisma: number;
}

export type PersonalityTrait = 'patience' | 'empathy' | 'cunning' | 'logic' | 'kindness' | 'charisma';

export const PERSONALITY_TRAITS: readonly PersonalityTrait[] = [
  'patience', 'empathy', 'cunning', 'logic', 'kindness', 'charisma'
] as const;

export interface PlayerCharacter {
  readonly id: string;
  readonly name: string;
  readonly personality: Personality;
}

export interface NPC {
  readonly id: string;
  readonly archetype: string;
  readonly personality: Personality;
  readonly affection: number;
  readonly trust: number;
}

export type ElementalPath = 'Fire' | 'Water' | 'Air' | 'Earth' | 'Shadow' | 'Light';

export interface Combatant {
  readonly id: string;
  readonly name: string;
  readonly archetype: string;
  readonly rank: number;
  readonly stamina: number;
  readonly maxStamina: number;
  readonly power: number;
  readonly speed: number;
  readonly energy: number;
  readonly maxEnergy: number;
  readonly ascensionLevel: 0 | 1 | 2 | 3;
  readonly elementalPath: ElementalPath;
  readonly isKO: boolean;
}

export type CombatPhase = 'AI_DECISION' | 'VISUAL_INFO' | 'PC_DECLARATION' | 'ACTION_RESOLUTION' | 'PER_ATTACK';

export interface CombatState {
  readonly round: number;
  readonly phase: CombatPhase;
  readonly playerParty: readonly Combatant[];
  readonly enemyParty: readonly Combatant[];
  readonly status: 'active' | 'victory' | 'defeat';
}

export interface GameState {
  readonly player: PlayerCharacter;
  readonly npcs: Record<string, NPC>;
  readonly team: readonly string[];
  readonly combatState: CombatState | null;
  readonly timestamp: number;
}

export interface ApiError {
  code: string;
  message: string;
}

export interface ApiResponse<T = unknown> {
  success: boolean;
  data?: T;
  error?: ApiError;
}
```

Note: `Combatant` omits `activeBuffs` and `reactionSkills` -- not needed for HUD display. `GameState` omits `currentDialogueNode`, `saveSlot`, `narrativeState`, `conversationLog` -- not consumed by Sprint 5 components. These can be added in future sprints.

### 5.3 API Client (A8)

#### File: `client/src/api/apiClient.ts`

**Named exports:**
- `apiGet<T>(path: string): Promise<T>` -- GET request, returns unwrapped `data` from ApiResponse
- `apiPost<T>(path: string, body?: unknown): Promise<T>` -- POST request, returns unwrapped `data`
- `ApiClientError` class extending Error with `code: string` and `message: string`

**Logic:**
- Base URL: empty string (Vite proxy handles `/api/*` routing, A5)
- `Content-Type: application/json` on all POST requests
- On non-ok HTTP response: throw `ApiClientError` with code from response body if parseable, else `'NETWORK_ERROR'`
- On ok response: parse JSON as `ApiResponse<T>`, if `success` is false throw `ApiClientError` with `error.code` and `error.message`
- On ok + success: return `data` cast to `T`
- On fetch exception (network down): throw `ApiClientError` with code `'NETWORK_ERROR'`

### 5.4 Design Tokens (T2)

#### File: `client/src/styles/tokens.css`

All values defined as CSS custom properties on `:root`:

**Color Palette -- Leather Browns:**
- `--color-bg-deep`: `#1a1410` (page background)
- `--color-bg-panel`: `#2a2118` (HUD panel background)
- `--color-bg-panel-hover`: `#352a1f` (hover state for interactive panels)
- `--color-border-frame`: `#5c4a3a` (HUD border, "armor frame")
- `--color-border-accent`: `#8b7355` (highlighted borders)
- `--color-text-primary`: `#e8dcc8` (main text)
- `--color-text-secondary`: `#a89880` (secondary/label text)
- `--color-text-muted`: `#6b5d50` (disabled text)

**Metal Grays:**
- `--color-metal-light`: `#c0b8a8` (bright metal accents)
- `--color-metal-mid`: `#7a7060` (mid-tone metal)
- `--color-metal-dark`: `#4a4238` (dark metal)

**Stamina States (5 defined thresholds):**
- `--color-stamina-green`: `#4a9e4a` (>60%)
- `--color-stamina-green-light`: `#6ab86a` (gradient light end for green)
- `--color-stamina-yellow`: `#c4a833` (40-60%)
- `--color-stamina-yellow-light`: `#d4c050` (gradient light end for yellow)
- `--color-stamina-orange`: `#c87533` (20-40%)
- `--color-stamina-orange-light`: `#d89050` (gradient light end for orange)
- `--color-stamina-red`: `#c44040` (1-20%)
- `--color-stamina-red-light`: `#d86060` (gradient light end for red)
- `--color-stamina-black`: `#1a1a1a` (0% -- KO)

**Energy:**
- `--color-energy-filled`: `#5088c0` (filled segment)
- `--color-energy-empty`: `#2a2a30` (empty segment)
- `--color-energy-border`: `#4a6888` (segment border)

**Personality Trait Colors (one per trait for bar fills):**
- `--color-trait-patience`: `#6a9fb5`
- `--color-trait-empathy`: `#b56a9f`
- `--color-trait-cunning`: `#b5956a`
- `--color-trait-logic`: `#6ab58a`
- `--color-trait-kindness`: `#b57a6a`
- `--color-trait-charisma`: `#8a6ab5`

**Spacing Scale:**
- `--space-xs`: `4px`
- `--space-sm`: `8px`
- `--space-md`: `16px`
- `--space-lg`: `24px`
- `--space-xl`: `32px`

**Typography:**
- `--font-family`: `'Segoe UI', system-ui, -apple-system, sans-serif`
- `--font-size-xs`: `0.75rem` (12px)
- `--font-size-sm`: `0.875rem` (14px)
- `--font-size-base`: `1rem` (16px)
- `--font-size-lg`: `1.25rem` (20px)
- `--font-size-xl`: `1.5rem` (24px)
- `--font-weight-normal`: `400`
- `--font-weight-medium`: `500`
- `--font-weight-bold`: `700`

**Border & Shadow:**
- `--border-radius-sm`: `4px`
- `--border-radius-md`: `8px`
- `--border-width`: `2px`
- `--shadow-panel`: `0 2px 8px rgba(0, 0, 0, 0.4)`
- `--shadow-inset`: `inset 0 1px 3px rgba(0, 0, 0, 0.3)`

**Transitions:**
- `--transition-fast`: `150ms ease`
- `--transition-normal`: `250ms ease`
- `--transition-slow`: `400ms ease`

#### File: `client/src/styles/reset.css`

Minimal reset:
- `*, *::before, *::after { box-sizing: border-box; margin: 0; padding: 0; }`
- `body { font-family: var(--font-family); font-size: var(--font-size-base); color: var(--color-text-primary); background-color: var(--color-bg-deep); line-height: 1.5; -webkit-font-smoothing: antialiased; }`
- `img, svg { display: block; max-width: 100%; }`
- `button { font: inherit; color: inherit; cursor: pointer; }`
- `ul, ol { list-style: none; }`

### 5.5 Component Specifications

#### 5.5.1 App (root)

**File:** `client/src/App.tsx`
**Export:** `export function App()` (named, A4)
**Props:** None
**Local state:** None
**Renders:**
```
<GameStateProvider>
  <EquinoxHUD />
</GameStateProvider>
```
**Styling file:** `client/src/App.module.css`
- `.app` -- `min-height: 100vh; display: flex; justify-content: center; align-items: flex-start; padding: var(--space-lg);`

---

#### 5.5.2 GameStateProvider

**File:** `client/src/context/GameStateContext.tsx`
**Exports:**
- `export function GameStateProvider({ children }: GameStateProviderProps)` (named, A4)
- `export function useGameState(): GameStateContextValue` (named hook)

**Props interface:**
```typescript
interface GameStateProviderProps {
  children: React.ReactNode; // required
}
```

**Context value interface:**
```typescript
interface GameStateContextValue {
  gameState: GameState | null;       // null only during initial load
  npcs: NPC[];                       // all available NPCs (from gameState.npcs)
  loading: boolean;                  // true during fetch/create
  error: string | null;              // error message string or null
  setTeam: (npcIds: [string, string]) => Promise<void>;  // calls POST /api/player/team
  refreshState: () => Promise<void>; // re-fetches GET /api/game/state
  dismissError: () => void;          // clears error state
}
```

**Local state:**
| State var | Type | Initial | Trigger |
|-----------|------|---------|---------|
| `gameState` | `GameState \| null` | `null` | Set after successful fetch |
| `loading` | `boolean` | `true` | Set false after initial load completes (success or error) |
| `error` | `string \| null` | `null` | Set on API errors, cleared by dismissError |

**Hook usage:**
- `useState` for gameState, loading, error
- `useEffect` (mount, empty deps) for initial fetch sequence
- `useCallback` for setTeam, refreshState, dismissError (stable references)
- `useMemo` for the context value object (deps: gameState, loading, error, setTeam, refreshState, dismissError)
- `useMemo` for npcs array derived from gameState.npcs (deps: gameState)

**Mount sequence (useEffect):**
1. Call `apiGet<GameState>('/api/game/state')`
2. On success: set gameState, set loading false
3. On error with code `GAME_NOT_FOUND` (404): call `apiPost<GameState>('/api/game/new', {})`, then call `apiGet<GameState>('/api/game/state')` again, set gameState (A2)
4. On any other error: set error message, set loading false
5. On auto-create failure: set error message, set loading false

**setTeam logic:**
1. Call `apiPost<{ team: readonly string[] }>('/api/player/team', { npcIds })`
2. On success: call `refreshState()` to get full updated GameState
3. On error: set error with message from API

**refreshState logic:**
1. Call `apiGet<GameState>('/api/game/state')`
2. On success: set gameState
3. On error: set error

**dismissError logic:**
1. Set error to null

---

#### 5.5.3 ErrorBanner

**File:** `client/src/components/ErrorBanner.tsx`
**Export:** `export function ErrorBanner({ message, onDismiss }: ErrorBannerProps)` (named, A4)

**Props interface:**
```typescript
interface ErrorBannerProps {
  message: string;    // required -- error message text
  onDismiss: () => void; // required -- callback when user dismisses
}
```

**Local state:** None
**Renders:** A `<div role="alert">` containing the message text and a dismiss button.

**Styling file:** `client/src/components/ErrorBanner.module.css`
- `.banner` -- `background: #5c2020; border: var(--border-width) solid #8b3030; border-radius: var(--border-radius-md); padding: var(--space-sm) var(--space-md); display: flex; justify-content: space-between; align-items: center; margin-bottom: var(--space-md); color: var(--color-text-primary);`
- `.message` -- `flex: 1; font-size: var(--font-size-sm);`
- `.dismissButton` -- `background: none; border: none; color: var(--color-text-secondary); font-size: var(--font-size-lg); padding: var(--space-xs); line-height: 1;`
- `.dismissButton:hover` -- `color: var(--color-text-primary);`

**Accessibility:**
- Container: `role="alert"` (implicit live region, announces on render)
- Dismiss button: `aria-label="Dismiss error"`
- Dismiss button text content: Unicode multiply sign (x)

---

#### 5.5.4 LoadingScreen

**File:** `client/src/components/LoadingScreen.tsx`
**Export:** `export function LoadingScreen()` (named, A4)

**Props:** None
**Local state:** None
**Renders:** A centered container with text "Loading..." and a CSS-animated pulse effect.

**Styling file:** `client/src/components/LoadingScreen.module.css`
- `.container` -- `display: flex; justify-content: center; align-items: center; min-height: 200px;`
- `.text` -- `font-size: var(--font-size-lg); color: var(--color-text-secondary); animation: pulse 1.5s ease-in-out infinite;`
- `@keyframes pulse` -- `0%, 100% { opacity: 1; } 50% { opacity: 0.4; }`
- `@media (prefers-reduced-motion: reduce)` -- `.text { animation: none; }`

**Accessibility:**
- Container: `role="status"`, `aria-label="Loading game state"`

---

#### 5.5.5 StaminaBar

**File:** `client/src/components/StaminaBar.tsx`
**Export:** `export function StaminaBar({ current, max, label }: StaminaBarProps)` (named, A4)

**Props interface:**
```typescript
interface StaminaBarProps {
  current: number;   // required -- current stamina value
  max: number;       // required -- max stamina value
  label: string;     // required -- combatant name or identifier for a11y
}
```

**Local state:** None

**Derived values (computed inline, no memoization needed -- simple arithmetic):**
- `percentage`: `max > 0 ? Math.round((current / max) * 100) : 0`
- `staminaState`: determined by percentage thresholds:
  - `percentage > 60` -> `'green'`
  - `percentage > 40` -> `'yellow'`
  - `percentage > 20` -> `'orange'`
  - `percentage > 0` -> `'red'`
  - `percentage === 0` -> `'black'`

**Renders:**
```
<div class={styles.container} role="progressbar"
     aria-valuenow={current} aria-valuemin={0} aria-valuemax={max}
     aria-label={`${label} stamina: ${current} of ${max}`}>
  <div class={styles.track}>
    <div class={styles.fill} data-state={staminaState}
         style={{ width: `${percentage}%` }} />
  </div>
  <span class={styles.label}>{current}/{max}</span>
</div>
```

**Styling file:** `client/src/components/StaminaBar.module.css`
- `.container` -- `display: flex; align-items: center; gap: var(--space-sm);`
- `.track` -- `flex: 1; height: 16px; background: var(--color-metal-dark); border-radius: var(--border-radius-sm); border: 1px solid var(--color-border-frame); overflow: hidden; box-shadow: var(--shadow-inset);`
- `.fill` -- `height: 100%; transition: width var(--transition-normal); border-radius: var(--border-radius-sm) 0 0 var(--border-radius-sm);`
- `.fill[data-state="green"]` -- `background: linear-gradient(to bottom, var(--color-stamina-green-light), var(--color-stamina-green));` (A3, D4)
- `.fill[data-state="yellow"]` -- `background: linear-gradient(to bottom, var(--color-stamina-yellow-light), var(--color-stamina-yellow));`
- `.fill[data-state="orange"]` -- `background: linear-gradient(to bottom, var(--color-stamina-orange-light), var(--color-stamina-orange));`
- `.fill[data-state="red"]` -- `background: linear-gradient(to bottom, var(--color-stamina-red-light), var(--color-stamina-red));`
- `.fill[data-state="black"]` -- `background: var(--color-stamina-black);`
- `.label` -- `font-size: var(--font-size-xs); color: var(--color-text-secondary); min-width: 60px; text-align: right; font-variant-numeric: tabular-nums;`
- `@media (prefers-reduced-motion: reduce)` -- `.fill { transition: none; }`

**Accessibility:**
- `role="progressbar"` with `aria-valuenow`, `aria-valuemin`, `aria-valuemax`
- `aria-label` includes combatant name and fraction

---

#### 5.5.6 EnergyDisplay

**File:** `client/src/components/EnergyDisplay.tsx`
**Export:** `export function EnergyDisplay({ current, max, label }: EnergyDisplayProps)` (named, A4)

**Props interface:**
```typescript
interface EnergyDisplayProps {
  current: number;   // required -- current energy segments
  max: number;       // required -- max energy segments (from Combatant.maxEnergy, A7)
  label: string;     // required -- combatant name for a11y
}
```

**Local state:** None

**Derived values:**
- `segments`: Array of length `max`, each element is `index < current ? 'filled' : 'empty'`

**Renders:**
```
<div class={styles.container} role="meter"
     aria-valuenow={current} aria-valuemin={0} aria-valuemax={max}
     aria-label={`${label} energy: ${current} of ${max}`}>
  {segments.map((state, i) => (
    <div key={i} class={styles.segment} data-filled={state === 'filled'} />
  ))}
</div>
```

**Styling file:** `client/src/components/EnergyDisplay.module.css`
- `.container` -- `display: flex; gap: 3px; align-items: center;`
- `.segment` -- `width: 12px; height: 20px; border-radius: 2px; border: 1px solid var(--color-energy-border); transition: background-color var(--transition-fast);`
- `.segment[data-filled="true"]` -- `background-color: var(--color-energy-filled); box-shadow: 0 0 4px rgba(80, 136, 192, 0.3);`
- `.segment[data-filled="false"]` -- `background-color: var(--color-energy-empty);`
- `@media (prefers-reduced-motion: reduce)` -- `.segment { transition: none; }`

**Accessibility:**
- `role="meter"` with `aria-valuenow`, `aria-valuemin`, `aria-valuemax`
- `aria-label` includes combatant name and fraction

---

#### 5.5.7 PersonalityBreakdown

**File:** `client/src/components/PersonalityBreakdown.tsx`
**Export:** `export function PersonalityBreakdown({ personality }: PersonalityBreakdownProps)` (named, A4)

**Props interface:**
```typescript
interface PersonalityBreakdownProps {
  personality: Personality; // required -- the 6-trait personality object
}
```

**Local state:**
| State var | Type | Initial | Trigger |
|-----------|------|---------|---------|
| `expanded` | `boolean` | `false` | Toggled by clicking the header button |

**Renders:**
```
<div class={styles.container}>
  <button class={styles.header}
          onClick={() => setExpanded(!expanded)}
          aria-expanded={expanded}
          aria-controls="personality-details">
    <span class={styles.headerText}>Personality</span>
    <span class={styles.chevron} data-expanded={expanded}>&#9660;</span>
  </button>
  {expanded && (
    <div id="personality-details" class={styles.details} role="region" aria-label="Personality traits">
      {PERSONALITY_TRAITS.map(trait => (
        <div key={trait} class={styles.traitRow}>
          <span class={styles.traitName}>{capitalize(trait)}</span>
          <div class={styles.traitBar}>
            <div class={styles.traitFill}
                 style={{ width: `${personality[trait]}%`, backgroundColor: `var(--color-trait-${trait})` }} />
          </div>
          <span class={styles.traitValue}>{personality[trait]}%</span>
        </div>
      ))}
    </div>
  )}
</div>
```

**Utility function (inline):**
- `capitalize(s: string): string` -- `s.charAt(0).toUpperCase() + s.slice(1)`

**Styling file:** `client/src/components/PersonalityBreakdown.module.css`
- `.container` -- `border: var(--border-width) solid var(--color-border-frame); border-radius: var(--border-radius-md); overflow: hidden;`
- `.header` -- `width: 100%; display: flex; justify-content: space-between; align-items: center; padding: var(--space-sm) var(--space-md); background: var(--color-bg-panel); border: none; cursor: pointer; transition: background-color var(--transition-fast);`
- `.header:hover` -- `background: var(--color-bg-panel-hover);`
- `.header:focus-visible` -- `outline: 2px solid var(--color-border-accent); outline-offset: -2px;`
- `.headerText` -- `font-size: var(--font-size-sm); font-weight: var(--font-weight-medium); color: var(--color-text-primary);`
- `.chevron` -- `font-size: var(--font-size-xs); color: var(--color-text-secondary); transition: transform var(--transition-fast);`
- `.chevron[data-expanded="true"]` -- `transform: rotate(180deg);`
- `.details` -- `padding: var(--space-sm) var(--space-md); background: var(--color-bg-panel); display: flex; flex-direction: column; gap: var(--space-xs);`
- `.traitRow` -- `display: flex; align-items: center; gap: var(--space-sm);`
- `.traitName` -- `font-size: var(--font-size-xs); color: var(--color-text-secondary); min-width: 70px;`
- `.traitBar` -- `flex: 1; height: 8px; background: var(--color-metal-dark); border-radius: var(--border-radius-sm); overflow: hidden;`
- `.traitFill` -- `height: 100%; border-radius: var(--border-radius-sm); transition: width var(--transition-normal);`
- `.traitValue` -- `font-size: var(--font-size-xs); color: var(--color-text-secondary); min-width: 32px; text-align: right; font-variant-numeric: tabular-nums;`
- `@media (prefers-reduced-motion: reduce)` -- `.chevron, .traitFill { transition: none; }`

**Accessibility:**
- Header is a `<button>` -- keyboard accessible by default (Enter/Space)
- `aria-expanded` reflects current state
- `aria-controls` links button to details region
- Details `role="region"` with `aria-label`
- `:focus-visible` outline on header button

---

#### 5.5.8 TeamSelector

**File:** `client/src/components/TeamSelector.tsx`
**Export:** `export function TeamSelector({ currentTeam, npcs, onSetTeam, disabled }: TeamSelectorProps)` (named, A4)

**Props interface:**
```typescript
interface TeamSelectorProps {
  currentTeam: readonly string[];  // required -- current team NPC IDs (0 or 2 items)
  npcs: NPC[];                     // required -- all available NPCs
  onSetTeam: (npcIds: [string, string]) => Promise<void>; // required -- calls setTeam from context
  disabled: boolean;               // required -- true during combat or narrative
}
```

**Local state:**
| State var | Type | Initial | Trigger |
|-----------|------|---------|---------|
| `selectedIds` | `Set<string>` | `new Set(currentTeam)` | Toggled by clicking NPC buttons |
| `submitting` | `boolean` | `false` | Set true during API call, false after |

**Note:** `selectedIds` is re-initialized from `currentTeam` prop. Use a `useEffect` that syncs `selectedIds` when `currentTeam` changes (covers the case where server state updates from another source).

**Derived values:**
- `canSubmit`: `selectedIds.size === 2 && !submitting && !disabled && !setsEqual(selectedIds, new Set(currentTeam))`
- `setsEqual(a, b)`: inline utility -- `a.size === b.size && [...a].every(x => b.has(x))`

**Interaction handlers:**
- `toggleNpc(id: string)`: If `selectedIds` has `id`, remove it. If `selectedIds` size < 2 and does not have `id`, add it. If size is 2 and does not have `id`, no-op (must deselect one first).
- `handleSubmit()`: Set submitting true, call `onSetTeam([...selectedIds] as [string, string])`, set submitting false. On error: error is handled by GameStateProvider (propagates to ErrorBanner).

**Renders:**
```
<div class={styles.container}>
  <h3 class={styles.heading}>Team</h3>
  <div class={styles.npcList} role="group" aria-label="Select team members">
    {npcs.map(npc => (
      <button key={npc.id}
              class={styles.npcButton}
              data-selected={selectedIds.has(npc.id)}
              onClick={() => toggleNpc(npc.id)}
              disabled={disabled || (selectedIds.size >= 2 && !selectedIds.has(npc.id))}
              aria-pressed={selectedIds.has(npc.id)}>
        <span class={styles.npcName}>{npc.id === 'npc_scout_elena' ? 'Elena' : npc.id === 'npc_merchant_lars' ? 'Lars' : 'Kade'}</span>
        <span class={styles.npcArchetype}>{npc.archetype}</span>
      </button>
    ))}
  </div>
  <button class={styles.confirmButton}
          onClick={handleSubmit}
          disabled={!canSubmit}>
    {submitting ? 'Setting...' : 'Confirm Team'}
  </button>
</div>
```

**NPC display name mapping:** Extract display name from NPC id by taking the last segment after the final underscore and capitalizing. Alternatively, use a simple lookup since there are only 3 NPCs:
```typescript
function displayName(npc: NPC): string {
  const parts = npc.id.split('_');
  const name = parts[parts.length - 1];
  return name.charAt(0).toUpperCase() + name.slice(1);
}
```

**Styling file:** `client/src/components/TeamSelector.module.css`
- `.container` -- `display: flex; flex-direction: column; gap: var(--space-sm);`
- `.heading` -- `font-size: var(--font-size-sm); font-weight: var(--font-weight-medium); color: var(--color-text-primary); margin: 0;`
- `.npcList` -- `display: flex; flex-direction: column; gap: var(--space-xs);`
- `.npcButton` -- `display: flex; justify-content: space-between; align-items: center; padding: var(--space-xs) var(--space-sm); background: var(--color-bg-panel); border: var(--border-width) solid var(--color-border-frame); border-radius: var(--border-radius-sm); cursor: pointer; transition: border-color var(--transition-fast), background-color var(--transition-fast);`
- `.npcButton:hover:not(:disabled)` -- `background: var(--color-bg-panel-hover);`
- `.npcButton:focus-visible` -- `outline: 2px solid var(--color-border-accent); outline-offset: 2px;`
- `.npcButton[data-selected="true"]` -- `border-color: var(--color-border-accent); background: var(--color-bg-panel-hover);`
- `.npcButton:disabled` -- `opacity: 0.5; cursor: not-allowed;`
- `.npcName` -- `font-size: var(--font-size-sm); font-weight: var(--font-weight-medium); color: var(--color-text-primary);`
- `.npcArchetype` -- `font-size: var(--font-size-xs); color: var(--color-text-secondary);`
- `.confirmButton` -- `padding: var(--space-xs) var(--space-md); background: var(--color-metal-dark); border: var(--border-width) solid var(--color-border-frame); border-radius: var(--border-radius-sm); color: var(--color-text-primary); font-size: var(--font-size-sm); font-weight: var(--font-weight-medium); transition: background-color var(--transition-fast);`
- `.confirmButton:hover:not(:disabled)` -- `background: var(--color-metal-mid);`
- `.confirmButton:focus-visible` -- `outline: 2px solid var(--color-border-accent); outline-offset: 2px;`
- `.confirmButton:disabled` -- `opacity: 0.4; cursor: not-allowed;`
- `@media (prefers-reduced-motion: reduce)` -- `.npcButton, .confirmButton { transition: none; }`

**Accessibility:**
- NPC buttons: `aria-pressed` for toggle state
- Group: `role="group"` with `aria-label`
- Disabled state communicated via `disabled` attribute (native)
- `:focus-visible` outlines on all buttons

---

#### 5.5.9 EquinoxHUD (shell)

**File:** `client/src/components/EquinoxHUD.tsx`
**Export:** `export function EquinoxHUD()` (named, A4)

**Props:** None (consumes context directly)

**Hook usage:**
- `useGameState()` -- destructures `gameState`, `npcs`, `loading`, `error`, `setTeam`, `dismissError`

**Derived values:**
- `playerCombatant`: If `gameState.combatState` is non-null, find the first entry in `gameState.combatState.playerParty` where `id` starts with `'player_'`. May be null if player combatant not found (defensive).
- `isInCombat`: `gameState?.combatState !== null`
- `isInNarrative`: `gameState?.narrativeState !== null` (for disabling team selector -- narrativeState is on GameState, but we omitted it from frontend types; use a simple check: `(gameState as any)?.narrativeState != null` OR add `narrativeState: unknown | null` to the frontend GameState type)

**Resolution for narrativeState:** Add `readonly narrativeState: unknown | null;` to the frontend `GameState` type. This is a minimal addition that avoids importing narrative types while allowing the team-lock check.

**Renders:**
```
<div class={styles.hud}>
  <div class={styles.frame}>
    <h2 class={styles.title}>{gameState.player.name}</h2>

    {error && <ErrorBanner message={error} onDismiss={dismissError} />}

    {loading && <LoadingScreen />}

    {!loading && gameState && (
      <>
        {/* Combat section -- conditional (D1) */}
        {playerCombatant && (
          <section class={styles.combatSection} aria-label="Combat stats">
            <StaminaBar
              current={playerCombatant.stamina}
              max={playerCombatant.maxStamina}
              label={gameState.player.name} />
            <EnergyDisplay
              current={playerCombatant.energy}
              max={playerCombatant.maxEnergy}
              label={gameState.player.name} />
          </section>
        )}

        {/* Personality -- always visible */}
        <section class={styles.personalitySection} aria-label="Personality">
          <PersonalityBreakdown personality={gameState.player.personality} />
        </section>

        {/* Team selector -- always visible, disabled during combat/narrative */}
        <section class={styles.teamSection} aria-label="Team selection">
          <TeamSelector
            currentTeam={gameState.team}
            npcs={npcs}
            onSetTeam={setTeam}
            disabled={isInCombat || isInNarrative} />
        </section>
      </>
    )}
  </div>
</div>
```

**Styling file:** `client/src/components/EquinoxHUD.module.css`
- `.hud` -- `width: 100%; max-width: 400px;`
- `.frame` -- `background: var(--color-bg-panel); border: var(--border-width) solid var(--color-border-frame); border-radius: var(--border-radius-md); padding: var(--space-md); box-shadow: var(--shadow-panel); display: flex; flex-direction: column; gap: var(--space-md);`
- `.frame::before` -- `content: ''; display: block; height: 3px; background: linear-gradient(to right, transparent, var(--color-border-accent), transparent); margin: 0 calc(-1 * var(--space-md)); margin-top: calc(-1 * var(--space-md)); border-radius: var(--border-radius-md) var(--border-radius-md) 0 0;` (top accent line for "armor" feel)
- `.title` -- `font-size: var(--font-size-lg); font-weight: var(--font-weight-bold); color: var(--color-metal-light); text-align: center; letter-spacing: 0.05em;`
- `.combatSection` -- `display: flex; flex-direction: column; gap: var(--space-sm);`
- `.personalitySection` -- (no special styles beyond container)
- `.teamSection` -- (no special styles beyond container)

**Accessibility:**
- Each section wrapped in `<section>` with `aria-label`
- HUD frame has no interactive role itself (purely structural)

---

### 5.6 State Management Summary

**Single context pattern:**

```
GameStateProvider (owns state)
  |
  +-- fetch /api/game/state on mount
  |   (if 404 -> POST /api/game/new -> re-fetch)
  |
  +-- exposes: gameState, npcs, loading, error, setTeam, refreshState, dismissError
  |
  +-- EquinoxHUD (reads gameState, npcs, loading, error)
       |
       +-- StaminaBar (reads playerCombatant.stamina/maxStamina)
       +-- EnergyDisplay (reads playerCombatant.energy/maxEnergy)
       +-- PersonalityBreakdown (reads gameState.player.personality)
       +-- TeamSelector (reads gameState.team, npcs; dispatches setTeam)
       +-- ErrorBanner (reads error; dispatches dismissError)
```

**Data flow:**
- All state lives in GameStateProvider
- Child components receive data via context (useGameState hook) or props
- Mutations flow up through action methods (setTeam) which call the API then refresh state
- No local component state holds server data (except TeamSelector's selectedIds for optimistic UI during selection)

---

### 5.7 Updated Frontend GameState Type

Add `narrativeState` field to support team-lock check:

```typescript
export interface GameState {
  readonly player: PlayerCharacter;
  readonly npcs: Record<string, NPC>;
  readonly team: readonly string[];
  readonly combatState: CombatState | null;
  readonly narrativeState: unknown | null;  // added for team-lock check
  readonly timestamp: number;
}
```

---

## 6. Acceptance Mapping

### T1: Initialize Vite + React project
- **AC1** (dev server renders component): `client/package.json` scripts, `client/vite.config.ts`, `client/src/main.tsx` renders `<App />`
- **AC2** (API proxy without CORS): `client/vite.config.ts` proxy config targeting `http://localhost:3000` for `/api` prefix (A5)
- **AC3** (TypeScript strict + DOM): `client/tsconfig.json` with `strict: true`, `lib: ["ES2022", "DOM", "DOM.Iterable"]`
- **AC4** (concurrent dev servers): Root `package.json` `dev:all` script with `concurrently`

### T2: Design token system and base styles
- **AC1** (complete color palette): `client/src/styles/tokens.css` defines leather browns, metal grays, and all 5 stamina color states as CSS custom properties
- **AC2** (base reset + dark bg): `client/src/styles/reset.css` resets browser defaults; body background `var(--color-bg-deep)` (#1a1410)
- **AC3** (downstream token access): All component `.module.css` files reference tokens via `var(--token-name)`

### T3: GameStateProvider context
- **AC1** (fetch on mount): `GameStateProvider` useEffect calls `apiGet('/api/game/state')` on mount (A1), auto-creates if 404 (A2)
- **AC2** (child access to state): `useGameState()` hook returns `gameState` containing player, personality, team; combat stats available when `combatState` is non-null
- **AC3** (action methods + refresh): `setTeam` calls POST /api/player/team then refreshes; `refreshState` re-fetches full state
- **AC4** (loading/error states): `loading` and `error` exposed on context value; consumed by EquinoxHUD to show LoadingScreen/ErrorBanner

### T4: Stamina bar and energy display
- **AC1** (stamina percentage + color): `StaminaBar` computes percentage, sets `data-state` attribute for 5 threshold states
- **AC2** (CSS-only color transitions): `StaminaBar.module.css` uses `[data-state="..."]` attribute selectors with gradient fills (A3, D4), no JS style injection for colors
- **AC3** (energy filled/empty segments): `EnergyDisplay` renders `max` segments, fills `current` of them (A7 -- uses actual maxEnergy)
- **AC4** (updates on state change): Both consume props from EquinoxHUD which reads context; re-render when context updates

### T5: Stance, personality, and path selector
- **AC1** (stance indicator): DEFERRED to Sprint 7 per D2. Not implemented.
- **AC2** (personality expand/collapse): `PersonalityBreakdown` with click-to-toggle (D5), shows 6 traits with percentage values
- **AC3** (team selector): `TeamSelector` displays available NPCs, allows selecting exactly 2 (D3 reinterpretation)
- **AC4** (team switch API + update): `TeamSelector.handleSubmit` calls `onSetTeam` -> `setTeam` -> POST /api/player/team -> `refreshState`

### T6: Compose HUD shell
- **AC1** (cohesive layout): `EquinoxHUD` composes StaminaBar, EnergyDisplay, PersonalityBreakdown, TeamSelector in a flex column layout
- **AC2** (armor-like frame): `.frame` with panel bg, border, shadow, accent line pseudo-element for armor aesthetic
- **AC3** (interactive elements function): PersonalityBreakdown expand/collapse and TeamSelector selection/confirm work within composed HUD
- **AC4** (live backend data): `App.tsx` wraps EquinoxHUD in GameStateProvider; all data from GET /api/game/state

---

## 7. Integration Points

| Integration | File | Identifier | Direction |
|-------------|------|-----------|-----------|
| Backend game state | `src/api/game.ts` | `GET /api/game/state` | Frontend reads |
| Backend game create | `src/api/game.ts` | `POST /api/game/new` | Frontend writes |
| Backend team set | `src/api/player.ts` | `POST /api/player/team` | Frontend writes |
| Backend NPC list | `src/api/npc.ts` | `GET /api/npc` | Not directly used (NPCs from GameState.npcs) |
| API response envelope | `src/types/index.ts:170-174` | `ApiResponse<T>` | Duplicated in `client/src/types/index.ts` |
| GameState shape | `src/types/index.ts:95-105` | `GameState` interface | Duplicated (subset) in `client/src/types/index.ts` |
| Combatant shape | `src/types/combat.ts:101-117` | `Combatant` interface | Duplicated (subset) in `client/src/types/index.ts` |
| Vite proxy | `client/vite.config.ts` | `/api` -> `localhost:3000` | Dev-time network |
| Concurrent startup | Root `package.json` | `dev:all` script | Process management |

---

## 8. Open Items

- [VERIFY] Confirm Vite 6 + React 19 compatibility at install time. Both are current stable releases as of this writing, but verify no peer dependency conflicts during `npm install`.
- [VERIFY] Confirm the `player_` prefix convention for identifying the player combatant in `playerParty`. Based on `src/state/gameState.ts:31` the player id is `player_${uuid}`, so filtering by `id.startsWith('player_')` should work, but verify against actual combat state initialization in `src/combat/` if available.
- [VERIFY] Confirm that `concurrently@^9.0.0` is the latest major version at execution time.

---

## 9. Producer Handoff

**Output format:** TypeScript + CSS source files in `client/` directory
**Producer:** code-writer
**Instruction tone:** Precise, mechanical. Follow the specification exactly. Do not invent props, state, or styles not specified.

### File Creation Order

1. **Root `package.json` edit** -- add `concurrently` devDependency and `dev:all` script (~5 lines changed)
2. **`client/package.json`** -- project manifest with dependencies (~20 lines)
3. **`client/tsconfig.json`** -- TypeScript config (~20 lines)
4. **`client/tsconfig.node.json`** -- Vite config TS support (~10 lines)
5. **`client/vite.config.ts`** -- Vite config with proxy (~20 lines)
6. **`client/index.html`** -- SPA entry HTML (~15 lines)
7. **`client/src/types/index.ts`** -- Frontend type definitions (~85 lines)
8. **`client/src/api/apiClient.ts`** -- Fetch wrapper with error handling (~60 lines)
9. **`client/src/styles/tokens.css`** -- Design tokens as CSS custom properties (~90 lines)
10. **`client/src/styles/reset.css`** -- Browser reset and base styles (~20 lines)
11. **`client/src/context/GameStateContext.tsx`** -- Provider + hook (~110 lines)
12. **`client/src/components/ErrorBanner.tsx`** -- Error banner component (~25 lines)
13. **`client/src/components/ErrorBanner.module.css`** -- Error banner styles (~20 lines)
14. **`client/src/components/LoadingScreen.tsx`** -- Loading state component (~15 lines)
15. **`client/src/components/LoadingScreen.module.css`** -- Loading styles with pulse animation (~15 lines)
16. **`client/src/components/StaminaBar.tsx`** -- Stamina bar component (~40 lines)
17. **`client/src/components/StaminaBar.module.css`** -- Stamina bar styles with data-state selectors (~35 lines)
18. **`client/src/components/EnergyDisplay.tsx`** -- Energy segment display component (~30 lines)
19. **`client/src/components/EnergyDisplay.module.css`** -- Energy display styles (~15 lines)
20. **`client/src/components/PersonalityBreakdown.tsx`** -- Expandable personality panel (~50 lines)
21. **`client/src/components/PersonalityBreakdown.module.css`** -- Personality styles with trait colors (~45 lines)
22. **`client/src/components/TeamSelector.tsx`** -- Team selection component (~75 lines)
23. **`client/src/components/TeamSelector.module.css`** -- Team selector styles (~40 lines)
24. **`client/src/components/EquinoxHUD.tsx`** -- HUD shell composing all sub-components (~65 lines)
25. **`client/src/components/EquinoxHUD.module.css`** -- HUD frame and layout styles (~30 lines)
26. **`client/src/App.tsx`** -- Root app component (~15 lines)
27. **`client/src/App.module.css`** -- App layout styles (~8 lines)
28. **`client/src/main.tsx`** -- Entry point rendering App (~15 lines)

**Total estimated:** ~28 files, ~1,060 lines

### Content Block Summary

Each file's content is fully specified in Section 5. The producer should:
1. Create the `client/` directory structure first
2. Follow the file creation order above (dependencies before dependents)
3. Run `npm install` in `client/` after creating package.json
4. Verify `npm run dev` starts successfully before proceeding with components
5. After all files are created, verify `npm run dev:all` from root starts both servers
6. Verify the HUD renders in browser at `http://localhost:5173` with backend running
