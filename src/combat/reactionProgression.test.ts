/**
 * Ancient Order - Reaction Progression Tests (ADR-054 Layer B)
 *
 * Covers the decoded XP curve, rank derivation, rank→rate ramps, full
 * ReactionSkills derivation, and mid-combat XP awards (success 4 / fail 2,
 * cap at 1675, rederive only the leveled reaction, enemy no-op).
 */

import { describe, it, expect } from 'vitest';
import type { Combatant } from '../types/combat.js';
import {
  XP_THRESHOLDS,
  MAX_REACTION_RANK,
  MAX_REACTION_XP,
  ZERO_REACTION_PROGRESS,
  rankFromXp,
  ratesForRank,
  reactionSkillsFromProgress,
  awardReactionXp,
} from './reactionProgression.js';

// ============================================================================
// Decoded constants
// ============================================================================

describe('XP curve constants', () => {
  it('matches the decoded Reaction Progression & Log thresholds', () => {
    expect(XP_THRESHOLDS).toEqual([
      0, 100, 215, 345, 490, 650, 825, 1015, 1220, 1440, 1675,
    ]);
    expect(MAX_REACTION_RANK).toBe(11);
    expect(MAX_REACTION_XP).toBe(1675);
  });

  it('has a step that grows by +15 each rank', () => {
    const steps = XP_THRESHOLDS.slice(1).map((v, i) => v - XP_THRESHOLDS[i]);
    expect(steps).toEqual([100, 115, 130, 145, 160, 175, 190, 205, 220, 235]);
  });
});

// ============================================================================
// rankFromXp
// ============================================================================

describe('rankFromXp', () => {
  it('returns rank 1 at and below the first threshold', () => {
    expect(rankFromXp(0)).toBe(1);
    expect(rankFromXp(99)).toBe(1);
  });

  it('advances exactly at each cumulative threshold', () => {
    expect(rankFromXp(100)).toBe(2);
    expect(rankFromXp(214)).toBe(2);
    expect(rankFromXp(215)).toBe(3);
    expect(rankFromXp(1674)).toBe(10);
    expect(rankFromXp(1675)).toBe(11);
  });

  it('caps at rank 11 for XP beyond the table', () => {
    expect(rankFromXp(99_999)).toBe(11);
  });
});

// ============================================================================
// ratesForRank
// ============================================================================

describe('ratesForRank', () => {
  it('returns the band floors at rank 1', () => {
    expect(ratesForRank('block', 1)).toEqual({ SR: 0.4, SMR: 0.55, FMR: 0.4 });
    expect(ratesForRank('dodge', 1)).toEqual({ SR: 0.3, SMR: 0.8, FMR: 0.1 });
    expect(ratesForRank('parry', 1)).toEqual({ SR: 0.1, SMR: 0.9, FMR: 0.0 });
  });

  it('reaches the band ceilings at rank 11 (matches ADR-054 SMR column)', () => {
    expect(ratesForRank('block', 11).SMR).toBeCloseTo(0.8, 5);
    expect(ratesForRank('dodge', 11).SMR).toBeCloseTo(0.9, 5);
    expect(ratesForRank('parry', 11).SMR).toBeCloseTo(1.0, 5);
    // Spot-check the SR ramps at the ceiling too.
    expect(ratesForRank('block', 11).SR).toBeCloseTo(0.9, 5);
    expect(ratesForRank('dodge', 11).SR).toBeCloseTo(0.55, 5);
    expect(ratesForRank('parry', 11).SR).toBeCloseTo(0.35, 5);
  });

  it('interpolates a mid rank by base + step·(rank−1)', () => {
    // Block rank 3: SR 0.4+0.10=0.50, SMR 0.55+0.05=0.60, FMR 0.40+0.05=0.45
    const r = ratesForRank('block', 3);
    expect(r.SR).toBeCloseTo(0.5, 5);
    expect(r.SMR).toBeCloseTo(0.6, 5);
    expect(r.FMR).toBeCloseTo(0.45, 5);
  });

  it('clamps out-of-range ranks to [1, 11]', () => {
    expect(ratesForRank('parry', 0)).toEqual(ratesForRank('parry', 1));
    expect(ratesForRank('parry', 99)).toEqual(ratesForRank('parry', 11));
  });
});

