# Research Plan — Frontend Component Specialist

## Research Plan

### R1: Backend API endpoint inventory and response shapes
Read the following files in full to extract every registered route, its HTTP method and path, the request parameters it accepts, and the shape of the data it returns inside the `ApiResponse<T>` envelope:
- `C:\Users\taylo\Claude_Projects\Ancient-Order\src\api\game.ts`
- `C:\Users\taylo\Claude_Projects\Ancient-Order\src\api\player.ts`
- `C:\Users\taylo\Claude_Projects\Ancient-Order\src\api\npc.ts`
- `C:\Users\taylo\Claude_Projects\Ancient-Order\src\api\narrative.ts`
- `C:\Users\taylo\Claude_Projects\Ancient-Order\src\api\dialogue.ts`
- `C:\Users\taylo\Claude_Projects\Ancient-Order\src\api\combat.ts`
- `C:\Users\taylo\Claude_Projects\Ancient-Order\src\api\index.ts`

Why it matters: Task T3 (`[SPEC]` decision) requires determining which specific endpoints GameStateProvider calls on initial mount and how to combine responses to build the state shape that feeds T4/T5 components (stamina, energy, stance, personality, active path). Without the actual route paths, request shapes, and response data, it is impossible to specify that API integration or to define the frontend `GameState` interface.

### R2: Backend type definitions and ApiResponse contract
Read `C:\Users\taylo\Claude_Projects\Ancient-Order\src\types\index.ts` in full to extract all exported TypeScript interfaces and types — particularly `GameState`, `PlayerState`, `Personality`, `CombatState`, `ActionResult`, stance enums, path types, and the `ApiResponse<T>` generic wrapper.

Why it matters: The frontend `GameStateProvider` context type must align with backend data shapes to avoid runtime mismatch. The `[SPEC]` decision for T3 requires knowing the exact field names for stamina (percentage vs raw value), energy (current vs max), stance (string literal union vs enum), personality trait names, and active path identifier. The `[USER]` decisions for T4 (stamina bar) and T5 (personality breakdown, path selector) need these field names to present realistic interaction options to the user. Knowing `ApiResponse<T>` exactly determines error-handling UX patterns.
