/**
 * formulas.test.ts — TDD tests for the combat formula suite.
 *
 * Each formula has its test written first (TDD per ADR-015).
 * Input/output values are derived from GM_Combat_Tracker_Documentation.md
 * and the GM Combat Tracker Excel source of truth.
 *
 * Test sequence mirrors the formula implementation order:
 *   1. Rank KO threshold + check
 *   2. Blindside threshold + check
 *   3. Crushing Blow threshold + check
 *   4. Damage formulas (Block, Dodge, Parry, Defenseless)
 *   5. Base damage (shared utility)
 *   6. Special damage bonus
 *   7. Evade regen
 *   8. Energy gain
 *   9. Ascension level
 *  10. Dynamic modifiers
 */

import { describe, it, expect } from 'vitest';
import {
  calculateRankKOThreshold,
  checkRankKO,
  calculateBlindsideThreshold,
  checkBlindside,
  calculateCrushingBlowThreshold,
  checkCrushingBlow,
  calculateBlockDamage,
  calculateDodgeDamage,
  calculateParryDamage,
  calculateDefenselessDamage,
  calculateBaseDamage,
  calculateSpecialDamageBonus,
  calculateEvadeRegen,
  calculateEnergyGain,
  calculateAscensionLevel,
  applyDynamicModifiers,
} from './formulas.js';
import type { AscensionLevel } from '../types/combat.js';

// ============================================================================
// 1. Rank KO Threshold
// ============================================================================

describe('calculateRankKOThreshold', () => {
  it('returns correct threshold when attacker rank is 5 and target rank is 3', () => {
    // ((5 - 3) * 3) / 10 = 6 / 10 = 0.6
    expect(calculateRankKOThreshold(5, 3)).toBeCloseTo(0.6, 10);
  });

  it('returns 0.3 when rank difference is 1', () => {
    // ((2 - 1) * 3) / 10 = 0.3
    expect(calculateRankKOThreshold(2, 1)).toBeCloseTo(0.3, 10);
  });

  it('returns 0.9 when rank difference is 3', () => {
    // ((4 - 1) * 3) / 10 = 0.9
    expect(calculateRankKOThreshold(4, 1)).toBeCloseTo(0.9, 10);
  });

  it('handles fractional rank differences', () => {
    // ((3.5 - 2.0) * 3) / 10 = (1.5 * 3) / 10 = 0.45
    expect(calculateRankKOThreshold(3.5, 2.0)).toBeCloseTo(0.45, 10);
  });

  it('returns 0 when ranks are equal', () => {
    // ((3 - 3) * 3) / 10 = 0
    expect(calculateRankKOThreshold(3, 3)).toBeCloseTo(0, 10);
  });
});

// ============================================================================
// 2. Rank KO Check
// ============================================================================

describe('checkRankKO', () => {
  it('returns true when roll/20 >= (1 - threshold) — example from spec: threshold=0.6, roll=14', () => {
    // (14 / 20) = 0.7 >= (1 - 0.6) = 0.4 → true
    expect(checkRankKO(0.6, 14)).toBe(true);
  });

  it('returns false when roll/20 < (1 - threshold)', () => {
    // (7 / 20) = 0.35 >= (1 - 0.6) = 0.4 → false
    expect(checkRankKO(0.6, 7)).toBe(false);
  });

  it('returns true at the exact threshold boundary', () => {
    // threshold = 0.6 → required = 0.4 → roll = 0.4 * 20 = 8
    // (8 / 20) = 0.4 >= 0.4 → true
    expect(checkRankKO(0.6, 8)).toBe(true);
  });

  it('returns false for threshold=0 (equal ranks)', () => {
    // (1 - 0) = 1.0 → would need roll/20 >= 1.0 → impossible with roll < 20
    expect(checkRankKO(0, 15)).toBe(false);
  });

  it('returns true at high threshold with moderate roll', () => {
    // threshold = 0.9 → required = 0.1 → roll=3: (3/20)=0.15 >= 0.1 → true
    expect(checkRankKO(0.9, 3)).toBe(true);
  });

  it('returns false just below the boundary', () => {
    // threshold = 0.5 → required = 0.5 → need roll/20 >= 0.5, i.e., roll >= 10
    // roll=9: (9/20)=0.45 < 0.5 → false
    expect(checkRankKO(0.5, 9)).toBe(false);
  });
});

