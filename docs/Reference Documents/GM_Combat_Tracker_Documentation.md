# GM Combat Tracker
## Complete System Documentation

---

## System Overview

This combat tracker is a sophisticated 3v3 turn-based combat system built for a single-player tabletop RPG. The system manages combat between 3 Player Character Combatants (PCcs) and 3 AI Combatants (AIcs), with deep mechanical systems for progression, elemental paths, and tactical decision-making.

---

## Sheet Architecture (17 Sheets)

| Sheet | Purpose |
|-------|---------|
| **Input** | Central hub - round state, action declarations, execution tracking, combat records |
| **Math** | All combat calculations - dynamic stats, damage resolution, defense thresholds |
| **Paths** | Elemental path system - 6 paths with bonuses, energy accumulation, ascension levels |
| **Character Sheet P1/P2/P3** | PCc persistent data - stats, reaction skills, path attunement, personality |
| **Battle Scenarios** | Pre-configured enemy teams for encounters with full stat blocks |
| **BuffsDebuffs** | Active status effects with duration tracking (Round Start to Round End) |
| **Rank Progression** | XP thresholds and CTP (Core Training Points) per rank - 10 echelons |
| **Reaction Progression & Log** | Defensive skill XP tracking (Block/Dodge/Parry) - 11 ranks per skill |
| **Battle Log** | Combat history and team XP calculation based on rank differential |
| **Special Techniques** | Path-specific special attacks with defense constraints and power modifiers |
| **Personality** | Character trait tracking (Patience, Empathy, Cunning, Logic, Kindness, Charisma) |
| **Combatant info data** | UI-ready data - stance indicators, stamina colors, energy segment display |
| **Defense Simulations** | Balancing tool for calculating expected damage outcomes at different defense ranks |
| **Combatant Information** | Formatted display data for testing |
| **Defense Constraints** | Reference data for special technique constraints |

---

## Combatant Stats Structure

### Core Attributes

- **Stamina:** Health pool (base × 5 = max stamina)
- **Power:** Damage output for attacks and specials
- **Speed:** Initiative order and Blindside calculations

### Defensive Skills

Each combatant has 3 defensive skills, each with 3 rates that determine effectiveness:

| Skill | Success Rate (SR) | Success Mitigation (SMR) | Fail Mitigation (FMR) |
|-------|-------------------|--------------------------|----------------------|
| **Block** | Chance to successfully block | Damage reduced on success | Damage reduced on failure |
| **Dodge** | Chance to fully evade | N/A (full evasion) | Damage reduced on failure |
| **Parry** | Chance to counter-attack | N/A (triggers counter) | Damage reduced on failure |

*Each skill has its own rank (1-11) that improves through combat XP. Success = 4 XP, Failure = 2 XP.*

### Dynamic Stats (Math Sheet Rows 12-18)

The Math sheet calculates effective stats by combining base values with buff/debuff modifiers. All 39 columns track:

- Max/Current Stamina, Stamina Consumption Rate
- Power, Speed (with modifiers)
- All 9 defense rates (3 per skill) with modifiers
- Evade Regen Rate, Attack/Special/Group Cost
- Blindside Rate, Crushing Blow Rate

---

## Round Structure

Combat follows a structured phase system that creates tactical depth through information asymmetry - AI decides first, player reacts to visual cues.

### Phase 1: AIc Decision Phase (Hidden)

- AI selects actions and targets for all AIcs
- Decisions are locked before player sees anything

### Phase 2: Visual Information Phase

- Player sees AIc stances (A/D/E/S/G for Attack/Defend/Evade/Special/Group)
- Player sees AIc stamina levels (color-coded: Green 100-75%, Yellow 74-50%, Orange 49-25%, Red 24-1%, Black 0%)
- Player sees AIc targeting information

### Phase 3: PCc Action Declaration Phase

- Player selects actions for each PCc based on visible information
- Strategic response to AIc positioning and actions

### Phase 4: Action Resolution (Priority Order)

| Priority | Action | Effect |
|----------|--------|--------|
| 1 | **DEFEND** | Intercept attacks targeting an ally - redirects attack to defender |
| 2 | **GROUP** | Coordinated team action (inspired by Skies of Arcadia crew specials) - undefined |
| 3 | **SPECIAL / ATTACK** | Deal damage - Special uses energy segments for boosted power + defense constraint |
| 4 | **EVADE** | Recover 30% of max stamina (base Evade Regen Rate = 0.30) |

*Within same priority: sorted by Speed + random factor (Input!F22:F27) for tie-breaking.*

### Phase 5: Per-Attack Resolution

