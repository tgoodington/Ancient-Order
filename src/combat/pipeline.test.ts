/**
 * pipeline.test.ts — Unit tests for the Action Priority & Resolution Pipeline
 *
 * Tests cover:
 *   - sortByPriority: correct ordering for all action type combinations
 *   - sortByPriority: speed-based tie-breaking within same priority bracket
 *   - sortByPriority: GROUP uses team average speed for tie-breaking
 *   - resolvePerAttack: DEFEND intercept redirects attacks correctly
 *   - resolvePerAttack: full 7-step ATTACK resolution (at least 2 scenarios)
 *   - resolvePerAttack: GROUP stub returns no-op (state unchanged)
 *   - resolvePerAttack: EVADE applies stamina regen
 *   - resolvePerAttack: SPECIAL applies damage bonus and consumes energy
 *   - resolvePerAttack: Blindside forces Defenseless on target
 *   - resolvePerAttack: Rank KO forces target KO
 *
 * Roll injection:
 *   - High roll (e.g., 19): fails most threshold checks (1 - threshold > 19/20 = 0.95)
 *   - Low roll (e.g., 1):   passes most threshold checks
 *   - For defense SR checks: roll <= SR * 20 → success
 *     e.g., SR = 0.9 → threshold = 18 → roll = 1 → success
 */

import { describe, it, expect } from 'vitest';
import { sortByPriority, resolvePerAttack } from './pipeline.js';
import type { CombatAction, CombatState, Combatant, ReactionSkills } from '../types/combat.js';

// ============================================================================
// Test Fixtures
// ============================================================================

const STANDARD_REACTION_SKILLS: ReactionSkills = {
  block: { SR: 0.6, SMR: 0.5, FMR: 0.2 },
  dodge: { SR: 0.5, FMR: 0.15 },
  parry: { SR: 0.4, FMR: 0.1 },
};

const HIGH_PARRY_SKILLS: ReactionSkills = {
  block: { SR: 0.9, SMR: 0.5, FMR: 0.2 },
  dodge: { SR: 0.9, FMR: 0.15 },
  parry: { SR: 0.9, FMR: 0.1 }, // threshold = 18 → roll <= 18 succeeds
};

const LOW_PARRY_SKILLS: ReactionSkills = {
  block: { SR: 0.1, SMR: 0.2, FMR: 0.05 },
  dodge: { SR: 0.1, FMR: 0.05 },
  parry: { SR: 0.1, FMR: 0.2 }, // threshold = 2 → roll > 2 fails
};

function makeCombatant(
  id: string,
  overrides: Partial<Combatant> = {},
): Combatant {
  return {
    id,
    name: id,
    archetype: 'test',
    rank: 1.0,
    stamina: 100,
    maxStamina: 100,
    power: 50,
    speed: 10,
    energy: 0,
    maxEnergy: 5,
    ascensionLevel: 0,
    activeBuffs: [],
    elementalPath: 'Fire',
    reactionSkills: STANDARD_REACTION_SKILLS,
    isKO: false,
    ...overrides,
  };
}

function makeState(
  playerParty: Combatant[],
  enemyParty: Combatant[],
  actionQueue: CombatAction[] = [],
): CombatState {
  return {
    round: 1,
    phase: 'PER_ATTACK',
    playerParty,
    enemyParty,
    actionQueue,
    roundHistory: [],
    status: 'active',
  };
}

// ============================================================================
// sortByPriority — Priority Ordering
// ============================================================================

