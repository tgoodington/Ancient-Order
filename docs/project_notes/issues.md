# Work Log

This file logs work completed on tickets. Keep it simple - just enough to remember what was done.

---

## Entries

### 2026-01-22 - SPRINT1-001: Project Setup and Memory System
- **Status**: Completed
- **Description**: Set up project memory system, reviewed Sprint 1 documentation, identified technical inconsistencies
- **Notes**: 6 documents reviewed, key facts and decisions logged, bugs/observations documented

### 2026-01-22 - SPRINT1-002: Backend Implementation
- **Status**: Completed
- **Description**: Implemented Sprint 1 backend per Sprint1_ClaudeCode_Prompt.md
- **Notes**: All 4 phases complete - types, personality system, state management, persistence, dialogue engine, REST API

### 2026-01-23 - DOC-001: Documentation Reorganization and Assimilation
- **Status**: Completed
- **Description**: Assimilated new handoff documents into project, consolidated INDEX.md into project_notes
- **Changes**:
  - Added Claude_Code_Handoff_Document.md and GM_Combat_Tracker_Documentation.md references
  - Updated key_facts.md with combat system reference, Act 1 narrative, sprint schedule
  - Added ADR-007/008/009 for combat system decisions
  - Migrated useful INDEX.md content to key_facts.md (Quick Topic Lookup section)
  - Deleted redundant INDEX.md
  - Updated CLAUDE.md with new doc paths

### 2026-02-03 - INFRA-001: Project Memory System Upgrade and Version Control Setup
- **Status**: Completed
- **Description**: Upgraded project memory system to new Intuition-coordinated standards, initialized git repository, and pushed to GitHub
- **Changes**:
  - Upgraded memory system: new workflow phases (Discovery → Planning → Execution)
  - Created AGENTS.md with comprehensive multi-agent documentation
  - Enhanced CLAUDE.md with Intuition workflow sections
  - Created .project-memory-state.json for session-aware behavior
  - Created .claude/settings.local.json for agent pre-authorization
  - Removed legacy agent_templates folder and all references
  - Created project_plan.md documenting Phase 1 completion and roadmap
  - Initialized git repository with comprehensive .gitignore
  - Made initial commit (33 files, 7,863 insertions)
  - Pushed to GitHub: https://github.com/tgoodington/Ancient-Order
- **URL**: https://github.com/tgoodington/Ancient-Order
- **Notes**: Project infrastructure complete. Memory system ready for coordinated development. State file updated: plan_status now "planned"

### 2026-02-11 - PLAN-001: Sprint 1 Closeout + Sprint 2 Combat Engine Planning
- **Status**: Completed
- **Description**: Comprehensive planning for Sprint 1 test coverage and complete Sprint 2 combat engine implementation
- **Scope**: 19 tasks across 2 workstreams (5 Sprint 1 closeout, 14 Sprint 2 combat)
- **Key Decisions**:
  - Three-tier workflow: Planning → Design Exploration (optional) → Execution (ADR-011)
  - Pipeline combat architecture with independent CombatState (ADR-012, ADR-013)
  - Behavior tree AI system with test-driven formula porting (ADR-014, ADR-015)
- **Design Explorations Required**: 2 tasks flagged for `/intuition-design` (behavior tree AI, Group action type)
- **Estimated Duration**: 10-14 days with parallelization
- **Artifacts**: plan.md (comprehensive), execution_brief.md, 5 new ADRs
- **Decisions Updated**: Transitioned from two-tier to three-tier workflow per CLAUDE.md

### 2026-02-21 - PLAN-002: Sprint 1 + Sprint 2 Comprehensive Planning
- **Status**: Completed
- **Description**: Complete planning for full backend rebuild (Sprint 1 + Sprint 2) with fresh technical architecture
- **Scope**: 22 tasks (9 Sprint 1 narrative, 13 Sprint 2 combat engine)
- **Key Decisions**:
  - Framework: Fastify 4.18 (plugin architecture, TypeScript-native)
  - Testing: Vitest (native TypeScript, fast watch mode for TDD)
  - Sequencing: Linear (Sprint 1 → Sprint 2)
  - Immutability: Deferred to engineering phase (recommendation: Readonly<> types + spread operators)
- **Design Explorations Required**: 2 items flagged for `/intuition-design` before execution (Behavior Tree AI, Group Action Type)
- **Artifacts**: plan.md (comprehensive, Tier: Comprehensive), design_brief.md (Behavior Tree AI), 3 new ADRs (016, 017, 018)
- **Next Phase**: Design exploration for Behavior Tree AI, then Group Action Type design, then execution
- **Status Tracking**: State updated to design phase, design queue initialized with 2 items

