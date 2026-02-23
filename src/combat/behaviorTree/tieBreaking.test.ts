import { describe, it, expect } from 'vitest';
import { PATH_TIEBREAK, resolveTie } from './tieBreaking.js';
import type { ScoredCandidate, ElementalPath } from '../../types/combat.js';

function makeCandidate(
  actionType: ScoredCandidate['actionType'],
  targetId: string | null,
  score: number,
): ScoredCandidate {
  return { actionType, targetId, score, scoreBreakdown: {} };
}

describe('PATH_TIEBREAK table', () => {
  it('Fire path — ATTACK first (offensive-leaning)', () => {
    expect(PATH_TIEBREAK['Fire'][0]).toBe('ATTACK');
  });

  it('Shadow path — SPECIAL first', () => {
    expect(PATH_TIEBREAK['Shadow'][0]).toBe('SPECIAL');
  });

  it('Earth path — DEFEND first among action paths', () => {
    expect(PATH_TIEBREAK['Earth'][0]).toBe('DEFEND');
  });

  it('Water path — DEFEND first (defensive-leaning)', () => {
    expect(PATH_TIEBREAK['Water'][0]).toBe('DEFEND');
  });

  it('Air path — EVADE first', () => {
    expect(PATH_TIEBREAK['Air'][0]).toBe('EVADE');
  });

  it('Light path — SPECIAL first', () => {
    expect(PATH_TIEBREAK['Light'][0]).toBe('SPECIAL');
  });

  it('all paths include all 5 action types', () => {
    const paths: ElementalPath[] = ['Fire', 'Shadow', 'Earth', 'Water', 'Air', 'Light'];
    for (const path of paths) {
      const order = PATH_TIEBREAK[path];
      expect(order).toHaveLength(5);
      expect(order).toContain('ATTACK');
      expect(order).toContain('DEFEND');
      expect(order).toContain('EVADE');
      expect(order).toContain('SPECIAL');
      expect(order).toContain('GROUP');
    }
  });
});

describe('resolveTie', () => {
  it('picks ATTACK over DEFEND for Fire path when tied', () => {
    const tied = [
      makeCandidate('DEFEND', 'enemy_1', 1.5),
      makeCandidate('ATTACK', 'enemy_1', 1.5),
    ];
    const map = new Map([['enemy_1', 0.5]]);
    const winner = resolveTie(tied, 'Fire', map);
    expect(winner.actionType).toBe('ATTACK');
  });

  it('picks SPECIAL over ATTACK for Shadow path when tied', () => {
    const tied = [
      makeCandidate('ATTACK', 'enemy_1', 2.0),
      makeCandidate('SPECIAL', 'enemy_1', 2.0),
    ];
    const map = new Map([['enemy_1', 0.4]]);
    const winner = resolveTie(tied, 'Shadow', map);
    expect(winner.actionType).toBe('SPECIAL');
  });

  it('picks EVADE over DEFEND for Air path when tied', () => {
    const tied = [
      makeCandidate('DEFEND', 'ally_1', 1.0),
      makeCandidate('EVADE', null, 1.0),
    ];
    const map = new Map([['ally_1', 0.3]]);
    const winner = resolveTie(tied, 'Air', map);
    expect(winner.actionType).toBe('EVADE');
  });

  it('prefers DEFEND over EVADE for Water path when tied', () => {
    const tied = [
      makeCandidate('EVADE', null, 1.5),
      makeCandidate('DEFEND', 'ally_2', 1.5),
    ];
    const map = new Map([['ally_2', 0.2]]);
    const winner = resolveTie(tied, 'Water', map);
    expect(winner.actionType).toBe('DEFEND');
  });

  it('same action type: picks target with lower stamina', () => {
    const tied = [
      makeCandidate('ATTACK', 'enemy_high', 2.0),
      makeCandidate('ATTACK', 'enemy_low', 2.0),
    ];
    const map = new Map([
      ['enemy_high', 0.7],
      ['enemy_low', 0.2],
    ]);
    const winner = resolveTie(tied, 'Fire', map);
    expect(winner.targetId).toBe('enemy_low');
  });

  it('single candidate: returns it unchanged', () => {
    const tied = [makeCandidate('ATTACK', 'enemy_1', 3.0)];
    const map = new Map([['enemy_1', 0.5]]);
    const winner = resolveTie(tied, 'Fire', map);
    expect(winner.actionType).toBe('ATTACK');
    expect(winner.targetId).toBe('enemy_1');
  });

  it('Light path prefers SPECIAL over DEFEND', () => {
    const tied = [
      makeCandidate('DEFEND', 'ally_1', 1.0),
      makeCandidate('SPECIAL', 'enemy_1', 1.0),
    ];
    const map = new Map([['ally_1', 0.4], ['enemy_1', 0.5]]);
    const winner = resolveTie(tied, 'Light', map);
    expect(winner.actionType).toBe('SPECIAL');
  });
});
