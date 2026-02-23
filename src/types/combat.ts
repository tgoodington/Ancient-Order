/**
 * Ancient Order - Sprint 2 Combat Type Definitions
 *
 * All combat interfaces for the Sprint 2 combat engine.
 * This file has no imports from types/index.ts — it is a standalone leaf.
 * index.ts imports CombatState from this file to type GameState.combatState.
 *
 * Dependency direction: index.ts → combat.ts (one-way, no circular imports).
 */

// ============================================================================
// Elemental Path
// ============================================================================

/**
 * The six elemental paths that define a combatant's buff/debuff style.
 * Action paths (Water, Earth, Shadow) debuff the target's defensive rates.
 * Reaction paths (Fire, Air, Light) boost own defensive rates.
 */
export type ElementalPath = 'Fire' | 'Water' | 'Air' | 'Earth' | 'Shadow' | 'Light';

// ============================================================================
// Action & Phase Enumerations
// ============================================================================

/**
 * All valid combat action types.
 * Priority order: GROUP(0) > DEFEND(1) > ATTACK(2) = SPECIAL(2) > EVADE(3)
 */
export type ActionType = 'ATTACK' | 'DEFEND' | 'EVADE' | 'SPECIAL' | 'GROUP';

/**
 * The 5 phases of a combat round, executed in sequence.
 */
export type CombatPhase =
  | 'AI_DECISION'
  | 'VISUAL_INFO'
  | 'PC_DECLARATION'
  | 'ACTION_RESOLUTION'
  | 'PER_ATTACK';

/**
 * Defense mechanic types available to combatants.
 */
export type DefenseType = 'block' | 'dodge' | 'parry' | 'defenseless';

/**
 * Ascension levels unlocked through energy accumulation.
 * Thresholds: 35 segments → level 1, 95 → level 2, 180 → level 3.
 */
export type AscensionLevel = 0 | 1 | 2 | 3;

// ============================================================================
// Combatant
// ============================================================================

/**
 * Reaction skill rates for Block, Dodge, and Parry.
 * SR = Success Rate, SMR = Success Mitigation Rate, FMR = Failure Mitigation Rate.
 * All values are 0.0–1.0 probability ranges.
 */
export interface ReactionSkills {
  readonly block: {
    readonly SR: number; // probability of successful block
    readonly SMR: number; // damage multiplier on block success (reduction factor)
    readonly FMR: number; // damage multiplier on block failure (reduction factor)
  };
  readonly dodge: {
    readonly SR: number; // probability of successful dodge
    readonly FMR: number; // damage multiplier on dodge failure
  };
  readonly parry: {
    readonly SR: number; // probability of successful parry
    readonly FMR: number; // damage multiplier on parry failure
  };
}

/**
 * An active buff or debuff applied to a combatant.
 */
export interface Buff {
  readonly type: string; // e.g., "block_sr_boost", "dodge_sr_debuff"
  readonly source: string; // combatant ID or elemental path that applied this
  readonly duration: number; // rounds remaining (-1 = permanent for this combat)
  readonly modifier: number; // numeric modifier value (positive = buff, negative = debuff)
}

/**
 * A debuff effect targeting a specific stat.
 */
export interface DebuffEffect {
  readonly stat: string; // stat being debuffed, e.g., "block_SR", "speed"
  readonly amount: number; // reduction amount
  readonly source: string; // combatant ID or path that applied this
}

/**
 * A single combatant in a combat encounter.
 * Tracks live combat stats (stamina, energy) separate from GameState character stats.
 */
export interface Combatant {
  readonly id: string;
  readonly name: string;
  readonly archetype: string; // matches NPC archetype or player class
  readonly rank: number; // decimal rank (e.g., 1.0, 2.5, 5.0)
  readonly stamina: number; // current stamina (0 = KO)
  readonly maxStamina: number;
  readonly power: number; // attack power stat
  readonly speed: number; // determines priority tie-breaking and Blindside
  readonly energy: number; // current energy segments
  readonly maxEnergy: number; // max energy segments for current ascension level
  readonly ascensionLevel: AscensionLevel;
  readonly activeBuffs: readonly Buff[];
  readonly elementalPath: ElementalPath;
  readonly reactionSkills: ReactionSkills;
  readonly isKO: boolean;
}

// ============================================================================
// Combat Actions
// ============================================================================

/**
 * A single combat action declared by a combatant.
 * Used for both player declarations and AI-generated actions.
 */
export interface CombatAction {
  readonly combatantId: string;
  readonly type: ActionType;
  readonly targetId: string | null; // null for EVADE; GROUP uses leader's targetId
  readonly energySegments?: number; // SPECIAL only: segments to spend
}

