# Sprint 1 Technical Reference: Personality & Dialogue Implementation

## Personality System Deep Dive

### Core Mechanics

**Traits (6 total, always sum to 100%):**
- Patience (5-35%) - Wisdom category
- Empathy (5-35%) - Wisdom category
- Cunning (5-35%) - Intelligence category
- Logic (5-35%) - Intelligence category
- Kindness (5-35%) - Charisma category
- Charisma (5-35%) - Charisma category

**Adjustment Rules:**
1. Player makes dialogue choice
2. Choice includes personality adjustment (e.g., `{cunning: +6}`)
3. Target trait increases by adjustment amount
4. If target trait would exceed 35%, cap at 35%
5. Redistribute remaining adjustment across other traits proportionally
6. Ensure final sum = 100%

### Personality Adjustment Algorithm

```typescript
function adjustPersonality(
  currentPersonality: Personality,
  adjustments: Partial<Personality>
): Personality {
  // Step 1: Apply direct adjustments
  let newPersonality = { ...currentPersonality };
  const adjustmentKeys = Object.keys(adjustments) as Array<keyof Personality>;
  
  for (const key of adjustmentKeys) {
    newPersonality[key] = Math.min(
      35,
      Math.max(5, newPersonality[key] + (adjustments[key] || 0))
    );
  }
  
  // Step 2: Normalize to sum = 100%
  const currentSum = Object.values(newPersonality).reduce((a, b) => a + b, 0);
  const difference = 100 - currentSum;
  
  if (difference !== 0) {
    const unadjustedKeys = Object.keys(newPersonality)
      .filter(k => !adjustmentKeys.includes(k as keyof Personality)) as Array<keyof Personality>;
    
    if (unadjustedKeys.length > 0) {
      // Distribute difference proportionally among unadjusted traits
      for (const key of unadjustedKeys) {
        const proportionalChange = (newPersonality[key] / 
          unadjustedKeys.reduce((sum, k) => sum + newPersonality[k], 0)) * difference;
        
        newPersonality[key] = Math.max(5, newPersonality[key] + proportionalChange);
      }
    } else {
      // All traits were adjusted - redistribute evenly
      const totalToDistribute = difference / adjustmentKeys.length;
      for (const key of adjustmentKeys) {
        newPersonality[key] = Math.max(5, Math.min(35, newPersonality[key] + totalToDistribute));
      }
    }
  }
  
  // Final normalization to ensure sum = 100%
  const finalSum = Object.values(newPersonality).reduce((a, b) => a + b, 0);
  const finalAdjustment = (100 - finalSum) / 6;
  
  for (const key in newPersonality) {
    newPersonality[key as keyof Personality] = 
      Math.max(5, Math.min(35, newPersonality[key as keyof Personality] + finalAdjustment));
  }
  
  return newPersonality;
}
```

### Example Personality Adjustments

**Scenario: Starting personality before dialogue**
```
Patience:  18%
Empathy:   16%
Cunning:   12%
Logic:     18%
Kindness:  18%
Charisma:  18%
Total:     100%
```

**Choice A: Aggressive confrontation → `{cunning: +6}`**
```
After adjustment:
Cunning:   18% (12 + 6)
Other traits reduced proportionally to rebalance

Result:
Patience:  17%
Empathy:   15%
Cunning:   18%
Logic:     17%
Kindness:  17%
Charisma:  16%
Total:     100%
```

**Choice B: Compassionate response → `{empathy: +6, kindness: +4}`**
```
After adjustment:
Empathy:   22% (16 + 6)
Kindness:  22% (18 + 4)
Cunning and Logic reduced proportionally

Result:
Patience:  18%
Empathy:   22%
Cunning:   10%
Logic:     16%
Kindness:  22%
Charisma:  12%
Total:     100%
```

---

## Personality Gate Implementation

### Gate Types

