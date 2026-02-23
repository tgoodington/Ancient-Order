/**
 * elementalPaths.test.ts — Tests for elemental path buff/debuff application.
 *
 * Tests cover:
 *   - Each reaction path (Fire, Air, Light) boosts own defensive SR
 *   - Each action path (Water, Earth, Shadow) debuffs target defensive SR
 *   - Special defense constraint enforced per path (getSpecialForceDefense)
 *   - Immutability: input combatants are never mutated
 *   - ELEMENTAL_PATH_CONFIG shape validation
 */

import { describe, it, expect } from 'vitest';
import {
  ELEMENTAL_PATH_CONFIG,
  applyPathBuff,
  applyPathDebuff,
  getSpecialForceDefense,
} from './elementalPaths.js';
import type { Combatant } from '../types/combat.js';

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
// ELEMENTAL_PATH_CONFIG shape tests
// ============================================================================

describe('ELEMENTAL_PATH_CONFIG', () => {
  it('contains all 6 elemental paths', () => {
    const paths = Object.keys(ELEMENTAL_PATH_CONFIG);
    expect(paths).toContain('Fire');
    expect(paths).toContain('Water');
    expect(paths).toContain('Air');
    expect(paths).toContain('Earth');
    expect(paths).toContain('Shadow');
    expect(paths).toContain('Light');
    expect(paths).toHaveLength(6);
  });

  it('classifies Fire, Air, Light as reaction paths', () => {
    expect(ELEMENTAL_PATH_CONFIG.Fire.type).toBe('reaction');
    expect(ELEMENTAL_PATH_CONFIG.Air.type).toBe('reaction');
    expect(ELEMENTAL_PATH_CONFIG.Light.type).toBe('reaction');
  });

  it('classifies Water, Earth, Shadow as action paths', () => {
    expect(ELEMENTAL_PATH_CONFIG.Water.type).toBe('action');
    expect(ELEMENTAL_PATH_CONFIG.Earth.type).toBe('action');
    expect(ELEMENTAL_PATH_CONFIG.Shadow.type).toBe('action');
  });

  it('assigns correct defenseBoost per path', () => {
    expect(ELEMENTAL_PATH_CONFIG.Fire.defenseBoost).toBe('parry');
    expect(ELEMENTAL_PATH_CONFIG.Water.defenseBoost).toBe('dodge');
    expect(ELEMENTAL_PATH_CONFIG.Air.defenseBoost).toBe('dodge');
    expect(ELEMENTAL_PATH_CONFIG.Earth.defenseBoost).toBe('block');
    expect(ELEMENTAL_PATH_CONFIG.Shadow.defenseBoost).toBe('parry');
    expect(ELEMENTAL_PATH_CONFIG.Light.defenseBoost).toBe('block');
  });

  it('assigns correct specialForces per path (matches defenseBoost)', () => {
    expect(ELEMENTAL_PATH_CONFIG.Fire.specialForces).toBe('parry');
    expect(ELEMENTAL_PATH_CONFIG.Water.specialForces).toBe('dodge');
    expect(ELEMENTAL_PATH_CONFIG.Air.specialForces).toBe('dodge');
    expect(ELEMENTAL_PATH_CONFIG.Earth.specialForces).toBe('block');
    expect(ELEMENTAL_PATH_CONFIG.Shadow.specialForces).toBe('parry');
    expect(ELEMENTAL_PATH_CONFIG.Light.specialForces).toBe('block');
  });
});

// ============================================================================
// applyPathBuff — reaction paths boost own SR
// ============================================================================

