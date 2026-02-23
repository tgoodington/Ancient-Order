/**
 * counterChain.test.ts — Unit tests for the Counter Chain Resolution System
 *
 * Tests cover:
 *  - Counter chain of length 1 (one counter, target fails to Parry → chain ends)
 *  - Counter chain of length 2 (first counter parried, second counter fails → chain ends)
 *  - Counter chain of length 3 (two successful parries, third fails → chain ends)
 *  - Termination condition: Parry fails (default termination)
 *  - Termination condition: KO (stamina reaches 0 after damage)
 *  - Termination condition: target already KO'd at chain start
 *  - Actions array contains all exchanges
 *  - State is immutable (original state unchanged)
 *
 * Roll injection strategy:
 *   - To SUCCEED at Parry: roll must be <= SR * 20
 *     e.g., SR = 0.9 → threshold = 18 → use roll = 1 (guaranteed success)
 *   - To FAIL at Parry: roll must be > SR * 20
 *     e.g., SR = 0.1 → threshold = 2 → use roll = 19 (guaranteed failure)
 *
 * We use high SR (0.9) for combatants that should succeed at parrying,
 * and low SR (0.1) for combatants that should fail.
 */

import { describe, it, expect } from 'vitest';
import { resolveCounterChain } from './counterChain.js';
import type { CombatState, Combatant, ReactionSkills } from '../types/combat.js';

// ============================================================================
// Fixtures
// ============================================================================

/** Reaction skills giving a high success rate (guaranteed success with roll = 1) */
const HIGH_PARRY_SKILLS: ReactionSkills = {
  block: { SR: 0.9, SMR: 0.5, FMR: 0.2 },
  dodge: { SR: 0.9, FMR: 0.15 },
  parry: { SR: 0.9, FMR: 0.1 }, // threshold = 18 → roll <= 18 succeeds
};

/** Reaction skills giving a low success rate (guaranteed failure with roll = 19) */
const LOW_PARRY_SKILLS: ReactionSkills = {
  block: { SR: 0.1, SMR: 0.2, FMR: 0.05 },
  dodge: { SR: 0.1, FMR: 0.05 },
  parry: { SR: 0.1, FMR: 0.2 }, // threshold = 2 → roll > 2 fails; roll = 19 → failure
};

function makeCombatant(
  id: string,
  stamina: number,
  parrySkills: ReactionSkills,
  power: number = 50,
): Combatant {
  return {
    id,
    name: id,
    archetype: 'test',
    rank: 1.0,
    stamina,
    maxStamina: stamina,
    power,
    speed: 10,
    energy: 0,
    maxEnergy: 5,
    ascensionLevel: 0,
    activeBuffs: [],
    elementalPath: 'Fire',
    reactionSkills: parrySkills,
    isKO: stamina <= 0,
  };
}

function makeState(playerCombatant: Combatant, enemyCombatant: Combatant): CombatState {
  return {
    round: 1,
    phase: 'PER_ATTACK',
    playerParty: [playerCombatant],
    enemyParty: [enemyCombatant],
    actionQueue: [],
    roundHistory: [],
    status: 'active',
  };
}

// ============================================================================
// Roll sequence helpers
// ============================================================================

/**
 * Creates a rollFn that returns values from a predefined sequence.
 * Repeats the last value if the sequence is exhausted.
 */
function makeRollSequence(rolls: number[]): () => number {
  let index = 0;
  return () => {
    const roll = rolls[Math.min(index, rolls.length - 1)];
    index++;
    return roll;
  };
}

// ============================================================================
// Chain length 1 — Parry fails immediately
// ============================================================================

