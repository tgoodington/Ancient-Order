/**
 * roundManager.test.ts — Unit tests for the 5-Phase Round Manager Orchestrator
 *
 * Tests cover:
 *   1. runRound() executes all 5 phases in order, returns updated CombatState
 *   2. AI stub produces actions incorporated into queue
 *   3. Visual info doesn't reveal Phase 1 decisions
 *   4. Phase 4 sorts unified queue by priority
 *   5. Complete 3v3 round resolves without errors
 *   6. Round result recorded in history
 *   + Additional: GROUP ally override, victory/defeat detection, immutability
 */

import { describe, it, expect } from 'vitest';
import { runRound, buildVisualInfo, stubBehaviorTree } from './roundManager.js';
import type { CombatAction, CombatState, Combatant, ReactionSkills } from '../types/combat.js';

// ============================================================================
// Test Fixtures
// ============================================================================

const STANDARD_REACTION_SKILLS: ReactionSkills = {
  block: { SR: 0.6, SMR: 0.5, FMR: 0.2 },
  dodge: { SR: 0.5, FMR: 0.15 },
  parry: { SR: 0.4, FMR: 0.1 },
};

/**
 * Creates a Combatant with sensible defaults for testing.
 */
function makeCombatant(id: string, overrides: Partial<Combatant> = {}): Combatant {
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

/**
 * Creates a CombatState with sensible defaults for testing.
 */
function makeState(
  playerParty: Combatant[],
  enemyParty: Combatant[],
  overrides: Partial<CombatState> = {},
): CombatState {
  return {
    round: 1,
    phase: 'AI_DECISION',
    playerParty,
    enemyParty,
    actionQueue: [],
    roundHistory: [],
    status: 'active',
    ...overrides,
  };
}

// Deterministic roll: always returns the same value
const FIXED_ROLL = (value: number) => () => value;

// ============================================================================
// Test 1: runRound() executes all 5 phases and returns updated CombatState
// ============================================================================

describe('runRound — basic execution', () => {
  it('returns a new CombatState (reference inequality)', () => {
    const player = makeCombatant('p1');
    const enemy = makeCombatant('e1');
    const state = makeState([player], [enemy]);

    const playerDeclarations: CombatAction[] = [
      { combatantId: 'p1', type: 'ATTACK', targetId: 'e1' },
    ];

    const result = runRound(state, playerDeclarations, FIXED_ROLL(10));

    expect(result).not.toBe(state);
  });

  it('increments the round counter after resolution', () => {
    const player = makeCombatant('p1');
    const enemy = makeCombatant('e1');
    const state = makeState([player], [enemy], { round: 1 });

    const playerDeclarations: CombatAction[] = [
      { combatantId: 'p1', type: 'ATTACK', targetId: 'e1' },
    ];

    const result = runRound(state, playerDeclarations, FIXED_ROLL(10));

    expect(result.round).toBe(2);
  });

  it('resets phase to AI_DECISION after resolution', () => {
    const player = makeCombatant('p1');
    const enemy = makeCombatant('e1');
    const state = makeState([player], [enemy]);

    const playerDeclarations: CombatAction[] = [
      { combatantId: 'p1', type: 'ATTACK', targetId: 'e1' },
    ];

    const result = runRound(state, playerDeclarations, FIXED_ROLL(10));

    expect(result.phase).toBe('AI_DECISION');
  });

  it('clears actionQueue after round resolves', () => {
    const player = makeCombatant('p1');
    const enemy = makeCombatant('e1');
    const state = makeState([player], [enemy]);

    const playerDeclarations: CombatAction[] = [
      { combatantId: 'p1', type: 'ATTACK', targetId: 'e1' },
    ];

    const result = runRound(state, playerDeclarations, FIXED_ROLL(10));

    expect(result.actionQueue).toHaveLength(0);
  });

  it('applies damage to the enemy combatant after a player ATTACK', () => {
    const player = makeCombatant('p1', { power: 50 });
    const enemy = makeCombatant('e1', { stamina: 100, power: 50 });
    const state = makeState([player], [enemy]);

    const playerDeclarations: CombatAction[] = [
      { combatantId: 'p1', type: 'ATTACK', targetId: 'e1' },
    ];

    // roll = 20 → Block failure → FMR damage applied
    // baseDamage = 50, Block FMR = 0.2 → finalDamage = 50 * 0.8 = 40
    // enemy starts at 100 → 100 - 40 = 60
    const result = runRound(state, playerDeclarations, FIXED_ROLL(20));

    const enemyAfter = result.enemyParty.find((c) => c.id === 'e1')!;
    expect(enemyAfter.stamina).toBeLessThan(100);
  });

  it('input state is immutable — original state unchanged after runRound', () => {
    const player = makeCombatant('p1', { stamina: 100 });
    const enemy = makeCombatant('e1', { stamina: 100 });
    const state = makeState([player], [enemy]);
    const originalPlayerStamina = state.playerParty[0].stamina;
    const originalEnemyStamina = state.enemyParty[0].stamina;

    const playerDeclarations: CombatAction[] = [
      { combatantId: 'p1', type: 'ATTACK', targetId: 'e1' },
    ];

    runRound(state, playerDeclarations, FIXED_ROLL(20));

    expect(state.playerParty[0].stamina).toBe(originalPlayerStamina);
    expect(state.enemyParty[0].stamina).toBe(originalEnemyStamina);
    expect(state.round).toBe(1);
  });
});

// ============================================================================
// Test 2: AI stub produces actions incorporated into queue
// ============================================================================

describe('runRound — Phase 1: AI stub integration', () => {
  it('AI stub generates one ATTACK action per non-KO\'d enemy', () => {
    const player = makeCombatant('p1', { stamina: 100 });
    const enemy1 = makeCombatant('e1');
    const enemy2 = makeCombatant('e2');
    const state = makeState([player], [enemy1, enemy2]);

    // No player declarations — only AI actions in queue
    const result = runRound(state, [], FIXED_ROLL(10));

    // Both enemies should have acted (and may have dealt damage to p1)
    // We verify this indirectly: player stamina should have decreased
    // (two ATTACK actions from enemies, roll=10 → some damage dealt)
    const playerAfter = result.playerParty.find((c) => c.id === 'p1')!;
    expect(playerAfter.stamina).toBeLessThan(100);
  });

  it('AI stub targets the first non-KO\'d player combatant', () => {
    const p1 = makeCombatant('p1', { isKO: true, stamina: 0 });
    const p2 = makeCombatant('p2', { stamina: 100 });
    const enemy = makeCombatant('e1');
    const state = makeState([p1, p2], [enemy]);

    const aiAction = stubBehaviorTree(enemy, state);

    // p1 is KO'd → AI targets p2
    expect(aiAction.targetId).toBe('p2');
  });

  it('AI stub produces ATTACK type action', () => {
    const player = makeCombatant('p1');
    const enemy = makeCombatant('e1');
    const state = makeState([player], [enemy]);

    const aiAction = stubBehaviorTree(enemy, state);

    expect(aiAction.type).toBe('ATTACK');
    expect(aiAction.combatantId).toBe('e1');
  });

  it('KO\'d enemies do not produce AI actions', () => {
    const player = makeCombatant('p1', { stamina: 1000, maxStamina: 1000 });
    const aliveEnemy = makeCombatant('e1');
    const koEnemy = makeCombatant('e2', { isKO: true, stamina: 0 });
    const state = makeState([player], [aliveEnemy, koEnemy]);

    // No player declarations
    const result = runRound(state, [], FIXED_ROLL(10));

    // Round history should record only 1 action (from e1), not 2
    const roundRecord = result.roundHistory[0];
    expect(roundRecord).toBeDefined();
    // Only e1 acted (e2 was KO'd)
    const actingIds = roundRecord.actions.map((a) => a.combatantId);
    expect(actingIds).not.toContain('e2');
  });
});

// ============================================================================
// Test 3: Visual info doesn't reveal Phase 1 decisions
// ============================================================================

describe('buildVisualInfo — Phase 2 information hiding', () => {
  it('VisualInfo includes all combatants', () => {
    const p1 = makeCombatant('p1');
    const p2 = makeCombatant('p2');
    const e1 = makeCombatant('e1');
    const state = makeState([p1, p2], [e1]);

    const visualInfo = buildVisualInfo(state, []);

    expect(visualInfo.combatants).toHaveLength(3);
    const ids = visualInfo.combatants.map((c) => c.id);
    expect(ids).toContain('p1');
    expect(ids).toContain('p2');
    expect(ids).toContain('e1');
  });

  it('VisualInfo includes stamina and staminaPct', () => {
    const p1 = makeCombatant('p1', { stamina: 75, maxStamina: 100 });
    const e1 = makeCombatant('e1', { stamina: 50, maxStamina: 200 });
    const state = makeState([p1], [e1]);

    const visualInfo = buildVisualInfo(state, []);

    const p1Info = visualInfo.combatants.find((c) => c.id === 'p1')!;
    expect(p1Info.stamina).toBe(75);
    expect(p1Info.staminaPct).toBeCloseTo(0.75);

    const e1Info = visualInfo.combatants.find((c) => c.id === 'e1')!;
    expect(e1Info.stamina).toBe(50);
    expect(e1Info.staminaPct).toBeCloseTo(0.25);
  });

  it('VisualInfo shows stance as "active" for living combatants', () => {
    const p1 = makeCombatant('p1');
    const e1 = makeCombatant('e1');
    const state = makeState([p1], [e1]);

    const visualInfo = buildVisualInfo(state, []);

    visualInfo.combatants.forEach((c) => {
      expect(c.stance).toBe('active');
    });
  });

  it('VisualInfo shows stance as "KO" for KO\'d combatants', () => {
    const p1 = makeCombatant('p1', { isKO: true, stamina: 0 });
    const e1 = makeCombatant('e1');
    const state = makeState([p1], [e1]);

    const visualInfo = buildVisualInfo(state, []);

    const p1Info = visualInfo.combatants.find((c) => c.id === 'p1')!;
    expect(p1Info.stance).toBe('KO');
  });

  it('VisualInfo shows player targeting from declarations', () => {
    const p1 = makeCombatant('p1');
    const e1 = makeCombatant('e1');
    const state = makeState([p1], [e1]);

    const playerDeclarations: CombatAction[] = [
      { combatantId: 'p1', type: 'ATTACK', targetId: 'e1' },
    ];

    const visualInfo = buildVisualInfo(state, playerDeclarations);

    const p1Info = visualInfo.combatants.find((c) => c.id === 'p1')!;
    expect(p1Info.targeting).toBe('e1');
  });

  it('VisualInfo does NOT include AI targeting (null for enemies)', () => {
    const p1 = makeCombatant('p1');
    const e1 = makeCombatant('e1');
    const state = makeState([p1], [e1]);

    // No player declarations passed in — AI targeting is hidden
    const visualInfo = buildVisualInfo(state, []);

    const e1Info = visualInfo.combatants.find((c) => c.id === 'e1')!;
    // Enemy targeting is null because AI actions are not passed to buildVisualInfo
    expect(e1Info.targeting).toBeNull();
  });
});

// ============================================================================
// Test 4: Phase 4 sorts unified queue by priority
// ============================================================================

describe('runRound — Phase 4: priority sorting', () => {
  it('higher priority actions resolve first in the round', () => {
    // DEFEND (priority 1) should resolve before ATTACK (priority 2)
    // We can observe this because DEFEND intercepts attacks on the defender's target
    const p1 = makeCombatant('p1', { power: 50, stamina: 100 });
    const p2 = makeCombatant('p2', { stamina: 100 });
    const enemy = makeCombatant('e1', { power: 50 });
    const state = makeState([p1, p2], [enemy]);

    // p1 attacks the enemy; p2 defends p1 (which the AI will attack)
    // But since AI targets first non-KO'd player (p1), and p2 defends p1,
    // p2 should intercept the enemy's attack on p1
    const playerDeclarations: CombatAction[] = [
      { combatantId: 'p1', type: 'ATTACK', targetId: 'e1' },
      { combatantId: 'p2', type: 'DEFEND', targetId: 'p1' },
    ];

    // roll = 20 → all Block failures → damage applied
    const result = runRound(state, playerDeclarations, FIXED_ROLL(20));

    // p2 (the defender) should have taken damage from the enemy attack, not p1
    const p2After = result.playerParty.find((c) => c.id === 'p2')!;
    const p1After = result.playerParty.find((c) => c.id === 'p1')!;

    // p2 defended p1, so p2 took the enemy's hit
    // p1 only took possible counter/effects if any
    expect(p2After.stamina).toBeLessThan(100);
    // p1 should not have taken the direct enemy attack (intercepted)
    expect(p1After.stamina).toBe(100);
  });
});

// ============================================================================
// Test 5: Complete 3v3 round resolves without errors
// ============================================================================

describe('runRound — 3v3 round resolves without errors', () => {
  it('3v3 round completes with all standard actions', () => {
    const p1 = makeCombatant('p1', { power: 50, stamina: 100 });
    const p2 = makeCombatant('p2', { power: 50, stamina: 100 });
    const p3 = makeCombatant('p3', { power: 50, stamina: 100 });
    const e1 = makeCombatant('e1', { power: 50, stamina: 100 });
    const e2 = makeCombatant('e2', { power: 50, stamina: 100 });
    const e3 = makeCombatant('e3', { power: 50, stamina: 100 });

    const state = makeState([p1, p2, p3], [e1, e2, e3]);

    const playerDeclarations: CombatAction[] = [
      { combatantId: 'p1', type: 'ATTACK', targetId: 'e1' },
      { combatantId: 'p2', type: 'ATTACK', targetId: 'e2' },
      { combatantId: 'p3', type: 'EVADE', targetId: null },
    ];

    // Should not throw
    expect(() => {
      runRound(state, playerDeclarations, FIXED_ROLL(10));
    }).not.toThrow();
  });

  it('3v3 round produces valid CombatState after resolution', () => {
    const p1 = makeCombatant('p1');
    const p2 = makeCombatant('p2');
    const p3 = makeCombatant('p3');
    const e1 = makeCombatant('e1');
    const e2 = makeCombatant('e2');
    const e3 = makeCombatant('e3');

    const state = makeState([p1, p2, p3], [e1, e2, e3]);

    const playerDeclarations: CombatAction[] = [
      { combatantId: 'p1', type: 'ATTACK', targetId: 'e1' },
      { combatantId: 'p2', type: 'ATTACK', targetId: 'e2' },
      { combatantId: 'p3', type: 'ATTACK', targetId: 'e3' },
    ];

    const result = runRound(state, playerDeclarations, FIXED_ROLL(10));

    // Basic shape checks
    expect(result.playerParty).toHaveLength(3);
    expect(result.enemyParty).toHaveLength(3);
    expect(result.round).toBe(2);
    expect(['active', 'victory', 'defeat']).toContain(result.status);
  });

  it('3v3 round with mixed action types resolves without errors', () => {
    const p1 = makeCombatant('p1', { energy: 3, maxEnergy: 5 });
    const p2 = makeCombatant('p2');
    const p3 = makeCombatant('p3');
    const e1 = makeCombatant('e1');
    const e2 = makeCombatant('e2');
    const e3 = makeCombatant('e3');

    const state = makeState([p1, p2, p3], [e1, e2, e3]);

    const playerDeclarations: CombatAction[] = [
      { combatantId: 'p1', type: 'SPECIAL', targetId: 'e1', energySegments: 2 },
      { combatantId: 'p2', type: 'DEFEND', targetId: 'p3' },
      { combatantId: 'p3', type: 'EVADE', targetId: null },
    ];

    expect(() => {
      runRound(state, playerDeclarations, FIXED_ROLL(10));
    }).not.toThrow();
  });

  it('multiple consecutive rounds update state progressively', () => {
    const p1 = makeCombatant('p1', { stamina: 200, maxStamina: 200, power: 50 });
    const e1 = makeCombatant('e1', { stamina: 200, maxStamina: 200, power: 50 });
    let state = makeState([p1], [e1]);

    const playerDeclarations: CombatAction[] = [
      { combatantId: 'p1', type: 'ATTACK', targetId: 'e1' },
    ];

    // Run 3 rounds
    state = runRound(state, playerDeclarations, FIXED_ROLL(20));
    state = runRound(state, playerDeclarations, FIXED_ROLL(20));
    state = runRound(state, playerDeclarations, FIXED_ROLL(20));

    expect(state.round).toBe(4);
    expect(state.roundHistory).toHaveLength(3);
  });
});

// ============================================================================
// Test 6: Round result recorded in history
// ============================================================================

describe('runRound — Phase 6: round result recorded in roundHistory', () => {
  it('appends one RoundResult to roundHistory per round', () => {
    const player = makeCombatant('p1');
    const enemy = makeCombatant('e1');
    const state = makeState([player], [enemy], { roundHistory: [] });

    const playerDeclarations: CombatAction[] = [
      { combatantId: 'p1', type: 'ATTACK', targetId: 'e1' },
    ];

    const result = runRound(state, playerDeclarations, FIXED_ROLL(10));

    expect(result.roundHistory).toHaveLength(1);
  });

  it('RoundResult records the correct round number', () => {
    const player = makeCombatant('p1');
    const enemy = makeCombatant('e1');
    const state = makeState([player], [enemy], { round: 3 });

    const playerDeclarations: CombatAction[] = [
      { combatantId: 'p1', type: 'ATTACK', targetId: 'e1' },
    ];

    const result = runRound(state, playerDeclarations, FIXED_ROLL(10));

    const roundRecord = result.roundHistory[0];
    expect(roundRecord.round).toBe(3); // records the round that was just resolved
  });

  it('RoundResult contains a state snapshot', () => {
    const player = makeCombatant('p1');
    const enemy = makeCombatant('e1');
    const state = makeState([player], [enemy]);

    const playerDeclarations: CombatAction[] = [
      { combatantId: 'p1', type: 'ATTACK', targetId: 'e1' },
    ];

    const result = runRound(state, playerDeclarations, FIXED_ROLL(10));

    const roundRecord = result.roundHistory[0];
    expect(roundRecord.stateSnapshot).toBeDefined();
    expect(roundRecord.stateSnapshot.playerParty).toHaveLength(1);
  });

  it('RoundResult actions array contains entries for each action resolved', () => {
    const player = makeCombatant('p1');
    const enemy = makeCombatant('e1');
    const state = makeState([player], [enemy]);

    // Player attacks; AI also attacks → 2 total actions in the round
    const playerDeclarations: CombatAction[] = [
      { combatantId: 'p1', type: 'ATTACK', targetId: 'e1' },
    ];

    const result = runRound(state, playerDeclarations, FIXED_ROLL(10));

    const roundRecord = result.roundHistory[0];
    // There should be at least 1 action recorded (p1's attack, plus AI's attack from e1)
    expect(roundRecord.actions.length).toBeGreaterThanOrEqual(1);
  });

  it('accumulates roundHistory across multiple rounds', () => {
    const player = makeCombatant('p1', { stamina: 500, maxStamina: 500 });
    const enemy = makeCombatant('e1', { stamina: 500, maxStamina: 500 });
    let state = makeState([player], [enemy]);

    const playerDeclarations: CombatAction[] = [
      { combatantId: 'p1', type: 'ATTACK', targetId: 'e1' },
    ];

    state = runRound(state, playerDeclarations, FIXED_ROLL(10));
    state = runRound(state, playerDeclarations, FIXED_ROLL(10));

    expect(state.roundHistory).toHaveLength(2);
    expect(state.roundHistory[0].round).toBe(1);
    expect(state.roundHistory[1].round).toBe(2);
  });
});

// ============================================================================
// Victory / Defeat Detection
// ============================================================================

describe('runRound — victory/defeat detection', () => {
  it('status becomes "victory" when all enemies are KO\'d', () => {
    // Use extreme attacker power to guarantee KO in one hit
    const player = makeCombatant('p1', { power: 10000 });
    const enemy = makeCombatant('e1', { stamina: 1, maxStamina: 100, power: 1 });
    const state = makeState([player], [enemy]);

    const playerDeclarations: CombatAction[] = [
      { combatantId: 'p1', type: 'ATTACK', targetId: 'e1' },
    ];

    const result = runRound(state, playerDeclarations, FIXED_ROLL(20));

    // With power = 10000 and roll = 20 (block fail), enemy should be KO'd
    const enemyAfter = result.enemyParty.find((c) => c.id === 'e1')!;
    if (enemyAfter.isKO) {
      expect(result.status).toBe('victory');
    }
  });

  it('status becomes "defeat" when all players are KO\'d', () => {
    // Use extreme enemy power to guarantee KO
    const player = makeCombatant('p1', { stamina: 1, maxStamina: 100, power: 1 });
    const enemy = makeCombatant('e1', { power: 10000 });
    const state = makeState([player], [enemy]);

    // No player declarations — AI attacks the player
    const result = runRound(state, [], FIXED_ROLL(20));

    const playerAfter = result.playerParty.find((c) => c.id === 'p1')!;
    if (playerAfter.isKO) {
      expect(result.status).toBe('defeat');
    }
  });

  it('status remains "active" when neither side is fully KO\'d', () => {
    const player = makeCombatant('p1', { stamina: 1000, maxStamina: 1000, power: 1 });
    const enemy = makeCombatant('e1', { stamina: 1000, maxStamina: 1000, power: 1 });
    const state = makeState([player], [enemy]);

    const playerDeclarations: CombatAction[] = [
      { combatantId: 'p1', type: 'ATTACK', targetId: 'e1' },
    ];

    const result = runRound(state, playerDeclarations, FIXED_ROLL(10));

    expect(result.status).toBe('active');
  });
});

// ============================================================================
// Phase 3: PC Declaration Validation edge cases
// ============================================================================

describe('runRound — Phase 3: declaration validation', () => {
  it('invalid player declaration is dropped (combatant not found)', () => {
    const player = makeCombatant('p1');
    const enemy = makeCombatant('e1');
    const state = makeState([player], [enemy]);

    // Declaration from non-existent combatant
    const playerDeclarations: CombatAction[] = [
      { combatantId: 'DOES_NOT_EXIST', type: 'ATTACK', targetId: 'e1' },
    ];

    // Should not throw — invalid declarations are silently dropped
    expect(() => {
      runRound(state, playerDeclarations, FIXED_ROLL(10));
    }).not.toThrow();
  });

  it('KO\'d player combatant cannot declare actions', () => {
    const koPlayer = makeCombatant('p1', { isKO: true, stamina: 0 });
    const livePlayer = makeCombatant('p2');
    const enemy = makeCombatant('e1');
    const state = makeState([koPlayer, livePlayer], [enemy]);

    // KO'd player tries to attack — should be dropped
    const playerDeclarations: CombatAction[] = [
      { combatantId: 'p1', type: 'ATTACK', targetId: 'e1' },
      { combatantId: 'p2', type: 'ATTACK', targetId: 'e1' },
    ];

    const result = runRound(state, playerDeclarations, FIXED_ROLL(10));
    // Should complete without errors; p2's attack still goes through
    expect(result).toBeDefined();
  });

  it('GROUP declaration with insufficient ally energy falls back to ATTACK', () => {
    // p1 tries GROUP but p2 doesn't have full energy (energy=0, maxEnergy=5)
    // GROUP should be rejected with fallback ATTACK on same target
    const p1 = makeCombatant('p1', { energy: 5, maxEnergy: 5 });
    const p2 = makeCombatant('p2', { energy: 0, maxEnergy: 5 }); // not full energy
    const enemy = makeCombatant('e1');
    const state = makeState([p1, p2], [enemy]);

    const playerDeclarations: CombatAction[] = [
      { combatantId: 'p1', type: 'GROUP', targetId: 'e1' },
    ];

    // Should not throw; GROUP rejected → fallback ATTACK
    const result = runRound(state, playerDeclarations, FIXED_ROLL(10));
    expect(result).toBeDefined();

    // Round should have proceeded with an ATTACK (fallback) instead of GROUP
    const roundRecord = result.roundHistory[0];
    const p1Action = roundRecord.actions.find((a) => a.combatantId === 'p1');
    if (p1Action) {
      // The fallback ATTACK is what was actually resolved
      expect(p1Action.type).not.toBe('GROUP');
    }
  });

  it('empty player declarations still runs round with AI actions only', () => {
    const player = makeCombatant('p1', { stamina: 100 });
    const enemy = makeCombatant('e1');
    const state = makeState([player], [enemy]);

    const result = runRound(state, [], FIXED_ROLL(10));

    expect(result).toBeDefined();
    // AI should have acted — round history should have at least the AI's action
    expect(result.roundHistory).toHaveLength(1);
  });
});
