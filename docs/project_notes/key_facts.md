# Key Facts

This file stores project constants, configuration, and frequently-needed **non-sensitive** information.

## Security Warning

**NEVER store passwords, API keys, or sensitive credentials in this file.** This file is committed to version control.

---

## Project Information

- **Project Name:** Ancient Order - Act 1 Prototype
- **Purpose:** Turn-based combat RPG, growing from the investor/publisher pitch prototype into the shipping game (ADR-055)
- **Current Sprint:** Sprint 6 (in-browser game session, local saves, Phaser renderer)
- **Target Platform:** PC first (Steam), consoles later. The game runs entirely in the browser today; desktop packaging (Electron or Tauri) is a later sprint.

## Technical Stack

- **Runtime:** Runs entirely in the browser; no server. One package at the repo root (ESM, `"type": "module"`).
- **Language:** TypeScript ~6.0 (strict mode, ES2022 target)
- **Client:** Vite 8, React 19 (Equinox HUD), Phaser 4 (renderer, lazy-loaded)
- **Game interface:** `src/session/` is the single interface the UI and tests call; it calls the engine.
- **State Management:** Spread operator + `Readonly<T>` type annotations (no Immer)
- **Persistence:** Versioned save envelopes in browser `localStorage` behind an async backend interface (`src/persistence/`), 10 slots; a file backend replaces it in the desktop build.
- **Testing Framework:** Vitest 5 (Node + jsdom projects)
- **Linting:** ESLint 10 flat config (typescript-eslint) + Prettier
- **Build:** `npm run build` runs `tsc -b` then `vite build`, producing a static web build in `dist/` (gitignored).
- **Quality gate:** `npm run verify` runs `tsc -b`, `eslint src tests`, and `vitest run` across both projects. **Run this before committing.** NOTE: Vitest uses esbuild and does NOT type-check — a green test run does not imply a clean `tsc`. Always rely on `verify`, not `npm test` alone.
- **Lockfile:** `package-lock.json` (root) is committed for reproducible installs.

**NOTE (2026-02-21):** Full backend rebuild of Sprint 1+2 completed with Fastify+ESM+Vitest. All imports use `.js` extensions (NodeNext convention).

**Sprint 1 Status (2026-02-22):** COMPLETE — 261 tests passing.

**Sprint 2 Status (2026-02-23):** **COMPLETE** — 793 tests passing across 25 files, zero TypeScript errors, security review PASS. Ready for user verification of Excel formulas and encounter fixture stats before pitch demo.

**Sprint 3 Status (2026-03-03):** **COMPLETE** — 969 tests passing across 31 files. Narrative progression layer fully implemented: scene graph engine, choice/consequence system, team synergy bonuses, narrative layer, Act 1 demo content (4 scenes). Security review PASS (2 medium issues remediated).

**Sprint 5 Status (2026-03-22):** **COMPLETE** — React frontend bootstrapped (originally a separate `client/` package talking to a server; since folded into the root package and onto the in-browser session in Sprint 6).

**Sprint 4 Status (2026-03-08):** **COMPLETE** — 1019 tests passing (969 existing + 50 new), zero regressions. Persistence & API Completion: saves management (GET/DELETE), player personality/team endpoints, NPC live-state fallback, deep ValidationResult type. All 50 tests passed on first run. Test files: `src/state/stateUpdaters.test.ts` (appended, 11 tests), `src/persistence/saveLoad.test.ts` (appended, 22 tests), `tests/sprint4.integration.test.ts` (new, 17 tests).

## Core Game Systems

### Personality System
- **Traits (6):** Patience, Empathy, Cunning, Logic, Kindness, Charisma
- **Categories:** Wisdom (Patience, Empathy), Intelligence (Cunning, Logic), Charisma (Kindness, Charisma)
- **Range:** 5% to 35% per trait
- **Sum Constraint:** Always equals 100%
- **Adjustment Rate:** Up to 6% per dialogue choice
- **Changes:** Only player personality changes; NPCs are fixed archetypes

### Dialogue System
- **Gates:** Personality thresholds unlock/hide dialogue options
- **Operators:** `gte` (>=), `lte` (<=), `eq` (==)
- **No Dead Ends:** All dialogue options lead somewhere; players never locked out
- **NPC Behavior:** NPCs stay true to archetype regardless of player choices

### Save/Load System
- **Format:** Versioned save envelopes in browser `localStorage` (`src/persistence/`)
- **Slots:** 10 slots
- **Metadata:** id, timestamp, version, playerName, location

## Player Character