describe('resolveCounterChain — chain length 1', () => {
  it('terminates on first Parry failure and applies damage to the original attacker', () => {
    // parrier (player) has LOW parry skills → will fail when parried against
    // originalAttacker (enemy) has LOW parry skills → will fail at the counter parry
    const parrier = makeCombatant('player_1', 200, LOW_PARRY_SKILLS, 50);
    const originalAttacker = makeCombatant('enemy_1', 200, LOW_PARRY_SKILLS, 50);
    const state = makeState(parrier, originalAttacker);

    // roll = 19 → fails the Parry (LOW_PARRY_SKILLS SR = 0.1, threshold = 2, roll 19 > 2 → fail)
    const rollFn = makeRollSequence([19]);
    const result = resolveCounterChain(state, originalAttacker, parrier, rollFn);

    expect(result.chainLength).toBe(1);
    expect(result.actions).toHaveLength(1);
    expect(result.actions[0].attackerId).toBe('player_1');
    expect(result.actions[0].targetId).toBe('enemy_1');
    expect(result.actions[0].defenseType).toBe('parry');
    expect(result.actions[0].defenseOutcome.success).toBe(false);
    expect(result.actions[0].counterChain).toBe(true);

    // Damage should have been applied: enemy stamina should be less than 200
    const updatedEnemy = result.state.enemyParty.find((c) => c.id === 'enemy_1');
    expect(updatedEnemy!.stamina).toBeLessThan(200);
  });

  it('records the correct damage value in the action result', () => {
    const parrier = makeCombatant('player_1', 200, LOW_PARRY_SKILLS, 50);
    const originalAttacker = makeCombatant('enemy_1', 200, LOW_PARRY_SKILLS, 60);
    const state = makeState(parrier, originalAttacker);

    // Guaranteed failure roll
    const rollFn = makeRollSequence([19]);
    const result = resolveCounterChain(state, originalAttacker, parrier, rollFn);

    // calculateBaseDamage(50, 60) = 50 * (50/60) ≈ 41.67
    // Parry failure: damage * (1 - FMR) = 41.67 * (1 - 0.2) ≈ 33.33
    const expectedBase = 50 * (50 / 60);
    const expectedDamage = expectedBase * (1 - LOW_PARRY_SKILLS.parry.FMR);
    expect(result.actions[0].damage).toBeCloseTo(expectedDamage, 5);
  });
});

// ============================================================================
// Chain length 2 — One successful Parry, then failure
// ============================================================================

describe('resolveCounterChain — chain length 2', () => {
  it('resolves two exchanges: first parry succeeds (no damage), second parry fails (damage applied)', () => {
    // parrier (player) has HIGH parry skills → succeeds on first roll
    // originalAttacker (enemy) has LOW parry skills → fails on second roll
    const parrier = makeCombatant('player_1', 200, HIGH_PARRY_SKILLS, 50);
    const originalAttacker = makeCombatant('enemy_1', 200, LOW_PARRY_SKILLS, 50);
    const state = makeState(parrier, originalAttacker);

    // Exchange 1: player counter-attacks enemy, enemy tries to Parry
    //   enemy SR = 0.1 → threshold = 2 → roll = 19 → FAIL → damage applied, chain ends
    // Wait — that would only be 1 exchange. We need the enemy to succeed first, then fail.
    // Let's swap: originalAttacker (enemy) has HIGH parry skills and parrier (player) has LOW.
    //
    // Exchange 1: player (HIGH parry SR) counter-attacks enemy — enemy (HIGH SR = 0.9) tries Parry
    //   roll = 1 → 1 <= 18 → SUCCESS → no damage, roles swap
    // Exchange 2: enemy counter-attacks player — player (LOW SR = 0.1) tries Parry
    //   roll = 19 → 19 > 2 → FAIL → damage applied to player, chain ends

    const playerHighParry = makeCombatant('player_1', 200, HIGH_PARRY_SKILLS, 50);
    const enemyLowParry = makeCombatant('enemy_1', 200, LOW_PARRY_SKILLS, 50);
    const state2 = makeState(playerHighParry, enemyLowParry);

    // But wait: in exchange 1, player attacks enemy (playerHighParry attacks enemyLowParry).
    // enemy (LOW parry) tries to Parry: roll = 19 → fail → chain ends at length 1.
    // We need enemy to have HIGH parry for exchange 1, and player to have LOW for exchange 2.

    const playerLowParry = makeCombatant('player_2', 200, LOW_PARRY_SKILLS, 50);
    const enemyHighParry = makeCombatant('enemy_2', 200, HIGH_PARRY_SKILLS, 50);
    const stateChain2 = makeState(playerLowParry, enemyHighParry);

    // Exchange 1: playerLowParry (parrier) attacks enemyHighParry (original attacker)
    //   enemyHighParry SR = 0.9 → threshold = 18 → roll = 1 → SUCCESS → no damage, roles swap
    // Exchange 2: enemyHighParry (now attacker) attacks playerLowParry (now target)
    //   playerLowParry SR = 0.1 → threshold = 2 → roll = 19 → FAIL → damage applied
    const rollFn = makeRollSequence([1, 19]);
    const result = resolveCounterChain(stateChain2, enemyHighParry, playerLowParry, rollFn);

    expect(result.chainLength).toBe(2);
    expect(result.actions).toHaveLength(2);

    // Exchange 1: player attacks enemy, enemy parry succeeds
    expect(result.actions[0].attackerId).toBe('player_2');
    expect(result.actions[0].targetId).toBe('enemy_2');
    expect(result.actions[0].defenseOutcome.success).toBe(true);
    expect(result.actions[0].damage).toBe(0);

    // Exchange 2: enemy attacks player, player parry fails
    expect(result.actions[1].attackerId).toBe('enemy_2');
    expect(result.actions[1].targetId).toBe('player_2');
    expect(result.actions[1].defenseOutcome.success).toBe(false);
    expect(result.actions[1].damage).toBeGreaterThan(0);

    // Damage applied to player (playerLowParry)
    const updatedPlayer = result.state.playerParty.find((c) => c.id === 'player_2');
    expect(updatedPlayer!.stamina).toBeLessThan(200);

    // Enemy stamina untouched
    const updatedEnemy = result.state.enemyParty.find((c) => c.id === 'enemy_2');
    expect(updatedEnemy!.stamina).toBe(200);
  });
});

