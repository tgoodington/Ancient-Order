# Project Map
_Reflects: verified (2026-10-30) · Reconciled with code: 2026-10-30_

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

### sprint-7-godot
Players can walk a town and talk with its people, and every mechanic the demo exists to show off comes through there. Who the player is shifts with what they choose, opens or closes options, changes how people respond, follows them into later moments, and shapes who fights beside them. When the designer settles a story, a character, or a place, it goes in as content. The town and conversations are built on the foundation the game will ship on, so they grow into the full game's world instead of being rebuilt for it.

## Overview
Ancient Order is a turn-based combat RPG for players, where the player's personality shapes both the story and how combat resolves. It runs as a self-contained game on the player's own machine, with no server and saves kept locally. Its rules live in a plain C# library inside a Godot 4.7 (.NET) project, the engine it will ship on (Steam). The Godot build is the playable game, run from the editor: a start screen, a walkable 3D town, and conversations that carry the demo's mechanics, with in-game saves. The earlier browser build stays in the repo, frozen, as the reference. Its story, characters, and places are content packs, decided by the designer outside the build pipeline and plugged into systems that don't depend on any particular story.

## Operational Foundation
- **Stack:** .NET 10 and C# (ADR-063). `AncientOrder.sln` at the root holds `rules/AncientOrder.Rules` (the rules library, with zero dependencies), `rules/AncientOrder.Rules.Tests` (xUnit v3 on Microsoft.Testing.Platform), `godot/AncientOrder.csproj` (Godot 4.7.2 .NET), and `tools/AncientOrder.ContentCheck` (console tool) with `tools/AncientOrder.ContentCheck.Tests`. `global.json` pins the SDK. The frozen browser build (TypeScript 6, Vite 8, React 19, Phaser 4, Vitest 5, ESLint 10) sits beside it in `src/`, `tests/`, and `package.json`; nothing in C# touches it and it gets no new work.
- **Runtime:** Everything runs on the player's machine. `GameSession` is the only way into the rules (ADR-057), and Godot reaches it through one composition root, `godot/GameHost.cs`. The main scene's root is Screen flow (`godot/ui/ScreenFlow.cs`): it owns the session, renders every snapshot, and hands the session only to the town stage and the presenters. The town stage, panels, and screens read ready-made views from snapshots and never call rules functions directly; the town's area and reach checks are helpers on the town view. Snapshot and async-save results are handed back to the main thread explicitly. In the browser build, `src/session/` plays the same role and `src/ui/appSession.ts` is its composition root.
- **Layering:** The rules library never references Godot or any package, enforced by a build guard (AORULES001) and an architecture test. In the browser build, engine, session, and persistence are platform-neutral (`tsconfig.engine.json`) and browser code lives in `src/ui/` and `src/render/`.
- **Conventions:** Immutable records; order always explicit (ordered lists, lookup-only maps); refusals as `Result` values; rolls, clock, ids, and seeds handed in; JS-identical rounding and stable sorts; banned nondeterministic or transcendental APIs. Authoritative list: T1 in `docs/project_notes/branches/godot-foundation/tasks.json`.
- **Content:** A read-only `GameContent` is handed to the session; the library has none built in.
  - **Packs:** content packs live in `godot/content/<pack>/`: a `pack.json` index plus strict JSON files read into typed records. They are loaded through `IContentSource`, which has folder, in-memory, and Godot `res://` versions.
  - **Choosing the pack:** the project setting `ancient_order/content/pack` picks it (default `res://content/standin`, the clearly marked stand-in pack).
  - **Checking:** every pack passes the content check before play.
  - **Exports:** release export presets must list `*.json` in their non-resource filter.
  - **Test content** lives in the test projects.
  - **Browser build:** loads its own content from `src/fixtures/` and `src/session/content.ts`.
- **Presentation rule (ADR-075):** players never see raw numbers, with one exception: the party readout, which covers the player and the party only.
  - Reactions, relationships, demeanor, and requirements reach players as cues.
  - Enemies never get a readout.
  - The rules keep exact numbers.
