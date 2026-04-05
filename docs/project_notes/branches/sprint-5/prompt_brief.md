# Prompt Brief: Sprint 5 — Frontend React Phase 1

## Problem Statement
The backend is complete through Sprint 4 with a full REST API surface, but there is no frontend. Sprint 5 bootstraps the React application and delivers the first player-facing component — the Equinox HUD — fully functional over live backend data. This is the foundation that Sprints 6 (town scene) and 7 (combat UI) will build upon.

## Commander's Intent
**Desired end state:** A player sees a polished, armor-themed HUD displaying their real game state (stamina, energy, stance, personality, path) with all interactive elements working — path switching, personality expansion, etc. The HUD feels like part of the game world, not a developer prototype.
**Non-negotiables:** HUD must connect to real backend API (not mock data); all HUD elements from the spec must be implemented and interactive; visual aesthetic matches the armor/leather design language.
**Boundaries:** Phase 1 frontend only — no town scene, dialogue UI, or combat UI. No backend modifications. Toolchain choice deferred to outline phase research.

## Success Criteria
- React app builds, runs, and connects to the existing Fastify backend on port 3000
- GameStateProvider context fetches and manages state from all relevant API endpoints
- Equinox HUD renders stamina bar (color-coded: Green 100-75%, Yellow 74-50%, Orange 49-25%, Red 24-1%, Black 0% KO)
- Equinox HUD renders energy segments (6 max), current stance indicator (A/D/E/S/G)
- Expandable personality breakdown displays all 6 traits with percentages
- Path selector allows switching between two paths
- All HUD interactions are functional (expand/collapse, path switch)
- Visual aesthetic matches minimal, armor-like design spec (browns, leather, metal)

## Scope
**In scope:**
- React project initialization and build toolchain setup (specific choice deferred to outline)
- GameStateProvider context wrapping API state synchronization
- Complete Equinox HUD component with all interactive elements from spec
- Visual styling matching the armor/leather aesthetic from the design spec
- Development proxy or CORS setup for backend communication

**Out of scope:**
- Town navigation / dialogue UI (Sprint 6)
- Combat UI / action declaration (Sprint 7)
- Backend code changes (API is complete from Sprint 4)
- Deployment pipeline / CI/CD
- Authentication / user management

## Constraints
- Must consume existing REST API without backend modifications
- Immutable state pattern (ADR-005) — frontend mirrors backend's approach
- ESM modules with .js extensions (ADR-016)
- Vercel deployment target (stateless)
- Existing backend runs on Fastify, port 3000

## Key Assumptions
| Assumption | Confidence | Basis |
|-----------|-----------|-------|
| Existing API endpoints provide all data the HUD needs | High | Sprint 4 completed full API surface with game state, player, personality, and narrative endpoints |
| No authentication needed for Phase 1 | Medium | Single-player local dev environment; no multi-user scenario yet |
| HUD design spec in handoff doc is complete enough to implement from | Medium | Spec lists all elements but may need creative decisions on exact interaction behavior |
| Backend CORS/proxy configuration is straightforward | High | Standard dev setup for React + API server |

## Open Questions for Planning
- React toolchain choice (Vite vs Next.js, styling approach — CSS Modules, Tailwind, Styled Components, etc.)
- Frontend project structure (monorepo subfolder like `client/` vs separate project)
- How GameStateProvider handles state refresh (polling interval, event-driven, or on-demand)
- Exact interaction behavior for path selector (API call on switch? Optimistic update?)
- Personality expansion UX details (animation, layout when expanded)

## Decision Posture
| Area | Posture | Notes |
|------|---------|-------|
| React toolchain & project structure | I decide | Outline recommends, user approves build tool, styling, and folder layout |
| HUD visual design & aesthetic | I decide | Outline recommends, user approves color palette, component styling, layout |
| GameStateProvider architecture | I decide | Outline recommends, user approves state management patterns and API integration |
