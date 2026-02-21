# Planning Brief: Ancient Order Sprint 1 + Sprint 2 Rebuild

## Discovery Summary

The Ancient Order backend prototype is being completely rebuilt from an empty codebase. The game design is well-validated (from prior prototype and extensive reference documentation), but all technical and architectural decisions are open for fresh evaluation with new tooling. Sprint 1 covers core narrative systems (personality, dialogue, state, persistence, API), and Sprint 2 implements the full combat engine with all formulas ported from Excel. The project is scoped for a single continuous effort, with success measured by a 3v3 combat encounter resolving correctly through all phases with exact formula matching and a working demo for investor/publisher pitch.

## Problem Statement

Complete rebuild of personality-driven narrative backend and turn-based combat engine with fresh technical architecture, preserving game design specifications while enabling new tooling decisions.

## Goals & Success Criteria

- **Sprint 1 complete:** Personality system (6 traits, 5-35%, sum=100%), dialogue engine with gates, immutable state, JSON persistence (10 slots), full REST API
- **Sprint 2 complete:** 5-phase combat pipeline, all Excel formulas ported with exact accuracy, behavior tree AI, 6 elemental paths, energy/ascension system, counter chains, full combat API
- **3v3 encounter resolves correctly** through all phases with formula output matching Excel source
- **Test coverage:** All pure function logic covered (unit + integration)
- **Working demo** suitable for investor/publisher pitch
- **Behavior tree AI** and **Group action type** fully designed before implementation

## Key Constraints

### Game Design (Fixed)
- Personality: 6 traits, 5-35% range per trait, sum always = 100%
- NPCs: Fixed archetypes (only player personality changes)
- Dialogue: No dead ends (all gates have ungated fallback)
- Combat: All formulas exact replicas from `GM Combat Tracker.xlsx` (ADR-007, ADR-015)

### Technical (Open for Re-evaluation)
- Framework choice (was Express.js)
- Architecture patterns (immutability approach, module boundaries, etc.)
- State management (was spread-operator immutability)
- Test framework (was Jest)
- Persistence: JSON files confirmed (ADR-005)
- API style: REST confirmed (ADR-006)

### Reference Scope
- **Game design source:** `docs/Reference Documents/` (Sprint1 specs + Combat docs + Excel)
- **Implementation reference:** Archived prototype at `docs/project_notes/archive/` (patterns, not code to copy)

## Architectural Context

### Inherited Decisions (Persisting)
- **ADR-005:** JSON file-based persistence (10 save slots in `saves/` directory)
- **ADR-006:** Express.js REST API (may be re-evaluated, but REST style confirmed)
- **ADR-007:** Combat formulas exact replicas from Excel (non-negotiable)
- **ADR-008:** Information asymmetry in combat (AI hides until Phase 2)
- **ADR-009:** Reaction vs Action path philosophy (6 elemental paths)
- **ADR-012:** Pipeline combat architecture (5 phases, each a pure function)
- **ADR-013:** Independent CombatState with round-boundary sync
- **ADR-014:** Behavior tree AI for NPC decisions (requires design before execution)
- **ADR-015:** Test-driven formula porting from Excel

### New Technical Decisions Needed
- Framework and runtime approach (preserving Node.js as runtime, but framework is TBD)
- Immutability pattern and state management approach
- Test framework selection
- Module organization and dependency patterns
- Type system approach (TypeScript confirmed, but structure TBD)

## Assumptions & Risks

| Assumption | Confidence | Basis | Risk |
|-----------|-----------|-------|------|
| Game design specs are complete and accurate | High | Used in prior prototype, validated by archived implementation | Low — specs are fixed |
| Excel formulas are internally consistent and balanced | High | 17-sheet model independently developed and tested | Low — formulas are source of truth |
| Sprint 1 + Sprint 2 can be built as continuous effort | Medium | Prior plan treated them as one scope; unclear if build sequencing is optimal | Medium — may need staging if too large |
| 3 test NPCs sufficient for demo | High | Established in Sprint 1 spec; covers 3 archetypes and 2 factions | Low — NPCs are fixed |
| New tooling choices will simplify implementation vs archived prototype | Medium | User stated old tech decisions are no longer relevant | Medium — planning must validate this |
| Behavior tree AI can be designed and implemented in one sprint | Low | No existing spec; complex subsystem with multiple unknowns | High — flagged for design exploration before execution |
| Group action mechanics can be designed and implemented in one sprint | Low | Completely undefined in all documentation | High — flagged for design exploration before execution |

## References

- **Discovery:** `docs/project_notes/trunk/discovery_brief.md`
- **Game Design Specs:** `docs/Reference Documents/Sprint1_*.md` (4 files + Handoff Document)
- **Combat Specs:** `docs/Reference Documents/GM_Combat_Tracker_Documentation.md` + Excel
- **Inherited ADRs:** ADR-005, ADR-006, ADR-007, ADR-008, ADR-009, ADR-012, ADR-013, ADR-014, ADR-015 in `docs/project_notes/decisions.md`
- **Archived Patterns:** `docs/project_notes/archive/src-sprint1-prototype/` (reference implementation for patterns)

## Key Open Questions for Planning

1. **Technical stack:** What framework, architecture, and test approach best fit the current tooling landscape?
2. **Build sequencing:** Is continuous Sprint 1+2 build optimal, or should tasks be staged?
3. **Behavior Tree AI:** What node types, evaluation model, and integration pattern?
4. **Group Action Type:** What are the mechanics, targeting rules, and synergy effects?
5. **Combat encounter configuration:** Static JSON templates or something more dynamic?
