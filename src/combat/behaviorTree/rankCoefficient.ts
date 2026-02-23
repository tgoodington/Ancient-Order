/**
 * Rank-based decision quality scaling for the behavior tree evaluator.
 *
 * Low-rank combatants rely mostly on instinct (base scores dominate).
 * High-rank combatants apply full tactical awareness (factors contribute fully).
 *
 * rankCoefficient scales the factor contribution portion of the score formula:
 *   finalScore = baseScore + (factorContribution * rankCoefficient)
 */

/** Minimum coefficient — even rank-1 combatants use 20% factor influence. */
const RANK_FLOOR = 0.2;

/** Maximum rank value in the system (Legend tier). */
const RANK_MAX = 10.0;

/**
 * Compute the rank-based decision quality coefficient.
 *
 * Examples:
 * - Stone  (rank 1.0) → 0.20 (floor applied)
 * - Silver (rank 3.0) → 0.30
 * - Gold   (rank 5.0) → 0.50
 * - Diamond(rank 7.0) → 0.70
 * - Legend (rank 10.0)→ 1.00
 *
 * @param rank - Combatant's decimal rank (e.g., 2.5)
 * @returns Coefficient in range [RANK_FLOOR, 1.0]
 */
export function rankCoefficient(rank: number): number {
  return Math.max(RANK_FLOOR, rank / RANK_MAX);
}
