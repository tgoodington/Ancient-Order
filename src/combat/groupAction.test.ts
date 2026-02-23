/**
 * groupAction.test.ts — Tests for GROUP action resolution.
 *
 * Coverage (from design_spec_group_action_type.md verification considerations):
 *   1. GROUP declaration rejected when any ally lacks full energy
 *   2. GROUP fires with reduced participants when an ally is KO'd (multiplier unchanged)
 *   3. Total damage = (sum of individual damages) × 1.5
 *   4. Block defense is applied to GROUP damage
 *   5. All participants' energy is 0 after GROUP execution
 *   6. Priority sort places GROUP at index 0 before DEFEND, ATTACK, EVADE
 *   7. Solo GROUP (leader only, all allies KO'd) is valid
 *   8. Target already KO'd — GROUP no-ops on damage, energy still consumed
 */

import { describe, it, expect } from 'vitest';
import { resolveGroup, GROUP_ACTION_CONFIG } from './groupAction.js';
import { sortByPriority } from './pipeline.js';
import { validateDeclaration } from './declaration.js';
import type {
  CombatState,
  Combatant,
  CombatAction,
  GroupActionDeclaration,
  ReactionSkills,
} from '../types/combat.js';
import { calculateBaseDamage } from './formulas.js';

// ============================================================================
// Test Fixtures
// ============================================================================

/**
 * Base reaction skills for test combatants.
 * Block SR = 0.5 (50% success), SMR = 0.4 (40% mitigation), FMR = 0.1 (10% mitigation).
 */
const BASE_REACTION_SKILLS: ReactionSkills = {
  block: { SR: 0.5, SMR: 0.4, FMR: 0.1 },
  dodge: { SR: 0.5, FMR: 0.1 },
  parry: { SR: 0.5, FMR: 0.1 },
};

/**
 * Creates a minimal non-KO'd combatant with full energy for GROUP eligibility.
 */
function makeCombatant(
  id: string,
  overrides: Partial<Combatant> = {},
): Combatant {
  return {
    id,
    name: id,
    archetype: 'warrior',
    rank: 2.0,
    stamina: 100,
    maxStamina: 100,
    power: 50,
    speed: 30,
    energy: 5,
    maxEnergy: 5,
    ascensionLevel: 0,
    activeBuffs: [],
    elementalPath: 'Fire',
    reactionSkills: BASE_REACTION_SKILLS,
    isKO: false,
    ...overrides,
  };
}

/**
 * Builds a minimal CombatState for testing.
 */
function makeState(
  playerParty: Combatant[],
  enemyParty: Combatant[],
): CombatState {
  return {
    round: 1,
    phase: 'ACTION_RESOLUTION',
    playerParty,
    enemyParty,
    actionQueue: [],
    roundHistory: [],
    status: 'active',
  };
}

// Standard 3-member player party: leader + 2 allies, all at full energy
const LEADER = makeCombatant('leader', { power: 60 });
const ALLY_1 = makeCombatant('ally1', { power: 50 });
const ALLY_2 = makeCombatant('ally2', { power: 40 });

// Enemy target
const ENEMY = makeCombatant('enemy1', {
  power: 55,
  stamina: 300,
  maxStamina: 300,
  reactionSkills: BASE_REACTION_SKILLS,
});

const BASE_STATE = makeState([LEADER, ALLY_1, ALLY_2], [ENEMY]);

const GROUP_DECLARATION: GroupActionDeclaration = {
  leaderId: 'leader',
  targetId: 'enemy1',
};

// ============================================================================
// 1. Declaration Validation: GROUP rejected when any ally lacks full energy
// ============================================================================

