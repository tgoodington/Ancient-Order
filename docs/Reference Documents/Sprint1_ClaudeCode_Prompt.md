# Sprint 1: Backend Development Prompt for Claude Code

## Project Overview

You're building the backend for a turn-based combat RPG prototype with sophisticated personality systems, tactical 3v3 combat, and meaningful narrative choices. The prototype targets Act 1 and is designed for investor pitches or publisher support.

**Key Design Philosophy:**
- Personality (6 traits) is as important as combat mechanics
- Choices have narrative consequences but no mechanical punishment
- Only the player character's personality changes; NPCs remain fixed archetypes
- Personality gates create different dialogue paths without dead ends
- Player is never locked out of content due to personality choices

---

## Technical Architecture Requirements

### Stack
- **Backend:** Node.js + Express.js
- **State Management:** Immutable game state structure (objects treated as immutable)
- **Storage:** JSON file-based save/load system
- **Deployment:** Designed for Vercel (stateless backend)
- **API:** REST endpoints for testing and React frontend integration (future)

### Core Principles
1. **Immutable State:** All state updates create new objects rather than mutating existing ones
2. **Single Source of Truth:** All game state flows through centralized state management
3. **Deterministic:** Combat and personality systems must be deterministic for reproducibility
4. **Decoupled:** Combat system operates independently of narrative system
5. **Persistence:** Save/load must preserve exact game state for resuming mid-Act

---

## Sprint 1 Implementation Goals

### Goal 1: Immutable Game State Structure

Create a comprehensive game state object that tracks:

```typescript
interface GameState {
  // Metadata
  id: string
  timestamp: number
  version: string
  
  // World State
  currentLocation: string
  conversationLog: ConversationEntry[]
  
  // Player Character
  player: PlayerCharacter
  
  // NPCs (persistent across game)
  npcs: Record<string, NPC>
  
  // Active Combat (null when not in combat)
  activeCombat: CombatState | null
  
  // Combat History (for progress tracking)
  combatHistory: CompletedCombat[]
  
  // World State Flags
  questFlags: Record<string, boolean>
  relationshipFlags: Record<string, boolean>
}
```

**Player Character Structure:**
```typescript
interface PlayerCharacter {
  id: string
  name: string
  title: string
  currentRank: number // e.g., 2.5 = 5th Degree
  stamina: { current: number; max: number }
  power: number
  speed: number
  
  // Personality Traits (5-35% range, always sum to 100%)
  personality: {
    patience: number      // Wisdom
    empathy: number       // Wisdom
    cunning: number       // Intelligence
    logic: number         // Intelligence
    kindness: number      // Charisma
    charisma: number      // Charisma
  }
  
  // Progression
  coreTrainingPoints: number
  reactionSkills: {
    block: number   // 0-11 ranks
    dodge: number   // 0-11 ranks
    parry: number   // 0-11 ranks
  }
  
  // Current Attunement
  elementalPath: ElementalPath // Fire, Water, Air, Earth, Shadow, Light
  pathSegments: { current: number; max: number }
  pathAscensionLevel: number
  
  // Team Composition (for 3v3 combat)
  team: string[] // NPC IDs of party members
}
```

**NPC Structure (Fixed Archetypes):**
```typescript
interface NPC {
  id: string
  name: string
  archetype: string // "Loyal Scout", "Scheming Merchant", etc.
  faction: "DEUS" | "Rogues" | "Neutral"
  
  // Fixed personality that doesn't change
  basePersonality: {
    patience: number
    empathy: number
    cunning: number
    logic: number
    kindness: number
    charisma: number
  }
  
  // Relationship tracking with player
  affection: number // -100 to +100
  trust: number     // -100 to +100
  
  // Dialogue options (personality-gated)
  dialogueTree: DialogueNode[]
  
  // Availability flags
  joinableInTeam: boolean
  availableLocations: string[]
  questsAvailable: string[]
}
```

**Personality-Gated Dialogue:**
```typescript
interface DialogueOption {
  id: string
  text: string
  
  // Personality Gate (optional - if null, always available)
  personalityGate: {
    trait: keyof PlayerCharacter['personality']
    minimumValue: number // 10-35% range
    operator: 'gte' | 'lte' | 'eq' // Greater than/equal, less than/equal, exact
  } | null
  
  // Effect on personality when chosen
  personalityAdjustment: {
    patience?: number
    empathy?: number
    cunning?: number
    logic?: number
    kindness?: number
    charisma?: number
  }
  
  // Narrative consequence
  consequenceText: string
  
  // Relationship impact
  affectionChange?: number
  trustChange?: number
  
  nextNodeId: string | null // null = end conversation
}

interface DialogueNode {
  id: string
  speakerId: string // NPC id or "player"
  text: string
  options: DialogueOption[]
}
```

### Goal 2: Personality Adjustment Logic

Implement personality system with the following constraints:
- **Range:** Each trait 5% to 35%
- **Redistribution:** When one trait increases, others decrease proportionally to maintain sum = 100%
- **Adjustment Rate:** 6% per trait per dialogue choice
- **Categories:** Wisdom (Patience, Empathy), Intelligence (Cunning, Logic), Charisma (Kindness, Charisma)

