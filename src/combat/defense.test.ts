/**
 * defense.test.ts — Unit tests for the Defense Resolution System
 *
 * Tests cover:
 *  - Each defense type success case (Block, Dodge, Parry)
 *  - Each defense type failure case (Block, Dodge, Parry)
 *  - Defenseless (always fails, always takes full damage)
 *  - resolveDefense dispatcher (all 4 types)
 *  - Roll boundary conditions (exactly at threshold, just above/below)
 *
 * Roll check: roll <= SR * 20 → success
 * e.g., SR = 0.6 → threshold = 12 → roll = 12 succeeds, roll = 13 fails
 */

import { describe, it, expect } from 'vitest';
import {
  resolveBlock,
  resolveDodge,
  resolveParry,
  resolveDefenseless,
  resolveDefense,
} from './defense.js';
import type { ReactionSkills } from '../types/combat.js';

// ============================================================================
// Shared test fixtures
// ============================================================================

/** Standard reaction skills used for most tests. */
const STANDARD_REACTION_SKILLS: ReactionSkills = {
  block: { SR: 0.6, SMR: 0.5, FMR: 0.2 },
  dodge: { SR: 0.5, FMR: 0.15 },
  parry: { SR: 0.4, FMR: 0.1 },
};

const RAW_DAMAGE = 100;

// ============================================================================
// resolveBlock
// ============================================================================

describe('resolveBlock', () => {
  it('succeeds when roll is at threshold (roll === SR * 20)', () => {
    // SR = 0.6 → threshold = 12 → roll = 12 → success
    const result = resolveBlock(RAW_DAMAGE, 0.6, 0.5, 0.2, 12);
    expect(result.success).toBe(true);
    // damage = 100 * (1 - SMR) = 100 * 0.5 = 50
    expect(result.damage).toBe(50);
    expect(result.crushingBlowEligible).toBe(true);
  });

  it('succeeds when roll is below threshold (roll < SR * 20)', () => {
    // SR = 0.6 → threshold = 12 → roll = 5 → success
    const result = resolveBlock(RAW_DAMAGE, 0.6, 0.5, 0.2, 5);
    expect(result.success).toBe(true);
    expect(result.damage).toBe(50);
    expect(result.crushingBlowEligible).toBe(true);
  });

  it('fails when roll is above threshold (roll > SR * 20)', () => {
    // SR = 0.6 → threshold = 12 → roll = 13 → failure
    const result = resolveBlock(RAW_DAMAGE, 0.6, 0.5, 0.2, 13);
    expect(result.success).toBe(false);
    // damage = 100 * (1 - FMR) = 100 * 0.8 = 80
    expect(result.damage).toBe(80);
    expect(result.crushingBlowEligible).toBe(true);
  });

  it('fails when roll is at maximum (roll = 20)', () => {
    const result = resolveBlock(RAW_DAMAGE, 0.6, 0.5, 0.2, 20);
    expect(result.success).toBe(false);
    expect(result.damage).toBe(80);
  });

  it('always marks crushingBlowEligible as true (eligibility is the caller\'s responsibility)', () => {
    const success = resolveBlock(RAW_DAMAGE, 0.1, 0.5, 0.2, 1); // guaranteed success
    const failure = resolveBlock(RAW_DAMAGE, 0.1, 0.5, 0.2, 19); // guaranteed failure
    expect(success.crushingBlowEligible).toBe(true);
    expect(failure.crushingBlowEligible).toBe(true);
  });

  it('applies different SMR and FMR values correctly', () => {
    // High SMR, low FMR scenario
    const success = resolveBlock(200, 0.8, 0.9, 0.1, 10); // roll 10 <= 16 → success
    expect(success.damage).toBeCloseTo(200 * 0.1); // 200 * (1 - 0.9) = 20

    const failure = resolveBlock(200, 0.8, 0.9, 0.1, 17); // roll 17 > 16 → failure
    expect(failure.damage).toBeCloseTo(200 * 0.9); // 200 * (1 - 0.1) = 180
  });
});

// ============================================================================
// resolveDodge
// ============================================================================