- **Dev tools:** designer-only tools in `godot/devtools/` (F3 overlay of hidden numbers, load adjustments, full content problems), available in the editor and debug builds.
  - **Release exclusion:** the `AO_DEVTOOLS` symbol plus `Compile Remove` in the `ExportRelease` configuration leave out `devtools/**` and `tests/**`, and a run-time `OS.IsDebugBuild()` check guards the one call site.
  - **Export presets:** release presets must also exclude those folders.
- **Determinism:** Each fight carries its own seeded dice (xoshiro256**), seeded from the session's seed source and saved with the fight, so a reload replays the same luck.
- **Saves:** A versioned format (separate save records, culture-independent bytes, an upgrade step, reconciled against current content on load), kept as files in a folder chosen by the host (`user://saves` in Godot), written to a temp file then swapped in, and accessed asynchronously. Browser saves (`localStorage`, `src/persistence/`) stay with the frozen build and are not carried over.
- **Deployment:** None yet; the game runs locally. Godot exports desktop builds later; net10 export is unverified until export templates are installed. A fresh machine needs nuget.org on first build (Godot.NET.Sdk). No environment variables or secrets.
- **Developer workflow:** Open `godot/project.godot` in the Godot 4.7.2 (.NET) editor and press F5 to play.
  - **`./verify.ps1` runs in order:**
    1. `dotnet format --verify-no-changes`
    2. `dotnet build AncientOrder.sln`
    3. Release build: `dotnet build godot/AncientOrder.csproj -c ExportRelease`, which proves the game compiles without the dev tools and tests
    4. `dotnet test`
    5. Content check: the tool runs over every `godot/content/*` pack
    6. A first-time Godot `--import`, only when `godot/.godot` is missing
    7. The headless smoke scene, with saves under `user://smoke/saves`
    8. Godot screen tests: `--headless --path godot res://tests/TestRunner.tscn`
  - **Finding Godot:** via `$env:GODOT` or the install path in key_facts.md.
  - **Pieces alone:**
    - rules and tool tests: `dotnet test` from the repo root
    - content check: `dotnet run --project tools/AncientOrder.ContentCheck -- [pack folders]`
    - one screen test: add `-- --filter=Name` to the Godot test command
  - **Frozen browser build:** `npm run dev` to play, `npm run verify` to check it.

## Capabilities

### The frozen browser build stays playable as the reference
- **What stakeholders can do:** Start the browser build with one command (`npm run dev`, no server) and check it with another (`npm run verify`). It starts a new game on every load, over a plain scene stage, with the Equinox HUD and team selection (team buttons show companion names only, ADR-061). Combat, narrative, dialogue, and personality behave as before, with the old routes' latent bugs fixed and combat orders limited to the player's own party with bounded energy spend. Its ten local save slots have no save UI. It gets no new work (ADR-063).
- **Components:** App session setup, Game state provider, Game session, Equinox HUD, Game content, Game engine, Save storage, Save backend, Game window layout, Renderer bridge, Scene renderer, Project scripts, Test suites
- **Key connections:**
  - App session setup creates the Game session, with Save storage over the browser Save backend, and starts a new game on every page load.
  - Game state provider subscribes to the Game session. The Equinox HUD reads the game and sends team changes through it, with refusals in the HUD's error banner.
  - The Game session goes to the Game engine for every rule and plays from Game content.
  - Game window layout puts the Renderer bridge (Phaser Scene renderer) behind the HUD, and clicks outside the HUD reach the stage.
  - Project scripts start the build and run the Test suites.

### Personality, characters, and game state run on the shipping rules
- **What stakeholders can do:** Rely on personality (6 traits, 5–35%, summing to 100%), fixed character archetypes, and game state that is never changed in place, with anything not deliberately changed behaving as in the browser build.
- **Components:** Personality rules, Game state, Rules content, Content packs, Character catalog, Characters module, Rules library, Rules tests
- **Key connections:** Game state → Personality rules to keep the player's personality within limits; a new game builds Game state from the Character catalog in the Rules content built from a Content pack and handed to the C# game session; the Characters module combines catalog and state for every character read and change; Rules tests carry over the TypeScript tests to prove unchanged behavior.