### 2026-02-23 - BUILD-001: Sprint 2 Combat Engine Implementation + Verification
- **Status**: Completed
- **Description**: Full implementation of 13 Sprint 2 tasks (Combat Type System through E2E Demo Encounter)
- **Scope**: Tasks 10-22 (2 design items, 11 implementation items, 1 E2E validation)
- **Completion**: 793/793 tests passing across 25 test files
- **Verification**: Security PASS (no secrets, no injection), Code Review PASS (with corrections applied), Tests PASS, Build PASS (zero TypeScript errors)
- **Corrections Applied**:
  - Task 11 Code Review FAIL → Fixed: Added exact numeric test values for `calculateBaseDamage`, added "pending Excel verification" note
  - Task 17 Code Review FAIL → Fixed: Removed dead `lerpScores` code, added documentation comments, added interior bracket tests
  - Security MEDIUM-1 → Fixed: Added `maxItems: 10` to declare endpoint schema
- **Key Implementation**:
  - 60+ new source files (types, combat engine, behavior tree, API layer, fixtures)
  - 22-file behavior tree system with 7 scoring factors, 3 archetype profiles
  - 1.5x multiplier GROUP action type with priority 0, energy gate, forced Block defense
  - 5-phase round orchestrator with real AI evaluator integration
  - 3v3 E2E demo encounter validating all 5 action types
- **Required User Actions**:
  - Verify `src/fixtures/encounter.json` stats against Excel "Battle Scenarios" sheet
  - Verify `calculateBaseDamage` formula against Excel `Math!A40:AM54`
- **Artifacts**: build_report.md, 793 tests, ~5000 lines of production code
- **URL**: https://github.com/tgoodington/Ancient-Order (push candidate after git confirmation)

### 2026-03-03 - BUILD-002: Sprint 3 Narrative & State Machine Implementation
- **Status**: Completed
- **Description**: Full implementation of Sprint 3: scene graph engine, choice/consequence system, team synergy bonuses, narrative REST API, and Act 1 demo content
- **Scope**: 12 tasks (T1-T12), 2 specialist domains (Game Backend Engine, Narrative Designer)
- **Completion**: 969/969 tests passing across 31 test files (up from 793)
- **Files**: 16 new files, 9 modified files
- **Review Chain**:
  - Phase 1 (Backend): Specialist review PASS (1 remediation), Security review PASS (2 issues fixed: maxLength on API inputs, gate detail leak removed)
  - Phase 2 (Narrative): Specialist review PASS (1 remediation: flag prerequisite scene added, tests strengthened), Security review PASS (no findings)
- **Key Deliverables**:
  - `src/types/narrative.ts` — 20 type exports
  - `src/narrative/` — sceneEngine, choiceEngine, narrativeStateMachine, synergyCalculator
  - `src/api/narrative.ts` — 6 REST endpoints
  - `src/fixtures/scenes/act1_demo.json` — 4 demo scenes
- **Deviations**: 4 scenes instead of 3 (added scene_combat_briefing to satisfy flag prerequisite AC)
- **Artifacts**: build_report.md, 4 new ADRs (036-039)

### 2026-03-03 - PLAN-003: Sprint 3 Planning & Detail Phase
- **Status**: Completed
- **Description**: v9 workflow planning, team assembly, and detail phase for Sprint 3
- **Scope**: 12 tasks planned, 2 specialists assembled (Game Backend Engine, Narrative Designer)
- **Key Decisions**: Flat AND prerequisites (D1), scene fixtures in src/fixtures/scenes/ (D3), synergy via initCombatState (D5), Ironhold town name (D5-narrative)
- **Artifacts**: plan.md, team_assignment.json, 2 blueprints, build_brief.md

### 2026-03-07 - PLAN-004: Sprint 4 Planning & Detail Phase
- **Status**: Completed
- **Description**: v9 workflow planning, team assembly, and detail phase for Sprint 4
- **Scope**: 3 tasks planned, 1 specialist assembled (Game Backend Engine)
- **Key Decisions**: Team locked during combat/narrative (D1), ValidationResult discriminated union (D2), live NPC state when game active (D3), reuse VALIDATION_ERROR code (D4), team as string array on GameState (D5), top-level-only combat validation (D6)
- **Artifacts**: outline.md, team_assignment.json, 1 blueprint, prompt_brief.md

