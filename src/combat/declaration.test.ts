/**
 * Ancient Order - Player Declaration Validation Tests
 *
 * Tests validateDeclaration() against all 6 acceptance criteria:
 *  1. Valid declarations for all 5 action types pass
 *  2. Invalid target (KO'd / non-existent) is rejected with descriptive error
 *  3. Insufficient stamina is rejected
 *  4. SPECIAL with no energy is rejected
 *  5. GROUP with incomplete energy is rejected, fallback is ATTACK on same target
 *  6. Pure function (same inputs always produce same result)
 */

import { describe, it, expect } from 'vitest';
import { validateDeclaration, maxEnergyForAscensionLevel } from './declaration.js';
import type { CombatState, Combatant, CombatAction } from '../types/combat.js';

// ============================================================================
// Test Fixture Builders
// ============================================================================

/**
 * Builds a minimal valid Combatant for testing.
 * Defaults: alive, healthy stamina, 2 energy of max 3.
 */
function makeCombatant(overrides: Partial<Combatant> = {}): Combatant {
  return {
    id: 'combatant_1',
    name: 'Test Fighter',
    archetype: 'scout',
    rank: 2.0,
    stamina: 100,
    maxStamina: 100,
    power: 10,
    speed: 10,
    energy: 2,
    maxEnergy: 3,
    ascensionLevel: 0,
    activeBuffs: [],
    elementalPath: 'Fire',
    reactionSkills: {
      block: { SR: 0.6, SMR: 0.3, FMR: 0.1 },
      dodge: { SR: 0.5, FMR: 0.1 },
      parry: { SR: 0.4, FMR: 0.1 },
    },
    isKO: false,
    ...overrides,
  };
}

/**
 * The standard three-player-party attacker (player_1) used in most tests.
 */
const ATTACKER = makeCombatant({ id: 'player_1', name: 'Hero' });

/**
 * A second player party member used in DEFEND tests and GROUP energy tests.
 */
const ALLY_2 = makeCombatant({ id: 'player_2', name: 'Ally Two' });

/**
 * A third player party member with full energy — for GROUP happy-path tests.
 */
const ALLY_3 = makeCombatant({ id: 'player_3', name: 'Ally Three', energy: 3, maxEnergy: 3 });

/**
 * The primary enemy target — alive and valid.
 */
const ENEMY_1 = makeCombatant({ id: 'enemy_1', name: 'Enemy One' });

/**
 * A second enemy used for multi-enemy scenarios.
 */
const ENEMY_2 = makeCombatant({ id: 'enemy_2', name: 'Enemy Two' });

/**
 * Builds a minimal valid CombatState for testing.
 * Default: 3 players vs 3 enemies, all alive.
 */
function makeState(overrides: Partial<CombatState> = {}): CombatState {
  return {
    round: 1,
    phase: 'PC_DECLARATION',
    playerParty: [ATTACKER, ALLY_2, ALLY_3],
    enemyParty: [ENEMY_1, ENEMY_2],
    actionQueue: [],
    roundHistory: [],
    status: 'active',
    ...overrides,
  };
}

// ============================================================================
// Acceptance Criterion 1: Valid declarations for all 5 action types pass
// ============================================================================

describe('validateDeclaration — valid declarations (AC1)', () => {
  const state = makeState();

  it('accepts a valid ATTACK action targeting a non-KO enemy', () => {
    const action: CombatAction = {
      combatantId: 'player_1',
      type: 'ATTACK',
      targetId: 'enemy_1',
    };
    const result = validateDeclaration(state, action);
    expect(result.valid).toBe(true);
  });

  it('accepts a valid DEFEND action targeting a non-KO ally', () => {
    const action: CombatAction = {
      combatantId: 'player_1',
      type: 'DEFEND',
      targetId: 'player_2',
    };
    const result = validateDeclaration(state, action);
    expect(result.valid).toBe(true);
  });

  it('accepts a valid EVADE action with null target', () => {
    const action: CombatAction = {
      combatantId: 'player_1',
      type: 'EVADE',
      targetId: null,
    };
    const result = validateDeclaration(state, action);
    expect(result.valid).toBe(true);
  });

  it('accepts a valid SPECIAL action targeting a non-KO enemy', () => {
    // player_1 has energy: 2, maxEnergy: 3 — energy > 0, so SPECIAL is valid
    const action: CombatAction = {
      combatantId: 'player_1',
      type: 'SPECIAL',
      targetId: 'enemy_1',
      energySegments: 2,
    };
    const result = validateDeclaration(state, action);
    expect(result.valid).toBe(true);
  });

  it('accepts a valid GROUP action when all allies have full energy', () => {
    // All three player_1/player_2/player_3 need full energy for GROUP.
    // player_1: energy 2, maxEnergy 3 — NOT full by default. Build a state where all are full.
    const fullEnergyState = makeState({
      playerParty: [
        makeCombatant({ id: 'player_1', energy: 3, maxEnergy: 3 }),
        makeCombatant({ id: 'player_2', energy: 3, maxEnergy: 3 }),
        makeCombatant({ id: 'player_3', energy: 3, maxEnergy: 3 }),
      ],
    });
    const action: CombatAction = {
      combatantId: 'player_1',
      type: 'GROUP',
      targetId: 'enemy_1',
    };
    const result = validateDeclaration(fullEnergyState, action);
    expect(result.valid).toBe(true);
  });
});