// ============================================================================
// Chain length 3 — Two successful Parries, then failure
// ============================================================================

describe('resolveCounterChain — chain length 3', () => {
  it('resolves three exchanges: two parries succeed, third fails and applies damage', () => {
    // Exchange 1: player attacks enemy → enemy parry succeeds (roll 1) → roles swap
    // Exchange 2: enemy attacks player → player parry succeeds (roll 1) → roles swap
    // Exchange 3: player attacks enemy → enemy parry fails (roll 19) → damage applied
    //
    // Both combatants need HIGH parry SR (0.9) so that rolls of 1 succeed,
    // but we need the third roll to fail. We use a roll sequence: [1, 1, 19].
    // The combatant receiving the third attack (enemy) will fail with roll 19.
    // enemy SR = 0.9 → threshold = 18 → roll = 19 > 18 → FAIL

    const playerHighParry = makeCombatant('player_3', 500, HIGH_PARRY_SKILLS, 50);
    const enemyHighParry = makeCombatant('enemy_3', 500, HIGH_PARRY_SKILLS, 50);
    const state = makeState(playerHighParry, enemyHighParry);

    // Rolls: exchange 1 enemy (SR=0.9) uses roll=1 → success
    //        exchange 2 player (SR=0.9) uses roll=1 → success
    //        exchange 3 enemy (SR=0.9) uses roll=19 → fail
    const rollFn = makeRollSequence([1, 1, 19]);
    const result = resolveCounterChain(state, enemyHighParry, playerHighParry, rollFn);

    expect(result.chainLength).toBe(3);
    expect(result.actions).toHaveLength(3);

    // Exchange 1: player → enemy, success (no damage)
    expect(result.actions[0].attackerId).toBe('player_3');
    expect(result.actions[0].targetId).toBe('enemy_3');
    expect(result.actions[0].defenseOutcome.success).toBe(true);
    expect(result.actions[0].damage).toBe(0);

    // Exchange 2: enemy → player, success (no damage)
    expect(result.actions[1].attackerId).toBe('enemy_3');
    expect(result.actions[1].targetId).toBe('player_3');
    expect(result.actions[1].defenseOutcome.success).toBe(true);
    expect(result.actions[1].damage).toBe(0);

    // Exchange 3: player → enemy, failure (damage applied)
    expect(result.actions[2].attackerId).toBe('player_3');
    expect(result.actions[2].targetId).toBe('enemy_3');
    expect(result.actions[2].defenseOutcome.success).toBe(false);
    expect(result.actions[2].damage).toBeGreaterThan(0);

    // Enemy stamina reduced by the damage from exchange 3
    const updatedEnemy = result.state.enemyParty.find((c) => c.id === 'enemy_3');
    expect(updatedEnemy!.stamina).toBeLessThan(500);

    // Player stamina untouched (only enemy took damage)
    const updatedPlayer = result.state.playerParty.find((c) => c.id === 'player_3');
    expect(updatedPlayer!.stamina).toBe(500);
  });
});

