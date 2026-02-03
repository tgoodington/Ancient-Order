# Sprint 1: API Reference & TypeScript Interfaces

## Complete TypeScript Interface Definitions

### Core Game State

```typescript
// Game State (root object)
interface GameState {
  // Metadata
  id: string // UUID or similar
  timestamp: number // Unix timestamp of last update
  version: string // "0.1.0" format
  
  // World State
  currentLocation: string // "Harbor", "Market", "Tavern", etc.
  conversationLog: ConversationEntry[]
  
  // Player
  player: PlayerCharacter
  
  // NPCs (keyed by NPC ID)
  npcs: Record<string, NPC>
  
  // Active Combat (null when not in combat)
  activeCombat: CombatState | null
  
  // Combat History (for stats/progression)
  combatHistory: CompletedCombat[]
  
  // World Flags (for quest/narrative tracking)
  questFlags: Record<string, boolean>
  relationshipFlags: Record<string, boolean>
}

// Player Character
interface PlayerCharacter {
  id: string
  name: string
  title: string // "Seeker", "Wanderer", etc.
  currentRank: number // 1.0 to 10.0 (decimal for degree)
  
  // Base Stats
  stamina: {
    current: number
    max: number
  }
  power: number
  speed: number
  
  // Personality (6 traits, 5-35% range, sum = 100%)
  personality: {
    patience: number
    empathy: number
    cunning: number
    logic: number
    kindness: number
    charisma: number
  }
  
  // Progression
  coreTrainingPoints: number
  reactionSkills: {
    block: number // 0-11 ranks
    dodge: number // 0-11 ranks
    parry: number // 0-11 ranks
  }
  
  // Elemental Path
  elementalPath: 'Fire' | 'Water' | 'Air' | 'Earth' | 'Shadow' | 'Light'
  pathSegments: {
    current: number
    max: number
  }
  pathAscensionLevel: number // 0-3
  
  // Party/Team
  team: string[] // NPC IDs of party members (up to 2 companions)
}

// NPC (Fixed Archetype)
interface NPC {
  id: string
  name: string
  archetype: string // "Loyal Scout", "Scheming Merchant", etc.
  faction: 'DEUS' | 'Rogues' | 'Neutral'
  
  // Base personality (NEVER CHANGES)
  basePersonality: {
    patience: number
    empathy: number
    cunning: number
    logic: number
    kindness: number
    charisma: number
  }
  
  // Relationship tracking (changes with player choices)
  affection: number // -100 to +100
  trust: number // -100 to +100
  
  // Availability
  joinableInTeam: boolean
  availableLocations: string[]
  questsAvailable: string[]
  
  // Dialogue
  dialogueTree: DialogueNode[]
}

// Dialogue Node
interface DialogueNode {
  id: string
  speakerId: string // NPC id or "player"
  text: string
  options: DialogueOption[]
}

// Dialogue Option
interface DialogueOption {
  id: string
  text: string
  
  // Personality Gate (optional)
  personalityGate: {
    trait: keyof PlayerCharacter['personality']
    operator: 'gte' | 'lte' | 'eq'
    value: number
  } | null
  
  // Effect on player
  personalityAdjustment: Partial<PlayerCharacter['personality']>
  consequenceText: string
  
  // Effect on NPC
  affectionChange?: number
  trustChange?: number
  
  // Progression
  nextNodeId: string | null // null = end conversation
}

// Conversation Entry (history)
interface ConversationEntry {
  timestamp: number
  npcId: string
  nodeId: string
  optionChosen: {
    id: string
    text: string
  }
  personalityBefore: PlayerCharacter['personality']
  personalityAfter: PlayerCharacter['personality']
  affectionChange: number
  trustChange: number
}

// Combat State (future sprint, included for reference)
interface CombatState {
  id: string
  round: number
  playerTeamIds: string[]
  enemyTeamIds: string[]
  playerTeam: CombatCombatant[]
  enemyTeam: CombatCombatant[]
  combatHistory: CombatAction[]
  // ... other combat fields
}

// Completed Combat (history entry)
interface CompletedCombat {
  id: string
  timestamp: number
  location: string
  playerTeamIds: string[]
  enemyTeamIds: string[]
  result: 'victory' | 'defeat'
  xpGained: number
  reward: any // gold, items, etc.
}

// Personality type alias
type Personality = PlayerCharacter['personality'];
type PersonalityKey = keyof Personality;
```

