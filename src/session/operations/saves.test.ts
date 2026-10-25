/**
 * Session save operations: save, load, list, delete rules and messages,
 * and the load-clears-combat fix.
 */

import { describe, it, expect } from 'vitest';

import { createGameSession } from '../index.js';
import type { GameSession } from '../index.js';
import { createMemoryBackend, createSaveStore } from '../../persistence/index.js';
import type { SaveBackend } from '../../persistence/index.js';
import {
  QUICK_FIGHT_ENCOUNTERS,
  WIN_ENCOUNTER_ID,
  fightToEnd,
} from '../../../tests/helpers/quickFight.js';

const KEY = (slot: number): string => `ancient-order/saves/manual/${slot}`;

function setup(): { session: GameSession; backend: SaveBackend } {
  const backend = createMemoryBackend();
  const session = createGameSession({ saves: createSaveStore(backend) });
  return { session, backend };
}

function codeOf(res: { ok: boolean; error?: { code: string } }): string | undefined {
  return res.ok ? undefined : res.error?.code;
}

const ATTACKS = [
  { combatantId: 'player_warrior', type: 'ATTACK', targetId: 'enemy_brute' },
  { combatantId: 'player_scout', type: 'ATTACK', targetId: 'enemy_archer' },
];

describe('saveToSlot', () => {
  it('saves the active game and returns metadata', async () => {
    const { session } = setup();
    session.newGame();
    const res = await session.saveToSlot(3);
    expect(res.ok).toBe(true);
    if (res.ok) {
      expect(res.data.slot).toBe(3);
      expect(typeof res.data.timestamp).toBe('number');
      expect(typeof res.data.playerName).toBe('string');
    }
  });

  it('writes the save to the backend', async () => {
    const { session, backend } = setup();
    session.newGame();
    await session.saveToSlot(1);
    expect(await backend.read(KEY(1))).not.toBeNull();
  });

  it('returns GAME_NOT_FOUND with no active game', async () => {
    const res = await setup().session.saveToSlot(1);
    expect(res.ok).toBe(false);
    if (!res.ok) {
      expect(res.error.code).toBe('GAME_NOT_FOUND');
      expect(res.error.message).toBe('No active game');
    }
  });

  it('returns INVALID_SLOT for out-of-range and non-integer slots', async () => {
    const { session } = setup();
    session.newGame();
    for (const slot of [0, 11, -1, 1.5, '1', null, undefined, NaN]) {
      expect(codeOf(await session.saveToSlot(slot))).toBe('INVALID_SLOT');
    }
  });

  it('reports INVALID_SLOT before GAME_NOT_FOUND', async () => {
    expect(codeOf(await setup().session.saveToSlot(0))).toBe('INVALID_SLOT');
  });

  it('uses the route message for an invalid slot', async () => {
    const { session } = setup();
    session.newGame();
    const res = await session.saveToSlot(11);
    expect(!res.ok && res.error.message).toBe('Slot must be an integer between 1 and 10');
  });

  it('does not change the session snapshot', async () => {
    const { session } = setup();
    session.newGame();
    const snap = session.getSnapshot();
    await session.saveToSlot(1);
    expect(session.getSnapshot()).toBe(snap);
  });

  it('maps a backend write failure to STORAGE_ERROR', async () => {
    const backend: SaveBackend = {
      ...createMemoryBackend(),
      write: async () => {
        throw Object.assign(new Error('full'), { code: 'STORAGE_ERROR' });
      },
    };
    const session = createGameSession({ saves: createSaveStore(backend) });
    session.newGame();
    expect(codeOf(await session.saveToSlot(1))).toBe('STORAGE_ERROR');
  });
});