1. Identify True Target (check for Defenders intercepting)
2. Rank KO Roll (if attacker rank > target rank by 0.5+)
3. Blindside Roll (if attacker speed > target speed)
4. Reaction Selection (may be constrained by Special/Blindside/KO)
5. Defense Roll and Damage Calculation
6. Counter Chain (if Parry succeeds - can chain indefinitely)
7. Stamina and Energy Updates, Buff/Debuff Application

---

## Combat Mechanics Deep Dive

### Rank KO (Instant Knockout)

**Condition:** Attacker Rank > Target Rank by at least 0.5

**Threshold Formula:** `((Attacker Rank - Target Rank) × 3) / 10`

**Roll Check:** `If (Roll/20) >= (1 - Threshold) → Instant KO`

*Risk/Reward: Fighting higher-ranked enemies = more XP but risk of instant KO. Enemy team rank is always disclosed (honorable combat culture).*

### Blindside (Speed Dominance)

**Condition:** Attacker Speed > Target Speed

**Threshold Formula:** `(Attacker Speed - Target Speed) / Target Speed`

**Roll Check:** `If (Roll/20) >= (1 - Threshold) → Blindside Success`

**Effect:** Target is forced into Defenseless - Block, Dodge, and Parry are all unavailable. The attacker is too fast for the defender to react.

### Crushing Blow (Power Dominance)

**Condition:** Defense was Block AND Action Power > Target Power

**Threshold Formula:** `(Action Power - Target Power) / Target Power`

**Roll Check:** `If (Roll/20) >= (1 - Threshold) → Crushing Blow`

**Effect:** Applies debuffs to target's Block SR, SMR, and FMR. Only possible against Block - Dodge/Parry cannot trigger Crushing Blow.

### Defense Resolution

| Defense | Success Effect | Failure Effect |
|---------|----------------|----------------|
| **Defenseless** | N/A | Full damage taken |
| **Block** | Damage × (1 - SMR) | Damage × (1 - FMR), Crushing Blow possible |
| **Dodge** | No damage taken | Damage × (1 - FMR) |
| **Parry** | Triggers Counter attack | Damage × (1 - FMR) |

### Parry/Counter Chain

1. If Parry succeeds → Counter attack inserted into execution queue
2. Counter uses the counter-attacker's Power stat
3. Original attacker can react to Counter (may choose Parry)
4. If they Parry successfully → another Counter (chain continues)
5. Chain ends when: Parry fails, someone is KO'd, or stamina depleted

*Counter entries in the sorted action row are marked with 'c' suffix (e.g., '1c', '2c'). The system supports up to 15 action slots per round (Input rows 32-46).*

---

## Path / Elemental System

### Six Paths with Opposing Focuses

| Path | Style | Energy Focus | Offensive Effect | Defensive Bonus |
|------|-------|--------------|------------------|-----------------|
| **Fire** | Reaction | Parry SR | Boosts own Parry | +Parry SR |
| **Water** | Action | Dodge SR | Debuffs target Dodge | - |
| **Air** | Reaction | Dodge SR | Boosts own Dodge | +Dodge SR |
| **Earth** | Action | Block SR | Debuffs target Block | - |
| **Shadow** | Action | Parry SR | Debuffs target Parry | - |
| **Light** | Reaction | Block SR | Boosts own Block | +Block SR |

*Style Focus: Reaction paths improve your own defensive rates. Action paths reduce enemy defensive rates.*

### Ascension Levels

| Level | Segments Required | Accumulation Bonus | Starting Segments |
|-------|-------------------|-------------------|-------------------|
| 0 | 0 | +0% | 0 |
| 1 | 35 | +25% | 0 |
| 2 | 95 (35+60) | +25% | 1 |
| 3 | 180 (95+85) | +50% | 2 |

### Energy Segment Gains

| Event Type | Result | Base Gain |
|------------|--------|-----------|
| Action (Attack/Special) | Success | 1.0 |
| Action (Attack/Special) | Failure | 0.5 |
| Reaction (Block/Dodge/Parry) | Success | 0.5 |
| Reaction (Block/Dodge/Parry) | Failure | 0.25 |

### Special Techniques (Defense Constraints)

Each path's Special attack costs energy segments (1-5), boosts damage by 10% per segment used, and forces the target to use a specific defense:

| Path | Forced Defense | Description |
|------|----------------|-------------|
| **Fire** | Parry only | Fast combo attack, self-buffs Parry rate |
| **Water** | Dodge only | Decisive strike, debuffs target Dodge rate |
| **Air** | Dodge only | Decisive strike, self-buffs Dodge rate |
| **Earth** | Block only | Massive blow, debuffs target Block rate |
| **Shadow** | Parry only | Fast combo attack, debuffs target Parry rate |
| **Light** | Block only | Massive blow, self-buffs Block rate |