describe('GROUP declaration validation', () => {
  it('accepts GROUP when all non-KO\'d members have full energy', () => {
    const action: CombatAction = {
      combatantId: 'leader',
      type: 'GROUP',
      targetId: 'enemy1',
    };
    const result = validateDeclaration(BASE_STATE, action);
    expect(result.valid).toBe(true);
  });

  it('rejects GROUP when the leader lacks full energy', () => {
    const leaderLowEnergy = { ...LEADER, energy: 3 };
    const state = makeState([leaderLowEnergy, ALLY_1, ALLY_2], [ENEMY]);
    const action: CombatAction = {
      combatantId: 'leader',
      type: 'GROUP',
      targetId: 'enemy1',
    };
    const result = validateDeclaration(state, action);
    expect(result.valid).toBe(false);
    if (!result.valid) {
      expect(result.error).toContain('full energy');
      // Fallback should be ATTACK on same target
      expect(result.fallback).toEqual({
        combatantId: 'leader',
        type: 'ATTACK',
        targetId: 'enemy1',
      });
    }
  });

  it('rejects GROUP when ally1 lacks full energy', () => {
    const ally1LowEnergy = { ...ALLY_1, energy: 2 };
    const state = makeState([LEADER, ally1LowEnergy, ALLY_2], [ENEMY]);
    const action: CombatAction = {
      combatantId: 'leader',
      type: 'GROUP',
      targetId: 'enemy1',
    };
    const result = validateDeclaration(state, action);
    expect(result.valid).toBe(false);
    if (!result.valid) {
      expect(result.error).toContain('ally1');
      expect(result.fallback?.type).toBe('ATTACK');
    }
  });

  it('rejects GROUP when ally2 lacks full energy', () => {
    const ally2LowEnergy = { ...ALLY_2, energy: 0 };
    const state = makeState([LEADER, ALLY_1, ally2LowEnergy], [ENEMY]);
    const action: CombatAction = {
      combatantId: 'leader',
      type: 'GROUP',
      targetId: 'enemy1',
    };
    const result = validateDeclaration(state, action);
    expect(result.valid).toBe(false);
    if (!result.valid) {
      expect(result.fallback?.type).toBe('ATTACK');
    }
  });

  it('accepts GROUP when KO\'d ally has no energy (KO\'d are excluded from energy check)', () => {
    const koDAlly = { ...ALLY_2, isKO: true, energy: 0 };
    const state = makeState([LEADER, ALLY_1, koDAlly], [ENEMY]);
    const action: CombatAction = {
      combatantId: 'leader',
      type: 'GROUP',
      targetId: 'enemy1',
    };
    const result = validateDeclaration(state, action);
    expect(result.valid).toBe(true);
  });
});

// ============================================================================
// 2. Reduced participants when ally is KO'd (multiplier unchanged)
// ============================================================================

