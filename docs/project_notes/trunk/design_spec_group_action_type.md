# Design Specification: Group Action Type

**Date:** 2026-02-21
**Status:** Approved
**Plan Reference:** Task 18 (Group Action Type)
**Domain:** Game Design / Code Architecture

## 1. Overview

**Purpose:** GROUP is a coordinated team action where one combatant (the leader) initiates a synchronized assault, pulling all allies into a single unified strike against one target. It is the highest-priority action in the combat pipeline — a powerful combo finisher gated behind full team energy buildup.

**Scope:**
- Included: Leader declaration, ally conscription, priority 0 resolution, Block-only defense suppression, 1.5x damage multiplier, energy gate and consumption, opposing GROUP interaction
- Excluded: Multiple GROUP varieties (deferred; POC implements one), positioning-based tactics, sustained group stances, partial ally participation by choice

**Key Design Decisions:**
- **Priority 0 (highest):** GROUP resolves before all other actions, including DEFEND. Nothing can interfere with GROUP except an opposing GROUP that resolves first.
- **Leader-initiated override:** One combatant declares GROUP; all non-KO'd allies are conscripted (their individual declarations are discarded).
- **Energy gate:** All participants must have full energy segments at declaration time. GROUP consumes all participants' energy on execution.
- **Block-only defense suppression:** Target is forced to Block only — no Dodge, no Parry, no counter chains.
- **DEFEND intercept immunity:** GROUP resolves before DEFEND sets up; interception is impossible.
- **1.5x flat multiplier on total damage:** Applies to the sum of all participants' damage, regardless of how many participants are active at resolution time.
- **Flexible participant count:** GROUP fires with whoever is still non-KO'd at resolution (can reduce to a duo or solo if opposing GROUP KO'd allies first). Multiplier stays.
- **Designed for extensibility:** Synergy bonus type and join rules are POC defaults; future GROUP varieties can define different bonuses and conditions.

---

## 2. Elements

### Core Types

```typescript
// GroupActionDeclaration: what the leader declares in Phase 3
interface GroupActionDeclaration {
  leaderId: string;          // combatant declaring GROUP
  targetId: string;          // single enemy target
}

// GroupResolutionResult: output of GROUP resolution
interface GroupResolutionResult {
  participantIds: string[];  // non-KO'd allies who participated (including leader)
  targetId: string;
  individualDamages: Record<string, number>;  // damage per participant before multiplier
  totalDamage: number;                        // sum of individual damages × 1.5
  defenseResult: BlockDefenseResult;          // target's Block outcome
  finalDamage: number;                        // totalDamage after Block mitigation
}

// BlockDefenseResult: simplified defense (Block only)
interface BlockDefenseResult {
  type: 'block_success' | 'block_failure';
  damageMultiplier: number;  // SMR on success, FMR on failure
}

// GroupActionConfig: extensibility hook for future GROUP varieties
interface GroupActionConfig {
  damageMultiplier: number;  // 1.5 for POC
  energyRequirement: 'full';  // POC: all segments filled
}
```

### Element Inventory

- **GROUP Leader**: The combatant who declares GROUP in Phase 3. Sets the target. Participates in the attack.
- **Pulled Allies**: All non-KO'd allies of the leader. Their Phase 3 declarations are overridden and discarded. They participate in the attack.
- **Energy Gate**: Condition checked at declaration — all allies (including leader) must have full energy segments. Rejects GROUP if not met.
- **Synergy Multiplier**: 1.5x flat multiplier on total group damage (configurable per future GROUP variety).
- **Block-Only Defense**: Target's defense options are suppressed to Block only at resolution time.
- **GroupActionConfig**: Data object defining the POC GROUP variety's rules. Enables future varieties without code changes to the resolver.

### Boundaries & Ownership

- **`combat/groupAction.ts`** owns: eligibility validation, ally conscription, damage aggregation, multiplier application, producing `GroupResolutionResult`
- **`combat/pipeline.ts`** owns: priority ordering (GROUP at priority 0), feeding GROUP into resolution
- **`combat/defense.ts`** owns: executing the Block-only defense roll against GROUP damage (called by groupAction resolver with forced Block type)
- **`combat/declaration.ts`** owns: rejecting invalid GROUP declarations at Phase 3 (energy check)
- **`combat/roundManager.ts`** owns: discarding overridden ally declarations when GROUP is accepted

---

## 3. Connections

### Relationship Map