// ============================================================================
// 3. Blindside Threshold
// ============================================================================

describe('calculateBlindsideThreshold', () => {
  it('returns 0.25 when attacker speed=10, target speed=8 — from spec', () => {
    // (10 - 8) / 8 = 2 / 8 = 0.25
    expect(calculateBlindsideThreshold(10, 8)).toBeCloseTo(0.25, 10);
  });

  it('returns 0.5 when attacker speed=15, target speed=10', () => {
    // (15 - 10) / 10 = 5 / 10 = 0.5
    expect(calculateBlindsideThreshold(15, 10)).toBeCloseTo(0.5, 10);
  });

  it('returns 0 when speeds are equal', () => {
    // (10 - 10) / 10 = 0
    expect(calculateBlindsideThreshold(10, 10)).toBeCloseTo(0, 10);
  });

  it('handles larger speed differences', () => {
    // (20 - 10) / 10 = 1.0
    expect(calculateBlindsideThreshold(20, 10)).toBeCloseTo(1.0, 10);
  });
});

// ============================================================================
// 4. Blindside Check
// ============================================================================

describe('checkBlindside', () => {
  it('returns true when roll/20 >= (1 - threshold) — threshold=0.25, roll=16 from spec', () => {
    // (16 / 20) = 0.8 >= (1 - 0.25) = 0.75 → true
    expect(checkBlindside(0.25, 16)).toBe(true);
  });

  it('returns false when roll is below threshold boundary', () => {
    // (14 / 20) = 0.7 >= 0.75 → false
    expect(checkBlindside(0.25, 14)).toBe(false);
  });

  it('returns true at the exact boundary', () => {
    // threshold=0.25 → required=0.75 → roll=15: (15/20)=0.75 >= 0.75 → true
    expect(checkBlindside(0.25, 15)).toBe(true);
  });

  it('returns false for threshold=0 (equal speeds)', () => {
    // (1 - 0) = 1.0 → need roll/20 >= 1 → impossible with roll < 20
    expect(checkBlindside(0, 19)).toBe(false);
  });

  it('returns true at high threshold with low roll', () => {
    // threshold=0.9 → required=0.1 → roll=3: (3/20)=0.15 >= 0.1 → true
    expect(checkBlindside(0.9, 3)).toBe(true);
  });
});

// ============================================================================
// 5. Crushing Blow Threshold
// ============================================================================

describe('calculateCrushingBlowThreshold', () => {
  it('returns 0.25 when actionPower=125, targetPower=100', () => {
    // (125 - 100) / 100 = 0.25
    expect(calculateCrushingBlowThreshold(125, 100)).toBeCloseTo(0.25, 10);
  });

  it('returns 0.5 when actionPower=150, targetPower=100', () => {
    // (150 - 100) / 100 = 0.5
    expect(calculateCrushingBlowThreshold(150, 100)).toBeCloseTo(0.5, 10);
  });

  it('returns 0 when action power equals target power', () => {
    // (100 - 100) / 100 = 0
    expect(calculateCrushingBlowThreshold(100, 100)).toBeCloseTo(0, 10);
  });

  it('handles non-round numbers', () => {
    // (120 - 80) / 80 = 40/80 = 0.5
    expect(calculateCrushingBlowThreshold(120, 80)).toBeCloseTo(0.5, 10);
  });
});

// ============================================================================
// 6. Crushing Blow Check
// ============================================================================