**Example Personality State:**
```
Patience:  15%
Empathy:   15%
Cunning:   15%
Logic:     20%
Kindness:  15%
Charisma:  5%
```

**Personality Adjustment Function Specification:**
- **Input:** Current personality object + adjustment deltas (e.g., `{cunning: +6}`)
- **Processing:**
  1. Clamp all traits to 5-35% range
  2. If traits sum > 100%, proportionally reduce untouched traits
  3. If traits sum < 100%, proportionally increase untouched traits
  4. Ensure final sum = 100%
- **Output:** New personality object (new object, not mutated)
- **Important:** This happens only when player makes dialogue choices, not during combat

### Goal 3: NPC Archetype System

Create 3-4 test NPCs representing different archetype/faction combinations:

**Example 1: Loyal Scout (DEUS)**
```typescript
{
  id: "npc_scout_elena",
  name: "Elena",
  archetype: "Loyal Scout",
  faction: "DEUS",
  basePersonality: {
    patience: 20,
    empathy: 20,
    cunning: 10,
    logic: 15,
    kindness: 20,
    charisma: 15
  },
  affection: 0,
  trust: 0,
  joinableInTeam: true,
  availableLocations: ["Harbor", "Market", "Tavern"],
  questsAvailable: ["escort_to_tower", "gather_intel"]
}
```

**Example 2: Scheming Merchant (Neutral)**
```typescript
{
  id: "npc_merchant_lars",
  name: "Lars",
  archetype: "Scheming Merchant",
  faction: "Neutral",
  basePersonality: {
    patience: 10,
    empathy: 8,
    cunning: 28,
    logic: 25,
    kindness: 12,
    charisma: 17
  },
  affection: 0,
  trust: -20,
  joinableInTeam: false,
  availableLocations: ["Market", "Warehouse"],
  questsAvailable: ["smuggle_goods", "information_trade"]
}
```

**Example 3: Rogue Outlaw (Rogues)**
```typescript
{
  id: "npc_outlaw_kade",
  name: "Kade",
  archetype: "Rogue Outlaw",
  faction: "Rogues",
  basePersonality: {
    patience: 12,
    empathy: 8,
    cunning: 25,
    logic: 18,
    kindness: 10,
    charisma: 27
  },
  affection: 0,
  trust: 0,
  joinableInTeam: true,
  availableLocations: ["Underground", "Tavern", "Ruins"],
  questsAvailable: ["raid_convoy", "steal_artifact"]
}
```

### Goal 4: Dialogue Adaptation Based on Player Personality

Implement dialogue system where:
- **Same dialogue tree** for all players (same NPC archetype)
- **Different dialogue options available** based on player personality gates
- **All dialogue options lead somewhere** (no dead ends)
- **NPC responses don't change** - the NPC stays true to their archetype

**Example Dialogue Adaptation:**
```
NPC: "We need someone with cunning to pull off this heist."

Option A (No Gate):
  "I'll help you. What's the plan?"
  → Leads to quest briefing

Option B (Cunning >= 25):
  "A heist? What's the catch? What are you leaving out?"
  → Same quest, player gets additional context
  → Personality adjustment: cunning +6 (capped at 35)

Option C (Cunning <= 10):
  "I'm not sure I can pull off a heist..."
  → Same quest, player can still join
  → Personality adjustment: empathy +6 (capped at 35)
```

**Key:** All three options lead to the same quest outcome; they just flavor the narrative journey differently.

### Goal 5: Save/Load System

Implement file-based persistence:
- **Format:** JSON files in a `saves/` directory
- **Metadata:** Game state includes `id`, `timestamp`, `version`
- **Functions:**
  - `saveGame(gameState, slotNumber)` → writes to `saves/slot_${slotNumber}.json`
  - `loadGame(slotNumber)` → reads from `saves/slot_${slotNumber}.json` and returns GameState
  - `listSaves()` → returns metadata of all saved games (name, location, timestamp)
  - `deleteSave(slotNumber)` → removes save file

**Save File Structure:**
```json
{
  "id": "game_session_xyz123",
  "timestamp": 1705000000000,
  "version": "0.1.0",
  "currentLocation": "Harbor",
  "player": { ... },
  "npcs": { ... },
  "activeCombat": null,
  "combatHistory": [],
  "conversationLog": [],
  "questFlags": {},
  "relationshipFlags": {}
}
```

### Goal 6: Express.js REST API Structure

Create REST endpoints for testing and future React integration:

```
POST   /api/game/new          - Initialize new game
GET    /api/game/state        - Get current game state
POST   /api/game/save/:slot   - Save to slot
GET    /api/game/load/:slot   - Load from slot
GET    /api/game/saves        - List all saves

GET    /api/npc/:id           - Get NPC data
GET    /api/dialogue/:npcId   - Get current dialogue node

POST   /api/dialogue/choose   - Player selects dialogue option
                               Body: { optionId, npcId }
                               Returns: Updated game state

GET    /api/player/personality - Get player personality
POST   /api/player/team       - Set team composition
                               Body: { npcIds: [id1, id2, id3] }

(Combat endpoints for later sprints)
```