- **Round Manager (Phase 3)** → **declaration.ts**: Validates GROUP leader's declaration (energy check on all allies)
- **Round Manager (Phase 3)** → **Round Manager**: On valid GROUP, marks allies' declarations as overridden (do not enqueue their individual actions)
- **Round Manager (Phase 4)** → **pipeline.ts**: Priority sort places GROUP at priority 0 (before priority 1 DEFEND)
- **pipeline.ts** → **groupAction.ts**: Calls `resolveGroup(state, declaration, config)` at priority 0
- **groupAction.ts** → **defense.ts**: Calls defense resolution with forced Block type for the GROUP's aggregated damage
- **Behavior tree (Phase 1)** → **groupAction.ts**: Evaluator checks `groupActionsEnabled` AND all team members have full energy before adding GROUP to candidate list
- **Priority sort tie-break**: If both teams declare GROUP, team average speed determines which GROUP resolves first. Slower team's GROUP then resolves with potentially fewer participants.

### Integration Points

- **`combat/declaration.ts`**: Must handle `ActionType = 'GROUP'`. Validation logic: check `combatant.energy === maxEnergyForAscensionLevel` for leader AND all non-KO'd allies. Reject with descriptive error if not met; fallback action is ATTACK on same target.
- **`combat/pipeline.ts` — priority sort**: GROUP is priority 0. Priority table becomes: `{ GROUP: 0, DEFEND: 1, ATTACK: 2, SPECIAL: 2, EVADE: 3 }`. Tie within priority 0 (both teams GROUP): resolved by team average speed (descending). Tie within same priority and team average speed: random factor.
- **`combat/defense.ts`**: Called by GROUP resolver with forced `defenseType: 'block'`. Returns a `BlockDefenseResult` using target's existing SR/SMR/FMR Block rates.
- **`combat/behaviorTree/evaluator.ts`**: Before adding GROUP to candidate list, checks: `config.groupActionsEnabled === true` AND all non-KO'd allies have full energy. If either fails, GROUP is excluded from candidates entirely (not scored).
- **`types/combat.ts`**: No new fields needed on `CombatAction` for GROUP — `{ combatantId: leaderId, type: 'GROUP', targetId: enemyTargetId }` is sufficient.

### File Structure

```
src/combat/
├── groupAction.ts     # resolveGroup() — main GROUP resolution function
```

No subdirectory needed for POC. Single resolver function with `GroupActionConfig` parameter for future extensibility.

---

## 4. Dynamics

### Core Behavior: GROUP Resolution

```
resolveGroup(state: CombatState, declaration: GroupActionDeclaration, config: GroupActionConfig): CombatState

1. Identify participants: leader + all non-KO'd allies in leader's party
2. Calculate individual damage for each participant using existing attack formula
   - Use same formula as ATTACK (base power, rank modifiers, buffs applied)
   - No per-participant synergy bonus — multiplier applies to total
3. Sum individual damages: totalBeforeMultiplier = sum(individualDamages)
4. Apply synergy multiplier: groupDamage = totalBeforeMultiplier × config.damageMultiplier (1.5)
5. Resolve Block defense on target:
   - Force defenseType = 'block' regardless of target's DEFEND declaration
   - Call defense.resolveBlock(target, groupDamage) → BlockDefenseResult
   - Apply damage reduction: finalDamage = groupDamage × defenseResult.damageMultiplier
6. Apply finalDamage to target's stamina (immutable state update)
7. Apply energy consumption: set all participants' energy to 0
8. Record result in RoundResult / roundHistory
9. Return new CombatState
```

### Declaration Validation (Phase 3)

```
validateGroupDeclaration(state: CombatState, declaration: GroupActionDeclaration): ValidationResult

1. Identify leader's party (player or enemy)
2. For leader AND each non-KO'd ally in the party:
   - Check: combatant.energy === maxEnergyForAscensionLevel(combatant.ascensionLevel)
   - If any fail: return { valid: false, fallback: { ...declaration, type: 'ATTACK' } }
3. Check target is valid (non-KO'd, in opposing party)
4. If all pass: return { valid: true }
```

### Opposing GROUP Tie-Breaking

```
When both teams have a GROUP action at priority 0:
1. Compute team average speed for each GROUP's party (non-KO'd members only)
2. Higher team average speed resolves first
3. If still tied: random factor (same mechanism as within-priority tie-breaking)
4. Slower GROUP resolves second — with whatever participants remain non-KO'd
   - If opposing GROUP KO'd 1 ally: duo GROUP fires (2 participants × 1.5x)
   - If opposing GROUP KO'd 2 allies: solo GROUP fires (1 participant × 1.5x)
   - If opposing GROUP KO'd all allies: leader alone fires (1 participant × 1.5x, still valid)
```

### Edge Cases

