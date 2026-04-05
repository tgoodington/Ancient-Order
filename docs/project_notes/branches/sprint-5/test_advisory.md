# Test Advisory — Sprint 5

Domain-specific testing knowledge extracted from specialist blueprints.

## Frontend Component

- **apiClient.ts**: Pure fetch wrapper — test with mock fetch. Edge cases: network failure, non-JSON response, API error envelope with `success: false`.
- **GameStateProvider**: Test the 404-then-create flow, loading/error states, and that context values propagate. Mock apiClient.
- **StaminaBar**: Test all 5 color threshold boundaries (>60%, 40-60%, 20-40%, 1-20%, 0%). Test that `data-state` attribute changes correctly at exact boundaries.
- **EnergyDisplay**: Test filled vs empty segment count at 0, partial, and max energy. Verify maxEnergy from data (A7), not hardcoded.
- **PersonalityBreakdown**: Test expand/collapse toggle, all 6 traits render, percentage values sum display.
- **TeamSelector**: Test NPC listing, selection of exactly 2, API call on confirm, disabled state during combat/narrative.
- **Error boundary**: Verify ErrorBanner shows on API failure, dismisses correctly.