describe('checkCrushingBlow', () => {
  it('returns true when roll/20 >= (1 - threshold)', () => {
    // threshold=0.25 → required=0.75 → roll=16: (16/20)=0.8 >= 0.75 → true
    expect(checkCrushingBlow(0.25, 16)).toBe(true);
  });

  it('returns false when roll is below boundary', () => {
    // threshold=0.25 → required=0.75 → roll=14: (14/20)=0.7 < 0.75 → false
    expect(checkCrushingBlow(0.25, 14)).toBe(false);
  });

  it('returns true at the exact boundary', () => {
    // threshold=0.25 → required=0.75 → roll=15: (15/20)=0.75 >= 0.75 → true
    expect(checkCrushingBlow(0.25, 15)).toBe(true);
  });

  it('returns false for threshold=0 (equal power)', () => {
    expect(checkCrushingBlow(0, 19)).toBe(false);
  });
});

// ============================================================================
// 7. Block Damage
// ============================================================================

describe('calculateBlockDamage', () => {
  it('returns damage * (1 - SMR) on block success — damage=100, SMR=0.4 → 60', () => {
    // 100 * (1 - 0.4) = 60
    expect(calculateBlockDamage(100, 0.4, 0.2, true)).toBeCloseTo(60, 10);
  });

  it('returns damage * (1 - FMR) on block failure — damage=100, FMR=0.2 → 80', () => {
    // 100 * (1 - 0.2) = 80
    expect(calculateBlockDamage(100, 0.4, 0.2, false)).toBeCloseTo(80, 10);
  });

  it('handles SMR=0 on success — full damage if no mitigation', () => {
    // 100 * (1 - 0) = 100
    expect(calculateBlockDamage(100, 0, 0.1, true)).toBeCloseTo(100, 10);
  });

  it('handles SMR=1 on success — zero damage if full mitigation', () => {
    // 100 * (1 - 1) = 0
    expect(calculateBlockDamage(100, 1.0, 0.5, true)).toBeCloseTo(0, 10);
  });

  it('handles FMR=0 on failure — full damage taken', () => {
    // 100 * (1 - 0) = 100
    expect(calculateBlockDamage(100, 0.4, 0, false)).toBeCloseTo(100, 10);
  });

  it('scales correctly with non-round damage values', () => {
    // damage=75, SMR=0.5 → 75 * 0.5 = 37.5
    expect(calculateBlockDamage(75, 0.5, 0.2, true)).toBeCloseTo(37.5, 10);
  });
});

// ============================================================================
// 8. Dodge Damage
// ============================================================================

describe('calculateDodgeDamage', () => {
  it('returns 0 on dodge success — from spec', () => {
    expect(calculateDodgeDamage(100, 0.2, true)).toBe(0);
  });

  it('returns damage * (1 - FMR) on dodge failure', () => {
    // 100 * (1 - 0.2) = 80
    expect(calculateDodgeDamage(100, 0.2, false)).toBeCloseTo(80, 10);
  });

  it('returns 0 on success regardless of damage value', () => {
    expect(calculateDodgeDamage(999, 0.5, true)).toBe(0);
  });

  it('returns full damage on failure with FMR=0', () => {
    // 100 * (1 - 0) = 100
    expect(calculateDodgeDamage(100, 0, false)).toBeCloseTo(100, 10);
  });

  it('scales correctly with non-round damage values', () => {
    // damage=60, FMR=0.3 → 60 * 0.7 = 42
    expect(calculateDodgeDamage(60, 0.3, false)).toBeCloseTo(42, 10);
  });
});

// ============================================================================
// 9. Parry Damage
// ============================================================================

describe('calculateParryDamage', () => {
  it('returns 0 on parry success (counter triggered)', () => {
    expect(calculateParryDamage(100, 0.2, true)).toBe(0);
  });

  it('returns damage * (1 - FMR) on parry failure', () => {
    // 100 * (1 - 0.2) = 80
    expect(calculateParryDamage(100, 0.2, false)).toBeCloseTo(80, 10);
  });

  it('returns 0 on success regardless of damage value', () => {
    expect(calculateParryDamage(500, 0.8, true)).toBe(0);
  });

  it('returns full damage on failure with FMR=0', () => {
    expect(calculateParryDamage(100, 0, false)).toBeCloseTo(100, 10);
  });
});

