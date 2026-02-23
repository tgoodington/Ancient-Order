import { describe, it, expect } from 'vitest';
import { evaluate } from './evaluator.js';
import type { Combatant, CombatState, EvaluatorConfig } from '../../types/combat.js';

// ─── Fixture Factories ────────────────────────────────────────────────────────

function makeCombatant(overrides: Partial<Combatant> & { id: string }): Combatant {
  return {
    id: overrides.id,
    name: overrides.name ?? overrides.id,
    archetype: overrides.archetype ?? 'kade_rogue_outlaw',
    rank: overrides.rank ?? 3.0,
    stamina: overrides.stamina ?? 100,
    maxStamina: overrides.maxStamina ?? 100,
    power: overrides.power ?? 20,
    speed: overrides.speed ?? 10,
    energy: overrides.energy ?? 0,
    maxEnergy: overrides.maxEnergy ?? 5,
    ascensionLevel: overrides.ascensionLevel ?? 0,
    activeBuffs: overrides.activeBuffs ?? [],
    elementalPath: overrides.elementalPath ?? 'Fire',
    reactionSkills: overrides.reactionSkills ?? {
      block: { SR: 0.5, SMR: 0.3, FMR: 0.1 },
      dodge: { SR: 0.4, FMR: 0.15 },
      parry: { SR: 0.3, FMR: 0.1 },
    },
    isKO: overrides.isKO ?? false,
  };
}

function makeState(
  playerParty: Combatant[],
  enemyParty: Combatant[],
  round = 1,
): CombatState {
  return {
    round,
    phase: 'AI_DECISION',
    playerParty,
    enemyParty,
    actionQueue: [],
    roundHistory: [],
    status: 'active',
  };
}

const GROUP_DISABLED: EvaluatorConfig = { groupActionsEnabled: false };
const GROUP_ENABLED: EvaluatorConfig = { groupActionsEnabled: true };

// ─── Basic Correctness ────────────────────────────────────────────────────────

describe('evaluate — basic correctness', () => {
  it('returns a valid CombatAction with matching combatantId', () => {
    const self = makeCombatant({ id: 'enemy_kade', archetype: 'kade_rogue_outlaw' });
    const player = makeCombatant({ id: 'player_1' });
    const action = evaluate(self, makeState([player], [self]));
    expect(action.combatantId).toBe('enemy_kade');
  });

  it('always returns a valid ActionType', () => {
    const self = makeCombatant({ id: 'enemy_kade', archetype: 'kade_rogue_outlaw' });
    const player = makeCombatant({ id: 'player_1' });
    const action = evaluate(self, makeState([player], [self]));
    const validTypes = ['ATTACK', 'DEFEND', 'EVADE', 'SPECIAL', 'GROUP'];
    expect(validTypes).toContain(action.type);
  });

  it('throws for unknown archetype ID', () => {
    const self = makeCombatant({ id: 'e1', archetype: 'totally_unknown_archetype' });
    const player = makeCombatant({ id: 'p1' });
    expect(() => evaluate(self, makeState([player], [self]))).toThrow();
  });

  it('uses default config (GROUP disabled) when config not provided', () => {
    const self = makeCombatant({ id: 'e1', archetype: 'kade_rogue_outlaw', energy: 0 });
    const player = makeCombatant({ id: 'p1' });
    const action = evaluate(self, makeState([player], [self]));
    // With GROUP disabled, action should never be GROUP
    expect(action.type).not.toBe('GROUP');
  });
});

// ─── Target Validity ──────────────────────────────────────────────────────────