describe('applyPathBuff', () => {
  it('Fire path adds a parrySR_boost buff to the combatant', () => {
    const combatant = makeCombatant({ elementalPath: 'Fire' });
    const result = applyPathBuff(combatant, 'Fire');

    expect(result.activeBuffs).toHaveLength(1);
    const buff = result.activeBuffs[0];
    expect(buff.type).toBe('parrySR_boost');
    expect(buff.source).toBe('Fire');
    expect(buff.modifier).toBeGreaterThan(0);
  });

  it('Air path adds a dodgeSR_boost buff to the combatant', () => {
    const combatant = makeCombatant({ elementalPath: 'Air' });
    const result = applyPathBuff(combatant, 'Air');

    expect(result.activeBuffs).toHaveLength(1);
    const buff = result.activeBuffs[0];
    expect(buff.type).toBe('dodgeSR_boost');
    expect(buff.source).toBe('Air');
    expect(buff.modifier).toBeGreaterThan(0);
  });

  it('Light path adds a blockSR_boost buff to the combatant', () => {
    const combatant = makeCombatant({ elementalPath: 'Light' });
    const result = applyPathBuff(combatant, 'Light');

    expect(result.activeBuffs).toHaveLength(1);
    const buff = result.activeBuffs[0];
    expect(buff.type).toBe('blockSR_boost');
    expect(buff.source).toBe('Light');
    expect(buff.modifier).toBeGreaterThan(0);
  });

  it('buff modifier is positive (SR increase)', () => {
    const combatant = makeCombatant();

    const fireBuff = applyPathBuff(combatant, 'Fire').activeBuffs[0];
    const airBuff = applyPathBuff(combatant, 'Air').activeBuffs[0];
    const lightBuff = applyPathBuff(combatant, 'Light').activeBuffs[0];

    expect(fireBuff.modifier).toBe(0.1);
    expect(airBuff.modifier).toBe(0.1);
    expect(lightBuff.modifier).toBe(0.1);
  });

  it('does not mutate the original combatant', () => {
    const combatant = makeCombatant();
    const originalBuffCount = combatant.activeBuffs.length;
    applyPathBuff(combatant, 'Fire');
    expect(combatant.activeBuffs).toHaveLength(originalBuffCount);
  });

  it('returns a new combatant reference', () => {
    const combatant = makeCombatant();
    const result = applyPathBuff(combatant, 'Fire');
    expect(result).not.toBe(combatant);
  });

  it('appends buff without removing existing buffs', () => {
    const existingBuff = { type: 'power_boost', source: 'test', duration: 1, modifier: 10 };
    const combatant = makeCombatant({ activeBuffs: [existingBuff] });
    const result = applyPathBuff(combatant, 'Fire');

    expect(result.activeBuffs).toHaveLength(2);
    expect(result.activeBuffs[0]).toBe(existingBuff);
  });
});

// ============================================================================
// applyPathDebuff — action paths debuff target SR
// ============================================================================

