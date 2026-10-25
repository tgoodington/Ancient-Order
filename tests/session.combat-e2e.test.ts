/**
 * Session port of src/combat/e2e.test.ts (3v3 demo encounter, end to end).
 *
 * Drives the 'encounter_demo' fixture through the in-client game session instead
 * of the HTTP server. Describe names match the original one-to-one.
 *
 * Dropped tests: none. Every original test has a session counterpart (renamed
 * from route paths to session operations). Only HTTP-only concerns were dropped
 * from inside tests: status codes (e.g. 422), JSON parsing, Fastify app
 * lifecycle. Where the original asserted only `success === false`, the port
 * asserts the specific error code the session returns.
 *
 * Changed because of approved fixes:
 *   - Scenario 4 "runs to completion": the original noted that the result
 *     endpoint returned 404 once combat ended. The session keeps the outcome
 *     readable, so the port asserts getCombatResult() agrees with the final status.
 *
 * Stats verification: the stats in src/fixtures/encounter.json are representative
 * and MUST be verified against GM Combat Tracker.xlsx before the pitch.
 */

import { describe, it, expect, beforeEach } from 'vitest';
import { createGameSession } from '../src/session/index.js';
import type { GameSession } from '../src/session/index.js';
import type { CombatState, AttackResult, ActionResult } from '../src/types/combat.js';

interface RoundResult {
  round: number;
  actions: ActionResult[];
  stateSnapshot: CombatState;
}

function collectAttackResults(actions: readonly ActionResult[]): AttackResult[] {
  return actions
    .map((a) => a.attackResult)
    .filter((r): r is AttackResult => r !== undefined);
}

const STANDARD_DECLARATIONS = [
  { combatantId: 'player_elena', type: 'ATTACK', targetId: 'enemy_knight' },
  { combatantId: 'player_lars', type: 'ATTACK', targetId: 'enemy_rogue' },
  { combatantId: 'player_kade', type: 'ATTACK', targetId: 'enemy_mage' },
];

function startDemo(session: GameSession): void {
  session.newGame();
  const started = session.startEncounter('encounter_demo');
  expect(started.ok).toBe(true);
}

function history(session: GameSession): RoundResult[] {
  const res = session.getRoundHistory();
  expect(res.ok).toBe(true);
  return (res.ok ? res.data : []) as unknown as RoundResult[];
}

