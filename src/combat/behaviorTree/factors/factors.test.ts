import { describe, it, expect } from 'vitest';
import { ownStaminaFactor } from './ownStamina.js';
import { allyInDangerFactor } from './allyInDanger.js';
import { targetVulnerabilityFactor } from './targetVulnerability.js';
import { energyAvailabilityFactor } from './energyAvailability.js';
import { speedAdvantageFactor } from './speedAdvantage.js';
import { roundPhaseFactor } from './roundPhase.js';
import { teamBalanceFactor } from './teamBalance.js';
import { FACTORS } from './index.js';
import type { CombatPerception, TargetPerception } from '../../../types/combat.js';

// ─── Fixture Factories ────────────────────────────────────────────────────────

function makePerception(overrides: Partial<CombatPerception> = {}): CombatPerception {
  return {
    selfId: 'self',
    selfStaminaPct: 0.8,
    selfEnergy: 0,
    selfAscension: 0,
    selfRank: 3.0,
    selfPath: 'Fire',
    allies: [],
    lowestAllyStaminaPct: 1.0,
    teamAvgStaminaPct: 0.8,
    allyCount: 0,
    enemies: [],
    weakestEnemyStaminaPct: 0.5,
    enemyAvgStaminaPct: 0.5,
    enemyCount: 1,
    round: 1,
    ...overrides,
  };
}

function makeTarget(overrides: Partial<TargetPerception> = {}): TargetPerception {
  return {
    id: 'target',
    staminaPct: 0.5,
    speedDelta: 0,
    rankDelta: 0,
    power: 20,
    ...overrides,
  };
}

// ─── OwnStamina ───────────────────────────────────────────────────────────────

describe('ownStaminaFactor', () => {
  it('at selfStaminaPct=0.0: EVADE score is 0.9 (low bracket start)', () => {
    const p = makePerception({ selfStaminaPct: 0.0 });
    const scores = ownStaminaFactor.evaluate(p, null);
    expect(scores.EVADE).toBeCloseTo(0.9);
    expect(scores.ATTACK).toBeCloseTo(-0.5);
  });

  it('at selfStaminaPct=0.0: ATTACK score is -0.5 (low bracket start)', () => {
    const p = makePerception({ selfStaminaPct: 0.0 });
    const scores = ownStaminaFactor.evaluate(p, null);
    expect(scores.ATTACK).toBeCloseTo(-0.5);
  });

  it('low bracket interior (pct=0.15): EVADE and ATTACK interpolated (t=0.5 between LOW and MID)', () => {
    const p = makePerception({ selfStaminaPct: 0.15 });
    const scores = ownStaminaFactor.evaluate(p, null);
    // t = 0.15 / 0.3 = 0.5; EVADE = 0.9 + (0.2 - 0.9) * 0.5 = 0.55
    expect(scores.EVADE).toBeCloseTo(0.55);
    // ATTACK = -0.5 + (0.0 - (-0.5)) * 0.5 = -0.25
    expect(scores.ATTACK).toBeCloseTo(-0.25);
  });

  it('at selfStaminaPct=0.3: transitions to mid bracket (EVADE near 0.2, ATTACK near 0)', () => {
    const p = makePerception({ selfStaminaPct: 0.3 });
    const scores = ownStaminaFactor.evaluate(p, null);
    expect(scores.EVADE).toBeCloseTo(0.2);
    expect(scores.ATTACK).toBeCloseTo(0.0);
  });

  it('mid bracket interior (pct=0.45): EVADE and ATTACK interpolated (t=0.5 between MID and HIGH)', () => {
    const p = makePerception({ selfStaminaPct: 0.45 });
    const scores = ownStaminaFactor.evaluate(p, null);
    // t = (0.45 - 0.3) / 0.3 = 0.5; EVADE = 0.2 + (-0.3 - 0.2) * 0.5 = -0.05
    expect(scores.EVADE).toBeCloseTo(-0.05);
    // ATTACK = 0.0 + (0.2 - 0.0) * 0.5 = 0.1
    expect(scores.ATTACK).toBeCloseTo(0.1);
  });

  it('at selfStaminaPct=0.6: transitions to high bracket (ATTACK near 0.2, EVADE near -0.3)', () => {
    const p = makePerception({ selfStaminaPct: 0.6 });
    const scores = ownStaminaFactor.evaluate(p, null);
    expect(scores.ATTACK).toBeCloseTo(0.2);
    expect(scores.EVADE).toBeCloseTo(-0.3);
  });

  it('high stamina (> 0.6): returns HIGH_SCORES flat', () => {
    const p = makePerception({ selfStaminaPct: 0.9 });
    const scores = ownStaminaFactor.evaluate(p, null);
    expect(scores.ATTACK).toBeCloseTo(0.2);
    expect(scores.EVADE).toBeCloseTo(-0.3);
    expect(scores.SPECIAL).toBeCloseTo(0.1);
  });

  it('GROUP score is always 0 in all brackets', () => {
    [0.0, 0.15, 0.45, 0.9].forEach((pct) => {
      const p = makePerception({ selfStaminaPct: pct });
      expect(ownStaminaFactor.evaluate(p, null).GROUP).toBeCloseTo(0);
    });
  });
});