// ============================================================================
// 10. Defenseless Damage
// ============================================================================

describe('calculateDefenselessDamage', () => {
  it('returns full damage (100%)', () => {
    expect(calculateDefenselessDamage(100)).toBe(100);
  });

  it('passes through any damage value unchanged', () => {
    expect(calculateDefenselessDamage(250)).toBe(250);
    expect(calculateDefenselessDamage(0)).toBe(0);
    expect(calculateDefenselessDamage(37.5)).toBe(37.5);
  });
});

// ============================================================================
// 11. Base Damage (shared utility)
// ============================================================================

describe('calculateBaseDamage', () => {
  // NOTE: Formula pending Excel verification against Math!A40:AM54. Values computed from current power-ratio model. If Excel shows a different formula, update these test values.

  it('returns exact 100 when equal power (targetPower=attackerPower)', () => {
    // Formula: attackerPower * (attackerPower / targetPower) + modifier
    // 100 * (100 / 100) + 0 = 100 * 1.0 = 100
    expect(calculateBaseDamage(100, 100)).toBe(100);
  });

  it('returns exact 225 when stronger attacker (150 vs 100)', () => {
    // 150 * (150 / 100) + 0 = 150 * 1.5 = 225
    expect(calculateBaseDamage(150, 100)).toBe(225);
  });

  it('returns exact 64 when weaker attacker (80 vs 100)', () => {
    // 80 * (80 / 100) + 0 = 80 * 0.8 = 64
    expect(calculateBaseDamage(80, 100)).toBe(64);
  });

  it('returns exact 110 with modifier of 10 (equal power + flat boost)', () => {
    // 100 * (100 / 100) + 10 = 100 + 10 = 110
    expect(calculateBaseDamage(100, 100, 10)).toBe(110);
  });

  it('returns exact 400 for high power ratio (200 vs 100)', () => {
    // 200 * (200 / 100) + 0 = 200 * 2.0 = 400
    expect(calculateBaseDamage(200, 100)).toBe(400);
  });
});

// ============================================================================
// 12. Special Damage Bonus
// ============================================================================

describe('calculateSpecialDamageBonus', () => {
  it('returns baseDamage * (1 + 0.10 * 1 segment) — 10% bonus for 1 segment', () => {
    // 100 * (1 + 0.10 * 1) = 100 * 1.10 = 110
    expect(calculateSpecialDamageBonus(100, 1)).toBeCloseTo(110, 10);
  });

  it('returns baseDamage * (1 + 0.10 * 3) — 30% bonus for 3 segments', () => {
    // 100 * 1.30 = 130
    expect(calculateSpecialDamageBonus(100, 3)).toBeCloseTo(130, 10);
  });

  it('returns baseDamage * (1 + 0.10 * 5) — 50% bonus for 5 segments (max)', () => {
    // 100 * 1.50 = 150
    expect(calculateSpecialDamageBonus(100, 5)).toBeCloseTo(150, 10);
  });

  it('returns base damage unchanged with 0 segments', () => {
    // 100 * (1 + 0) = 100
    expect(calculateSpecialDamageBonus(100, 0)).toBeCloseTo(100, 10);
  });

  it('scales correctly with non-round base damage', () => {
    // baseDamage=75, segments=2 → 75 * 1.20 = 90
    expect(calculateSpecialDamageBonus(75, 2)).toBeCloseTo(90, 10);
  });
});

// ============================================================================
// 13. Evade Regen
// ============================================================================

