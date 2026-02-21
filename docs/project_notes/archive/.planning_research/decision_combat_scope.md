# Decision Research: Combat System Scope

## Full Subsystem Map (12 subsystems identified)

1. Round Structure (5 phases, sequential)
2. Action System (5 types: Attack, Defend, Evade, Special, Group)
3. Defense Resolution (4 states: Defenseless, Block, Dodge, Parry with SR/SMR/FMR)
4. Rank KO (speed dominance instant KO)
5. Blindside (speed dominance forced Defenseless)
6. Crushing Blow (power dominance Block debuff)
7. Path/Elemental System (6 paths, buff/debuff)
8. Energy/Ascension (segments, levels 0-3, accumulation bonuses)
9. Damage Calculation (multi-stage with mitigation)
10. Stamina Tracking (health pool, KO detection)
11. Counter Chains (Parry success cascading)
12. AI Decision System (NOT specified in docs — needs design)

## Critical Dependencies
- Rank KO blocks all defense reactions
- Blindside overrides chosen defense
- Crushing Blow requires Block defense
- Counter chains require Parry success, can cascade
- Action priority sorting gates resolution order
- Buff/debuff durations persist across rounds
- Energy accumulation modified by ascension level

## Data Structures Required
- Combatant (stats, defensive skills with ranks, path, ascension, buffs/debuffs)
- Action (actor, type, target, segments, priority)
- Combat Resolution (full attack result chain)
- Combat State (round tracking, action queue, history)

## AI Decision System Note
Documentation mentions future considerations (targeting, speed exploitation, threat recognition) but no system structure. Must be designed from scratch.
