# Discovery Brief: Ancient Order Sprint 3 - Narrative & State Machine

## Problem Statement

With Sprint 1 (personality system) and Sprint 2 (combat engine) complete and tested, the backend requires Act 1 narrative progression, choice consequence tracking, and team dynamics calculation. Sprint 3 builds the narrative state machine that drives the game's story flow and player decision impact, preparing the system for frontend integration in Sprints 5-7.

## Success Criteria

- Act 1 scene progression system fully operational: scenes, transitions, state management
- Choice-tracking and consequence system implemented: player choices affect future scenes and NPC states
- Team synergy bonus calculations working: combinations of party members grant bonuses based on personality interactions
- Scene/choice state persists to save system (integrating with Sprint 1 persistence layer)
- All narrative logic REST API endpoints functional
- Integration with existing personality system for trait-based scene gates and NPC dialogue variations

## Scope

**In scope:**
- Act 1 scene architecture: scene definitions, prerequisites, outcomes
- Choice system: player choice presentation, consequence chain, state mutation
- Consequence tracking: how choices affect subsequent scenes, NPC states, and global game state
- Team synergy system: personality combination bonuses (e.g., Elena + Kade personality synergy = 15% ATK bonus)
- Scene state machine: valid transitions, branching logic, linear/branching story paths
- Narrative API endpoints: scene queries, choice submission, state progression
- Integration with Sprint 1 persistence (save/load narrative state)
- Integration with Sprint 2 combat encounter setup (synergy bonuses applied to combat)

**Out of scope:**
- React frontend scene rendering (Sprint 5-7)
- Advanced branching complexity (e.g., multi-flag consequences, dynamic scene scaling)
- Narrative content generation (scenes and choices are static JSON templates)
- Full Act 2-9 story (Act 1 only — designed for investor demo)

## Constraints

- **Game Design (fixed):** 3 test NPCs (Elena, Lars, Kade), personality traits affect scene presentation and dialogue options, no dead ends (all gates have fallback paths)
- **Technical (inherited from Sprint 1+2):** TypeScript + Express, immutable state management, JSON file persistence, REST API, no mutations
- **Integration Points (mandatory):** Personality system (trait range, bounds checking), combat system (synergy bonuses applied before combat), existing save/load architecture

## Key Assumptions

| Assumption | Confidence | Basis |
|-----------|-----------|-------|
| Sprint 1+2 architecture and patterns are locked | High | Completed sprints, established in codebase |
| Synergy bonus math is simple (percentage multipliers) | Medium | Not detailed in specs; needs design |
| Act 1 story scope is 4-6 scenes for demo | High | Typical for investor pitch pacing |
| Scene definitions are static JSON (no procedural generation) | High | Scope constraint stated as out |
| Player party is always 3 characters (no dynamic composition) | High | Sprint 2 enforces 3v3 combat |

## Open Questions for Planning

- **Scene Architecture:** How are scenes defined and stored? JSON templates with state overrides, or a more complex state machine? How do prerequisites work (trait gates, choice history, etc.)?
- **Consequence System:** How deep should choice consequences go? Single next-scene effects, or ripple consequences across multiple scenes?
- **Synergy Calculation:** What's the formula? Is it pairs of party members or full team composition? Are synergies additive or multiplicative?
- **Branching Strategy:** How many branches from choices? Linear story with minimal branching, or more complex choice tree?
- **Scene Persistence:** Do scenes have in-scene choices (e.g., investigative branches within one scene) or only inter-scene choices?