// ============================================================================
// Acceptance Criterion 2: Invalid targets are rejected with descriptive errors
// ============================================================================

describe('validateDeclaration — target validation (AC2)', () => {
  const state = makeState();

  it('rejects ATTACK targeting a KO\'d enemy', () => {
    const ko_enemy = makeCombatant({ id: 'enemy_1', isKO: true, stamina: 0 });
    const stateWithKO = makeState({ enemyParty: [ko_enemy, ENEMY_2] });
    const action: CombatAction = {
      combatantId: 'player_1',
      type: 'ATTACK',
      targetId: 'enemy_1',
    };
    const result = validateDeclaration(stateWithKO, action);
    expect(result.valid).toBe(false);
    if (!result.valid) {
      expect(result.error).toMatch(/KO/i);
    }
  });

  it('rejects ATTACK targeting a non-existent combatant ID', () => {
    const action: CombatAction = {
      combatantId: 'player_1',
      type: 'ATTACK',
      targetId: 'ghost_enemy',
    };
    const result = validateDeclaration(state, action);
    expect(result.valid).toBe(false);
    if (!result.valid) {
      expect(result.error).toBeTruthy();
    }
  });

  it('rejects ATTACK with null target', () => {
    const action: CombatAction = {
      combatantId: 'player_1',
      type: 'ATTACK',
      targetId: null,
    };
    const result = validateDeclaration(state, action);
    expect(result.valid).toBe(false);
    if (!result.valid) {
      expect(result.error).toBeTruthy();
    }
  });

  it('rejects ATTACK targeting an ally (must target enemy)', () => {
    const action: CombatAction = {
      combatantId: 'player_1',
      type: 'ATTACK',
      targetId: 'player_2', // player_2 is an ally, not an enemy
    };
    const result = validateDeclaration(state, action);
    expect(result.valid).toBe(false);
  });

  it('rejects SPECIAL targeting a KO\'d enemy', () => {
    const ko_enemy = makeCombatant({ id: 'enemy_1', isKO: true, stamina: 0 });
    const stateWithKO = makeState({ enemyParty: [ko_enemy, ENEMY_2] });
    const action: CombatAction = {
      combatantId: 'player_1',
      type: 'SPECIAL',
      targetId: 'enemy_1',
    };
    const result = validateDeclaration(stateWithKO, action);
    expect(result.valid).toBe(false);
    if (!result.valid) {
      expect(result.error).toMatch(/KO/i);
    }
  });

  it('rejects DEFEND targeting a KO\'d ally', () => {
    const ko_ally = makeCombatant({ id: 'player_2', isKO: true, stamina: 0 });
    const stateWithKO = makeState({
      playerParty: [ATTACKER, ko_ally, ALLY_3],
    });
    const action: CombatAction = {
      combatantId: 'player_1',
      type: 'DEFEND',
      targetId: 'player_2',
    };
    const result = validateDeclaration(stateWithKO, action);
    expect(result.valid).toBe(false);
    if (!result.valid) {
      expect(result.error).toMatch(/KO/i);
    }
  });

  it('rejects DEFEND targeting a non-existent ally', () => {
    const action: CombatAction = {
      combatantId: 'player_1',
      type: 'DEFEND',
      targetId: 'ghost_ally',
    };
    const result = validateDeclaration(state, action);
    expect(result.valid).toBe(false);
  });

  it('rejects DEFEND targeting an enemy (must target ally)', () => {
    const action: CombatAction = {
      combatantId: 'player_1',
      type: 'DEFEND',
      targetId: 'enemy_1', // enemy is not in own party
    };
    const result = validateDeclaration(state, action);
    expect(result.valid).toBe(false);
  });

  it('rejects EVADE when a non-null target is supplied', () => {
    const action: CombatAction = {
      combatantId: 'player_1',
      type: 'EVADE',
      targetId: 'enemy_1',
    };
    const result = validateDeclaration(state, action);
    expect(result.valid).toBe(false);
    if (!result.valid) {
      expect(result.error).toBeTruthy();
    }
  });

  it('rejects GROUP targeting a KO\'d enemy', () => {
    const ko_enemy = makeCombatant({ id: 'enemy_1', isKO: true, stamina: 0 });
    const fullEnergyState = makeState({
      playerParty: [
        makeCombatant({ id: 'player_1', energy: 3, maxEnergy: 3 }),
        makeCombatant({ id: 'player_2', energy: 3, maxEnergy: 3 }),
        makeCombatant({ id: 'player_3', energy: 3, maxEnergy: 3 }),
      ],
      enemyParty: [ko_enemy, ENEMY_2],
    });
    const action: CombatAction = {
      combatantId: 'player_1',
      type: 'GROUP',
      targetId: 'enemy_1',
    };
    const result = validateDeclaration(fullEnergyState, action);
    expect(result.valid).toBe(false);
    if (!result.valid) {
      expect(result.error).toMatch(/KO/i);
    }
  });
});

