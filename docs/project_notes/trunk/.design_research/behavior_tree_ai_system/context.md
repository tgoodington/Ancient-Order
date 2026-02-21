# Context Research: Behavior Tree AI System

**Date:** 2026-02-21

## Existing Work

- No prior behavior tree implementation exists anywhere in the project
- Combat was never implemented in the archived Sprint 1 prototype
- ADR-014 established that behavior tree AI requires design before implementation
- ADR-015 mandates TDD approach for all combat formula porting

## NPC Archetypes (Fixed)

1. **Elena (Loyal Scout, DEUS)** — Healer/support. Patience 20%, Empathy 20%, Kindness 20%, Cunning 10%, Logic 15%, Charisma 15%
2. **Lars (Scheming Merchant, Neutral)** — Tank/defender. Cunning 28%, Logic 25%, Charisma 17%, Kindness 12%, Patience 10%, Empathy 8%
3. **Kade (Rogue Outlaw, Rogues)** — Striker/aggressive. Charisma 27%, Cunning 25%, Logic 18%, Patience 12%, Kindness 10%, Empathy 8%

## Integration Context

- **Entry point:** Phase 1 of 5-phase combat round pipeline (hidden from player)
- **Interface:** `evaluate(combatant: Combatant, state: CombatState): CombatAction`
- **Called by:** `combat/roundManager.ts` — once per NPC per round
- **Output:** Single `CombatAction` per combatant (actionType, targetId, energySegments)
- **Constraint:** Must not mutate state, must be deterministic, must be pure

## Combat State Available to AI

- Combatant stats: stamina (current/max), power, speed, energy segments, ascension level
- Defensive skills: block/dodge/parry with SR/SMR/FMR rates and ranks (1-11)
- Elemental path: current path, ascension level
- Active buffs/debuffs
- Round history (prior actions and outcomes)
- Team composition (both parties)

## Action Types (5 choices)

1. DEFEND (priority 1) — intercept ally's incoming attack
2. GROUP (priority 2) — team coordination (design pending)
3. ATTACK/SPECIAL (priority 3) — offensive; SPECIAL costs energy segments
4. EVADE (priority 4) — recover 30% max stamina + gain energy

## Documented Behavioral Patterns (from GM Combat Tracker)

- Targeting weak combatants (low stamina)
- Exploiting speed advantages (Blindside potential)
- Recognizing rank threats (Rank KO potential)
- Probing player defensive preferences
- Protecting damaged allies
- Strategic Special usage based on energy and defense constraints

## Architecture Constraints

- Lives in `combat/behaviorTree/` directory
- Data-driven archetype profiles (not hardcoded switch statements)
- Deterministic evaluation (same state → same decision)
- Pure function (no side effects, no state mutation)
- Type definitions in `types/combat.ts`