describe('GROUP with KO\'d ally — reduced participant count', () => {
  it('fires with 2 participants when 1 ally is KO\'d', () => {
    const koDAlly2 = { ...ALLY_2, isKO: true, stamina: 0 };
    const state = makeState([LEADER, ALLY_1, koDAlly2], [ENEMY]);

    // Deterministic roll: always 0 (block fails — roll 0 <= SR * 20 = 10: actually succeeds)
    // Use a roll that guarantees block failure: roll > SR * 20 = 0.5 * 20 = 10
    const failRoll = () => 15; // 15 > 10 → block fails
    const newState = resolveGroup(state, GROUP_DECLARATION, GROUP_ACTION_CONFIG, failRoll);

    // KO'd ally (ally2) should NOT have energy consumed (already KO'd, not a participant)
    const ally2After = newState.enemyParty.find(() => false); // ally2 is in playerParty
    const ally2InPlayer = newState.playerParty.find((c) => c.id === 'ally2');
    // ally2 was already KO'd, isKO should still be true
    expect(ally2InPlayer?.isKO).toBe(true);

    // Leader and ally1 should have energy zeroed
    const leaderAfter = newState.playerParty.find((c) => c.id === 'leader');
    const ally1After = newState.playerParty.find((c) => c.id === 'ally1');
    expect(leaderAfter?.energy).toBe(0);
    expect(ally1After?.energy).toBe(0);
  });

  it('damage calculation uses only non-KO\'d participants', () => {
    const koDAlly2 = { ...ALLY_2, isKO: true, stamina: 0 };
    const state = makeState([LEADER, ALLY_1, koDAlly2], [ENEMY]);

    // Compute expected damage manually:
    // Participants: leader (power=60), ally1 (power=50) — ally2 is KO'd
    const leaderDmg = calculateBaseDamage(LEADER.power, ENEMY.power);  // 60 * (60/55)
    const ally1Dmg = calculateBaseDamage(ALLY_1.power, ENEMY.power);   // 50 * (50/55)
    const totalBeforeMultiplier = leaderDmg + ally1Dmg;
    const groupDamage = totalBeforeMultiplier * 1.5;

    // Block failure roll to get full damage through: roll > SR * 20 = 10
    const failRoll = () => 15;
    // Block failure damage: groupDamage * (1 - FMR) = groupDamage * (1 - 0.1) = groupDamage * 0.9
    const expectedFinalDamage = groupDamage * (1 - ENEMY.reactionSkills.block.FMR);

    const newState = resolveGroup(state, GROUP_DECLARATION, GROUP_ACTION_CONFIG, failRoll);
    const enemyAfter = newState.enemyParty.find((c) => c.id === 'enemy1');
    const damageDealt = ENEMY.stamina - (enemyAfter?.stamina ?? ENEMY.stamina);

    expect(damageDealt).toBeCloseTo(expectedFinalDamage, 5);
  });
});

// ============================================================================
// 3. Total damage = (sum of individual damages) × 1.5
// ============================================================================

describe('GROUP damage calculation', () => {
  it('applies 1.5x synergy multiplier to the sum of all individual damages', () => {
    // All 3 participants, block failure (roll > 10)
    const failRoll = () => 15;
    const newState = resolveGroup(BASE_STATE, GROUP_DECLARATION, GROUP_ACTION_CONFIG, failRoll);

    // Expected: sum(leader, ally1, ally2 damages) * 1.5 * (1 - FMR)
    const leaderDmg = calculateBaseDamage(LEADER.power, ENEMY.power);
    const ally1Dmg = calculateBaseDamage(ALLY_1.power, ENEMY.power);
    const ally2Dmg = calculateBaseDamage(ALLY_2.power, ENEMY.power);
    const sumDmg = leaderDmg + ally1Dmg + ally2Dmg;
    const groupDamage = sumDmg * 1.5;
    const expectedFinal = groupDamage * (1 - ENEMY.reactionSkills.block.FMR); // failure mitigation

    const enemyAfter = newState.enemyParty.find((c) => c.id === 'enemy1');
    const damageDealt = ENEMY.stamina - (enemyAfter?.stamina ?? ENEMY.stamina);
    expect(damageDealt).toBeCloseTo(expectedFinal, 5);
  });

  it('multiplier stays at 1.5x regardless of participant count (solo GROUP)', () => {
    // All allies are KO'd — leader fires alone
    const koDAlly1 = { ...ALLY_1, isKO: true, stamina: 0 };
    const koDAlly2 = { ...ALLY_2, isKO: true, stamina: 0 };
    const state = makeState([LEADER, koDAlly1, koDAlly2], [ENEMY]);

    const failRoll = () => 15;
    const newState = resolveGroup(state, GROUP_DECLARATION, GROUP_ACTION_CONFIG, failRoll);

    // Only leader participates
    const leaderDmg = calculateBaseDamage(LEADER.power, ENEMY.power);
    const groupDamage = leaderDmg * 1.5;
    const expectedFinal = groupDamage * (1 - ENEMY.reactionSkills.block.FMR);

    const enemyAfter = newState.enemyParty.find((c) => c.id === 'enemy1');
    const damageDealt = ENEMY.stamina - (enemyAfter?.stamina ?? ENEMY.stamina);
    expect(damageDealt).toBeCloseTo(expectedFinal, 5);
  });

  it('uses custom damageMultiplier from config', () => {
    const customConfig = { damageMultiplier: 2.0, energyRequirement: 'full' as const };
    const failRoll = () => 15;
    const newState = resolveGroup(BASE_STATE, GROUP_DECLARATION, customConfig, failRoll);

    const leaderDmg = calculateBaseDamage(LEADER.power, ENEMY.power);
    const ally1Dmg = calculateBaseDamage(ALLY_1.power, ENEMY.power);
    const ally2Dmg = calculateBaseDamage(ALLY_2.power, ENEMY.power);
    const groupDamage = (leaderDmg + ally1Dmg + ally2Dmg) * 2.0;
    const expectedFinal = groupDamage * (1 - ENEMY.reactionSkills.block.FMR);

    const enemyAfter = newState.enemyParty.find((c) => c.id === 'enemy1');
    const damageDealt = ENEMY.stamina - (enemyAfter?.stamina ?? ENEMY.stamina);
    expect(damageDealt).toBeCloseTo(expectedFinal, 5);
  });
});