**1. Minimum Threshold Gate**
```typescript
interface MinimumGate {
  trait: keyof Personality;
  minimumValue: number; // 5-35
  operator: 'gte';
}

// Example: cunning >= 25
// Player can only see this option if cunning trait is 25% or higher
```

**2. Maximum Threshold Gate**
```typescript
interface MaximumGate {
  trait: keyof Personality;
  maximumValue: number; // 5-35
  operator: 'lte';
}

// Example: kindness <= 15
// Player can only see this option if kindness trait is 15% or lower
```

**3. Exact Value Gate**
```typescript
interface ExactGate {
  trait: keyof Personality;
  value: number;
  operator: 'eq';
}

// Example: patience == 20
// Player can only see this option if patience is exactly 20%
// (Least common, use sparingly)
```

### Gate Evaluation Logic

```typescript
function isDialogueOptionAvailable(
  option: DialogueOption,
  playerPersonality: Personality
): boolean {
  if (!option.personalityGate) {
    return true; // No gate = always available
  }
  
  const { trait, operator } = option.personalityGate;
  const traitValue = playerPersonality[trait];
  
  switch (operator) {
    case 'gte':
      return traitValue >= (option.personalityGate as MinimumGate).minimumValue;
    case 'lte':
      return traitValue <= (option.personalityGate as MaximumGate).maximumValue;
    case 'eq':
      return traitValue === (option.personalityGate as ExactGate).value;
    default:
      return false;
  }
}
```

### Design Principle: No Dead Ends

**WRONG:** Only high-cunning players can accept a quest
```typescript
Option A: "I'll help you steal the artifact" (cunning >= 25)
Option B: [None - quest locked for low-cunning players]
```

**RIGHT:** All players can accept the quest, different personality gates affect how
```typescript
Option A: "I'll help you steal the artifact" (cunning >= 25)
  → Same quest, player portrayed as enthusiastic thief
  
Option B: "I'll help, but I don't like it" (cunning <= 15)
  → Same quest, player portrayed as reluctant accomplice
  
Option C: "Maybe we can negotiate instead?" (no gate)
  → Same quest, player tries different approach (still fails/succeeds based on choices)
```

---

## Example NPC Dialogue Trees

### NPC 1: Elena (Loyal Scout, DEUS)

**Archetype Traits:**
- High empathy, kindness, patience
- Low cunning (honest, direct)
- DEUS faction (lawful, structured)

```typescript
const elenaDialogueTree: DialogueNode[] = [
  {
    id: "elena_greet",
    speakerId: "npc_scout_elena",
    text: "Hail, friend. I'm Elena, a scout for DEUS. We could use someone with your... determination.",
    options: [
      {
        id: "elena_opt_1",
        text: "Tell me about DEUS and what you do.",
        personalityGate: null, // Always available
        consequenceText: "Elena explains DEUS's mission to protect the innocent from rogue elements.",
        affectionChange: 0,
        trustChange: 5,
        nextNodeId: "elena_mission"
      },
      {
        id: "elena_opt_2",
        text: "I don't trust organizations. What's the catch?",
        personalityGate: { trait: 'cunning', minimumValue: 18, operator: 'gte' },
        consequenceText: "Elena nods thoughtfully. 'No catch—just honest work protecting people. Some distrust authority, and that's fair. But DEUS has no hidden agenda.'",
        personalityAdjustment: { cunning: -3, logic: 2 }, // Slight personality shift
        affectionChange: 2,
        trustChange: 8,
        nextNodeId: "elena_mission"
      },
      {
        id: "elena_opt_3",
        text: "DEUS sounds noble. I want to help.",
        personalityGate: { trait: 'kindness', minimumValue: 20, operator: 'gte' },
        consequenceText: "Elena's face brightens. 'I knew there was good in you. We need more people like you.'",
        personalityAdjustment: { kindness: 3, empathy: 2 },
        affectionChange: 5,
        trustChange: 10,
        nextNodeId: "elena_mission"
      }
    ]
  },
  {
    id: "elena_mission",
    speakerId: "npc_scout_elena",
    text: "Excellent. We have a problem in the Harbor district. Supplies meant for orphans are going missing. DEUS suspects rouge operatives are intercepting them.",
    options: [
      {
        id: "elena_mission_opt_1",
        text: "I'll help you investigate. The orphans need those supplies.",
        personalityGate: null,
        consequenceText: "Elena hands you a map marked with supply route checkpoints. 'Start at the warehouse at dusk. Be careful.'",
        personalityAdjustment: { empathy: 4, kindness: 2 },
        affectionChange: 3,
        trustChange: 8,
        nextNodeId: null // Quest accepted, conversation ends
      },
      {
        id: "elena_mission_opt_2",
        text: "Interesting business opportunity. What's my cut?",
        personalityGate: { trait: 'cunning', minimumValue: 22, operator: 'gte' },
        consequenceText: "Elena frowns slightly, but doesn't judge. 'DEUS pays fairly. 50 gold per confirmed rogue apprehended. Help us, and we'll pay you.'",
        personalityAdjustment: { cunning: 4, empathy: -2 },
        affectionChange: -2,
        trustChange: 4,
        nextNodeId: null
      },
      {
        id: "elena_mission_opt_3",
        text: "That sounds dangerous. Can I think about it?",
        personalityGate: { trait: 'patience', minimumValue: 18, operator: 'gte' },
        consequenceText: "Elena understands. 'Take your time. But the supplies are disappearing daily. We can't wait forever.'",
        personalityAdjustment: { patience: 2 },
        affectionChange: 1,
        trustChange: 3,
        nextNodeId: null // Quest available but not accepted yet
      }
    ]
  }
];
```