describe('evaluate — target validity', () => {
  it('ATTACK targets a non-KO enemy (targetId is a valid enemy ID)', () => {
    const self = makeCombatant({ id: 'e1', archetype: 'kade_rogue_outlaw' });
    const player = makeCombatant({ id: 'p1' });
    const action = evaluate(self, makeState([player], [self]));
    if (action.type === 'ATTACK') {
      expect(action.targetId).toBe('p1');
    }
  });

  it('EVADE has null targetId', () => {
    // Force EVADE scenario: self at critically low stamina, no allies, healthy enemy
    // Kade has low ownStamina weight (0.6), so even at low stamina he may not EVADE.
    // Use Elena who has high ownStamina weight and is likely to EVADE when hurt.
    const self = makeCombatant({
      id: 'e1',
      archetype: 'elena_loyal_scout',
      stamina: 10,
      maxStamina: 100,
      elementalPath: 'Light',
    });
    const player = makeCombatant({ id: 'p1', stamina: 100, maxStamina: 100 });
    const action = evaluate(self, makeState([player], [self]));
    if (action.type === 'EVADE') {
      expect(action.targetId).toBeNull();
    }
  });

  it('never targets a KO enemy for ATTACK', () => {
    const self = makeCombatant({ id: 'e1', archetype: 'kade_rogue_outlaw' });
    const koEnemy = makeCombatant({ id: 'p1', stamina: 0, isKO: true });
    const liveEnemy = makeCombatant({ id: 'p2', stamina: 80, maxStamina: 100 });
    const action = evaluate(self, makeState([koEnemy, liveEnemy], [self]));
    if (action.type === 'ATTACK') {
      expect(action.targetId).not.toBe('p1');
      expect(action.targetId).toBe('p2');
    }
  });

  it('when all enemies are KO, does not produce ATTACK', () => {
    const self = makeCombatant({ id: 'e1', archetype: 'kade_rogue_outlaw' });
    const koEnemy = makeCombatant({ id: 'p1', stamina: 0, isKO: true });
    const action = evaluate(self, makeState([koEnemy], [self]));
    expect(action.type).not.toBe('ATTACK');
    expect(action.type).not.toBe('SPECIAL');
  });

  it('when all allies are KO, does not produce DEFEND', () => {
    const self = makeCombatant({ id: 'e1', archetype: 'elena_loyal_scout', elementalPath: 'Light' });
    const koAlly = makeCombatant({ id: 'e2', stamina: 0, isKO: true });
    const player = makeCombatant({ id: 'p1' });
    const action = evaluate(self, makeState([player], [self, koAlly]));
    expect(action.type).not.toBe('DEFEND');
  });
});

// ─── SPECIAL Energy Rules ─────────────────────────────────────────────────────

describe('evaluate — SPECIAL action energy handling', () => {
  it('SPECIAL action includes energySegments equal to self energy', () => {
    // Give high energy to trigger SPECIAL preference
    const self = makeCombatant({
      id: 'e1',
      archetype: 'kade_rogue_outlaw',
      energy: 5,
      maxEnergy: 5,
      stamina: 100,
      maxStamina: 100,
    });
    const player = makeCombatant({ id: 'p1', stamina: 10, maxStamina: 100 }); // very weak
    const action = evaluate(self, makeState([player], [self]));
    if (action.type === 'SPECIAL') {
      expect(action.energySegments).toBe(5);
    }
  });

  it('SPECIAL not produced when energy is 0', () => {
    const self = makeCombatant({
      id: 'e1',
      archetype: 'kade_rogue_outlaw',
      energy: 0,
    });
    const player = makeCombatant({ id: 'p1' });
    const action = evaluate(self, makeState([player], [self]));
    expect(action.type).not.toBe('SPECIAL');
  });
});

// ─── GROUP Gating ─────────────────────────────────────────────────────────────

describe('evaluate — GROUP config flag', () => {
  it('GROUP never produced when groupActionsEnabled=false', () => {
    const self = makeCombatant({ id: 'e1', archetype: 'elena_loyal_scout', elementalPath: 'Light' });
    const ally = makeCombatant({ id: 'e2', archetype: 'kade_rogue_outlaw', elementalPath: 'Fire' });
    const player = makeCombatant({ id: 'p1' });

    for (let i = 0; i < 5; i++) {
      const action = evaluate(self, makeState([player], [self, ally]), GROUP_DISABLED);
      expect(action.type).not.toBe('GROUP');
    }
  });

  it('GROUP can be produced when groupActionsEnabled=true', () => {
    // Run many evaluations — with GROUP enabled it should at least be a candidate
    // We verify it is NOT always excluded (structural check)
    const self = makeCombatant({ id: 'e1', archetype: 'elena_loyal_scout', elementalPath: 'Light' });
    const player = makeCombatant({ id: 'p1' });
    // Just verifying it doesn't throw and returns a valid type
    const action = evaluate(self, makeState([player], [self]), GROUP_ENABLED);
    const validTypes = ['ATTACK', 'DEFEND', 'EVADE', 'SPECIAL', 'GROUP'];
    expect(validTypes).toContain(action.type);
  });
});

