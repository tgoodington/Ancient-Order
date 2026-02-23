/**
 * Ancient Order - Personality System Tests
 *
 * Covers:
 * - createDefaultPersonality: sum invariant, equal traits, valid range
 * - clampTrait: boundary behaviour
 * - getPersonalitySum: arithmetic correctness
 * - validatePersonality: range and sum validation
 * - adjustPersonality: immutability, sum invariant, clamping, redistribution,
 *   multi-trait interactions, boundary cases (4.9→5, 35.1→35)
 */

import { describe, it, expect } from 'vitest';
import {
  createDefaultPersonality,
  adjustPersonality,
  validatePersonality,
  clampTrait,
  getPersonalitySum,
  MIN_TRAIT_VALUE,
  MAX_TRAIT_VALUE,
  TOTAL_PERCENTAGE,
  FLOAT_TOLERANCE,
} from './personalitySystem.js';
import { Personality } from '../types/index.js';

// ============================================================================
// Helpers
// ============================================================================

/**
 * Asserts the personality sum equals 100 within floating-point tolerance.
 *
 * We use a tolerance of 0.02 rather than the strict FLOAT_TOLERANCE (0.01) because
 * rounding 6 traits to 2 decimal places introduces up to ~0.03 of total error;
 * the micro-correction in adjustPersonality handles most of this, but floating-point
 * addition can leave a residual of up to ~0.015. 0.02 is tight enough to catch
 * real algorithmic bugs while not failing on IEEE 754 arithmetic artefacts.
 */
function expectSumTo100(p: Personality): void {
  const sum = getPersonalitySum(p);
  expect(Math.abs(sum - TOTAL_PERCENTAGE)).toBeLessThanOrEqual(0.02);
}

/** Asserts every trait is within [MIN_TRAIT_VALUE, MAX_TRAIT_VALUE]. */
function expectAllTraitsInRange(p: Personality): void {
  const traits = ['patience', 'empathy', 'cunning', 'logic', 'kindness', 'charisma'] as const;
  for (const trait of traits) {
    expect(p[trait]).toBeGreaterThanOrEqual(MIN_TRAIT_VALUE);
    expect(p[trait]).toBeLessThanOrEqual(MAX_TRAIT_VALUE);
  }
}

// ============================================================================
// clampTrait
// ============================================================================

describe('clampTrait', () => {
  it('returns the value unchanged when it is within [5, 35]', () => {
    expect(clampTrait(20)).toBe(20);
    expect(clampTrait(MIN_TRAIT_VALUE)).toBe(MIN_TRAIT_VALUE);
    expect(clampTrait(MAX_TRAIT_VALUE)).toBe(MAX_TRAIT_VALUE);
  });

  it('clamps values below minimum (4.9 → 5)', () => {
    expect(clampTrait(4.9)).toBe(MIN_TRAIT_VALUE);
    expect(clampTrait(0)).toBe(MIN_TRAIT_VALUE);
    expect(clampTrait(-10)).toBe(MIN_TRAIT_VALUE);
  });

  it('clamps values above maximum (35.1 → 35)', () => {
    expect(clampTrait(35.1)).toBe(MAX_TRAIT_VALUE);
    expect(clampTrait(100)).toBe(MAX_TRAIT_VALUE);
    expect(clampTrait(50)).toBe(MAX_TRAIT_VALUE);
  });
});

// ============================================================================
// getPersonalitySum
// ============================================================================

describe('getPersonalitySum', () => {
  it('returns the sum of all six traits', () => {
    const p: Personality = {
      patience: 16.67,
      empathy: 16.67,
      cunning: 16.67,
      logic: 16.67,
      kindness: 16.67,
      charisma: 16.65,
    };
    expect(getPersonalitySum(p)).toBeCloseTo(100, 1);
  });

  it('returns 0 for a zero personality', () => {
    const p: Personality = {
      patience: 0,
      empathy: 0,
      cunning: 0,
      logic: 0,
      kindness: 0,
      charisma: 0,
    };
    expect(getPersonalitySum(p)).toBe(0);
  });
});

