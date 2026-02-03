# Sprint 1: System Architecture & Data Flow Diagram

## Core System Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                     Game Backend Architecture                     │
│                                                                   │
│  ┌──────────────────────────────────────────────────────────┐  │
│  │                    EXPRESS.JS SERVER                      │  │
│  │                                                            │  │
│  │  ┌────────────────────────────────────────────────────┐  │  │
│  │  │              REST API Endpoints                     │  │  │
│  │  │                                                    │  │  │
│  │  │  /api/game/*     (new, state, save, load, list)   │  │  │
│  │  │  /api/npc/*      (get NPC data)                   │  │  │
│  │  │  /api/dialogue/* (get nodes, choose options)      │  │  │
│  │  │  /api/player/*   (personality, team mgmt)         │  │  │
│  │  └────────────────────────────────────────────────────┘  │  │
│  │                        ↑    ↑                              │  │
│  │  ┌─────────────────────────────────────────────────────┐ │  │
│  │  │              State Management Layer                 │ │  │
│  │  │                                                     │ │  │
│  │  │  ┌──────────────┐  ┌────────────┐  ┌────────────┐ │ │  │
│  │  │  │ Game State   │  │ Personality│  │  Dialogue  │ │ │  │
│  │  │  │  (Immutable) │  │   Engine   │  │   Engine   │ │ │  │
│  │  │  └──────────────┘  └────────────┘  └────────────┘ │ │  │
│  │  │       ↓                  ↓                ↓         │ │  │
│  │  │  ┌─────────────────────────────────────────────┐  │ │  │
│  │  │  │         State Update Functions              │  │ │  │
│  │  │  │  (Immutable Object Creation)                │  │ │  │
│  │  │  │  • adjustPersonality()                      │  │ │  │
│  │  │  │  • selectDialogueOption()                  │  │ │  │
│  │  │  │  • updateNPCRelationship()                 │  │ │  │
│  │  │  └─────────────────────────────────────────────┘  │ │  │
│  │  └─────────────────────────────────────────────────────┘ │  │
│  │                        ↓                                   │  │
│  │  ┌─────────────────────────────────────────────────────┐ │  │
│  │  │          Persistence Layer (JSON Files)             │ │  │
│  │  │                                                     │ │  │
│  │  │  saveGame(gameState, slot) → saves/slot_N.json    │ │  │
│  │  │  loadGame(slot) → GameState                       │ │  │
│  │  │  listSaves() → Save Metadata                      │ │  │
│  │  └─────────────────────────────────────────────────────┘ │  │
│  └──────────────────────────────────────────────────────────┘  │
│                                                                   │
└─────────────────────────────────────────────────────────────────┘
                              ↓
                   ┌──────────────────┐
                   │   saves/ (JSON)  │
                   │  slot_1.json     │
                   │  slot_2.json     │
                   │  ...slot_10.json │
                   └──────────────────┘
```

---

## Game State Structure (Immutable Tree)

```
GameState
├── Metadata
│   ├── id: string
│   ├── timestamp: number
│   └── version: string
│
├── World
│   ├── currentLocation: string
│   ├── questFlags: Record<string, boolean>
│   ├── relationshipFlags: Record<string, boolean>
│   └── conversationLog: ConversationEntry[]
│
├── Player
│   ├── id, name, title
│   ├── Stats (stamina, power, speed)
│   ├── Personality ⭐ (Mutable via dialogue)
│   │   ├── patience: 5-35%
│   │   ├── empathy: 5-35%
│   │   ├── cunning: 5-35%
│   │   ├── logic: 5-35%
│   │   ├── kindness: 5-35%
│   │   └── charisma: 5-35%
│   │       └─ Sum = 100% (always)
│   ├── Progression (rank, XP, skills)
│   ├── Elemental Path (type, segments, ascension)
│   └── Team (array of NPC IDs)
│
├── NPCs (Record)
│   ├── npc_scout_elena
│   │   ├── Fixed Data
│   │   │   ├── name, archetype, faction
│   │   │   └── basePersonality (never changes)
│   │   ├── Mutable Relationship Data
│   │   │   ├── affection: -100 to +100
│   │   │   └── trust: -100 to +100
│   │   ├── Dialogue Tree
│   │   │   ├── elena_greet (node)
│   │   │   │   └── options[3] (with personality gates)
│   │   │   └── elena_mission (node)
│   │   │       └── options[3]
│   │   └── Availability (locations, quests, joinable)
│   │
│   ├── npc_merchant_lars
│   │   └── ... (same structure)
│   │
│   └── npc_outlaw_kade
│       └── ... (same structure)
│
├── Active Combat (null during exploration)
│   └── CombatState (Sprint 3-4)
│
└── Combat History
    └── CompletedCombat[]
```

---

## Dialogue System Data Flow

```
┌─────────────────────────────────────────────────────────────┐
│                    Dialogue Interaction Flow                 │
└─────────────────────────────────────────────────────────────┘

1. INITIATE CONVERSATION
   User: "Click on Elena"
   ↓
   API: GET /api/dialogue/npc_scout_elena
   ↓
   System: Load NPC dialogue tree, get starting node (elena_greet)
   ↓
   Return: Node with 3 options, 1-2 are personality-gated

2. EVALUATE PERSONALITY GATES
   ┌─────────────────────────────────────────┐
   │ Player Personality:                     │
   │  cunning: 12% (below 18% gate)          │
   └─────────────────────────────────────────┘
   
   Option A: "Tell me about DEUS"
   Gate: null ✓ Available
   
   Option B: "What's the catch?"
   Gate: cunning >= 18 ✗ Blocked (player has 12%)
   
   Option C: "DEUS sounds noble"
   Gate: kindness >= 20 ✗ Blocked (player has 16%)

3. DISPLAY AVAILABLE OPTIONS
   User sees Options A and C only

4. PLAYER SELECTS OPTION
   User: Select Option A
   ↓
   API: POST /api/dialogue/choose
   Body: {
     npcId: "npc_scout_elena",
     optionId: "elena_opt_1",
     currentNodeId: "elena_greet"
   }

5. PROCESS DIALOGUE CHOICE
   
   a) Apply Personality Adjustment
      Adjustment: { empathy: +4, kindness: +2 }
      
      Before: {patience:18, empathy:16, cunning:12, logic:18, kindness:18, charisma:18}
      
      Direct apply:
        empathy:  16 + 4 = 20
        kindness: 18 + 2 = 20
        Total so far: 38 (need to reduce others to hit 100)
      
      Redistribute:
        - Other traits reduced proportionally: -3%, -3%, -1%, -1%
        - Ensure all remain in 5-35% range
      
      After: {patience:17, empathy:20, cunning:12, logic:17, kindness:20, charisma:14}
      
      ✓ Verify: Sum = 100%

   b) Update NPC Relationship
      affectionChange: 0 (no change)
      trustChange: +5
      → elena.trust: 0 + 5 = 5

   c) Record Conversation Entry
      Add to conversationLog:
      {
        timestamp: 1705000000000,
        npcId: "npc_scout_elena",
        nodeId: "elena_greet",
        optionChosen: { id: "elena_opt_1", text: "Tell me..." },
        personalityBefore: { ... },
        personalityAfter: { ... },
        affectionChange: 0,
        trustChange: 5
      }

   d) Advance to Next Node
      nextNodeId: "elena_mission"
      Load node and re-evaluate gates for new options

6. RETURN RESPONSE
   API returns:
   {
     success: true,
     personalityBefore: { ... },
     personalityAfter: { ... },
     consequenceText: "Elena explains DEUS's mission...",
     npcAffectionChange: 0,
     npcTrustChange: 5,
     nextNode: {
       nodeId: "elena_mission",
       text: "Excellent. We have a problem...",
       options: [ ... ]  // New options with re-evaluated gates
     },
     gameState: { ... }  // Full updated state
   }

7. UPDATE UI
   Display consequence text and next node
   User can choose next option
   (Or escape conversation and game state remains updated)
```

---

## Personality System Mechanics

```
┌──────────────────────────────────────────────────────────────┐
│         Personality Adjustment with Redistribution            │
└──────────────────────────────────────────────────────────────┘

Starting Personality:
  P: 18%  E: 16%  C: 12%  L: 18%  K: 18%  Ch: 18%  = 100%

Dialogue Choice Adjustment: { cunning: +6 }

Step 1: Apply to Target Trait (Capped at 35%)
  cunning: 12 + 6 = 18% ✓ (within 5-35%)

Step 2: Calculate Current Sum
  18 + 16 + 18 + 18 + 18 + 18 = 106%
  Overage: 6%

Step 3: Redistribute Among Unadjusted Traits
  Unadjusted traits: P, E, L, K, Ch (5 traits)
  
  Distribute -6% proportionally:
    P: (18 / 92) × -6 = -1.17% ≈ -1%
    E: (16 / 92) × -6 = -1.04% ≈ -1%
    L: (18 / 92) × -6 = -1.17% ≈ -1%
    K: (18 / 92) × -6 = -1.17% ≈ -1%
    Ch: (18 / 92) × -6 = -1.17% ≈ -1%
    Subtotal: -5% (need -6%)

Step 4: Apply Bounds (5-35%) and Rebalance
  After redistribution:
    P: 18 - 1 = 17%
    E: 16 - 1 = 15%
    C: 12 + 6 = 18%
    L: 18 - 1 = 17%
    K: 18 - 1 = 17%
    Ch: 18 - 1 = 17%
    Sum: 101% (still off by 1%)

Step 5: Final Normalization
  Divide remainder evenly across all traits:
    Difference: 100 - 101 = -1%
    Per trait: -1% / 6 = -0.167%
    
  Final result:
    P: 17 - 0.167 = 16.833% ≈ 16.8%
    E: 15 - 0.167 = 14.833% ≈ 14.8%
    C: 18 - 0.167 = 17.833% ≈ 17.8%
    L: 17 - 0.167 = 16.833% ≈ 16.8%
    K: 17 - 0.167 = 16.833% ≈ 16.8%
    Ch: 17 - 0.167 = 16.833% ≈ 16.8%
    Sum: 100.0% ✓

Result After Adjustment:
  P: 16.8%  E: 14.8%  C: 17.8%  L: 16.8%  K: 16.8%  Ch: 16.8%
  (Visually: Cunning increased, others decreased slightly)

Gates Now Available:
  • cunning >= 18: BLOCKED (has 17.8%)
  • cunning >= 17: AVAILABLE
  • cunning <= 20: AVAILABLE
  • kindness >= 20: BLOCKED (has 16.8%)
```

---

## Save/Load Cycle

```
┌─────────────────────────────────────────────────────────────┐
│                   Save/Load File Structure                   │
└─────────────────────────────────────────────────────────────┘

DIRECTORY: saves/
├── slot_1.json  (160KB avg)
├── slot_2.json
├── slot_3.json
└── slot_10.json

FILE: saves/slot_1.json
┌─────────────────────────────────────────┐
│ {                                       │
│   "id": "game_xyz_123",                 │
│   "timestamp": 1705000000000,           │
│   "version": "0.1.0",                   │
│   "currentLocation": "Harbor",          │
│   "player": {                           │
│     "name": "Kael",                     │
│     "personality": {                    │
│       "patience": 16.8,                 │
│       "empathy": 14.8,                  │
│       ...                               │
│     },                                  │
│     "team": [                           │
│       "npc_scout_elena",                │
│       "npc_outlaw_kade"                 │
│     ]                                   │
│   },                                    │
│   "npcs": {                             │
│     "npc_scout_elena": {                │
│       "affection": 0,                   │
│       "trust": 5                        │
│     },                                  │
│     ...                                 │
│   },                                    │
│   "conversationLog": [ ... ],           │
│   "activeCombat": null,                 │
│   "combatHistory": [],                  │
│   "questFlags": {},                     │
│   "relationshipFlags": {}               │
│ }                                       │
└─────────────────────────────────────────┘

SAVE WORKFLOW:
  1. POST /api/game/save/1
  2. System serializes full gameState to JSON
  3. Writes to saves/slot_1.json
  4. Returns metadata (player name, location, playtime)

LOAD WORKFLOW:
  1. GET /api/game/load/1
  2. System reads saves/slot_1.json
  3. Parses JSON back to GameState object
  4. Validates all constraints (personality sum, bounds, etc.)
  5. Returns full gameState
  6. Player can continue exactly where they left off

VERIFICATION (Post-Load):
  ✓ Player name matches save
  ✓ Personality sum = 100%
  ✓ All traits 5-35%
  ✓ NPC affection/trust values preserved
  ✓ Conversation history intact
  ✓ Team composition valid
```

---

## API Request/Response Examples

```
┌─────────────────────────────────────────────────────────────┐
│              Example: Dialogue Choice Flow                   │
└─────────────────────────────────────────────────────────────┘

REQUEST: POST /api/dialogue/choose
Content-Type: application/json

{
  "npcId": "npc_scout_elena",
  "optionId": "elena_opt_1",
  "currentNodeId": "elena_greet"
}

RESPONSE: 200 OK
Content-Type: application/json

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
    "text": "Excellent. We have a problem in the Harbor district. Supplies meant for orphans are going missing...",
    "options": [
      {
        "id": "elena_mission_opt_1",
        "text": "I'll help you investigate. The orphans need those supplies.",
        "available": true,
        "personalityGate": null
      },
      {
        "id": "elena_mission_opt_2",
        "text": "Interesting business opportunity. What's my cut?",
        "available": true,
        "personalityGate": {
          "trait": "cunning",
          "operator": "gte",
          "value": 22
        }
      },
      {
        "id": "elena_mission_opt_3",
        "text": "That sounds dangerous. Can I think about it?",
        "available": true,
        "personalityGate": {
          "trait": "patience",
          "operator": "gte",
          "value": 18
        }
      }
    ]
  },
  "gameState": {
    // Full updated GameState
  }
}
```

---

## NPC Personality Archetype System

```
┌──────────────────────────────────────────────────────────┐
│              NPC Personality Archetypes                  │
└──────────────────────────────────────────────────────────┘

ELENA: Loyal Scout (DEUS)
┌────────────────────────────────────────┐
│ Archetype Profile:                     │
│  - High empathy (20%) → compassionate  │
│  - High kindness (20%) → selfless      │
│  - High patience (20%) → tolerant      │
│  - Low cunning (10%) → direct/honest   │
│  - Moderate logic (15%) → practical    │
│  - Moderate charisma (15%) → friendly  │
│                                        │
│ Dialogue Approach:                     │
│  - Appeals to morality and helping     │
│  - Offers quests about helping people │
│  - Respects kindness, skeptical of    │
│    cunning players                     │
│  - Builds trust through honesty        │
└────────────────────────────────────────┘

LARS: Scheming Merchant (Neutral)
┌────────────────────────────────────────┐
│ Archetype Profile:                     │
│  - High cunning (28%) → manipulative   │
│  - High logic (25%) → analytical       │
│  - Low empathy (8%) → self-interested  │
│  - Low kindness (12%) → pragmatic      │
│  - Moderate patience (10%) → impatient │
│  - Moderate charisma (17%) → charming  │
│                                        │
│ Dialogue Approach:                     │
│  - Appeals to profit and deals         │
│  - Offers quests about smuggling       │
│  - Respects cunning, distrusts naïveté │
│  - Builds trust through mutual benefit │
└────────────────────────────────────────┘

KADE: Rogue Outlaw (Rogues)
┌────────────────────────────────────────┐
│ Archetype Profile:                     │
│  - High charisma (27%) → charismatic   │
│  - High cunning (25%) → sneaky         │
│  - Moderate empathy (8%) → for allies  │
│  - Low patience (12%) → impulsive      │
│  - Low kindness (10%) → self-serving   │
│  - Moderate logic (18%) → tactical     │
│                                        │
│ Dialogue Approach:                     │
│  - Appeals to freedom and rebellion    │
│  - Offers quests about heists/thefts   │
│  - Respects charisma, dismisses morals │
│  - Builds trust through action/style   │
└────────────────────────────────────────┘

PERSONALITY GATEKEEPING:
  When Player chooses dialogue option aligned with NPC archetype,
  they often unlock additional options or improve relationships.
  
  Example: Charismatic player + Kade = +5 affection
  Example: Cunning player + Elena = trust stays neutral
  (Elena doesn't dislike cunning; she just doesn't rely on it)
```

---

## Immutability Pattern (Code Pattern Reference)

```javascript
// WRONG: Mutating existing object
function updateNPC(npc, affectionChange) {
  npc.affection += affectionChange;  // ❌ MUTATES
  return npc;
}

// RIGHT: Creating new object
function updateNPC(npc, affectionChange) {
  return {
    ...npc,  // Copy all properties
    affection: npc.affection + affectionChange  // Override one property
  };
}

// Updating nested object (game state)
function selectDialogueOption(gameState, npcId, affectionChange) {
  return {
    ...gameState,  // Copy root
    npcs: {
      ...gameState.npcs,  // Copy npcs object
      [npcId]: {
        ...gameState.npcs[npcId],  // Copy specific NPC
        affection: gameState.npcs[npcId].affection + affectionChange
      }
    },
    timestamp: Date.now()  // Update timestamp
  };
}

// Updating personality with redistribution
function adjustPersonalityAndRedistribute(currentPersonality, adjustments) {
  let newPers = { ...currentPersonality };
  
  // Apply adjustments and redistribute...
  // (See Technical Reference for full algorithm)
  
  return newPers;  // Return new object, never mutate
}
```

---

## Error Handling Flow

```
┌──────────────────────────────────────────────────────┐
│           Error Handling During Dialogue             │
└──────────────────────────────────────────────────────┘

REQUEST: POST /api/dialogue/choose
{ npcId: "elena", optionId: "elena_opt_2", ... }

↓

VALIDATION CHECK 1: NPC Exists?
  ✓ Yes, found "npc_scout_elena"

↓

VALIDATION CHECK 2: Option Exists?
  ✓ Yes, "elena_opt_2" in elena_greet node

↓

VALIDATION CHECK 3: Personality Gate Met?
  Gate: cunning >= 25
  Player has: cunning = 12%
  ✗ NO! Gate failed

↓

ERROR RESPONSE: 400 Bad Request

{
  "success": false,
  "error": {
    "code": "DIALOGUE_GATE_FAILED",
    "message": "Player personality does not meet gate requirement",
    "details": {
      "gate": "cunning >= 25",
      "playerValue": 12,
      "required": 25,
      "shortfall": 13
    }
  }
}

↓

CLIENT ACTION: 
  - Show message to player
  - Keep showing available options
  - Suggest which personality traits to develop
  - Allow player to choose different option
```

---

## Development Checklist by System

```
PERSONALITY SYSTEM
  [ ] Trait initialization (5-35% range, sum = 100%)
  [ ] Adjustment function with redistribution
  [ ] Bounds enforcement (5-35%)
  [ ] Sum validation (floating-point tolerance)
  [ ] Multiple adjustment handling
  [ ] Validation endpoint

DIALOGUE SYSTEM
  [ ] Dialogue tree structure for 3+ NPCs
  [ ] Personality gate evaluation
  [ ] Option filtering (only show available)
  [ ] Node traversal (next_node_id)
  [ ] Conversation history recording
  [ ] Consequence text display

NPC SYSTEM
  [ ] NPC data structure (fixed archetype)
  [ ] Affection/trust tracking
  [ ] Dialogue tree loading
  [ ] Personality gate integration
  [ ] Relationship updates after choices

GAME STATE
  [ ] Immutable state creation
  [ ] No mutations in state updates
  [ ] Timestamp tracking
  [ ] Location tracking
  [ ] History/log management

PERSISTENCE
  [ ] Save to JSON file (slot 1-10)
  [ ] Load from JSON file
  [ ] Validate on load
  [ ] List saves metadata
  [ ] Delete saves

API
  [ ] Game endpoints (new, state, save, load, list)
  [ ] NPC endpoints (get)
  [ ] Dialogue endpoints (get node, choose option)
  [ ] Player endpoints (personality, team)
  [ ] Error handling
  [ ] Input validation
```

---

Good luck with implementation! The architecture is solid—just follow the patterns, respect immutability, and keep personality sum validation tight. 🎮
