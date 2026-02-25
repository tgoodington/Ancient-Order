# Design Brief: Act 1 Narrative Design

**Current Item:** Act 1 Narrative Design (2 of 2)
**Date:** 2026-02-24
**Status:** In Progress

---

## Overview

Sprint 3 requires authoring 2-3 functional Act 1 starter scenes that demonstrate the narrative engine. Scene content, branching strategy, choice gating, and consequence flag design all need creative collaboration before implementation begins. This design session establishes the narrative arc, branching model, scene scope, and tone to guide scene content authoring.

---

## Plan Context

Act 1 is designed as an opening sequence for the investor demo. The full Act 1 blueprint exists (6 scenes from The Championship Fight through First Gym Town), but the POC scope is 2-3 starter scenes. The design session determines:
1. **Branching strategy** — linear spine + variants vs true branching vs minimal choice
2. **Scene scope** — which 2-3 scenes from the full blueprint, or original starter content
3. **Choice design** — how personality gates, narrative weight, and consequences balance
4. **Flag system** — what prerequisite conditions and state tracking feels meaningful

---

## Task Details

**Task 10:** Act 1 Starter Scenes (implementation) — author 2-3 scenes as JSON scene graph data

**Description (from plan):**
Author 2-3 functional Act 1 scenes as JSON scene graph data. Scenes must include: narrative text, player choices with personality gates, consequence flags, and prerequisite conditions demonstrating cross-scene flag propagation. Scene content, branching model, and narrative arc must come from the design spec (which covers both branching strategy and scene content).

**Acceptance Criteria (baseline from plan):**
1. 2-3 scenes form a playable narrative sequence with at least one branching point
2. At least one choice is personality-gated with an ungated fallback
3. At least one scene has a prerequisite checking a flag set by a prior scene's choice
4. All scenes pass dead-end validation (no unreachable dead ends)
5. Scene data is valid JSON loadable by the scene graph engine

---

## Design Rationale

The planning phase flagged narrative authoring as medium-confidence on branching strategy:
- "Linear story with minimal branching" — simplest, fits demo pacing
- "Linear spine + variants" — more replay value, player personality shapes dialogue
- "True branching" — maximal choice impact, but exponential content volume

The planning phase also noted: "Scene content authoring is the highest effort risk — real narrative text is slower to produce than code. The 2-3 scene target is intentionally conservative."

**Design questions:**
1. **Branching model:** Which approach best serves the investor demo in scope? (Linear spine is recommended starting point)
2. **Scene selection:** Which 2-3 scenes from the Act 1 blueprint? Or original simplified starter? (Recommendation: start with The Championship Fight or Town Exploration simplified)
3. **Tone & pacing:** Narrative voice, scene length, choice consequence weight?
4. **Personality gate placement:** Which scenes/choices demonstrate personality gates? Which traits matter?
5. **Prerequisite flags:** What cross-scene consequences feel meaningful? Simple (visit flag) or complex?

---

## Context from Team Synergy Design

**Recently completed:** Team Synergy System (Paradigm-Based) — Well Rounded and Bond paradigms with 25% and 80% thresholds. This affects how personality choices in dialogue ripple into combat. Consider how Act 1 narrative progression allows players to explore personality space for synergy achievement.

---

## Design Constraints

- **No dead ends:** Every reachable scene has at least one choice with no prerequisites or met prerequisites (ADR-004)
- **Party always 3 members:** Elena, Lars, Kade (fixed NPCs) — no party swapping in Act 1
- **2-3 scenes maximum:** Scope constraint; demonstrate engine, not full Act 1
- **Personality trait bounds:** 5-35%, sum = 100% — dialogue choices adjust traits via existing personality system
- **Static JSON fixtures:** Scenes are data, not generated; no procedural content

---

## Design Context from Plan

**From Section 2.5 (Parent Context):**
- Sprint 1+2 personality system and dialogue engine are foundation; scenes build on top
- Dialogue gates use trait thresholds (e.g., "cunning >= 20%") and operators (gte, lte, eq)
- Dialogue choices trigger personality adjustments via existing `applyPersonalityAdjustment` function

**From Section 10 (Planning Context):**
- Scene JSON location: TBD by design (`src/fixtures/scenes/` or `src/narrative/scenes/`)
- Scene prerequisite conditions: trait gates, choice flags, or both
- Choice consequence effects: personality adjustments, NPC state changes, flag setting
- Engineering must understand narrative intent before implementing scene engine

---

## Design Queue

| Item | Status | Remaining |
|------|--------|-----------|
| Team Synergy System | **Completed** | Spec ready for execution |
| Act 1 Narrative Design | **In Progress** | This design session |

---

## References

- **Plan:** `docs/project_notes/branches/sprint-3/plan.md` (Section 6: Tasks 2-10, Task 10 details, Design Recommendations)
- **Discovery:** `docs/project_notes/branches/sprint-3/discovery_brief.md` (Act 1 scope, narrative constraints)
- **Team Synergy Spec:** `docs/project_notes/branches/sprint-3/design_spec_team_synergy_system.md` (Well Rounded & Bond paradigms inform personality space design)
- **Parent narrative foundation:** `docs/Reference Documents/Sprint1_Technical_Reference.md` (Dialogue gates, personality system)
- **Act 1 blueprint:** `docs/project_notes/key_facts.md` (Act 1 Narrative Structure: 6 scenes full blueprint)