---

## REST API Endpoints

### Game Management

#### POST /api/game/new
**Initialize a new game**

**Request:**
```json
{
  "playerName": "Kael",
  "difficulty": "normal"
}
```

**Response:**
```json
{
  "success": true,
  "gameState": {
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
}
```

**Status Codes:**
- 200: Game created successfully
- 400: Invalid player name or parameters

---

#### GET /api/game/state
**Get current game state**

**Response:**
```json
{
  "success": true,
  "gameState": { ... }
}
```

**Status Codes:**
- 200: State returned
- 404: No active game loaded

---

#### POST /api/game/save/:slot
**Save game to slot (1-10)**

**Request Body:**
```json
{
  "slotName": "After Harbor Quest" // optional
}
```

**Response:**
```json
{
  "success": true,
  "slot": 1,
  "savedAt": "2024-01-12T15:30:00Z",
  "playerName": "Kael",
  "location": "Market",
  "playtime": "2:45"
}
```

**Status Codes:**
- 200: Game saved
- 400: Invalid slot number (1-10)
- 409: Slot already occupied (overwrite warning)

---

#### GET /api/game/load/:slot
**Load game from slot**

**Response:**
```json
{
  "success": true,
  "gameState": { ... }
}
```

**Status Codes:**
- 200: Game loaded
- 404: Slot not found

---

#### GET /api/game/saves
**List all saved games**

**Response:**
```json
{
  "success": true,
  "saves": [
    {
      "slot": 1,
      "slotName": "After Harbor Quest",
      "playerName": "Kael",
      "location": "Market",
      "playtime": "2:45",
      "savedAt": "2024-01-12T15:30:00Z"
    },
    {
      "slot": 2,
      "slotName": null,
      "playerName": "Aria",
      "location": "Underground",
      "playtime": "4:12",
      "savedAt": "2024-01-11T20:15:00Z"
    }
  ]
}
```

**Status Codes:**
- 200: Returns array of saves (may be empty)

---

#### DELETE /api/game/saves/:slot
**Delete saved game**

**Response:**
```json
{
  "success": true,
  "message": "Save slot 1 deleted"
}
```

**Status Codes:**
- 200: Save deleted
- 404: Slot not found

---

### NPC & Dialogue Management

#### GET /api/npc/:npcId
**Get NPC data**

**Response:**
```json
{
  "success": true,
  "npc": {
    "id": "npc_scout_elena",
    "name": "Elena",
    "archetype": "Loyal Scout",
    "faction": "DEUS",
    "basePersonality": { ... },
    "affection": 0,
    "trust": 5,
    "joinableInTeam": true,
    "availableLocations": ["Harbor", "Market", "Tavern"],
    "questsAvailable": ["escort_to_tower", "gather_intel"]
  }
}
```

**Status Codes:**
- 200: NPC found
- 404: NPC not found

---

#### GET /api/dialogue/:npcId
**Get current dialogue node for NPC**

**Query Params:**
- `nodeId` (optional): Specific node to fetch. If not provided, starts with `${npcId}_greet`

**Response:**
```json
{
  "success": true,
  "dialogue": {
    "nodeId": "elena_greet",
    "speakerId": "npc_scout_elena",
    "text": "Hail, friend. I'm Elena, a scout for DEUS. We could use someone with your... determination.",
    "options": [
      {
        "id": "elena_opt_1",
        "text": "Tell me about DEUS and what you do.",
        "available": true,
        "personalityGate": null
      },
      {
        "id": "elena_opt_2",
        "text": "I don't trust organizations. What's the catch?",
        "available": true,
        "personalityGate": {
          "trait": "cunning",
          "operator": "gte",
          "value": 18
        }
      },
      {
        "id": "elena_opt_3",
        "text": "DEUS sounds noble. I want to help.",
        "available": false,
        "personalityGate": {
          "trait": "kindness",
          "operator": "gte",
          "value": 20
        }
      }
    ]
  }
}
```

**Status Codes:**
- 200: Dialogue returned
- 404: NPC or node not found
- 400: Invalid nodeId format