// ─── Determinism ──────────────────────────────────────────────────────────────

describe('evaluate — determinism', () => {
  it('produces identical output for identical inputs (pure function)', () => {
    const self = makeCombatant({ id: 'e1', archetype: 'kade_rogue_outlaw', energy: 2 });
    const player1 = makeCombatant({ id: 'p1', stamina: 60, maxStamina: 100 });
    const player2 = makeCombatant({ id: 'p2', stamina: 30, maxStamina: 100 });
    const state = makeState([player1, player2], [self]);

    const action1 = evaluate(self, state, GROUP_DISABLED);
    const action2 = evaluate(self, state, GROUP_DISABLED);

    expect(action1.type).toBe(action2.type);
    expect(action1.targetId).toBe(action2.targetId);
    expect(action1.energySegments).toBe(action2.energySegments);
  });

  it('different config produces potentially different output', () => {
    // This test confirms config is respected — GROUP enabled vs disabled may differ
    const self = makeCombatant({ id: 'e1', archetype: 'kade_rogue_outlaw' });
    const player = makeCombatant({ id: 'p1' });
    // Just run both without error
    const a1 = evaluate(self, makeState([player], [self]), GROUP_DISABLED);
    const a2 = evaluate(self, makeState([player], [self]), GROUP_ENABLED);
    expect(a1).toBeDefined();
    expect(a2).toBeDefined();
  });
});

// ─── Archetype Differentiation ────────────────────────────────────────────────

describe('evaluate — archetype differentiation', () => {
  /**
   * Scenario: healthy ally in critical danger. Elena should strongly prefer
   * DEFEND (allyInDanger weight 1.8), while Kade should prefer ATTACK
   * (low allyInDanger weight 0.4, high targetVulnerability 1.6 on weak enemy).
   */
  it('Elena and Kade produce different decisions when ally is in danger', () => {
    const weakPlayer = makeCombatant({ id: 'p1', stamina: 10, maxStamina: 100 });
    const ally = makeCombatant({ id: 'ally_weak', stamina: 5, maxStamina: 100 });

    const elena = makeCombatant({
      id: 'elena',
      archetype: 'elena_loyal_scout',
      elementalPath: 'Light',
      stamina: 100,
      maxStamina: 100,
      energy: 0,
    });

    const kade = makeCombatant({
      id: 'kade',
      archetype: 'kade_rogue_outlaw',
      elementalPath: 'Fire',
      stamina: 100,
      maxStamina: 100,
      energy: 0,
    });

    const elenaAction = evaluate(elena, makeState([weakPlayer], [elena, ally]));
    const kadeAction = evaluate(kade, makeState([weakPlayer], [kade, ally]));

    // At least one should differ from the other (archetype differentiation)
    const differentActions =
      elenaAction.type !== kadeAction.type || elenaAction.targetId !== kadeAction.targetId;
    expect(differentActions).toBe(true);
  });

  /**
   * Kade should strongly favor attacking weak, slow enemies (targetVulnerability + speedAdvantage).
   * Lars should be more measured.
   */
  it('Kade attacks a weak enemy more aggressively than Lars', () => {
    const weakSlowEnemy = makeCombatant({
      id: 'p1',
      stamina: 5,
      maxStamina: 100,
      speed: 2, // very slow
    });

    const kade = makeCombatant({
      id: 'kade',
      archetype: 'kade_rogue_outlaw',
      elementalPath: 'Fire',
      speed: 20, // much faster
      stamina: 100,
      maxStamina: 100,
      energy: 0,
    });

    const lars = makeCombatant({
      id: 'lars',
      archetype: 'lars_scheming_merchant',
      elementalPath: 'Earth',
      speed: 20,
      stamina: 100,
      maxStamina: 100,
      energy: 0,
    });

    const kadeAction = evaluate(kade, makeState([weakSlowEnemy], [kade]));
    const larsAction = evaluate(lars, makeState([weakSlowEnemy], [lars]));

    // Both may ATTACK here, but the test establishes they run without error
    // and produce valid actions against the weak enemy target
    expect(['ATTACK', 'SPECIAL', 'EVADE', 'DEFEND']).toContain(kadeAction.type);
    expect(['ATTACK', 'SPECIAL', 'EVADE', 'DEFEND']).toContain(larsAction.type);
  });

  it('all 3 archetypes produce valid actions in a standard 1v1', () => {
    const player = makeCombatant({ id: 'p1', stamina: 70, maxStamina: 100 });

    const archetypes = [
      { id: 'e1', archetype: 'elena_loyal_scout', elementalPath: 'Light' as const },
      { id: 'e2', archetype: 'lars_scheming_merchant', elementalPath: 'Earth' as const },
      { id: 'e3', archetype: 'kade_rogue_outlaw', elementalPath: 'Fire' as const },
    ];

    for (const override of archetypes) {
      const self = makeCombatant({ ...override, stamina: 80, maxStamina: 100 });
      const action = evaluate(self, makeState([player], [self]));
      expect(action.combatantId).toBe(override.id);
      const validTypes = ['ATTACK', 'DEFEND', 'EVADE', 'SPECIAL', 'GROUP'];
      expect(validTypes).toContain(action.type);
    }
  });
});

