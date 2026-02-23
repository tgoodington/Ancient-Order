/**
 * energy.test.ts — Tests for energy segment tracking and ascension management.
 *
 * Tests cover:
 *   - addEnergySegments: correct gain values with ascension bonus at all levels
 *   - checkAscensionAdvance: advances at all 3 thresholds (35, 95, 180)
 *   - getStartingSegments: correct lookup per ascension level
 *   - resetRoundEnergy: resets to starting segments for current ascension level
 *   - Immutability: input combatants never mutated
 */

import { describe, it, expect } from 'vitest';
import {
  addEnergySegments,
  checkAscensionAdvance,
  getStartingSegments,
  resetRoundEnergy,
} from './energy.js';
import type { Combatant, AscensionLevel } from '../types/combat.js';

// ============================================================================
// Test fixture factory
// ============================================================================

function makeCombatant(overrides: Partial<Combatant> = {}): Combatant {
  return {
    id: 'test_combatant',
    name: 'Test Fighter',
    archetype: 'test',
    rank: 1.0,
    stamina: 100,
    maxStamina: 100,
    power: 50,
    speed: 50,
    energy: 0,
    maxEnergy: 5,
    ascensionLevel: 0,
    activeBuffs: [],
    elementalPath: 'Fire',
    reactionSkills: {
      block: { SR: 0.5, SMR: 0.4, FMR: 0.2 },
      dodge: { SR: 0.5, FMR: 0.2 },
      parry: { SR: 0.5, FMR: 0.1 },
    },
    isKO: false,
    ...overrides,
  };
}

// ============================================================================
// getStartingSegments
// ============================================================================

describe('getStartingSegments', () => {
  it('level 0 → 0 starting segments', () => {
    expect(getStartingSegments(0)).toBe(0);
  });

  it('level 1 → 0 starting segments', () => {
    expect(getStartingSegments(1)).toBe(0);
  });

  it('level 2 → 1 starting segment', () => {
    expect(getStartingSegments(2)).toBe(1);
  });

  it('level 3 → 2 starting segments', () => {
    expect(getStartingSegments(3)).toBe(2);
  });
});

// ============================================================================
// addEnergySegments — base gains (level 0, no bonus)
// ============================================================================

describe('addEnergySegments — level 0 (no accumulation bonus)', () => {
  it('actionSuccess at level 0 gains 1.0 segment', () => {
    const combatant = makeCombatant({ energy: 0, ascensionLevel: 0 });
    const result = addEnergySegments(combatant, 'actionSuccess', 'success');
    expect(result.energy).toBeCloseTo(1.0);
  });

  it('actionSuccess failure at level 0 gains 0.5 segments', () => {
    const combatant = makeCombatant({ energy: 0, ascensionLevel: 0 });
    const result = addEnergySegments(combatant, 'actionSuccess', 'failure');
    expect(result.energy).toBeCloseTo(0.5);
  });

  it('reactionSuccess at level 0 gains 0.5 segments', () => {
    const combatant = makeCombatant({ energy: 0, ascensionLevel: 0 });
    const result = addEnergySegments(combatant, 'reactionSuccess', 'success');
    expect(result.energy).toBeCloseTo(0.5);
  });

  it('reactionSuccess failure at level 0 gains 0.25 segments', () => {
    const combatant = makeCombatant({ energy: 0, ascensionLevel: 0 });
    const result = addEnergySegments(combatant, 'reactionSuccess', 'failure');
    expect(result.energy).toBeCloseTo(0.25);
  });
});

// ============================================================================
// addEnergySegments — accumulation bonus at level 1 (+25%)
// ============================================================================

describe('addEnergySegments — level 1 (+25% accumulation bonus)', () => {
  it('actionSuccess at level 1 gains 1.25 segments (1.0 × 1.25)', () => {
    const combatant = makeCombatant({ energy: 0, ascensionLevel: 1 });
    const result = addEnergySegments(combatant, 'actionSuccess', 'success');
    expect(result.energy).toBeCloseTo(1.25);
  });

  it('actionSuccess failure at level 1 gains 0.625 segments (0.5 × 1.25)', () => {
    const combatant = makeCombatant({ energy: 0, ascensionLevel: 1 });
    const result = addEnergySegments(combatant, 'actionSuccess', 'failure');
    expect(result.energy).toBeCloseTo(0.625);
  });

  it('reactionSuccess at level 1 gains 0.625 segments (0.5 × 1.25)', () => {
    const combatant = makeCombatant({ energy: 0, ascensionLevel: 1 });
    const result = addEnergySegments(combatant, 'reactionSuccess', 'success');
    expect(result.energy).toBeCloseTo(0.625);
  });

  it('reactionSuccess failure at level 1 gains 0.3125 segments (0.25 × 1.25)', () => {
    const combatant = makeCombatant({ energy: 0, ascensionLevel: 1 });
    const result = addEnergySegments(combatant, 'reactionSuccess', 'failure');
    expect(result.energy).toBeCloseTo(0.3125);
  });
});