// ─── AllyInDanger ─────────────────────────────────────────────────────────────

describe('allyInDangerFactor', () => {
  it('critical ally danger (< 0.3): DEFEND score near 0.8', () => {
    const p = makePerception({ lowestAllyStaminaPct: 0.1, allyCount: 1 });
    const scores = allyInDangerFactor.evaluate(p, null);
    expect(scores.DEFEND).toBeCloseTo(0.8, 1);
  });

  it('critical ally danger: ATTACK score negative', () => {
    const p = makePerception({ lowestAllyStaminaPct: 0.15, allyCount: 1 });
    const scores = allyInDangerFactor.evaluate(p, null);
    expect(scores.ATTACK).toBeLessThan(0);
  });

  it('moderate ally danger (0.3-0.6): DEFEND score near 0.3', () => {
    const p = makePerception({ lowestAllyStaminaPct: 0.45, allyCount: 1 });
    const scores = allyInDangerFactor.evaluate(p, null);
    expect(scores.DEFEND).toBeCloseTo(0.3, 1);
  });

  it('healthy allies (> 0.6): all zeros', () => {
    const p = makePerception({ lowestAllyStaminaPct: 0.9, allyCount: 1 });
    const scores = allyInDangerFactor.evaluate(p, null);
    expect(scores.ATTACK).toBeCloseTo(0);
    expect(scores.DEFEND).toBeCloseTo(0);
    expect(scores.EVADE).toBeCloseTo(0);
    expect(scores.SPECIAL).toBeCloseTo(0);
  });

  it('no allies: returns all zeros regardless of lowestAllyStaminaPct', () => {
    const p = makePerception({ lowestAllyStaminaPct: 0.1, allyCount: 0 });
    const scores = allyInDangerFactor.evaluate(p, null);
    expect(scores.DEFEND).toBeCloseTo(0);
    expect(scores.ATTACK).toBeCloseTo(0);
  });
});

// ─── TargetVulnerability ──────────────────────────────────────────────────────

describe('targetVulnerabilityFactor', () => {
  it('null target: returns all zeros', () => {
    const p = makePerception();
    const scores = targetVulnerabilityFactor.evaluate(p, null);
    expect(scores.ATTACK).toBeCloseTo(0);
    expect(scores.DEFEND).toBeCloseTo(0);
    expect(scores.SPECIAL).toBeCloseTo(0);
  });

  it('critical target (< 0.25): ATTACK near 0.8', () => {
    const scores = targetVulnerabilityFactor.evaluate(makePerception(), makeTarget({ staminaPct: 0.1 }));
    expect(scores.ATTACK).toBeCloseTo(0.8, 1);
  });

  it('critical target: SPECIAL near 0.6', () => {
    const scores = targetVulnerabilityFactor.evaluate(makePerception(), makeTarget({ staminaPct: 0.1 }));
    expect(scores.SPECIAL).toBeCloseTo(0.6, 1);
  });

  it('moderate target (0.25-0.5): ATTACK near 0.4', () => {
    const scores = targetVulnerabilityFactor.evaluate(makePerception(), makeTarget({ staminaPct: 0.35 }));
    expect(scores.ATTACK).toBeCloseTo(0.4, 1);
  });

  it('healthy target (> 0.5): ATTACK near 0.1', () => {
    const scores = targetVulnerabilityFactor.evaluate(makePerception(), makeTarget({ staminaPct: 0.9 }));
    expect(scores.ATTACK).toBeCloseTo(0.1, 1);
  });

  it('healthy target: DEFEND is 0', () => {
    const scores = targetVulnerabilityFactor.evaluate(makePerception(), makeTarget({ staminaPct: 0.8 }));
    expect(scores.DEFEND).toBeCloseTo(0);
  });
});