// ============================================================================
// Resolution Results
// ============================================================================

/**
 * Outcome of a defense roll for a single attack.
 */
export interface DefenseResult {
  readonly type: DefenseType;
  readonly success: boolean;
  readonly damageMultiplier: number; // final damage = rawDamage * damageMultiplier
}

/**
 * Result of a single per-attack resolution step.
 */
export interface AttackResult {
  readonly attackerId: string;
  readonly targetId: string;
  readonly damage: number; // final damage applied to target stamina
  readonly defenseType: DefenseType;
  readonly defenseOutcome: DefenseResult;
  readonly rankKO: boolean; // true if Rank KO roll succeeded
  readonly blindside: boolean; // true if Blindside roll succeeded
  readonly crushingBlow: boolean; // true if Crushing Blow roll succeeded
  readonly counterChain: boolean; // true if a counter chain was triggered
}

/**
 * A generic action result entry in the round history.
 * Wraps attack-specific details or other action outcomes.
 */
export interface ActionResult {
  readonly combatantId: string;
  readonly type: ActionType;
  readonly attackResult?: AttackResult; // present for ATTACK, SPECIAL, GROUP
}

/**
 * Complete record of one round's resolution.
 */
export interface RoundResult {
  readonly round: number;
  readonly actions: readonly ActionResult[];
  readonly stateSnapshot: CombatState; // state after this round completed
}

// ============================================================================
// Combat State
// ============================================================================

/**
 * Root combat state object. Independent from GameState per ADR-013.
 * All transitions produce a new CombatState (immutable spread pattern).
 */
export interface CombatState {
  readonly round: number;
  readonly phase: CombatPhase;
  readonly playerParty: readonly Combatant[];
  readonly enemyParty: readonly Combatant[];
  readonly actionQueue: readonly CombatAction[];
  readonly roundHistory: readonly RoundResult[];
  readonly status: 'active' | 'victory' | 'defeat';
}

// ============================================================================
// Encounter Configuration
// ============================================================================

/**
 * Initial configuration for a single combatant in an encounter.
 * Used to instantiate Combatant objects at encounter start.
 */
export interface CombatantConfig {
  readonly id: string;
  readonly name: string;
  readonly archetype: string;
  readonly rank: number;
  readonly stamina: number; // serves as both starting and max stamina
  readonly power: number;
  readonly speed: number;
  readonly elementalPath: ElementalPath;
  readonly reactionSkills: ReactionSkills;
  readonly ascensionLevel?: AscensionLevel; // defaults to 0 if omitted
}

/**
 * Static configuration for a combat encounter (loaded from fixture JSON).
 */
export interface EncounterConfig {
  readonly id: string;
  readonly name: string;
  readonly playerParty: readonly CombatantConfig[];
  readonly enemyParty: readonly CombatantConfig[];
}

// ============================================================================
// Group Action Types (from design_spec_group_action_type.md)
// ============================================================================

/**
 * What the GROUP leader declares in Phase 3.
 */
export interface GroupActionDeclaration {
  readonly leaderId: string;
  readonly targetId: string;
}

/**
 * Output of GROUP resolution, detailing damage breakdown and defense outcome.
 */
export interface GroupResolutionResult {
  readonly participantIds: readonly string[];
  readonly targetId: string;
  readonly individualDamages: Record<string, number>;
  readonly totalDamage: number; // sum of individual damages × multiplier
  readonly defenseResult: BlockDefenseResult;
  readonly finalDamage: number; // totalDamage after Block mitigation
}

/**
 * Simplified defense result for GROUP (Block only).
 */
export interface BlockDefenseResult {
  readonly type: 'block_success' | 'block_failure';
  readonly damageMultiplier: number; // SMR on success, FMR on failure
}

/**
 * Extensibility hook for future GROUP varieties.
 * POC default: { damageMultiplier: 1.5, energyRequirement: 'full' }
 */
export interface GroupActionConfig {
  readonly damageMultiplier: number;
  readonly energyRequirement: 'full';
}

// ============================================================================
// Behavior Tree AI Types (from design_spec_behavior_tree_ai_system.md)
// ============================================================================

/**
 * Score contribution per action type from a single scoring factor.
 * Values are typically in the -1.0 to 1.0 range.
 */
export type ActionScores = Record<ActionType, number>;

/**
 * A single evaluator that scores one aspect of the combat state.
 * Each factor returns scores for all 5 action types simultaneously.
 */
export interface ScoringFactor {
  readonly name: string;
  evaluate(self: CombatPerception, target: TargetPerception | null): ActionScores;
}