describe('applyPathDebuff', () => {
  it('Water path adds a dodgeSR debuff to the target', () => {
    const target = makeCombatant();
    const result = applyPathDebuff(target, 'Water');

    expect(result.activeBuffs).toHaveLength(1);
    const debuff = result.activeBuffs[0];
    expect(debuff.type).toBe('dodgeSR_debuff');
    expect(debuff.source).toBe('Water');
    expect(debuff.modifier).toBeLessThan(0);
  });

  it('Earth path adds a blockSR debuff to the target', () => {
    const target = makeCombatant();
    const result = applyPathDebuff(target, 'Earth');

    expect(result.activeBuffs).toHaveLength(1);
    const debuff = result.activeBuffs[0];
    expect(debuff.type).toBe('blockSR_debuff');
    expect(debuff.source).toBe('Earth');
    expect(debuff.modifier).toBeLessThan(0);
  });

  it('Shadow path adds a parrySR debuff to the target', () => {
    const target = makeCombatant();
    const result = applyPathDebuff(target, 'Shadow');

    expect(result.activeBuffs).toHaveLength(1);
    const debuff = result.activeBuffs[0];
    expect(debuff.type).toBe('parrySR_debuff');
    expect(debuff.source).toBe('Shadow');
    expect(debuff.modifier).toBeLessThan(0);
  });

  it('debuff modifier is negative (SR reduction)', () => {
    const target = makeCombatant();

    const waterDebuff = applyPathDebuff(target, 'Water').activeBuffs[0];
    const earthDebuff = applyPathDebuff(target, 'Earth').activeBuffs[0];
    const shadowDebuff = applyPathDebuff(target, 'Shadow').activeBuffs[0];

    expect(waterDebuff.modifier).toBe(-0.1);
    expect(earthDebuff.modifier).toBe(-0.1);
    expect(shadowDebuff.modifier).toBe(-0.1);
  });

  it('does not mutate the original target', () => {
    const target = makeCombatant();
    const originalBuffCount = target.activeBuffs.length;
    applyPathDebuff(target, 'Water');
    expect(target.activeBuffs).toHaveLength(originalBuffCount);
  });

  it('returns a new combatant reference', () => {
    const target = makeCombatant();
    const result = applyPathDebuff(target, 'Earth');
    expect(result).not.toBe(target);
  });

  it('appends debuff without removing existing buffs', () => {
    const existingBuff = { type: 'power_boost', source: 'test', duration: 1, modifier: 10 };
    const target = makeCombatant({ activeBuffs: [existingBuff] });
    const result = applyPathDebuff(target, 'Shadow');

    expect(result.activeBuffs).toHaveLength(2);
    expect(result.activeBuffs[0]).toBe(existingBuff);
  });
});

// ============================================================================
// getSpecialForceDefense — defense constraint per path
// ============================================================================

describe('getSpecialForceDefense', () => {
  it('Fire forces parry defense', () => {
    expect(getSpecialForceDefense('Fire')).toBe('parry');
  });

  it('Water forces dodge defense', () => {
    expect(getSpecialForceDefense('Water')).toBe('dodge');
  });

  it('Air forces dodge defense', () => {
    expect(getSpecialForceDefense('Air')).toBe('dodge');
  });

  it('Earth forces block defense', () => {
    expect(getSpecialForceDefense('Earth')).toBe('block');
  });

  it('Shadow forces parry defense', () => {
    expect(getSpecialForceDefense('Shadow')).toBe('parry');
  });

  it('Light forces block defense', () => {
    expect(getSpecialForceDefense('Light')).toBe('block');
  });

  it('returns a valid DefenseType for every path', () => {
    const validDefenseTypes = ['block', 'dodge', 'parry', 'defenseless'];
    const paths = ['Fire', 'Water', 'Air', 'Earth', 'Shadow', 'Light'] as const;

    for (const path of paths) {
      const forced = getSpecialForceDefense(path);
      expect(validDefenseTypes).toContain(forced);
    }
  });
});

// ============================================================================
// Reaction vs. Action path separation
// ============================================================================

describe('reaction paths only buff self, action paths only debuff target', () => {
  it('applying Fire buff produces a positive modifier (self-enhancement)', () => {
    const self = makeCombatant({ elementalPath: 'Fire' });
    const buffed = applyPathBuff(self, 'Fire');
    const buff = buffed.activeBuffs.find((b) => b.source === 'Fire');
    expect(buff?.modifier).toBeGreaterThan(0);
  });

  it('applying Water debuff produces a negative modifier (target reduction)', () => {
    const target = makeCombatant();
    const debuffed = applyPathDebuff(target, 'Water');
    const debuff = debuffed.activeBuffs.find((b) => b.source === 'Water');
    expect(debuff?.modifier).toBeLessThan(0);
  });

  it('reaction path buff targets the correct defense type (Fire → parry)', () => {
    const self = makeCombatant();
    const buffed = applyPathBuff(self, 'Fire');
    expect(buffed.activeBuffs[0].type).toContain('parry');
  });

  it('action path debuff targets the correct defense type (Earth → block)', () => {
    const target = makeCombatant();
    const debuffed = applyPathDebuff(target, 'Earth');
    expect(debuffed.activeBuffs[0].type).toContain('block');
  });
});