- **Name:** Kael
- **Starting rank:** 1.0 (5th Degree)
- **Age at demo:** Young adult (already trained, already on the tournament journey)
- **Starting personality:** Balanced across all 6 traits (~16-18% each)
- **Starting elemental path:** Light (Kindness-aligned)
- **Identity:** Personality is NOT fixed — every dialogue choice evolves Kael's traits, opening and closing narrative paths

## Party NPCs (Joinable Warriors)

Party composition changes as Kael meets new warriors on the journey. Post-demo, any warrior can join or leave the party. Elena and Kade are the starting companions for the Sprint 3 demo.

**IMPORTANT — Archetype Labels:** The labels "Loyal Scout," "Scheming Merchant," and "Rogue Outlaw" are **internal Sprint 1 personality-coding shorthand** used to define trait distributions and dialogue style. They are NOT in-world titles, character classes, or faction identifiers. In this world, warriors are simply warriors — venerated for their craft and rank.

| ID | Name | Internal Label (Sprint 1) | Faction | Joinable | Elemental Path |
|----|------|--------------------------|---------|----------|---------------|
| npc_scout_elena | Elena | Loyal Scout | DEUS-affiliated | Yes | Light |
| npc_merchant_lars | Lars | Scheming Merchant | Neutral | No | Earth |
| npc_outlaw_kade | Kade | Rogue Outlaw | Rogues-affiliated | Yes | Fire |

**Party member note:** Elena and Kade's faction affiliations are background character detail, not active party-level tension (ADR-027). They are warriors traveling with Kael. Their faction politics stay in the world layer — encounter NPCs carry the DEUS/Rogues story arc, not party members.

## Factions

- **DEUS:** Lawful, structured, protect the innocent
- **Rogues:** Chaotic, freedom fighters, steal from the rich
- **Neutral:** Profit-driven, self-interested

## Act 1 Narrative Structure (6 Scenes) — Post-Demo Full Story

> **Note:** The Sprint 3 investor demo does NOT use this sequence. See "Sprint 3 Demo Scene Design" below.
> This is the intended full Act 1 story arc for post-demo development.