/**
 * Data-driven NPC combat personality profile.
 * Pure data — no behavior logic.
 */
export interface ArchetypeProfile {
  readonly name: string; // e.g., "elena_loyal_scout"
  readonly baseScores: ActionScores; // starting bias per action type
  readonly factorWeights: Record<string, number>; // weight multiplier per factor name
  readonly elementalPath: ElementalPath; // determines tie-break order
}

/**
 * Runtime feature toggles for the behavior tree evaluator.
 */
export interface EvaluatorConfig {
  readonly groupActionsEnabled: boolean; // false until Group Action implemented
}

/**
 * Intermediate evaluation result for one (actionType, target) candidate pair.
 */
export interface ScoredCandidate {
  readonly actionType: ActionType;
  readonly targetId: string | null;
  readonly score: number;
  readonly scoreBreakdown: Record<string, number>; // per-factor contribution
}

/**
 * Pre-computed readonly snapshot mediating between raw CombatState and factors.
 * Built once per NPC per round — eliminates redundant computation.
 */
export interface CombatPerception {
  // Self
  readonly selfId: string;
  readonly selfStaminaPct: number; // current / max (0.0 - 1.0)
  readonly selfEnergy: number; // current energy segments
  readonly selfAscension: number; // ascension level (0-3)
  readonly selfRank: number; // decimal rank (e.g., 2.5)
  readonly selfPath: ElementalPath;

  // Team (allies, excluding self)
  readonly allies: readonly AllyPerception[]; // sorted by stamina % ascending
  readonly lowestAllyStaminaPct: number; // min stamina % across non-KO'd allies
  readonly teamAvgStaminaPct: number; // average stamina % across non-KO'd allies + self
  readonly allyCount: number; // count of non-KO'd allies

  // Enemies
  readonly enemies: readonly EnemyPerception[]; // sorted by stamina % ascending
  readonly weakestEnemyStaminaPct: number; // min stamina % across non-KO'd enemies
  readonly enemyAvgStaminaPct: number; // average stamina % across non-KO'd enemies
  readonly enemyCount: number; // count of non-KO'd enemies

  // Context
  readonly round: number; // current round number (1-based)
}

/**
 * Perception of a single ally combatant.
 */
export interface AllyPerception {
  readonly id: string;
  readonly staminaPct: number;
  readonly isKO: boolean;
}

/**
 * Perception of a single enemy combatant.
 */
export interface EnemyPerception {
  readonly id: string;
  readonly staminaPct: number;
  readonly isKO: boolean;
  readonly speedDelta: number; // (self.speed - enemy.speed) / enemy.speed
  readonly rankDelta: number; // (self.rank - enemy.rank)
  readonly power: number; // for Crushing Blow assessment
}

/**
 * Perception of a specific target (ally or enemy) during factor evaluation.
 */
export interface TargetPerception {
  readonly id: string;
  readonly staminaPct: number;
  readonly speedDelta: number;
  readonly rankDelta: number;
  readonly power: number;
}

// ============================================================================
// Combat Constants
// ============================================================================

/**
 * Priority order for action resolution.
 * Lower number = resolves first.
 * GROUP(0) > DEFEND(1) > ATTACK/SPECIAL(2) > EVADE(3)
 *
 * Updated per design_spec_group_action_type.md — GROUP is priority 0.
 */
export const ACTION_PRIORITY: Record<ActionType, number> = {
  GROUP: 0,
  DEFEND: 1,
  ATTACK: 2,
  SPECIAL: 2,
  EVADE: 3,
} as const;

/**
 * Energy segments gained per combat event type and outcome.
 * Multiplied by ascension accumulation bonus to get final gain.
 */
export const ENERGY_GAINS: Record<string, number> = {
  actionSuccess: 1.0,
  actionFailure: 0.5,
  reactionSuccess: 0.5,
  reactionFailure: 0.25,
} as const;

/**
 * Total energy segment thresholds for ascension level advancement.
 * Ascending: 35 segments → level 1, 95 → level 2, 180 → level 3.
 */
export const ASCENSION_THRESHOLDS: readonly number[] = [35, 95, 180] as const;

/**
 * Starting energy segments at the beginning of each round per ascension level.
 * Index = ascension level (0–3).
 */
export const ASCENSION_STARTING_SEGMENTS: readonly number[] = [0, 0, 1, 2] as const;

/**
 * Accumulation bonus multiplier per ascension level.
 * energyGain * (1 + bonus). Index = ascension level (0–3).
 */
export const ASCENSION_ACCUMULATION_BONUS: readonly number[] = [0, 0.25, 0.25, 0.5] as const;
