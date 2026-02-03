/**
 * Ancient Order - Personality System
 *
 * Handles personality trait adjustments with the following constraints:
 * - Each trait ranges from 5% to 35%
 * - All traits must sum to exactly 100%
 * - When traits are adjusted, others are redistributed proportionally
 */

import { Personality, PersonalityTrait, PersonalityAdjustment } from '../types';

// Constants
const MIN_TRAIT_VALUE = 5;
const MAX_TRAIT_VALUE = 35;
const TOTAL_PERCENTAGE = 100;
const FLOAT_TOLERANCE = 0.01;

const PERSONALITY_TRAITS: PersonalityTrait[] = [
  'patience', 'empathy', 'cunning', 'logic', 'kindness', 'charisma'
];

/**
 * Validates that a personality object meets all constraints.
 */
export function validatePersonality(personality: Personality): {
  valid: boolean;
  errors: string[];
} {
  const errors: string[] = [];

  // Check each trait is within bounds
  for (const trait of PERSONALITY_TRAITS) {
    const value = personality[trait];
    if (value < MIN_TRAIT_VALUE) {
      errors.push(`${trait} (${value}) is below minimum ${MIN_TRAIT_VALUE}`);
    }
    if (value > MAX_TRAIT_VALUE) {
      errors.push(`${trait} (${value}) is above maximum ${MAX_TRAIT_VALUE}`);
    }
  }

  // Check sum equals 100
  const sum = getPersonalitySum(personality);
  if (Math.abs(sum - TOTAL_PERCENTAGE) > FLOAT_TOLERANCE) {
    errors.push(`Sum (${sum.toFixed(2)}) does not equal ${TOTAL_PERCENTAGE}`);
  }

  return {
    valid: errors.length === 0,
    errors
  };
}

/**
 * Gets the sum of all personality traits.
 */
export function getPersonalitySum(personality: Personality): number {
  return PERSONALITY_TRAITS.reduce((sum, trait) => sum + personality[trait], 0);
}

/**
 * Clamps a value to the valid trait range [5, 35].
 */
function clampTrait(value: number): number {
  return Math.max(MIN_TRAIT_VALUE, Math.min(MAX_TRAIT_VALUE, value));
}

/**
 * Creates a new personality object with all traits rounded to 1 decimal place.
 */
function roundPersonality(personality: Personality): Personality {
  const rounded: Personality = {
    patience: Math.round(personality.patience * 10) / 10,
    empathy: Math.round(personality.empathy * 10) / 10,
    cunning: Math.round(personality.cunning * 10) / 10,
    logic: Math.round(personality.logic * 10) / 10,
    kindness: Math.round(personality.kindness * 10) / 10,
    charisma: Math.round(personality.charisma * 10) / 10,
  };

  // Final normalization to ensure sum = 100 after rounding
  const sum = getPersonalitySum(rounded);
  const diff = TOTAL_PERCENTAGE - sum;

  if (Math.abs(diff) > 0.001) {
    // Apply tiny correction to the largest trait (least visible change)
    const largestTrait = PERSONALITY_TRAITS.reduce((max, trait) =>
      rounded[trait] > rounded[max] ? trait : max
    );
    rounded[largestTrait] = Math.round((rounded[largestTrait] + diff) * 10) / 10;
  }

  return rounded;
}

/**
 * Adjusts personality traits based on dialogue choices.
 *
 * Algorithm:
 * 1. Apply direct adjustments to specified traits (capped at 5-35%)
 * 2. Calculate how much the total deviates from 100%
 * 3. Redistribute the difference proportionally among non-adjusted traits
 * 4. If redistribution causes bounds violations, iteratively rebalance
 * 5. Round and normalize to ensure exact 100% sum
 *
 * @param currentPersonality - The current personality state
 * @param adjustments - The trait changes to apply (e.g., { cunning: 6, empathy: -2 })
 * @returns A new Personality object (original is not mutated)
 */
