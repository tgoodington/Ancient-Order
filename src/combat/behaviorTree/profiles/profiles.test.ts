import { describe, it, expect } from 'vitest';
import { elenaProfile } from './elena.js';
import { larsProfile } from './lars.js';
import { kadeProfile } from './kade.js';
import { getProfile } from './index.js';
import type { ArchetypeProfile } from '../../../types/combat.js';

const ALL_PROFILES: ArchetypeProfile[] = [elenaProfile, larsProfile, kadeProfile];
const ACTION_TYPES = ['ATTACK', 'DEFEND', 'EVADE', 'SPECIAL', 'GROUP'] as const;
const EXPECTED_FACTORS = [
  'ownStamina',
  'allyInDanger',
  'targetVulnerability',
  'energyAvailability',
  'speedAdvantage',
  'roundPhase',
  'teamBalance',
];

describe('archetype profiles structure', () => {
  it.each([
    ['Elena', elenaProfile],
    ['Lars', larsProfile],
    ['Kade', kadeProfile],
  ])('%s has all 5 action types in baseScores', (_name, profile) => {
    for (const type of ACTION_TYPES) {
      expect(profile.baseScores).toHaveProperty(type);
      expect(typeof profile.baseScores[type]).toBe('number');
    }
  });

  it.each([
    ['Elena', elenaProfile],
    ['Lars', larsProfile],
    ['Kade', kadeProfile],
  ])('%s has weights for all 7 factors', (_name, profile) => {
    for (const factor of EXPECTED_FACTORS) {
      expect(profile.factorWeights).toHaveProperty(factor);
      expect(typeof profile.factorWeights[factor]).toBe('number');
    }
  });

  it.each([
    ['Elena', elenaProfile],
    ['Lars', larsProfile],
    ['Kade', kadeProfile],
  ])('%s has a valid elementalPath', (_name, profile) => {
    const validPaths = ['Fire', 'Water', 'Air', 'Earth', 'Shadow', 'Light'];
    expect(validPaths).toContain(profile.elementalPath);
  });
});

describe('archetype profile values (from design spec)', () => {
  describe('Elena (Loyal Scout)', () => {
    it('has Light path', () => {
      expect(elenaProfile.elementalPath).toBe('Light');
    });

    it('has high allyInDanger weight (1.8 — most protective)', () => {
      expect(elenaProfile.factorWeights.allyInDanger).toBe(1.8);
    });

    it('has low speedAdvantage weight (0.3 — not aggressive)', () => {
      expect(elenaProfile.factorWeights.speedAdvantage).toBe(0.3);
    });

    it('DEFEND base score higher than ATTACK base score', () => {
      expect(elenaProfile.baseScores.DEFEND).toBeGreaterThan(elenaProfile.baseScores.ATTACK);
    });

    it('exact base scores from spec', () => {
      expect(elenaProfile.baseScores.ATTACK).toBe(0.3);
      expect(elenaProfile.baseScores.DEFEND).toBe(0.5);
      expect(elenaProfile.baseScores.EVADE).toBe(0.3);
      expect(elenaProfile.baseScores.SPECIAL).toBe(0.4);
      expect(elenaProfile.baseScores.GROUP).toBe(0.2);
    });
  });

  describe('Lars (Scheming Merchant)', () => {
    it('has Earth path', () => {
      expect(larsProfile.elementalPath).toBe('Earth');
    });

    it('has high ownStamina weight (1.5 — self-preserving)', () => {
      expect(larsProfile.factorWeights.ownStamina).toBe(1.5);
    });

    it('has high energyAvailability weight (1.4 — careful resource management)', () => {
      expect(larsProfile.factorWeights.energyAvailability).toBe(1.4);
    });

    it('exact base scores from spec', () => {
      expect(larsProfile.baseScores.ATTACK).toBe(0.4);
      expect(larsProfile.baseScores.DEFEND).toBe(0.5);
      expect(larsProfile.baseScores.EVADE).toBe(0.4);
      expect(larsProfile.baseScores.SPECIAL).toBe(0.3);
      expect(larsProfile.baseScores.GROUP).toBe(0.2);
    });
  });

  describe('Kade (Rogue Outlaw)', () => {
    it('has Fire path', () => {
      expect(kadeProfile.elementalPath).toBe('Fire');
    });

    it('has high targetVulnerability weight (1.6 — opportunist)', () => {
      expect(kadeProfile.factorWeights.targetVulnerability).toBe(1.6);
    });

    it('has high speedAdvantage weight (1.5 — exploits slow targets)', () => {
      expect(kadeProfile.factorWeights.speedAdvantage).toBe(1.5);
    });

    it('has low allyInDanger weight (0.4 — ignores team)', () => {
      expect(kadeProfile.factorWeights.allyInDanger).toBe(0.4);
    });

    it('ATTACK base score is highest of all 3 archetypes', () => {
      expect(kadeProfile.baseScores.ATTACK).toBeGreaterThan(elenaProfile.baseScores.ATTACK);
      expect(kadeProfile.baseScores.ATTACK).toBeGreaterThan(larsProfile.baseScores.ATTACK);
    });

    it('DEFEND base score is lowest (aggressive, not defensive)', () => {
      expect(kadeProfile.baseScores.DEFEND).toBeLessThan(elenaProfile.baseScores.DEFEND);
      expect(kadeProfile.baseScores.DEFEND).toBeLessThan(larsProfile.baseScores.DEFEND);
    });

    it('exact base scores from spec', () => {
      expect(kadeProfile.baseScores.ATTACK).toBe(0.6);
      expect(kadeProfile.baseScores.DEFEND).toBe(0.2);
      expect(kadeProfile.baseScores.EVADE).toBe(0.3);
      expect(kadeProfile.baseScores.SPECIAL).toBe(0.4);
      expect(kadeProfile.baseScores.GROUP).toBe(0.1);
    });
  });
});

describe('profile registry', () => {
  it('retrieves Elena by archetype ID', () => {
    const profile = getProfile('elena_loyal_scout');
    expect(profile).toBeDefined();
    expect(profile?.elementalPath).toBe('Light');
  });

  it('retrieves Lars by archetype ID', () => {
    const profile = getProfile('lars_scheming_merchant');
    expect(profile).toBeDefined();
    expect(profile?.elementalPath).toBe('Earth');
  });

  it('retrieves Kade by archetype ID', () => {
    const profile = getProfile('kade_rogue_outlaw');
    expect(profile).toBeDefined();
    expect(profile?.elementalPath).toBe('Fire');
  });

  it('returns undefined for unknown archetype ID', () => {
    expect(getProfile('unknown_archetype')).toBeUndefined();
  });

  it('all 3 profiles retrievable', () => {
    expect(ALL_PROFILES).toHaveLength(3);
  });
});