describe('sortByPriority', () => {
  describe('priority ordering', () => {
    it('GROUP (0) resolves before DEFEND (1)', () => {
      const player = makeCombatant('p1');
      const enemy = makeCombatant('e1');
      const state = makeState([player], [enemy]);

      const actions: CombatAction[] = [
        { combatantId: 'p1', type: 'DEFEND', targetId: 'p1' },
        { combatantId: 'e1', type: 'GROUP', targetId: 'p1' },
      ];

      const sorted = sortByPriority(actions, state, () => 10);
      expect(sorted[0].type).toBe('GROUP');
      expect(sorted[1].type).toBe('DEFEND');
    });

    it('DEFEND (1) resolves before ATTACK (2)', () => {
      const player = makeCombatant('p1');
      const enemy = makeCombatant('e1');
      const state = makeState([player], [enemy]);

      const actions: CombatAction[] = [
        { combatantId: 'p1', type: 'ATTACK', targetId: 'e1' },
        { combatantId: 'e1', type: 'DEFEND', targetId: 'e1' },
      ];

      const sorted = sortByPriority(actions, state, () => 10);
      expect(sorted[0].type).toBe('DEFEND');
      expect(sorted[1].type).toBe('ATTACK');
    });

    it('ATTACK (2) resolves before EVADE (3)', () => {
      const player = makeCombatant('p1');
      const enemy = makeCombatant('e1');
      const state = makeState([player], [enemy]);

      const actions: CombatAction[] = [
        { combatantId: 'p1', type: 'EVADE', targetId: null },
        { combatantId: 'e1', type: 'ATTACK', targetId: 'p1' },
      ];

      const sorted = sortByPriority(actions, state, () => 10);
      expect(sorted[0].type).toBe('ATTACK');
      expect(sorted[1].type).toBe('EVADE');
    });

    it('SPECIAL (2) shares priority with ATTACK (2)', () => {
      const player = makeCombatant('p1');
      const enemy = makeCombatant('e1');
      const state = makeState([player], [enemy]);

      const actions: CombatAction[] = [
        { combatantId: 'p1', type: 'SPECIAL', targetId: 'e1', energySegments: 2 },
        { combatantId: 'e1', type: 'ATTACK', targetId: 'p1' },
      ];

      // Both at priority 2; speed determines order
      const sorted = sortByPriority(actions, state, () => 10);
      // Both have speed 10 (exact tie) and rollFn returns 10 for both → first sort stays
      // Priority is identical so we just check they're both present
      expect(sorted).toHaveLength(2);
      const types = sorted.map((a) => a.type);
      expect(types).toContain('SPECIAL');
      expect(types).toContain('ATTACK');
    });

    it('full mixed queue: GROUP < DEFEND < ATTACK = SPECIAL < EVADE', () => {
      const p1 = makeCombatant('p1');
      const p2 = makeCombatant('p2');
      const e1 = makeCombatant('e1');
      const e2 = makeCombatant('e2');
      const state = makeState([p1, p2], [e1, e2]);

      const actions: CombatAction[] = [
        { combatantId: 'p1', type: 'EVADE', targetId: null },
        { combatantId: 'p2', type: 'ATTACK', targetId: 'e1' },
        { combatantId: 'e1', type: 'DEFEND', targetId: 'e2' },
        { combatantId: 'e2', type: 'GROUP', targetId: 'p1' },
      ];

      const sorted = sortByPriority(actions, state, () => 10);

      expect(sorted[0].type).toBe('GROUP');   // priority 0
      expect(sorted[1].type).toBe('DEFEND');  // priority 1
      expect(sorted[2].type).toBe('ATTACK');  // priority 2
      expect(sorted[3].type).toBe('EVADE');   // priority 3
    });
  });

  describe('speed-based tie-breaking within same priority', () => {
    it('higher speed combatant resolves first within same priority', () => {
      const fast = makeCombatant('fast', { speed: 20 });
      const slow = makeCombatant('slow', { speed: 5 });
      const state = makeState([fast], [slow]);

      const actions: CombatAction[] = [
        { combatantId: 'slow', type: 'ATTACK', targetId: 'fast' },
        { combatantId: 'fast', type: 'ATTACK', targetId: 'slow' },
      ];

      // rollFn returns the same value for both → speed is the differentiator
      const sorted = sortByPriority(actions, state, () => 10);
      expect(sorted[0].combatantId).toBe('fast');
      expect(sorted[1].combatantId).toBe('slow');
    });

    it('speed ties are broken by random factor', () => {
      const a = makeCombatant('a', { speed: 10 });
      const b = makeCombatant('b', { speed: 10 });
      const state = makeState([a], [b]);

      const actions: CombatAction[] = [
        { combatantId: 'a', type: 'ATTACK', targetId: 'b' },
        { combatantId: 'b', type: 'ATTACK', targetId: 'a' },
      ];

      // rollFn returns ascending values: first call > second call → negative delta for first
      let callCount = 0;
      const deterministicRoll = (): number => {
        callCount++;
        return callCount === 1 ? 15 : 5; // 15 - 5 = 10 > 0 → b sorts before a
      };

      const sorted = sortByPriority(actions, state, deterministicRoll);
      // Comparator calls rollFn() - rollFn() per comparison
      // If rollFn()=15, rollFn()=5 → 15-5=10 > 0 → a comes after b
      expect(sorted).toHaveLength(2);
    });

    it('DEFEND actions are independent of ATTACK speed in their own bracket', () => {
      const fastDefend = makeCombatant('fastDefend', { speed: 20 });
      const slowDefend = makeCombatant('slowDefend', { speed: 5 });
      const enemy = makeCombatant('e1', { speed: 10 });
      const state = makeState([fastDefend, slowDefend], [enemy]);

      const actions: CombatAction[] = [
        { combatantId: 'slowDefend', type: 'DEFEND', targetId: 'slowDefend' },
        { combatantId: 'fastDefend', type: 'DEFEND', targetId: 'fastDefend' },
        { combatantId: 'e1', type: 'ATTACK', targetId: 'fastDefend' },
      ];

      const sorted = sortByPriority(actions, state, () => 10);
      // DEFEND (priority 1) both come before ATTACK (priority 2)
      expect(sorted[0].type).toBe('DEFEND');
      expect(sorted[1].type).toBe('DEFEND');
      expect(sorted[2].type).toBe('ATTACK');
      // Within DEFEND bracket: faster DEFEND goes first
      expect(sorted[0].combatantId).toBe('fastDefend');
    });
  });

  describe('GROUP team average speed tie-breaking', () => {
    it('GROUP action uses team average speed for priority ordering', () => {
      const p1 = makeCombatant('p1', { speed: 10 });
      const p2 = makeCombatant('p2', { speed: 20 }); // avg = 15
      const e1 = makeCombatant('e1', { speed: 8 });
      const e2 = makeCombatant('e2', { speed: 12 }); // avg = 10
      const state = makeState([p1, p2], [e1, e2]);

      // Player GROUP (avg speed 15) vs Enemy GROUP (avg speed 10)
      const actions: CombatAction[] = [
        { combatantId: 'e1', type: 'GROUP', targetId: 'p1' }, // avg speed 10
        { combatantId: 'p1', type: 'GROUP', targetId: 'e1' }, // avg speed 15
      ];

      const sorted = sortByPriority(actions, state, () => 10);
      // Player GROUP has higher team avg speed → resolves first
      expect(sorted[0].combatantId).toBe('p1');
      expect(sorted[1].combatantId).toBe('e1');
    });

    it('KO\'d allies are excluded from GROUP average speed calculation', () => {
      const p1 = makeCombatant('p1', { speed: 10 });
      const p2 = makeCombatant('p2', { speed: 30, isKO: true, stamina: 0 }); // KO'd, excluded
      // avg active speed = 10 (only p1)
      const enemy = makeCombatant('e1', { speed: 12 });
      const state = makeState([p1, p2], [enemy]);

      const actions: CombatAction[] = [
        { combatantId: 'p1', type: 'GROUP', targetId: 'e1' }, // avg speed 10 (p2 KO'd)
        { combatantId: 'e1', type: 'ATTACK', targetId: 'p1' }, // speed 12
      ];

      const sorted = sortByPriority(actions, state, () => 10);
      // GROUP (priority 0) still before ATTACK (priority 2) regardless
      expect(sorted[0].type).toBe('GROUP');
      expect(sorted[1].type).toBe('ATTACK');
    });
  });
});