---

#### POST /api/dialogue/choose
**Player selects a dialogue option**

**Request:**
```json
{
  "npcId": "npc_scout_elena",
  "optionId": "elena_opt_1",
  "currentNodeId": "elena_greet"
}
```

**Response:**
```json
{
  "success": true,
  "personalityBefore": {
    "patience": 18,
    "empathy": 16,
    "cunning": 12,
    "logic": 18,
    "kindness": 18,
    "charisma": 18
  },
  "personalityAfter": {
    "patience": 17,
    "empathy": 20,
    "cunning": 11,
    "logic": 17,
    "kindness": 18,
    "charisma": 17
  },
  "consequenceText": "Elena explains DEUS's mission to protect the innocent from rogue elements.",
  "npcAffectionChange": 0,
  "npcTrustChange": 5,
  "nextNode": {
    "nodeId": "elena_mission",
    "speakerId": "npc_scout_elena",
    "text": "Excellent. We have a problem in the Harbor district...",
    "options": [ ... ]
  },
  "gameState": { ... } // Full updated game state
}
```

**Status Codes:**
- 200: Choice processed successfully
- 400: Option not available (personality gate failed)
- 400: Invalid optionId
- 404: NPC or node not found
- 409: Player personality would violate constraints (should not happen)

---

### Player Management

#### GET /api/player
**Get player character data**

**Response:**
```json
{
  "success": true,
  "player": {
    "id": "player_1",
    "name": "Kael",
    "title": "Seeker",
    "currentRank": 1.0,
    "stamina": { "current": 100, "max": 100 },
    "power": 15,
    "speed": 12,
    "personality": { ... },
    "coreTrainingPoints": 0,
    "reactionSkills": { ... },
    "elementalPath": "Light",
    "pathSegments": { "current": 0, "max": 35 },
    "pathAscensionLevel": 0,
    "team": ["npc_scout_elena", "npc_merchant_lars"]
  }
}
```

**Status Codes:**
- 200: Player data returned

---

#### GET /api/player/personality
**Get player personality traits**

**Response:**
```json
{
  "success": true,
  "personality": {
    "patience": 18,
    "empathy": 16,
    "cunning": 12,
    "logic": 18,
    "kindness": 18,
    "charisma": 18
  },
  "categories": {
    "wisdom": { "patience": 18, "empathy": 16, "total": 34 },
    "intelligence": { "cunning": 12, "logic": 18, "total": 30 },
    "charisma": { "kindness": 18, "charisma": 18, "total": 36 }
  }
}
```

**Status Codes:**
- 200: Personality returned

---

#### POST /api/player/team
**Set player's team composition**

**Request:**
```json
{
  "npcIds": ["npc_scout_elena", "npc_outlaw_kade"]
}
```

**Response:**
```json
{
  "success": true,
  "team": [
    {
      "id": "npc_scout_elena",
      "name": "Elena",
      "archetype": "Loyal Scout"
    },
    {
      "id": "npc_outlaw_kade",
      "name": "Kade",
      "archetype": "Rogue Outlaw"
    }
  ],
  "gameState": { ... }
}
```

**Status Codes:**
- 200: Team updated
- 400: Invalid NPC IDs or NPCs not joinable
- 400: Too many party members (max 2 companions + player)

---

## Error Response Format

All endpoints follow this error format:

```json
{
  "success": false,
  "error": {
    "code": "DIALOGUE_GATE_FAILED",
    "message": "Player personality does not meet gate requirement (cunning >= 25)",
    "details": {
      "gate": "cunning >= 25",
      "playerValue": 18,
      "required": 25
    }
  }
}
```

**Common Error Codes:**
- `GAME_NOT_FOUND` - No game loaded in session
- `INVALID_SLOT` - Save slot number out of range (1-10)
- `SAVE_NOT_FOUND` - Requested save slot is empty
- `NPC_NOT_FOUND` - NPC ID doesn't exist
- `DIALOGUE_NODE_NOT_FOUND` - Node ID doesn't exist for NPC
- `DIALOGUE_OPTION_NOT_AVAILABLE` - Personality gate failed
- `INVALID_PERSONALITY_ADJUSTMENT` - Would violate constraints
- `TEAM_COMPOSITION_INVALID` - NPC not joinable or too many members

