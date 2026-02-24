# Sprint 3 Planning Decisions Log

## Scene Architecture
- **Decision**: How scenes are structured and stored
- **Choice**: Scene graph with prerequisites — directed graph of JSON nodes with IDs, content, choices (personality-gated), and prerequisite conditions (trait checks, prior choices). State machine tracks current scene + visited set + choice history.
- **Status**: Locked
- **Rationale**: Mirrors existing dialogue engine pattern (graph traversal with gates). Simple, testable, JSON-serializable. Natural fit for the "no dead ends" constraint.
- **Alternatives**: Flat scene list with transition rules (simpler but fragile), hierarchical acts/chapters (over-engineered for 4-6 scene demo)

## Consequence System
- **Decision**: How deep choice consequences propagate
- **Choice**: Local effects + named flags. Each choice has immediate effects (personality shift, NPC state change) plus sets flags in a choice history map. Future scene prerequisites and dialogue gates check these flags. 1-hop consequences, multiple readers.
- **Status**: Locked
- **Rationale**: Gives meaningful impact ("your choice in Scene 2 unlocked this") without unbounded complexity. Flag map is trivially serializable. Testable with known flag combinations.
- **Alternatives**: Ripple chains (immersive but hard to test), immediate only (choices feel shallow)

## Synergy Calculation
- **Decision**: How team synergy bonuses are calculated
- **Choice**: Deferred to design session
- **Status**: DESIGN REQUIRED
- **Rationale**: Game design decision with balancing implications. Needs deeper exploration of formula, pair vs team composition, stat bonus types, and player transparency. Clear boundary with GROUP action synergy (combat-time 1.5x multiplier is separate).
- **Alternatives**: TBD in design session

## Branching Strategy
- **Decision**: How Act 1 scene graph branches from player choices
- **Choice**: Deferred to design session
- **Status**: DESIGN REQUIRED
- **Rationale**: Game design decision intertwined with synergy calculation and scene content. Determines content volume, test path count, and demo pacing. Needs exploration of linear spine + variants vs true branching vs minimal branching in context of the full narrative design.
- **Alternatives**: TBD in design session

## Synergy-Combat Boundary
- **Decision**: How Sprint 3 synergy integrates with Sprint 2 combat
- **Choice**: Synergy bonuses applied at combat initialization via initCombatState (combat/sync.ts). Separate from GROUP action's 1.5x multiplier.
- **Status**: Locked
- **Rationale**: GROUP synergy is a combat-time resolution mechanic (1.5x flat multiplier on GROUP action). Team synergy is a pre-combat party composition bonus. Clean separation via the existing sync boundary (ADR-013).
- **Alternatives**: None — ADR-013 already defines this boundary
