import { describe, it, expect, vi } from 'vitest';
import { createGameSession, defaultContent } from '../index.js';
import type { GameSession } from '../index.js';
import {
  LOSS_ENCOUNTER_ID,
  QUICK_FIGHT_ENCOUNTERS,
  WIN_ENCOUNTER_ID,
  fightToEnd,
} from '../../../tests/helpers/quickFight.js';

const WARRIOR_ATTACK = { combatantId: 'player_warrior', type: 'ATTACK', targetId: 'enemy_brute' };
const SCOUT_ATTACK = { combatantId: 'player_scout', type: 'ATTACK', targetId: 'enemy_archer' };

function inCombat(options?: Parameters<typeof createGameSession>[0]): GameSession {
  const session = createGameSession(options);
  session.newGame();
  const started = session.startEncounter('encounter_test');
  expect(started.ok).toBe(true);
  return session;
}

function quickFightSession(): GameSession {
  return createGameSession({ content: { encounters: QUICK_FIGHT_ENCOUNTERS } });
}

describe('startEncounter', () => {
  it('refuses a non-string id with VALIDATION_ERROR', () => {
    const session = createGameSession();
    session.newGame();
    const r = session.startEncounter(42);
    expect(r.ok === false && r.error.code).toBe('VALIDATION_ERROR');
  });

  it('refuses without a game with GAME_NOT_FOUND', () => {
    const r = createGameSession().startEncounter('encounter_test');
    expect(r.ok === false && r.error.code).toBe('GAME_NOT_FOUND');
  });

  it('refuses an unknown encounter with ENCOUNTER_NOT_FOUND and leaves state unchanged', () => {
    const session = createGameSession();
    session.newGame();
    const snap = session.getSnapshot();
    const r = session.startEncounter('nope');
    expect(r.ok === false && r.error.code).toBe('ENCOUNTER_NOT_FOUND');
    expect(session.getSnapshot()).toBe(snap);
  });

  it('refuses a second start with COMBAT_ALREADY_ACTIVE and keeps the running fight', () => {
    const session = inCombat();
    session.declareActions([WARRIOR_ATTACK]);
    const snap = session.getSnapshot();
    const listener = vi.fn();
    session.subscribe(listener);

    const again = session.startEncounter('encounter_demo');

    expect(again.ok === false && again.error.code).toBe('COMBAT_ALREADY_ACTIVE');
    expect(session.getSnapshot()).toBe(snap);
    expect(listener).not.toHaveBeenCalled();
    const view = session.getCombatView();
    expect(view.ok && view.data.combatants.some((c) => c.id === 'player_warrior')).toBe(true);
  });

  it('clears the previous result and orders when a new fight starts', () => {
    const session = quickFightSession();
    session.newGame();
    session.startEncounter(WIN_ENCOUNTER_ID);
    expect(fightToEnd(session)).toBe('victory');
    expect(session.getSnapshot().lastCombatOutcome).not.toBeNull();

    session.startEncounter(WIN_ENCOUNTER_ID);

    expect(session.getSnapshot().lastCombatOutcome).toBeNull();
    expect(session.getSnapshot().pendingDeclarations).toEqual([]);
    const result = session.getCombatResult();
    expect(result.ok && result.data.status).toBe('active');
  });
});