### The fight lineup and team synergy run on the shipping rules
- **What stakeholders can do:** Pick companions for a fight from the party and have team synergy boost them, with unchanged behavior proven by carried-over tests.
- **Components:** Fight lineup, Party, Team synergy, Stat bonuses, Characters module, Game state, Rules tests
- **Key connections:** Fight lineup reads and updates Game state, accepting only Party members within the size limits set by content; Team synergy reads the player and the lineup through the Characters module and is worked out on demand, never stored; Fight setup turns synergy into Stat bonuses on the player's side when a fight starts.

### Combat runs on the shipping rules
- **What stakeholders can do:** Play every part of combat with anything not deliberately changed playing out exactly as in the browser build for the same rolls, and replay any fight exactly from its seed and orders.
- **Components:** Combat rules, Enemy decision-making, Rules content, Archetypes, Encounters, Fight setup, Team synergy, Stat bonuses, Rules tests
- **Key connections:** Fight setup builds the player's side from the player and the lineup (combat stats from the Character catalog) and the enemies from Encounters, with Stat bonuses from Team synergy; Combat rules resolve each round with the fight's own dice, asking Enemy decision-making for enemy orders; Enemy decision-making reads each enemy's Archetype; the Character catalog and Encounters name Archetypes, checked when Rules content is built; Fight setup writes reaction progress back to Game state.

### The Godot screens reach the rules through one game session
- **What stakeholders can do:** Play the town, conversation, and menu screens (and, later, combat) on one session that returns every result as a success or a refusal and publishes a new snapshot on each change.
- **Components:** C# game session, Snapshot views, Game host, Screen flow, Game state, Characters module, Fight lineup, Team synergy, Fight setup, Combat rules, C# save storage
- **Key connections:**
  - Game host builds the content from the content pack, along with the file saves and the C# game session.
  - The C# game session goes to the rules for every action and change.
  - Each new snapshot carries a version number, the current synergy, and ready-made Snapshot views: town, conversation, last choice, party, and hand-off.
  - Screen flow is the one listener. It hands each snapshot to the main thread and routes it to the screens.
  - Screen flow, the Town stage, and the presenters send refusals to the Equinox theme's error banner.
  - The C# game session uses C# save storage to save and load slots.

### Saves live as files on the player's machine
- **What stakeholders can do:** Save to, load from, list, and delete ten slots in a versioned format kept as files on the player's machine, with damaged saves refused cleanly, a mid-fight save resuming with the same dice, and a mid-conversation save resuming at the same step; browser saves stay with the browser build.
- **Components:** C# save storage, Save format, Save files, C# game session
- **Key connections:** C# game session → C# save storage for slot operations; Save format checks every save, upgrades older versions, and pairs it with the current content before loading, listing each adjustment in a load report; C# save storage → Save files for raw reads and writes in a folder chosen by whatever runs the rules.

### The designer runs and checks the Godot build
- **What stakeholders can do:** Open the Godot project in the editor and run it straight to the start screen, and verify everything with one command: the game's content, the screens, and a release build without dev tools.
- **Components:** Godot project, Game host, Smoke run, Stand-in content pack, Content check, Verify script, Rules library, C# game session, Save files, Rules tests, Godot screen tests
- **Key connections:**
  - Game host goes to the C# game session in the Rules library, which never depends on Godot.
  - Game host gives Save files the game's own save folder.
  - Smoke run builds the Game host and starts a game through it. It then drives its own session on the Stand-in content pack through a fight and a mid-fight save and load.
  - Verify script builds everything, including a release build. It runs the Rules tests and the Content check, then runs the Smoke run and the Godot screen tests headless.