// ============================================================================
// Acceptance Criterion 3: Insufficient stamina is rejected
// ============================================================================

describe('validateDeclaration — stamina check (AC3)', () => {
  it('rejects any action when combatant has zero stamina (but isKO might be false — belt-and-suspenders)', () => {
    // Combatant has stamina 0 but isKO = false (edge case where flags diverge)
    const lowStaminaCombatant = makeCombatant({ id: 'player_1', stamina: 0, isKO: false });
    const state = makeState({
      playerParty: [lowStaminaCombatant, ALLY_2, ALLY_3],
    });
    const action: CombatAction = {
      combatantId: 'player_1',
      type: 'ATTACK',
      targetId: 'enemy_1',
    };
    const result = validateDeclaration(state, action);
    expect(result.valid).toBe(false);
    if (!result.valid) {
      expect(result.error).toMatch(/stamina/i);
    }
  });

  it('passes when combatant has positive stamina', () => {
    const state = makeState();
    const action: CombatAction = {
      combatantId: 'player_1',
      type: 'ATTACK',
      targetId: 'enemy_1',
    };
    const result = validateDeclaration(state, action);
    expect(result.valid).toBe(true);
  });
});

// ============================================================================
// Acceptance Criterion 4: SPECIAL with no energy is rejected
// ============================================================================

describe('validateDeclaration — SPECIAL energy check (AC4)', () => {
  it('rejects SPECIAL when combatant energy is 0', () => {
    const noEnergyCombatant = makeCombatant({ id: 'player_1', energy: 0 });
    const state = makeState({
      playerParty: [noEnergyCombatant, ALLY_2, ALLY_3],
    });
    const action: CombatAction = {
      combatantId: 'player_1',
      type: 'SPECIAL',
      targetId: 'enemy_1',
    };
    const result = validateDeclaration(state, action);
    expect(result.valid).toBe(false);
    if (!result.valid) {
      expect(result.error).toMatch(/energy/i);
    }
  });

  it('accepts SPECIAL when combatant energy is greater than 0', () => {
    // player_1 default has energy: 2
    const state = makeState();
    const action: CombatAction = {
      combatantId: 'player_1',
      type: 'SPECIAL',
      targetId: 'enemy_1',
    };
    const result = validateDeclaration(state, action);
    expect(result.valid).toBe(true);
  });

  it('accepts SPECIAL at exactly 1 energy segment', () => {
    const oneEnergyCombatant = makeCombatant({ id: 'player_1', energy: 1 });
    const state = makeState({
      playerParty: [oneEnergyCombatant, ALLY_2, ALLY_3],
    });
    const action: CombatAction = {
      combatantId: 'player_1',
      type: 'SPECIAL',
      targetId: 'enemy_1',
    };
    const result = validateDeclaration(state, action);
    expect(result.valid).toBe(true);
  });
});

