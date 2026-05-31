# Intuition (Enuncia Pipeline)

A discovery-driven workflow system for Claude Code. Transforms project visions into structured deliverables through foundational discovery, experience-slice decomposition, technical design, and verified implementation.

## Workflow

```
/intuition-enuncia-discovery → /intuition-enuncia-compose → /intuition-enuncia-design →
  /intuition-enuncia-execute → /intuition-enuncia-verify → complete
        ↓
  /intuition-enuncia-start → create branch or /intuition-debugger
```

Run `/clear` before each phase skill to keep context clean.

The first cycle is the **trunk**. After trunk completes, create **branches** for new features or changes.

## Skills

| Skill | What it does |
|-------|-------------|
| `/intuition-enuncia-start` | Detects where you left off, routes to next step |
| `/intuition-enuncia-discovery` | Foundational project discovery — Who, Where, What, Why |
| `/intuition-enuncia-compose` | Maps experience slices, decomposes into buildable tasks |
| `/intuition-enuncia-design` | Technical design — enriches tasks with specs, updates project map |
| `/intuition-enuncia-execute` | Delegates production to subagents, verifies outputs |
| `/intuition-enuncia-verify` | Wires code into project, gets it running, tests the live system |
| `/intuition-enuncia-handoff` | Branch creation and context management |
| `/intuition-initialize` | Sets up project memory (you already ran this) |
| `/intuition-meander` | Thought partner — reason through problems collaboratively |
| `/intuition-think-tank` | Rapid expert-panel analysis |
| `/intuition-debugger` | Expert diagnostic and resolution service |

## Quick Start

### First cycle (trunk)

1. `/intuition-enuncia-start` — see where you are
2. `/intuition-enuncia-discovery` — articulate what you're building and why
3. `/intuition-enuncia-compose` — decompose into experience slices and tasks
4. `/intuition-enuncia-design` — technical design for each task group
5. `/intuition-enuncia-execute` — build from specs
6. `/intuition-enuncia-verify` — wire in, get it running, test the live system (code projects)

Run `/clear` before each phase skill.

### After trunk completes

- `/intuition-enuncia-start` — see project status, create a branch, or start fresh
- `/intuition-debugger` — investigate hard problems