// ============================================================================
// addEnergySegments — accumulation bonus at level 2 (+25%)
// ============================================================================

describe('addEnergySegments — level 2 (+25% accumulation bonus)', () => {
  it('actionSuccess at level 2 gains 1.25 segments (1.0 × 1.25)', () => {
    const combatant = makeCombatant({ energy: 0, ascensionLevel: 2 });
    const result = addEnergySegments(combatant, 'actionSuccess', 'success');
    expect(result.energy).toBeCloseTo(1.25);
  });

  it('reactionSuccess failure at level 2 gains 0.3125 segments (0.25 × 1.25)', () => {
    const combatant = makeCombatant({ energy: 0, ascensionLevel: 2 });
    const result = addEnergySegments(combatant, 'reactionSuccess', 'failure');
    expect(result.energy).toBeCloseTo(0.3125);
  });
});

// ============================================================================
// addEnergySegments — accumulation bonus at level 3 (+50%)
// ============================================================================

describe('addEnergySegments — level 3 (+50% accumulation bonus)', () => {
  it('actionSuccess at level 3 gains 1.5 segments (1.0 × 1.5)', () => {
    const combatant = makeCombatant({ energy: 0, ascensionLevel: 3 });
    const result = addEnergySegments(combatant, 'actionSuccess', 'success');
    expect(result.energy).toBeCloseTo(1.5);
  });

  it('actionSuccess failure at level 3 gains 0.75 segments (0.5 × 1.5)', () => {
    const combatant = makeCombatant({ energy: 0, ascensionLevel: 3 });
    const result = addEnergySegments(combatant, 'actionSuccess', 'failure');
    expect(result.energy).toBeCloseTo(0.75);
  });

  it('reactionSuccess at level 3 gains 0.75 segments (0.5 × 1.5)', () => {
    const combatant = makeCombatant({ energy: 0, ascensionLevel: 3 });
    const result = addEnergySegments(combatant, 'reactionSuccess', 'success');
    expect(result.energy).toBeCloseTo(0.75);
  });

  it('reactionSuccess failure at level 3 gains 0.375 segments (0.25 × 1.5)', () => {
    const combatant = makeCombatant({ energy: 0, ascensionLevel: 3 });
    const result = addEnergySegments(combatant, 'reactionSuccess', 'failure');
    expect(result.energy).toBeCloseTo(0.375);
  });
});

// ============================================================================
// addEnergySegments — accumulation (energy adds correctly)
// ============================================================================

describe('addEnergySegments — accumulation behavior', () => {
  it('energy accumulates across multiple calls', () => {
    let combatant = makeCombatant({ energy: 0, ascensionLevel: 0 });
    combatant = addEnergySegments(combatant, 'actionSuccess', 'success'); // +1.0
    combatant = addEnergySegments(combatant, 'reactionSuccess', 'success'); // +0.5
    combatant = addEnergySegments(combatant, 'actionSuccess', 'failure'); // +0.5
    expect(combatant.energy).toBeCloseTo(2.0);
  });

  it('does not mutate the original combatant', () => {
    const combatant = makeCombatant({ energy: 0 });
    addEnergySegments(combatant, 'actionSuccess', 'success');
    expect(combatant.energy).toBe(0);
  });

  it('returns a new combatant reference', () => {
    const combatant = makeCombatant({ energy: 0 });
    const result = addEnergySegments(combatant, 'actionSuccess', 'success');
    expect(result).not.toBe(combatant);
  });
});

// ============================================================================
// checkAscensionAdvance — thresholds 35, 95, 180
// ============================================================================

