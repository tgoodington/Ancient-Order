# Ancient Order Project Plan

## Project Overview

**Ancient Order** - Turn-based combat RPG prototype with personality-driven narrative. Act 1 backend for investor/publisher pitch.

**Status**: Sprint 1 Backend Implementation (In Progress)

---

## Completed Work

### Phase 1: Project Initialization and Memory System Setup (2026-02-03)

**Objective**: Establish project infrastructure, upgrade memory system to new standards, and prepare for coordinated development.

**Completed Tasks**:
- ✅ Upgraded project memory system from old standards to new Intuition-coordinated system
- ✅ Created AGENTS.md with comprehensive multi-agent system documentation
- ✅ Enhanced CLAUDE.md with three-phase workflow (Discovery → Planning → Execution)
- ✅ Created .project-memory-state.json for session-aware agent behavior
- ✅ Created .claude/settings.local.json for agent permission pre-authorization
- ✅ Cleaned up legacy agent_templates folder and all references
- ✅ Initialized git repository with comprehensive .gitignore
- ✅ Created initial commit with all project files (33 files, 7,863 insertions)
- ✅ Pushed repository to GitHub (https://github.com/tgoodington/Ancient-Order)

**Artifacts**:
- Memory system: bugs.md, decisions.md (10 ADRs), key_facts.md, issues.md
- Project documentation: CLAUDE.md, AGENTS.md, MEMORY_SYSTEM_UPDATE.md
- Configuration: .gitignore, .claude/settings.local.json, .project-memory-state.json
- Version control: GitHub repository initialized and pushed

---

## Current State

- **Project Memory State**: `initialized: true`, `plan_status: none`
- **Git Repository**: Master branch at commit 6c3497d
- **Agent System**: Transitioned from two-tier to three-phase Intuition workflow
- **Development Ready**: Yes - infrastructure complete, ready for feature development

---

## Upcoming Work

### Phase 2: Sprint 1 Backend Implementation (Planned)

**Scope**: Complete core backend implementation per Sprint1_ClaudeCode_Prompt.md

**Key Components**:
1. Type System & Interfaces (TypeScript)
2. Personality System (trait adjustment, redistribution)
3. Dialogue Engine (gates, dialogue trees, dialogue choices)
4. Game State Management (immutable state, state updates)
5. Persistence System (save/load JSON files)
6. REST API Endpoints (game, NPC, dialogue, player endpoints)
7. NPC System (fixed archetypes, Elena/Lars/Kade)

**Workflow**:
- Use `/intuition-discovery` for exploration of complex components
- Use `/intuition-plan` for strategic design decisions
- Use `/intuition-execute` for implementation and testing

**Estimated Scope**: 4-6 core systems, ~15 TypeScript files

---

### Phase 3: Sprint 2 - Combat System (Future)

⚠️ **Note**: This phase should begin with `/intuition-discovery` to explore combat system requirements and design before planning and implementation.

**Overview**: Implement 5-phase turn-based combat system

**Key Features**:
- Rank KO, Blindside, Crushing Blow mechanics
- Six elemental paths (Fire, Water, Air, Earth, Shadow, Light)
- Energy segment accumulation and Ascension levels
- Defense resolution (Dodge, Block, Parry)
- Information asymmetry (AI commits actions before player sees)

**Formula Source**: All formulas from GM Combat Tracker.xlsx (Excel source of truth)

**Scope**: ~10 combat-related TypeScript files

**Recommended Workflow**: Discovery (Waldo) → Handoff → Planning (Magellan) → Handoff → Execution (Faraday)

---

### Phase 4: Sprint 3 - Narrative & State Machine (Future)

⚠️ **Note**: This phase should begin with `/intuition-discovery` to explore narrative structure and dialogue requirements before planning and implementation.

**Overview**: Implement Act 1 narrative structure

**Scenes** (6 total):
1. The Championship Fight (cinematic intro, tutorial combat)
2. Town Exploration (4 tasks with mentor Dontan)
3. Dontan's Trials (dialogue-based warrior worth tests)
4. Time Skip (5-year training montage narrative)
5. Setting Out (team registration, final preparations)
6. First Gym Town (demonstrates personality gates with new NPCs)

**Recommended Workflow**: Discovery (Waldo) → Handoff → Planning (Magellan) → Handoff → Execution (Faraday)

---

## Technical Architecture

### Current Stack
- **Runtime**: Node.js
- **Framework**: Express.js
- **Language**: TypeScript
- **State Management**: Immutable (no mutations)
- **Persistence**: JSON file-based (saves/ directory)
- **API**: REST endpoints

### Project Structure
```
/Ancient Order/
├── src/
│   ├── types/           # TypeScript interfaces
│   ├── state/           # Game state management
│   ├── dialogue/        # Dialogue engine
│   ├── personality/     # Personality system
│   ├── persistence/     # Save/load functions
│   └── api/             # Express.js endpoints
├── docs/
│   ├── Reference Documents/  # Sprint specs and guides
│   └── project_notes/        # Memory system
├── CLAUDE.md            # Project instructions
├── AGENTS.md            # Agent system documentation
└── .claude/settings.local.json
```

---

## Key Constraints & Principles

**Personality System**:
- 6 traits: Patience, Empathy, Cunning, Logic, Kindness, Charisma
- Range: 5-35% per trait
- Sum: Always equals 100%
- Only player personality changes; NPCs are fixed archetypes

**Dialogue System**:
- No dead ends (all options lead somewhere)
- Personality gates affect flavor, not quest availability
- Operators: `gte`, `lte`, `eq`

**State Management**:
- All updates create new objects (spread operator pattern)
- Timestamp updated on every change
- Deterministic and testable

**Immutability**:
- No mutations anywhere
- Makes save/load reliable
- Future React integration benefits from predictable updates

---

## Next Steps

1. **Immediate** (when ready):
   - Update .project-memory-state.json: `plan_status: "planned"`
   - Use `/intuition-discovery` to explore Sprint 1 requirements in depth
   - Use `/intuition-plan` to create detailed phase breakdown

2. **Short Term** (next session):
   - Implement core type system (TypeScript interfaces)
   - Build personality system with redistribution algorithm
   - Create dialogue engine with gate evaluation

3. **Documentation**:
   - Keep decisions.md updated with architectural choices
   - Log bugs.md for issues encountered and solutions
   - Update issues.md as work completes

---

## Reference Documents

**Quick Links**:
- **Sprint 1 Spec**: `docs/Reference Documents/Sprint1_ClaudeCode_Prompt.md`
- **Technical Details**: `docs/Reference Documents/Sprint1_Technical_Reference.md`
- **API Reference**: `docs/Reference Documents/Sprint1_API_Reference.md`
- **Architecture**: `docs/Reference Documents/Sprint1_Architecture_Diagram.md`
- **Combat System**: `docs/Reference Documents/GM_Combat_Tracker_Documentation.md`
- **Project Overview**: `docs/Reference Documents/Claude_Code_Handoff_Document.md`

---

## Memory System Status

All memory files are active and up-to-date:
- **bugs.md**: 0 active issues (baseline)
- **decisions.md**: 10 ADRs (comprehensive architectural decisions)
- **key_facts.md**: Complete project configuration and reference guide
- **issues.md**: Work log with 3+ historical entries
- **.project-memory-state.json**: Workflow state tracking

Memory protocols are documented in CLAUDE.md and AGENTS.md. All agents follow memory-aware protocols when proposing changes or completing work.

---

**Plan Created**: 2026-02-03
**Last Updated**: 2026-02-03
**Status**: Ready for implementation