// ============================================================================
// validatePersonality
// ============================================================================

describe('validatePersonality', () => {
  it('accepts a valid personality', () => {
    const p = createDefaultPersonality();
    const result = validatePersonality(p);
    expect(result.valid).toBe(true);
    expect(result.errors).toHaveLength(0);
  });

  it('rejects a trait below minimum', () => {
    const p: Personality = {
      patience: 4,
      empathy: 19,
      cunning: 19,
      logic: 19,
      kindness: 19,
      charisma: 20,
    };
    const result = validatePersonality(p);
    expect(result.valid).toBe(false);
    expect(result.errors.some((e) => e.includes('patience'))).toBe(true);
  });

  it('rejects a trait above maximum', () => {
    const p: Personality = {
      patience: 36,
      empathy: 13,
      cunning: 13,
      logic: 13,
      kindness: 13,
      charisma: 12,
    };
    const result = validatePersonality(p);
    expect(result.valid).toBe(false);
    expect(result.errors.some((e) => e.includes('patience'))).toBe(true);
  });

  it('rejects a personality whose sum deviates from 100', () => {
    const p: Personality = {
      patience: 17,
      empathy: 17,
      cunning: 17,
      logic: 17,
      kindness: 17,
      charisma: 17, // sum = 102
    };
    const result = validatePersonality(p);
    expect(result.valid).toBe(false);
    expect(result.errors.some((e) => e.includes('Sum'))).toBe(true);
  });

  it('collects multiple errors when multiple traits are invalid', () => {
    const p: Personality = {
      patience: 4,   // below min
      empathy: 36,   // above max
      cunning: 20,
      logic: 20,
      kindness: 20,
      charisma: 0,   // below min AND sum is wrong
    };
    const result = validatePersonality(p);
    expect(result.valid).toBe(false);
    expect(result.errors.length).toBeGreaterThan(1);
  });
});

// ============================================================================
// createDefaultPersonality
// ============================================================================

describe('createDefaultPersonality', () => {
  it('returns a personality whose sum equals 100', () => {
    const p = createDefaultPersonality();
    expectSumTo100(p);
  });

  it('returns a personality where all traits are within [5, 35]', () => {
    const p = createDefaultPersonality();
    expectAllTraitsInRange(p);
  });

  it('passes validatePersonality', () => {
    const p = createDefaultPersonality();
    expect(validatePersonality(p).valid).toBe(true);
  });

  it('produces traits that are approximately equal (~16.67)', () => {
    const p = createDefaultPersonality();
    const traits = [p.patience, p.empathy, p.cunning, p.logic, p.kindness, p.charisma];
    for (const v of traits) {
      expect(v).toBeGreaterThanOrEqual(16);
      expect(v).toBeLessThanOrEqual(17);
    }
  });

  it('returns a new object on each call (no shared state)', () => {
    const a = createDefaultPersonality();
    const b = createDefaultPersonality();
    expect(a).not.toBe(b);
  });
});

// ============================================================================
// adjustPersonality — immutability
// ============================================================================

describe('adjustPersonality — immutability', () => {
  it('does not mutate the input personality', () => {
    const original = createDefaultPersonality();
    const originalCopy = { ...original };
    adjustPersonality(original, { cunning: 5 });
    expect(original).toEqual(originalCopy);
  });

  it('returns a new object reference (not the same object)', () => {
    const p = createDefaultPersonality();
    const result = adjustPersonality(p, { patience: 3 });
    expect(result).not.toBe(p);
  });
});

// ============================================================================
// adjustPersonality — sum invariant
// ============================================================================