// ============================================================================
// 4. Block defense applied to final damage
// ============================================================================

describe('GROUP Block defense', () => {
  it('applies Block success mitigation (SMR) when roll succeeds', () => {
    // Block success: roll <= SR * 20 = 0.5 * 20 = 10 → use roll = 5
    const successRoll = () => 5;

    const leaderDmg = calculateBaseDamage(LEADER.power, ENEMY.power);
    const ally1Dmg = calculateBaseDamage(ALLY_1.power, ENEMY.power);
    const ally2Dmg = calculateBaseDamage(ALLY_2.power, ENEMY.power);
    const groupDamage = (leaderDmg + ally1Dmg + ally2Dmg) * 1.5;
    const expectedFinal = groupDamage * (1 - ENEMY.reactionSkills.block.SMR); // success: SMR = 0.4

    const newState = resolveGroup(BASE_STATE, GROUP_DECLARATION, GROUP_ACTION_CONFIG, successRoll);
    const enemyAfter = newState.enemyParty.find((c) => c.id === 'enemy1');
    const damageDealt = ENEMY.stamina - (enemyAfter?.stamina ?? ENEMY.stamina);
    expect(damageDealt).toBeCloseTo(expectedFinal, 5);
  });

  it('applies Block failure mitigation (FMR) when roll fails', () => {
    // Block failure: roll > SR * 20 = 10 → use roll = 15
    const failRoll = () => 15;

    const leaderDmg = calculateBaseDamage(LEADER.power, ENEMY.power);
    const ally1Dmg = calculateBaseDamage(ALLY_1.power, ENEMY.power);
    const ally2Dmg = calculateBaseDamage(ALLY_2.power, ENEMY.power);
    const groupDamage = (leaderDmg + ally1Dmg + ally2Dmg) * 1.5;
    const expectedFinal = groupDamage * (1 - ENEMY.reactionSkills.block.FMR); // failure: FMR = 0.1

    const newState = resolveGroup(BASE_STATE, GROUP_DECLARATION, GROUP_ACTION_CONFIG, failRoll);
    const enemyAfter = newState.enemyParty.find((c) => c.id === 'enemy1');
    const damageDealt = ENEMY.stamina - (enemyAfter?.stamina ?? ENEMY.stamina);
    expect(damageDealt).toBeCloseTo(expectedFinal, 5);
  });

  it('only uses Block defense — never dodge or parry (target cannot avoid GROUP)', () => {
    // This is verified structurally — resolveGroup always calls resolveBlock,
    // not resolveDefense which would allow other defense types.
    // The target's stamina should always decrease (Block never achieves zero damage
    // unlike successful Dodge or Parry), unless SMR = 1.0 which is not the case here.
    const successRoll = () => 5; // block success
    const newState = resolveGroup(BASE_STATE, GROUP_DECLARATION, GROUP_ACTION_CONFIG, successRoll);
    const enemyAfter = newState.enemyParty.find((c) => c.id === 'enemy1');
    // With Block success + SMR=0.4, some damage still applies (60% of groupDamage)
    expect((enemyAfter?.stamina ?? ENEMY.stamina)).toBeLessThan(ENEMY.stamina);
  });
});

