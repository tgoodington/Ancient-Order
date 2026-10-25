# Project Map
_Reflects: verified (2026-10-25) · Reconciled with code: 2026-10-25_

## Project North Star
Ancient Order is a turn-based combat RPG where who the player is genuinely shapes how the story unfolds and how combat resolves — a world players want to spend time in, not a showcase of systems.

## Branch Goals
[Populated by branch discovery. Each branch adds a subsection at the same altitude as the Project North Star, scoped to what that branch delivers. Does not replace the Project North Star.]

<!-- Per-branch subsections go here, e.g.:
### [branch-name]
[Branch-specific goal at the same altitude as the Project North Star.]
-->

### sprint-6
Ancient Order runs as a self-contained game on the player's own machine. Each player owns their world and their saves, everything that worked before still works the same, and the game stands on a foundation built to ship.

## Overview
Ancient Order is a turn-based combat RPG for players, where the player's personality shapes both the story and how combat resolves. It runs as a self-contained game on the player's own machine — no server, with saves kept locally — playable in a browser today and headed for a desktop (Steam) release.

## Operational Foundation
- **Stack:** TypeScript 6 in one package at the repo root. Vite 8 serves and builds a React 19 app; Phaser 4 renders scenes; Vitest 5 (Node + jsdom projects) tests; ESLint 10 with typescript-eslint lints. No server.
- **Runtime:** Everything runs on the player's machine in the browser (a desktop wrapper comes later). The game session (`src/session/`) is the game's single interface: the UI and tests call it, and it calls the engine.
- **Layering:** Engine (`src/combat`, `narrative`, `personality`, `dialogue`, `state`, `types`, `fixtures`), session, and persistence are platform-neutral — type-checked with no DOM or Node globals (`tsconfig.engine.json`) and lint-barred from importing React, Phaser, UI, or render code. Browser code lives in `src/ui/` and `src/render/` (`tsconfig.app.json`); `src/ui/appSession.ts` is the composition root.
- **Saves:** Versioned save envelopes in browser `localStorage` behind an async backend interface (`src/persistence/`); a file backend replaces it in the desktop build.
- **Content:** Scenes, encounters, and NPC conversations are data the session loads by default (`src/fixtures/`, `src/session/content.ts`), swappable for tests and future content.
- **Deployment:** None yet — the game runs locally. `npm run build` produces a static web build; Steam packaging (Electron or Tauri) is a later sprint (ADR-055). No environment variables or secrets.
- **Developer workflow:** `npm install` once. `npm run dev` starts the game and opens the browser. `npm run verify` runs typecheck (`tsc -b`), lint, and every test — run it before committing. Test suite alone: `npm test` (`vitest run`, both projects).

## Capabilities

### Players open the game and play with no server running
- **What stakeholders can do:** Open or reload the game to start a new game, and see and use the Equinox HUD and team selection as before (team buttons now show companion names only, ADR-061).
- **Components:** App session setup, Game state provider, Game session, Equinox HUD
- **Key connections:** App session setup creates the Game session and starts a new game on every page load; Game state provider subscribes to the Game session; Equinox HUD reads the provider and sends team changes back through it, with refusals shown in the HUD's error banner.

### Players keep their saves on their own machine
- **What stakeholders can do:** Save to, load from, list, and delete 10 save slots stored locally (no save UI yet; autosaves plus manual saves are the long-term direction).
- **Components:** Game session, Save storage, Save backend
- **Key connections:** Game session → Save storage to save, list, and delete slots; Save storage → Game session to load a slot as a fresh active game; Save storage → Save backend for the raw reads and writes (browser storage now, files in the desktop build).

### Every system players had before still works inside the game
- **What stakeholders can do:** Rely on combat, narrative, dialogue, and personality behaving with the same rules and outcomes as before (with the old routes' latent bugs fixed, and combat orders limited to the player's own party with bounded energy spend), now running inside the game itself.
- **Components:** Game session, Game content, Game engine, Test suites
- **Key connections:** Game session → Game engine for every rule and state change; Game content supplies scenes, encounters, and conversations to the Game session; Test suites → Game session to prove behavior until these systems get UI.

### Players see the game on a scene stage
- **What stakeholders can do:** See the game window as a rendered scene, currently a plain deep-brown backdrop, with the HUD layered on top and unchanged.
- **Components:** Game window layout, Renderer bridge, Scene renderer, Equinox HUD
- **Key connections:** Game window layout places the Renderer bridge behind the Equinox HUD; Renderer bridge starts the Scene renderer with the backdrop color from the design tokens and tells it which scene to show (tests swap in a stand-in renderer); the Equinox HUD keeps its input, while clicks elsewhere pass through to the stage; on a window shorter than the HUD, the HUD layer scrolls.

### The designer runs the game with one command
- **What stakeholders can do:** Start the game with one command and verify everything with one command, with no server to run.
- **Components:** Project scripts, Test suites
- **Key connections:** Project scripts → the game in the browser; Project scripts → Test suites for typecheck, lint, and all tests.

## Component Reference
- **Game engine** — The combat, narrative, personality, and dialogue rules, platform-neutral and built into the client.
- **Game session** — Holds the player's one active game as immutable snapshots and is the single way the client starts, reads, and changes it, returning the old routes' results and errors with their latent bugs fixed. Plays from its own frozen copy of the game content.
- **Game content** — The scenes, encounters, and NPC conversations the session plays from, swappable for tests and future content (scenes are still the Sprint 3 test scene graph, not `act1_demo.json`; NPC greetings are placeholders until Sprint 7).
- **Save storage** — Ten local save slots in a versioned save format with validation and older-save handling, shaped so autosaves can be added later.
- **Save backend** — The raw storage beneath save storage: browser storage today, files in the desktop build.
- **App session setup** — Creates the game's one session with browser saves attached and starts a fresh game on every page load.
- **Game state provider** — Connects the HUD to the game session by subscribing to its snapshots and surfacing refused actions as errors.
- **Equinox HUD** — The stamina and energy bars (shown in combat), personality breakdown, and team selector, layered over the scene stage.
- **Game window layout** — The full-window layout that puts the scene stage behind the HUD, passes clicks outside the HUD through to the stage, and scrolls the HUD on short windows.
- **Renderer bridge** — The React component that starts, switches, and cleanly shuts down the scene renderer behind the HUD.
- **Scene renderer** — Phaser 4 running registered scenes, currently a single empty stage that the Sprint 7 town will join.
- **Project scripts** — One command to start the game and one command to typecheck, lint, and run every test.
- **Test suites** — Engine, game session, save storage, and UI tests that prove behavior holds with the server gone.

## Pointers
- Bugs → `docs/project_notes/bugs.md`
- Decisions → `docs/project_notes/decisions.md`
- Config, credentials, URLs → `docs/project_notes/key_facts.md`
- Work log → `docs/project_notes/issues.md`
- Map evolution by phase → `docs/project_notes/map_history.md`
