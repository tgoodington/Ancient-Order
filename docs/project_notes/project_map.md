# Project Map
_Reflects: verified (2026-10-28) · Reconciled with code: 2026-10-28_

## Project North Star
Ancient Order is a turn-based combat RPG where who the player is genuinely shapes how the story unfolds and how combat resolves — a world players want to spend time in, not a showcase of systems.

**Current phase — the demo:** a slice of the game that shows off its core mechanics well enough that players come back for, and pay for, the full game — built on foundations that carry into it.

## Branch Goals
[Populated by branch discovery. Each branch adds a subsection at the same altitude as the Project North Star, scoped to what that branch delivers. Does not replace the Project North Star.]

<!-- Per-branch subsections go here, e.g.:
### [branch-name]
[Branch-specific goal at the same altitude as the Project North Star.]
-->

### sprint-6
Ancient Order runs as a self-contained game on the player's own machine. Each player owns their world and their saves, everything that worked before still works the same, and the game stands on a foundation built to ship.

### sprint-7
Players can walk a town and talk with its people, and every mechanic the demo exists to show off comes through there. Who the player is shifts with what they choose, opens or closes options, changes how people respond, follows them into later moments, and shapes who fights beside them. When the designer settles a story, a character, or a scene, it goes in as content, and the systems are ready for whatever the creative work decides.

### godot-foundation
Ancient Order's rules move onto the foundation the game will ship on, and the designer stands behind every one that carries forward — both what it does and how it's built. Anything that plays differently from today does so on purpose, and the overworld, conversations, and combat still to come can be built on top without revisiting the rules.

## Overview
Ancient Order is a turn-based combat RPG for players, where the player's personality shapes both the story and how combat resolves. It runs as a self-contained game on the player's own machine, with no server and saves kept locally. Its rules live in a plain C# library inside a Godot 4.7 (.NET) project, the engine it will ship on (Steam). That project has no screens yet, so the earlier browser build stays in the repo, frozen, as the playable reference. Its story, characters, and places are content, decided by the designer outside the build pipeline and plugged into systems that don't depend on any particular story.

## Operational Foundation
- **Stack:** .NET 10 and C# (ADR-063). `AncientOrder.sln` at the root holds `rules/AncientOrder.Rules` (the rules library, with zero dependencies), `rules/AncientOrder.Rules.Tests` (xUnit v3 on Microsoft.Testing.Platform), and `godot/AncientOrder.csproj` (Godot 4.7.2 .NET). `global.json` pins the SDK. The frozen browser build (TypeScript 6, Vite 8, React 19, Phaser 4, Vitest 5, ESLint 10) sits beside it in `src/`, `tests/`, and `package.json`; nothing in C# touches it and it gets no new work.
- **Runtime:** Everything runs on the player's machine. `GameSession` is the only way into the rules (ADR-057), and Godot reaches it through one composition root, `godot/GameHost.cs`. The Godot main scene is an empty node: no screens yet. In the browser build, `src/session/` plays the same role and `src/ui/appSession.ts` is its composition root.
- **Layering:** The rules library never references Godot or any package, enforced by a build guard (AORULES001) and an architecture test. In the browser build, engine, session, and persistence are platform-neutral (`tsconfig.engine.json`) and browser code lives in `src/ui/` and `src/render/`.
- **Conventions:** Immutable records; order always explicit (ordered lists, lookup-only maps); refusals as `Result` values; rolls, clock, ids, and seeds handed in; JS-identical rounding and stable sorts; banned nondeterministic or transcendental APIs. Authoritative list: T1 in `docs/project_notes/branches/godot-foundation/tasks.json`.
- **Content:** A read-only `GameContent` (settings, characters, archetypes, synergy bonuses, encounters) is handed to the session; the library has none built in. Test content lives in the test project, and clearly marked stand-in content lives in `godot/StandIn` until content packs exist. The browser build loads its own scenes, encounters, and NPC conversations from `src/fixtures/` and `src/session/content.ts`.
- **Determinism:** Each fight carries its own seeded dice (xoshiro256**), seeded from the session's seed source and saved with the fight, so a reload replays the same luck.
- **Saves:** A versioned format (separate save records, culture-independent bytes, an upgrade step, reconciled against current content on load), kept as files in a folder chosen by the host (`user://saves` in Godot), written to a temp file then swapped in, and accessed asynchronously. Browser saves (`localStorage`, `src/persistence/`) stay with the frozen build and are not carried over.
- **Deployment:** None yet; the game runs locally. Godot exports desktop builds later; net10 export is unverified until export templates are installed. A fresh machine needs nuget.org on first build (Godot.NET.Sdk). No environment variables or secrets.
- **Developer workflow:** Open `godot/project.godot` in the Godot 4.7.2 (.NET) editor. `./verify.ps1` runs, in order: `dotnet format --verify-no-changes`, `dotnet build AncientOrder.sln`, `dotnet test`, a first-time Godot `--import` when `godot/.godot` is missing, and the headless smoke scene (saves under `user://smoke/saves`). Godot is found via `$env:GODOT` or the install path in key_facts.md. Test suite alone: `dotnet test` from the repo root. Frozen browser build: `npm run dev` to play, `npm run verify` to check it.