### NPC 2: Lars (Scheming Merchant, Neutral)

**Archetype Traits:**
- High cunning, logic
- Low empathy, kindness
- Neutral faction (profit-driven)

```typescript
const larsDialogueTree: DialogueNode[] = [
  {
    id: "lars_greet",
    speakerId: "npc_merchant_lars",
    text: "Well, well. A newcomer with potential. I'm Lars. I deal in... information and goods that others overlook.",
    options: [
      {
        id: "lars_opt_1",
        text: "What kind of goods?",
        personalityGate: null,
        consequenceText: "Lars leans in conspiratorially. 'Anything that pays well. Weapons, artifacts, forbidden texts. No morality attached to transactions, friend.'",
        affectionChange: 0,
        trustChange: -5, // He's suspicious of naive people
        nextNodeId: "lars_deal"
      },
      {
        id: "lars_opt_2",
        text: "You look like someone who knows how to get what they want.",
        personalityGate: { trait: 'cunning', minimumValue: 25, operator: 'gte' },
        consequenceText: "Lars grins. 'Now we're speaking the same language. I respect someone who doesn't waste time with pleasantries.'",
        personalityAdjustment: { cunning: 3 },
        affectionChange: 4,
        trustChange: 8,
        nextNodeId: "lars_deal"
      },
      {
        id: "lars_opt_3",
        text: "I'm not interested in black-market dealings.",
        personalityGate: { trait: 'kindness', minimumValue: 22, operator: 'gte' },
        consequenceText: "Lars shrugs indifferently. 'Your loss. But when you change your mind—and you will—know where to find me.'",
        personalityAdjustment: { kindness: 2, logic: -2 },
        affectionChange: -8,
        trustChange: -3,
        nextNodeId: null
      }
    ]
  },
  {
    id: "lars_deal",
    speakerId: "npc_merchant_lars",
    text: "I have a proposition for you. A shipment of rare components is passing through the warehouse district. The buyer has deep pockets. You deliver it, we split 30-70. I get the larger cut because I arrange everything.",
    options: [
      {
        id: "lars_deal_opt_1",
        text: "That's robbery. 30% is insultingly low.",
        personalityGate: { trait: 'logic', minimumValue: 20, operator: 'gte' },
        consequenceText: "Lars laughs. 'Fair point. How about 40-60? You do the dangerous work, I take the organization risk.'",
        personalityAdjustment: { logic: 3, cunning: 2 },
        affectionChange: 2,
        trustChange: 5,
        nextNodeId: "lars_negotiate"
      },
      {
        id: "lars_deal_opt_2",
        text: "I'll do it. 30% sounds reasonable.",
        personalityGate: { trait: 'cunning', minimumValue: 24, operator: 'gte' },
        consequenceText: "Lars nods approvingly. 'Excellent. You understand necessity. Come back when it's done.'",
        personalityAdjustment: { cunning: 4 },
        affectionChange: 3,
        trustChange: 10,
        nextNodeId: null
      },
      {
        id: "lars_deal_opt_3",
        text: "I can't help with smuggling. It's wrong.",
        personalityGate: { trait: 'kindness', minimumValue: 20, operator: 'gte' },
        consequenceText: "Lars doesn't look surprised. 'Morals. Understandable, if impractical. When idealism fails you, remember my offer.'",
        personalityAdjustment: { kindness: 3, empathy: 2 },
        affectionChange: -5,
        trustChange: 0,
        nextNodeId: null
      }
    ]
  },
  {
    id: "lars_negotiate",
    speakerId: "npc_merchant_lars",
    text: "45-55 is my final offer. Not a copper less. What's your answer?",
    options: [
      {
        id: "lars_negotiate_accept",
        text: "45-55 works. Let's do this.",
        personalityGate: null,
        consequenceText: "Lars extends his hand. 'Pleasure doing business with someone practical.'",
        affectionChange: 2,
        trustChange: 8,
        nextNodeId: null
      },
      {
        id: "lars_negotiate_refuse",
        text: "I've changed my mind. I'm not doing this.",
        personalityGate: null,
        consequenceText: "Lars doesn't seem bothered. 'Your choice. But you've marked yourself as unreliable now, friend. We're done here.'",
        personalityAdjustment: { kindness: 4 },
        affectionChange: -10,
        trustChange: -15,
        nextNodeId: null
      }
    ]
  }
];
```

