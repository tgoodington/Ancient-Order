# Design Specification: Behavior Tree AI System

**Date:** 2026-02-21
**Status:** Approved
**Plan Reference:** Task 17 (Behavior Tree AI)
**Domain:** Game Design / Code Architecture

## 1. Overview

**Purpose:** A utility-scoring AI system that evaluates combat state and produces deterministic, archetype-differentiated combat decisions for each NPC during the hidden Phase 1 of the 5-phase combat round pipeline.

**Scope:**
- Included: Evaluator engine, 7 scoring factors, rank-based decision quality, combat perception layer, 3 archetype profiles, path-based tie-breaking, evaluator configuration
- Excluded: GROUP action scoring (deferred to Group Action Type design), player-side AI, difficulty settings, learning/adaptive AI

**Key Design Decisions:**
- **Utility scoring over classic behavior tree:** Transparent, testable, data-driven. Every decision is traceable to a score breakdown. No tree traversal order dependencies.
- **Multi-output factors:** Each factor returns scores for all 5 action types simultaneously. Reduces factor count (7 vs ~20 for flat), models real combat reasoning naturally.
- **Combined (action, target) scoring:** All action+target pairs evaluated together. Enables "ATTACK weakest enemy" to naturally beat "DEFEND healthy ally" without two-phase separation.
- **Combat perception layer:** Pre-computed readonly snapshot mediates between raw CombatState and factors. Enforces immutability, eliminates redundant computation.
- **Rank-based decision quality:** Linear coefficient (floor 0.2) scales factor influence by combatant rank. Low-rank NPCs rely on instinct (base scores); high-rank NPCs use full tactical awareness.
- **Path-based tie-breaking:** Elemental path determines action priority for score ties. Generalizes to any future combatant without per-character configuration.

## 2. Elements

### Core Types

```typescript
// ActionScores: score contribution per action type
type ActionScores = Record<ActionType, number>;

// ScoringFactor: evaluates one aspect of combat state
interface ScoringFactor {
  name: string;
  evaluate(self: CombatPerception, target: TargetPerception | null): ActionScores;
}

// ArchetypeProfile: data-driven NPC combat personality
interface ArchetypeProfile {
  name: string;                              // e.g., "elena_loyal_scout"
  baseScores: ActionScores;                  // starting bias per action type
  factorWeights: Record<string, number>;     // weight multiplier per factor name
  elementalPath: ElementalPath;              // determines tie-break order
}

// EvaluatorConfig: runtime feature toggles
interface EvaluatorConfig {
  groupActionsEnabled: boolean;              // false until Group Action designed
}

// ScoredCandidate: intermediate evaluation result
interface ScoredCandidate {
  actionType: ActionType;
  targetId: string | null;
  score: number;
  scoreBreakdown: Record<string, number>;    // per-factor contribution (for debugging/testing)
}
```

### Combat Perception

```typescript
interface CombatPerception {
  // Self
  selfId: string;
  selfStaminaPct: number;          // current / max (0.0 - 1.0)
  selfEnergy: number;              // current energy segments
  selfAscension: number;           // ascension level (0-3)
  selfRank: number;                // decimal rank (e.g., 2.5)
  selfPath: ElementalPath;         // elemental path

  // Team (allies, excluding self)
  allies: AllyPerception[];        // sorted by stamina % ascending
  lowestAllyStaminaPct: number;    // min stamina % across non-KO'd allies
  teamAvgStaminaPct: number;       // average stamina % across non-KO'd allies + self
  allyCount: number;               // count of non-KO'd allies

  // Enemies
  enemies: EnemyPerception[];      // sorted by stamina % ascending
  weakestEnemyStaminaPct: number;  // min stamina % across non-KO'd enemies
  enemyAvgStaminaPct: number;      // average stamina % across non-KO'd enemies
  enemyCount: number;              // count of non-KO'd enemies

  // Context
  round: number;                   // current round number (1-based)
}

interface AllyPerception {
  id: string;
  staminaPct: number;
  isKO: boolean;
}

interface EnemyPerception {
  id: string;
  staminaPct: number;
  isKO: boolean;
  speedDelta: number;              // (self.speed - enemy.speed) / enemy.speed
  rankDelta: number;               // (self.rank - enemy.rank)
  power: number;                   // for Crushing Blow assessment
}

interface TargetPerception {
  id: string;
  staminaPct: number;
  speedDelta: number;
  rankDelta: number;
  power: number;
}
```

