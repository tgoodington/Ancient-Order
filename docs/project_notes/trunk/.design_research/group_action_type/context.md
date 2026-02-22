# Context Research: Group Action Type

## Existing Work

- GROUP is enumerated as a valid `ActionType` in the planned type system (`'ATTACK' | 'DEFEND' | 'EVADE' | 'SPECIAL' | 'GROUP'`)
- Priority 2 slot is reserved (after DEFEND, before ATTACK/SPECIAL)
- GM Combat Tracker Documentation (line 91): "Coordinated team action (inspired by Skies of Arcadia crew specials) - undefined"
- No mechanics, no implementation, no archived code for GROUP exists anywhere
- Task 15 creates a GROUP stub (returns no-op) — Task 18 replaces it with real implementation
- Behavior tree AI design spec has GROUP scoring fields in all 7 factors (currently 0 in most cases), disabled via `groupActionsEnabled: false` config flag

## Integration Context

### Must Work With:
- **CombatAction interface**: `{ combatantId, type: ActionType, targetId: string | null, energySegments?: number }`
- **Round Manager Phase 4**: Priority sort — GROUP at priority 2, Speed-based tie-breaking within priority
- **Round Manager Phase 5**: Per-attack pipeline (7 sub-steps: true target → Rank KO → Blindside → reaction → defense → counter → updates)
- **Defense system**: Block/Dodge/Parry with SR/SMR/FMR rates
- **Counter chain**: Parry success → counter attack → chain until failure/KO/stamina depletion
- **Behavior tree evaluator**: Must be able to score and select GROUP actions when enabled
- **3v3 structure**: 3 player allies, 3 enemy NPCs

### Boundaries:
- GROUP resolution must produce standard `CombatAction` outputs for the pipeline
- Must not mutate state (immutable, pure functions)
- Must be deterministic (same state → same result)
- Must be unit-testable for each combination and synergy scenario

### Archetype GROUP Base Scores (from BT design):
- Elena (Light, healer-support): GROUP base 0.2
- Lars (Earth, tank-defender): GROUP base 0.2
- Kade (Fire, striker-aggressive): GROUP base 0.1