export function adjustPersonality(
  currentPersonality: Personality,
  adjustments: PersonalityAdjustment
): Personality {
  // Start with a copy of current personality
  const newPersonality: Personality = { ...currentPersonality };

  // Track which traits were directly adjusted
  const adjustedTraits = new Set<PersonalityTrait>(
    Object.keys(adjustments) as PersonalityTrait[]
  );

  // Step 1: Apply direct adjustments (with bounds clamping)
  for (const trait of adjustedTraits) {
    const adjustment = adjustments[trait] ?? 0;
    newPersonality[trait] = clampTrait(newPersonality[trait] + adjustment);
  }

  // Step 2: Calculate how far off from 100% we are
  let currentSum = getPersonalitySum(newPersonality);
  let difference = TOTAL_PERCENTAGE - currentSum;

  // Step 3: Redistribute among non-adjusted traits proportionally
  const unadjustedTraits = PERSONALITY_TRAITS.filter(t => !adjustedTraits.has(t));

  if (unadjustedTraits.length > 0 && Math.abs(difference) > FLOAT_TOLERANCE) {
    // Calculate total of unadjusted traits for proportional distribution
    const unadjustedTotal = unadjustedTraits.reduce(
      (sum, trait) => sum + newPersonality[trait], 0
    );

    if (unadjustedTotal > 0) {
      // Distribute difference proportionally based on current values
      for (const trait of unadjustedTraits) {
        const proportion = newPersonality[trait] / unadjustedTotal;
        const change = difference * proportion;
        newPersonality[trait] = clampTrait(newPersonality[trait] + change);
      }
    } else {
      // Edge case: all unadjusted traits are at minimum, distribute evenly
      const perTrait = difference / unadjustedTraits.length;
      for (const trait of unadjustedTraits) {
        newPersonality[trait] = clampTrait(newPersonality[trait] + perTrait);
      }
    }
  }

  // Step 4: Iterative rebalancing if we're still off
  // This handles cases where clamping prevented full redistribution
  for (let iteration = 0; iteration < 10; iteration++) {
    currentSum = getPersonalitySum(newPersonality);
    difference = TOTAL_PERCENTAGE - currentSum;

    if (Math.abs(difference) <= FLOAT_TOLERANCE) {
      break;
    }

    // Find traits that can absorb the difference
    const adjustableTraits = PERSONALITY_TRAITS.filter(trait => {
      if (difference > 0) {
        // Need to increase: trait must be below max
        return newPersonality[trait] < MAX_TRAIT_VALUE;
      } else {
        // Need to decrease: trait must be above min
        return newPersonality[trait] > MIN_TRAIT_VALUE;
      }
    });

    if (adjustableTraits.length === 0) {
      // Cannot adjust further (all at bounds) - this shouldn't happen with valid input
      break;
    }

    // Distribute remaining difference evenly among adjustable traits
    const perTrait = difference / adjustableTraits.length;
    for (const trait of adjustableTraits) {
      newPersonality[trait] = clampTrait(newPersonality[trait] + perTrait);
    }
  }

  // Step 5: Round and ensure exact 100% sum
  return roundPersonality(newPersonality);
}

/**
 * Creates a default balanced personality (all traits equal).
 */
export function createDefaultPersonality(): Personality {
  const value = TOTAL_PERCENTAGE / PERSONALITY_TRAITS.length; // ~16.67
  return {
    patience: Math.round(value * 10) / 10,
    empathy: Math.round(value * 10) / 10,
    cunning: Math.round(value * 10) / 10,
    logic: Math.round(value * 10) / 10,
    kindness: Math.round(value * 10) / 10,
    charisma: Math.round(value * 10) / 10 + 0.1, // Adjust to hit exactly 100
  };
}

/**
 * Creates a custom personality with the given values.
 * Validates and normalizes to ensure constraints are met.
 */
export function createPersonality(values: Partial<Personality>): Personality {
  const base = createDefaultPersonality();
  const personality: Personality = { ...base, ...values };

  // Normalize to ensure sum = 100%
  const sum = getPersonalitySum(personality);
  if (Math.abs(sum - TOTAL_PERCENTAGE) > FLOAT_TOLERANCE) {
    const factor = TOTAL_PERCENTAGE / sum;
    for (const trait of PERSONALITY_TRAITS) {
      personality[trait] = clampTrait(personality[trait] * factor);
    }
  }

  return roundPersonality(personality);
}

/**
 * Gets personality categories with totals.
 */
export function getPersonalityCategories(personality: Personality) {
  return {
    wisdom: {
      patience: personality.patience,
      empathy: personality.empathy,
      total: personality.patience + personality.empathy,
    },
    intelligence: {
      cunning: personality.cunning,
      logic: personality.logic,
      total: personality.cunning + personality.logic,
    },
    charisma: {
      kindness: personality.kindness,
      charisma: personality.charisma,
      total: personality.kindness + personality.charisma,
    },
  };
}

/**
 * Compares two personality objects and returns the differences.
 */
export function getPersonalityDiff(
  before: Personality,
  after: Personality
): PersonalityAdjustment {
  const diff: PersonalityAdjustment = {};

  for (const trait of PERSONALITY_TRAITS) {
    const change = after[trait] - before[trait];
    if (Math.abs(change) > FLOAT_TOLERANCE) {
      diff[trait] = Math.round(change * 10) / 10;
    }
  }

  return diff;
}