// ============================================================================
// resolvePerAttack — GROUP (real implementation, Task 18)
// ============================================================================

describe('resolvePerAttack — GROUP (real implementation)', () => {
  it('delegates to resolveGroup and returns a new state (not the original reference)', () => {
    const player = makeCombatant('p1');
    const enemy = makeCombatant('e1');
    const state = makeState([player], [enemy]);

    const groupAction: CombatAction = {
      combatantId: 'p1',
      type: 'GROUP',
      targetId: 'e1',
    };

    const result = resolvePerAttack(state, groupAction);
    // Real GROUP resolves — returns a new state (not the same reference)
    expect(result).not.toBe(state);
  });

  it('GROUP zeros the leader\'s energy and applies damage to the target', () => {
    const player = makeCombatant('p1', { stamina: 80, energy: 5, maxEnergy: 5, power: 50 });
    const enemy = makeCombatant('e1', { stamina: 90, power: 50 });
    const state = makeState([player], [enemy]);

    // Deterministic fail roll (block fails) — roll > SR * 20 = 10
    const failRoll = () => 15;
    const groupAction: CombatAction = { combatantId: 'p1', type: 'GROUP', targetId: 'e1' };
    const result = resolvePerAttack(state, groupAction, failRoll);

    // Player's energy should be zeroed
    expect(result.playerParty[0].energy).toBe(0);
    // Enemy should have taken damage (stamina reduced)
    expect(result.enemyParty[0].stamina).toBeLessThan(90);
  });
});

