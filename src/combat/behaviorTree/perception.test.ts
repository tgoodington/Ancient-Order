import { describe, it, expect } from 'vitest';
import { buildPerception } from './perception.js';
import type { Combatant, CombatState } from '../../types/combat.js';

/** Minimal combatant factory — fills in sensible defaults */
function makeCombatant(overrides: Partial<Combatant> & { id: string }): Combatant {
  return {
    id: overrides.id,
    name: overrides.name ?? overrides.id,
    archetype: overrides.archetype ?? 'test_archetype',
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

describe('buildPerception', () => {
  describe('self fields', () => {
    it('computes selfStaminaPct correctly', () => {
      const self = makeCombatant({ id: 'enemy_1', stamina: 50, maxStamina: 100 });
      const ally = makeCombatant({ id: 'player_1' });
      const state = makeState([ally], [self]);
      const p = buildPerception(self, state);
      expect(p.selfStaminaPct).toBeCloseTo(0.5);
    });

    it('sets selfId, selfEnergy, selfRank, selfPath from combatant', () => {
      const self = makeCombatant({
        id: 'enemy_x', rank: 4.5, energy: 3, elementalPath: 'Shadow', stamina: 80, maxStamina: 80,
      });
      const p = buildPerception(self, makeState([makeCombatant({ id: 'player_1' })], [self]));
      expect(p.selfId).toBe('enemy_x');
      expect(p.selfRank).toBe(4.5);
      expect(p.selfEnergy).toBe(3);
      expect(p.selfPath).toBe('Shadow');
    });

    it('handles maxStamina=0 without dividing by zero (selfStaminaPct=0)', () => {
      const self = makeCombatant({ id: 'e1', stamina: 0, maxStamina: 0 });
      const p = buildPerception(self, makeState([makeCombatant({ id: 'p1' })], [self]));
      expect(p.selfStaminaPct).toBe(0);
    });
  });

  describe('ally perception', () => {
    it('excludes self from allies list', () => {
      const self = makeCombatant({ id: 'enemy_1' });
      const ally = makeCombatant({ id: 'enemy_2' });
      const p = buildPerception(self, makeState([makeCombatant({ id: 'player_1' })], [self, ally]));
      expect(p.allies.every((a) => a.id !== 'enemy_1')).toBe(true);
      expect(p.allies.some((a) => a.id === 'enemy_2')).toBe(true);
    });

    it('sorts allies by staminaPct ascending (weakest first)', () => {
      const self = makeCombatant({ id: 'e1' });
      const ally1 = makeCombatant({ id: 'e2', stamina: 80, maxStamina: 100 });
      const ally2 = makeCombatant({ id: 'e3', stamina: 30, maxStamina: 100 });
      const p = buildPerception(self, makeState([makeCombatant({ id: 'p1' })], [self, ally1, ally2]));
      expect(p.allies[0].id).toBe('e3');
      expect(p.allies[1].id).toBe('e2');
    });

    it('computes lowestAllyStaminaPct correctly', () => {
      const self = makeCombatant({ id: 'e1' });
      const ally1 = makeCombatant({ id: 'e2', stamina: 90, maxStamina: 100 });
      const ally2 = makeCombatant({ id: 'e3', stamina: 20, maxStamina: 100 });
      const p = buildPerception(self, makeState([makeCombatant({ id: 'p1' })], [self, ally1, ally2]));
      expect(p.lowestAllyStaminaPct).toBeCloseTo(0.2);
    });

    it('lowestAllyStaminaPct is 1.0 when no allies exist', () => {
      const self = makeCombatant({ id: 'e1' });
      const p = buildPerception(self, makeState([makeCombatant({ id: 'p1' })], [self]));
      expect(p.lowestAllyStaminaPct).toBe(1.0);
      expect(p.allyCount).toBe(0);
    });

    it('excludes KO allies from allyCount and lowestAllyStaminaPct', () => {
      const self = makeCombatant({ id: 'e1' });
      const allyKO = makeCombatant({ id: 'e2', stamina: 0, maxStamina: 100, isKO: true });
      const allyAlive = makeCombatant({ id: 'e3', stamina: 60, maxStamina: 100 });
      const p = buildPerception(self, makeState([makeCombatant({ id: 'p1' })], [self, allyKO, allyAlive]));
      expect(p.allyCount).toBe(1);
      expect(p.lowestAllyStaminaPct).toBeCloseTo(0.6);
    });
  });

  describe('team average stamina', () => {
    it('includes self in teamAvgStaminaPct', () => {
      // self = 50%, ally = 100% → avg = 75%
      const self = makeCombatant({ id: 'e1', stamina: 50, maxStamina: 100 });
      const ally = makeCombatant({ id: 'e2', stamina: 100, maxStamina: 100 });
      const p = buildPerception(self, makeState([makeCombatant({ id: 'p1' })], [self, ally]));
      expect(p.teamAvgStaminaPct).toBeCloseTo(0.75);
    });
  });

  describe('enemy perception', () => {
    it('sorts enemies by staminaPct ascending', () => {
      const self = makeCombatant({ id: 'e1' });
      const enemy1 = makeCombatant({ id: 'p1', stamina: 90, maxStamina: 100 });
      const enemy2 = makeCombatant({ id: 'p2', stamina: 40, maxStamina: 100 });
      const p = buildPerception(self, makeState([enemy1, enemy2], [self]));
      expect(p.enemies[0].id).toBe('p2');
      expect(p.enemies[1].id).toBe('p1');
    });

    it('computes speedDelta correctly', () => {
      const self = makeCombatant({ id: 'e1', speed: 15 });
      const enemy = makeCombatant({ id: 'p1', speed: 10 });
      const p = buildPerception(self, makeState([enemy], [self]));
      // speedDelta = (15 - 10) / 10 = 0.5
      expect(p.enemies[0].speedDelta).toBeCloseTo(0.5);
    });

    it('computes rankDelta correctly', () => {
      const self = makeCombatant({ id: 'e1', rank: 5.0 });
      const enemy = makeCombatant({ id: 'p1', rank: 3.0 });
      const p = buildPerception(self, makeState([enemy], [self]));
      // rankDelta = 5.0 - 3.0 = 2.0
      expect(p.enemies[0].rankDelta).toBeCloseTo(2.0);
    });

    it('computes weakestEnemyStaminaPct', () => {
      const self = makeCombatant({ id: 'e1' });
      const enemy1 = makeCombatant({ id: 'p1', stamina: 80, maxStamina: 100 });
      const enemy2 = makeCombatant({ id: 'p2', stamina: 25, maxStamina: 100 });
      const p = buildPerception(self, makeState([enemy1, enemy2], [self]));
      expect(p.weakestEnemyStaminaPct).toBeCloseTo(0.25);
    });

    it('weakestEnemyStaminaPct is 1.0 when all enemies are KO', () => {
      const self = makeCombatant({ id: 'e1' });
      const enemy = makeCombatant({ id: 'p1', stamina: 0, isKO: true });
      const p = buildPerception(self, makeState([enemy], [self]));
      expect(p.weakestEnemyStaminaPct).toBe(1.0);
      expect(p.enemyCount).toBe(0);
    });

    it('handles negative speedDelta when self is slower', () => {
      const self = makeCombatant({ id: 'e1', speed: 5 });
      const enemy = makeCombatant({ id: 'p1', speed: 15 });
      const p = buildPerception(self, makeState([enemy], [self]));
      // speedDelta = (5 - 15) / 15 ≈ -0.667
      expect(p.enemies[0].speedDelta).toBeCloseTo(-0.667);
    });
  });

  describe('party identification', () => {
    it('identifies enemy party combatant correctly — enemies become foes', () => {
      const self = makeCombatant({ id: 'enemy_npc' });
      const player = makeCombatant({ id: 'player_hero' });
      const p = buildPerception(self, makeState([player], [self]));
      expect(p.enemies.some((e) => e.id === 'player_hero')).toBe(true);
      expect(p.allies.some((a) => a.id === 'player_hero')).toBe(false);
    });

    it('identifies player party combatant correctly — enemies become foes', () => {
      const self = makeCombatant({ id: 'player_hero' });
      const enemy = makeCombatant({ id: 'enemy_npc' });
      const p = buildPerception(self, makeState([self], [enemy]));
      expect(p.enemies.some((e) => e.id === 'enemy_npc')).toBe(true);
    });

    it('includes round from state', () => {
      const self = makeCombatant({ id: 'e1' });
      const p = buildPerception(self, makeState([makeCombatant({ id: 'p1' })], [self], 4));
      expect(p.round).toBe(4);
    });
  });
});