| Scenario | Handling |
|----------|----------|
| Not all allies have full energy at declaration | GROUP rejected; leader's action becomes ATTACK on same target; allies keep original declarations |
| Target is already KO'd when GROUP resolves | Should not occur (declaration validates target); if it somehow does, GROUP no-ops (produces no damage) |
| Opposing GROUP goes first, KO's one ally | GROUP fires with remaining non-KO'd participants; multiplier unchanged |
| Opposing GROUP goes first, KO's all allies | Leader fires solo GROUP (1 participant × 1.5x); not an error |
| Both teams GROUP same target | First GROUP damages target; second GROUP may find target has reduced stamina or is KO'd — if KO'd, second GROUP no-ops |
| AI declares GROUP but `groupActionsEnabled = false` | Evaluator never produces GROUP candidates; config gate prevents this scenario |
| SPECIAL energy use — can energy be spent between declaration and GROUP? | No. GROUP resolves at priority 0 — no other actions resolve before it. Energy state at declaration equals energy state at resolution (assuming no opposing GROUP). |

### Rules & Invariants

- **Priority 0 is absolute:** GROUP always resolves before any non-GROUP action. No other mechanism can insert actions before GROUP.
- **Multiplier always applies:** 1.5x applies regardless of participant count (1, 2, or 3). The multiplier rewards whatever coordination remains.
- **Block-only is unconditional:** Target cannot select Dodge or Parry against GROUP regardless of any buff, stance, or elemental path constraint.
- **No counter chains from GROUP:** Block cannot trigger counters. GROUP's Block-only defense suppression inherently excludes Parry, which is the counter trigger.
- **Energy consumption is atomic:** All participants' energy is reset to 0 on GROUP execution, regardless of whether GROUP damages the target or no-ops.
- **Immutability:** `resolveGroup()` is a pure function — returns new `CombatState`, never mutates input.
- **Determinism:** Same `CombatState` + `GroupActionDeclaration` + `GroupActionConfig` always produces the same result.

---

## 5. Implementation Notes

**Suggested approach:**
1. Extend `combat/declaration.ts` to handle `ActionType = 'GROUP'` with energy validation
2. Update priority table in `combat/pipeline.ts`: `GROUP: 0` (shift DEFEND to 1, ATTACK/SPECIAL to 2, EVADE to 3)
3. Update Round Manager Phase 3 to mark ally declarations as overridden when GROUP is accepted
4. Implement `resolveGroup()` in `combat/groupAction.ts` — calls existing attack formula per participant, then sums + multiplies, then calls defense.resolveBlock()
5. Update behavior tree evaluator to check energy gate before adding GROUP to candidates

**Constraints from existing context:**
- `resolveGroup()` must produce a new `CombatState` (immutable); no mutations (ADR-012)
- Individual attack damage calculation must use the same formula as `pipeline.ts` ATTACK resolution — do not duplicate formula logic; extract or import
- `GroupActionConfig` parameter enables future GROUP varieties; hardcode `{ damageMultiplier: 1.5, energyRequirement: 'full' }` as the POC default export
- Behavior tree integration: once GROUP is implemented, set `groupActionsEnabled: true` in evaluator config and add GROUP-specific factor scoring (energy readiness across team as a factor input)

**Priority table change (from plan):**
```
Before: { DEFEND: 1, GROUP: 2, ATTACK: 3, SPECIAL: 3, EVADE: 4 }
After:  { GROUP: 0, DEFEND: 1, ATTACK: 2, SPECIAL: 2, EVADE: 3 }
```
Task 15 (pipeline.ts) implements the original table. Task 18 updates it. No other tasks are affected — priority numbers are internal to the sort function.

**Verification considerations:**
- Unit test: GROUP declaration rejected when any ally lacks full energy
- Unit test: GROUP fires with 2 participants when 1 ally is KO'd (e.g., mock state with KO'd ally)
- Unit test: total damage = (sum of individual damages) × 1.5
- Unit test: target's Block reduces GROUP damage using existing Block formula
- Unit test: participants' energy is 0 after GROUP execution
- Unit test: priority sort places GROUP at index 0 before DEFEND, ATTACK, EVADE
- Integration test: full round with both teams declaring GROUP — faster team resolves first, slower team fires with remaining participants

---

## 6. References

- Plan task: Task 18 (Section 6, plan.md); priority system in Task 15
- Related decisions: ADR-012 (Immutable pipeline), ADR-013 (Independent CombatState), ADR-015 (TDD formula porting)
- Behavior Tree AI Spec: `docs/project_notes/trunk/design_spec_behavior_tree_ai_system.md` (GROUP factor scoring, `groupActionsEnabled` config)
- Context research: `docs/project_notes/trunk/.design_research/group_action_type/context.md`
- Combat system: `docs/Reference Documents/GM_Combat_Tracker_Documentation.md`
