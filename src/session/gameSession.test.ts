import { describe, it, expect, vi } from 'vitest';
import { createGameSession } from './index.js';
import {
  QUICK_FIGHT_ENCOUNTERS,
  WIN_ENCOUNTER_ID,
  fightToEnd,
} from '../../tests/helpers/quickFight.js';

describe('createGameSession smoke', () => {
  it('refuses with no game, then starts and notifies once', () => {
    const session = createGameSession();
    const before = session.getSnapshot();
    const listener = vi.fn();
    session.subscribe(listener);
    expect(session.getState().ok).toBe(false);
    expect(session.getSnapshot()).toBe(before);
    expect(listener).not.toHaveBeenCalled();
    expect(session.newGame().ok).toBe(true);
    expect(listener).toHaveBeenCalledTimes(1);
    expect(session.getSnapshot()).not.toBe(before);
  });

  it('rejects an empty personality adjustment without changing the snapshot', () => {
    const session = createGameSession();
    session.newGame();
    const snap = session.getSnapshot();
    const r = session.adjustPersonality({});
    expect(r.ok === false && r.error.code).toBe('VALIDATION_ERROR');
    expect(session.getSnapshot()).toBe(snap);
  });
});

describe('snapshot immutability', () => {
  it('leaves earlier snapshots untouched by later operations', () => {
    const session = createGameSession();
    session.newGame();
    const first = session.getSnapshot();
    const firstGame = first.game;
    const patience = firstGame!.player.personality.patience;

    session.adjustPersonality({ patience: 5 });
    session.startEncounter('encounter_test');
    session.declareActions([
      { combatantId: 'player_warrior', type: 'ATTACK', targetId: 'enemy_brute' },
    ]);
    session.advanceRound();

    expect(first.game).toBe(firstGame);
    expect(first.game!.player.personality.patience).toBe(patience);
    expect(first.game!.combatState).toBeNull();
    expect(first.pendingDeclarations).toEqual([]);
    expect(session.getSnapshot()).not.toBe(first);
  });

  it('does not mutate the pending orders array when new orders replace it', () => {
    const session = createGameSession();
    session.newGame();
    session.startEncounter('encounter_test');
    session.declareActions([
      { combatantId: 'player_warrior', type: 'ATTACK', targetId: 'enemy_brute' },
    ]);
    const before = session.getSnapshot().pendingDeclarations;
    session.declareActions([
      { combatantId: 'player_scout', type: 'ATTACK', targetId: 'enemy_archer' },
    ]);
    expect(before).toHaveLength(1);
    expect(before[0].combatantId).toBe('player_warrior');
  });
});

describe('subscribe', () => {
  it('notifies once per state change', () => {
    const session = createGameSession();
    const listener = vi.fn();
    session.subscribe(listener);
    session.newGame();
    session.adjustPersonality({ patience: 1 });
    session.setTeam(['npc_scout_elena', 'npc_merchant_lars']);
    expect(listener).toHaveBeenCalledTimes(3);
  });

  it('never notifies on reads or refusals', () => {
    const session = createGameSession();
    const listener = vi.fn();
    session.subscribe(listener);
    session.getState();
    session.getNpc('npc_scout_elena');
    session.listNpcs();
    session.adjustPersonality({ patience: 1 });
    session.newGame();
    listener.mockClear();

    session.getState();
    session.getPlayer();
    session.getPersonality();
    session.getNarrativeState();
    session.getSynergy();
    session.getCombatResult();
    session.adjustPersonality({});
    session.startScene('nope');
    session.startEncounter('nope');
    session.chooseDialogueOption('x');
    session.setTeam(['a']);

    expect(listener).not.toHaveBeenCalled();
  });

  it('stops notifying after unsubscribe and supports several listeners', () => {
    const session = createGameSession();
    const a = vi.fn();
    const b = vi.fn();
    const offA = session.subscribe(a);
    session.subscribe(b);
    session.newGame();
    offA();
    session.adjustPersonality({ patience: 1 });
    expect(a).toHaveBeenCalledTimes(1);
    expect(b).toHaveBeenCalledTimes(2);
  });

  it('notifies after the snapshot is updated', () => {
    const session = createGameSession();
    let seen: unknown = null;
    session.subscribe(() => {
      seen = session.getSnapshot().game;
    });
    session.newGame();
    expect(seen).not.toBeNull();
  });
});

describe('newGame', () => {
  it('clears pending orders and the last combat result (approved fix)', () => {
    const session = createGameSession({ content: { encounters: QUICK_FIGHT_ENCOUNTERS } });
    session.newGame();
    session.startEncounter(WIN_ENCOUNTER_ID);
    expect(fightToEnd(session)).toBe('victory');
    session.startEncounter(WIN_ENCOUNTER_ID);
    session.declareActions([
      { combatantId: 'player_warrior', type: 'ATTACK', targetId: 'enemy_brute' },
    ]);
    expect(session.getSnapshot().pendingDeclarations).toHaveLength(1);

    session.newGame();

    const snap = session.getSnapshot();
    expect(snap.pendingDeclarations).toEqual([]);
    expect(snap.lastCombatOutcome).toBeNull();
    expect(snap.game?.combatState).toBeNull();
    expect(session.getCombatResult().ok).toBe(false);
  });

  it('replaces the whole game, including dialogue position and narrative', () => {
    const session = createGameSession();
    session.newGame();
    session.startScene('scene_opening');
    session.startDialogue('npc_scout_elena');
    session.adjustPersonality({ patience: 8 });

    session.newGame();

    const game = session.getSnapshot().game!;
    expect(game.narrativeState).toBeNull();
    expect(game.currentDialogueNode).toBeNull();
    expect(game.conversationLog).toEqual([]);
  });
});

describe('engine failure', () => {
  it('turns an unexpected throw into INTERNAL_ERROR with state unchanged and no notification', () => {
    const broken = {
      id: 'encounter_broken',
      name: 'Broken',
      playerParty: null,
      enemyParty: null,
    };
    const session = createGameSession({
      content: { encounters: { encounter_broken: broken as never } },
    });
    session.newGame();
    const snap = session.getSnapshot();
    const listener = vi.fn();
    session.subscribe(listener);

    const r = session.startEncounter('encounter_broken');

    expect(r.ok === false && r.error.code).toBe('INTERNAL_ERROR');
    expect(r.ok === false && r.error.cause).toBeInstanceOf(Error);
    expect(session.getSnapshot()).toBe(snap);
    expect(listener).not.toHaveBeenCalled();
    expect(session.getState().ok).toBe(true);
  });
});