### NPC 3: Kade (Rogue Outlaw, Rogues)

**Archetype Traits:**
- High charisma, cunning
- Moderate empathy (for friends)
- Low patience, kindness (chaotic)

```typescript
const kadeDialogueTree: DialogueNode[] = [
  {
    id: "kade_greet",
    speakerId: "npc_outlaw_kade",
    text: "Hey there, beautiful. Name's Kade. I heard there's a new player in town with... potential. Interested in running jobs with the Rogues?",
    options: [
      {
        id: "kade_opt_1",
        text: "Who are the Rogues, and what kind of jobs?",
        personalityGate: null,
        consequenceText: "Kade grins. 'We're the freedom fighters everyone pretends don't exist. We take from the rich, help the desperate, and have fun doing it. You in?'",
        affectionChange: 1,
        trustChange: 3,
        nextNodeId: "kade_jobs"
      },
      {
        id: "kade_opt_2",
        text: "I like your confidence. What's the first job?",
        personalityGate: { trait: 'charisma', minimumValue: 24, operator: 'gte' },
        consequenceText: "Kade laughs and claps you on the back. 'Ha! I knew I'd like you. We steal from a DEUS convoy tomorrow. Gold meant for soldiers, but it's really just extortion from farmers.'",
        personalityAdjustment: { charisma: 2, cunning: 3 },
        affectionChange: 5,
        trustChange: 10,
        nextNodeId: "kade_jobs"
      },
      {
        id: "kade_opt_3",
        text: "DEUS is trying to help people. I won't work against them.",
        personalityGate: { trait: 'kindness', minimumValue: 20, operator: 'gte' },
        consequenceText: "Kade's smile fades. 'Ah, a true believer. That's... unfortunate. But if you change your mind, the Rogues don't give up on talent.'",
        personalityAdjustment: { kindness: 3, empathy: 2 },
        affectionChange: -4,
        trustChange: 1,
        nextNodeId: null
      }
    ]
  },
  {
    id: "kade_jobs",
    speakerId: "npc_outlaw_kade",
    text: "First job: DEUS convoy hits the Trade Route at dawn. Gold shipment. We need someone fast and smart to intercept it. You handle the guards; I'll get the gold out.",
    options: [
      {
        id: "kade_jobs_opt_1",
        text: "I'm in. When do we move?",
        personalityGate: null,
        consequenceText: "Kade hands you a worn map. 'Meet me at the old bridge at sundown. Bring something sharp and wear dark clothes.'",
        affectionChange: 3,
        trustChange: 8,
        nextNodeId: null
      },
      {
        id: "kade_jobs_opt_2",
        text: "What if we get caught? What's the exit plan?",
        personalityGate: { trait: 'logic', minimumValue: 18, operator: 'gte' },
        consequenceText: "Kade nods approvingly. 'Smart. We have safe houses in the Underground. DEUS won't find us there. And if things go sideways, we scatter and regroup later.'",
        personalityAdjustment: { logic: 2, cunning: 2 },
        affectionChange: 4,
        trustChange: 12,
        nextNodeId: null
      },
      {
        id: "kade_jobs_opt_3",
        text: "Robbing a convoy is too risky. Find someone else.",
        personalityGate: { trait: 'patience', minimumValue: 20, operator: 'gte' },
        consequenceText: "Kade shrugs. 'Maybe you're right to be cautious. But we'll need that gold, and we won't forget your caution. Maybe next time.'",
        personalityAdjustment: { patience: 2 },
        affectionChange: 0,
        trustChange: 3,
        nextNodeId: null
      }
    ]
  }
];
```