// ─── EnergyAvailability ───────────────────────────────────────────────────────

describe('energyAvailabilityFactor', () => {
  it('high energy (>= 3): SPECIAL near 0.7', () => {
    const p = makePerception({ selfEnergy: 4 });
    expect(energyAvailabilityFactor.evaluate(p, null).SPECIAL).toBeCloseTo(0.7);
  });

  it('high energy: EVADE penalized (-0.2)', () => {
    const p = makePerception({ selfEnergy: 3 });
    expect(energyAvailabilityFactor.evaluate(p, null).EVADE).toBeCloseTo(-0.2);
  });

  it('mid energy (1-2): SPECIAL near 0.3', () => {
    const p = makePerception({ selfEnergy: 2 });
    expect(energyAvailabilityFactor.evaluate(p, null).SPECIAL).toBeCloseTo(0.3);
  });

  it('mid energy: ATTACK and EVADE are 0', () => {
    const p = makePerception({ selfEnergy: 1 });
    const scores = energyAvailabilityFactor.evaluate(p, null);
    expect(scores.ATTACK).toBeCloseTo(0);
    expect(scores.EVADE).toBeCloseTo(0);
  });

  it('zero energy: ATTACK and EVADE are 0.1', () => {
    const p = makePerception({ selfEnergy: 0 });
    const scores = energyAvailabilityFactor.evaluate(p, null);
    expect(scores.ATTACK).toBeCloseTo(0.1);
    expect(scores.EVADE).toBeCloseTo(0.1);
  });

  it('zero energy: SPECIAL is 0', () => {
    const p = makePerception({ selfEnergy: 0 });
    expect(energyAvailabilityFactor.evaluate(p, null).SPECIAL).toBeCloseTo(0);
  });
});

// ─── SpeedAdvantage ───────────────────────────────────────────────────────────

describe('speedAdvantageFactor', () => {
  it('null target: returns all zeros', () => {
    const scores = speedAdvantageFactor.evaluate(makePerception(), null);
    expect(scores.ATTACK).toBeCloseTo(0);
    expect(scores.EVADE).toBeCloseTo(0);
  });

  it('significantly faster (speedDelta > 0.3): ATTACK near 0.6', () => {
    const scores = speedAdvantageFactor.evaluate(makePerception(), makeTarget({ speedDelta: 0.5 }));
    expect(scores.ATTACK).toBeCloseTo(0.6, 1);
  });

  it('significantly faster: SPECIAL near 0.3', () => {
    const scores = speedAdvantageFactor.evaluate(makePerception(), makeTarget({ speedDelta: 0.6 }));
    expect(scores.SPECIAL).toBeCloseTo(0.3, 1);
  });

  it('slight advantage (speedDelta 0-0.3): ATTACK near 0.2', () => {
    const scores = speedAdvantageFactor.evaluate(makePerception(), makeTarget({ speedDelta: 0.0 }));
    expect(scores.ATTACK).toBeCloseTo(0.2, 1);
  });

  it('slower (speedDelta < 0): ATTACK negative, DEFEND and EVADE positive', () => {
    const scores = speedAdvantageFactor.evaluate(makePerception(), makeTarget({ speedDelta: -0.3 }));
    expect(scores.ATTACK).toBeLessThan(0);
    expect(scores.DEFEND).toBeGreaterThan(0);
    expect(scores.EVADE).toBeGreaterThan(0);
  });
});

// ─── RoundPhase ───────────────────────────────────────────────────────────────