---

## Progression Systems

### Rank Echelons (10 Tiers, 10 Degrees Each)

Characters progress through 10 echelons, each with 10 degrees (1st through 10th). Rank is expressed as a decimal (e.g., 2.5 = 5th Degree Iron).

| Rank 1-2 | Rank 3-4 | Rank 5-6 | Rank 7-8 | Rank 9-10 |
|----------|----------|----------|----------|-----------|
| Stone | Bronze | Gold | Diamond | Grand Master |
| Iron | Silver | Platinum | Master | Legend |

### Team XP (Battle Log)

- Based on rank differential between enemy team and party
- Range: -10 to +10 differential, mapped to XP values via lookup table
- Higher-ranked enemies = more XP reward but higher Rank KO risk

### Reaction Skill XP (Per-Character, Per-Skill)

- Success = 4 XP, Failure = 2 XP
- 11 ranks per skill (Block, Dodge, Parry tracked separately)
- XP thresholds: 0 → 100 → 215 → 345 → 490 → 650 → 825 → 1015 → 1220 → 1440 → 1675

### Core Training Points (CTP)

- Earned through rank progression
- Spent on Stamina, Power, Speed improvements

### Personality System

Tracks 6 traits across 3 categories for narrative/choice purposes:

- **Wisdom:** Patience, Empathy
- **Intelligence:** Cunning, Logic
- **Charisma:** Kindness, Charisma

*Traits range from 5% (min) to 35% (max), with 6% adjustment rate redistributed across traits.*

---

## Google Apps Script Functions

The following functions automate combat workflow (originally for Google Sheets, non-functional in Excel):

| Function | Purpose |
|----------|---------|
| `CombatInitialize()` | Set initial paths (X6:X8→D6:D8), copy starting energy segments (K92:T97→A67:J72) |
| `NewRound()` | Increment round counter, reset execution area (A32:J46), clear prep phase (B22:F27), update copy row pointers |
| `ApplyRandomFactor()` | Generate random values in F22:F27 for initiative tie-breaking within same priority |
| `NewCombat()` | Full reset - copy path data from Paths sheet, reset round to 1, clear all combat records (A67:J364, U67:V364), clear buffs |
| `NextRoundPrep()` | Copy sorted prep data to records, increment row pointers for next round |
| `CopyRecords()` | Archive execution results to combat log, handle buff logging, process reaction XP gains, apply Crushing Blow debuffs |

---

## Key Formula Locations

| Location | Purpose |
|----------|---------|
| `Math!A13:AM18` | Dynamic stats for all 6 combatants (39 columns of calculated values) |
| `Math!A22:N27` | Path tracking - current segments, bonuses, style focus per combatant |
| `Math!A31:D36` | Round prep phase - priority and speed sorting |
| `Math!A40:AM54` | Execution phase - all damage/defense calculations for 15 action slots |
| `Input!A22:F27` | Round prep input (Actor, Action Type, Sub Type, Target, Segments, Random) |
| `Input!A32:K46` | Execution phase display (pulled from Math calculations) |
| `Input!L50:S64` | Sorted action order, parry chain tracking, defender redirection |
| `Input!A67:J364` | Combat records - full history of all actions/reactions/damage |
| `Input!L2:M5` | Available reactions (constrained by Special/Blindside/KO status) |
| `Paths!A2:I28` | Path tier bonuses by ascension level (6 paths × 4 ascension levels) |
| `Paths!K2:N7` | Path definitions (Style Focus, Energy Focus, Focus Statement) |
| `Paths!P2:S5` | Ascension requirements and bonuses |
| `BuffsDebuffs!A3:F17` | Special technique buffs/debuffs (auto-generated from execution) |
| `BuffsDebuffs!A20:F34` | Crushing Blow debuffs (Block SR, SMR, FMR penalties) |
| `BuffsDebuffs!G:L` | Active buff/debuff records with duration tracking |

---

## Design Notes

### Information Asymmetry

The system creates tactical depth through asymmetric information flow. AIcs commit to actions before players see the battlefield state, then players respond with full knowledge of enemy intentions. This makes player decisions meaningful while keeping AI behavior predictable enough to be readable through visual cues (stance indicators, stamina colors).

### Risk/Reward Balance

Multiple mechanics create risk/reward decisions:
- Fighting higher-ranked enemies for more XP vs. Rank KO risk
- Using Block for mitigation vs. Crushing Blow vulnerability
- Investing in Parry for counter potential vs. zero mitigation on failure
- Spending energy segments on powerful Specials vs. saving for later