---

## Dialogue System Architecture

### Conversation State Tracking

When player is in dialogue, track:
```typescript
interface ConversationState {
  currentNpcId: string
  currentNodeId: string
  conversationHistory: {
    nodeId: string
    speakerId: string
    playerChoice?: {
      optionId: string
      text: string
    }
    timestamp: number
  }[]
}
```

### Dialogue Flow

1. **Player initiates conversation** with NPC
2. **Load starting node** (usually `${npcId}_greet`)
3. **Display node text** and available options (filtered by personality gates)
4. **Player selects option**
5. **Apply changes:**
   - Update player personality (with redistribution)
   - Update NPC affection/trust
   - Record conversation history
6. **Advance to next node** (if exists) or end conversation
7. **Return to world state** with updated game state

### Data Structure

```typescript
interface ConversationEntry {
  timestamp: number
  npcId: string
  nodeId: string
  optionChosen: {
    id: string
    text: string
  }
  personalityBefore: Personality
  personalityAfter: Personality
  affectionChange: number
  trustChange: number
}
```

---

## Testing Checklist

### Personality System
- [ ] Adjustment correctly increases target trait (capped at 35%)
- [ ] Other traits decrease proportionally
- [ ] Final sum always equals 100%
- [ ] Multiple adjustments (e.g., +6 and +4) distribute correctly
- [ ] Minimum/maximum bounds (5-35%) are enforced

### Personality Gates
- [ ] Dialogue options correctly filtered by personality thresholds
- [ ] Options with no gate are always available
- [ ] Multiple dialogue options per node work correctly
- [ ] At least one option is always available (no dead ends)

### Dialogue Trees
- [ ] Each NPC has unique personality reflected in dialogue
- [ ] Dialogue options progress conversation logically
- [ ] `nextNodeId: null` properly ends conversation
- [ ] Affection/trust changes apply correctly

### Save/Load
- [ ] Personality redistributes identically after save/load
- [ ] NPC affection/trust values persist
- [ ] Conversation history preserved
- [ ] Game state is byte-for-byte identical after save/load cycle

### API Endpoints
- [ ] `/api/game/state` returns current state with proper structure
- [ ] `/api/dialogue/choose` updates personality and returns new state
- [ ] Error handling for invalid dialogue options
- [ ] Save/load endpoints work with multiple slots