describe('roundPhaseFactor', () => {
  it('early rounds (1-2): EVADE positive and highest offensive type', () => {
    const p1 = makePerception({ round: 1 });
    const s1 = roundPhaseFactor.evaluate(p1, null);
    expect(s1.EVADE).toBeCloseTo(0.3);
    expect(s1.SPECIAL).toBeLessThan(0);
  });

  it('early round 2: same as round 1', () => {
    const s1 = roundPhaseFactor.evaluate(makePerception({ round: 1 }), null);
    const s2 = roundPhaseFactor.evaluate(makePerception({ round: 2 }), null);
    expect(s1.ATTACK).toBeCloseTo(s2.ATTACK);
  });

  it('mid rounds (3-5): ATTACK and SPECIAL both 0.2', () => {
    const scores = roundPhaseFactor.evaluate(makePerception({ round: 4 }), null);
    expect(scores.ATTACK).toBeCloseTo(0.2);
    expect(scores.SPECIAL).toBeCloseTo(0.2);
    expect(scores.DEFEND).toBeCloseTo(0);
    expect(scores.EVADE).toBeCloseTo(0);
  });

  it('late rounds (6+): SPECIAL highest at 0.4', () => {
    const scores = roundPhaseFactor.evaluate(makePerception({ round: 8 }), null);
    expect(scores.SPECIAL).toBeCloseTo(0.4);
    expect(scores.ATTACK).toBeCloseTo(0.3);
    expect(scores.DEFEND).toBeLessThan(0);
    expect(scores.EVADE).toBeLessThan(0);
  });

  it('boundary round 3 is mid', () => {
    const scores = roundPhaseFactor.evaluate(makePerception({ round: 3 }), null);
    expect(scores.ATTACK).toBeCloseTo(0.2);
  });

  it('boundary round 6 is late', () => {
    const scores = roundPhaseFactor.evaluate(makePerception({ round: 6 }), null);
    expect(scores.SPECIAL).toBeCloseTo(0.4);
  });
});

// ─── TeamBalance ──────────────────────────────────────────────────────────────

describe('teamBalanceFactor', () => {
  it('winning (teamAvg > enemyAvg + 0.2): ATTACK positive', () => {
    const p = makePerception({ teamAvgStaminaPct: 0.8, enemyAvgStaminaPct: 0.4 });
    const scores = teamBalanceFactor.evaluate(p, null);
    expect(scores.ATTACK).toBeCloseTo(0.3);
    expect(scores.SPECIAL).toBeCloseTo(0.2);
  });

  it('winning: DEFEND and EVADE penalized', () => {
    const p = makePerception({ teamAvgStaminaPct: 0.9, enemyAvgStaminaPct: 0.5 });
    const scores = teamBalanceFactor.evaluate(p, null);
    expect(scores.DEFEND).toBeLessThan(0);
    expect(scores.EVADE).toBeLessThan(0);
  });

  it('roughly even (±0.2): all zeros', () => {
    const p = makePerception({ teamAvgStaminaPct: 0.6, enemyAvgStaminaPct: 0.6 });
    const scores = teamBalanceFactor.evaluate(p, null);
    expect(scores.ATTACK).toBeCloseTo(0);
    expect(scores.DEFEND).toBeCloseTo(0);
    expect(scores.EVADE).toBeCloseTo(0);
  });

  it('losing (teamAvg < enemyAvg - 0.2): DEFEND highest', () => {
    const p = makePerception({ teamAvgStaminaPct: 0.3, enemyAvgStaminaPct: 0.7 });
    const scores = teamBalanceFactor.evaluate(p, null);
    expect(scores.DEFEND).toBeCloseTo(0.4);
    expect(scores.EVADE).toBeCloseTo(0.3);
    expect(scores.ATTACK).toBeLessThan(0);
  });

  it('losing: GROUP score is 0.2', () => {
    const p = makePerception({ teamAvgStaminaPct: 0.2, enemyAvgStaminaPct: 0.9 });
    const scores = teamBalanceFactor.evaluate(p, null);
    expect(scores.GROUP).toBeCloseTo(0.2);
  });
});

// ─── Factor Registry ──────────────────────────────────────────────────────────

describe('FACTORS registry', () => {
  it('contains exactly 7 factors', () => {
    expect(FACTORS).toHaveLength(7);
  });

  it('all factors have unique names', () => {
    const names = FACTORS.map((f) => f.name);
    const uniqueNames = new Set(names);
    expect(uniqueNames.size).toBe(names.length);
  });

  it('all factors implement evaluate()', () => {
    for (const factor of FACTORS) {
      expect(typeof factor.evaluate).toBe('function');
    }
  });

  it('all factors have expected names', () => {
    const names = FACTORS.map((f) => f.name);
    expect(names).toContain('ownStamina');
    expect(names).toContain('allyInDanger');
    expect(names).toContain('targetVulnerability');
    expect(names).toContain('energyAvailability');
    expect(names).toContain('speedAdvantage');
    expect(names).toContain('roundPhase');
    expect(names).toContain('teamBalance');
  });
});
