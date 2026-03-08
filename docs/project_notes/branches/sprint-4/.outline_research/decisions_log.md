# Sprint 4 Decisions Log

## Validation Strategy
- **Decision**: How validateGameState() handles invalid state on load
- **Choice**: Reject with clear typed error
- **Status**: Locked
- **Rationale**: Simpler implementation, easier debugging, no risk of silent data corruption. Frontend shows "save corrupted" message.
- **Alternatives**: Attempt repair (complex, masks bugs), Reject with detailed logging (partial overlap with chosen approach — error details can be included in the rejection response)

## Endpoint Scope
- **Decision**: Which missing endpoints to implement
- **Choice**: 4 truly missing routes (saves list/delete, personality GET, team POST), not 8
- **Status**: Recommended
- **Rationale**: Audit reveals 3 "gaps" are intentional deviations from Sprint 1 spec made during implementation (dialogue path structure, load method). These have 969 tests behind them and the brief says "existing endpoint behavior unchanged."
- **Alternatives**: Align all endpoints to Sprint 1 spec (would break existing tests and patterns)