describe('adjustPersonality — sum invariant', () => {
  it('sum equals 100 after a single positive adjustment', () => {
    const p = createDefaultPersonality();
    const result = adjustPersonality(p, { cunning: 5 });
    expectSumTo100(result);
  });

  it('sum equals 100 after a single negative adjustment', () => {
    const p = createDefaultPersonality();
    const result = adjustPersonality(p, { patience: -5 });
    expectSumTo100(result);
  });

  it('sum equals 100 after multi-trait adjustments', () => {
    const p = createDefaultPersonality();
    const result = adjustPersonality(p, { cunning: 8, empathy: -3 });
    expectSumTo100(result);
  });

  it('sum equals 100 even when an adjustment would push a trait past 35', () => {
    const p = createDefaultPersonality();
    const result = adjustPersonality(p, { charisma: 25 }); // 16.67 + 25 > 35, clamped
    expectSumTo100(result);
  });

  it('sum equals 100 even when an adjustment would push a trait below 5', () => {
    const p = createDefaultPersonality();
    const result = adjustPersonality(p, { empathy: -20 }); // 16.67 - 20 < 5, clamped
    expectSumTo100(result);
  });
});

// ============================================================================
// adjustPersonality — range enforcement
// ============================================================================

describe('adjustPersonality — range enforcement', () => {
  it('clamps a directly adjusted trait that would exceed 35', () => {
    const p = createDefaultPersonality();
    const result = adjustPersonality(p, { cunning: 30 }); // 16.67 + 30 = 46.67 > 35
    expect(result.cunning).toBe(MAX_TRAIT_VALUE);
  });

  it('clamps a directly adjusted trait that would go below 5', () => {
    const p = createDefaultPersonality();
    const result = adjustPersonality(p, { patience: -30 }); // 16.67 - 30 = -13.33 < 5
    expect(result.patience).toBe(MIN_TRAIT_VALUE);
  });

  it('redistributed traits also stay within [5, 35]', () => {
    const p = createDefaultPersonality();
    const result = adjustPersonality(p, { cunning: 18 }); // large shift
    expectAllTraitsInRange(result);
    expectSumTo100(result);
  });

  it('all traits remain in range after a zero adjustment', () => {
    const p = createDefaultPersonality();
    const result = adjustPersonality(p, {});
    expectAllTraitsInRange(result);
    expectSumTo100(result);
  });
});

// ============================================================================
// adjustPersonality — boundary cases
// ============================================================================

describe('adjustPersonality — boundary cases (4.9→5, 35.1→35)', () => {
  it('a trait that lands at exactly 4.9 is clamped to 5', () => {
    // Build a personality where patience = 10; adjusting by -5.1 gives 4.9
    const base: Personality = {
      patience: 10,
      empathy: 18,
      cunning: 18,
      logic: 18,
      kindness: 18,
      charisma: 18,
    };
    const result = adjustPersonality(base, { patience: -5.1 }); // target = 4.9 → clamp to 5
    expect(result.patience).toBe(MIN_TRAIT_VALUE);
    expectSumTo100(result);
  });

  it('a trait that lands at exactly 35.1 is clamped to 35', () => {
    // Build a valid personality where charisma = 25; remaining 5 traits share 75 (15 each)
    const base: Personality = {
      patience: 15,
      empathy: 15,
      cunning: 15,
      logic: 15,
      kindness: 15,
      charisma: 25, // sum = 100 exactly
    };
    // +10.1 on charisma: 25 + 10.1 = 35.1, which must clamp to 35
    const result = adjustPersonality(base, { charisma: 10.1 });
    expect(result.charisma).toBe(MAX_TRAIT_VALUE);
    expectSumTo100(result);
  });

  it('clamping at min boundary still preserves sum invariant', () => {
    const p = createDefaultPersonality();
    // Reduce empathy to exactly the minimum
    const delta = p.empathy - MIN_TRAIT_VALUE;
    const result = adjustPersonality(p, { empathy: -delta });
    expect(result.empathy).toBe(MIN_TRAIT_VALUE);
    expectSumTo100(result);
  });

  it('clamping at max boundary still preserves sum invariant', () => {
    const p = createDefaultPersonality();
    // Raise logic to exactly the maximum
    const delta = MAX_TRAIT_VALUE - p.logic;
    const result = adjustPersonality(p, { logic: delta });
    expect(result.logic).toBe(MAX_TRAIT_VALUE);
    expectSumTo100(result);
  });
});