### The designer sees the hidden numbers while building content
- **What stakeholders can do:** Press F3 in the editor or a debug build to see the numbers players never see:
  - exact traits
  - what the last choice changed
  - each reaction's strength and its affection and trust change
  - the relationships and demeanor of everyone present
  - locked requirements against current values
  - flags and synergy scores

  The designer also sees every adjustment a load made against changed content, and the full list of content problems. None of this ships.
- **Components:** Dev tools, Screen flow, Snapshot views, C# game session
- **Key connections:**
  - Screen flow installs the Dev tools only in debug builds and passes them read-only events: snapshots, load reports, and content-check reports.
  - Dev tools read Snapshot views and never change the game.
  - The Verify script's release build proves the dev tools are left out.

### Players start a new game or load one
- **What stakeholders can do:** Run the game to a start screen titled with the game's name in the Equinox look. From there, begin a new game in the town the content names, load a save and pick up wherever it left off, or quit. From inside the game, the Esc menu returns to the start screen or quits, each asking first.
- **Components:** Start screen, Screen flow, Save screen, Equinox theme, C# game session
- **Key connections:**
  - Start screen → Screen flow, which starts a new game through the C# game session or opens the Save screen to load.
  - After a load, Screen flow opens whichever screen matches the loaded state (town, conversation, or hand-off).
  - Screen flow's Esc menu and the hand-off screen lead back to the start screen.

### Players walk a 3D town built from content
- **What stakeholders can do:** Walk whatever town the content describes by clicking where to go. The town shows in 3D with placeholder shapes, seen through a camera at a fixed angle that zooms. People's posture shows how they feel about the player.
- **Components:** Town stage, Town rules, Content packs, Conditions, C# game session
- **Key connections:** Content packs supply each town's walkable ground, obstacles, people, and spots; Town rules decide who and what is present from Conditions and hold the player's position; Town stage draws the town from each session snapshot, checks arrival and walk-over areas with the town view's reach and area helpers so it agrees with Town rules, and sends the player's moves back through the C# game session.

### People and spots in the town lead into conversations
- **What stakeholders can do:** Click a person to walk into reach and talk, and trigger spots by interacting or walking over them, with walking paused while a conversation runs.
- **Components:** Town stage, Town rules, Conversation player, Conditions
- **Key connections:** Town stage → C# game session to trigger a person or spot; Town rules open the first entry whose Conditions hold, starting it in the Conversation player; Town stage stops movement until the conversation ends.

### Any written conversation plays on screen
- **What stakeholders can do:** Read one-on-one talks and narrated scenes with any number of speakers and move through them by choosing or continuing.
- **Components:** Conversation player, Conversation panel, Content packs, C# game session
- **Key connections:** Content packs supply conversations; Conversation player steps through them inside the C# game session, recording the conversation and step in Game state; Conversation panel shows each step and sends choices back through the session.

### Choices shift who the player is, and who they are opens options
- **What stakeholders can do:**
  - Shift who they are through their choices. The new values show in their party readout, never as numbers in the conversation.
  - Meet choices they can't make yet, shown greyed with an in-world line, always alongside an ordinary fallback.
  - Make choices whose outcome hinges on who they are, with clues in what people say.
- **Components:** Conversation player, Personality rules, Conditions, Content check, Conversation panel, Party screen, Snapshot views
- **Key connections:**
  - The Conversation player applies a choice's personality shift through Personality rules and reports it.
  - Conditions lock choices or pick a destination by trait threshold. The rules resolve each locked choice's in-world line for the Conversation panel, and only the dev tools show the requirement detail.
  - Content check refuses any step without an always-available choice, and any locked choice with no line to show.
  - The Party screen shows the current traits.

### People respond according to their archetype
- **What stakeholders can do:**
  - See the characters present react to their choices in keeping with each one's fixed archetype. Reactions show as body language: a written line and a lean in the town for now, animation later.
  - Read how someone feels about them from that person's demeanor before they speak.
