/**
 * Ancient Order - Personality System
 *
 * Handles personality trait adjustments with the following constraints:
 * - Each trait ranges from 5% to 35%
 * - All traits must sum to exactly 100%
 * - When traits are adjusted, others are redistributed proportionally
 *
 * All functions are pure (no side effects, no mutations).
 */

import { Personality, PersonalityTrait, PersonalityAdjustment } from '../types/index.js';

// ============================================================================
// Constants
// ============================================================================

export const MIN_TRAIT_VALUE = 5;
export const MAX_TRAIT_VALUE = 35;
export const TOTAL_PERCENTAGE = 100;
export const FLOAT_TOLERANCE = 0.01;

const PERSONALITY_TRAITS: PersonalityTrait[] = [
  'patience',
  'empathy',
  'cunning',
  'logic',
  'kindness',
  'charisma',
];

// ============================================================================
// Public API
// ============================================================================

/**
 * Gets the sum of all personality traits.
 */
export function getPersonalitySum(personality: Personality): number {
  return PERSONALITY_TRAITS.reduce((sum, trait) => sum + personality[trait], 0);
}

/**
 * Clamps a value to the valid trait range [MIN_TRAIT_VALUE, MAX_TRAIT_VALUE].
 */
export function clampTrait(value: number): number {
  return Math.max(MIN_TRAIT_VALUE, Math.min(MAX_TRAIT_VALUE, value));
}

/**
 * Validates that a personality object meets all constraints.
 * Checks: each trait in [5, 35] and sum equals 100.
 */
export function validatePersonality(personality: Personality): {
  valid: boolean;
  errors: string[];
} {
  const errors: string[] = [];

  for (const trait of PERSONALITY_TRAITS) {
    const value = personality[trait];
    if (value < MIN_TRAIT_VALUE) {
      errors.push(`${trait} (${value}) is below minimum ${MIN_TRAIT_VALUE}`);
    }
    if (value > MAX_TRAIT_VALUE) {
      errors.push(`${trait} (${value}) is above maximum ${MAX_TRAIT_VALUE}`);
    }
  }

  const sum = getPersonalitySum(personality);
  if (Math.abs(sum - TOTAL_PERCENTAGE) > FLOAT_TOLERANCE) {
    errors.push(`Sum (${sum.toFixed(2)}) does not equal ${TOTAL_PERCENTAGE}`);
  }

  return {
    valid: errors.length === 0,
    errors,
  };
}

/**
 * Creates a default balanced personality with all traits equal (~16.67%),
 * normalized so the sum equals exactly 100.
 */
export function createDefaultPersonality(): Personality {
  const baseValue = TOTAL_PERCENTAGE / PERSONALITY_TRAITS.length; // 16.666...
  const rounded = Math.round(baseValue * 100) / 100; // 16.67

  // Start with the rounded value for all traits
  const personality: Personality = {
    patience: rounded,
    empathy: rounded,
    cunning: rounded,
    logic: rounded,
    kindness: rounded,
    charisma: rounded,
  };

  // Correct any rounding error by adjusting the last trait (charisma)
  const sum = getPersonalitySum(personality);
  const diff = TOTAL_PERCENTAGE - sum;
  if (Math.abs(diff) > 0.001) {
    personality.charisma = Math.round((personality.charisma + diff) * 100) / 100;
  }

  return personality;
}

/**
 * Adjusts personality traits based on dialogue choices.
 *
 * Algorithm (3-pass normalization):
 *   Pass 1: Apply direct adjustments, clamp each trait to [5, 35].
 *   Pass 2: Calculate sum. Distribute difference proportionally across unadjusted
 *           traits (or evenly across all traits if all were adjusted). Re-clamp.
 *   Pass 3: Distribute any remaining rounding error equally across adjustable
 *           traits, re-clamp, then round each trait to 2 decimal places and
 *           apply a final micro-correction to guarantee sum = 100.
 *
 * @param current - The current personality (not mutated)
 * @param adjustments - Trait deltas to apply (e.g. { cunning: 6, empathy: -2 })
 * @returns A new Personality object with all constraints satisfied
 */
export function adjustPersonality(
  current: Readonly<Personality>,
  adjustments: PersonalityAdjustment
): Personality {
  // Work on a mutable copy
  const result: Personality = { ...current };

  const adjustedTraits = new Set<PersonalityTrait>(
    Object.keys(adjustments) as PersonalityTrait[]
  );

  // ---- Pass 1: Apply adjustments, clamp each trait -------------------------
  for (const trait of adjustedTraits) {
    const delta = adjustments[trait] ?? 0;
    result[trait] = clampTrait(result[trait] + delta);
  }

  // ---- Pass 2: Redistribute sum deficit/surplus ----------------------------
  const unadjustedTraits = PERSONALITY_TRAITS.filter((t) => !adjustedTraits.has(t));
  const redistributionTargets = unadjustedTraits.length > 0 ? unadjustedTraits : PERSONALITY_TRAITS;

  const sumAfterPass1 = getPersonalitySum(result);
  const difference = TOTAL_PERCENTAGE - sumAfterPass1;

  if (Math.abs(difference) > FLOAT_TOLERANCE) {
    const targetTotal = redistributionTargets.reduce((s, t) => s + result[t], 0);

    if (targetTotal > 0) {
      // Distribute proportionally
      for (const trait of redistributionTargets) {
        const proportion = result[trait] / targetTotal;
        result[trait] = clampTrait(result[trait] + difference * proportion);
      }
    } else {
      // Edge case: targets are all at minimum — distribute evenly
      const perTrait = difference / redistributionTargets.length;
      for (const trait of redistributionTargets) {
        result[trait] = clampTrait(result[trait] + perTrait);
      }
    }
  }

  // ---- Pass 3: Iterative cleanup for residual error, then round ------------
  // Iteratively distribute any remaining error (handles clamping side-effects)
  for (let iteration = 0; iteration < 10; iteration++) {
    const currentSum = getPersonalitySum(result);
    const residual = TOTAL_PERCENTAGE - currentSum;

    if (Math.abs(residual) <= FLOAT_TOLERANCE) {
      break;
    }

    // Find traits that have room to absorb the residual
    const absorbable = PERSONALITY_TRAITS.filter((trait) =>
      residual > 0 ? result[trait] < MAX_TRAIT_VALUE : result[trait] > MIN_TRAIT_VALUE
    );

    if (absorbable.length === 0) {
      break;
    }

    const perTrait = residual / absorbable.length;
    for (const trait of absorbable) {
      result[trait] = clampTrait(result[trait] + perTrait);
    }
  }

  // Round each trait to 2 decimal places
  for (const trait of PERSONALITY_TRAITS) {
    result[trait] = Math.round(result[trait] * 100) / 100;
  }

  // Final micro-correction: apply any residual rounding error to the largest trait,
  // then re-clamp to ensure the correction doesn't violate the [5, 35] bound.
  const finalSum = getPersonalitySum(result);
  const finalDiff = TOTAL_PERCENTAGE - finalSum;
  if (Math.abs(finalDiff) > 0.001) {
    const largestTrait = PERSONALITY_TRAITS.reduce((max, trait) =>
      result[trait] > result[max] ? trait : max
    );
    result[largestTrait] = clampTrait(
      Math.round((result[largestTrait] + finalDiff) * 100) / 100
    );
  }

  return result;
}
