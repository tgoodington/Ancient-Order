import { describe, it, expect } from 'vitest';
import {
  validateCombatActions,
  validateIdString,
  validatePersonalityAdjustment,
  validateTeamIds,
} from './validation.js';

function code(err: ReturnType<typeof validateIdString>): string | undefined {
  return err?.code;
}

describe('validatePersonalityAdjustment', () => {
  it('accepts one or more known traits with finite numbers', () => {
    expect(validatePersonalityAdjustment({ patience: 5 })).toBeNull();
    expect(validatePersonalityAdjustment({ patience: -5, charisma: 0.5 })).toBeNull();
  });

  it.each([null, undefined, 'x', 5, [], [1]])('rejects non-object %j', (input) => {
    expect(code(validatePersonalityAdjustment(input))).toBe('VALIDATION_ERROR');
  });

  it('rejects an empty object', () => {
    expect(validatePersonalityAdjustment({})?.message).toContain('fewer than 1');
  });

  it('rejects unknown traits, including prototype names', () => {
    expect(code(validatePersonalityAdjustment({ courage: 1 }))).toBe('VALIDATION_ERROR');
    expect(code(validatePersonalityAdjustment({ constructor: 1 }))).toBe('VALIDATION_ERROR');
    expect(code(validatePersonalityAdjustment({ patience: 1, extra: 1 }))).toBe('VALIDATION_ERROR');
  });

  it.each(['5', null, NaN, Infinity, {}])('rejects non-finite-number value %j', (value) => {
    expect(code(validatePersonalityAdjustment({ patience: value }))).toBe('VALIDATION_ERROR');
  });
});

describe('validateIdString', () => {
  it('accepts a string with no limit, or within the limit', () => {
    expect(validateIdString('id', 'x'.repeat(500), undefined)).toBeNull();
    expect(validateIdString('id', 'x'.repeat(128), 128)).toBeNull();
  });

  it('rejects non-strings and strings over the limit', () => {
    for (const bad of [undefined, null, 1, {}, []]) {
      expect(code(validateIdString('id', bad, undefined))).toBe('VALIDATION_ERROR');
    }
    expect(validateIdString('startingSceneId', 'x'.repeat(129), 128)?.message).toBe(
      'startingSceneId must NOT have more than 128 characters'
    );
  });
});

describe('validateCombatActions', () => {
  const ok = { combatantId: 'a', type: 'ATTACK' };

  it('accepts an empty array and well-formed actions', () => {
    expect(validateCombatActions([])).toBeNull();
    expect(validateCombatActions([ok])).toBeNull();
    expect(validateCombatActions([{ ...ok, targetId: null }, { ...ok, targetId: 'b' }])).toBeNull();
    expect(validateCombatActions([{ ...ok, energySegments: 2 }])).toBeNull();
  });

  it('rejects a non-array', () => {
    for (const bad of ['x', {}, null, undefined, 5]) {
      expect(code(validateCombatActions(bad))).toBe('VALIDATION_ERROR');
    }
  });

  it('allows 10 actions and rejects 11', () => {
    expect(validateCombatActions(Array.from({ length: 10 }, () => ok))).toBeNull();
    expect(validateCombatActions(Array.from({ length: 11 }, () => ok))?.message).toContain(
      'more than 10'
    );
  });

  it('rejects non-object items', () => {
    for (const bad of ['x', null, 5, [ok]]) {
      expect(code(validateCombatActions([bad]))).toBe('VALIDATION_ERROR');
    }
  });

  it('requires combatantId and type', () => {
    expect(validateCombatActions([{ type: 'ATTACK' }])?.message).toContain("'combatantId'");
    expect(validateCombatActions([{ combatantId: 'a' }])?.message).toContain("'type'");
  });

  it('rejects extra keys and wrongly typed fields', () => {
    expect(validateCombatActions([{ ...ok, extra: 1 }])?.message).toContain('additional properties');
    expect(code(validateCombatActions([{ ...ok, combatantId: 1 }]))).toBe('VALIDATION_ERROR');
    expect(code(validateCombatActions([{ ...ok, type: 1 }]))).toBe('VALIDATION_ERROR');
    expect(code(validateCombatActions([{ ...ok, targetId: 1 }]))).toBe('VALIDATION_ERROR');
    expect(code(validateCombatActions([{ ...ok, energySegments: '2' }]))).toBe('VALIDATION_ERROR');
    expect(code(validateCombatActions([{ ...ok, energySegments: NaN }]))).toBe('VALIDATION_ERROR');
  });

  it('reports the index of the offending item', () => {
    expect(validateCombatActions([ok, { type: 'ATTACK' }])?.message).toContain('actions/1');
  });
});

describe('validateTeamIds', () => {
  it('accepts exactly two strings of at most 64 characters', () => {
    expect(validateTeamIds(['a', 'b'])).toBeNull();
    expect(validateTeamIds(['a', 'x'.repeat(64)])).toBeNull();
  });

  it('rejects a non-array', () => {
    for (const bad of ['ab', {}, null, undefined]) {
      expect(code(validateTeamIds(bad))).toBe('VALIDATION_ERROR');
    }
  });

  it('rejects any count other than 2', () => {
    for (const bad of [[], ['a'], ['a', 'b', 'c']]) {
      expect(validateTeamIds(bad)?.message).toBe('npcIds must contain exactly 2 items');
    }
  });

  it('rejects non-string and over-long entries', () => {
    expect(validateTeamIds(['a', 5])?.message).toBe('npcIds/1 must be string');
    expect(validateTeamIds(['x'.repeat(65), 'b'])?.message).toContain('more than 64');
  });
});