1. **The Championship Fight** - Cinematic intro, tutorial combat, reveals player as kid
2. **Town Exploration** - 4 tasks to convince mentor Dontan (school, DEUS, Auntie M's cat, battle)
3. **Dontan's Trials** - Dialogue-based warrior worth tests
4. **Time Skip** - 5-year training montage (narrative only)
5. **Setting Out** - Team registration, final preparations, departure
6. **First Gym Town** - Act 1 ends here, demonstrates personality gates with new NPCs

## Sprint 3 Demo Scene Design (Mid-Journey Slice)

The investor/publisher demo uses a **purpose-built mid-journey scene set**, not the Act 1 opening. See ADR-028.

**Setting:** A Gym Town on the tournament circuit. Kael is already a trained warrior with Elena and Kade as party.

**3-Scene Structure:**
1. **Town Arrival** — DEUS presence and culture world-building, personality choices with a DEUS NPC, flag-setting choice
2. **Escalation** — Rogue run-in surfaces as a consequence of Scene 1 flag (how player handled DEUS determines Rogue approach)
3. **Gym Fight** — Climactic combat encounter

**Implemented scene set (Sprint 3):**
- `scene_ironhold_arrival` — DEUS presence, personality choices, flag-setting
- `scene_market_disturbance` — Rogue run-in, consequence of Scene 1 flags
- `scene_gym_registration` — Climactic registration with personality gates
- `scene_combat_briefing` — Flag-gated terminal scene (requires `gathered_intel`)
- Scene data: `src/fixtures/scenes/act1_demo.json`

**Narrative Engine Modules (Sprint 3):**
- `src/narrative/sceneEngine.ts` — Scene graph traversal, prerequisite evaluation
- `src/narrative/choiceEngine.ts` — Choice validation, consequence application
- `src/narrative/narrativeStateMachine.ts` — State machine orchestrator
- `src/narrative/synergyCalculator.ts` — Well Rounded + Bond paradigm calculators
- `src/session/` — game session wrapping the narrative engine

## Project Structure

```
/Ancient Order/
├── src/
│   ├── types/           # TypeScript interfaces (index.ts, narrative.ts, combat.ts)
│   ├── state/           # Game state management
│   ├── dialogue/        # Dialogue trees and engine
│   ├── personality/     # Personality adjustment logic
│   ├── combat/          # Combat engine, behavior-tree AI
│   ├── narrative/       # Scene engine, choice engine, state machine, synergy calculator
│   ├── fixtures/        # Synergy config, encounters, scenes/ (act1_demo.json)
│   ├── session/         # Game session: the game's single interface
│   ├── persistence/     # Save storage, backends, validation
│   ├── ui/              # React app and Equinox HUD
│   └── render/          # Phaser renderer
├── tests/               # Session-level integration tests
├── docs/
│   ├── Reference Documents/   # Historical specs
│   └── project_notes/
├── CLAUDE.md
├── index.html
├── package.json
└── tsconfig*.json
```

## Game Interface

The game has no network API. `src/session/` is its interface: the UI and tests call the session, and the session calls the engine. See `src/session/` for the operations.

## Documentation Reference

All docs in `docs/Reference Documents/`:

| Document | Purpose |
|----------|---------|
| Claude_Code_Handoff_Document.md | Project overview & 9-sprint roadmap |
| Sprint1_ClaudeCode_Prompt.md | Main Sprint 1 spec (start here) |
| Sprint1_Technical_Reference.md | Personality algorithm, dialogue gates, NPC examples |
| Sprint1_API_Reference.md | TypeScript interfaces, REST endpoints, testing scenarios |
| Sprint1_Architecture_Diagram.md | Visual system flows, data structures |
| Sprint1_Documentation_Guide.md | Sprint 1 planning guide |
| GM_Combat_Tracker_Documentation.md | Combat mechanics, formulas, data structures |
| GM Combat Tracker.xlsx | Source of truth for combat math (17 sheets) |

## Quick Topic Lookup

**Personality System:**
- Algorithm: Sprint1_Technical_Reference.md → Section 1
- API: Sprint1_API_Reference.md → GET /api/player/personality
- Architecture: Sprint1_Architecture_Diagram.md → Personality System Mechanics

**Dialogue System:**
- Gates: Sprint1_Technical_Reference.md → Section 2
- NPC Examples: Sprint1_Technical_Reference.md → Section 3 (Elena, Lars, Kade)
- API: Sprint1_API_Reference.md → Dialogue Management

**Save/Load:**
- Spec: Sprint1_ClaudeCode_Prompt.md → Goal 5
- API: Sprint1_API_Reference.md → Game Management
- Flow: Sprint1_Architecture_Diagram.md → Save/Load Cycle

**Combat System:**
- Mechanics: GM_Combat_Tracker_Documentation.md → Combat Mechanics Deep Dive
- Formulas: GM_Combat_Tracker_Documentation.md → Key Formula Locations
- Data Structures: GM_Combat_Tracker_Documentation.md → Data Structures for Implementation

**TypeScript Interfaces:**
- Sprint1_API_Reference.md → Complete TypeScript Interface Definitions

**Testing:**
- Scenarios: Sprint1_API_Reference.md → Testing Scenarios (4 end-to-end tests)
- Checklists: Sprint1_Technical_Reference.md → Testing Checklist

## Sprint Schedule (Planned)

- **Sprint 1:** Backend Core (Game State & Personality)
- **Sprint 2:** Combat Engine (port Excel formulas, 5-phase rounds, path/energy system)
- **Sprint 3:** Narrative & State Machine (Act 1 scenes, choice tracking, team synergy)
- **Sprint 4:** Persistence (save/load, player and NPC state access)
- **Sprint 5:** Frontend React Phase 1 (Vite setup, GameStateProvider, Equinox HUD) — COMPLETE
- **Sprint 6:** In-browser game session, local saves, Phaser renderer
- **Later:** Town scenes, Combat UI, PC/Steam packaging (ADR-055)

## Local Development

- `npm install` once.
- `npm run dev` starts the game (Vite, opens the browser at http://localhost:5173).
- `npm run verify` typechecks, lints, and runs every test. Run it before committing.
- `npm run build` produces the static web build.
- Saves live in browser `localStorage`.

## Combat System (Sprint 2 Reference)

### Round Structure (5 Phases)
1. **AI Decision Phase** - AI selects actions, locked before player sees anything
2. **Visual Information Phase** - Player sees stances (A/D/E/S/G) and stamina colors
3. **PC Declaration Phase** - Player responds to visible information
4. **Action Resolution** - Priority order: Defend > Group > Attack/Special > Evade
5. **Per-Attack Resolution** - Rank KO, Blindside, Defense, Damage, Counters

### Core Combat Mechanics
- **Rank KO:** If attacker rank > target rank by 0.5+, roll for instant knockout
- **Blindside:** If attacker speed > target speed, roll to force Defenseless
- **Crushing Blow:** If Block used and action power > target power, debuff Block rates
- **Parry Chains:** Successful Parry triggers counter, can chain indefinitely

### Six Paths (Elemental System)
| Path | Style | Effect | Forced Defense |
|------|-------|--------|----------------|
| Fire | Reaction | Boosts own Parry | Parry only |
| Water | Action | Debuffs target Dodge | Dodge only |
| Air | Reaction | Boosts own Dodge | Dodge only |
| Earth | Action | Debuffs target Block | Block only |
| Shadow | Action | Debuffs target Parry | Parry only |
| Light | Reaction | Boosts own Block | Block only |

### Ascension Levels
| Level | Segments Required | Accumulation Bonus | Starting Segments |
|-------|-------------------|-------------------|-------------------|
| 0 | 0 | +0% | 0 |
| 1 | 35 | +25% | 0 |
| 2 | 95 | +25% | 1 |
| 3 | 180 | +50% | 2 |

### Stamina Color Coding
**Game design spec (Visual Info Phase, original):**
- Green: 100%-75%
- Yellow: 74%-50%
- Orange: 49%-25%
- Red: 24%-1%
- Black: 0% (KO)

**Frontend HUD implementation (Sprint 5 — user-accepted specialist design):**
- Green: >60%
- Yellow: >40%
- Orange: >20%
- Red: >0%
- Black: 0% (KO)
Note: Frontend uses symmetric 20% bands. Diverges from design spec. User accepted this during Sprint 5 build (2026-03-22).

### Team Synergy Bonuses (Sprint 3)

**Paradigm-Based Model (POC Design — 2026-02-24):**
- Only the best-matched satisfied paradigm applies (highest-only rule)
- Thresholds: binary (meet it or don't; no partial bonuses)

**Well Rounded Paradigm:**
- Trigger: Every personality trait has ≥25% representation across party
- Bonus: All player party combatants get power ×1.10 (+10% ATK)
- Demo-achievable: Player contributes patience/empathy/kindness ≥25% (NPCs cover cunning/logic/charisma)
- Match quality: (min trait party-max / 25%) clamped to 1.0

**Bond Paradigm:**
- Trigger: Player personality ≥80% aligned with one NPC's dominant traits (top 2 by value)
- Bonus: All player party combatants get speed ×1.10 (+10% SPD, affects initiative & Blindside)
- Demo-achievable: Player mirrors Elena (patience+empathy), Lars (cunning+logic), or Kade (charisma+cunning)
- Match quality: Best alignment ratio across all NPCs

**Paradigm Comparison:**
If both satisfied, highest match quality wins. Tiebreak: Well Rounded > Bond. Provides narrative feedback potential ("92% aligned with Lars").

### Rank Echelons (10 Tiers)
Stone, Iron, Bronze, Silver, Gold, Platinum, Diamond, Master, Grand Master, Legend

### Key Formula Sources
- All combat formulas must be ported from `GM Combat Tracker.xlsx`
- See `GM_Combat_Tracker_Documentation.md` for formula locations and data structures

## Group Action Type (Sprint 2, Task 18)

**Mechanics (POC):**
- Leader-initiated: one combatant declares GROUP, all allies conscripted
- Full trio attack: 3 allies vs 1 enemy target
- Priority 0 (highest): resolves before all other actions
- Energy gate: all participants require full energy at declaration
- Defense suppression: target forced to Block only (no Dodge/Parry)
- Damage: (damageA + damageB + damageC) × 1.5x
- Flexible participants: fires with non-KO'd allies; multiplier unchanged
- Opposing GROUP tie-break: higher team average speed resolves first

**Design Decisions (ADR-020):**
- Priority table updated: GROUP=0, DEFEND=1, ATTACK/SPECIAL=2, EVADE=3
- Validation at declaration (Phase 3), resolution at priority 0
- Energy consumption: all participants reset to 0 (atomic)
- Extensible via GroupActionConfig for future varieties

## Behavior Tree AI System (Sprint 2, Task 17)

**Evaluation Model:** Utility scoring (7 multi-output factors) — not classic behavior tree traversal.

**Scoring Factors (7 + rank meta-factor):**
1. OwnStamina — Self-preservation (favor EVADE when low)
2. AllyInDanger — Team protection (favor DEFEND when ally critical)
3. TargetVulnerability — Offensive opportunity (favor ATTACK when weak target)
4. EnergyAvailability — Resource management (favor SPECIAL when energy available)
5. SpeedAdvantage — Blindside exploitation (favor ATTACK when faster)
6. RoundPhase — Temporal strategy (early: build energy, late: press advantage)
7. TeamBalance — Team stamina comparison (losing: shift defensive, winning: aggressive)
8. **Rank Coefficient** (meta-factor): Linear 0.2-1.0 scaling by combatant rank

**Architecture:**
- Evaluator engine in `combat/behaviorTree/evaluator.ts`
- Factors in `combat/behaviorTree/factors/` (one file per factor)
- CombatPerception builder in `combat/behaviorTree/perception.ts` (pre-computed readonly snapshot)
- Archetype profiles (Elena, Lars, Kade) in `combat/behaviorTree/profiles/`
- Path-based tie-breaking in `combat/behaviorTree/tieBreaking.ts`

**Key Interfaces:**
```
ScoringFactor: name, evaluate(perception, target) → ActionScores
ArchetypeProfile: name, baseScores, factorWeights, elementalPath
CombatPerception: selfStaminaPct, allies[], enemies[], round, etc.
ScoredCandidate: actionType, targetId, score, scoreBreakdown
```

**Design Decisions (ADR-019):**
- Combined (action, target) scoring: ~5-15 candidates evaluated per NPC
- Multi-output factors vs flat: reduces to 7 factors vs ~20
- Perception layer: enforces immutability, avoids redundant computation
- GROUP excluded via config flag until Group Action Type designed
- Deterministic: no randomness in evaluator

**Archetype Profiles:**
- **Elena (Loyal Scout):** Light path. Support-weighted. Responds strongly to ally danger (factor weight 1.8). Prefers DEFEND.
- **Lars (Scheming Merchant):** Earth path. Defensive/efficient. Self-preservation (weight 1.5). Strategic energy use (weight 1.4). Adapts per round.
- **Kade (Rogue Outlaw):** Fire path. Aggressive. Target vulnerability (weight 1.6) and speed advantage (weight 1.5). Ignores team needs.

**Sprint 4 Status (2026-03-07):** **BUILD COMPLETE** — 969 tests passing. API surface completed: saves management (GET/DELETE), player personality/team endpoints, NPC live-state fallback. Deep 6-phase validation (ValidationResult type). Security review PASS (2 issues remediated). Test phase pending.

## Sprint 4 Domain Facts (2026-03-07)

### GameState.team Field
- **Type:** `readonly team: readonly string[]` on `GameState` interface
- **Values:** Empty `[]` when no team selected; exactly 2 NPC ID strings when team is set
- **Position:** After `npcs` field in GameState interface
- **Initialization:** `[]` in `createNewGameState()`
- **Backward compat:** loading normalizes missing `team` to `[]` (same pattern as `narrativeState`)

### State Updater Signature Pattern
- All state updaters follow: `(state: Readonly<GameState>, ...args) => GameState`
- All updaters wrap return with `updateTimestamp()` (from `src/state/stateUpdaters.ts`)
- New `updateTeamComposition(state, npcIds)` follows this pattern

### ValidationResult Type
- **Location:** `src/persistence/validation.ts` (local export, not in types barrel)
- **Shape:** `{ valid: true } | { valid: false; errors: string[] }` (discriminated union)
- **Replaces:** Boolean type predicate return from `validateGameState()`

### Validation Constants (for validateGameState)
- **Personality traits:** `['patience', 'empathy', 'cunning', 'logic', 'kindness', 'charisma']`
- **Personality sum tolerance:** 0.01 (floating-point)
- **CombatPhase valid values:** `'AI_DECISION' | 'VISUAL_INFO' | 'PC_DECLARATION' | 'ACTION_RESOLUTION' | 'PER_ATTACK'`
- **Combat status valid values:** `'active' | 'victory' | 'defeat'`
- **Team validation:** accepts `undefined` (backward compat) or array of 0 or 2 strings

### Error Codes (Pre-Existing, Confirmed)
- `TEAM_COMPOSITION_INVALID` — already defined in `ErrorCodes` (src/types/index.ts line 197)
- `VALIDATION_ERROR` — already defined in `ErrorCodes` (src/types/index.ts line 198), 
- Loading now uses `VALIDATION_ERROR` instead of `SAVE_NOT_FOUND` for corrupt saves

### Error Throwing Pattern (Codebase Convention)
```typescript
const err = new Error(message);
(err as NodeJS.ErrnoException & { code: string }).code = ErrorCodes.CODE_NAME;
throw err;
```

### Session State Access Pattern
- The game session (`src/session/`) holds the active game state; the UI subscribes to it and tests call it directly.