// ============================================================================
// Acceptance Criterion 5: GROUP with incomplete energy is rejected with fallback
// ============================================================================

describe('validateDeclaration — GROUP energy gate (AC5)', () => {
  it('rejects GROUP when the leader lacks full energy, provides ATTACK fallback', () => {
    // Leader has energy 1 of maxEnergy 3
    const partialLeader = makeCombatant({ id: 'player_1', energy: 1, maxEnergy: 3 });
    const fullAlly2 = makeCombatant({ id: 'player_2', energy: 3, maxEnergy: 3 });
    const fullAlly3 = makeCombatant({ id: 'player_3', energy: 3, maxEnergy: 3 });
    const state = makeState({
      playerParty: [partialLeader, fullAlly2, fullAlly3],
    });
    const action: CombatAction = {
      combatantId: 'player_1',
      type: 'GROUP',
      targetId: 'enemy_1',
    };
    const result = validateDeclaration(state, action);
    expect(result.valid).toBe(false);
    if (!result.valid) {
      expect(result.error).toMatch(/energy/i);
      expect(result.fallback).toBeDefined();
      expect(result.fallback?.type).toBe('ATTACK');
      expect(result.fallback?.combatantId).toBe('player_1');
      expect(result.fallback?.targetId).toBe('enemy_1');
    }
  });

  it('rejects GROUP when a non-leader ally lacks full energy, provides ATTACK fallback', () => {
    const fullLeader = makeCombatant({ id: 'player_1', energy: 3, maxEnergy: 3 });
    const partialAlly = makeCombatant({ id: 'player_2', energy: 2, maxEnergy: 3 }); // not full
    const fullAlly3 = makeCombatant({ id: 'player_3', energy: 3, maxEnergy: 3 });
    const state = makeState({
      playerParty: [fullLeader, partialAlly, fullAlly3],
    });
    const action: CombatAction = {
      combatantId: 'player_1',
      type: 'GROUP',
      targetId: 'enemy_1',
    };
    const result = validateDeclaration(state, action);
    expect(result.valid).toBe(false);
    if (!result.valid) {
      expect(result.error).toMatch(/energy/i);
      expect(result.fallback).toBeDefined();
      expect(result.fallback?.type).toBe('ATTACK');
      expect(result.fallback?.targetId).toBe('enemy_1');
    }
  });

  it('accepts GROUP when all non-KO allies have full energy', () => {
    const fullEnergyState = makeState({
      playerParty: [
        makeCombatant({ id: 'player_1', energy: 3, maxEnergy: 3 }),
        makeCombatant({ id: 'player_2', energy: 3, maxEnergy: 3 }),
        makeCombatant({ id: 'player_3', energy: 3, maxEnergy: 3 }),
      ],
    });
    const action: CombatAction = {
      combatantId: 'player_1',
      type: 'GROUP',
      targetId: 'enemy_1',
    };
    const result = validateDeclaration(fullEnergyState, action);
    expect(result.valid).toBe(true);
  });

  it('accepts GROUP even when one ally is KO\'d, as long as non-KO\'d allies all have full energy', () => {
    // Design spec: KO'd allies are excluded from GROUP participant count.
    // GROUP should still succeed if all non-KO'd allies have full energy.
    const fullLeader = makeCombatant({ id: 'player_1', energy: 3, maxEnergy: 3 });
    const koAlly = makeCombatant({ id: 'player_2', isKO: true, stamina: 0, energy: 0, maxEnergy: 3 });
    const fullAlly3 = makeCombatant({ id: 'player_3', energy: 3, maxEnergy: 3 });
    const state = makeState({
      playerParty: [fullLeader, koAlly, fullAlly3],
    });
    const action: CombatAction = {
      combatantId: 'player_1',
      type: 'GROUP',
      targetId: 'enemy_1',
    };
    const result = validateDeclaration(state, action);
    expect(result.valid).toBe(true);
  });

  it('rejects GROUP when energy is 0 (empty, not just partial)', () => {
    const emptyLeader = makeCombatant({ id: 'player_1', energy: 0, maxEnergy: 3 });
    const fullAlly2 = makeCombatant({ id: 'player_2', energy: 3, maxEnergy: 3 });
    const fullAlly3 = makeCombatant({ id: 'player_3', energy: 3, maxEnergy: 3 });
    const state = makeState({
      playerParty: [emptyLeader, fullAlly2, fullAlly3],
    });
    const action: CombatAction = {
      combatantId: 'player_1',
      type: 'GROUP',
      targetId: 'enemy_1',
    };
    const result = validateDeclaration(state, action);
    expect(result.valid).toBe(false);
    if (!result.valid) {
      expect(result.fallback?.type).toBe('ATTACK');
    }
  });
});

