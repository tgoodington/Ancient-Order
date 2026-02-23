/**
 * Behavior Tree Evaluator — Main entry point for NPC combat AI.
 *
 * Implements a utility-scoring system: every (actionType, target) candidate pair
 * receives a numeric score assembled from 7 independent factors weighted by the
 * combatant's archetype profile. The highest-scoring candidate wins.
 *
 * Design decisions:
 * - Utility scoring over classic behavior tree: transparent, testable, data-driven.
 * - Multi-output factors: each factor returns scores for all 5 action types at once.
 * - Combined (action, target) scoring: no two-phase separation needed.
 * - Rank coefficient: low-rank NPCs rely on instinct; high-rank use full tactics.
 * - Path tie-breaking: elemental path determines action priority in ties.
 *
 * Public interface: evaluate(combatant, state, config?) → CombatAction
 * Internal flow:    profile → perception → rank coeff → candidates → score → tie-break → output
 */

import type {
  Combatant,
  CombatState,
  CombatAction,
  ActionType,
  EvaluatorConfig,
  ScoredCandidate,
  TargetPerception,
  EnemyPerception,
  AllyPerception,
} from '../../types/combat.js';
import { buildPerception } from './perception.js';
import { rankCoefficient } from './rankCoefficient.js';
import { resolveTie } from './tieBreaking.js';
import { FACTORS } from './factors/index.js';
import { getProfile } from './profiles/index.js';

/** Default evaluator configuration — GROUP disabled until Task 19 integration. */
const DEFAULT_CONFIG: EvaluatorConfig = {
  groupActionsEnabled: false,
};

/**
 * Determine which action types are valid for the current combatant in this state.
 * SPECIAL requires energy > 0. GROUP requires config flag.
 */
function getValidActionTypes(
  selfEnergy: number,
  config: EvaluatorConfig,
): ActionType[] {
  const types: ActionType[] = ['ATTACK', 'DEFEND', 'EVADE'];
  if (selfEnergy > 0) types.push('SPECIAL');
  if (config.groupActionsEnabled) types.push('GROUP');
  return types;
}

/**
 * Convert an EnemyPerception to a TargetPerception for factor evaluation.
 */
function enemyToTarget(enemy: EnemyPerception): TargetPerception {
  return {
    id: enemy.id,
    staminaPct: enemy.staminaPct,
    speedDelta: enemy.speedDelta,
    rankDelta: enemy.rankDelta,
    power: enemy.power,
  };
}

/**
 * Convert an AllyPerception to a TargetPerception for factor evaluation.
 * Allies don't have speedDelta/rankDelta/power from self's perspective in the
 * perception snapshot, so we use neutral defaults.
 */
function allyToTarget(ally: AllyPerception): TargetPerception {
  return {
    id: ally.id,
    staminaPct: ally.staminaPct,
    speedDelta: 0,
    rankDelta: 0,
    power: 0,
  };
}

/**
 * Evaluate the best combat action for a non-KO'd NPC combatant.
 *
 * Algorithm:
 *   1. Load archetype profile by combatant.archetype
 *   2. buildPerception(combatant, state) → CombatPerception
 *   3. rankCoefficient(combatant.rank) → coefficient (0.2–1.0)
 *   4. Filter valid action types
 *   5. For each valid (actionType, target) pair:
 *        score = baseScore + Σ(weight × factor.evaluate(perception, target)) × coefficient
 *   6. Sort candidates by score descending
 *   7. Tie-break: PATH_TIEBREAK then lowest target stamina
 *   8. Return CombatAction
 *
 * @param combatant - The NPC making a decision (must not be KO'd)
 * @param state - Current combat state (read-only)
 * @param config - Feature flags (defaults: groupActionsEnabled=false)
 * @returns A valid CombatAction for this combatant
 */
