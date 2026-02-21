# Design Brief: Group Action Type

**Current Item:** Group Action Type
**Status:** In Progress (2 of 2)
**Plan Tasks:** Task 18 (Combat Engine)
**Date:** 2026-02-21

---

## Summary

The Ancient Order backend plan requires a GROUP action type for team-based combat coordination. GROUP is a 5th action type (alongside ATTACK, DEFEND, EVADE, SPECIAL) with priority level 2 (resolved after DEFEND, before ATTACK). The mechanics—targeting rules, synergy effects, interaction with the defense/counter systems—are completely undefined in all reference documentation and must be invented through design collaboration.

---

## Plan Context

Sprint 2 implements a 5-phase turn-based combat pipeline. Phase 4 (Action Resolution) sorts all declared actions by priority:
- Priority 1: DEFEND
- Priority 2: **GROUP** (to be designed)
- Priority 3: ATTACK / SPECIAL
- Priority 4: EVADE

GROUP actions resolve at priority 2, meaning they execute before individual attacks. The design must specify how GROUP coordinates multiple allies, how synergy bonuses are applied, what happens when team members declare conflicting GROUP actions, and how GROUP interacts with enemy DEFEND/COUNTER mechanics.

**Current game context:**
- 3v3 combat: 3 player-controlled allies, 3 enemy NPCs
- 5 action types currently defined: ATTACK, DEFEND, EVADE, SPECIAL, GROUP
- 4 priority levels established: 1 (DEFEND), 2 (GROUP), 3 (ATTACK/SPECIAL), 4 (EVADE)
- Defense system: Block, Dodge, Parry with SR/SMR/FMR rates
- Counter chain system: successful Parry triggers counter, chain continues until Parry fails or stamina depletes
- Behavior tree AI just designed; GROUP actions excluded until now via config flag

**Integration context:**
- GROUP actions declared in Phase 3 alongside individual actions
- Round Manager Phase 4 sorts all actions including GROUP by priority
- GROUP resolution happens in the per-attack pipeline (Phase 5 or sub-phase thereof)
- GROUP stub exists in Task 15 (returns no-op); GROUP spec will replace it in Task 18

---

## Task Details

From `plan.md`, Task 18 (Group Action Type):

- **Component**: `combat/groupAction.ts`
- **Description**: Implement the GROUP action type mechanics. Targeting rules, synergy effects, resolution within the priority system, and interaction with defense/counter systems must come from the design spec produced by `/intuition-design`. This task implements that spec and replaces the GROUP stub from Task 15.
- **Acceptance Criteria** *(to be refined after design — minimum requirements)*:
  1. GROUP actions resolve at priority 2 per the established priority system
  2. Targeting rules are enforced (invalid GROUP configurations rejected)
  3. Synergy effects apply correctly as defined in the design spec
  4. GROUP resolution integrates with the existing per-attack pipeline without breaking ATTACK/DEFEND/EVADE/SPECIAL resolution
- **Dependencies**: Task 10 (Combat Type System) + this design spec
- **Downstream**: Task 19 (Combat Integration) depends on this being implemented

---

## Design Rationale

**Why this item is design-required:**

1. **Completely undefined** — No reference implementation exists, even in the archived prototype. Combat was never implemented. GROUP is mentioned in the Excel as "inspiration from Skies of Arcadia crew specials" but has no mechanics documented.
2. **Architectural impact** — GROUP resolves at priority 2 (before individual attacks). The targeting model, synergy resolution, and interaction with defense chains will affect how the per-attack pipeline is structured.
3. **Design decisions with lasting consequences** — Choices about team coordination, synergy triggers, and conflict resolution affect:
   - How many GROUP actions can execute per round (all three allies coordinating, or only coordinated pairs?)
   - Whether GROUP synergy adds flat bonuses or multipliers
   - How GROUP interacts with enemy defense (does GROUP bypass individual Parry counters? can GROUP be Defended against as a unit?)
4. **Scoping boundary** — GROUP is distinct from individual attacks. Design must clearly define what is in-scope (mechanics) and out-of-scope (e.g., positioning-based tactics, sustained group stance).

---

## Design Constraints

**Fixed constraints (from game design & architecture):**
- GROUP actions must resolve at priority 2 (after DEFEND, before ATTACK/SPECIAL)
- Must integrate with the 5-phase combat pipeline (Phase 3 declaration, Phase 4 priority sort, Phase 5 resolution)
- Behavior tree AI must be able to decide to take GROUP actions (evaluator currently has GROUP disabled via config flag)
- Must work within 3v3 team structure (not all group sizes; design is for this specific constraint)
- Defense resolution system (Block/Dodge/Parry with SR/SMR/FMR rates) already exists; GROUP cannot bypass or fundamentally alter it