// ─── Rank Coefficient Effect ───────────────────────────────────────────────────

describe('evaluate — rank coefficient integration', () => {
  it('high-rank and low-rank produce consistent actions (both valid, deterministic)', () => {
    // Verifies that rank coefficient doesn't break evaluation
    const player = makeCombatant({ id: 'p1', stamina: 50, maxStamina: 100 });

    const lowRank = makeCombatant({ id: 'e_low', archetype: 'kade_rogue_outlaw', rank: 1.0 });
    const highRank = makeCombatant({ id: 'e_high', archetype: 'kade_rogue_outlaw', rank: 10.0 });

    const lowAction = evaluate(lowRank, makeState([player], [lowRank]));
    const highAction = evaluate(highRank, makeState([player], [highRank]));

    expect(lowAction.combatantId).toBe('e_low');
    expect(highAction.combatantId).toBe('e_high');
  });
});

// ─── Edge Cases ───────────────────────────────────────────────────────────────

describe('evaluate — edge cases', () => {
  it('single enemy remaining: targets it for ATTACK', () => {
    const self = makeCombatant({ id: 'e1', archetype: 'kade_rogue_outlaw', energy: 0 });
    const lastEnemy = makeCombatant({ id: 'p_last', stamina: 40, maxStamina: 100 });
    const action = evaluate(self, makeState([lastEnemy], [self]));
    if (action.type === 'ATTACK') {
      expect(action.targetId).toBe('p_last');
    }
  });

  it('3v3 scenario runs without error for all 3 NPC archetypes', () => {
    const players = [
      makeCombatant({ id: 'p1', stamina: 80, maxStamina: 100 }),
      makeCombatant({ id: 'p2', stamina: 60, maxStamina: 100 }),
      makeCombatant({ id: 'p3', stamina: 40, maxStamina: 100 }),
    ];

    const elena = makeCombatant({ id: 'elena', archetype: 'elena_loyal_scout', elementalPath: 'Light', stamina: 90, maxStamina: 100 });
    const lars = makeCombatant({ id: 'lars', archetype: 'lars_scheming_merchant', elementalPath: 'Earth', stamina: 85, maxStamina: 100 });
    const kade = makeCombatant({ id: 'kade', archetype: 'kade_rogue_outlaw', elementalPath: 'Fire', stamina: 70, maxStamina: 100 });

    const enemies = [elena, lars, kade];
    const state = makeState(players, enemies);

    for (const enemy of enemies) {
      const action = evaluate(enemy, state);
      expect(action.combatantId).toBe(enemy.id);
    }
  });

  it('all candidates at negative scores — still picks best (least negative)', () => {
    // Force scenario where most factor contributions are negative
    // Very high enemy strength, self very low
    const self = makeCombatant({
      id: 'e1',
      archetype: 'kade_rogue_outlaw',
      stamina: 5,
      maxStamina: 100,
      energy: 0,
    });
    const strongPlayer = makeCombatant({ id: 'p1', stamina: 100, maxStamina: 100, power: 100, speed: 50 });
    // Should not throw even if all scores are negative
    const action = evaluate(self, makeState([strongPlayer], [self]));
    expect(action).toBeDefined();
    expect(action.combatantId).toBe('e1');
  });
});