### Element Inventory

- **Evaluator engine**: Orchestrates scoring loop, applies rank coefficient, selects winner
- **ScoringFactor** (×7): Individual combat state evaluators (see Section 4)
- **ArchetypeProfile** (×3): Data-driven combat personality for Elena, Lars, Kade
- **CombatPerception**: Readonly pre-computed snapshot of combat state
- **Rank coefficient function**: Decision quality scaling by combatant rank
- **Path tie-break table**: 6 elemental paths → 6 action priority orderings
- **EvaluatorConfig**: Runtime feature flags (GROUP enabled/disabled)

### Boundaries & Ownership

- **Evaluator engine** owns: scoring loop, candidate generation, winner selection. Does NOT own: factor logic, profile data, perception building.
- **Factors** own: scoring logic for their domain (e.g., OwnStamina knows how stamina levels affect action preference). Do NOT own: final action selection or target validation.
- **CombatPerception** owns: translating raw CombatState into AI-readable format. Does NOT own: any scoring logic.
- **ArchetypeProfile** is pure data — no behavior.
- **Round Manager** calls the evaluator; evaluator does not call Round Manager.

## 3. Connections

### Relationship Map

- **Round Manager (Phase 1)** → **Evaluator**: Calls `evaluate(combatant, state, config)` once per NPC per round. Receives `CombatAction`.
- **Evaluator** → **buildPerception()**: Calls once per NPC to create `CombatPerception` from raw `Combatant` + `CombatState`.
- **Evaluator** → **ScoringFactors**: Iterates all 7 factors, passes perception + target, collects `ActionScores`.
- **Evaluator** → **ArchetypeProfile**: Reads base scores, factor weights, elemental path.
- **Evaluator** → **rankCoefficient()**: Computes decision quality modifier from combatant rank.
- **Evaluator** → **PATH_TIEBREAK**: Reads tie-break order from elemental path table.
- **ArchetypeProfile** → **types/combat.ts**: References `ActionType`, `ElementalPath`.
- **CombatPerception** → **CombatState**: Reads combatant arrays, round number. Never writes.

### Integration Points

- **combat/roundManager.ts (Phase 1)**: Calls evaluator for each enemy combatant. Expects: `evaluate(combatant: Combatant, state: CombatState): CombatAction`. The evaluator wraps this interface, internally building perception and running the scoring loop.
- **types/combat.ts**: Evaluator output must conform to existing `CombatAction` interface (`combatantId`, `type: ActionType`, `targetId`, `energySegments?`).
- **combat/pipeline.ts**: The `CombatAction` produced by the evaluator enters the same priority queue as player actions. No special handling needed.
- **combat/groupAction.ts** (future): When GROUP is designed, factors will add GROUP-specific scoring logic and `groupActionsEnabled` flips to `true`.

### File Structure

```
src/combat/behaviorTree/
├── evaluator.ts          # Main evaluate() function + scoring loop
├── perception.ts         # buildPerception() — CombatState → CombatPerception
├── factors/
│   ├── index.ts          # Factor registry (exports all factors)
│   ├── ownStamina.ts     # Factor: self-preservation
│   ├── allyInDanger.ts   # Factor: team protection
│   ├── targetVulnerability.ts  # Factor: offensive opportunity
│   ├── energyAvailability.ts   # Factor: resource management
│   ├── speedAdvantage.ts       # Factor: Blindside exploitation
│   ├── roundPhase.ts           # Factor: temporal strategy
│   └── teamBalance.ts          # Factor: team stamina comparison
├── profiles/
│   ├── index.ts          # Profile registry
│   ├── elena.ts          # Elena archetype profile
│   ├── lars.ts           # Lars archetype profile
│   └── kade.ts           # Kade archetype profile
├── rankCoefficient.ts    # Rank-based decision quality scaling
└── tieBreaking.ts        # Path-based tie-break table and logic
```

## 4. Dynamics

### Core Algorithm: evaluate()