---

## Data Validation Rules

### Personality Adjustments
```typescript
// Rule 1: All traits must be 5-35%
// Rule 2: Sum of all traits must equal 100%
// Rule 3: Adjustments are never direct mutations—calculate redistribution

// Valid adjustment example:
{
  "cunning": 6  // Single trait increase
}

// Valid adjustment example:
{
  "empathy": 6,
  "kindness": 4  // Multiple traits (proper redistribution required)
}

// Implementation checks:
- Sum before: 100%
- Apply adjustment to target trait (capped 5-35%)
- Redistribute remainder proportionally among other traits
- Ensure final sum = 100% (within floating-point tolerance)
```

### Team Composition
```typescript
// Rules:
- Maximum 2 companions (player + 2 NPCs = 3-person team)
- All NPCs must have joinableInTeam: true
- Cannot add same NPC twice
- Cannot add player to their own team
```

### Save Slot Management
```typescript
// Rules:
- Slots numbered 1-10
- Each slot holds exactly one game state
- Overwriting a slot requires confirmation (return 409 first)
- Slots are independent (loading one doesn't affect others)
```

---

## Implementation Notes

### Session State
In Sprint 1, the backend should maintain **one active game state in memory** per session. In future sprints (with database), this will be replaced with persistent storage per user.

```typescript
// Current (Sprint 1):
let activeGameState: GameState | null = null;

// Usage:
app.post('/api/game/new', (req, res) => {
  activeGameState = createNewGameState(req.body);
  res.json({ success: true, gameState: activeGameState });
});
```

### Personality Validation
Every endpoint that modifies personality should validate:
```typescript
function validatePersonality(p: Personality): boolean {
  const sum = Object.values(p).reduce((a, b) => a + b, 0);
  const allValid = Object.values(p).every(v => v >= 5 && v <= 35);
  return allValid && Math.abs(sum - 100) < 0.01; // floating-point tolerance
}
```

### Immutability Pattern
```typescript
// WRONG (mutates existing object):
const updatedNPC = gameState.npcs[npcId];
updatedNPC.affection += 5;
gameState.npcs[npcId] = updatedNPC;

// RIGHT (creates new objects):
const updatedNPC = {
  ...gameState.npcs[npcId],
  affection: gameState.npcs[npcId].affection + 5
};
const updatedNpcs = {
  ...gameState.npcs,
  [npcId]: updatedNPC
};
const updatedGameState = {
  ...gameState,
  npcs: updatedNpcs,
  timestamp: Date.now()
};
```

---

## Testing Scenarios

### Scenario 1: Dialogue Choice with Personality Gate
1. POST `/api/game/new` with `playerName: "Kael"`
2. GET `/api/dialogue/npc_scout_elena` (starts at `elena_greet`)
3. Verify response includes 3 options with different gates
4. POST `/api/dialogue/choose` with `elena_opt_2` (cunning >= 18 gate)
5. Verify:
   - Response includes personality adjustment
   - `personalityAfter.cunning` changed correctly
   - Redistribution occurred (other traits adjusted)
   - Sum of personality = 100%
   - Next node is `elena_mission`

### Scenario 2: Save and Load
1. POST `/api/game/new`
2. Make several dialogue choices (personality changes)
3. POST `/api/game/save/1` with game state
4. POST `/api/game/new` (reset with different name)
5. GET `/api/game/load/1`
6. Verify loaded state matches saved state exactly
   - Player name same
   - Personality identical
   - NPC affection/trust preserved
   - Conversation history preserved

### Scenario 3: Personality Constraint Violation
1. POST `/api/game/new`
2. Manually craft request to adjust personality beyond bounds
3. Verify server returns 409 error
4. Verify game state unchanged

### Scenario 4: Dialogue Gate Filtering
1. POST `/api/game/new`
2. Manipulate player personality to low cunning (5%)
3. GET `/api/dialogue/npc_merchant_lars`
4. Verify `lars_opt_2` is unavailable (cunning >= 25 gate)
5. Verify other options are available
6. Set cunning to 26%
7. GET `/api/dialogue/npc_merchant_lars` again
8. Verify `lars_opt_2` is now available