## Capabilities

### Players open the game and play with no server running
- **What stakeholders can do:** Open or reload the browser build to start a new game, and see and use the Equinox HUD and team selection (team buttons show companion names only, ADR-061). This build is frozen and stays the only playable build until the Godot build has screens.
- **Components:** App session setup, Game state provider, Game session, Equinox HUD
- **Key connections:** App session setup creates the Game session and starts a new game on every page load; Game state provider subscribes to the Game session; Equinox HUD reads the provider and sends team changes back through it, with refusals shown in the HUD's error banner.

### Players keep their saves on their own machine
- **What stakeholders can do:** In the browser build, save to, load from, list, and delete 10 save slots stored locally (no save UI yet; autosaves plus manual saves are the long-term direction).
- **Components:** Game session, Save storage, Save backend
- **Key connections:** Game session → Save storage to save, list, and delete slots; Save storage → Game session to load a slot as a fresh active game; Save storage → Save backend for the raw reads and writes in browser storage.

### Every system players had before still works inside the game
- **What stakeholders can do:** Rely on combat, narrative, dialogue, and personality in the browser build behaving with the same rules and outcomes as before (with the old routes' latent bugs fixed, and combat orders limited to the player's own party with bounded energy spend), running inside the game itself.
- **Components:** Game session, Game content, Game engine, Test suites
- **Key connections:** Game session → Game engine for every rule and state change; Game content supplies scenes, encounters, and conversations to the Game session; Test suites → Game session to prove behavior.

### Players see the game on a scene stage
- **What stakeholders can do:** See the browser game window as a rendered scene, currently a plain deep-brown backdrop, with the HUD layered on top.
- **Components:** Game window layout, Renderer bridge, Scene renderer, Equinox HUD
- **Key connections:** Game window layout places the Renderer bridge behind the Equinox HUD; Renderer bridge starts the Scene renderer with the backdrop color from the design tokens and tells it which scene to show (tests swap in a stand-in renderer); the Equinox HUD keeps its input, while clicks elsewhere pass through to the stage; on a window shorter than the HUD, the HUD layer scrolls.

### The designer runs the browser build with one command
- **What stakeholders can do:** Start the frozen browser build with one command and verify it with one command, with no server to run.
- **Components:** Project scripts, Test suites
- **Key connections:** Project scripts → the game in the browser; Project scripts → Test suites for typecheck, lint, and all tests.

### Personality, characters, and game state run on the shipping rules
- **What stakeholders can do:** Rely on personality (6 traits, 5–35%, summing to 100%), fixed character archetypes, and game state that is never changed in place, with anything not deliberately changed behaving as in the browser build.
- **Components:** Personality rules, Game state, Rules content, Character catalog, Characters module, Rules library, Rules tests
- **Key connections:** Game state → Personality rules to keep the player's personality within limits; a new game builds Game state from the Character catalog in the Rules content handed to the C# game session; the Characters module combines catalog and state for every character read and change; Rules tests carry over the TypeScript tests to prove unchanged behavior.