```
evaluate(combatant: Combatant, state: CombatState, config: EvaluatorConfig): CombatAction

1. Load archetype profile by combatant archetype ID
2. Build CombatPerception from combatant + state (buildPerception)
3. Compute rank coefficient from combatant rank
4. Determine valid action types (exclude GROUP if config.groupActionsEnabled === false)
5. For each valid action type:
   a. Get valid targets for this action type:
      - ATTACK: all non-KO'd enemies
      - SPECIAL: all non-KO'd enemies (only if self has energy segments > 0; skip entirely otherwise)
      - DEFEND: all non-KO'd allies
      - EVADE: [null] (self-targeting, always valid)
      - GROUP: [null] (team action, always valid when enabled)
   b. For each valid target:
      - Start with profile.baseScores[actionType]
      - For each factor in factor registry:
        - Compute: factorScores = factor.evaluate(perception, targetPerception)
        - Add: profile.factorWeights[factor.name] * factorScores[actionType]
      - Multiply total factor contribution by rankCoefficient
      - Final score = baseScore + (factorContribution * rankCoefficient)
      - Record candidate: { actionType, targetId, score, scoreBreakdown }
6. Sort candidates by score descending
7. If top candidates tie: apply path-based tie-breaking (PATH_TIEBREAK[profile.elementalPath])
8. If still tied (same action type): prefer target with lowest stamina %
9. Return CombatAction: { combatantId: self.id, type: winner.actionType, targetId: winner.targetId, energySegments: (if SPECIAL, segments to use) }
```

### Factor Scoring Logic

Each factor returns `ActionScores` (values typically in -1.0 to 1.0 range, scaled by profile weights).

**1. OwnStamina** — Self-preservation
- When selfStaminaPct < 0.3: { ATTACK: -0.5, DEFEND: 0.1, EVADE: 0.9, SPECIAL: -0.3, GROUP: 0 }
- When selfStaminaPct 0.3-0.6: { ATTACK: 0, DEFEND: 0, EVADE: 0.2, SPECIAL: 0, GROUP: 0 }
- When selfStaminaPct > 0.6: { ATTACK: 0.2, DEFEND: 0, EVADE: -0.3, SPECIAL: 0.1, GROUP: 0 }
- Scales linearly within each bracket

**2. AllyInDanger** — Team protection
- Scans allies for lowest stamina %. When lowestAllyStaminaPct < 0.3:
  { ATTACK: -0.2, DEFEND: 0.8, EVADE: -0.1, SPECIAL: 0.4, GROUP: 0.2 }
- When lowestAllyStaminaPct 0.3-0.6: { ATTACK: 0, DEFEND: 0.3, EVADE: 0, SPECIAL: 0.1, GROUP: 0 }
- When all allies healthy: all zeros

**3. TargetVulnerability** — Offensive opportunity (target-aware)
- When target.staminaPct < 0.25: { ATTACK: 0.8, DEFEND: -0.2, EVADE: -0.3, SPECIAL: 0.6, GROUP: 0 }
- When target.staminaPct 0.25-0.5: { ATTACK: 0.4, DEFEND: 0, EVADE: -0.1, SPECIAL: 0.3, GROUP: 0 }
- When target healthy: { ATTACK: 0.1, DEFEND: 0, EVADE: 0, SPECIAL: 0.1, GROUP: 0 }

**4. EnergyAvailability** — Resource management
- When selfEnergy >= 3: { ATTACK: -0.1, DEFEND: 0, EVADE: -0.2, SPECIAL: 0.7, GROUP: 0 }
- When selfEnergy 1-2: { ATTACK: 0, DEFEND: 0, EVADE: 0, SPECIAL: 0.3, GROUP: 0 }
- When selfEnergy == 0: { ATTACK: 0.1, DEFEND: 0, EVADE: 0.1, SPECIAL: 0, GROUP: 0 } (SPECIAL already filtered from candidates)

**5. SpeedAdvantage** — Blindside exploitation (target-aware)
- When speedDelta > 0.3 (significantly faster): { ATTACK: 0.6, DEFEND: -0.1, EVADE: -0.2, SPECIAL: 0.3, GROUP: 0 }
- When speedDelta 0-0.3: { ATTACK: 0.2, DEFEND: 0, EVADE: 0, SPECIAL: 0.1, GROUP: 0 }
- When speedDelta < 0 (slower): { ATTACK: -0.1, DEFEND: 0.1, EVADE: 0.1, SPECIAL: 0, GROUP: 0 }