### Path Synergies

The path system creates build diversity through opposing philosophies: Reaction paths (Fire, Air, Light) improve your own defenses while Action paths (Water, Earth, Shadow) debuff enemies. This pairs with the defense constraint system - knowing which path an enemy uses tells you which defense you'll be forced into.

### Group Action Concept

Inspired by Skies of Arcadia Legends crew specials (Prophecy, Blue Rogues). When selected, all combatants participate, forgoing individual choices for a coordinated team effect. Priority 2 (after Defend, before Attack) suggests it's a defensive/supportive mechanic. Implementation undefined - intended for future development.

### Future AI Considerations

AI decision-making should consider:
- Targeting weak PCcs (low stamina)
- Exploiting speed advantages for Blindside
- Recognizing threats
- Probing player defensive preferences
- Protecting damaged allies
- Using Special attacks strategically based on defense constraints

Priority targeting may scale with rank/degree to create difficulty progression.

---

## Visual Information System

### Combatant info data Sheet provides:

- **Stance Indicators** (AM9:AM14): A/D/E/S/G for action types
- **Stamina Color Coding** (AM3:AM5):
  - Green: 100%-75%
  - Yellow: 74%-50%
  - Orange: 49%-25%
  - Red: 24%-1%
  - Black: 0% (KO)
- **Energy Segment Display** (AN9:AR14): 5 binary indicators per combatant
- **Active Buffs**: Filtered by combatant for display

---

## Data Structures for Implementation

### Combatant Object

```
{
  name: string,
  team: "PCc" | "AIc",
  
  // Core Stats
  stamina: { base: number, current: number, max: number },
  power: number,
  speed: number,
  
  // Rank
  echelon: string,  // "Stone", "Iron", etc.
  degree: number,   // 1-10
  rank: number,     // decimal (e.g., 2.5)
  
  // Defensive Skills (each has rank 1-11)
  block: { rank: number, xp: number, sr: number, smr: number, fmr: number },
  dodge: { rank: number, xp: number, sr: number, smr: number, fmr: number },
  parry: { rank: number, xp: number, sr: number, smr: number, fmr: number },
  
  // Path
  path: string,           // "Fire", "Water", etc.
  ascension: number,      // 0-3
  energySegments: number, // current accumulated
  
  // Status
  isKO: boolean,
  buffs: [],
  debuffs: []
}
```

### Action Object

```
{
  actor: string,
  actionType: "Attack" | "Special" | "Defend" | "Evade" | "Group",
  subType: string | null,     // path name for Special
  target: string | null,
  segmentsUsed: number,       // for Special
  priority: number,           // 1-4
  orderSpeed: number          // speed + random factor
}
```

### Combat Resolution Object

```
{
  attacker: string,
  defender: string,
  trueTarget: string,         // may differ if Defend intercepts
  
  // Roll Results
  rankKO: { threshold: number, roll: number, success: boolean },
  blindside: { threshold: number, roll: number, success: boolean },
  
  // Defense
  defenseUsed: "Defenseless" | "Block" | "Dodge" | "Parry",
  defenseConstrained: boolean,  // forced by Special
  defenseSuccess: boolean,
  
  // Damage
  actionPower: number,
  damageDealt: number,
  damageReduced: number,
  
  // Crushing Blow (Block only)
  crushingBlow: { eligible: boolean, threshold: number, roll: number, success: boolean },
  
  // Energy Gains
  attackerEnergyGain: number,
  defenderEnergyGain: number,
  
  // Counter (Parry success)
  triggersCounter: boolean
}
```

---

## Combat Flow State Machine

```
COMBAT_START
    ↓
ROUND_START
    ↓
AIc_DECISION_PHASE (hidden)
    ↓
VISUAL_INFORMATION_PHASE
    ↓
PCc_DECLARATION_PHASE
    ↓
ACTION_RESOLUTION_PHASE
    ├── Sort by Priority, then Speed
    ├── For each action:
    │   ├── Identify True Target
    │   ├── Roll Rank KO
    │   ├── Roll Blindside
    │   ├── Determine Available Reactions
    │   ├── Defender Chooses Reaction (or forced)
    │   ├── Roll Defense
    │   ├── Calculate Damage
    │   ├── Apply Crushing Blow (if Block)
    │   ├── Update Stamina
    │   ├── Update Energy Segments
    │   ├── Apply Buffs/Debuffs
    │   └── If Parry Success → Insert Counter
    ↓
ROUND_END
    ├── Check for combat end (all PCcs KO or all AIcs KO)
    ├── Process buff/debuff durations
    └── Award XP
    ↓
ROUND_START (loop) or COMBAT_END
```