- **Components:** Reactions, Demeanor, Archetypes, Characters module, Conversation player, Conversation panel, Town stage
- **Key connections:**
  - The Conversation player calls Reactions for each choice that shifts personality or has an override.
  - Reactions read each present character's Archetype leanings, or the choice's written override. They change affection and trust through the Characters module and report a tell: direction, strength, and a line.
  - Demeanor comes from affection plus trust, against cutoffs set in content.
  - The Conversation panel shows each tell's line, and the Town stage plays its lean and holds each person's posture.

### Earlier choices carry forward
- **What stakeholders can do:** See flags, earlier choices, and conversations already seen change later lines, choices, conversations, and who stands where in the town.
- **Components:** Story memory, Conditions, Conversation player, Town rules
- **Key connections:** Conversation player records every choice and seen conversation in Story memory and applies flag effects; Conditions read Story memory, personality, relationships, and the party; Conversation player and Town rules consult Conditions for lines, choices, conversations, people, and spots.

### Choices shape the party, and the player picks who fights
- **What stakeholders can do:**
  - Win someone over, take on a new recruit, or see someone leave through conversations.
  - Pick the fight lineup from the party on one screen. That screen shows a readout with numbers for the player and, if content allows, for party members. Enemies never get one.
  - Feel how well the lineup works together through a line about it, with the bonus numbers shown only if content allows.
- **Components:** Party, Fight lineup, Conversation player, Party screen, Snapshot views, Team synergy
- **Key connections:**
  - The Conversation player applies join and leave effects to the Party.
  - The Fight lineup accepts only Party members.
  - The Party screen reads the party view from the session snapshot: readouts, party, lineup and readiness, synergy feel lines, and the display switches. It sends lineup picks back through the C# game session.

### The town hands off to the fight
- **What stakeholders can do:** Leave the town for the fight with a valid lineup, landing on a clearly marked placeholder until Sprint 8 builds the fight.
- **Components:** Fight hand-off, Fight lineup, Town rules, Conversation player, Screen flow, Hand-off placeholder screen
- **Key connections:**
  - A person or a spot (through Town rules), or a choice (through the Conversation player), leads to the Fight hand-off.
  - The Fight hand-off checks the Fight lineup and records the next fight and lineup in Game state.
  - Screen flow swaps the town for the Hand-off placeholder screen, which reads the hand-off view from the snapshot.

### Players save and load from inside the game
- **What stakeholders can do:** Save to, load from, and delete ten slots from an in-game screen, keeping the town, position, conversation in progress, story memory, party, lineup, and relationships, even after the content has changed.
- **Components:** Save screen, Screen flow, C# game session, C# save storage, Save format
- **Key connections:**
  - The Save screen calls the C# game session's asynchronous save operations and receives the results back on the main thread.
  - Save format version 2 carries this sprint's state, upgrades version 1 saves, and reconciles against current content.
  - Screen flow reopens whichever screen the loaded state calls for and passes the load report only to the Dev tools.

### Creative decisions go in as content
- **What stakeholders can do:** Hand over creative decisions in any form and have them turned into content packs (characters, archetypes, towns, conversations, fights) that are checked before they play, with no code changes for a new story.
- **Components:** Content packs, Content check, Content guide, Rules content, Verify script
- **Key connections:** Claude writes Content packs from the designer's decisions using the Content guide; Content packs build the Rules content only after passing the Content check; Verify script fails when the game's pack fails the check.