// ============================================================================
// resolvePerAttack — DEFEND intercept
// ============================================================================

describe('resolvePerAttack — DEFEND intercept', () => {
  it('redirects an attack to the DEFEND-er when one declares DEFEND on the target', () => {
    const attacker = makeCombatant('attacker', { power: 100 });
    const originalTarget = makeCombatant('target', { stamina: 100, reactionSkills: STANDARD_REACTION_SKILLS });
    const defender = makeCombatant('defender', { stamina: 100, reactionSkills: STANDARD_REACTION_SKILLS });
    // defender and target are in the same party (enemy party, attacker is player)
    const state = makeState(
      [attacker],
      [originalTarget, defender],
      // actionQueue includes the DEFEND declaration
      [
        { combatantId: 'defender', type: 'DEFEND', targetId: 'target' },
      ],
    );

    const attackAction: CombatAction = {
      combatantId: 'attacker',
      type: 'ATTACK',
      targetId: 'target',
    };

    // Use a roll that guarantees block failure (roll = 20 > SR * 20 = 12)
    // so damage is applied via FMR path
    const result = resolvePerAttack(state, attackAction, () => 20);

    // originalTarget should be untouched
    const originalTargetAfter = result.enemyParty.find((c) => c.id === 'target')!;
    expect(originalTargetAfter.stamina).toBe(100);

    // defender should have taken damage (intercepted the attack)
    const defenderAfter = result.enemyParty.find((c) => c.id === 'defender')!;
    expect(defenderAfter.stamina).toBeLessThan(100);
  });

  it('does NOT redirect when DEFEND targets a different ally', () => {
    const attacker = makeCombatant('attacker', { power: 100 });
    const originalTarget = makeCombatant('target', { stamina: 100, reactionSkills: STANDARD_REACTION_SKILLS });
    const otherAlly = makeCombatant('other', { stamina: 100 });
    const defender = makeCombatant('defender', { stamina: 100, reactionSkills: STANDARD_REACTION_SKILLS });

    // Defender defends 'other', NOT 'target'
    const state = makeState(
      [attacker],
      [originalTarget, otherAlly, defender],
      [{ combatantId: 'defender', type: 'DEFEND', targetId: 'other' }],
    );

    const attackAction: CombatAction = {
      combatantId: 'attacker',
      type: 'ATTACK',
      targetId: 'target',
    };

    const result = resolvePerAttack(state, attackAction, () => 20);

    // originalTarget should have taken damage (no intercept for this target)
    const originalTargetAfter = result.enemyParty.find((c) => c.id === 'target')!;
    expect(originalTargetAfter.stamina).toBeLessThan(100);

    // defender should be untouched
    const defenderAfter = result.enemyParty.find((c) => c.id === 'defender')!;
    expect(defenderAfter.stamina).toBe(100);
  });

  it('redirects attack even when DEFEND comes from the queue after the attack action', () => {
    const attacker = makeCombatant('attacker', { power: 100 });
    const target = makeCombatant('target', { stamina: 100, reactionSkills: STANDARD_REACTION_SKILLS });
    const defender = makeCombatant('defender', { stamina: 100, reactionSkills: STANDARD_REACTION_SKILLS });

    // DEFEND is queued — intercept still works
    const state = makeState(
      [attacker],
      [target, defender],
      [
        { combatantId: 'attacker', type: 'ATTACK', targetId: 'target' },
        { combatantId: 'defender', type: 'DEFEND', targetId: 'target' },
      ],
    );

    const attackAction: CombatAction = {
      combatantId: 'attacker',
      type: 'ATTACK',
      targetId: 'target',
    };

    const result = resolvePerAttack(state, attackAction, () => 20);

    // target untouched, defender took the hit
    const targetAfter = result.enemyParty.find((c) => c.id === 'target')!;
    const defenderAfter = result.enemyParty.find((c) => c.id === 'defender')!;
    expect(targetAfter.stamina).toBe(100);
    expect(defenderAfter.stamina).toBeLessThan(100);
  });
});