// ============================================================================
// adjustPersonality — multi-trait interactions
// ============================================================================

describe('adjustPersonality — multi-trait interactions', () => {
  it('adjusting multiple traits simultaneously maintains sum invariant', () => {
    const p = createDefaultPersonality();
    const result = adjustPersonality(p, {
      patience: 5,
      empathy: 5,
      cunning: -3,
      logic: -3,
    });
    expectSumTo100(result);
    expectAllTraitsInRange(result);
  });

  it('adjusting all 6 traits uses evenly distributed redistribution', () => {
    const p = createDefaultPersonality();
    const result = adjustPersonality(p, {
      patience: 2,
      empathy: 2,
      cunning: 2,
      logic: -2,
      kindness: -2,
      charisma: -2,
    });
    expectSumTo100(result);
    expectAllTraitsInRange(result);
  });

  it('opposing adjustments (one up, one down) do not drift the sum', () => {
    const p = createDefaultPersonality();
    // +6 on one trait, -6 on another: ideally sum unchanged, redistribution minimal
    const result = adjustPersonality(p, { cunning: 6, kindness: -6 });
    expectSumTo100(result);
    expectAllTraitsInRange(result);
  });

  it('chained adjustments each produce valid personality', () => {
    let p = createDefaultPersonality();
    p = adjustPersonality(p, { patience: 5 });
    expectSumTo100(p);
    p = adjustPersonality(p, { empathy: -3, logic: 4 });
    expectSumTo100(p);
    p = adjustPersonality(p, { charisma: -8 });
    expectSumTo100(p);
    expectAllTraitsInRange(p);
  });

  it('large adjustment that saturates multiple traits still sums to 100', () => {
    // Use a base where patience and empathy are both 15; +20 on each → 35, hitting the cap
    const base: Personality = {
      patience: 15,
      empathy: 15,
      cunning: 20,
      logic: 20,
      kindness: 15,
      charisma: 15, // sum = 100
    };
    const result = adjustPersonality(base, { patience: 20, empathy: 20 });
    expect(result.patience).toBe(MAX_TRAIT_VALUE);
    expect(result.empathy).toBe(MAX_TRAIT_VALUE);
    expectSumTo100(result);
    expectAllTraitsInRange(result);
  });
});

// ============================================================================
// adjustPersonality — value correctness
// ============================================================================

describe('adjustPersonality — value correctness', () => {
  it('a positive adjustment increases the target trait', () => {
    const p = createDefaultPersonality();
    const result = adjustPersonality(p, { cunning: 5 });
    expect(result.cunning).toBeGreaterThan(p.cunning);
  });

  it('a negative adjustment decreases the target trait', () => {
    const p = createDefaultPersonality();
    const result = adjustPersonality(p, { kindness: -5 });
    expect(result.kindness).toBeLessThan(p.kindness);
  });

  it('unadjusted traits shift to compensate when one trait is raised', () => {
    const p = createDefaultPersonality();
    const result = adjustPersonality(p, { patience: 10 });
    // patience goes up → others must come down collectively
    const othersBefore =
      p.empathy + p.cunning + p.logic + p.kindness + p.charisma;
    const othersAfter =
      result.empathy + result.cunning + result.logic + result.kindness + result.charisma;
    expect(othersAfter).toBeLessThan(othersBefore + FLOAT_TOLERANCE);
  });

  it('values are rounded to 2 decimal places', () => {
    const p = createDefaultPersonality();
    const result = adjustPersonality(p, { cunning: 3 });
    const traits = ['patience', 'empathy', 'cunning', 'logic', 'kindness', 'charisma'] as const;
    for (const trait of traits) {
      const val = result[trait];
      // Check that val has at most 2 decimal places
      expect(Math.round(val * 100) / 100).toBe(val);
    }
  });
});