### Stand-in content proves every mechanic
- **What stakeholders can do:** Play one stand-in playthrough that reaches every mechanic and all three ways of building the team and ends at the hand-off, clearly marked as test material.
- **Components:** Stand-in content pack, Content check, Rules tests
- **Key connections:**
  - The Stand-in content pack is the game's default pack and passes the Content check.
  - A rules-test playthrough walks the real pack files to the hand-off through the session. It fails, naming the gap, if any mechanic is missed. Mechanics include tells, demeanors, and a choice with a hidden outcome.

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
- **Rules content** — The read-only bundle built from a content pack and handed to the C# game session (settings, character catalog, archetypes, synergy bonuses, towns, conversations, encounters), validated when built, with none built into the rules.
- **Content packs** — A `pack.json` index plus strict JSON files, read into typed records through content sources (folder, in-memory, Godot `res://`), from which the rules build the game's content, every problem reported with its file and location; tests hand in their own packs.
- **Content check** — One check over a content pack for dead-end steps, broken references, bad personalities, malformed conditions and effects, people or spots off walkable ground, unset flags, locked choices with no line to show, and empty player-facing text (plus warnings), run by its console tool in verify and by the game before play. The loader's problems (such as broken references) come first, and the whole-pack analyses (dead ends, ground, flags, reachability) run once the pack loads, so a pack with both kinds shows them in two rounds.
- **Game state** — The C# per-playthrough state (player, each character's affection and trust, party, lineup, town and position, conversation in progress, story memory, hand-off, combat state, reaction progress), never changed in place.
- **Story memory** — The flags, choices taken, and conversations seen that the game state keeps for the rest of the playthrough.
- **Conditions** — The one condition system (flags, earlier choices, seen conversations, traits, relationships, party, lineup ready; all, any, not) shared by lines, choices, conversations, people, spots, and the hand-off.
- **Conversation player** — Plays any conversation from content step by step inside the game session, one format for one-on-one talks and narrated scenes, applying each choice's effects.
- **Reactions** — Works out how the characters present react to a choice from their archetype's leanings against its written shift, or the choice's written override, changing affection and trust and reporting a tell (direction, strength, body-language line).
- **Demeanor** — Each character's attitude toward the player (warm, neutral, wary, cold) from affection plus trust against cutoffs in content, shown as posture instead of numbers.
- **Party** — Who travels with the player, starting from content and changed by conversation choices, and the pool the fight lineup is picked from.
- **Town rules** — The town in the session: its walkable ground, the people and spots present by condition, the player's position in world coordinates, and what each person or spot opens.
- **Fight hand-off** — Checks the lineup and records the next fight and who goes in when the town ends, for Sprint 8 to start from.
- **Character catalog** — The read-only character definitions from content (name, archetype, fixed personality, starting relationships, optional combat stats), kept in content order.
- **Characters module** — The one place that reads and changes characters, combining the catalog with game state behind the session.
- **Fight lineup** — The C# rules for which party members go into a fight and when the lineup can change, with the size set by content.
- **Team synergy** — Content-defined bonuses scored from the player and the lineup's personalities, worked out on demand, best-only or all-that-qualify.
- **Stat bonuses** — The one pipeline that applies flat, additive-percent, and multiplier bonuses to a fighter's stats with a single rounding.
- **Combat rules** — The C# combat math, order declaration, counter chains, group actions, and round resolution, drawing on each fight's own dice.
- **Archetypes** — Content records keyed by archetype id giving each fighter's decision profile or basic behavior and each character's reaction leanings.
- **Enemy decision-making** — The C# logic that chooses each enemy's action and target from its archetype.
- **Encounters** — Content listing each fight's enemies and their stats.
- **Fight setup** — Builds a C# fight from the player, the lineup, and an encounter with synergy applied, and writes reaction progress back to game state.
- **C# game session** — The single C# entry point to the rules, returning results as values and publishing versioned immutable snapshots (ADR-057).
- **Snapshot views** — The ready-to-show views each snapshot carries (town, conversation, last choice report, party view with readouts and display switches, hand-off view), derived on commit so screens never read rules data themselves.
- **C# save storage** — Ten save slots with the browser build's refusals, accessed asynchronously behind swappable storage (ADR-058).
- **Save format** — The versioned save records with checks, an upgrade step, and reconciliation against current content that lists each adjustment in a load report.
- **Save files** — The storage beneath C# save storage that keeps saves as crash-safe files in a folder chosen by whatever runs the rules.
- **Godot project** — The Godot 4.7 (.NET) project connected to the rules library, opening on the start screen when run from the editor.
- **Game host** — The Godot composition root that loads and checks the content pack named by the project setting, and builds the file saves and the game session for Screen flow.
- **Smoke run** — The headless Godot scene that builds the Game host and starts a game through it (no save calls), then plays a fight with a mid-fight save and load in its own save folder to prove the wiring. Further headless checks in `godot/smoke/` (town walk, flow, conversation, panel, presenters, save screen, hand-off, dev tools) run on demand, outside verify.
- **Stand-in content pack** — The clearly marked stand-in cast, archetypes, town, conversations, and fight that is the game's default pack and exercises every mechanic.
- **Equinox theme** — The project default Godot theme carrying the Equinox HUD's look (colors, type, panels, buttons with visible focus, trait colors, line styles, error banner) to every screen.
- **Screen flow** — The main scene's root and owner of the session (handed only to the town stage and presenters): renders every snapshot on the main thread, moves the player between the start screen, the town, conversations, and the hand-off screen, runs the Esc menu (party, saves, return to start, quit; the last two ask first), shows refusals and short messages in the banner and the stand-in marker, and installs the dev tools in debug builds.
- **Start screen** — The first screen, titled with the game's name (the project name), offering a new game, loading a save, or quitting.
- **Town stage** — The 3D town scene: placeholder shapes under a fixed-angle zoom camera, click-to-move walking on a navigation mesh baked from content, names and interaction markers, posture by demeanor and leans for reactions, and position reports and triggers sent to the session.
- **Conversation panel** — The screen over the town showing speakers, lines, choices (locked ones greyed with an in-world line), and body-language reaction lines, with no numbers. Choices show only their text and are taken by mouse, by arrows and Enter (wrapping, skipping locked ones), or silently by the 1–9 keys. The panel grows with its content up to about two-thirds of the screen, scrolling with a visible bar only beyond that, and opens each step at its first new line.
- **Party screen** — The menu screen with the player's readout (traits and combat stats with numbers), the party (member readouts when content allows), the fight lineup against its limits, and a felt synergy line (bonus numbers when content allows); the lineup is picked here, and a pick past the limit snaps back with "Lineup full".
- **Save screen** — The in-game screen for saving to, loading from, and deleting the ten slots.
- **Screen presenters** — The conversation, party, and save presenters that Screen flow installs to connect each screen to the session; the conversation presenter holds a finished conversation's outcome on screen, with the town paused, until the player closes it.
- **Hand-off placeholder screen** — The clearly marked stand-in for the Sprint 8 fight, showing the next fight and the lineup going in.
- **Content guide** — The written reference to every content type, condition, effect, and check rule, and to the designer hand-off loop.
- **Rules tests** — The C# tests carried over from the TypeScript suite plus each sprint's rules and content-check tool tests, including the stand-in playthrough, run by `dotnet test`.
- **Godot screen tests** — Headless scene tests run by an in-house runner (`godot/tests/`) that check every screen's wiring and presentation, including guards that conversations show no numbers.
- **Dev tools** — Designer-only tools in `godot/devtools/` (hidden-numbers overlay, load adjustments, content problems), read-only, excluded from release builds.
- **Verify script** — `verify.ps1`, the one command that checks C# formatting, builds every C# project and a release build without dev tools or tests, runs every test and the content check, imports the Godot project on first use, and runs the Godot smoke run and screen tests headless.
- **Port record** — The record of every deliberate difference, changed or dropped test, deferred change, and open creative question from the move to C# (`docs/project_notes/port_record.md`).

## Pointers
- Bugs → `docs/project_notes/bugs.md`
- Decisions → `docs/project_notes/decisions.md`
- Config, credentials, URLs → `docs/project_notes/key_facts.md`
- Work log → `docs/project_notes/issues.md`
- Map evolution by phase → `docs/project_notes/map_history.md`
- TypeScript → C# port record → `docs/project_notes/port_record.md`
