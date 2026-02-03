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

This project uses a three-phase workflow coordinated by the Intuition system, with institutional knowledge maintained in `docs/project_notes/` for consistency across sessions.

### Workflow Phases

The project follows a structured three-phase workflow:

**Phase 1: Discovery (Waldo)**
- Purpose: Deep understanding of the problem through collaborative dialogue
- Framework: GAPP (Problem → Goals → UX Context → Personalization)
- Output: `discovery_brief.md` and `discovery_output.json` with comprehensive context
- When: Starting new features or investigating complex problems
- Skill: `/intuition-discovery`

**Phase 1.5: Discovery → Planning Handoff (Orchestrator)**
- Purpose: Process discovery output, update memory files, brief planner
- Process: Extract insights, document in key_facts/decisions, generate planning_brief.md
- Output: Fresh context for planning phase
- When: Discovery complete, before planning begins
- Skill: `/intuition-handoff`

**Phase 2: Planning (Magellan)**
- Purpose: Strategic synthesis and structured execution planning
- Process: Research codebase, identify patterns, create detailed plan
- Output: `plan.md` with tasks, dependencies, risks
- When: After discovery handoff, ready to design approach
- Skill: `/intuition-plan`

**Phase 2.5: Planning → Execution Handoff (Orchestrator)**
- Purpose: Process plan, brief executor, update memory
- Process: Extract task structure and risks, generate execution_brief.md
- Output: Fresh context for execution phase
- When: Plan approved, before execution begins
- Skill: `/intuition-handoff`

**Phase 3: Execution (Faraday)**
- Purpose: Methodical implementation with verification and quality checks
- Process: Delegate to specialized sub-agents, coordinate work, verify outputs
- Output: Implemented features, updated memory, completion report
- When: Plan approved and ready to implement
- Skill: `/intuition-execute`

**Recommended Workflow**: Discovery → Handoff → Planning → Handoff → Execution (→ Repeat for next feature)

### Memory Files

**Core Memory Files** (initialized at setup):
- **bugs.md** - Bug log with dates, solutions, and prevention notes
- **decisions.md** - Architectural Decision Records (ADRs) with context and trade-offs
- **key_facts.md** - Project configuration, credentials, ports, important URLs
- **issues.md** - Work log with ticket IDs, descriptions, and URLs
- **.project-memory-state.json** - Workflow phase tracking and session state

**Phase Output Files** (created during workflow):
- **discovery_brief.md** - Discovery phase synthesis (created by Waldo)
- **discovery_output.json** - Structured findings from discovery (created by Waldo, processed by Handoff)
- **planning_brief.md** - Brief for planning phase (created by Handoff orchestrator)
- **plan.md** - Structured project plan with tasks, dependencies, risks (created by Magellan)
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

**When discovery is complete:**
If discovery brief is finalized - prompt them to run handoff before planning:
- "Discovery looks complete! Use `/intuition-handoff` to process insights and prepare for planning."
- Handoff orchestrator updates memory files and creates planning brief

**When user suggests planning work:**
If the user mentions designing features, architecture, complex multi-step work, or asks "how should we approach..." - prompt them to use `/intuition-plan`:
- "This sounds like a good candidate for planning. Want to use `/intuition-plan` to develop a structured approach first?"
- Don't proceed with ad-hoc planning; guide them to the planning workflow

**When plan is ready for execution:**
If the user approves a plan or indicates readiness - prompt them to run handoff before execution:
- "Great, the plan looks ready! Use `/intuition-handoff` to prepare execution context."
- Handoff orchestrator creates execution brief and updates state

**When user is ready to execute:**
If handoff is complete - prompt them to use `/intuition-execute`:
- "Execution brief is ready! Use `/intuition-execute` to kick off coordinated implementation."
- Don't start implementing directly; hand off to the execution workflow