describe('loadFromSlot', () => {
  it('loads a saved game into the session', async () => {
    const { session } = setup();
    session.newGame();
    session.adjustPersonality({ patience: 8 });
    const before = session.getState();
    await session.saveToSlot(1);
    session.newGame();
    const res = await session.loadFromSlot(1);
    expect(res.ok).toBe(true);
    const after = session.getState();
    expect(before.ok && after.ok).toBe(true);
    expect(after.ok && before.ok && after.data).toEqual(before.ok && before.data);
  });

  it('returns SAVE_NOT_FOUND for an empty slot and leaves the game alone', async () => {
    const { session } = setup();
    session.newGame();
    const snap = session.getSnapshot();
    expect(codeOf(await session.loadFromSlot(9))).toBe('SAVE_NOT_FOUND');
    expect(session.getSnapshot()).toBe(snap);
  });

  it('works with no active game', async () => {
    const { session, backend } = setup();
    session.newGame();
    await session.saveToSlot(2);
    const fresh = createGameSession({ saves: createSaveStore(backend) });
    expect(fresh.getSnapshot().game).toBeNull();
    expect((await fresh.loadFromSlot(2)).ok).toBe(true);
    expect(fresh.getSnapshot().game).not.toBeNull();
  });

  it('returns INVALID_SLOT for bad slots', async () => {
    const { session } = setup();
    for (const slot of [0, 11, 2.5, 'x']) {
      expect(codeOf(await session.loadFromSlot(slot))).toBe('INVALID_SLOT');
    }
  });

  it('returns VALIDATION_ERROR for a corrupted save and keeps the current game', async () => {
    const { session, backend } = setup();
    session.newGame();
    await backend.write(KEY(4), '{not json');
    const snap = session.getSnapshot();
    expect(codeOf(await session.loadFromSlot(4))).toBe('VALIDATION_ERROR');
    expect(session.getSnapshot()).toBe(snap);
  });

  it('notifies subscribers when the load commits', async () => {
    const { session } = setup();
    session.newGame();
    await session.saveToSlot(1);
    let calls = 0;
    session.subscribe(() => {
      calls++;
    });
    await session.loadFromSlot(1);
    expect(calls).toBe(1);
  });

  it('clears pending combat orders', async () => {
    const { session } = setup();
    session.newGame();
    await session.saveToSlot(1);
    expect(session.startEncounter('encounter_test').ok).toBe(true);
    expect(session.declareActions(ATTACKS).ok).toBe(true);
    expect(session.getSnapshot().pendingDeclarations.length).toBeGreaterThan(0);

    expect((await session.loadFromSlot(1)).ok).toBe(true);
    expect(session.getSnapshot().pendingDeclarations).toEqual([]);
    expect(session.getSnapshot().game?.combatState ?? null).toBeNull();
  });

  it('clears the last fight result', async () => {
    const session = createGameSession({
      saves: createSaveStore(createMemoryBackend()),
      content: { encounters: QUICK_FIGHT_ENCOUNTERS },
    });
    session.newGame();
    await session.saveToSlot(1);
    session.startEncounter(WIN_ENCOUNTER_ID);
    expect(fightToEnd(session)).toBe('victory');
    expect(session.getSnapshot().lastCombatOutcome).not.toBeNull();

    expect((await session.loadFromSlot(1)).ok).toBe(true);
    expect(session.getSnapshot().lastCombatOutcome).toBeNull();
  });

  it('resumes a game saved mid-conversation at the same dialogue node', async () => {
    const { session } = setup();
    session.newGame();
    const started = session.startDialogue('npc_scout_elena');
    expect(started.ok).toBe(true);
    const nodeId = started.ok ? started.data.node.id : '';
    await session.saveToSlot(1);

    session.newGame();
    expect(session.getSnapshot().game?.currentDialogueNode).toBeNull();

    await session.loadFromSlot(1);
    expect(session.getSnapshot().game?.currentDialogueNode).toBe(nodeId);

    const chosen = session.chooseDialogueOption('opt_farewell');
    expect(chosen.ok).toBe(true);
    expect(chosen.ok && chosen.data.nextNode).toBeNull();
  });
});

describe('listSlots', () => {
  it('lists 10 empty slots on a fresh store', async () => {
    const res = await setup().session.listSlots();
    expect(res.ok && res.data).toHaveLength(10);
    expect(res.ok && res.data.every((s) => s.status === 'empty')).toBe(true);
  });

  it('works without an active game', async () => {
    expect((await setup().session.listSlots()).ok).toBe(true);
  });

  it('reflects a saved slot with metadata', async () => {
    const { session } = setup();
    session.newGame();
    await session.saveToSlot(1);
    const res = await session.listSlots();
    const slot1 = res.ok ? res.data.find((s) => s.slot === 1) : undefined;
    expect(slot1?.exists).toBe(true);
    expect(slot1?.status).toBe('saved');
    expect(slot1?.metadata?.slot).toBe(1);
    expect(typeof slot1?.metadata?.timestamp).toBe('number');
  });

  it('shows a corrupted slot as corrupted without failing the list', async () => {
    const { session, backend } = setup();
    await backend.write(KEY(3), 'garbage');
    const res = await session.listSlots();
    expect(res.ok && res.data[2].status).toBe('corrupted');
    expect(res.ok && res.data[0].status).toBe('empty');
  });

  it('maps a backend read failure to STORAGE_ERROR', async () => {
    const backend: SaveBackend = {
      ...createMemoryBackend(),
      read: async () => {
        throw Object.assign(new Error('nope'), { code: 'STORAGE_ERROR' });
      },
    };
    const session = createGameSession({ saves: createSaveStore(backend) });
    expect(codeOf(await session.listSlots())).toBe('STORAGE_ERROR');
  });
});

describe('deleteSlot', () => {
  it('deletes an existing save', async () => {
    const { session } = setup();
    session.newGame();
    await session.saveToSlot(1);
    const res = await session.deleteSlot(1);
    expect(res.ok && res.data).toEqual({ slot: 1, deleted: true });
    expect(codeOf(await session.loadFromSlot(1))).toBe('SAVE_NOT_FOUND');
  });

  it('returns SAVE_NOT_FOUND for an empty slot', async () => {
    expect(codeOf(await setup().session.deleteSlot(5))).toBe('SAVE_NOT_FOUND');
  });

  it('allows deleting a corrupted slot', async () => {
    const { session, backend } = setup();
    await backend.write(KEY(2), '{not json');
    expect((await session.deleteSlot(2)).ok).toBe(true);
    expect(await backend.read(KEY(2))).toBeNull();
  });

  it('returns INVALID_SLOT for bad slots', async () => {
    const { session } = setup();
    for (const slot of [0, 11, 'abc', 1.5]) {
      expect(codeOf(await session.deleteSlot(slot))).toBe('INVALID_SLOT');
    }
  });

  it('does not touch the active game', async () => {
    const { session } = setup();
    session.newGame();
    await session.saveToSlot(1);
    const snap = session.getSnapshot();
    await session.deleteSlot(1);
    expect(session.getSnapshot()).toBe(snap);
  });
});
