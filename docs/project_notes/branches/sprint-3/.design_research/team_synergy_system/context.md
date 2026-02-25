# Context Research: Team Synergy System

## Existing Work

### Personality System
- 6 traits: patience, empathy, cunning, logic, kindness, charisma
- Range: 5-35% each, sum = 100%
- Player personality is mutable; NPC personalities are frozen
- `adjustPersonality()` in `src/personality/personalitySystem.ts`

### NPC Archetypes (fixed)
- **Elena** (Loyal Scout): patience 20, empathy 20, cunning 10, logic 15, kindness 20, charisma 15
- **Lars** (Scheming Merchant): patience 10, empathy 8, cunning 28, logic 25, kindness 12, charisma 17
- **Kade** (Rogue Outlaw): patience 12, empathy 8, cunning 25, logic 18, kindness 10, charisma 27

### Combat Sync Layer
- `initCombatState(gameState, encounterConfig)` in `src/combat/sync.ts`
- Creates CombatState from EncounterConfig via `_configToCombatant()`
- Comment on line 77: "passed for future stat-sync extensibility" — anticipates synergy injection
- No synergy applied currently

### Combatant Stats
- Combatant has: rank, stamina, maxStamina, power, speed, energy, maxEnergy, ascensionLevel, activeBuffs, elementalPath, reactionSkills, isKO
- Stats are config-driven at combat init time
- CombatState is fully isolated from GameState (ADR-013)

### State Updater Pattern
- All follow: `(state: Readonly<GameState>, ...) => GameState`
- Spread to create new objects, `updateTimestamp()` wraps every transition

## Integration Boundaries

1. **Temporal**: Synergy bonus triggers at combat init. Must read from GameState, apply to Combatant stats.
2. **Structural**: Combatants have no personality field. Synergy must map personality → stat modifiers (power, speed) or inject as activeBuffs.
3. **Immutability**: Must produce new objects, not mutate.
4. **Scope**: Player personality only. NPC personalities are immutable archetypes.

## GROUP Action Boundary
- GROUP's 1.5x multiplier is a combat-time resolution mechanic (applies during GROUP action resolution)
- Team synergy is a pre-combat party composition bonus (applies at `initCombatState`)
- These are explicitly separate systems per ADR-013