// ============================================================================
// 5. All participants' energy zeroed after GROUP
// ============================================================================

describe('GROUP energy consumption', () => {
  it('zeros all non-KO\'d participants\' energy after GROUP', () => {
    const failRoll = () => 15;
    const newState = resolveGroup(BASE_STATE, GROUP_DECLARATION, GROUP_ACTION_CONFIG, failRoll);

    const leaderAfter = newState.playerParty.find((c) => c.id === 'leader');
    const ally1After = newState.playerParty.find((c) => c.id === 'ally1');
    const ally2After = newState.playerParty.find((c) => c.id === 'ally2');

    expect(leaderAfter?.energy).toBe(0);
    expect(ally1After?.energy).toBe(0);
    expect(ally2After?.energy).toBe(0);
  });

  it('does not zero energy of KO\'d allies (they were not participants)', () => {
    // KO'd ally starts with 0 energy — stays 0 — and is excluded from participation
    const koDAlly2 = { ...ALLY_2, isKO: true, stamina: 0, energy: 0 };
    const state = makeState([LEADER, ALLY_1, koDAlly2], [ENEMY]);
    const failRoll = () => 15;
    const newState = resolveGroup(state, GROUP_DECLARATION, GROUP_ACTION_CONFIG, failRoll);

    const ally2After = newState.playerParty.find((c) => c.id === 'ally2');
    // ally2 was KO'd and had 0 energy; confirm it's still KO'd and energy is 0
    expect(ally2After?.isKO).toBe(true);
    expect(ally2After?.energy).toBe(0);
  });

  it('zeros energy even when target is already KO\'d (energy consumed atomically)', () => {
    const koDEnemy = { ...ENEMY, isKO: true, stamina: 0 };
    const state = makeState([LEADER, ALLY_1, ALLY_2], [koDEnemy]);

    const anyRoll = () => 10;
    const newState = resolveGroup(state, GROUP_DECLARATION, GROUP_ACTION_CONFIG, anyRoll);

    const leaderAfter = newState.playerParty.find((c) => c.id === 'leader');
    const ally1After = newState.playerParty.find((c) => c.id === 'ally1');
    const ally2After = newState.playerParty.find((c) => c.id === 'ally2');

    expect(leaderAfter?.energy).toBe(0);
    expect(ally1After?.energy).toBe(0);
    expect(ally2After?.energy).toBe(0);
  });
});

// ============================================================================
// 6. Priority sort — GROUP at index 0
// ============================================================================

describe('GROUP priority sort placement', () => {
  it('GROUP sorts before DEFEND, ATTACK, and EVADE', () => {
    const actions: CombatAction[] = [
      { combatantId: 'ally1', type: 'EVADE', targetId: null },
      { combatantId: 'enemy1', type: 'ATTACK', targetId: 'leader' },
      { combatantId: 'ally2', type: 'DEFEND', targetId: 'leader' },
      { combatantId: 'leader', type: 'GROUP', targetId: 'enemy1' },
    ];

    const state = makeState([LEADER, ALLY_1, ALLY_2], [ENEMY]);
    const deterministicRoll = () => 10; // fixed roll for tie-breaking
    const sorted = sortByPriority(actions, state, deterministicRoll);

    expect(sorted[0].type).toBe('GROUP');
    expect(sorted[1].type).toBe('DEFEND');
    expect(sorted[2].type).toBe('ATTACK');
    expect(sorted[3].type).toBe('EVADE');
  });

  it('GROUP at priority 0, ATTACK at priority 2, EVADE at priority 3', () => {
    const actions: CombatAction[] = [
      { combatantId: 'enemy1', type: 'ATTACK', targetId: 'leader' },
      { combatantId: 'leader', type: 'GROUP', targetId: 'enemy1' },
      { combatantId: 'ally1', type: 'EVADE', targetId: null },
    ];

    const state = makeState([LEADER, ALLY_1, ALLY_2], [ENEMY]);
    const deterministicRoll = () => 10;
    const sorted = sortByPriority(actions, state, deterministicRoll);

    expect(sorted[0].combatantId).toBe('leader'); // GROUP first
    expect(sorted[0].type).toBe('GROUP');
    expect(sorted[1].type).toBe('ATTACK');
    expect(sorted[2].type).toBe('EVADE');
  });
});