describe('declareActions', () => {
  it('refuses without a game, and without a fight', () => {
    const none = createGameSession().declareActions([WARRIOR_ATTACK]);
    expect(none.ok === false && none.error.code).toBe('GAME_NOT_FOUND');

    const session = createGameSession();
    session.newGame();
    const noFight = session.declareActions([WARRIOR_ATTACK]);
    expect(noFight.ok === false && noFight.error.code).toBe('NO_ACTIVE_COMBAT');
  });

  it('stores accepted orders in the session and reports them', () => {
    const session = inCombat();
    const r = session.declareActions([WARRIOR_ATTACK, SCOUT_ATTACK]);
    expect(r.ok).toBe(true);
    if (!r.ok) return;
    expect(r.data.accepted).toHaveLength(2);
    expect(r.data.rejected).toHaveLength(0);
    expect(session.getSnapshot().pendingDeclarations).toHaveLength(2);
  });

  it('normalizes a missing target to null and omits energySegments when not given', () => {
    const session = inCombat();
    const r = session.declareActions([{ combatantId: 'player_warrior', type: 'EVADE' }]);
    expect(r.ok).toBe(true);
    const [evade] = session.getSnapshot().pendingDeclarations;
    expect(evade.targetId).toBeNull();
    expect('energySegments' in evade).toBe(false);
  });

  it('refuses energySegments on a non-SPECIAL order', () => {
    const session = inCombat();
    const r = session.declareActions([WARRIOR_ATTACK, { ...SCOUT_ATTACK, energySegments: 2 }]);
    expect(r.ok).toBe(true);
    if (!r.ok) return;
    expect(r.data.rejected).toHaveLength(1);
    expect(r.data.rejected[0].error).toBe('energySegments is only allowed for SPECIAL');
    expect(session.getSnapshot().pendingDeclarations).toEqual([WARRIOR_ATTACK]);
  });

  it('refuses an order for a combatant outside the player party', () => {
    const session = inCombat();
    const r = session.declareActions([
      WARRIOR_ATTACK,
      { combatantId: 'enemy_brute', type: 'ATTACK', targetId: 'player_warrior' },
    ]);
    expect(r.ok).toBe(true);
    if (!r.ok) return;
    expect(r.data.rejected).toHaveLength(1);
    expect(r.data.rejected[0].error).toBe("Combatant 'enemy_brute' is not in the player's party");
    expect(session.getSnapshot().pendingDeclarations).toEqual([WARRIOR_ATTACK]);

    const only = session.declareActions([
      { combatantId: 'enemy_brute', type: 'ATTACK', targetId: 'player_warrior' },
    ]);
    expect(only.ok === false && only.error.code).toBe('COMBAT_INVALID_DECLARATION');
    expect(session.getSnapshot().pendingDeclarations).toEqual([WARRIOR_ATTACK]);
  });

  describe('SPECIAL energySegments', () => {
    // Ascension level 3 starts with 2 energy segments, so no random rounds are needed.
    function energizedSession(): GameSession {
      const base = defaultContent.encounters.encounter_test;
      const encounter = {
        ...base,
        id: 'encounter_energized',
        playerParty: base.playerParty.map((c) =>
          c.id === 'player_warrior' ? { ...c, ascensionLevel: 3 as const } : c
        ),
      };
      const session = createGameSession({ content: { encounters: { encounter_energized: encounter } } });
      session.newGame();
      expect(session.startEncounter('encounter_energized').ok).toBe(true);
      return session;
    }
    const special = (energySegments?: number) => ({
      combatantId: 'player_warrior',
      type: 'SPECIAL',
      targetId: 'enemy_brute',
      ...(energySegments !== undefined ? { energySegments } : {}),
    });

    it('keeps energySegments when it is an integer within the combatant energy', () => {
      const session = energizedSession();
      const r = session.declareActions([special(2)]);
      expect(r.ok && r.data.accepted).toHaveLength(1);
      expect(session.getSnapshot().pendingDeclarations).toEqual([special(2)]);
    });

    it('accepts an omitted energySegments (spend all) without adding the key', () => {
      const session = energizedSession();
      const r = session.declareActions([special()]);
      expect(r.ok).toBe(true);
      expect('energySegments' in session.getSnapshot().pendingDeclarations[0]).toBe(false);
    });

    it.each([0, 3, -1, 1.5])('refuses energySegments %s and keeps previous orders', (n) => {
      const session = energizedSession();
      session.declareActions([special(1)]);
      const pending = session.getSnapshot().pendingDeclarations;
      const r = session.declareActions([special(n)]);
      expect(r.ok === false && r.error.code).toBe('COMBAT_INVALID_DECLARATION');
      expect(session.getSnapshot().pendingDeclarations).toBe(pending);
    });
  });

  it('substitutes the fallback ATTACK for a GROUP the allies lack energy for', () => {
    const session = inCombat();
    const r = session.declareActions([
      { combatantId: 'player_warrior', type: 'GROUP', targetId: 'enemy_brute' },
    ]);
    expect(r.ok).toBe(true);
    if (!r.ok) return;
    expect(r.data.rejected).toHaveLength(1);
    expect(r.data.rejected[0].type).toBe('GROUP');
    expect(r.data.rejected[0].valid).toBe(false);
    expect(r.data.rejected[0].error).toContain('full energy');
    expect(r.data.accepted).toEqual([
      { combatantId: 'player_warrior', type: 'ATTACK', targetId: 'enemy_brute', valid: true },
    ]);
    expect(session.getSnapshot().pendingDeclarations).toEqual([
      { combatantId: 'player_warrior', type: 'ATTACK', targetId: 'enemy_brute' },
    ]);
  });

  it('accepts the valid part of a mixed declaration and reports the rest as rejected', () => {
    const session = inCombat();
    const r = session.declareActions([
      WARRIOR_ATTACK,
      { combatantId: 'player_scout', type: 'ATTACK', targetId: 'player_mage' },
    ]);
    expect(r.ok).toBe(true);
    if (!r.ok) return;
    expect(r.data.accepted).toHaveLength(1);
    expect(r.data.rejected).toHaveLength(1);
    expect(session.getSnapshot().pendingDeclarations).toHaveLength(1);
  });

  it('refuses a fully invalid declaration and keeps the previous orders (approved fix)', () => {
    const session = inCombat();
    session.declareActions([WARRIOR_ATTACK]);
    const pending = session.getSnapshot().pendingDeclarations;
    const snap = session.getSnapshot();
    const listener = vi.fn();
    session.subscribe(listener);

    const bad = session.declareActions([{ combatantId: 'nobody', type: 'ATTACK' }]);

    expect(bad.ok).toBe(false);
    if (bad.ok) return;
    expect(bad.error.code).toBe('COMBAT_INVALID_DECLARATION');
    expect(bad.error.message).toBe('All declarations were invalid');
    expect((bad.error.details as { rejected: unknown[] }).rejected).toHaveLength(1);
    expect(session.getSnapshot()).toBe(snap);
    expect(session.getSnapshot().pendingDeclarations).toBe(pending);
    expect(pending).toEqual([WARRIOR_ATTACK]);
    expect(listener).not.toHaveBeenCalled();
  });

  it('replaces earlier orders instead of appending', () => {
    const session = inCombat();
    session.declareActions([WARRIOR_ATTACK]);
    session.declareActions([SCOUT_ATTACK]);
    expect(session.getSnapshot().pendingDeclarations).toEqual([SCOUT_ATTACK]);
  });

  it('clears orders when given an empty array', () => {
    const session = inCombat();
    session.declareActions([WARRIOR_ATTACK]);
    const r = session.declareActions([]);
    expect(r.ok).toBe(true);
    expect(session.getSnapshot().pendingDeclarations).toEqual([]);
  });

  it('refuses malformed input with VALIDATION_ERROR and keeps orders', () => {
    const session = inCombat();
    session.declareActions([WARRIOR_ATTACK]);
    const snap = session.getSnapshot();
    const r = session.declareActions([{ combatantId: 'player_warrior' }]);
    expect(r.ok === false && r.error.code).toBe('VALIDATION_ERROR');
    expect(session.getSnapshot()).toBe(snap);
  });
});

