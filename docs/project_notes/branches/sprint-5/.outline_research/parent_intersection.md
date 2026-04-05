# Parent Intersection: Sprint 4 → Sprint 5

**Shared Components:**
- src/api/index.ts — Sprint 4 registered endpoint plugins; Sprint 5 calls all endpoints from React
- src/types/index.ts — Sprint 4 validated GameState shape; Sprint 5 GameStateProvider uses these types
- src/persistence/saveLoad.ts — Sprint 4 wired list/delete saves; Sprint 5 consumes these endpoints
- ApiResponse<T> envelope — Sprint 4 standardized; Sprint 5 frontend parses this contract

**Inherited Decisions:**
- D2: Immutable state pattern → frontend must mirror
- ADR-016: ESM modules with .js extensions
- Backend on port 3000 → React needs dev proxy/CORS
- Party size = 3, personality constraints enforced by backend

**Conflicts:** None. Sprint 5 constraint: "No backend modifications."

**Patterns to Reuse:**
- ApiResponse<T> parsing in frontend
- Immutable state updates in React context
- Modular organization (separate providers per domain)
