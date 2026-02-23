/**
 * Factor registry — exports all scoring factors for the behavior tree evaluator.
 *
 * The evaluator iterates this array in order. Factor evaluation order does NOT
 * affect final scores (each factor is independent per design spec invariants).
 *
 * To add a new factor: implement it as a ScoringFactor object, import it here,
 * and append to the FACTORS array.
 */

import type { ScoringFactor } from '../../../types/combat.js';

import { ownStaminaFactor } from './ownStamina.js';
import { allyInDangerFactor } from './allyInDanger.js';
import { targetVulnerabilityFactor } from './targetVulnerability.js';
import { energyAvailabilityFactor } from './energyAvailability.js';
import { speedAdvantageFactor } from './speedAdvantage.js';
import { roundPhaseFactor } from './roundPhase.js';
import { teamBalanceFactor } from './teamBalance.js';

/**
 * All 7 scoring factors used by the evaluator.
 * Each factor contributes ActionScores weighted by the archetype profile.
 */
export const FACTORS: readonly ScoringFactor[] = [
  ownStaminaFactor,
  allyInDangerFactor,
  targetVulnerabilityFactor,
  energyAvailabilityFactor,
  speedAdvantageFactor,
  roundPhaseFactor,
  teamBalanceFactor,
] as const;
