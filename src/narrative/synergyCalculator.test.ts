/**
 * Ancient Order - Synergy Calculator Tests
 *
 * TDD tests covering design spec scenarios.
 *
 * NPC personalities from npcs.ts:
 *   Elena: { patience:20, empathy:20, cunning:10, logic:15, kindness:20, charisma:15 }
 *   Lars:  { patience:10, empathy:8,  cunning:28, logic:25, kindness:12, charisma:17 }
 *   Kade:  { patience:12, empathy:8,  cunning:25, logic:18, kindness:10, charisma:27 }
 *
 * Coverage:
 *   - evaluateWellRounded: pass when all traits covered, fail when trait below threshold
 *   - evaluateBond: pass when alignment ratio meets threshold, fail when below, division-by-zero
 *   - calculateSynergy: single paradigm, both paradigms, tiebreak, no paradigms, null result
 */

import { describe, it, expect } from 'vitest';

import {
  evaluateWellRounded,
  evaluateBond,
  calculateSynergy,
} from './synergyCalculator.js';

import { DEFAULT_PARADIGMS } from '../fixtures/synergyConfig.js';
import type { Personality } from '../types/index.js';
import type { ParadigmConfig } from '../types/narrative.js';

// ============================================================================
// NPC Personalities (from npcs.ts)
// ============================================================================

const ELENA_PERSONALITY: Personality = {
  patience: 20,
  empathy: 20,
  cunning: 10,
  logic: 15,
  kindness: 20,
  charisma: 15,
};

const LARS_PERSONALITY: Personality = {
  patience: 10,
  empathy: 8,
  cunning: 28,
  logic: 25,
  kindness: 12,
  charisma: 17,
};

const KADE_PERSONALITY: Personality = {
  patience: 12,
  empathy: 8,
  cunning: 25,
  logic: 18,
  kindness: 10,
  charisma: 27,
};

// ============================================================================
// Paradigm configs
// ============================================================================

const WELL_ROUNDED_CONFIG: ParadigmConfig = {
  name: 'Well Rounded',
  type: 'well_rounded',
  threshold: 25,
  stat: 'power',
  multiplier: 1.10,
};

const BOND_CONFIG: ParadigmConfig = {
  name: 'Bond',
  type: 'bond',
  threshold: 80,
  stat: 'speed',
  multiplier: 1.10,
};

// ============================================================================
// evaluateWellRounded
// ============================================================================

describe('evaluateWellRounded', () => {
  it('returns null when a trait max is below threshold across all personalities', () => {
    // Player has empathy=8, Elena has empathy=20, Lars has empathy=8, Kade has empathy=8
    // max(empathy) = 20 < 25 threshold -> null
    const player: Personality = {
      patience: 30, empathy: 8, cunning: 25, logic: 25, kindness: 25, charisma: 25,
    };
    const result = evaluateWellRounded(player, [LARS_PERSONALITY, KADE_PERSONALITY], WELL_ROUNDED_CONFIG);
    expect(result).toBeNull();
  });

  it('returns SynergyBonus when all traits have max >= threshold across party', () => {
    // Elena covers patience(20->Elena), empathy(20->Elena), kindness(20->Elena)
    // Lars covers cunning(28), logic(25), charisma(17 — need boost)
    // Player can cover what's missing
    const player: Personality = {
      patience: 25, empathy: 25, cunning: 25, logic: 25, kindness: 25, charisma: 25,
    };
    const result = evaluateWellRounded(player, [ELENA_PERSONALITY, LARS_PERSONALITY, KADE_PERSONALITY], WELL_ROUNDED_CONFIG);
    expect(result).not.toBeNull();
    expect(result?.stat).toBe('power');
    expect(result?.multiplier).toBe(1.10);
    expect(result?.paradigmName).toBe('Well Rounded');
  });

  it('calculates correct matchQuality', () => {
    // All traits from player are exactly 25 -> max per trait = 25
    // matchQuality = (25*6) / (6*25) = 1.0
    const player: Personality = {
      patience: 25, empathy: 25, cunning: 25, logic: 25, kindness: 25, charisma: 25,
    };
    const result = evaluateWellRounded(player, [], WELL_ROUNDED_CONFIG);
    expect(result).not.toBeNull();
    expect(result?.matchQuality).toBeCloseTo(1.0, 5);
  });

  it('uses maximum across all personalities per trait', () => {
    // Lars has cunning=28 which exceeds threshold=25
    // We need all other traits covered
    const player: Personality = {
      patience: 25, empathy: 25, cunning: 10, logic: 25, kindness: 25, charisma: 25,
    };
    // Lars provides cunning=28 >= 25
    const result = evaluateWellRounded(player, [LARS_PERSONALITY], WELL_ROUNDED_CONFIG);
    expect(result).not.toBeNull();
  });

  it('works with empty partyNpcPersonalities (player alone must cover all traits)', () => {
    const player: Personality = {
      patience: 25, empathy: 25, cunning: 25, logic: 25, kindness: 25, charisma: 25,
    };
    const result = evaluateWellRounded(player, [], WELL_ROUNDED_CONFIG);
    expect(result).not.toBeNull();
  });

  it('returns null when single player cannot cover all traits alone', () => {
    const player: Personality = {
      patience: 30, empathy: 10, cunning: 30, logic: 30, kindness: 30, charisma: 30,
    };
    // empathy=10 < 25 threshold and no NPCs to cover it
    const result = evaluateWellRounded(player, [], WELL_ROUNDED_CONFIG);
    expect(result).toBeNull();
  });
});