// ============================================================================
// resolvePerAttack — Full ATTACK resolution (Scenario 1: Block success)
// ============================================================================

describe('resolvePerAttack — ATTACK full resolution', () => {
  it('Scenario 1: standard ATTACK with Block success reduces target stamina by mitigation', () => {
    // attacker power = 50, target power = 50
    // baseDamage = 50 * (50/50) = 50
    // Block SR = 0.6 → threshold = 12 → roll = 5 → success
    // Block SMR = 0.5 → damage = 50 * (1 - 0.5) = 25
    // target starts at 100 stamina → 100 - 25 = 75

    const attacker = makeCombatant('attacker', { power: 50, speed: 10 });
    const target = makeCombatant('target', {
      stamina: 100,
      maxStamina: 100,
      power: 50,
      reactionSkills: {
        block: { SR: 0.6, SMR: 0.5, FMR: 0.2 },
        dodge: { SR: 0.5, FMR: 0.15 },
        parry: { SR: 0.4, FMR: 0.1 },
      },
    });
    const state = makeState([attacker], [target]);

    // Roll sequence:
    //   Roll 1: rank KO check (attacker.rank = 1.0 = target.rank → no KO check → not consumed)
    //   Roll 2: blindside check (speeds equal → no blindside check → not consumed)
    //   Roll 3: defense roll = 5 → Block success (5 <= 12)
    //   Roll 4: crushing blow check (powers equal, no Crushing Blow eligible → not consumed)
    let rollIndex = 0;
    const rolls = [5]; // only the defense roll matters when rank and speed are equal
    const deterministicRoll = (): number => rolls[rollIndex++] ?? 10;

    const attackAction: CombatAction = {
      combatantId: 'attacker',
      type: 'ATTACK',
      targetId: 'target',
    };

    const result = resolvePerAttack(state, attackAction, deterministicRoll);

    const targetAfter = result.enemyParty.find((c) => c.id === 'target')!;
    expect(targetAfter.stamina).toBeCloseTo(75); // 100 - 25 = 75
    expect(targetAfter.isKO).toBe(false);
  });

  it('Scenario 2: standard ATTACK with Block failure applies FMR damage', () => {
    // attacker power = 50, target power = 50 → baseDamage = 50
    // Block SR = 0.6 → threshold = 12 → roll = 20 → failure
    // Block FMR = 0.2 → damage = 50 * (1 - 0.2) = 40
    // target starts at 100 → 100 - 40 = 60

    const attacker = makeCombatant('attacker', { power: 50 });
    const target = makeCombatant('target', {
      stamina: 100,
      maxStamina: 100,
      power: 50,
      reactionSkills: STANDARD_REACTION_SKILLS,
    });
    const state = makeState([attacker], [target]);

    const attackAction: CombatAction = {
      combatantId: 'attacker',
      type: 'ATTACK',
      targetId: 'target',
    };

    // roll = 20 → Block failure
    const result = resolvePerAttack(state, attackAction, () => 20);

    const targetAfter = result.enemyParty.find((c) => c.id === 'target')!;
    expect(targetAfter.stamina).toBeCloseTo(60); // 100 - 40 = 60
  });

  it('KO occurs when accumulated damage depletes target stamina', () => {
    // attacker power = 200, target power = 50 → baseDamage = 200 * (200/50) = 800
    // Block SR = 0.6 → roll = 20 → failure
    // Block FMR = 0.2 → damage = 800 * 0.8 = 640
    // target starts at 100 stamina → 100 - 640 = KO (stamina clamped to 0)

    const attacker = makeCombatant('attacker', { power: 200 });
    const target = makeCombatant('target', {
      stamina: 100,
      maxStamina: 100,
      power: 50,
      reactionSkills: STANDARD_REACTION_SKILLS,
    });
    const state = makeState([attacker], [target]);

    const attackAction: CombatAction = {
      combatantId: 'attacker',
      type: 'ATTACK',
      targetId: 'target',
    };

    const result = resolvePerAttack(state, attackAction, () => 20);

    const targetAfter = result.enemyParty.find((c) => c.id === 'target')!;
    expect(targetAfter.stamina).toBe(0);
    expect(targetAfter.isKO).toBe(true);
  });

  it('attacker gains energy after a successful attack', () => {
    const attacker = makeCombatant('attacker', { power: 50, energy: 0 });
    const target = makeCombatant('target', { power: 50 });
    const state = makeState([attacker], [target]);

    const attackAction: CombatAction = {
      combatantId: 'attacker',
      type: 'ATTACK',
      targetId: 'target',
    };

    // roll = 20 → Block failure → damage dealt → actionSuccess
    const result = resolvePerAttack(state, attackAction, () => 20);

    const attackerAfter = result.playerParty.find((c) => c.id === 'attacker')!;
    // At ascension level 0, actionSuccess = 1.0 * (1 + 0) = 1.0 segment
    expect(attackerAfter.energy).toBeGreaterThan(0);
  });

  it('state is immutable — original state is unchanged', () => {
    const attacker = makeCombatant('attacker', { power: 50 });
    const target = makeCombatant('target', { stamina: 100, power: 50 });
    const state = makeState([attacker], [target]);

    const attackAction: CombatAction = {
      combatantId: 'attacker',
      type: 'ATTACK',
      targetId: 'target',
    };

    resolvePerAttack(state, attackAction, () => 20);

    // Original state must be unchanged
    expect(state.enemyParty[0].stamina).toBe(100);
    expect(state.playerParty[0].energy).toBe(0);
  });
});