export function evaluate(
  combatant: Combatant,
  state: CombatState,
  config: EvaluatorConfig = DEFAULT_CONFIG,
): CombatAction {
  // Step 1: load profile
  const profile = getProfile(combatant.archetype);
  if (!profile) {
    throw new Error(
      `No archetype profile found for archetype: "${combatant.archetype}". ` +
      `Register it in profiles/index.ts.`,
    );
  }

  // Step 2: build perception snapshot
  const perception = buildPerception(combatant, state);

  // Step 3: rank coefficient
  const coeff = rankCoefficient(combatant.rank);

  // Step 4: valid action types
  const validActionTypes = getValidActionTypes(combatant.energy, config);

  // Step 5: generate and score all (actionType, target) candidates
  const candidates: ScoredCandidate[] = [];

  for (const actionType of validActionTypes) {
    // Determine valid targets for this action type
    let targetList: Array<{ targetId: string | null; target: TargetPerception | null }> = [];

    if (actionType === 'ATTACK' || actionType === 'SPECIAL') {
      // Offensive actions: target non-KO'd enemies
      const liveEnemies = perception.enemies.filter((e) => !e.isKO);
      targetList = liveEnemies.map((e) => ({
        targetId: e.id,
        target: enemyToTarget(e),
      }));
    } else if (actionType === 'DEFEND') {
      // Defensive support: target non-KO'd allies
      const liveAllies = perception.allies.filter((a) => !a.isKO);
      targetList = liveAllies.map((a) => ({
        targetId: a.id,
        target: allyToTarget(a),
      }));
    } else if (actionType === 'EVADE' || actionType === 'GROUP') {
      // Self/team actions: no specific target
      targetList = [{ targetId: null, target: null }];
    }

    // If no valid targets exist for this action type, skip it entirely
    if (targetList.length === 0) continue;

    for (const { targetId, target } of targetList) {
      // Start with the profile's base score for this action type
      const baseScore = profile.baseScores[actionType];

      // Accumulate factor contributions
      let factorContribution = 0;
      const scoreBreakdown: Record<string, number> = {};

      for (const factor of FACTORS) {
        const factorScores = factor.evaluate(perception, target);
        const weight = profile.factorWeights[factor.name] ?? 0;
        const contribution = weight * factorScores[actionType];
        factorContribution += contribution;
        scoreBreakdown[factor.name] = contribution;
      }

      // Apply rank coefficient to factor contribution only (base scores are always full)
      const finalScore = baseScore + factorContribution * coeff;

      candidates.push({
        actionType,
        targetId,
        score: finalScore,
        scoreBreakdown,
      });
    }
  }

  // Edge case: no valid candidates generated (shouldn't happen — EVADE is always valid)
  // but as a safety net, return EVADE with no target
  if (candidates.length === 0) {
    return {
      combatantId: combatant.id,
      type: 'EVADE',
      targetId: null,
    };
  }

  // Step 6: sort by score descending
  candidates.sort((a, b) => b.score - a.score);

  // Step 7: collect all candidates tied with the top score
  const topScore = candidates[0].score;
  const tied = candidates.filter((c) => c.score === topScore);

  // Build a target stamina map for tie-breaking
  const targetStaminaMap = new Map<string, number>();
  for (const enemy of perception.enemies) {
    targetStaminaMap.set(enemy.id, enemy.staminaPct);
  }
  for (const ally of perception.allies) {
    targetStaminaMap.set(ally.id, ally.staminaPct);
  }

  // Apply path-based tie-breaking
  const winner =
    tied.length === 1
      ? tied[0]
      : resolveTie(tied, profile.elementalPath, targetStaminaMap);

  // Step 8: build and return CombatAction
  const action: CombatAction = {
    combatantId: combatant.id,
    type: winner.actionType,
    targetId: winner.targetId,
  };

  // For SPECIAL: use all available energy segments (per design spec edge case)
  if (winner.actionType === 'SPECIAL') {
    return { ...action, energySegments: combatant.energy };
  }

  return action;
}
