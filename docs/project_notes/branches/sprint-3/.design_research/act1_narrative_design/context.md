# Act 1 Narrative Design - Context Research

## Existing Foundation

**No scene content exists yet.** The dialogue engine, personality system, and combat engine are all implemented and tested, but no narrative scene JSON files or story text have been authored.

## Act 1 Blueprint (from key_facts.md)

6 planned scenes:
1. The Championship Fight - Cinematic intro, tutorial combat, reveals player as kid
2. Town Exploration - 4 tasks to convince mentor Dontan (school, DEUS, Auntie M's cat, battle)
3. Dontan's Trials - Dialogue-based warrior worth tests
4. Time Skip - 5-year training montage (narrative only)
5. Setting Out - Team registration, final preparations, departure
6. First Gym Town - Act 1 ends, demonstrates personality gates with new NPCs

**POC scope: 2-3 scenes only.**

## Dialogue Engine Pattern (src/dialogue/)

- Node ID convention: `${npcId}_${descriptor}`
- Starting node: `${npcId}_greet`
- Node structure: id, npcId, text, options[]
- Option structure: id, text, optional gate, optional personalityAdjustment, optional npcAdjustment, nextNodeId
- Gate operators: gte (>=), lte (<=), eq (==) on personality traits
- Constraint: every node must have >= 1 ungated option (ADR-004)
- Validation: `validateDialogueTree()` checks for dead ends

## Personality System Constraints

- 6 traits: Patience, Empathy, Cunning, Logic, Kindness, Charisma
- Range: 5-35% per trait, sum = 100%
- Adjustment rate: up to 6% per choice
- NPCs have fixed personalities; only player changes

## State Update Pattern

- `applyPersonalityAdjustment(state, adjustment)` - personality changes
- `updateNPCRelationship(state, npcId, affectionChange, trustChange)` - NPC relationship changes
- `processDialogueChoice()` - logs conversation entry
- All updates are pure functions returning new state

## NPC Reference

- Elena (Loyal Scout): Patience+Empathy dominant, Light path, DEUS faction
- Lars (Scheming Merchant): Cunning+Logic dominant, Earth path, Neutral faction
- Kade (Rogue Outlaw): Charisma+Cunning dominant, Fire path, Rogues faction

## Team Synergy Connection

Well Rounded paradigm needs all 6 traits >= 25% across party. Bond paradigm needs 80% alignment with one NPC. Player personality choices in dialogue ripple into combat bonuses.

## Integration Points

- Scene graph engine (Task 2) will load and traverse scene JSON
- Choice engine (Task 3) processes selections and sets flags
- Narrative state machine (Task 4) tracks current scene, visited, flags
- Scenes must be JSON-serializable, static fixture data
- Combat transitions via `initCombatState()` with encounter configs