// ============================================================================
// Termination condition: Parry failure
// ============================================================================

describe('resolveCounterChain — termination: Parry failure', () => {
  it('terminates immediately when the first Parry attempt fails', () => {
    const parrier = makeCombatant('p', 100, LOW_PARRY_SKILLS, 50);
    const attacker = makeCombatant('e', 100, LOW_PARRY_SKILLS, 50);
    const state = makeState(parrier, attacker);

    // roll = 19 → guaranteed failure for LOW_PARRY_SKILLS (SR=0.1, threshold=2)
    const rollFn = makeRollSequence([19]);
    const result = resolveCounterChain(state, attacker, parrier, rollFn);

    expect(result.chainLength).toBe(1);
    expect(result.actions[0].defenseOutcome.success).toBe(false);
  });
});

// ============================================================================
// Termination condition: KO
// ============================================================================

describe('resolveCounterChain — termination: KO', () => {
  it('terminates when target is KO\'d after damage application', () => {
    // Give the enemy only 1 stamina so any damage KOs them.
    // Player (parrier) attacks enemy (originalAttacker) first.
    // enemy tries to Parry: fails → damage applied → enemy KO'd → chain ends.
    const parrier = makeCombatant('player_ko', 200, HIGH_PARRY_SKILLS, 100);
    const originalAttacker = makeCombatant('enemy_ko', 1, LOW_PARRY_SKILLS, 10); // 1 stamina
    const state = makeState(parrier, originalAttacker);

    // roll = 19 → guaranteed failure → damage applied → enemy KO'd
    const rollFn = makeRollSequence([19]);
    const result = resolveCounterChain(state, originalAttacker, parrier, rollFn);

    expect(result.chainLength).toBe(1);

    // Enemy should be KO'd
    const updatedEnemy = result.state.enemyParty.find((c) => c.id === 'enemy_ko');
    expect(updatedEnemy!.stamina).toBe(0);
    expect(updatedEnemy!.isKO).toBe(true);
  });

  it('terminates when target is already KO\'d at chain start', () => {
    // Target is already KO before the chain begins
    const parrier = makeCombatant('player_ko2', 200, HIGH_PARRY_SKILLS, 50);
    const originalAttackerAlreadyKO: Combatant = {
      ...makeCombatant('enemy_ko2', 0, LOW_PARRY_SKILLS, 50),
      isKO: true,
    };
    const state = makeState(parrier, originalAttackerAlreadyKO);

    const rollFn = makeRollSequence([1, 1, 1]); // should not be called
    const result = resolveCounterChain(state, originalAttackerAlreadyKO, parrier, rollFn);

    // Chain should not execute any exchanges
    expect(result.chainLength).toBe(0);
    expect(result.actions).toHaveLength(0);
  });
});

// ============================================================================
// Termination condition: Stamina depletion
// ============================================================================

