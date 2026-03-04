# Detail Brief

## Current Specialist
- **Name**: narrative-designer
- **Display Name**: Narrative Designer
- **Domain**: narrative/game-design
- **Profile Path**: C:/Users/taylo/.claude/specialists/narrative-designer/narrative-designer.specialist.md

## Assigned Tasks

### Task T10: Act 1 Narrative Design
- **Depth**: Deep
- **Description**: Design the Act 1 narrative arc for the investor demo: branching strategy (linear spine + variants vs true branching vs minimal), scene scope (which 2-3 scenes from the full Act 1 blueprint), choice design (personality gate placement, narrative weight, consequence flags), and scene pacing. This is creative/game-design work that produces a narrative design document specifying scene content, choice trees, flag names, prerequisite conditions, and personality gate thresholds for each scene.
- **Acceptance Criteria**:
  1. Branching strategy selected and documented with rationale
  2. 2-3 scenes defined with narrative text outlines, choice trees, and flag specifications
  3. At least one choice is personality-gated with an ungated fallback path
  4. At least one scene has a prerequisite checking a flag set by a prior scene's choice
  5. Flag naming convention established and all flags documented
  6. Scene pacing supports investor demo flow (opening hook → choice → consequence visible)
- **Dependencies**: None (can run in parallel with engine work)
- **Files**: Design document output (blueprint from detail phase)

---

### Task T11: Act 1 Scene JSON Authoring
- **Depth**: Standard
- **Description**: Author the Act 1 starter scenes as JSON scene graph data, implementing the narrative design from Task 10. Convert the design document's scene outlines, choice trees, flag specs, and prerequisites into valid JSON loadable by the scene graph engine.
- **Acceptance Criteria**:
  1. 2-3 scenes authored as JSON matching the scene graph engine's expected format
  2. All personality gates, prerequisite conditions, and consequence flags match the Task 10 design
  3. All scenes pass dead-end validation (no unreachable dead ends)
  4. Scene data is valid JSON loadable by the scene graph engine (Task 2)
  5. Unit tests validate scene data structure and dead-end freedom
- **Dependencies**: T2, T3, T10
- **Files**: Scene JSON files (location determined during detail phase)

---

## Prior Blueprints

**Read before starting your exploration:**
- **Game Backend Engine Blueprint** (`docs/project_notes/branches/sprint-3/blueprints/game-backend-engine.md`): Specifies the complete narrative engine architecture including:
  - Scene graph JSON format (structure, fields, validation)
  - Choice effect types (personality adjustments, NPC state changes, flag setting)
  - NarrativeState shape (currentSceneId, visitedSceneIds, choiceFlags Record<string, boolean>)
  - ScenePrerequisite types (trait checks, flag checks, visited-scene checks)
  - Dead-end validation requirements (every scene has ≥1 ungated choice)
  - JSON file paths: `src/fixtures/scenes/` for scene data files

---

## Plan Context

### Narrative/Game-Design Considerations (from plan.md Section 10)

- **Branching strategy** determines content volume and test complexity — favor bounded scope. Linear spine + variants is the recommended starting point (caps content).
- **Act 1 is for investor demo** — pacing must hook quickly and demonstrate choice consequence within 2-3 scenes.
- **3 NPCs (Elena, Lars, Kade)** with fixed personality archetypes — gates must be achievable through normal gameplay.
- **Flag naming must be consistent** and documented for cross-scene prerequisite evaluation.
- **Personality gate thresholds must be achievable** through normal gameplay (traits range 5-35%, player starts at 16.67% baseline).
- **No dead ends constraint** — every scene needs at least one ungated choice.
- **Consequence flags** should be semantically meaningful (e.g., `helped_elena`, `revealed_rogue_presence`, not generic like `flag_1`).

### World Context (from ADRs)

- **ADR-027 (Party Members Are Neutral Warriors)**: Party members (Elena, Lars, Kade) have no faction alignment. Faction tension (DEUS vs Rogues) is world-level, not intra-party.
- **ADR-028 (Act 1 Demo — Mid-Journey Gym Town Slice)**: The demo is NOT the Act 1 opening. It's a mid-journey slice in a Gym Town. Player is already a trained warrior; party is assembled. This affects character voice and pacing assumptions.
- **ADR-029 (Rogues Faction — Sporadic Arc Pattern)**: Rogues appear as low-level background presence in early scenes (hint, run-in). Not the main event. Faction tension is seeded, not resolved.

### Related ADRs (Scene & Choice Design)

- **ADR-030 (Scene Prerequisite Model)**: Scene prerequisites use flat array with implicit AND. No OR logic in POC.
- **ADR-031 (Scene Choice Effects)**: Choices reuse existing `applyPersonalityAdjustment()`, `updateNPCAffection()`, `updateNPCTrust()` updaters for consistency.
- **ADR-032 (Scene JSON Storage)**: Scene JSON files stored in `src/fixtures/scenes/`.
- **ADR-033 (NarrativeState Result Type)**: State machine transitions return discriminated union for error handling.
- **ADR-026 (Team Synergy System)**: Two paradigms (Well Rounded, Bond) award stat bonuses for party composition. Player personality choices influence synergy eligibility.

### Open Questions (Resolved During Planning)

These questions from the plan were explored during Game Backend Engine detail and should inform scene design:
1. ✅ Should narrative types live in `types/index.ts` or `types/narrative.ts`? → Decision D1 (ADR-030): `types/narrative.ts` as standalone leaf file
2. ✅ How much overlap between scene graph engine and dialogue engine? → Decision D2 (ADR-031): Independent modules, no shared utilities
3. ✅ Scene JSON files location? → Decision D3 (ADR-032): `src/fixtures/scenes/`
4. ✅ Reuse of existing NPC state updaters? → Decision D1 (ADR-031): Yes, direct reuse

---

## Detail Queue
- [completed] Game Backend Engine (Phase 1)
- [in_progress] Narrative Designer (Phase 2)

---

## Next Steps After This Specialist

After Narrative Designer completes:
1. Run `/intuition-handoff` to transition to the build phase
2. Build brief will be generated for code-writer production
3. Game Backend Engine code (T1-T9, T12) will be implemented by code-writer producer
4. Scene JSON (T11) will be produced after blueprint is approved
5. Final integration validation (T12) verifies all systems working together