describe('resolveDodge', () => {
  it('succeeds (0 damage) when roll is at threshold (roll === SR * 20)', () => {
    // SR = 0.5 → threshold = 10 → roll = 10 → success
    const result = resolveDodge(RAW_DAMAGE, 0.5, 0.15, 10);
    expect(result.success).toBe(true);
    expect(result.damage).toBe(0);
  });

  it('succeeds (0 damage) when roll is below threshold', () => {
    const result = resolveDodge(RAW_DAMAGE, 0.5, 0.15, 1);
    expect(result.success).toBe(true);
    expect(result.damage).toBe(0);
  });

  it('fails with partial damage when roll is above threshold', () => {
    // SR = 0.5 → threshold = 10 → roll = 11 → failure
    const result = resolveDodge(RAW_DAMAGE, 0.5, 0.15, 11);
    expect(result.success).toBe(false);
    // damage = 100 * (1 - FMR) = 100 * 0.85 = 85
    expect(result.damage).toBe(85);
  });

  it('fails when roll is at maximum (roll = 20)', () => {
    const result = resolveDodge(RAW_DAMAGE, 0.5, 0.15, 20);
    expect(result.success).toBe(false);
    expect(result.damage).toBe(85);
  });

  it('applies FMR correctly on failure', () => {
    // FMR = 0.3 → failure damage = damage * (1 - 0.3) = damage * 0.7
    const result = resolveDodge(50, 0.4, 0.3, 20); // guaranteed failure
    expect(result.damage).toBeCloseTo(50 * 0.7); // 35
  });
});

// ============================================================================
// resolveParry
// ============================================================================

describe('resolveParry', () => {
  it('succeeds (0 damage, counter triggered) when roll is at threshold (roll === SR * 20)', () => {
    // SR = 0.4 → threshold = 8 → roll = 8 → success
    const result = resolveParry(RAW_DAMAGE, 0.4, 0.1, 8);
    expect(result.success).toBe(true);
    expect(result.damage).toBe(0);
    expect(result.counterTriggered).toBe(true);
  });

  it('succeeds when roll is below threshold', () => {
    const result = resolveParry(RAW_DAMAGE, 0.4, 0.1, 1);
    expect(result.success).toBe(true);
    expect(result.damage).toBe(0);
    expect(result.counterTriggered).toBe(true);
  });

  it('fails with partial damage and no counter when roll is above threshold', () => {
    // SR = 0.4 → threshold = 8 → roll = 9 → failure
    const result = resolveParry(RAW_DAMAGE, 0.4, 0.1, 9);
    expect(result.success).toBe(false);
    // damage = 100 * (1 - FMR) = 100 * 0.9 = 90
    expect(result.damage).toBe(90);
    expect(result.counterTriggered).toBe(false);
  });

  it('fails when roll is at maximum (roll = 20)', () => {
    const result = resolveParry(RAW_DAMAGE, 0.4, 0.1, 20);
    expect(result.success).toBe(false);
    expect(result.damage).toBe(90);
    expect(result.counterTriggered).toBe(false);
  });

  it('counterTriggered is false on failure', () => {
    const result = resolveParry(RAW_DAMAGE, 0.1, 0.2, 20); // guaranteed failure
    expect(result.counterTriggered).toBe(false);
  });

  it('applies FMR correctly on failure', () => {
    // FMR = 0.25 → failure damage = damage * (1 - 0.25) = damage * 0.75
    const result = resolveParry(80, 0.2, 0.25, 20); // guaranteed failure
    expect(result.damage).toBeCloseTo(80 * 0.75); // 60
  });
});

// ============================================================================
// resolveDefenseless
// ============================================================================

describe('resolveDefenseless', () => {
  it('always returns success = false', () => {
    const result = resolveDefenseless(100);
    expect(result.success).toBe(false);
  });

  it('always returns full damage (no mitigation)', () => {
    expect(resolveDefenseless(100).damage).toBe(100);
    expect(resolveDefenseless(0).damage).toBe(0);
    expect(resolveDefenseless(42.5).damage).toBe(42.5);
  });

  it('is deterministic regardless of any external state', () => {
    // Called with same damage multiple times — always same result
    const a = resolveDefenseless(75);
    const b = resolveDefenseless(75);
    expect(a).toEqual(b);
  });
});