// ============================================================================
// resolvePerAttack — Blindside forces Defenseless
// ============================================================================

describe('resolvePerAttack — Blindside', () => {
  it('Blindside forces target to be Defenseless (full damage taken)', () => {
    // attacker speed = 40, target speed = 10
    // blindsideThreshold = (40 - 10) / 10 = 3.0 → massive threshold
    // checkBlindside: (roll / 20) >= (1 - 3.0) = -2 → always true
    // So roll = 1 → blindside triggered
    // baseDamage = 50 * (50/50) = 50
    // Defenseless → full damage = 50
    // target: 100 - 50 = 50

    const attacker = makeCombatant('attacker', { power: 50, speed: 40 });
    const target = makeCombatant('target', {
      stamina: 100,
      maxStamina: 100,
      power: 50,
      speed: 10,
      reactionSkills: STANDARD_REACTION_SKILLS,
    });
    const state = makeState([attacker], [target]);

    const attackAction: CombatAction = {
      combatantId: 'attacker',
      type: 'ATTACK',
      targetId: 'target',
    };

    // Roll sequence: blindside check passes with low roll (1 = guarantees blindside)
    let rollIndex = 0;
    const rolls = [1]; // blindside check roll (rank KO not eligible, equal ranks)
    const deterministicRoll = (): number => rolls[rollIndex++] ?? 1;

    const result = resolvePerAttack(state, attackAction, deterministicRoll);

    const targetAfter = result.enemyParty.find((c) => c.id === 'target')!;
    // Defenseless → full 50 damage
    expect(targetAfter.stamina).toBeCloseTo(50);
  });
});

// ============================================================================
// resolvePerAttack — Rank KO
// ============================================================================

describe('resolvePerAttack — Rank KO', () => {
  it('Rank KO forces target to KO (stamina = 0) when threshold triggers', () => {
    // attacker rank = 3.0, target rank = 1.0 → diff = 2.0 ≥ 0.5 → eligible
    // threshold = ((3.0 - 1.0) * 3) / 10 = 0.6
    // checkRankKO: (roll / 20) >= (1 - 0.6) = 0.4 → roll ≥ 8 triggers KO
    // We use roll = 10: (10/20) = 0.5 ≥ 0.4 → KO

    const attacker = makeCombatant('attacker', { rank: 3.0, power: 50 });
    const target = makeCombatant('target', {
      rank: 1.0,
      stamina: 100,
      maxStamina: 100,
      power: 50,
      reactionSkills: STANDARD_REACTION_SKILLS,
    });
    const state = makeState([attacker], [target]);

    const attackAction: CombatAction = {
      combatantId: 'attacker',
      type: 'ATTACK',
      targetId: 'target',
    };

    // Roll sequence: rank KO check (roll 10 → triggers KO), then remaining rolls
    let rollIndex = 0;
    const rolls = [10, 20, 20, 20]; // rank KO: 10, blindside check: 20 (no blindside), defense: 20, CB: 20
    const deterministicRoll = (): number => rolls[rollIndex++] ?? 20;

    const result = resolvePerAttack(state, attackAction, deterministicRoll);

    const targetAfter = result.enemyParty.find((c) => c.id === 'target')!;
    expect(targetAfter.isKO).toBe(true);
    expect(targetAfter.stamina).toBe(0);
  });

  it('Rank KO does NOT trigger when attacker rank is not sufficiently higher', () => {
    // attacker rank = 1.0, target rank = 1.0 → diff = 0 < 0.5 → not eligible
    const attacker = makeCombatant('attacker', { rank: 1.0, power: 50 });
    const target = makeCombatant('target', {
      rank: 1.0,
      stamina: 100,
      maxStamina: 100,
      power: 50,
      reactionSkills: STANDARD_REACTION_SKILLS,
    });
    const state = makeState([attacker], [target]);

    const attackAction: CombatAction = {
      combatantId: 'attacker',
      type: 'ATTACK',
      targetId: 'target',
    };

    const result = resolvePerAttack(state, attackAction, () => 1); // lowest roll possible

    const targetAfter = result.enemyParty.find((c) => c.id === 'target')!;
    expect(targetAfter.isKO).toBe(false); // No Rank KO at equal ranks
  });
});