describe('resolveCounterChain — termination: stamina depletion', () => {
  it('terminates when target stamina reaches 0 and KO is set', () => {
    // Setup: enemy has just enough stamina to be KO'd in one hit.
    // Player (parrier) is strong; enemy (originalAttacker) has minimal stamina.
    // calculateBaseDamage(100, 10) = 100 * (100/10) = 1000 >> 5 stamina
    const parrier = makeCombatant('player_sd', 500, LOW_PARRY_SKILLS, 100);
    const originalAttacker = makeCombatant('enemy_sd', 5, LOW_PARRY_SKILLS, 10); // 5 stamina
    const state = makeState(parrier, originalAttacker);

    // Parry failure → damage applied → enemy KO'd (5 stamina vs ~1000 damage)
    const rollFn = makeRollSequence([19]);
    const result = resolveCounterChain(state, originalAttacker, parrier, rollFn);

    expect(result.chainLength).toBe(1);

    const updatedEnemy = result.state.enemyParty.find((c) => c.id === 'enemy_sd');
    expect(updatedEnemy!.stamina).toBe(0);
    expect(updatedEnemy!.isKO).toBe(true);
  });
});

// ============================================================================
// State immutability
// ============================================================================

describe('resolveCounterChain — immutability', () => {
  it('does not mutate the original state', () => {
    const parrier = makeCombatant('player_imm', 200, LOW_PARRY_SKILLS, 50);
    const originalAttacker = makeCombatant('enemy_imm', 200, LOW_PARRY_SKILLS, 50);
    const state = makeState(parrier, originalAttacker);

    const originalEnemyStamina = state.enemyParty[0].stamina;

    const rollFn = makeRollSequence([19]); // guaranteed failure → damage applied
    resolveCounterChain(state, originalAttacker, parrier, rollFn);

    // Original state should be unchanged
    expect(state.enemyParty[0].stamina).toBe(originalEnemyStamina);
  });

  it('returns a new state object (not the same reference) when damage is applied', () => {
    const parrier = makeCombatant('player_ref', 200, LOW_PARRY_SKILLS, 50);
    const originalAttacker = makeCombatant('enemy_ref', 200, LOW_PARRY_SKILLS, 50);
    const state = makeState(parrier, originalAttacker);

    const rollFn = makeRollSequence([19]);
    const result = resolveCounterChain(state, originalAttacker, parrier, rollFn);

    expect(result.state).not.toBe(state);
  });

  it('returns the same state reference when chain length is 0', () => {
    // Already-KO'd target → no exchanges → same state returned
    const parrier = makeCombatant('player_same', 200, HIGH_PARRY_SKILLS, 50);
    const koAttacker: Combatant = {
      ...makeCombatant('enemy_same', 0, LOW_PARRY_SKILLS, 50),
      isKO: true,
    };
    const state = makeState(parrier, koAttacker);

    const rollFn = makeRollSequence([]);
    const result = resolveCounterChain(state, koAttacker, parrier, rollFn);

    expect(result.state).toBe(state);
    expect(result.chainLength).toBe(0);
  });
});

// ============================================================================
// All actions have counterChain = true
// ============================================================================

describe('resolveCounterChain — AttackResult fields', () => {
  it('all actions in the chain have counterChain = true', () => {
    const playerHighParry = makeCombatant('p_cc', 500, HIGH_PARRY_SKILLS, 50);
    const enemyHighParry = makeCombatant('e_cc', 500, HIGH_PARRY_SKILLS, 50);
    const state = makeState(playerHighParry, enemyHighParry);

    // Two successes, then one failure → 3 exchanges
    const rollFn = makeRollSequence([1, 1, 19]);
    const result = resolveCounterChain(state, enemyHighParry, playerHighParry, rollFn);

    for (const action of result.actions) {
      expect(action.counterChain).toBe(true);
    }
  });

  it('all actions have rankKO, blindside, crushingBlow = false (not applicable in counter chain)', () => {
    const parrier = makeCombatant('p_fields', 200, LOW_PARRY_SKILLS, 50);
    const attacker = makeCombatant('e_fields', 200, LOW_PARRY_SKILLS, 50);
    const state = makeState(parrier, attacker);

    const rollFn = makeRollSequence([19]);
    const result = resolveCounterChain(state, attacker, parrier, rollFn);

    for (const action of result.actions) {
      expect(action.rankKO).toBe(false);
      expect(action.blindside).toBe(false);
      expect(action.crushingBlow).toBe(false);
    }
  });
});