describe('checkAscensionAdvance', () => {
  it('energy below 35 stays at level 0', () => {
    const combatant = makeCombatant({ energy: 34, ascensionLevel: 0 });
    const result = checkAscensionAdvance(combatant);
    expect(result.ascensionLevel).toBe(0);
  });

  it('energy at exactly 35 advances to level 1', () => {
    const combatant = makeCombatant({ energy: 35, ascensionLevel: 0 });
    const result = checkAscensionAdvance(combatant);
    expect(result.ascensionLevel).toBe(1);
  });

  it('energy above 35 but below 95 stays at level 1', () => {
    const combatant = makeCombatant({ energy: 94, ascensionLevel: 1 });
    const result = checkAscensionAdvance(combatant);
    expect(result.ascensionLevel).toBe(1);
  });

  it('energy at exactly 95 advances to level 2', () => {
    const combatant = makeCombatant({ energy: 95, ascensionLevel: 1 });
    const result = checkAscensionAdvance(combatant);
    expect(result.ascensionLevel).toBe(2);
  });

  it('energy above 95 but below 180 stays at level 2', () => {
    const combatant = makeCombatant({ energy: 179, ascensionLevel: 2 });
    const result = checkAscensionAdvance(combatant);
    expect(result.ascensionLevel).toBe(2);
  });

  it('energy at exactly 180 advances to level 3', () => {
    const combatant = makeCombatant({ energy: 180, ascensionLevel: 2 });
    const result = checkAscensionAdvance(combatant);
    expect(result.ascensionLevel).toBe(3);
  });

  it('energy well above 180 stays at level 3', () => {
    const combatant = makeCombatant({ energy: 300, ascensionLevel: 3 });
    const result = checkAscensionAdvance(combatant);
    expect(result.ascensionLevel).toBe(3);
  });

  it('returns same reference when level does not change', () => {
    const combatant = makeCombatant({ energy: 10, ascensionLevel: 0 });
    const result = checkAscensionAdvance(combatant);
    expect(result).toBe(combatant);
  });

  it('returns new reference when level advances', () => {
    const combatant = makeCombatant({ energy: 35, ascensionLevel: 0 });
    const result = checkAscensionAdvance(combatant);
    expect(result).not.toBe(combatant);
  });

  it('does not mutate the original combatant on advancement', () => {
    const combatant = makeCombatant({ energy: 35, ascensionLevel: 0 });
    checkAscensionAdvance(combatant);
    expect(combatant.ascensionLevel).toBe(0);
  });

  it('can advance multiple levels at once (level 0 → 3 from 180+ energy)', () => {
    // When energy jumps past multiple thresholds in one check
    const combatant = makeCombatant({ energy: 200, ascensionLevel: 0 });
    const result = checkAscensionAdvance(combatant);
    expect(result.ascensionLevel).toBe(3);
  });
});

// ============================================================================
// resetRoundEnergy
// ============================================================================

describe('resetRoundEnergy', () => {
  it('level 0 combatant resets to 0 energy', () => {
    const combatant = makeCombatant({ energy: 4.5, ascensionLevel: 0 });
    const result = resetRoundEnergy(combatant);
    expect(result.energy).toBe(0);
  });

  it('level 1 combatant resets to 0 energy', () => {
    const combatant = makeCombatant({ energy: 3.0, ascensionLevel: 1 });
    const result = resetRoundEnergy(combatant);
    expect(result.energy).toBe(0);
  });

  it('level 2 combatant resets to 1 energy segment', () => {
    const combatant = makeCombatant({ energy: 2.5, ascensionLevel: 2 });
    const result = resetRoundEnergy(combatant);
    expect(result.energy).toBe(1);
  });

  it('level 3 combatant resets to 2 energy segments', () => {
    const combatant = makeCombatant({ energy: 5.0, ascensionLevel: 3 });
    const result = resetRoundEnergy(combatant);
    expect(result.energy).toBe(2);
  });

  it('does not mutate the original combatant', () => {
    const combatant = makeCombatant({ energy: 4.5, ascensionLevel: 2 });
    resetRoundEnergy(combatant);
    expect(combatant.energy).toBe(4.5);
  });

  it('returns a new combatant reference', () => {
    const combatant = makeCombatant({ energy: 4.5, ascensionLevel: 2 });
    const result = resetRoundEnergy(combatant);
    expect(result).not.toBe(combatant);
  });

  it('preserves all other combatant fields on reset', () => {
    const combatant = makeCombatant({ energy: 3.0, ascensionLevel: 2, stamina: 80, power: 60 });
    const result = resetRoundEnergy(combatant);
    expect(result.stamina).toBe(80);
    expect(result.power).toBe(60);
    expect(result.ascensionLevel).toBe(2);
    expect(result.id).toBe(combatant.id);
  });
});

// ============================================================================
// Cross-system: ascension levels produce correct starting segments
// ============================================================================

describe('ascension levels and starting segments integration', () => {
  const cases: Array<{ level: AscensionLevel; expectedStart: number }> = [
    { level: 0, expectedStart: 0 },
    { level: 1, expectedStart: 0 },
    { level: 2, expectedStart: 1 },
    { level: 3, expectedStart: 2 },
  ];

  for (const { level, expectedStart } of cases) {
    it(`level ${level} → ${expectedStart} starting segments on reset`, () => {
      const combatant = makeCombatant({ energy: 99, ascensionLevel: level });
      const reset = resetRoundEnergy(combatant);
      expect(reset.energy).toBe(expectedStart);
      expect(getStartingSegments(level)).toBe(expectedStart);
    });
  }
});
