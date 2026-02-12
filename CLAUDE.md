# Ancient Order

Turn-based combat RPG prototype with personality-driven narrative. Act 1 backend for investor/publisher pitch.

## Technical Stack
- Node.js + Express.js + TypeScript
- Immutable state management (no mutations)
- JSON file-based persistence (saves/ directory)
- REST API for testing and future React frontend

## Key Constraints
- Personality traits: 6 traits, 5-35% range, sum always = 100%
- NPCs have fixed archetypes (only player personality changes)
- No dead ends in dialogue (all gates have ungated fallback)
- All state updates create new objects (immutability)

## Claude Code Preferences
- **Plan Mode Model:** When entering plan mode, use Haiku model for agents (Explore, Plan) to optimize cost/speed

## Documentation

### Sprint 1 (Narrative/State)
- `docs/Reference Documents/Sprint1_ClaudeCode_Prompt.md` - Main implementation spec
- `docs/Reference Documents/Sprint1_Technical_Reference.md` - Personality/dialogue deep dive
- `docs/Reference Documents/Sprint1_API_Reference.md` - TypeScript interfaces and endpoints
- `docs/Reference Documents/Sprint1_Architecture_Diagram.md` - Visual system flows

### Project Overview
- `docs/Reference Documents/Claude_Code_Handoff_Document.md` - Complete project overview & 9-sprint roadmap

### Combat System (Sprint 2)
- `docs/Reference Documents/GM_Combat_Tracker_Documentation.md` - Complete combat system documentation
- `docs/Reference Documents/GM Combat Tracker.xlsx` - Excel combat tracker with all formulas

## Project Workflow and Memory System

This project uses a three-tier workflow coordinated by the Intuition system, with institutional knowledge maintained in `docs/project_notes/` for consistency across sessions.

### Workflow Phases

The project follows a structured three-tier workflow. Each sprint starts with planning, which audits the codebase and existing specs. Planning flags complex tasks for design exploration before execution.

**Tier 1: Planning (Magellan)**
- Purpose: Strategic synthesis and structured execution planning
- Process: Research codebase, audit existing specs, identify patterns, create detailed plan
- Output: `plan.md` with tasks marked as execute-ready or `[DESIGN REQUIRED]`
- When: Starting a new sprint or feature
- Skill: `/intuition-plan`

**Tier 2: Design Exploration (Edison)** *(optional, per flagged task)*
- Purpose: Collaborative technical design for complex subsystems
- Process: Iterative dialogue using DIP framework (Data, Interfaces, Process), codebase research, trade-off analysis
- Output: `design_spec_[component].md` with types, interfaces, algorithms, integration points
- When: Planning flags a task as `[DESIGN REQUIRED]` — subsystem has no existing spec and needs architectural decisions the user should make
- Skill: `/intuition-design`
- Skip when: Task is straightforward, specs already exist, or work is purely mechanical

**Handoff (Orchestrator)**
- Purpose: Process phase outputs, brief the next phase, update memory
- Process: Extract decisions, update key_facts/decisions, generate briefs
- Output: Fresh context for next phase
- When: Between any phase transition
- Skill: `/intuition-handoff`

**Tier 3: Execution (Faraday)**
- Purpose: Methodical implementation with verification and quality checks
- Process: Delegate to specialized sub-agents, coordinate work, verify outputs
- Output: Implemented features, updated memory, completion report
- When: All tasks are fully specified (plan + design specs) and ready to implement
- Skill: `/intuition-execute`

**Standard Workflow**: Planning → Handoff → Execution
**Design-Required Workflow**: Planning → Design (per flagged task) → Handoff → Execution

### Task Classification

Planning classifies each task in `plan.md`:
- **Execute-ready**: Specs exist, implementation is straightforward. Goes directly to execution.
- **`[DESIGN REQUIRED]`**: No spec exists, subsystem needs collaborative design. Must go through `/intuition-design` before execution.

Examples:
- "Add unit tests for personality system" → Execute-ready (pure functions, clear inputs/outputs)
- "Port Rank KO formula from Excel" → Execute-ready (source of truth exists in spreadsheet)
- "Design behavior tree AI for NPC combat" → Design required (no spec, architectural decisions needed)
- "Design Group action type" → Design required (undefined in docs, needs invention)

### Memory Files

**Core Memory Files** (initialized at setup):
- **bugs.md** - Bug log with dates, solutions, and prevention notes
- **decisions.md** - Architectural Decision Records (ADRs) with context and trade-offs
- **key_facts.md** - Project configuration, credentials, ports, important URLs
- **issues.md** - Work log with ticket IDs, descriptions, and URLs
- **.project-memory-state.json** - Workflow phase tracking and session state

**Phase Output Files** (created during workflow):
- **plan.md** - Structured project plan with tasks, dependencies, risks (created by Magellan)
- **design_spec_[component].md** - Technical design specifications (created by Edison, one per designed component)
- **execution_brief.md** - Brief for execution phase (created by Handoff orchestrator)

### Memory-Aware Protocols

**Before proposing architectural changes:**
- Check `docs/project_notes/decisions.md` for existing decisions
- Verify the proposed approach doesn't conflict with past choices
- If it does conflict, acknowledge the existing decision and explain why a change is warranted

**When encountering errors or bugs:**
- Search `docs/project_notes/bugs.md` for similar issues
- Apply known solutions if found
- Document new bugs and solutions when resolved

**When looking up project configuration:**
- Check `docs/project_notes/key_facts.md` for credentials, ports, URLs, service accounts
- Prefer documented facts over assumptions

**When completing work on tickets:**
- Log completed work in `docs/project_notes/issues.md`
- Include ticket ID, date, brief description, and URL

**When user requests memory updates:**
- Update the appropriate memory file (bugs, decisions, key_facts, or issues)
- Follow the established format and style (bullet lists, dates, concise entries)

### Style Guidelines for Memory Files

- **Prefer bullet lists over tables** for simplicity and ease of editing
- **Keep entries concise** (1-3 lines for descriptions)
- **Always include dates** for temporal context
- **Include URLs** for tickets, documentation, monitoring dashboards
- **Manual cleanup** of old entries is expected (not automated)

### Smart Skill Suggestions

**When user suggests planning work:**
If the user mentions starting a new sprint, scoping a feature, or asks "how should we approach..." - prompt them to use `/intuition-plan`:
- "This sounds like a good candidate for planning. Want to use `/intuition-plan` to develop a structured approach first?"
- Don't proceed with ad-hoc planning; guide them to the planning workflow

**When plan has design-required tasks:**
If the plan flags tasks with `[DESIGN REQUIRED]` - prompt them to run design exploration before handoff:
- "The plan flagged [task] for design exploration. Run `/intuition-design` to flesh out the technical design before execution."
- Don't skip design for flagged tasks; execution agents shouldn't make design decisions autonomously

**When all design work is complete:**
If all `[DESIGN REQUIRED]` tasks have corresponding `design_spec_*.md` files - prompt handoff:
- "All design specs are ready. Use `/intuition-handoff` to prepare execution context."
- Handoff orchestrator incorporates design specs into execution brief

**When plan has no design-required tasks:**
If the user approves a plan with only execute-ready tasks - prompt handoff directly:
- "Plan looks ready — all tasks are execute-ready. Use `/intuition-handoff` to prepare execution context."

**When user is ready to execute:**
If handoff is complete - prompt them to use `/intuition-execute`:
- "Execution brief is ready! Use `/intuition-execute` to kick off coordinated implementation."
- Don't start implementing directly; hand off to the execution workflow