### 2026-03-07 - BUILD-003: Sprint 4 Persistence & API Completion
- **Status**: Completed
- **Description**: Full implementation of Sprint 4: saves management endpoints, player/NPC state endpoints, deepened state validation
- **Scope**: 3 tasks (T1-T3), 9 files modified (8 source + 1 test)
- **Completion**: 969/969 tests passing (no regressions)
- **Review Chain**: Specialist review PASS, Builder verification PASS, Security review PASS (2 issues remediated: missing DELETE slot validation, unbounded POST /team schema)
- **Artifacts**: build_report.md

### 2026-03-22 - TEST-005: Sprint 5 Frontend Test Suite
- **Status**: Completed
- **Description**: Created comprehensive test suite for Sprint 5 React frontend (Equinox HUD)
- **Scope**: 54 tests across 8 test files covering apiClient, GameStateProvider, and 6 UI components
- **Test Stack**: Vitest + @testing-library/react + jsdom
- **Results**: 54/54 passing, 3/3 mutations caught, 0 escalated issues
- **Artifacts**: test_report.md, test_strategy.md, 8 test files in client/src/

<!-- Add work log entries below this line -->

### 2026-05-31 - COMBAT-001: Reaction Overhaul Layer A (ADR-054)
- **Status**: Completed
- **Description**: Implemented Layer A of the corrected reaction model — SMR-mitigated dodge/parry success and a reworked counter chain that routes through reaction selection.
- **Scope**: `types/combat.ts`, `formulas.ts`, `defense.ts` (+ new shared `effectiveReactionSkills`), `pipeline.ts`, `counterChain.ts`, `encounter.json`, `api/combat.ts`; 7 combat test files re-baselined.
- **Verification**: 1030/1030 tests pass; `tsc --noEmit` clean; eslint clean.
- **Follow-ups**: Layer B (reaction rank/XP progression) pending.
- **Refs**: ADR-054; bugs.md "Dodge/Parry success" (now resolved).

### 2026-05-31 - COMBAT-002: Dodge/Parry SMR balance pass (ADR-054 Layer A)
- **Status**: Completed
- **Description**: Replaced uniform placeholder dodge/parry SMR with values SR-scaled onto the Excel `Defense Simulations` SMR bands (dodge 0.80–0.90, parry 0.90–1.00), per-file min→max. Better defenders mitigate more; top parriers reach SMR 1.0.
- **Scope**: `src/fixtures/encounter.json`, `src/api/combat.ts`. Decoded the full `Defense Simulations` rank→rates table into ADR-054 as Layer B's source data.
- **Verification**: 1030/1030 tests pass.
- **Note**: Interim values — Layer B will derive all reaction rates from per-reaction rank via the same table.
- **Refs**: ADR-054 (decoded table); commits cc96cba (Layer A), 7c71e60 (balance pass).

