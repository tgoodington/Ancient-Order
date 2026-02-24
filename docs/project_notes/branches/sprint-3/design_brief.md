# Design Brief: Team Synergy System

**Current Item:** Team Synergy System (1 of 2)
**Date:** 2026-02-24
**Status:** In Progress

---

## Overview

Sprint 3 requires a team synergy bonus system that calculates stat modifiers based on party member personality interactions. The system applies pre-combat bonuses to the player party's combatants when entering combat. The formula, calculation model (pair-based vs team composition), stat bonus types, and player transparency all need game design exploration.

---

## Plan Context

Sprint 3 builds the narrative progression layer for Act 1, connecting the personality system (Sprint 1) and combat engine (Sprint 2) through a cohesive story experience. The team synergy system is one of two design surfaces that require exploration before execution can proceed.

**Key constraint:** Team synergy is distinct from the GROUP action type's 1.5x damage multiplier (a combat-time resolution mechanic in Sprint 2). The synergy system is a pre-combat party composition bonus applied through the existing `initCombatState` function in `combat/sync.ts`.

---

## Task Details

**Tasks:** 7, 8
**Task 7:** Team Synergy Calculator (implementation) — builds the calculation engine
**Task 8:** Combat Synergy Integration (implementation) — wires bonuses into `initCombatState`

**Description (from plan):**
Implement the team synergy bonus calculation system. Given a party composition, compute stat bonuses based on personality interactions between party members. The formula, pair vs team model, bonus types, and integration pattern must come from this design spec.

**Acceptance Criteria (baseline from plan):**
1. Synergy bonuses computed correctly for the 3-member party
2. Bonuses are stat-typed (e.g., ATK +15%, DEF +10%) and expressible as percentage modifiers
3. Calculation is a pure function: same party composition → same bonuses
4. Bonuses are data-driven (configuration, not hardcoded per-character if-statements)
5. Unit tests verify correct bonus computation for known party compositions

---

## Design Rationale

The discovery brief identified synergy as a medium-confidence item needing design:
- "Synergy bonus math is simple (percentage multipliers)" — medium confidence, needs specification
- "Player party is always 3 characters" — high confidence, fixed by Sprint 2

Three viable approaches were considered during planning and flagged for design:
1. **Pair-based lookup table** (recommended as starting point) — each pair (Elena+Lars, Elena+Kade, Lars+Kade) has a static bonus entry. 3-member party = 3 pairs. Bonuses are additive.
2. **Trait-distance formula** — calculate similarity/complementarity between party members' personalities, convert to bonus percentage. Responds to player personality changes but harder to balance.
3. **Full team composition** — evaluate all 3 members together. More expressive but significantly more complex.

**Why it matters:** The choice affects:
- **Balancing complexity:** Pair-based is simplest and data-driven. Formula-based is dynamic but requires validation. Team composition adds significant complexity.
- **Player transparency:** Players should understand why they get bonuses. Static pair bonuses are transparent; formula-based requires explanation; team composition is opaque.
- **Content authoring:** Static pairs scale linearly (6 possible pairs for 3 NPCs). Formula scales to any character addition. Team composition compounds with each new character.
- **Integration with combat:** Pre-combat bonus application is straightforward for all models — the difference is in *how* bonuses are calculated, not how they're applied.

---

## Design Questions

1. **Calculation Model:** Pair-based lookup table, trait-distance formula, or full team composition? Recommend pair-based for the POC (simplest, transparent, and data-driven).

2. **Bonus Types:** What stat types should synergy modify? Options:
   - ATK only (simplest)
   - ATK + DEF (common in RPGs)
   - Full stat suite (ATK, DEF, SPD, accuracy, etc.)
   - Recommend starting with ATK + DEF (matches typical party synergy feel)

3. **Bonus Magnitude:** What's a reasonable percentage? Common ranges in RPGs:
   - Conservative: ±5-10%
   - Moderate: ±10-20%
   - Aggressive: ±20-30%
   - Recommend moderate range for investor demo (meaningful but not dominant)

4. **Synergy Activation:** Should synergies activate for:
   - Any 3-character party composition (current assumption)
   - Only when matching personalities (e.g., two characters with high Charisma synergize better)
   - Only with specific NPC combinations (hardcoded synergies for Elena+Kade, etc.)
   - Recommend first: all pairs in party get checked; second: baseline bonuses can be NPC-specific

5. **Negative Synergies:** Should incompatible personality combinations have *negative* synergy penalties? This adds depth but complexity.
   - Recommend starting with positive-only synergies (simpler, more forgiving for investor demo)

6. **Visibility to Player:** Should the player see synergy bonuses in:
   - Party setup screens?
   - Combat stat displays?
   - Narrative feedback ("Elena and Kade's natural synergy grants +15% ATK")?
   - Recommend: Combat stat displays + narrative feedback for demo effect

---

## Constraints

- **Party is always 3 characters** (Sprint 2 enforces this)
- **3 fixed NPCs** (Elena, Lars, Kade) with fixed personalities
- **Pure function:** Calculation must be deterministic (same party → same bonuses)
- **JSON-serializable:** Synergy bonus data must serialize to JSON fixtures
- **Immutable:** Synergy calculation doesn't mutate state (produces new bonus values)
- **Integration point:** Bonuses apply via `initCombatState` in `combat/sync.ts`, not by modifying `CombatState` directly (ADR-013)
- **GROUP action boundary:** Synergy bonus is distinct from GROUP action's 1.5x multiplier (combat-time mechanic, this is pre-combat)

---

## Design Context from Plan

**From Section 2.5 (Parent Context):**
- Sprint 1+2 use Fastify + Vitest + TypeScript, immutable state, JSON persistence
- State updaters follow pattern: `(state: GameState, ...) => GameState`
- Roll injection pattern for testability (optional `rollFn` parameter)
- NPC relationship updaters already exist: `updateNPCAffection`, `updateNPCTrust`, `updateNPCRelationship`

**From Section 10 (Engineering Context):**
- Synergy calculator location is TBD by design: `narrative/` (conceptual layer) or `combat/` (consumed layer)
- Should follow existing TDD pattern: test-first with known input/output pairs once formula is specified
- Decision likely affects Task 8 (Combat Synergy Integration) file placement

---

## Design Queue

| Item | Status | Remaining |
|------|--------|-----------|
| Team Synergy System | **In Progress** | This design session |
| Act 1 Narrative Design | Pending | After synergy completes |

---

## References

- **Plan:** `docs/project_notes/branches/sprint-3/plan.md` (Section 6: Tasks 7-8, Section 6.5: Design Recommendations, Section 10: Engineering Context)
- **Discovery:** `docs/project_notes/branches/sprint-3/discovery_brief.md` (Key Assumptions, Open Questions)
- **Parent decisions:** `docs/project_notes/trunk/plan.md` (Section 3: Technology Decisions)
- **Parent design specs:** `docs/project_notes/trunk/design_spec_group_action_type.md` (reference for GROUP action's 1.5x synergy, boundary with this system)