**6. RoundPhase** — Temporal strategy
- Rounds 1-2 (early): { ATTACK: 0.1, DEFEND: 0.2, EVADE: 0.3, SPECIAL: -0.2, GROUP: 0 } (build energy, assess)
- Rounds 3-5 (mid): { ATTACK: 0.2, DEFEND: 0, EVADE: 0, SPECIAL: 0.2, GROUP: 0 } (balanced)
- Rounds 6+ (late): { ATTACK: 0.3, DEFEND: -0.1, EVADE: -0.1, SPECIAL: 0.4, GROUP: 0 } (press advantage)

**7. TeamBalance** — Team stamina comparison
- When teamAvgStaminaPct > enemyAvgStaminaPct + 0.2 (winning): { ATTACK: 0.3, DEFEND: -0.1, EVADE: -0.2, SPECIAL: 0.2, GROUP: 0 }
- When roughly even (±0.2): all zeros
- When teamAvgStaminaPct < enemyAvgStaminaPct - 0.2 (losing): { ATTACK: -0.2, DEFEND: 0.4, EVADE: 0.3, SPECIAL: 0.1, GROUP: 0.2 }

### Rank Coefficient

```typescript
const RANK_FLOOR = 0.2;
const RANK_MAX = 10.0;

function rankCoefficient(rank: number): number {
  return Math.max(RANK_FLOOR, rank / RANK_MAX);
}
```

- Stone (1.0): 0.2 — factors contribute 20%, base scores dominate
- Silver (3.0): 0.3 — factors contribute 30%
- Gold (5.0): 0.5 — balanced influence
- Diamond (7.0): 0.7 — strongly tactical
- Legend (10.0): 1.0 — full tactical awareness

### Path-Based Tie-Breaking

```typescript
const PATH_TIEBREAK: Record<ElementalPath, ActionType[]> = {
  // Action paths (offensive-leaning)
  Fire:   ['ATTACK', 'SPECIAL', 'DEFEND', 'EVADE', 'GROUP'],
  Shadow: ['SPECIAL', 'ATTACK', 'EVADE', 'DEFEND', 'GROUP'],
  Earth:  ['DEFEND', 'ATTACK', 'SPECIAL', 'EVADE', 'GROUP'],

  // Reaction paths (defensive-leaning)
  Water:  ['DEFEND', 'EVADE', 'SPECIAL', 'ATTACK', 'GROUP'],
  Air:    ['EVADE', 'DEFEND', 'SPECIAL', 'ATTACK', 'GROUP'],
  Light:  ['SPECIAL', 'DEFEND', 'EVADE', 'ATTACK', 'GROUP'],
};
```

Tie-breaking order:
1. Higher score wins
2. If tied: action type earlier in PATH_TIEBREAK[combatant.path] wins
3. If still tied (same action type): target with lowest staminaPct wins

### Archetype Profiles

**Elena (Loyal Scout / Healer-Support)**
- Path: Light (Reaction)
- Base scores: { ATTACK: 0.3, DEFEND: 0.5, EVADE: 0.3, SPECIAL: 0.4, GROUP: 0.2 }
- Factor weights: { ownStamina: 1.0, allyInDanger: 1.8, targetVulnerability: 0.5, energyAvailability: 1.2, speedAdvantage: 0.3, roundPhase: 0.8, teamBalance: 1.4 }
- Character: Protective instincts. Strongly responds to ally danger. Uses SPECIAL for support. Avoids aggressive play unless team is winning.

**Lars (Scheming Merchant / Tank-Defender)**
- Path: Earth (Action)
- Base scores: { ATTACK: 0.4, DEFEND: 0.5, EVADE: 0.4, SPECIAL: 0.3, GROUP: 0.2 }
- Factor weights: { ownStamina: 1.5, allyInDanger: 1.0, targetVulnerability: 0.8, energyAvailability: 1.4, speedAdvantage: 0.6, roundPhase: 1.2, teamBalance: 1.0 }
- Character: Efficient and self-preserving. Manages energy carefully. Punishes weak targets when safe. Adapts strategy to round progression.