### 2026-05-31 - COMBAT-003: Reaction Progression Layer B (ADR-054)
- **Status**: Completed
- **Description**: Implemented per-reaction rank/XP progression. Player-party combatants accumulate reaction XP by using a reaction; rank and SR/SMR/FMR are derived from the decoded tables. Rates rederive mid-combat on rank-up; XP persists across combats in `GameState`.
- **Decisions (with designer)**: XP success +4 / fail +2 (cap 1675); recompute rates mid-combat on rank-up; **every player-party combatant progresses** (revises the ADR's single-player assumption — companions train too); new game starts at rank 1, demo seeding deferred. Decoded the XP curve from the `Reaction Progression & Log` sheet into ADR-054.
- **Scope**: new `src/combat/reactionProgression.ts`; `src/types/combat.ts` (`ReactionProgress`, `Combatant.reactionProgress`); `src/types/index.ts` + `src/state/gameState.ts` (`GameState.reactionProgress` map); `src/combat/sync.ts` (seed + derive + harvest); `src/combat/pipeline.ts` + `src/combat/counterChain.ts` (XP-on-use); `src/persistence/saveLoad.ts` (validate + back-compat). Tests: new `reactionProgression.test.ts` + additions to `sync.test.ts`, `saveLoad.test.ts`, `pipeline.test.ts`.
- **Verification**: 1060/1060 tests pass; tsc + eslint clean. No existing combat test re-baselined (they build Combatants directly, bypassing init-time seeding).
- **Known edge**: a mid-fight *block* rank-up rederives base block rates and would overwrite an outstanding Crushing Blow degradation (rare; documented in ADR-054).
- **Refs**: ADR-054 (Layer B implementation note, decoded XP curve).

### 2026-10-24 - BUILD-006: Sprint 6 Self-Contained Game Foundation (ADR-055–061)
- **Status**: Completed (verified 2026-10-25)
- **Description**: The game now runs entirely in the browser. The client merged into one root package (Vite 8, React 19, TypeScript 6, Vitest 5, ESLint 10). An in-client game session (`src/session/`) replaced the Fastify routes. Saves are local and versioned (`src/persistence/`, localStorage behind an async backend). A Phaser 4 stage sits behind the unchanged Equinox HUD. The server is deleted.
- **Fixes beyond the routes (designer-approved)**:
  - A refused declaration keeps the previous orders.
  - Starting a fight while one runs is refused.
  - The last fight result stays readable after the fight.
  - An unknown scene is refused.
  - Dialogue is owned by the session.
  - New game and load clear stale combat orders.
  - The save quirks are fixed.
  - Combat orders are limited to the player's party with bounded energy (ADR-060).
  - The HUD scrolls on short windows.
  - Team buttons show companion names only, without archetype labels (ADR-061, decided in verify).
- **Security fixes from review**:
  - Prototype-key id lookups (getNpc, setTeam, startDialogue, startEncounter).
  - Shared mutable content is now frozen per session.
  - A crafted save could break the save list; parseSave now never throws and has size and reason caps.
- **Verification**: `npm run verify`: 59 files, 1328 tests, tsc and eslint clean; `npm run build` passes with Phaser as its own chunk; npm audit 0.
- **Live verification (2026-10-25)**: A real headless Chrome, driven by Puppeteer, walked the dev and production builds. Results: 44 HUD and stage checks passed. These covered names-only buttons, personality, team selection, keyboard use, and one canvas under StrictMode, reload, and hot reload. They also covered the canvas painting #1a1410, clicks passing through to the stage, resizing, short-window HUD scrolling, and zero outside requests. Another 45 engine and save checks passed in-page, with saves surviving a full browser restart. After the label change, `npm run verify` reports 59 files and 1329 tests. Unreachable without UI: the error banner, the combat bars, and the disabled-in-combat team state; tests cover them. Pre-existing: the favicon.ico 404 (the page has never had an icon).
- **Refs**: docs/project_notes/branches/sprint-6/build_output.json; ADR-055–061.

### 2026-10-28 - BUILD-007: Godot Foundation — rules engine moved to C# (ADR-063)
- **Status**: Complete (verified 2026-10-28)
- **Description**: The rules moved to a plain C# library (`rules/AncientOrder.Rules`, net10.0, zero dependencies, no Godot reference) covering personality, characters and game state, lineup and synergy, combat math, enemy decisions, round resolution, fight setup, the game session (ADR-057), and versioned file saves (ADR-058). A Godot 4.7.2 (.NET) project (`godot/`) is wired to it with clearly marked stand-in content and a headless smoke run. No UI. The TypeScript build is untouched and remains the playable reference.
- **Deliberate differences**: every one is listed by system in `docs/project_notes/port_record.md`, alongside changed and dropped tests, set-aside work, the combat balance review list, and open creative questions.
- **Security hardening from review**: content and save input refused cleanly instead of throwing (nulls, NaN/Infinity, undefined enums, ids over 64 chars, duplicate fighter ids, finished fights in saves); bounded save reads; in-order queued session notifications; combat orders checked for party, energy range, and fight status.
- **Verification**: `pwsh -File ./verify.ps1` passes (format, build, 1256 C# tests, Godot import, headless smoke); `npm run verify` passes (59 files, 1329 tests).
- **Refs**: docs/project_notes/branches/godot-foundation/build_output.json; docs/project_notes/port_record.md; ADR-063.
- **Live verification (2026-10-28)**: The Godot 4.7.2 editor opened the project and ran the empty main scene (designer confirmed; also run headless), and the frozen browser build still loads at localhost:5173 (designer confirmed). A throwaway driver (scratch only) called the real library through `GameSession` the way a screen will: 88 checks across the session, personality limits (2,000 random shifts), lineup, combat to victory and defeat, refusals, snapshots and listeners, and file saves (slots 1-10, damaged, truncated, wrong-format, oversized, and future-version saves refused cleanly). A second process reopened the save folder and resumed a mid-fight save with a different seed source: all 9 remaining rounds replayed identically, and a control showed a different seed gives a different fight. A fresh-clone copy with no `godot/.godot` passed `verify.ps1`, including the first-time Godot import. `GameHost` was built under Godot's runtime without touching the real save folder.
- **Fixes in verify (designer-approved)**: the smoke run now builds `GameHost` and starts a game through it (no save calls), so `verify.ps1` guards the composition root; personality adjustment messages drop the HTTP-era "body" wording ("empathy must be number"), recorded in the port record.
- **Map**: reconciled with code; Sprint 7's planned (unbuilt) slices removed from the map at the designer's choice and logged in map_history.md.
- **Noted for later sprints (TypeScript parity, no change)**: a finished fight's reported round is one past the last round played (the counter advances after each round); stamina values carry floating-point noise (e.g. 22.800000000000004), so screens should round for display; the solution maps the Godot project's Release configuration to Debug (Godot only defines Debug/ExportDebug/ExportRelease, so no effect today).
- **Final verification**: `verify.ps1` passes (format, build with 0 warnings, 1256/1256 tests, smoke with the GameHost step); `npm run verify` passes (59 files, 1329 tests; no TypeScript file changed).

### 2026-10-30 - BUILD-008: Sprint 7 (Godot) — town and conversation systems
- **Status**: Complete (verified 2026-10-30)
- **Description**: A walkable 3D town and one conversation system built from content packs, carrying all five demo mechanics (personality shifts, personality-gated choices, archetype reactions with tells and demeanor, story memory, party changes), plus the screens to play them in the Equinox look (start, town, conversation panel, party/lineup, save/load, hand-off placeholder), designer-only F3 dev tools, a content check tool, a content guide (`docs/content_guide.md`), and a clearly marked stand-in pack that a rules-test playthrough proves reaches every mechanic. Save format moves to version 2 with an upgrade from version 1. 38 tasks.
- **Security hardening from review**: loader refuses null array items, extreme numbers and over-long ids instead of crashing or dead-ending; hand-off availability judged after a choice's earlier effects; tampered saves can't restore a conversation alongside a fight or hand-off; save names capped and control characters refused; async UI paths guarded against freed nodes; dev tools and tests excluded from release builds and checked by verify.
- **Verification**: `pwsh -File ./verify.ps1` passes three times in a row (format, build, release build without dev tools/tests, 1706 .NET tests, content check 0/0, smoke, 85 Godot screen tests).
- **For the designer to review in verify**: choice numbers shown in the conversation panel vs "no numbers" criterion; start screen title shows the pack name; default locked-line wording; town and screen looks (all placeholder).
- **Refs**: docs/project_notes/branches/sprint-7-godot/build_output.json; ADR-062, ADR-063.
- **Live verification (2026-10-30)**:
  - The designer opened the project in the Godot 4.7.2 editor, ran it to the start screen, and played the hands-on list ("all good").
  - A throwaway GDScript driver outside the repo played the real game in a 1600x900 window with real mouse and keyboard input:
    - the full stand-in route to the hand-off
    - saves and loads, including mid-conversation
    - the party screen, the Esc menu, return to start and New Game, F3, resizes to 1024x640 and 1920x1080, and Quit
  - Final run: 127/127 checks, with screenshots of every state.
  - The content check on a deliberately broken pack copy exits 1, naming each problem's file and place.
- **Fixes in verify**:
  - The conversation panel opened scrolled to the bottom, hiding the speaker's line. It now opens each step at its first new line and grows to fit up to about two-thirds of the screen, with a visible scrollbar beyond that. The theme's scrollbar had zero width.
  - Designer-approved (ADR-078):
    - choices show text only
    - arrows and Enter (wrapping, skipping locked choices) plus mouse, with silent 1-9 keys
    - Esc menu Return to Start and Quit, each asking first
    - start screen titled "Ancient Order"
    - "Lineup full" instead of rules text when a full lineup refuses a pick
  - Stale docs fixed: CLAUDE.md said "No UI yet"; key_facts had the old `godot/StandIn` path and was missing the screen-tests step.
- **Final verification**: `verify.ps1` passes: format, build with 0 warnings, release build without dev tools or tests, 1706/1706 .NET tests, content check 0/0, smoke, and 101/101 Godot screen tests (85 at build, plus 16 added in verify fixes).
- **Noted for later**:
  - UI and scaling pass:
    - the party screen's lineup and "Together" sections sit below the fold
    - the tall conversation panel covers the speaker and avatar, hiding reaction leans (camera framing)
    - town and screen looks
  - Content check: broken references stop the whole-pack analyses, so a designer sees problems in two rounds. This is by design and noted in the map.
  - Export sprint: Godot crashed at startup once when Windows audio (WASAPI) failed to initialize (see bugs.md); check for a fallback. The export-preset exclusions from build_output also carry over.
  - Open creative question: the default locked-line wording ("That is not open to you yet."); the stand-in pack overrides it.