describe('calculateEvadeRegen', () => {
  it('returns maxStamina * 0.30 — from spec: maxStamina=100 → 30', () => {
    expect(calculateEvadeRegen(100)).toBeCloseTo(30, 10);
  });

  it('returns 60 for maxStamina=200', () => {
    expect(calculateEvadeRegen(200)).toBeCloseTo(60, 10);
  });

  it('handles non-round max stamina values', () => {
    // 150 * 0.30 = 45
    expect(calculateEvadeRegen(150)).toBeCloseTo(45, 10);
  });

  it('returns 0 for maxStamina=0', () => {
    expect(calculateEvadeRegen(0)).toBeCloseTo(0, 10);
  });
});

// ============================================================================
// 14. Energy Gain
// ============================================================================

describe('calculateEnergyGain', () => {
  // Base gains from GM_Combat_Tracker_Documentation.md:
  //   actionSuccess: 1.0, actionFailure: 0.5
  //   reactionSuccess: 0.5, reactionFailure: 0.25
  //
  // Accumulation bonus (1 + bonus): level 0 = +0%, 1 = +25%, 2 = +25%, 3 = +50%

  describe('at ascension level 0 (no bonus)', () => {
    it('actionSuccess → 1.0 segments', () => {
      expect(calculateEnergyGain('actionSuccess', 'success', 0)).toBeCloseTo(1.0, 10);
    });

    it('actionFailure → 0.5 segments', () => {
      expect(calculateEnergyGain('actionSuccess', 'failure', 0)).toBeCloseTo(0.5, 10);
    });

    it('reactionSuccess → 0.5 segments', () => {
      expect(calculateEnergyGain('reactionSuccess', 'success', 0)).toBeCloseTo(0.5, 10);
    });

    it('reactionFailure → 0.25 segments', () => {
      expect(calculateEnergyGain('reactionSuccess', 'failure', 0)).toBeCloseTo(0.25, 10);
    });
  });

  describe('at ascension level 1 (+25% bonus)', () => {
    it('actionSuccess → 1.0 * 1.25 = 1.25 segments', () => {
      expect(calculateEnergyGain('actionSuccess', 'success', 1)).toBeCloseTo(1.25, 10);
    });

    it('actionFailure → 0.5 * 1.25 = 0.625 segments', () => {
      expect(calculateEnergyGain('actionSuccess', 'failure', 1)).toBeCloseTo(0.625, 10);
    });

    it('reactionSuccess → 0.5 * 1.25 = 0.625 segments', () => {
      expect(calculateEnergyGain('reactionSuccess', 'success', 1)).toBeCloseTo(0.625, 10);
    });

    it('reactionFailure → 0.25 * 1.25 = 0.3125 segments', () => {
      expect(calculateEnergyGain('reactionSuccess', 'failure', 1)).toBeCloseTo(0.3125, 10);
    });
  });

  describe('at ascension level 2 (+25% bonus)', () => {
    it('actionSuccess → 1.0 * 1.25 = 1.25 segments', () => {
      expect(calculateEnergyGain('actionSuccess', 'success', 2)).toBeCloseTo(1.25, 10);
    });

    it('reactionFailure → 0.25 * 1.25 = 0.3125 segments', () => {
      expect(calculateEnergyGain('reactionSuccess', 'failure', 2)).toBeCloseTo(0.3125, 10);
    });
  });

  describe('at ascension level 3 (+50% bonus)', () => {
    it('actionSuccess → 1.0 * 1.50 = 1.5 segments', () => {
      expect(calculateEnergyGain('actionSuccess', 'success', 3)).toBeCloseTo(1.5, 10);
    });

    it('actionFailure → 0.5 * 1.50 = 0.75 segments', () => {
      expect(calculateEnergyGain('actionSuccess', 'failure', 3)).toBeCloseTo(0.75, 10);
    });

    it('reactionSuccess → 0.5 * 1.50 = 0.75 segments', () => {
      expect(calculateEnergyGain('reactionSuccess', 'success', 3)).toBeCloseTo(0.75, 10);
    });

    it('reactionFailure → 0.25 * 1.50 = 0.375 segments', () => {
      expect(calculateEnergyGain('reactionSuccess', 'failure', 3)).toBeCloseTo(0.375, 10);
    });
  });
});

