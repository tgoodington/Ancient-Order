/**
 * Path-based tie-breaking for the behavior tree evaluator.
 *
 * When two candidates have identical scores, the combatant's elemental path
 * determines which action type takes priority. This generalizes naturally to
 * any future combatant without per-character configuration.
 *
 * Tie-breaking order:
 *   1. Higher score wins
 *   2. If tied: action type earlier in PATH_TIEBREAK[path] wins
 *   3. If still tied (same action type): target with lowest staminaPct wins
 */

import type { ActionType, ElementalPath } from '../../types/combat.js';
import type { ScoredCandidate } from '../../types/combat.js';

/**
 * Priority ordering of action types per elemental path.
 * Action paths (Fire, Shadow, Earth) lean offensive.
 * Reaction paths (Water, Air, Light) lean defensive.
 */
export const PATH_TIEBREAK: Record<ElementalPath, ActionType[]> = {
  // Action paths (offensive-leaning)
  Fire:   ['ATTACK', 'SPECIAL', 'DEFEND', 'EVADE', 'GROUP'],
  Shadow: ['SPECIAL', 'ATTACK', 'EVADE', 'DEFEND', 'GROUP'],
  Earth:  ['DEFEND', 'ATTACK', 'SPECIAL', 'EVADE', 'GROUP'],

  // Reaction paths (defensive-leaning)
  Water:  ['DEFEND', 'EVADE', 'SPECIAL', 'ATTACK', 'GROUP'],
  Air:    ['EVADE', 'DEFEND', 'SPECIAL', 'ATTACK', 'GROUP'],
  Light:  ['SPECIAL', 'DEFEND', 'EVADE', 'ATTACK', 'GROUP'],
};

/**
 * Get the tie-break priority index of an action type for the given path.
 * Lower index = higher priority.
 *
 * @param path - The combatant's elemental path
 * @param actionType - The action type to look up
 * @returns Index in the path's priority list (0 = highest priority)
 */
function tieBreakIndex(path: ElementalPath, actionType: ActionType): number {
  return PATH_TIEBREAK[path].indexOf(actionType);
}

/**
 * Apply path-based tie-breaking to a list of scored candidates that share
 * the top score. Returns the winner from the tied group.
 *
 * Algorithm:
 *   1. Among tied candidates, prefer the action type earliest in PATH_TIEBREAK[path]
 *   2. If still tied (same action type, different targets): prefer lowest staminaPct target
 *      (null targets, e.g. EVADE, are treated as staminaPct = 0 for comparison purposes)
 *
 * @param tied - Candidates that all share the top score (at least 1 element)
 * @param path - The evaluating combatant's elemental path
 * @param targetStaminaMap - Map of targetId → staminaPct for stamina-based tie-break
 * @returns The winning candidate
 */
export function resolveTie(
  tied: readonly ScoredCandidate[],
  path: ElementalPath,
  targetStaminaMap: Map<string, number>,
): ScoredCandidate {
  // Sort by tie-break index ascending, then by target stamina ascending
  const sorted = [...tied].sort((a, b) => {
    const indexA = tieBreakIndex(path, a.actionType);
    const indexB = tieBreakIndex(path, b.actionType);
    if (indexA !== indexB) return indexA - indexB;

    // Same action type: prefer lower target stamina (most vulnerable)
    const staminaA = a.targetId !== null ? (targetStaminaMap.get(a.targetId) ?? 0) : 0;
    const staminaB = b.targetId !== null ? (targetStaminaMap.get(b.targetId) ?? 0) : 0;
    return staminaA - staminaB;
  });

  return sorted[0];
}