### The fight lineup and team synergy run on the shipping rules
- **What stakeholders can do:** Pick companions for a fight and have team synergy boost the party, with unchanged behavior proven by carried-over tests.
- **Components:** Fight lineup, Team synergy, Stat bonuses, Characters module, Game state, Rules tests
- **Key connections:** Fight lineup reads and updates Game state, checking picks against the Character catalog; Team synergy reads the player and the lineup through the Characters module and is worked out on demand, never stored; Fight setup turns synergy into Stat bonuses on the player's side when a fight starts.

### Combat runs on the shipping rules
- **What stakeholders can do:** Play every part of combat with anything not deliberately changed playing out exactly as in the browser build for the same rolls, and replay any fight exactly from its seed and orders.
- **Components:** Combat rules, Enemy decision-making, Rules content, Archetypes, Encounters, Fight setup, Team synergy, Stat bonuses, Rules tests
- **Key connections:** Fight setup builds the player's side from the player and the lineup (combat stats from the Character catalog) and the enemies from Encounters, with Stat bonuses from Team synergy; Combat rules resolve each round with the fight's own dice, asking Enemy decision-making for enemy orders; Enemy decision-making reads each enemy's Archetype; the Character catalog and Encounters name Archetypes, checked when Rules content is built; Fight setup writes reaction progress back to Game state.

### Future screens reach the rules through one game session
- **What stakeholders can do:** Build the overworld, conversation, and combat screens on one session that returns every result as a success or a refusal and publishes a new snapshot on each change.
- **Components:** C# game session, Game host, Game state, Characters module, Fight lineup, Team synergy, Fight setup, Combat rules, C# save storage
- **Key connections:** Game host builds the content, file saves, and C# game session; C# game session → the rules for every action and change; each new snapshot carries a version number and the current synergy, and listeners hear about it once, isolated from each other's failures; C# game session → C# save storage to save and load slots.

### Saves live as files on the player's machine
- **What stakeholders can do:** Save to, load from, list, and delete ten slots in a versioned format kept as files on the player's machine, with damaged saves refused cleanly and a mid-fight save resuming with the same dice; browser saves stay with the browser build.
- **Components:** C# save storage, Save format, Save files, C# game session
- **Key connections:** C# game session → C# save storage for slot operations; Save format checks every save, upgrades older versions, and pairs it with the current content before loading; C# save storage → Save files for raw reads and writes in a folder chosen by whatever runs the rules.

### The Godot project stands ready for UI
- **What stakeholders can do:** Open the Godot project in the editor, already connected to the rules, and verify everything with one command, with no screens yet.
- **Components:** Godot project, Game host, Smoke run, Godot stand-in content, Verify script, Rules library, C# game session, Save files, Rules tests
- **Key connections:** Game host → C# game session in the Rules library, which never depends on Godot; Game host supplies the game's own save folder to Save files; Smoke run builds the Game host and starts a game through it, then drives its own session on Godot stand-in content through a fight and a mid-fight save and load; Verify script builds everything, runs the Rules tests, and runs the Smoke run headless.

### The designer sees every deliberate difference in one place
- **What stakeholders can do:** Read one record of every deliberate difference from the browser build, every changed or dropped test, every change set aside for its own sprint, and every open creative question raised.
- **Components:** Port record, Rules tests
- **Key connections:** Rules tests → Port record for every test changed or dropped, with a test that fails if any TypeScript test is unaccounted for; the designer's review decisions → Port record for every deliberate difference, deferred change, combat balance item, and open creative question.