describe('advanceRound', () => {
  it('refuses without a game, and without a fight', () => {
    const none = createGameSession().advanceRound();
    expect(none.ok === false && none.error.code).toBe('GAME_NOT_FOUND');

    const session = createGameSession();
    session.newGame();
    const noFight = session.advanceRound();
    expect(noFight.ok === false && noFight.error.code).toBe('NO_ACTIVE_COMBAT');
  });

  it('plays the round, clears the orders and grows the history', () => {
    const session = inCombat();
    session.declareActions([WARRIOR_ATTACK]);

    const r = session.advanceRound();

    expect(r.ok).toBe(true);
    if (!r.ok) return;
    expect(r.data.round).toBe(2);
    expect(r.data.combatants).toHaveLength(6);
    expect(session.getSnapshot().pendingDeclarations).toEqual([]);
    const history = session.getRoundHistory();
    expect(history.ok && history.data).toHaveLength(1);
  });

  it('runs a round with no orders (AI only)', () => {
    const session = inCombat();
    const r = session.advanceRound();
    expect(r.ok && r.data.round).toBe(2);
  });
});

describe('combat view', () => {
  it('refuses without a game, and without a fight', () => {
    const none = createGameSession().getCombatView();
    expect(none.ok === false && none.error.code).toBe('GAME_NOT_FOUND');

    const session = createGameSession();
    session.newGame();
    expect(session.getCombatView().ok === false).toBe(true);
    const history = session.getRoundHistory();
    expect(history.ok === false && history.error.code).toBe('NO_ACTIVE_COMBAT');
  });

  it('shows the player targets but hides what enemies have decided', () => {
    const session = inCombat();
    session.declareActions([WARRIOR_ATTACK]);

    const view = session.getCombatView();

    expect(view.ok).toBe(true);
    if (!view.ok) return;
    const byId = new Map(view.data.combatants.map((c) => [c.id, c]));
    expect(byId.get('player_warrior')?.targeting).toBe('enemy_brute');
    expect(byId.get('player_scout')?.targeting).toBeNull();
    for (const id of ['enemy_brute', 'enemy_archer', 'enemy_shaman']) {
      expect(byId.get(id)?.targeting).toBeNull();
    }
  });

  it('reading the view or history does not change or notify the session', () => {
    const session = inCombat();
    const snap = session.getSnapshot();
    const listener = vi.fn();
    session.subscribe(listener);
    session.getCombatView();
    session.getRoundHistory();
    session.getCombatResult();
    expect(session.getSnapshot()).toBe(snap);
    expect(listener).not.toHaveBeenCalled();
  });
});