// ============================================================================
// 7. Solo GROUP (leader only, all allies KO'd)
// ============================================================================

describe('Solo GROUP (leader only)', () => {
  it('fires with just the leader when all allies are KO\'d', () => {
    const koDAlly1 = { ...ALLY_1, isKO: true, stamina: 0 };
    const koDAlly2 = { ...ALLY_2, isKO: true, stamina: 0 };
    const state = makeState([LEADER, koDAlly1, koDAlly2], [ENEMY]);

    const failRoll = () => 15;
    const newState = resolveGroup(state, GROUP_DECLARATION, GROUP_ACTION_CONFIG, failRoll);

    // Leader's energy should be zeroed
    const leaderAfter = newState.playerParty.find((c) => c.id === 'leader');
    expect(leaderAfter?.energy).toBe(0);

    // Enemy should have taken damage (leader × 1.5 × FMR-mitigated)
    const leaderDmg = calculateBaseDamage(LEADER.power, ENEMY.power);
    const groupDamage = leaderDmg * 1.5;
    const expectedFinal = groupDamage * (1 - ENEMY.reactionSkills.block.FMR);

    const enemyAfter = newState.enemyParty.find((c) => c.id === 'enemy1');
    const damageDealt = ENEMY.stamina - (enemyAfter?.stamina ?? ENEMY.stamina);
    expect(damageDealt).toBeCloseTo(expectedFinal, 5);
  });
});

// ============================================================================
// 8. Immutability — input state is not mutated
// ============================================================================

describe('GROUP immutability', () => {
  it('returns a new CombatState object (does not mutate input)', () => {
    const failRoll = () => 15;
    const newState = resolveGroup(BASE_STATE, GROUP_DECLARATION, GROUP_ACTION_CONFIG, failRoll);
    expect(newState).not.toBe(BASE_STATE);
  });

  it('original player party is not mutated', () => {
    const originalLeaderEnergy = LEADER.energy;
    const failRoll = () => 15;
    resolveGroup(BASE_STATE, GROUP_DECLARATION, GROUP_ACTION_CONFIG, failRoll);
    // Original combatant objects should be unchanged
    expect(LEADER.energy).toBe(originalLeaderEnergy);
    expect(BASE_STATE.playerParty[0].energy).toBe(originalLeaderEnergy);
  });

  it('original enemy stamina is not mutated', () => {
    const originalStamina = ENEMY.stamina;
    const failRoll = () => 15;
    resolveGroup(BASE_STATE, GROUP_DECLARATION, GROUP_ACTION_CONFIG, failRoll);
    expect(ENEMY.stamina).toBe(originalStamina);
    expect(BASE_STATE.enemyParty[0].stamina).toBe(originalStamina);
  });
});

// ============================================================================
// 9. GROUP_ACTION_CONFIG exported constant
// ============================================================================

describe('GROUP_ACTION_CONFIG exported constant', () => {
  it('has damageMultiplier of 1.5', () => {
    expect(GROUP_ACTION_CONFIG.damageMultiplier).toBe(1.5);
  });

  it('has energyRequirement of "full"', () => {
    expect(GROUP_ACTION_CONFIG.energyRequirement).toBe('full');
  });
});
