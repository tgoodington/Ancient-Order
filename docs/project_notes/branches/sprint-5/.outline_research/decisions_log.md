# Decisions Log — Sprint 5 Outline

## React Toolchain
- **Decision**: Which React build tool and project structure to use
- **Choice**: Vite + React in `client/` subfolder with own package.json and tsconfig.json
- **Status**: Locked
- **Rationale**: ESM-native aligns with ADR-016, Vitest config sharing, no SSR/routing overhead needed for single-player game. Proxy to backend port 3000 is trivial in Vite config.
- **Alternatives**: Next.js (rejected — SSR/API routes unused, adds complexity), separate repo (rejected — unnecessary for solo dev)

## Styling Approach
- **Decision**: CSS framework/methodology for game UI
- **Choice**: CSS Modules with CSS custom properties for design tokens
- **Status**: Locked
- **Rationale**: Game aesthetic (armor/leather/metal) requires hand-crafted styles that fight utility frameworks. CSS Modules provide scoping built into Vite. Stamina color states map to data-attribute selectors (pure CSS). Zero runtime cost.
- **Alternatives**: Tailwind (rejected — arbitrary value overrides dominate for custom aesthetic), Styled Components (rejected — runtime cost for problem CSS already solves)

## State Management Strategy
- **Decision**: How GameStateProvider refreshes data from backend
- **Choice**: On-demand fetch (load on mount, re-fetch after user actions)
- **Status**: Locked
- **Rationale**: Single-player game — no external actors modify state. Polling and SSE/WebSocket add complexity for state changes that only occur on user input.
- **Alternatives**: Polling (rejected — wasted requests), event-driven/SSE (rejected — requires backend modifications ruled out of scope)
