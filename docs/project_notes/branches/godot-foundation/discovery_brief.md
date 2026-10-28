# Discovery Brief: Godot Foundation — Rules Engine Move to C#

## Parent Context
- **Parent**: sprint-6 (Self-contained game foundation)
- **Inherited dimensions**: None unchanged. ADR-063 (2026-10-26) moved the game to Godot 4.7 (.NET) with C#.
- **Changed dimensions**:
  - Who: players see nothing new this branch. The designer reviews each system as it moves.
  - Where: a plain C# library plus a Godot project, with no UI. The TypeScript build stays as a frozen, playable reference. Before, the game ran in the browser.
  - What: the engine and systems move to C#, keeping only what lasts. Each system is reviewed and changed case by case. No UI.
  - Why: new Branch Goal.
- **Project North Star**: Holds as-is

## Who — Stakeholders
- Designer/director: reviews each system as it moves, both the game rules and how the system is built, and decides which changes happen here and which wait.
- Players: see nothing new from this branch. The frozen TypeScript build stays their playable game for now.

## Where — Delivery
- A plain C# rules library with no Godot dependency, tested with `dotnet test`, plus a Godot 4.7 (.NET) project set up to use it. No UI. Nothing is playable in Godot yet.
- The TypeScript build stays in the repo, frozen and runnable in the browser. It is the reference for the rewrite and the only playable build until Godot has UI.

## What — The Idea
**Goals:**
- Port only what lasts to C#: combat, personality, party, state, session, and saves.
- Review each system with the designer as it moves, covering its rules and its structure.
- Set up the Godot project, wired to the library and ready for UI in later sprints.

**Requirements:**
- Review changes are decided case by case. Small ones are made in this branch. Large ones are recorded for their own sprint. The designer decides which is which.
- Anything not deliberately changed behaves as it does today, proven by tests carried over from the TypeScript suite.
- Every deliberate difference from the TypeScript build is recorded.
- Kept as before: personality limits (6 traits, 5–35%, summing to 100%), fixed NPC archetypes, and immutable state.
- Carried over as design: the session's result values and immutable snapshots (ADR-057), and versioned saves over a swappable backend (ADR-058).

**Constraints:**
- ADR-063: Godot 4.7 (.NET) and C#. The rules are independent of Godot and of rendering.
- ADR-062: systems only, with no creative questions. Creative ideas that come up are logged as open creative questions.
- The TypeScript build is frozen: no new work on it.

**Out of scope:**
- Any UI: HUD, scenes, stage.
- Parts that Sprint 7 retires: the Sprint 3 scene engine, the dialogue-tree engine, and the hard-coded NPC roster.
- Sprint 7 systems (content packs, conversations, town). These resume on Godot after this branch.
- Carrying browser saves into the Godot build.
- Large rule changes the designer sets aside for their own sprint.
- Web builds and Steam packaging or export.

## Why — Branch Goal
Ancient Order's rules move onto the foundation the game will ship on, and the designer stands behind every one that carries forward — both what it does and how it's built. Anything that plays differently from today does so on purpose, and the overworld, conversations, and combat still to come can be built on top without revisiting the rules.

## Decision Posture
Both: the designer wants to see all major creative and technical decisions.

## Open Questions
- None