// ============================================================================
// Acceptance Criterion 6: Pure function — same inputs always produce same result
// ============================================================================

describe('validateDeclaration — pure function (AC6)', () => {
  it('returns identical result when called twice with identical inputs', () => {
    const state = makeState();
    const action: CombatAction = {
      combatantId: 'player_1',
      type: 'ATTACK',
      targetId: 'enemy_1',
    };
    const result1 = validateDeclaration(state, action);
    const result2 = validateDeclaration(state, action);
    expect(result1).toEqual(result2);
  });

  it('does not mutate the CombatState on a valid declaration', () => {
    const state = makeState();
    const stateBefore = JSON.stringify(state);
    const action: CombatAction = {
      combatantId: 'player_1',
      type: 'ATTACK',
      targetId: 'enemy_1',
    };
    validateDeclaration(state, action);
    expect(JSON.stringify(state)).toBe(stateBefore);
  });

  it('does not mutate the CombatState on an invalid declaration', () => {
    const ko_enemy = makeCombatant({ id: 'enemy_1', isKO: true, stamina: 0 });
    const state = makeState({ enemyParty: [ko_enemy, ENEMY_2] });
    const stateBefore = JSON.stringify(state);
    const action: CombatAction = {
      combatantId: 'player_1',
      type: 'ATTACK',
      targetId: 'enemy_1',
    };
    validateDeclaration(state, action);
    expect(JSON.stringify(state)).toBe(stateBefore);
  });

  it('returns identical failure result when called twice with an invalid declaration', () => {
    const state = makeState();
    const action: CombatAction = {
      combatantId: 'nonexistent_id',
      type: 'ATTACK',
      targetId: 'enemy_1',
    };
    const result1 = validateDeclaration(state, action);
    const result2 = validateDeclaration(state, action);
    expect(result1).toEqual(result2);
    expect(result1.valid).toBe(false);
  });
});

// ============================================================================
// Check 1: Combatant existence and KO status
// ============================================================================

describe('validateDeclaration — combatant existence and KO (Check 1)', () => {
  it('rejects when combatant ID does not exist in either party', () => {
    const state = makeState();
    const action: CombatAction = {
      combatantId: 'nonexistent',
      type: 'ATTACK',
      targetId: 'enemy_1',
    };
    const result = validateDeclaration(state, action);
    expect(result.valid).toBe(false);
    if (!result.valid) {
      expect(result.error).toMatch(/not found/i);
    }
  });

  it('rejects when combatant is KO\'d', () => {
    const ko_player = makeCombatant({ id: 'player_1', isKO: true, stamina: 0 });
    const state = makeState({
      playerParty: [ko_player, ALLY_2, ALLY_3],
    });
    const action: CombatAction = {
      combatantId: 'player_1',
      type: 'ATTACK',
      targetId: 'enemy_1',
    };
    const result = validateDeclaration(state, action);
    expect(result.valid).toBe(false);
    if (!result.valid) {
      expect(result.error).toMatch(/KO/i);
    }
  });

  it('allows actions from enemy party combatants (AI declarations are also validated)', () => {
    const state = makeState();
    const action: CombatAction = {
      combatantId: 'enemy_1',
      type: 'ATTACK',
      targetId: 'player_1',
    };
    const result = validateDeclaration(state, action);
    // enemy_1 is in enemyParty, player_1 is an opposing target — should be valid
    expect(result.valid).toBe(true);
  });
});

// ============================================================================
// maxEnergyForAscensionLevel utility
// ============================================================================

describe('maxEnergyForAscensionLevel', () => {
  it('returns the combatant maxEnergy field directly', () => {
    const combatant = makeCombatant({ maxEnergy: 5 });
    expect(maxEnergyForAscensionLevel(combatant)).toBe(5);
  });

  it('returns 3 for a combatant with maxEnergy: 3', () => {
    const combatant = makeCombatant({ maxEnergy: 3 });
    expect(maxEnergyForAscensionLevel(combatant)).toBe(3);
  });
});