---

## Starting Point: Example Data

### Test Player Character
```typescript
const playerCharacter: PlayerCharacter = {
  id: "player_1",
  name: "Kael",
  title: "Seeker",
  currentRank: 1.0,
  stamina: { current: 100, max: 100 },
  power: 15,
  speed: 12,
  
  personality: {
    patience: 18,
    empathy: 16,
    cunning: 12,
    logic: 18,
    kindness: 18,
    charisma: 18
  },
  
  coreTrainingPoints: 0,
  reactionSkills: { block: 0, dodge: 0, parry: 0 },
  
  elementalPath: "Light",
  pathSegments: { current: 0, max: 35 },
  pathAscensionLevel: 0,
  
  team: ["npc_scout_elena", "npc_merchant_lars"]
}
```

### Test NPCs (3-4 examples)
Already defined above in Goal 3.

---

## Implementation Checklist

### Phase 1: Core Structure (Priority)
- [ ] Create TypeScript interfaces for GameState, PlayerCharacter, NPC, DialogueOption
- [ ] Implement personality adjustment logic with redistribution
- [ ] Create initial test game state with player + 3 NPCs
- [ ] Implement immutable state update patterns (object spread, new arrays)

### Phase 2: Persistence (Priority)
- [ ] Create `saves/` directory structure
- [ ] Implement `saveGame()` function
- [ ] Implement `loadGame()` function
- [ ] Implement `listSaves()` function

### Phase 3: Dialogue System (Priority)
- [ ] Create dialogue tree structure with personality gates
- [ ] Implement dialogue option filtering based on player personality
- [ ] Implement dialogue choice handler (updates personality, advances to next node)
- [ ] Create example dialogue trees for 2-3 NPCs

### Phase 4: API Endpoints (Testing)
- [ ] Initialize Express.js with basic middleware
- [ ] Create `/api/game/new` endpoint
- [ ] Create `/api/game/state` endpoint
- [ ] Create `/api/game/save/:slot` and `/api/game/load/:slot`
- [ ] Create `/api/dialogue/*` endpoints
- [ ] Add error handling and logging

---

## Combat System Context (Reference)

The combat system is complex and sophisticated. For Sprint 1, you don't need to implement it, but understand its structure for future integration:

- **3v3 Turn-Based:** 3 player combatants vs 3 AI combatants
- **State Management:** Combat state tracks active combatants, stamina, energy segments, buffs/debuffs
- **Round Structure:** 5 phases (AI Decision, Visual Info, Player Declaration, Resolution, Per-Attack Resolution)
- **Key Mechanics:** Rank KO, Blindside, Crushing Blow, Parry/Counter chains, Personality paths with ascension

Combat will be integrated in Sprint 3-4. Reference documentation: `GM_Combat_Tracker_Documentation.docx`

---

## Key Constraints & Design Notes

1. **Immutability:** Never mutate objects. Always create new objects when state changes.
2. **No Dead Ends:** Every dialogue option must lead somewhere. NPCs never close off content based on personality.
3. **Personality Redistribution:** Maintain sum = 100% at all times. No traits below 5% or above 35%.
4. **NPC Archetypes:** NPCs don't change personality based on player choices. Only player personality changes.
5. **Deterministic:** All systems must produce same output given same input (for save/load reliability).
6. **Modular:** Narrative system must work independently of combat system.

---

## Deliverables for Sprint 1

1. **Backend Repository Structure:**
   - `src/types/` - TypeScript interfaces
   - `src/state/` - State management and immutability helpers
   - `src/dialogue/` - Dialogue system implementation
   - `src/persistence/` - Save/load logic
   - `src/api/` - Express.js endpoints
   - `saves/` - Directory for save files

2. **Functional Implementation:**
   - Personality adjustment with redistribution
   - Dialogue tree traversal with personality gates
   - Complete save/load cycle (save → load → verify state identical)
   - Working REST API for testing

3. **Test Data:**
   - Player character with realistic stats
   - 3-4 fully-featured NPCs with dialogue trees
   - Example dialogue demonstrating personality-gating

4. **Documentation:**
   - API endpoint reference (what each endpoint does, example requests/responses)
   - Personality system explanation
   - How to extend with new NPCs/dialogue

---

## Questions for Clarification (Optional)

Before starting, confirm:
1. Should dialogue choices also affect NPC affection/trust values?
2. For the React frontend (future sprint), should API return structured dialogue options or raw dialogue text?
3. Should save files include player's name as metadata for display in save menu?
4. Are there specific quest flags we should seed in example data, or keep it empty for now?

---

## Notes

- This sprint is **narrative/state focused**, not combat-focused
- Combat system integration happens in Sprint 3-4
- Think of this as the "scaffolding" for the entire game
- Save/load must be rock-solid—prototype longevity depends on it
- Personality system is core to narrative—get this right early