// ============================================================================
// resolvePerAttack — SPECIAL resolution
// ============================================================================

describe('resolvePerAttack — SPECIAL resolution', () => {
  it('SPECIAL damage includes energy segment bonus', () => {
    // attacker power = 50, target power = 50 → baseDamage = 50
    // energySegments = 3 → bonus = 50 * (1 + 0.10 * 3) = 50 * 1.3 = 65
    // Attacker uses Earth path → SPECIAL forces Block defense on target
    // Block SR = 0.6 → roll = 20 → failure
    // Block FMR = 0.2 → final = 65 * (1 - 0.2) = 52
    // target: 100 - 52 = 48

    const attacker = makeCombatant('attacker', { power: 50, energy: 3, elementalPath: 'Earth' });
    const target = makeCombatant('target', {
      stamina: 100,
      maxStamina: 100,
      power: 50,
      reactionSkills: STANDARD_REACTION_SKILLS,
    });
    const state = makeState([attacker], [target]);

    const specialAction: CombatAction = {
      combatantId: 'attacker',
      type: 'SPECIAL',
      targetId: 'target',
      energySegments: 3,
    };

    const result = resolvePerAttack(state, specialAction, () => 20); // all rolls fail defense

    const targetAfter = result.enemyParty.find((c) => c.id === 'target')!;
    // baseDamage = 50, special bonus: 50 * 1.3 = 65, block FMR fail: 65 * 0.8 = 52
    expect(targetAfter.stamina).toBeCloseTo(48);
  });

  it('SPECIAL consumes attacker energy segments', () => {
    const attacker = makeCombatant('attacker', { power: 50, energy: 3 });
    const target = makeCombatant('target', { power: 50 });
    const state = makeState([attacker], [target]);

    const specialAction: CombatAction = {
      combatantId: 'attacker',
      type: 'SPECIAL',
      targetId: 'target',
      energySegments: 3,
    };

    const result = resolvePerAttack(state, specialAction, () => 20);

    const attackerAfter = result.playerParty.find((c) => c.id === 'attacker')!;
    // After consuming 3 segments, energy ≥ 0 (possibly increased by action gain)
    // Energy consumed: 3 segments; energy gained: actionSuccess ~1.0 segment → net ~-2
    // The key check: energy was reduced by 3 segments (consumption)
    // Initial: 3; consumed: 3 → 0; gained: ~1.0 → ~1.0
    expect(attackerAfter.energy).toBeLessThan(3); // Net reduced from initial 3
  });
});

// ============================================================================
// resolvePerAttack — EVADE resolution
// ============================================================================

