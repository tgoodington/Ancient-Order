# Planning Decisions Log

## Testing Scope (Sprint 1 Closeout)
- **Decision**: What testing scope to target for Sprint 1
- **Choice**: Unit tests only — personality system, dialogue engine, state updaters
- **Status**: Locked
- **Rationale**: Pure functions are trivially testable, cover ~80% of logic, minimal setup. API/persistence tests deferred.
- **Alternatives**: Unit + integration (supertest + fs mocks), Unit + integration + E2E scenarios

## Combat Engine Architecture
- **Decision**: How to structure the combat engine in code
- **Choice**: Pipeline architecture — staged pure functions transforming CombatState
- **Status**: Locked
- **Rationale**: Mirrors actual 5-phase combat flow, each stage independently testable, matches existing composition pattern (processDialogueChoice). Round Manager orchestrates phases.
- **Alternatives**: Subsystem-per-module (12 separate files, cross-import risk), Domain-grouped modules (4-5 grouped modules)

## Combat State Integration
- **Decision**: How CombatState relates to existing GameState
- **Choice**: Independent CombatState, sync at round boundaries
- **Status**: Locked
- **Rationale**: Combat pipeline owns focused CombatState during resolution (combatants, actions, queue). Merges into GameState.activeCombat after each round. Avoids expensive full-GameState copies on every micro-step.
- **Alternatives**: Nested inside GameState throughout (verbose), Hybrid with event log (more work, future frontend value)

## AI Decision System
- **Decision**: What approach for AI combat decision-making
- **Choice**: Behavior tree — full decision tree with priority nodes, conditions, fallbacks
- **Status**: Locked
- **Rationale**: User chose extensibility over speed. Sets up well for later sprints. Most flexible for NPC archetype differentiation.
- **Alternatives**: Rule-based with archetype profiles (recommended, simpler), Simple weighted random (fastest, least interesting)

## Formula Validation Strategy
- **Decision**: How to verify combat formulas after porting from Excel
- **Choice**: Test-driven porting — write tests first with known input/output pairs from Excel, then implement
- **Status**: Locked
- **Rationale**: Guarantees correctness for each formula category. Aligns with ADR-007 (exact replication). Provides regression protection for future balance changes.
- **Alternatives**: Manual spot-check (fast but error-prone), Golden file testing (full scenario diffs, requires Excel export setup)