describe('combat result', () => {
  it('refuses without a game, and when no fight ever happened', () => {
    const none = createGameSession().getCombatResult();
    expect(none.ok === false && none.error.code).toBe('GAME_NOT_FOUND');

    const session = createGameSession();
    session.newGame();
    const never = session.getCombatResult();
    expect(never.ok === false && never.error.code).toBe('NO_ACTIVE_COMBAT');
  });

  it('is active, with the round, while the fight runs', () => {
    const session = inCombat();
    const r = session.getCombatResult();
    expect(r.ok && r.data).toEqual({ status: 'active', round: 1 });
  });

  it('stays readable after a victory (approved fix)', () => {
    const session = quickFightSession();
    session.newGame();
    session.startEncounter(WIN_ENCOUNTER_ID);
    expect(fightToEnd(session)).toBe('victory');

    const r = session.getCombatResult();

    expect(r.ok).toBe(true);
    if (!r.ok) return;
    expect(r.data.status).toBe('victory');
    expect(r.data.round).toBeGreaterThanOrEqual(1);
    const again = session.getCombatResult();
    expect(again.ok && again.data).toEqual(r.data);
  });

  it('stays readable after a defeat (approved fix)', () => {
    const session = quickFightSession();
    session.newGame();
    session.startEncounter(LOSS_ENCOUNTER_ID);
    expect(fightToEnd(session)).toBe('defeat');

    const r = session.getCombatResult();

    expect(r.ok && r.data.status).toBe('defeat');
  });

  it('ends the fight: view and history refuse, combatState clears, team unlocks', () => {
    const session = quickFightSession();
    session.newGame();
    session.startEncounter(WIN_ENCOUNTER_ID);
    fightToEnd(session);

    expect(session.getSnapshot().game?.combatState).toBeNull();
    const view = session.getCombatView();
    expect(view.ok === false && view.error.code).toBe('NO_ACTIVE_COMBAT');
    const history = session.getRoundHistory();
    expect(history.ok === false && history.error.code).toBe('NO_ACTIVE_COMBAT');
    const round = session.advanceRound();
    expect(round.ok === false && round.error.code).toBe('NO_ACTIVE_COMBAT');
    const team = session.setTeam(['npc_scout_elena', 'npc_merchant_lars']);
    expect(team.ok).toBe(true);
  });

  it('is cleared by a new game (approved fix)', () => {
    const session = quickFightSession();
    session.newGame();
    session.startEncounter(WIN_ENCOUNTER_ID);
    fightToEnd(session);

    session.newGame();

    const r = session.getCombatResult();
    expect(r.ok === false && r.error.code).toBe('NO_ACTIVE_COMBAT');
    expect(session.getSnapshot().lastCombatOutcome).toBeNull();
  });
});