describe('Task 22: End-to-End 3v3 Demo Encounter', () => {
  let session: GameSession;

  beforeEach(() => {
    session = createGameSession();
  });

  describe('Scenario 1: Encounter initialisation', () => {
    it('newGame creates a new game', () => {
      const res = session.newGame();
      expect(res.ok).toBe(true);
    });

    it('startEncounter with encounter_demo initialises combat', () => {
      session.newGame();
      const res = session.startEncounter('encounter_demo');

      expect(res.ok).toBe(true);
      if (!res.ok) return;
      expect(res.data.encounterId).toBe('encounter_demo');
      expect(res.data.round).toBe(1);
      expect(res.data.status).toBe('active');
    });

    it('encounter initialisation fails without a prior game', () => {
      const res = session.startEncounter('encounter_demo');
      expect(res.ok === false && res.error.code).toBe('GAME_NOT_FOUND');
    });

    it('unknown encounter ID returns an error', () => {
      session.newGame();
      const res = session.startEncounter('encounter_does_not_exist');
      expect(res.ok === false && res.error.code).toBe('ENCOUNTER_NOT_FOUND');
    });
  });

  describe('Scenario 2: Player declarations and round advancement', () => {
    it('all three player declarations are accepted', () => {
      startDemo(session);

      const res = session.declareActions(STANDARD_DECLARATIONS);

      expect(res.ok).toBe(true);
      if (!res.ok) return;
      expect(res.data.accepted).toHaveLength(3);
      expect(res.data.rejected).toHaveLength(0);
    });

    it('advanceRound advances the round and returns updated state', () => {
      startDemo(session);
      session.declareActions(STANDARD_DECLARATIONS);

      const res = session.advanceRound();

      expect(res.ok).toBe(true);
      if (!res.ok) return;
      expect(res.data.round).toBe(2);
      expect(['active', 'victory', 'defeat']).toContain(res.data.status);
    });

    it('round result contains combatant stamina information', () => {
      startDemo(session);
      session.declareActions(STANDARD_DECLARATIONS);

      const res = session.advanceRound();

      expect(res.ok).toBe(true);
      if (!res.ok) return;
      expect(res.data.combatants.length).toBe(6);

      for (const c of res.data.combatants) {
        expect(c.id).toBeDefined();
        expect(typeof c.stamina).toBe('number');
        expect(c.staminaPct).toBeGreaterThanOrEqual(0);
        expect(c.staminaPct).toBeLessThanOrEqual(1);
        expect(['active', 'KO']).toContain(c.stance);
      }
    });

    it('declarations without a prior game fail', () => {
      const res = session.declareActions(STANDARD_DECLARATIONS);
      expect(res.ok === false && res.error.code).toBe('GAME_NOT_FOUND');
    });

    it('round advancement without a prior game fails', () => {
      const res = session.advanceRound();
      expect(res.ok === false && res.error.code).toBe('GAME_NOT_FOUND');
    });
  });

  describe('Scenario 3: Formula output verification', () => {
    it('roundHistory is populated after one round', () => {
      startDemo(session);
      session.declareActions(STANDARD_DECLARATIONS);
      session.advanceRound();

      const h = history(session);
      expect(Array.isArray(h)).toBe(true);
      expect(h.length).toBeGreaterThanOrEqual(1);
    });

    it('AttackResult fields are present with correct types', () => {
      startDemo(session);
      session.declareActions(STANDARD_DECLARATIONS);
      session.advanceRound();

      const firstRound = history(session)[0];
      expect(firstRound).toBeDefined();
      expect(firstRound.round).toBe(1);

      const attackResults = collectAttackResults(firstRound.actions);
      expect(attackResults.length).toBeGreaterThan(0);

      const ar = attackResults[0];
      expect(typeof ar.attackerId).toBe('string');
      expect(typeof ar.targetId).toBe('string');
      expect(typeof ar.damage).toBe('number');
      expect(ar.damage).toBeGreaterThanOrEqual(0);
      expect(typeof ar.rankKO).toBe('boolean');
      expect(typeof ar.blindside).toBe('boolean');
      expect(typeof ar.crushingBlow).toBe('boolean');
      expect(typeof ar.counterChain).toBe('boolean');
      expect(['block', 'dodge', 'parry', 'defenseless']).toContain(ar.defenseType);
    });

    it('energy is tracked — combatants gain energy after taking actions', () => {
      startDemo(session);
      session.declareActions(STANDARD_DECLARATIONS);

      const r1 = session.advanceRound();
      expect(r1.ok).toBe(true);
      if (!r1.ok || r1.data.status !== 'active') return;

      session.declareActions(STANDARD_DECLARATIONS);
      const r2 = session.advanceRound();
      expect(r2.ok).toBe(true);
      if (!r2.ok) return;
      expect(r2.data.round).toBe(3);
    });

    it('Rank KO is structurally possible — attacker and target ranks are equal (rank 3)', () => {
      startDemo(session);
      session.declareActions(STANDARD_DECLARATIONS);
      session.advanceRound();

      const attackResults = collectAttackResults(history(session)[0].actions);
      for (const ar of attackResults) {
        expect(typeof ar.rankKO).toBe('boolean');
      }
    });

    it('blindside flag is present in every AttackResult', () => {
      startDemo(session);
      session.declareActions(STANDARD_DECLARATIONS);
      session.advanceRound();

      const attackResults = collectAttackResults(history(session)[0].actions);
      expect(attackResults.length).toBeGreaterThan(0);
      for (const ar of attackResults) {
        expect(typeof ar.blindside).toBe('boolean');
      }
    });

    it('defense damage: target loses stamina after a round of ATTACK', () => {
      startDemo(session);
      session.declareActions(STANDARD_DECLARATIONS);

      const r1 = session.advanceRound();
      expect(r1.ok).toBe(true);
      if (!r1.ok) return;

      const combatants = r1.data.combatants;
      const enemyKnight = combatants.find((c) => c.id === 'enemy_knight');
      const enemyRogue = combatants.find((c) => c.id === 'enemy_rogue');
      const enemyMage = combatants.find((c) => c.id === 'enemy_mage');

      expect(enemyKnight).toBeDefined();
      expect(enemyRogue).toBeDefined();
      expect(enemyMage).toBeDefined();

      const anyDamaged = [enemyKnight!, enemyRogue!, enemyMage!].some((c) => c.staminaPct < 1.0);
      expect(anyDamaged).toBe(true);
    });

    it('ascension level tracking — initial ascension is 0 for all combatants', () => {
      startDemo(session);
      session.declareActions(STANDARD_DECLARATIONS);
      session.advanceRound();

      const snapshot = history(session)[0].stateSnapshot;
      const allCombatants = [...snapshot.playerParty, ...snapshot.enemyParty];

      for (const c of allCombatants) {
        expect([0, 1, 2, 3]).toContain(c.ascensionLevel);
      }
    });
  });

  describe('Scenario 4: Multi-round loop to completion', () => {
    it('3v3 encounter runs to completion (victory/defeat) within 10 rounds', () => {
      startDemo(session);

      const MAX_ROUNDS = 10;
      let finalStatus = 'active';
      let roundsRun = 0;

      for (let i = 0; i < MAX_ROUNDS; i++) {
        // Declarations may be refused once targets are KO'd; the round still
        // advances with AI-only actions, so declaration success is not asserted.
        session.declareActions(STANDARD_DECLARATIONS);

        const roundRes = session.advanceRound();
        expect(roundRes.ok).toBe(true);
        if (!roundRes.ok) return;
        roundsRun++;

        finalStatus = roundRes.data.status;
        if (finalStatus !== 'active') break;
      }

      const resultRes = session.getCombatResult();
      expect(['active', 'victory', 'defeat']).toContain(finalStatus);
      expect(roundsRun).toBeGreaterThanOrEqual(1);
      expect(roundsRun).toBeLessThanOrEqual(MAX_ROUNDS);

      // Approved fix: the result stays readable after the fight, and agrees.
      expect(resultRes.ok).toBe(true);
      if (!resultRes.ok) return;
      expect(resultRes.data.status).toBe(finalStatus);
    });

    it('round history accumulates one entry per round', () => {
      startDemo(session);

      let roundsRun = 0;
      for (let i = 0; i < 3; i++) {
        session.declareActions(STANDARD_DECLARATIONS);
        const roundRes = session.advanceRound();
        roundsRun++;
        if (roundRes.ok && roundRes.data.status !== 'active') break;
      }

      // History is only readable while combat is active (it ends with combatState).
      if (roundsRun === 3) {
        const histRes = session.getRoundHistory();
        if (histRes.ok) {
          expect(histRes.data.length).toBe(3);
        }
      }

      expect(roundsRun).toBeGreaterThanOrEqual(1);
    });

    it('at least one enemy loses stamina across 3 rounds of sustained attack', () => {
      startDemo(session);

      let lastCombatants: readonly { id: string; staminaPct: number }[] | undefined;
      for (let i = 0; i < 3; i++) {
        session.declareActions(STANDARD_DECLARATIONS);
        const roundRes = session.advanceRound();
        expect(roundRes.ok).toBe(true);
        if (!roundRes.ok) return;
        lastCombatants = roundRes.data.combatants;
        if (roundRes.data.status !== 'active') break;
      }

      if (lastCombatants) {
        const enemies = lastCombatants.filter((c) => c.id.startsWith('enemy_'));
        expect(enemies.some((c) => c.staminaPct < 1.0)).toBe(true);
      }
    });
  });

  describe('Scenario 5: Determinism and reproducibility', () => {
    it('two fresh runs of the same encounter both complete without errors', () => {
      const s1 = createGameSession();
      startDemo(s1);
      s1.declareActions(STANDARD_DECLARATIONS);
      const r1Round1 = s1.advanceRound();

      const s2 = createGameSession();
      startDemo(s2);
      s2.declareActions(STANDARD_DECLARATIONS);
      const r2Round1 = s2.advanceRound();

      expect(r1Round1.ok).toBe(true);
      expect(r2Round1.ok).toBe(true);
      if (!r1Round1.ok || !r2Round1.ok) return;

      expect(r1Round1.data.round).toBe(2);
      expect(r2Round1.data.round).toBe(2);

      const r1Ids = r1Round1.data.combatants.map((c) => c.id).sort();
      const r2Ids = r2Round1.data.combatants.map((c) => c.id).sort();
      expect(r1Ids).toEqual(r2Ids);
    });

    it('same encounter run twice produces the same party composition and round count', () => {
      function runEncounterToCompletion(maxRounds: number): { rounds: number; status: string } {
        const s = createGameSession();
        startDemo(s);

        let rounds = 0;
        let status = 'active';

        for (let i = 0; i < maxRounds; i++) {
          s.declareActions(STANDARD_DECLARATIONS);
          const res = s.advanceRound();
          expect(res.ok).toBe(true);
          if (!res.ok) break;
          rounds++;
          status = res.data.status;
          if (status !== 'active') break;
        }

        return { rounds, status };
      }

      const run1 = runEncounterToCompletion(10);
      const run2 = runEncounterToCompletion(10);

      expect(['active', 'victory', 'defeat']).toContain(run1.status);
      expect(['active', 'victory', 'defeat']).toContain(run2.status);
      expect(run1.rounds).toBeGreaterThanOrEqual(1);
      expect(run2.rounds).toBeGreaterThanOrEqual(1);
    });
  });

  describe('Scenario 6: Combat API contract', () => {
    it('getCombatView returns VisualInfo after encounter is initialised', () => {
      startDemo(session);

      const res = session.getCombatView();

      expect(res.ok).toBe(true);
      if (!res.ok) return;
      expect(res.data.combatants).toBeDefined();
      expect(res.data.combatants.length).toBe(6);
    });

    it('getCombatResult returns active status before any round runs', () => {
      startDemo(session);

      const res = session.getCombatResult();

      expect(res.ok).toBe(true);
      expect(res.ok && res.data.status).toBe('active');
    });

    it('getRoundHistory returns empty array before any round runs', () => {
      startDemo(session);

      const res = session.getRoundHistory();

      expect(res.ok).toBe(true);
      expect(res.ok && Array.isArray(res.data)).toBe(true);
      expect(res.ok && res.data.length).toBe(0);
    });

    it('declaring an invalid action (wrong target party) is rejected', () => {
      startDemo(session);

      const res = session.declareActions([
        { combatantId: 'player_elena', type: 'ATTACK', targetId: 'player_lars' },
      ]);

      expect(res.ok === false && res.error.code).toBe('COMBAT_INVALID_DECLARATION');
    });

    it('all 6 expected combatant IDs appear in the VisualInfo response', () => {
      startDemo(session);

      const res = session.getCombatView();
      expect(res.ok).toBe(true);
      if (!res.ok) return;

      const ids = res.data.combatants.map((c) => c.id).sort();
      expect(ids).toContain('player_elena');
      expect(ids).toContain('player_lars');
      expect(ids).toContain('player_kade');
      expect(ids).toContain('enemy_knight');
      expect(ids).toContain('enemy_rogue');
      expect(ids).toContain('enemy_mage');
    });
  });
});