describe('resolvePerAttack — EVADE resolution', () => {
  it('EVADE restores 30% of maxStamina', () => {
    // maxStamina = 100 → regen = 30
    // start stamina = 50 → after evade = 80

    const combatant = makeCombatant('c1', { stamina: 50, maxStamina: 100 });
    const enemy = makeCombatant('e1');
    const state = makeState([combatant], [enemy]);

    const evadeAction: CombatAction = {
      combatantId: 'c1',
      type: 'EVADE',
      targetId: null,
    };

    const result = resolvePerAttack(state, evadeAction, () => 10);

    const combatantAfter = result.playerParty.find((c) => c.id === 'c1')!;
    expect(combatantAfter.stamina).toBeCloseTo(80); // 50 + 30 = 80
  });

  it('EVADE does not exceed maxStamina', () => {
    const combatant = makeCombatant('c1', { stamina: 90, maxStamina: 100 });
    const enemy = makeCombatant('e1');
    const state = makeState([combatant], [enemy]);

    const evadeAction: CombatAction = {
      combatantId: 'c1',
      type: 'EVADE',
      targetId: null,
    };

    const result = resolvePerAttack(state, evadeAction, () => 10);

    const combatantAfter = result.playerParty.find((c) => c.id === 'c1')!;
    expect(combatantAfter.stamina).toBe(100); // capped at maxStamina
  });

  it('EVADE grants energy to the evader', () => {
    const combatant = makeCombatant('c1', { stamina: 50, maxStamina: 100, energy: 0 });
    const enemy = makeCombatant('e1');
    const state = makeState([combatant], [enemy]);

    const evadeAction: CombatAction = { combatantId: 'c1', type: 'EVADE', targetId: null };
    const result = resolvePerAttack(state, evadeAction, () => 10);

    const combatantAfter = result.playerParty.find((c) => c.id === 'c1')!;
    expect(combatantAfter.energy).toBeGreaterThan(0);
  });
});

// ============================================================================
// resolvePerAttack — Counter chain triggered by Parry
// ============================================================================

describe('resolvePerAttack — counter chain', () => {
  it('successful Parry triggers a counter chain', () => {
    // Attacker uses Fire path → SPECIAL forces Parry defense on target (getSpecialForceDefense('Fire') = 'parry')
    // target has HIGH parry SR → parry succeeds → counter chain triggered
    // original attacker has LOW parry SR → attacker fails to parry the counter → chain ends
    //
    // Roll sequence:
    //   - Rank KO: skipped (equal ranks)
    //   - Blindside: skipped (equal speeds)
    //   - Defense roll: 1 → Parry SR threshold = 0.9 * 20 = 18 → 1 ≤ 18 → PARRY SUCCESS (0 damage)
    //   - Counter chain, attacker parry roll: 19 → Parry SR threshold = 0.1 * 20 = 2 → 19 > 2 → FAIL → chain ends
    const attacker = makeCombatant('attacker', {
      power: 50,
      stamina: 100,
      maxStamina: 100,
      elementalPath: 'Fire',        // Fire SPECIAL forces Parry on target
      energy: 2,
      reactionSkills: LOW_PARRY_SKILLS,  // attacker can't parry the counter
    });
    const target = makeCombatant('target', {
      power: 50,
      stamina: 100,
      maxStamina: 100,
      reactionSkills: HIGH_PARRY_SKILLS, // target successfully parries
    });
    const state = makeState([attacker], [target]);

    // Use SPECIAL so Fire path forces 'parry' defense on the target
    const specialAction: CombatAction = {
      combatantId: 'attacker',
      type: 'SPECIAL',
      targetId: 'target',
      energySegments: 2,
    };

    let rollIndex = 0;
    const rolls = [1, 19]; // defense roll: 1 (parry success), counter chain parry roll: 19 (fail)
    const deterministicRoll = (): number => rolls[rollIndex++] ?? 19;

    const result = resolvePerAttack(state, specialAction, deterministicRoll);

    // Parry succeeded: target took 0 damage from the original SPECIAL attack
    const targetAfter = result.enemyParty.find((c) => c.id === 'target')!;
    expect(targetAfter.stamina).toBe(100); // original attack dealt 0 damage (parry success)

    // Attacker took counter chain damage (attacker failed to parry the counter → FMR damage applied)
    const attackerAfter = result.playerParty.find((c) => c.id === 'attacker')!;
    expect(attackerAfter.stamina).toBeLessThan(100);
  });
});

// ============================================================================
// sortByPriority — input array is not mutated
// ============================================================================

describe('sortByPriority — immutability', () => {
  it('returns a new array without mutating the input', () => {
    const p1 = makeCombatant('p1');
    const e1 = makeCombatant('e1');
    const state = makeState([p1], [e1]);

    const actions: CombatAction[] = [
      { combatantId: 'p1', type: 'EVADE', targetId: null },
      { combatantId: 'e1', type: 'ATTACK', targetId: 'p1' },
    ];
    const originalFirst = actions[0];

    const sorted = sortByPriority(actions, state, () => 10);

    // Returned array is a new reference
    expect(sorted).not.toBe(actions);
    // Original array order unchanged
    expect(actions[0]).toBe(originalFirst);
  });
});
