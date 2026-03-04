## Research Plan

### R1: Game Backend Engine Blueprint
Read the prior blueprint at `docs/project_notes/branches/sprint-3/blueprints/game-backend-engine.md` to understand the exact JSON format for scene graphs, choice effects, NarrativeState shape, ScenePrerequisite types, dead-end validation rules, and file paths. This is the primary constraint document for what format the narrative content must conform to.

### R2: Existing Type Definitions for Narrative System
Search `src/types/` for any narrative, scene, dialogue, or choice-related TypeScript interfaces. These define the data contracts that scene JSON must satisfy. Look for types like Scene, Choice, SceneNode, NarrativeState, PersonalityGate, ScenePrerequisite, ChoiceEffect, etc.

### R3: NPC Fixture Data and Personality Archetypes
Read NPC fixture files in `src/fixtures/` to understand Elena, Lars, and Kade's personality archetypes, trait distributions, and any existing relationship/affection/trust data. This determines what personality gates are narratively appropriate and what thresholds are achievable.

### R4: Personality System Implementation
Search `src/` for `applyPersonalityAdjustment`, `updateNPCAffection`, `updateNPCTrust` to understand the exact function signatures, parameter shapes, and trait names. This determines what effect types are available for scene choices and what values are valid.

### R5: Existing Scene/Dialogue Content
Search for any existing scene JSON files in `src/fixtures/scenes/`, `data/`, `content/`, `scenes/` or any `.json` files containing scene or dialogue data. Also check for any story outlines or lore documents in `docs/`. Need to understand if there's existing narrative content to build on or if we're starting from scratch.

### R6: Sprint 3 Plan and Design Context
Read `docs/project_notes/branches/sprint-3/plan.md` and `docs/project_notes/decisions.md` for the full sprint context, task dependencies, and all ADRs referenced in the plan context (especially ADR-027 through ADR-033) to ensure the narrative design respects all established decisions.

### R7: World Lore and Story Documents
Search `docs/` for any world-building documents, story outlines, Act 1 blueprints, or setting descriptions that establish the Gym Town setting, DEUS faction, Rogues faction, and the mid-journey context referenced in ADR-028. The narrative content needs to be consistent with established lore.