// ============================================================================
// reactionSkillsFromProgress
// ============================================================================

describe('reactionSkillsFromProgress', () => {
  it('derives each reaction independently from its own XP', () => {
    const skills = reactionSkillsFromProgress({ block: 0, dodge: 100, parry: 1675 });
    expect(skills.block).toEqual(ratesForRank('block', 1));
    expect(skills.dodge).toEqual(ratesForRank('dodge', 2));
    expect(skills.parry).toEqual(ratesForRank('parry', 11));
  });

  it('maps ZERO_REACTION_PROGRESS to all rank-1 rates', () => {
    expect(reactionSkillsFromProgress(ZERO_REACTION_PROGRESS)).toEqual({
      block: ratesForRank('block', 1),
      dodge: ratesForRank('dodge', 1),
      parry: ratesForRank('parry', 1),
    });
  });
});

// ============================================================================
// awardReactionXp
// ============================================================================

function makeProgressingCombatant(overrides: Partial<Combatant> = {}): Combatant {
  const progress = { block: 0, dodge: 0, parry: 0 };
  return {
    id: 'player_1',
    name: 'Hero',
    archetype: 'scout',
    rank: 2,
    stamina: 100,
    maxStamina: 100,
    power: 10,
    speed: 10,
    energy: 0,
    maxEnergy: 3,
    ascensionLevel: 0,
    activeBuffs: [],
    elementalPath: 'Fire',
    reactionProgress: progress,
    reactionSkills: reactionSkillsFromProgress(progress),
    isKO: false,
    ...overrides,
  };
}

describe('awardReactionXp', () => {
  it('grants +4 on a successful reaction', () => {
    const after = awardReactionXp(makeProgressingCombatant(), 'dodge', true);
    expect(after.reactionProgress).toEqual({ block: 0, dodge: 4, parry: 0 });
  });

  it('grants +2 on a failed reaction', () => {
    const after = awardReactionXp(makeProgressingCombatant(), 'dodge', false);
    expect(after.reactionProgress).toEqual({ block: 0, dodge: 2, parry: 0 });
  });

  it('rederives only the leveled reaction on a rank-up', () => {
    // block at 96 XP is rank 1; +4 → 100 XP → rank 2.
    const start = makeProgressingCombatant({
      reactionProgress: { block: 96, dodge: 0, parry: 0 },
      reactionSkills: reactionSkillsFromProgress({ block: 96, dodge: 0, parry: 0 }),
    });
    const after = awardReactionXp(start, 'block', true);
    expect(after.reactionProgress!.block).toBe(100);
    expect(after.reactionSkills.block).toEqual(ratesForRank('block', 2));
    // Untouched reactions keep their rank-1 rates.
    expect(after.reactionSkills.dodge).toEqual(ratesForRank('dodge', 1));
    expect(after.reactionSkills.parry).toEqual(ratesForRank('parry', 1));
  });

  it('does not rederive rates when no rank threshold is crossed', () => {
    const start = makeProgressingCombatant();
    const after = awardReactionXp(start, 'parry', true);
    expect(after.reactionSkills).toEqual(start.reactionSkills);
  });

  it('caps XP at the rank-11 threshold', () => {
    const start = makeProgressingCombatant({
      reactionProgress: { block: 0, dodge: 0, parry: 1673 },
      reactionSkills: reactionSkillsFromProgress({ block: 0, dodge: 0, parry: 1673 }),
    });
    const after = awardReactionXp(start, 'parry', true);
    expect(after.reactionProgress!.parry).toBe(MAX_REACTION_XP);
    expect(after.reactionSkills.parry).toEqual(ratesForRank('parry', 11));
  });

  it('returns the combatant unchanged once a reaction is capped', () => {
    const start = makeProgressingCombatant({
      reactionProgress: { block: 0, dodge: 0, parry: MAX_REACTION_XP },
      reactionSkills: reactionSkillsFromProgress({ block: 0, dodge: 0, parry: MAX_REACTION_XP }),
    });
    expect(awardReactionXp(start, 'parry', true)).toBe(start);
  });

  it('is a no-op for combatants without reactionProgress (enemies/NPCs)', () => {
    const enemy = makeProgressingCombatant({ id: 'enemy_1', reactionProgress: undefined });
    expect(awardReactionXp(enemy, 'block', true)).toBe(enemy);
  });
});