**Integration constraints:**
- GROUP actions are declared in Phase 3 alongside individual actions (no special phase needed)
- Resolution must produce the same `CombatAction` outputs for per-attack pipeline
- Must not mutate state or have side effects (consistent with pipeline architecture)
- Must be deterministic (same state → same behavior)
- Error handling for invalid GROUP configurations must be robust

**Architectural constraints (from plan Section 4, 5):**
- GROUP mechanics live in `combat/groupAction.ts` (or a directory if subcomponents needed)
- Type definitions are in `types/combat.ts`
- Integration point: called from `combat/roundManager.ts` Phase 5 (per-attack resolution)
- Tests: GROUP resolution must be unit-testable for each action combination and synergy scenario

---

## Design Questions to Resolve

**1. Targeting & Composition:**
- Can any number of allies participate in a GROUP action, or only coordinated pairs/trios?
- Does a GROUP action require all participants to declare GROUP, or can one ally trigger GROUP coordination?
- What is the targeting model? Do all GROUP participants attack the same target, or can they coordinate different targets?

**2. Synergy Mechanics:**
- What bonuses or effects trigger from GROUP coordination? Examples: +10% damage, guaranteed Critical Hit, forced Parry-Break?
- How are synergy bonuses applied? Flat value, multiplier, or something context-dependent (e.g., scales with matched elemental paths)?
- Do synergy effects apply to the GROUP action itself, or to subsequent individual attacks that round?

**3. Conflict Resolution:**
- What happens if two allies declare GROUP but don't coordinate on a target? (e.g., Alice declares GROUP→Attack Enemy1, Bob declares GROUP→Attack Enemy2)
- Can GROUP actions fail or be interrupted (like Parry chains can terminate)?
- Does GROUP have stamina cost? Energy cost? Special requirements?

**4. Defense & Counter Interaction:**
- Can enemies defend against GROUP actions? (e.g., can an enemy DEFEND to intercept one GROUP participant, or must all GROUP participants be intercepted together?)
- Do successful GROUP Parries trigger counter chains? If so, does the counter apply to all GROUP participants or just one?
- Does GROUP bypass certain defenses (e.g., ignore Block) or interact with them specially?

**5. Rank/Power Scaling:**
- Do synergy bonuses scale with participant rank or power levels?
- Is there a "coordination penalty" if participants have very different ranks?

**6. Demo Scope:**
- Is GROUP required for the pitch demo, or is a stub acceptable?
- If required, are all 3 allies coordinating expected, or can the demo showcase pairs?

---

## Prior Design Context (from Behavior Tree AI)

**Relevant Behavior Tree Decisions:**
- The behavior tree AI (just completed in Task 17) uses utility scoring with combined (action, target) evaluation
- GROUP actions are currently excluded from AI candidate evaluation via `groupActionsEnabled: false` config flag
- Once GROUP is designed and implemented, engineering will set `groupActionsEnabled = true` and add GROUP-specific factor contributions to the behavior tree

**Interaction with Behavior Tree:**
- The behavior tree must be able to decide GROUP actions based on ally coordination potential
- GROUP factors in the behavior tree (e.g., "TeamBonus" factor scoring GROUP higher when party is in sync) will inform AI GROUP participation

---

## Design Queue

| Item | Status | Tasks | Design Spec | Notes |
|------|--------|-------|------------|-------|
| **Behavior Tree AI System** | Completed ✓ | Task 17 | `design_spec_behavior_tree_ai_system.md` | Utility scoring, 7 factors, perception layer, rank scaling, path-based tie-breaking. GROUP currently disabled (config flag). |
| **Group Action Type** | In Progress | Task 18 | (this brief) | Designing now. Targeting, synergy, conflict resolution, defense interaction need invention. |

---

## References

- **Plan**: `docs/project_notes/trunk/plan.md` (Section 6, Task 18; Section 4: Component Architecture)
- **Combat System Spec**: `docs/Reference Documents/GM_Combat_Tracker_Documentation.md` (overview, priority system, defense mechanics)
- **Combat Type System**: `docs/project_notes/trunk/plan.md` (Section 5: Interface Contracts, CombatState, CombatAction)
- **Behavior Tree AI Spec**: `docs/project_notes/trunk/design_spec_behavior_tree_ai_system.md` (for context on AI integration)
- **Round Manager Integration**: `docs/project_notes/trunk/plan.md` (Section 4: Component Architecture, Phase 4 priority sort, Phase 5 per-attack resolution)
- **Prior Reference**: Skies of Arcadia crew special mechanics (mentioned in documentation as inspiration, but no specific mechanics documented)

---

## Next Steps

Run `/intuition-design` to begin collaborative design exploration of the GROUP action type. The design session will:
1. Explore targeting and composition models (free-form coordination, pair-based, or rigid structure)
2. Define synergy mechanics and bonus triggers
3. Establish conflict resolution rules
4. Map GROUP interaction with defense/counter systems
5. Produce `design_spec_group_action_type.md` with implementation blueprint
