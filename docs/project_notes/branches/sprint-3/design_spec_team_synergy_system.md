# Design Specification: Team Synergy System

**Date:** 2026-02-24
**Status:** Approved
**Plan Reference:** Task 7 (Team Synergy Calculator), Task 8 (Combat Synergy Integration)
**Domain:** Game Design / Code Architecture

---

## 1. Overview

**Purpose:** Compute pre-combat stat bonuses based on the party's collective personality distribution. The system evaluates the current party against named paradigms — pattern archetypes describing a specific personality distribution across all members. The best-matching satisfied paradigm applies its bonus to all player party combatants at combat initialization.

**Scope:**
- Included: Two paradigms (Well Rounded, Bond), binary threshold activation, highest-only rule, pure calculator function, integration with `initCombatState`, REST API exposure
- Excluded: Negative synergies/penalties, per-paradigm stat type variation beyond ATK/SPD, stacking bonuses, partial/graduated bonuses, in-round synergy mechanics (GROUP's 1.5x is a separate combat-time system)

**Key Design Decisions:**
- **Paradigm-based model:** Named archetypes with trigger conditions, not pair-wise comparisons. Fully extensible — new paradigms added to config, no code changes.
- **Highest-only application:** Only the best-matching satisfied paradigm applies. Rewards commitment to a single party identity.
- **Direct stat modification:** Bonuses are applied by scaling `power` or `speed` at `initCombatState`. No new buff types, no pipeline changes.
- **Pure function, data-driven config:** `calculateSynergy()` is deterministic; paradigm definitions live in a configuration object, not hardcoded logic.
- **Bond dominant traits derived dynamically:** NPC dominant traits are the top 2 traits by value in their personality object — no manual tagging. Automatically extends to any new NPC.

---

## 2. Elements

### Core Types

```typescript
// The stat types synergy can modify
type SynergyStatType = 'power' | 'speed';

// The output of a satisfied paradigm — what bonus to apply and to whom
interface SynergyBonus {
  readonly paradigmName: string;         // e.g., "Well Rounded", "Bond"
  readonly stat: SynergyStatType;        // which stat gets modified
  readonly multiplier: number;           // e.g., 1.10 for +10%
  readonly matchQuality: number;         // 0.0–1.0+, used for highest-only comparison
}

// The result of the synergy calculation — null if no paradigm is satisfied
type SynergyResult = SynergyBonus | null;

// Configuration for a single paradigm (data-driven, not hardcoded)
interface ParadigmConfig {
  readonly name: string;
  readonly type: 'well_rounded' | 'bond';
  readonly threshold: number;            // e.g., 0.25 for Well Rounded, 0.80 for Bond
  readonly stat: SynergyStatType;
  readonly multiplier: number;           // e.g., 1.10 for +10%
}
```

### Paradigm Inventory (POC)

**Well Rounded**
- Condition: For every personality trait (patience, empathy, cunning, logic, kindness, charisma), the maximum value of that trait across all party members ≥ 25%
- Bonus: All player party combatants get `power × 1.10`
- Thematic rationale: The party collectively covers all facets — versatility translates to combat effectiveness
- Achievable in demo: NPCs cover cunning (Lars 28), logic (Lars 25), charisma (Kade 27). Player must contribute patience, empathy, kindness ≥25% each via supportive dialogue choices.

**Bond**
- Condition: The player's personality is ≥80% aligned with at least one NPC's dominant traits (alignment ratio = player's sum of NPC's top-2 traits / NPC's own sum of top-2 traits)
- Bonus: All player party combatants get `speed × 1.10`
- Thematic rationale: Deep resonance with an ally creates a coordinated fighting style — the whole party moves with greater purpose
- Achievable in demo: Player mirroring Lars (cunning+logic), Elena (patience+empathy+kindness — player needs top-2 of Elena's profile), or Kade (charisma+cunning) through consistent dialogue choices

### Element Inventory

- **ParadigmConfig** — immutable configuration object defining one paradigm's rules
- **SynergyBonus** — the result payload: winning paradigm, stat type, multiplier, match quality
- **SynergyResult** — `SynergyBonus | null`; null means no synergy bonus applies
- **Player personality** — mutable; read from `GameState.playerCharacter.personality`
- **NPC personalities** — immutable frozen objects; read from party member NPC records

### Boundaries & Ownership

- `src/narrative/synergyCalculator.ts` owns: paradigm evaluation, match quality scoring, highest-only selection, producing `SynergyResult`
- `src/combat/sync.ts` owns: calling the calculator at init, applying the bonus to combatant stats
- `src/api/narrative.ts` owns: exposing synergy result via REST endpoint (calls same calculator)
- `src/fixtures/synergyConfig.ts` owns: the default `ParadigmConfig[]` array (POC configuration)

---

## 3. Connections

### Relationship Map

- **`calculateSynergy()`** ← `GameState.playerCharacter.personality`: player personality input
- **`calculateSynergy()`** ← party NPC personalities (passed as array): party member inputs
- **`calculateSynergy()`** ← `ParadigmConfig[]`: paradigm definitions (data-driven config)
- **`initCombatState()`** → `calculateSynergy()`: called at combat init before creating combatants
- **`initCombatState()`** → Combatant construction: synergy bonus applied via `power × multiplier` or `speed × multiplier`
- **Narrative REST API** → `calculateSynergy()`: exposed as "get synergy bonuses for current party" endpoint

### Integration Points

- **`src/combat/sync.ts` — `initCombatState()`**: After `_configToCombatant()` creates base combatants, pass the player personality and party NPC personalities to `calculateSynergy()`. If result is non-null, map player party combatants applying the stat multiplier. Enemy party is unaffected.
- **`src/types/index.ts` — `SynergyBonus` type**: Added by Task 1 (Narrative Type System) as part of the narrative type extension.
- **`src/api/narrative.ts` — narrative plugin**: Task 9 exposes `GET /narrative/synergy` calling `calculateSynergy()` directly from `fastify.gameState`.

### Function Signature

```typescript
// src/narrative/synergyCalculator.ts

function calculateSynergy(
  playerPersonality: Readonly<Personality>,
  partyNpcPersonalities: ReadonlyArray<Readonly<Personality>>,
  paradigms: ReadonlyArray<ParadigmConfig>
): SynergyResult
```

- `playerPersonality`: the player's current personality (read from GameState)
- `partyNpcPersonalities`: personalities of the NPC party members (read from GameState NPC records)
- `paradigms`: the paradigm configuration array (default from `synergyConfig.ts`, overridable for tests)
- Returns `SynergyResult` (`SynergyBonus | null`)

---

## 4. Dynamics

### Core Behaviors

**Well Rounded Evaluation**

```
evaluateWellRounded(playerPersonality, partyNpcPersonalities, config):

1. Collect all personalities: [playerPersonality, ...partyNpcPersonalities]
2. For each of the 6 trait keys (patience, empathy, cunning, logic, kindness, charisma):
   a. Find the maximum value across all personalities for that trait
   b. If max < config.threshold (0.25): return null (paradigm not satisfied)
3. If all 6 traits pass:
   a. matchQuality = sum(maxTraitValue[t] for each trait t) / (6 × threshold)
   b. Return SynergyBonus { paradigmName: config.name, stat: config.stat, multiplier: config.multiplier, matchQuality }
```

Note: Trait values are stored as integers (e.g., 20 = 20%). The threshold of 0.25 means 25. Normalise accordingly.

**Bond Evaluation**

```
evaluateBond(playerPersonality, partyNpcPersonalities, config):

1. For each NPC personality in partyNpcPersonalities:
   a. Identify the NPC's top 2 traits by value (dominant traits)
   b. npcDominantSum = npc[trait1] + npc[trait2]
   c. playerAlignmentSum = player[trait1] + player[trait2]
   d. alignmentRatio = playerAlignmentSum / npcDominantSum
   e. Record alignmentRatio for this NPC
2. bestRatio = max(all recorded alignmentRatios)
3. If bestRatio < config.threshold (0.80): return null (paradigm not satisfied)
4. Return SynergyBonus { paradigmName: config.name, stat: config.stat, multiplier: config.multiplier, matchQuality: bestRatio }
```

**Highest-Only Selection**

```
calculateSynergy(playerPersonality, partyNpcPersonalities, paradigms):

1. Evaluate each paradigm → collect all non-null SynergyBonus results
2. If results is empty: return null
3. Sort results by matchQuality descending
4. Tiebreak (equal matchQuality): 'well_rounded' > 'bond' (deterministic)
5. Return results[0]
```

**Bonus Application in `initCombatState`**

```
initCombatState(gameState, encounterConfig):

[existing logic: create base combatants from encounterConfig]

const synergyResult = calculateSynergy(
  gameState.playerCharacter.personality,
  getPartyNpcPersonalities(gameState),
  DEFAULT_PARADIGMS
)

if (synergyResult !== null) {
  playerPartyCombatants = playerPartyCombatants.map(combatant => ({
    ...combatant,
    [synergyResult.stat]: Math.round(combatant[synergyResult.stat] * synergyResult.multiplier)
  }))
}
```

### Edge Cases

| Scenario | Handling |
|----------|----------|
| Neither paradigm satisfied | `calculateSynergy()` returns null. Combatant stats unchanged from config values. |
| Both paradigms satisfied | Both evaluated; highest matchQuality wins. If tied, Well Rounded takes priority. |
| Player at default personality (~16.7% each) | No trait reaches 25% for Well Rounded. No trait pair dominant enough for Bond at 80%. No synergy. |
| Party NPC has no clearly dominant traits (e.g., all traits equal) | Bond: top-2 selection picks any two (first by trait key order). Alignment ratio likely ≥80% since player traits near default will match. Consider: if npcDominantSum = 0, skip this NPC (avoid division by zero). |
| Only one NPC in party | Well Rounded may still pass (single NPC can have multiple high traits). Bond: only one NPC is evaluated. |
| Enemy party | Synergy bonuses never apply to enemy combatants. Calculator is only called for player party. |
| Combat started with no GameState NarrativeState | Synergy calculator doesn't require NarrativeState — it reads directly from personality data. Always runs if personalities are present. |

### Rules & Invariants

- **Pure function:** Same `playerPersonality` + `partyNpcPersonalities` + `paradigms` always produces the same `SynergyResult`
- **No mutations:** `calculateSynergy()` does not modify any input objects
- **Stat rounding:** Apply `Math.round()` to the modified stat value (power and speed are integers)
- **Multiplier applies once:** Synergy is computed once at combat init. It does not re-evaluate during combat rounds.
- **Enemy party is never affected:** Only player party combatants receive synergy bonuses
- **Division-by-zero guard:** In Bond evaluation, if `npcDominantSum === 0`, skip that NPC (cannot compute alignment ratio)

---

## 5. Implementation Notes

**Suggested approach:**
1. Define `ParadigmConfig`, `SynergyBonus`, `SynergyResult` types in `src/types/index.ts` (or `src/types/narrative.ts`) as part of Task 1
2. Implement `calculateSynergy()` and evaluator functions in `src/narrative/synergyCalculator.ts` (Task 7) — TDD: write tests for known party compositions first
3. Create `src/fixtures/synergyConfig.ts` exporting `DEFAULT_PARADIGMS: ParadigmConfig[]` for Well Rounded and Bond
4. Extend `initCombatState()` in `src/combat/sync.ts` to call calculator and apply bonus (Task 8)
5. Add `GET /narrative/synergy` endpoint in `src/api/narrative.ts` (Task 9)

**Constraints from existing context:**
- Trait values are integers in range 5–35, summing to 100 — compare as integers, not decimals (threshold 25 not 0.25)
- Personality keys: `patience`, `empathy`, `cunning`, `logic`, `kindness`, `charisma`
- Combatant stats (`power`, `speed`) are integers — apply `Math.round()` after multiplier
- `initCombatState` must remain backward-compatible (no synergy context = no bonus = same result as today)
- ADR-012: all state objects are immutable; use spread operator for combatant modification
- ADR-013: CombatState is independent of GameState; synergy data is read at the sync boundary, not stored on CombatState

**Test scenarios (TDD targets):**
- Well Rounded: player personality [patience:26, empathy:26, kindness:26, cunning:8, logic:7, charisma:7] + Elena + Lars + Kade → bonus fires (player covers patience/empathy/kindness, NPCs cover rest)
- Well Rounded: player personality [patience:15, empathy:15, kindness:15, cunning:25, logic:15, charisma:15] → fails (patience/empathy/kindness below 25)
- Bond/Lars: player [patience:10, empathy:8, cunning:26, logic:24, kindness:7, charisma:25] → Lars alignment = (26+24)/(28+25) = 50/53 = 94.3% → Bond fires
- Bond fail: player at default (~16.7 each) → no NPC alignment ratio ≥80%
- Both satisfied: compute match qualities, confirm higher wins
- No synergy: confirm combatant stats unchanged
- Combat init backward compatibility: no personality data → no bonus, test suite green

---

## 6. References

- Plan tasks: Task 7 (Section 6, plan.md), Task 8 (Section 6, plan.md)
- Related decisions: ADR-012 (Immutable state), ADR-013 (Independent CombatState)
- GROUP action boundary: `docs/project_notes/trunk/design_spec_group_action_type.md` (1.5x multiplier is combat-time, distinct from pre-combat synergy)
- Context research: `docs/project_notes/branches/sprint-3/.design_research/team_synergy_system/context.md`
- NPC personalities: `src/state/npcs.ts` (Elena, Lars, Kade fixed archetypes)
- Combat sync layer: `src/combat/sync.ts` (`initCombatState` integration point)