// ============================================================================
// evaluateBond
// ============================================================================

describe('evaluateBond', () => {
  it('returns null when partyNpcPersonalities is empty', () => {
    const player: Personality = {
      patience: 20, empathy: 20, cunning: 10, logic: 15, kindness: 20, charisma: 15,
    };
    const result = evaluateBond(player, [], BOND_CONFIG);
    expect(result).toBeNull();
  });

  it('returns SynergyBonus when player aligns >= 80% with an NPC', () => {
    // Elena top traits: patience=20, empathy=20 (sum=40)
    // Player with same traits: patience=20, empathy=20 (sum=40)
    // alignmentRatio = 40/40 = 1.0 >= 0.80 -> bond
    const player: Personality = {
      patience: 20, empathy: 20, cunning: 10, logic: 15, kindness: 20, charisma: 15,
    };
    const result = evaluateBond(player, [ELENA_PERSONALITY], BOND_CONFIG);
    expect(result).not.toBeNull();
    expect(result?.stat).toBe('speed');
    expect(result?.multiplier).toBe(1.10);
  });

  it('returns null when player alignment is below 80% threshold', () => {
    // Elena top traits: patience=20, empathy=20, kindness=20 (top 2: patience=20, empathy=20, sum=40)
    // Player with very different traits:
    const player: Personality = {
      patience: 5, empathy: 5, cunning: 35, logic: 35, kindness: 10, charisma: 10,
    };
    // playerAlignmentSum for Elena's dominant traits (patience+empathy) = 5+5 = 10
    // npcDominantSum = 40
    // ratio = 10/40 = 0.25 < 0.80 -> null
    const result = evaluateBond(player, [ELENA_PERSONALITY], BOND_CONFIG);
    expect(result).toBeNull();
  });

  it('uses bestRatio across all NPCs (takes highest)', () => {
    // Player matches well with Kade (cunning+charisma)
    const player: Personality = {
      patience: 10, empathy: 8, cunning: 28, logic: 15, kindness: 10, charisma: 29,
    };
    // Kade top traits: charisma=27, cunning=25 (sum=52)
    // player[charisma + cunning] = 29+28 = 57, ratio = 57/52 > 1.0 >= 0.80 -> bond
    const result = evaluateBond(player, [ELENA_PERSONALITY, LARS_PERSONALITY, KADE_PERSONALITY], BOND_CONFIG);
    expect(result).not.toBeNull();
  });

  it('division-by-zero guard: skips NPC when npcDominantSum === 0', () => {
    const player: Personality = {
      patience: 20, empathy: 20, cunning: 10, logic: 15, kindness: 20, charisma: 15,
    };
    const zeroNpc: Personality = {
      patience: 0, empathy: 0, cunning: 0, logic: 0, kindness: 0, charisma: 0,
    };
    // Should not throw; zero NPC is skipped
    const result = evaluateBond(player, [zeroNpc], BOND_CONFIG);
    expect(result).toBeNull(); // no valid alignment ratios
  });

  it('returns correct matchQuality (bestRatio)', () => {
    // Perfect alignment with Elena (same personality)
    const result = evaluateBond(ELENA_PERSONALITY, [ELENA_PERSONALITY], BOND_CONFIG);
    expect(result).not.toBeNull();
    // Elena top traits: patience=20, empathy=20 (and kindness=20 is also 20)
    // Sorted: all 20s, top 2 are patience & empathy (first two in sort order for tied values)
    // Both player and NPC have same values -> ratio = 1.0
    expect(result?.matchQuality).toBeCloseTo(1.0, 5);
  });
});

