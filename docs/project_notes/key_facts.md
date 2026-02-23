# Key Facts

This file stores project constants, configuration, and frequently-needed **non-sensitive** information.

## Security Warning

**NEVER store passwords, API keys, or sensitive credentials in this file.** This file is committed to version control.

---

## Project Information

- **Project Name:** Ancient Order - Act 1 Prototype
- **Purpose:** Turn-based combat RPG prototype for investor pitches / publisher support
- **Current Sprint:** Sprint 1 (Backend / Narrative / State)
- **Target Deployment:** Vercel (stateless backend)

## Technical Stack

- **Runtime:** Node.js with **ESM** (`"type": "module"`, NodeNext resolution)
- **Framework:** Fastify (plugin-based architecture, TypeScript-native)
- **Language:** TypeScript 5.x (strict mode, ES2022 target)
- **State Management:** Spread operator + `Readonly<T>` type annotations (no Immer)
- **Persistence:** JSON file-based (saves/ directory, 10 slots, slot_N.json)
- **API Style:** REST endpoints (Fastify plugins with JSON Schema validation)
- **Testing Framework:** Vitest (native ESM, Jest-compatible API)
- **Linting:** ESLint flat config + Prettier
- **Build:** TypeScript compiler (tsc), no bundler, source maps + declarations enabled

**NOTE (2026-02-21):** Full backend rebuild of Sprint 1+2 completed with Fastify+ESM+Vitest. All imports use `.js` extensions (NodeNext convention).

**Sprint 1 Status (2026-02-22):** COMPLETE — 261 tests passing.

**Sprint 2 Status (2026-02-23):** **COMPLETE** — 793 tests passing across 25 files, zero TypeScript errors, security review PASS. Ready for user verification of Excel formulas and encounter fixture stats before pitch demo.

## Fastify State Pattern (CRITICAL)

Cross-plugin state uses a **container object**, NOT direct decoration assignment:
```typescript
fastify.decorate('gameStateContainer', { state: null as GameState | null });
// Read/write via: fastify.gameStateContainer.state
```
Direct `fastify.gameState = value` inside a plugin is NOT visible to sibling plugins (plugin encapsulation).

Error handler MUST be registered before plugins:
```typescript
fastify.setErrorHandler(handler);  // FIRST
fastify.register(plugin);          // THEN
```

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
- **Format:** JSON files in `saves/` directory
- **Slots:** 10 slots (slot_1.json through slot_10.json)
- **Metadata:** id, timestamp, version, playerName, location

## Test NPCs (Sprint 1)

| ID | Name | Archetype | Faction | Joinable |
|----|------|-----------|---------|----------|
| npc_scout_elena | Elena | Loyal Scout | DEUS | Yes |
| npc_merchant_lars | Lars | Scheming Merchant | Neutral | No |
| npc_outlaw_kade | Kade | Rogue Outlaw | Rogues | Yes |

## Factions

- **DEUS:** Lawful, structured, protect the innocent
- **Rogues:** Chaotic, freedom fighters, steal from the rich
- **Neutral:** Profit-driven, self-interested

## Act 1 Narrative Structure (6 Scenes)

1. **The Championship Fight** - Cinematic intro, tutorial combat, reveals player as kid
2. **Town Exploration** - 4 tasks to convince mentor Dontan (school, DEUS, Auntie M's cat, battle)
3. **Dontan's Trials** - Dialogue-based warrior worth tests
4. **Time Skip** - 5-year training montage (narrative only)
5. **Setting Out** - Team registration, final preparations, departure
6. **First Gym Town** - Act 1 ends here, demonstrates personality gates with new NPCs

## Project Structure

```
/Ancient Order/
├── src/
│   ├── types/           # TypeScript interfaces
│   ├── state/           # Game state management
│   ├── dialogue/        # Dialogue trees and engine
│   ├── personality/     # Personality adjustment logic
│   ├── persistence/     # Save/load functions
│   └── api/             # Express.js endpoints
├── saves/               # Save files (JSON)
├── docs/
│   ├── From Claude Browser Chat/
│   │   ├── INDEX.md
│   │   ├── Claude_Code_Handoff_Document.md
│   │   ├── GM_Combat_Tracker_Documentation.md
│   │   ├── GM Combat Tracker.xlsx
│   │   ├── Sprint1_*.md (5 files)
│   └── project_notes/
│       ├── bugs.md
│       ├── decisions.md
│       ├── key_facts.md
│       └── issues.md
├── CLAUDE.md
├── package.json
└── tsconfig.json
```

## API Endpoints (Sprint 1)

**Game Management:**
- POST /api/game/new
- GET /api/game/state
- POST /api/game/save/:slot
- GET /api/game/load/:slot
- GET /api/game/saves
- DELETE /api/game/saves/:slot

**NPC & Dialogue:**
- GET /api/npc/:npcId
- GET /api/dialogue/:npcId
- POST /api/dialogue/choose

**Player:**
- GET /api/player
- GET /api/player/personality
- POST /api/player/team

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
- **Sprint 4:** Persistence & API (save/load, REST endpoints)
- **Sprint 5-7:** Frontend (React - Equinox HUD, Town scenes, Combat UI)
- **Sprint 8-9:** Testing & Deployment (Vercel)

## Local Development

- **Default Port:** 3000 (assumed, not specified)
- **Save Directory:** ./saves/

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
- Green: 100%-75%
- Yellow: 74%-50%
- Orange: 49%-25%
- Red: 24%-1%
- Black: 0% (KO)

### Team Synergy Bonuses
- **Balanced** (all traits 15-25%): +5% all stats, +10% team XP
- **Specialist** (one trait 30%+): +15% trait-specific effects
- **Harmony** (two traits 25%+): +8% dual effects, special techniques unlock

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