// ============================================================================
// resolveDefense dispatcher
// ============================================================================

describe('resolveDefense', () => {
  describe('block dispatch', () => {
    it('returns type = block and correct success/damageMultiplier on success', () => {
      // SR = 0.6 → threshold = 12 → roll = 5 → success
      const result = resolveDefense('block', RAW_DAMAGE, STANDARD_REACTION_SKILLS, 5);
      expect(result.type).toBe('block');
      expect(result.success).toBe(true);
      // damageMultiplier = finalDamage / rawDamage = 50 / 100 = 0.5
      expect(result.damageMultiplier).toBeCloseTo(0.5);
    });

    it('returns type = block and correct success/damageMultiplier on failure', () => {
      // SR = 0.6 → threshold = 12 → roll = 20 → failure
      const result = resolveDefense('block', RAW_DAMAGE, STANDARD_REACTION_SKILLS, 20);
      expect(result.type).toBe('block');
      expect(result.success).toBe(false);
      // damageMultiplier = finalDamage / rawDamage = 80 / 100 = 0.8
      expect(result.damageMultiplier).toBeCloseTo(0.8);
    });
  });

  describe('dodge dispatch', () => {
    it('returns type = dodge and damageMultiplier = 0 on success', () => {
      // SR = 0.5 → threshold = 10 → roll = 5 → success
      const result = resolveDefense('dodge', RAW_DAMAGE, STANDARD_REACTION_SKILLS, 5);
      expect(result.type).toBe('dodge');
      expect(result.success).toBe(true);
      expect(result.damageMultiplier).toBe(0);
    });

    it('returns type = dodge and partial damageMultiplier on failure', () => {
      // SR = 0.5 → threshold = 10 → roll = 20 → failure
      const result = resolveDefense('dodge', RAW_DAMAGE, STANDARD_REACTION_SKILLS, 20);
      expect(result.type).toBe('dodge');
      expect(result.success).toBe(false);
      // damageMultiplier = 85/100 = 0.85
      expect(result.damageMultiplier).toBeCloseTo(0.85);
    });
  });

  describe('parry dispatch', () => {
    it('returns type = parry and damageMultiplier = 0 on success', () => {
      // SR = 0.4 → threshold = 8 → roll = 1 → success
      const result = resolveDefense('parry', RAW_DAMAGE, STANDARD_REACTION_SKILLS, 1);
      expect(result.type).toBe('parry');
      expect(result.success).toBe(true);
      expect(result.damageMultiplier).toBe(0);
    });

    it('returns type = parry and partial damageMultiplier on failure', () => {
      // SR = 0.4 → threshold = 8 → roll = 20 → failure
      const result = resolveDefense('parry', RAW_DAMAGE, STANDARD_REACTION_SKILLS, 20);
      expect(result.type).toBe('parry');
      expect(result.success).toBe(false);
      // damage = 100 * (1 - 0.1) = 90 → damageMultiplier = 0.9
      expect(result.damageMultiplier).toBeCloseTo(0.9);
    });
  });

  describe('defenseless dispatch', () => {
    it('returns type = defenseless, success = false, damageMultiplier = 1.0', () => {
      const result = resolveDefense('defenseless', RAW_DAMAGE, STANDARD_REACTION_SKILLS, 5);
      expect(result.type).toBe('defenseless');
      expect(result.success).toBe(false);
      expect(result.damageMultiplier).toBe(1.0);
    });

    it('ignores the roll value (always full damage)', () => {
      const low = resolveDefense('defenseless', RAW_DAMAGE, STANDARD_REACTION_SKILLS, 0);
      const high = resolveDefense('defenseless', RAW_DAMAGE, STANDARD_REACTION_SKILLS, 20);
      expect(low.damageMultiplier).toBe(1.0);
      expect(high.damageMultiplier).toBe(1.0);
    });
  });
});