// ============================================================================
// 15. Ascension Level
// ============================================================================

describe('calculateAscensionLevel', () => {
  // Thresholds: 35 → level 1, 95 → level 2, 180 → level 3

  it('returns 0 for segments < 35', () => {
    const result: AscensionLevel = calculateAscensionLevel(0);
    expect(result).toBe(0);
    expect(calculateAscensionLevel(34)).toBe(0);
  });

  it('returns 1 at exactly 35 segments', () => {
    expect(calculateAscensionLevel(35)).toBe(1);
  });

  it('returns 1 for segments between 35 and 94 inclusive', () => {
    expect(calculateAscensionLevel(60)).toBe(1);
    expect(calculateAscensionLevel(94)).toBe(1);
  });

  it('returns 2 at exactly 95 segments', () => {
    expect(calculateAscensionLevel(95)).toBe(2);
  });

  it('returns 2 for segments between 95 and 179 inclusive', () => {
    expect(calculateAscensionLevel(150)).toBe(2);
    expect(calculateAscensionLevel(179)).toBe(2);
  });

  it('returns 3 at exactly 180 segments', () => {
    expect(calculateAscensionLevel(180)).toBe(3);
  });

  it('returns 3 for segments above 180', () => {
    expect(calculateAscensionLevel(250)).toBe(3);
    expect(calculateAscensionLevel(1000)).toBe(3);
  });
});

// ============================================================================
// 16. Apply Dynamic Modifiers
// ============================================================================

describe('applyDynamicModifiers', () => {
  const baseStats = {
    power: 100,
    speed: 10,
    blockSR: 0.5,
    blockSMR: 0.4,
    blockFMR: 0.2,
    dodgeSR: 0.4,
    dodgeFMR: 0.2,
    parrySR: 0.3,
    parryFMR: 0.15,
  };

  it('returns base stats unchanged when no buffs or debuffs', () => {
    const result = applyDynamicModifiers(baseStats, [], []);
    expect(result).toEqual(baseStats);
  });

  it('applies a single buff to the correct stat', () => {
    const buffs = [{ type: 'power_boost', source: 'fire_path', duration: 1, modifier: 20 }];
    const result = applyDynamicModifiers(baseStats, buffs, []);
    expect(result.power).toBeCloseTo(120, 10);
    expect(result.speed).toBe(10); // unchanged
  });

  it('applies a single debuff reducing the correct stat', () => {
    const debuffs = [{ stat: 'blockSR', amount: 0.1, source: 'earth_path' }];
    const result = applyDynamicModifiers(baseStats, [], debuffs);
    expect(result.blockSR).toBeCloseTo(0.4, 10);
    expect(result.power).toBe(100); // unchanged
  });

  it('stacks multiple buffs on the same stat additively', () => {
    const buffs = [
      { type: 'power_boost', source: 'buff1', duration: 1, modifier: 10 },
      { type: 'power_boost', source: 'buff2', duration: 1, modifier: 15 },
    ];
    const result = applyDynamicModifiers(baseStats, buffs, []);
    expect(result.power).toBeCloseTo(125, 10);
  });

  it('applies buffs and debuffs independently without interference', () => {
    const buffs = [{ type: 'parrySR_boost', source: 'fire_path', duration: 1, modifier: 0.1 }];
    const debuffs = [{ stat: 'blockSR', amount: 0.15, source: 'earth_path' }];
    const result = applyDynamicModifiers(baseStats, buffs, debuffs);
    expect(result.parrySR).toBeCloseTo(0.4, 10);
    expect(result.blockSR).toBeCloseTo(0.35, 10);
  });

  it('returns a new object — does not mutate baseStats', () => {
    const buffs = [{ type: 'power_boost', source: 'test', duration: 1, modifier: 50 }];
    const result = applyDynamicModifiers(baseStats, buffs, []);
    expect(result).not.toBe(baseStats);
    expect(baseStats.power).toBe(100); // original unchanged
  });
});
