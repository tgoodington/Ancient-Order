## Research Plan

### R1: Type System and GameState Shape
Read `src/types/index.ts` and `src/types/combat.ts` in full. Need to understand the complete GameState interface, how combat types were added as a separate file, how interfaces compose (e.g., does GameState reference CombatState directly or optionally), and what NPC-related types exist (affection, trust, relationship). This is critical for T1 (narrative types) and determines whether narrative types go in a separate `types/narrative.ts` file or are added inline.

### R2: State Updaters and Pure Function Patterns
Read `src/state/stateUpdaters.ts` in full. Need to find the exact signatures for `updateNPCAffection`, `updateNPCTrust`, `updateNPCRelationship`, and `processDialogueChoice`. Understand the `(state: GameState, ...) => GameState` pattern, how immutability is enforced (spread operators, structuredClone, etc.), and how these functions chain.

### R3: Dialogue Engine as Scene Graph Precedent
Read `src/dialogue/dialogueEngine.ts` and any dialogue fixtures. The dialogue engine is the closest existing analog to the scene graph engine. Need to understand how it structures dialogue nodes, evaluates personality gates, enforces fallback paths, and exposes its public API.

### R4: Combat Sync Layer (Synergy Integration Point)
Read `src/combat/sync.ts` in full. This is where `initCombatState` lives and where synergy bonuses will be wired in. Need to understand the function signature, how it bridges GameState to CombatState.

### R5: Persistence Round-Trip Pattern
Read `src/persistence/saveLoad.ts` in full. Need to understand serialization format, how GameState is saved/loaded, backward compatibility handling.

### R6: Fastify API Plugin Pattern
Read `src/api/index.ts` and one existing plugin (e.g., dialogue plugin). Need to understand the plugin registration pattern, JSON Schema validation approach, ApiResponse envelope structure, error response conventions.

### R7: GameState Management and NPC State
Read `src/state/gameState.ts` or equivalent and NPC state files. Need to understand how GameState is initialized, how NPC state is structured.

### R8: Test Patterns and Fixtures
Read representative test files to understand organization, fixture creation patterns, assertion style.

### R9: Team Synergy Design Spec
Retrieve `design_spec_team_synergy_system.md` from git history — the approved spec for T7 and T8.

### R10: Configuration and Fixtures Pattern
Find data-driven configuration files and fixture patterns (e.g., encounter configs, NPC templates).
