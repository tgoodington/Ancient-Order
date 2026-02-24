# Planning Brief: Sprint 3 - Narrative & State Machine

## Discovery Summary

Sprint 3 builds the narrative progression system for Act 1, enabling player choice tracking, consequence propagation, and team dynamics bonuses. This layer connects the personality system (Sprint 1) and combat engine (Sprint 2) into a cohesive story experience, readying the backend for frontend integration in Sprints 5-7.

## Problem Statement

With combat and personality systems operational, the backend now needs the narrative scaffolding — Act 1 scenes, choice consequences, and team synergy effects — to move beyond isolated game systems into a unified game experience.

## Goals & Success Criteria

- Implement Act 1 scene graph with personality-driven branching
- Build choice consequence system that propagates effects through state
- Calculate team synergy bonuses from party member personality combinations
- Integrate narrative progression into persistence layer (save/load)
- Provide REST API for all narrative operations (scene queries, choice submission)
- Maintain backward compatibility with Sprint 1+2 systems

## Key Constraints

- TypeScript + Express architecture (inherited from Sprint 1+2)
- Immutable state management (all mutations create new objects)
- JSON file persistence (no database)
- 3-person party (fixed, enforced by Sprint 2 combat)
- 3 test NPCs with fixed archetypes (Elena, Lars, Kade)
- Personality traits must gate scene content (no dead ends)

## Architectural Context

**Inherited from Sprint 1+2:**
- Personality system: 6 traits, 5-35% range, sum=100%
- State shape: immutable game state object
- Persistence: JSON saves to `saves/` directory (10 slots)
- API: Express.js REST routes, no GraphQL
- Type safety: Full TypeScript with strict mode

**Sprint 3 adds:**
- Narrative layer: scenes, choices, state transitions
- Synergy calculation: party member personality interactions
- Consequence propagation: how choices affect future state

## Assumptions & Risks

| Item | Confidence | Notes |
|------|-----------|-------|
| Scene definitions as static JSON templates | High | Scope excludes procedural generation |
| Synergy math is simple percentage bonuses | Medium | Needs design specification |
| Act 1 is 4-6 scenes | Medium | Typical demo size; may expand |
| Personality gates work as trait range checks | High | Established pattern from Sprint 1 |

## References

- Discovery brief: `docs/project_notes/branches/sprint-3/discovery_brief.md`
- Sprint 1+2 codebase patterns: `src/` directory
- Handoff document: `docs/Reference Documents/Claude_Code_Handoff_Document.md`