## Component Reference
- **Game engine** — The browser build's combat, narrative, personality, and dialogue rules, platform-neutral and built into the client.
- **Game session** — Holds the browser build's one active game as immutable snapshots and is the single way the client starts, reads, and changes it, returning the old routes' results and errors with their latent bugs fixed. Plays from its own frozen copy of the game content.
- **Game content** — The scenes, encounters, and NPC conversations the browser build's session plays from, swappable for tests (scenes are still the Sprint 3 test scene graph, not `act1_demo.json`; NPC greetings are placeholders).
- **Save storage** — The browser build's ten local save slots in a versioned save format with validation and older-save handling.
- **Save backend** — The raw storage beneath save storage: browser `localStorage`.
- **App session setup** — Creates the browser build's one session with browser saves attached and starts a fresh game on every page load.
- **Game state provider** — Connects the HUD to the game session by subscribing to its snapshots and surfacing refused actions as errors.
- **Equinox HUD** — The stamina and energy bars (shown in combat), personality breakdown, and team selector, layered over the scene stage.
- **Game window layout** — The full-window layout that puts the scene stage behind the HUD, passes clicks outside the HUD through to the stage, and scrolls the HUD on short windows.
- **Renderer bridge** — The React component that starts, switches, and cleanly shuts down the scene renderer behind the HUD.
- **Scene renderer** — Phaser 4 running registered scenes, currently a single empty stage.
- **Project scripts** — One command to start the browser build and one command to typecheck, lint, and run every test.
- **Test suites** — The browser build's engine, game session, save storage, and UI tests.
- **Rules library** — The plain C# library holding the game's rules, independent of Godot and rendering, that the shipping game runs on.
- **Personality rules** — The C# personality system: six traits within 5–35% summing to 100%, the default personality, and adjustment.
- **Rules content** — The read-only bundle handed to the C# game session (settings, character catalog, archetypes, synergy bonuses, encounters), validated when built, with none built into the rules.
- **Game state** — The C# per-playthrough state (player, each character's affection and trust, lineup, combat state, reaction progress), never changed in place.
- **Character catalog** — The read-only character definitions from content (name, archetype, fixed personality, starting relationships, optional combat stats), kept in content order.
- **Characters module** — The one place that reads and changes characters, combining the catalog with game state behind the session.
- **Fight lineup** — The C# rules for which companions go into a fight and when the lineup can change, with the size set by content.
- **Team synergy** — Content-defined bonuses scored from the player and the lineup's personalities, worked out on demand, best-only or all-that-qualify.
- **Stat bonuses** — The one pipeline that applies flat, additive-percent, and multiplier bonuses to a fighter's stats with a single rounding.
- **Combat rules** — The C# combat math, order declaration, counter chains, group actions, and round resolution, drawing on each fight's own dice.
- **Archetypes** — Content records keyed by archetype id giving each fighter's decision profile or basic behavior (Sprint 7 adds reaction leanings).
- **Enemy decision-making** — The C# logic that chooses each enemy's action and target from its archetype.
- **Encounters** — Content listing each fight's enemies and their stats.
- **Fight setup** — Builds a C# fight from the player, the lineup, and an encounter with synergy applied, and writes reaction progress back to game state.
- **C# game session** — The single C# entry point to the rules, returning results as values and publishing versioned immutable snapshots (ADR-057).
- **C# save storage** — Ten save slots with the browser build's refusals, accessed asynchronously behind swappable storage (ADR-058).
- **Save format** — The versioned save records with checks, an upgrade step, and reconciliation against current content.
- **Save files** — The storage beneath C# save storage that keeps saves as crash-safe files in a folder chosen by whatever runs the rules.
- **Godot project** — The Godot 4.7 (.NET) project connected to the rules library, with an empty main scene and no UI yet.
- **Game host** — The Godot composition root that builds the content, file saves, and game session for future screens.
- **Smoke run** — The headless Godot scene that builds the Game host and starts a game through it (no save calls), then plays a fight with a mid-fight save and load in its own save folder to prove the wiring.
- **Godot stand-in content** — The clearly marked stand-in cast and encounter inside the Godot project until content packs exist.
- **Rules tests** — The C# tests carried over from the TypeScript suite, run by one command along with the Godot build.
- **Verify script** — `verify.ps1`, the one command that checks C# formatting, builds every C# project, runs every test, imports the Godot project on first use, and runs the Godot smoke run headless.
- **Port record** — The record of every deliberate difference, changed or dropped test, deferred change, and open creative question from the move to C# (`docs/project_notes/port_record.md`).

## Pointers
- Bugs → `docs/project_notes/bugs.md`
- Decisions → `docs/project_notes/decisions.md`
- Config, credentials, URLs → `docs/project_notes/key_facts.md`
- Work log → `docs/project_notes/issues.md`
- Map evolution by phase → `docs/project_notes/map_history.md`
- TypeScript → C# port record → `docs/project_notes/port_record.md`