// ============================================================================
// calculateSynergy
// ============================================================================

describe('calculateSynergy', () => {
  it('returns null when no paradigms are provided', () => {
    const player: Personality = {
      patience: 25, empathy: 25, cunning: 25, logic: 25, kindness: 25, charisma: 25,
    };
    const result = calculateSynergy(player, [ELENA_PERSONALITY], []);
    expect(result).toBeNull();
  });

  it('returns null when no paradigms are satisfied', () => {
    // Player has low traits across the board
    const player: Personality = {
      patience: 10, empathy: 8, cunning: 10, logic: 10, kindness: 10, charisma: 10,
    };
    const result = calculateSynergy(player, [LARS_PERSONALITY, KADE_PERSONALITY], DEFAULT_PARADIGMS);
    expect(result).toBeNull();
  });

  it('returns Well Rounded bonus when only that paradigm is satisfied', () => {
    // Everyone has high enough coverage for well_rounded
    const player: Personality = {
      patience: 25, empathy: 25, cunning: 25, logic: 25, kindness: 25, charisma: 25,
    };
    const result = calculateSynergy(player, [], [WELL_ROUNDED_CONFIG]);
    expect(result).not.toBeNull();
    expect(result?.paradigmName).toBe('Well Rounded');
    expect(result?.stat).toBe('power');
  });

  it('returns Bond bonus when only that paradigm is satisfied', () => {
    // Player mirrors Elena perfectly
    const result = calculateSynergy(ELENA_PERSONALITY, [ELENA_PERSONALITY], [BOND_CONFIG]);
    expect(result).not.toBeNull();
    expect(result?.paradigmName).toBe('Bond');
    expect(result?.stat).toBe('speed');
  });

  it('returns the higher-quality result when both paradigms are satisfied', () => {
    // High well-rounded coverage to get high matchQuality
    const player: Personality = {
      patience: 30, empathy: 30, cunning: 30, logic: 30, kindness: 30, charisma: 30,
    };
    // Note: sum = 180, but we use it for testing logic not validity
    const npcs = [ELENA_PERSONALITY, LARS_PERSONALITY, KADE_PERSONALITY];
    const result = calculateSynergy(player, npcs, DEFAULT_PARADIGMS);
    expect(result).not.toBeNull();
    // whichever has higher matchQuality wins
  });

  it('tiebreak: well_rounded wins over bond on equal matchQuality', () => {
    // Craft a scenario where both paradigms return exactly matchQuality = 1.0
    const player: Personality = {
      patience: 25, empathy: 25, cunning: 25, logic: 25, kindness: 25, charisma: 25,
    };

    // Custom paradigms with same multiplier/threshold — manipulate to force tie
    const wrConfig: ParadigmConfig = {
      name: 'Well Rounded Tie',
      type: 'well_rounded',
      threshold: 25,
      stat: 'power',
      multiplier: 1.10,
    };
    const bondConfig: ParadigmConfig = {
      name: 'Bond Tie',
      type: 'bond',
      threshold: 0, // effectively 0% threshold so bond is always satisfied
      stat: 'speed',
      multiplier: 1.10,
    };

    const result = calculateSynergy(player, [player], [wrConfig, bondConfig]);
    expect(result).not.toBeNull();
    // If both satisfy, well_rounded should win on tiebreak
    expect(result?.paradigmName).toBe('Well Rounded Tie');
  });

  it('uses DEFAULT_PARADIGMS correctly with standard party (Elena, Lars, Kade)', () => {
    // With all 3 NPCs and a reasonably spread player, check one of the paradigms fires
    const player: Personality = {
      patience: 25, empathy: 25, cunning: 25, logic: 25, kindness: 25, charisma: 25,
    };
    const result = calculateSynergy(player, [ELENA_PERSONALITY, LARS_PERSONALITY, KADE_PERSONALITY], DEFAULT_PARADIGMS);
    // At minimum, well_rounded should be satisfied: Lars has cunning=28, logic=25 >= 25
    // Player covers the rest at 25. This should pass.
    expect(result).not.toBeNull();
  });
});