**Kade (Rogue Outlaw / Striker-Aggressive)**
- Path: Fire (Action)
- Base scores: { ATTACK: 0.6, DEFEND: 0.2, EVADE: 0.3, SPECIAL: 0.4, GROUP: 0.1 }
- Factor weights: { ownStamina: 0.6, allyInDanger: 0.4, targetVulnerability: 1.6, energyAvailability: 1.0, speedAdvantage: 1.5, roundPhase: 0.5, teamBalance: 0.6 }
- Character: Aggressive opportunist. Exploits weak and slow targets. High risk tolerance. Ignores team needs. Presses advantages relentlessly.

### Edge Cases

| Scenario | Handling |
|----------|----------|
| Combatant is KO'd | Evaluator is not called for KO'd combatants (Round Manager skips them) |
| All enemies KO'd | No valid ATTACK/SPECIAL targets → only EVADE and DEFEND candidates generated. EVADE wins by default. |
| All allies KO'd | No valid DEFEND targets → DEFEND excluded from candidates. Scoring proceeds with remaining actions. |
| No energy segments | SPECIAL filtered from candidates entirely (not scored with 0). |
| GROUP disabled (config) | GROUP filtered from candidates entirely. |
| SPECIAL with energy: how many segments? | Use all available segments (maximize damage bonus). Can be refined later if strategic segment conservation matters. |
| All candidates score ≤ 0 | Still picks the highest score (least negative). This represents a desperate situation — the NPC picks the least-bad option. |
| Single enemy remaining | All offensive factors converge on that target. No target selection ambiguity. |
| Round 1 with no history | RoundPhase factor applies early-game defaults. All other factors evaluate current state (no history dependency in factors). |

### Rules & Invariants

- **Determinism:** Same `Combatant` + `CombatState` + `EvaluatorConfig` → always same `CombatAction`. No randomness in evaluation.
- **Purity:** Evaluator and all factors are pure functions. No state mutation, no side effects, no I/O.
- **Score range:** Factor outputs should stay in [-1.0, 1.0] range. Profile weights scale their influence. Final scores are unbounded but typically in [0.0, 3.0] range.
- **Valid output:** Evaluator always produces a valid `CombatAction` with a real target (or null for EVADE/GROUP). Never returns an invalid action type or KO'd target.
- **Factor independence:** Each factor evaluates independently. No factor reads another factor's output. Order of factor evaluation does not affect results.

## 5. Implementation Notes

**Suggested approach:**
1. Start with `perception.ts` — build and test the CombatPerception builder
2. Implement `rankCoefficient.ts` and `tieBreaking.ts` (small, self-contained utilities)
3. Implement factors one at a time, each with dedicated tests using constructed CombatPerception objects
4. Implement `evaluator.ts` scoring loop, wiring factors + profiles + rank + tie-breaking
5. Implement 3 archetype profiles as data objects
6. Integration test: given a known CombatState, verify each archetype produces the expected action

**Constraints from existing context:**
- Must conform to `BehaviorTreeEvaluator` interface from `types/combat.ts`: `evaluate(combatant: Combatant, state: CombatState): CombatAction`
- The public interface wraps the internal scoring system — externally it's the simple interface, internally it builds perception and runs the scoring loop
- All types referenced (Combatant, CombatState, CombatAction, ActionType) come from `types/combat.ts`
- Archetype profiles must be loadable by combatant archetype ID (data lookup, not switch statement)

**Verification considerations:**
- **Unit test each factor** with constructed CombatPerception objects representing specific scenarios (low stamina, ally in danger, etc.)
- **Unit test rank coefficient** across the full rank range
- **Unit test tie-breaking** for all 6 elemental paths
- **Unit test evaluator** with known profiles and perception objects — verify exact action+target selection
- **Archetype differentiation test:** Given identical CombatState, verify Elena/Lars/Kade produce different actions (at least 2 of 3 differ)
- **Determinism test:** Run evaluator twice with identical inputs, verify identical output
- **Edge case tests:** No valid targets, no energy, all allies KO'd, all candidates negative

## 6. References

- Plan task: Task 17 (Section 6, plan.md)
- Related decisions: ADR-014 (Behavior Tree AI), ADR-012 (Pipeline Combat Architecture), ADR-013 (Independent CombatState), ADR-008 (Information Asymmetry)
- Context research: `docs/project_notes/trunk/.design_research/behavior_tree_ai_system/context.md`
- Combat system: `docs/Reference Documents/GM_Combat_Tracker_Documentation.md`
- NPC archetypes: `docs/Reference Documents/Sprint1_ClaudeCode_Prompt.md`
