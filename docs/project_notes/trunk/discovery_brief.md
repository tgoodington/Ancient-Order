# Discovery Brief: Ancient Order Sprint 1 + Sprint 2 Rebuild

## Problem Statement
The Ancient Order backend prototype needs a complete rebuild from an empty codebase. Sprint 1 (personality-driven narrative systems) and Sprint 2 (combat engine) must be implemented with fresh technical decisions while preserving the established game design specifications. A prior prototype existed but has been archived — the game design is validated, but all technical and architectural choices are open for re-evaluation with new tooling.

## Success Criteria
- Full Sprint 1 backend operational: personality system (6 traits, 5-35% range, sum=100%), dialogue engine with personality gates, immutable state management, JSON file-based persistence (10 save slots), REST API for all game operations
- Full Sprint 2 combat engine operational: 5-phase round pipeline (AI Decision → Visual Info → PC Declaration → Action Resolution → Per-Attack Resolution), all combat formulas ported from GM Combat Tracker Excel with exact accuracy
- 3v3 combat encounter resolves completely through all phases with correct formula output matching Excel source of truth
- Behavior tree AI produces distinct NPC combat decisions by archetype
- 6 elemental paths with buff/debuff mechanics, energy/ascension system with 4 levels
- Defense resolution (Block/Dodge/Parry) with SR/SMR/FMR rates, counter chain system
- Working backend sufficient for investor/publisher pitch demo
- Test coverage for all pure function logic

## Scope

**In scope:**
- Sprint 1: TypeScript interfaces, game state management, personality system, dialogue engine, NPC system (3 test NPCs), save/load persistence, REST API endpoints
- Sprint 2: Combat type system, dominance checks (Rank KO, Blindside, Crushing Blow), defense resolution, damage calculation, stamina tracking, counter chains, elemental path system, energy/ascension system, action priority and resolution pipeline, player declaration validation, round manager orchestrator, behavior tree AI, Group action type, combat API endpoints, GameState-CombatState sync
- Unit and integration tests for all systems
- Test-driven porting of all combat formulas from Excel

**Out of scope:**
- React frontend (Sprints 5-7)
- Narrative state machine / Act 1 scene system (Sprint 3)
- Advanced persistence and API hardening (Sprint 4)
- Deployment to Vercel (Sprints 8-9)
- Game balance tuning (formulas are ported as-is from Excel)

## Constraints
- **Game Design (fixed):** Personality traits: 6 traits, 5-35% range, sum always = 100%. NPCs have fixed archetypes (only player personality changes). No dead ends in dialogue (all gates have ungated fallback). Combat formulas must exactly replicate Excel source of truth — no modifications or "improvements" during porting (reference: GM_Combat_Tracker_Documentation.md).
- **Technical (open):** All framework, architecture, testing, state management, and tooling decisions are open for evaluation during planning. No prior technical decisions are binding.
- **Reference Documents:** Game design source of truth lives in `docs/Reference Documents/` — Sprint1 specs (4 files), GM Combat Tracker Documentation, GM Combat Tracker Excel (17 sheets), Claude Code Handoff Document.

## Key Assumptions

| Assumption | Confidence | Basis |
|-----------|-----------|-------|
| Game design specs in reference docs are complete and accurate | High | Used for prior prototype, validated by archived implementation |
| Excel combat formulas are internally consistent and balanced | High | 17-sheet model developed and tested independently |
| Sprint 1 and Sprint 2 can be built as a single continuous effort | Medium | Prior plan treated them as one scope; may need sequencing decisions in planning |
| 3 test NPCs (Elena, Lars, Kade) are sufficient for demo | High | Established in Sprint 1 spec, covers 3 archetypes and 2 factions |
| Archived Sprint 1 prototype code is available as reference (not to copy) | High | Archived in docs/project_notes/archive/src-sprint1-prototype/ |

## Open Questions for Planning
- **Technical stack decisions:** Framework choice, architecture patterns, state management approach, test framework — all need evaluation against current tooling
- **Behavior Tree AI system:** No detailed spec exists. Node types, evaluation model, archetype profiles, and integration pattern need design exploration before implementation
- **Group Action Type:** Mechanics are undefined in all documentation. Targeting rules, synergy effects, and resolution within priority system need design exploration
- **Combat encounter configuration:** How are enemy parties defined for the demo? Static JSON templates or something more dynamic?
- **Build sequencing:** What's the optimal order for building Sprint 1 + Sprint 2 systems given they share type definitions and state management?
