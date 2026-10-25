/**
 * Tests for the save store (saveStore.ts) over the in-memory backend.
 * Nothing here touches the developer's machine.
 */

import { describe, it, expect, beforeEach } from 'vitest';

import { createMemoryBackend, createSaveStore, MAX_SLOT, MIN_SLOT } from './index.js';
import type { SaveBackend, SaveStore } from './index.js';
import { createNewGameState } from '../state/gameState.js';
import { ErrorCodes } from '../types/index.js';
import type { GameState } from '../types/index.js';

function makeGame(playerName: string = 'Rin'): GameState {
  const base = createNewGameState();
  return { ...base, player: { ...base.player, name: playerName } };
}

const keyFor = (slot: number): string => `ancient-order/saves/manual/${slot}`;

function envelopeJson(game: unknown, overrides: Record<string, unknown> = {}): string {
  return JSON.stringify({
    format: 'ancient-order-save',
    version: 1,
    kind: 'manual',
    slot: 1,
    savedAt: 1_700_000_000_000,
    playerName: 'Rin',
    game,
    ...overrides,
  });
}

async function codeOf(promise: Promise<unknown>): Promise<string | undefined> {
  try {
    await promise;
  } catch (err) {
    return (err as { code?: string }).code;
  }
  return undefined;
}

async function messageOf(promise: Promise<unknown>): Promise<string> {
  try {
    await promise;
  } catch (err) {
    return (err as Error).message;
  }
  throw new Error('Expected a rejection');
}

function failingBackend(
  inner: SaveBackend,
  fail: Partial<Record<'read' | 'write' | 'remove', boolean>>
): SaveBackend {
  const boom = (): never => {
    throw Object.assign(new Error('disk on fire'), { code: ErrorCodes.STORAGE_ERROR });
  };
  return {
    read: (k) => (fail.read ? boom() : inner.read(k)),
    write: (k, v) => (fail.write ? boom() : inner.write(k, v)),
    remove: (k) => (fail.remove ? boom() : inner.remove(k)),
  };
}

let backend: SaveBackend;
let store: SaveStore;

beforeEach(() => {
  backend = createMemoryBackend();
  store = createSaveStore(backend);
});

describe('constants', () => {
  it('exports MIN_SLOT as 1', () => {
    expect(MIN_SLOT).toBe(1);
  });

  it('exports MAX_SLOT as 10', () => {
    expect(MAX_SLOT).toBe(10);
  });
});

describe('saveGame + loadGame (round-trip)', () => {
  it('saves and loads state with deep equality', async () => {
    const state = makeGame();
    await store.save(1, state);
    expect(await store.load(1)).toEqual(state);
  });

  it('returned SaveMetadata contains correct slot and playerName', async () => {
    const meta = await store.save(3, makeGame('Kira'));
    expect(meta.slot).toBe(3);
    expect(meta.playerName).toBe('Kira');
    expect(typeof meta.timestamp).toBe('number');
  });

  it('writes valid JSON under the slot key', async () => {
    await store.save(2, makeGame());
    const raw = await backend.read(keyFor(2));
    expect(raw).not.toBeNull();
    expect(() => JSON.parse(raw as string)).not.toThrow();
  });

  it('overwrites an existing save in the same slot', async () => {
    await store.save(1, makeGame('First'));
    await store.save(1, makeGame('Second'));
    expect((await store.load(1)).player.name).toBe('Second');
  });

  it('returns a timestamp matching the one listed for the slot', async () => {
    const meta = await store.save(4, makeGame());
    const info = (await store.list())[3];
    expect(info.metadata?.timestamp).toBe(meta.timestamp);
    const envelope = JSON.parse((await backend.read(keyFor(4))) as string);
    expect(envelope.savedAt).toBe(meta.timestamp);
  });

  it('round-trips a populated reactionProgress, team, and narrativeState', async () => {
    const state: GameState = {
      ...makeGame(),
      team: ['npc_scout_elena', 'npc_merchant_lars'],
      reactionProgress: { player_1: { block: 215, dodge: 4, parry: 0 } },
      narrativeState: {
        currentSceneId: 'scene_two',
        visitedSceneIds: ['scene_opening', 'scene_two'],
        choiceFlags: { cautious_opening: true },
        sceneHistory: [{ sceneId: 'scene_opening', choiceId: 'choice_cautious', timestamp: 1 }],
      },
    };
    await store.save(5, state);
    expect(await store.load(5)).toEqual(state);
  });
});

describe('slot independence', () => {
  it('saves to different slots independently', async () => {
    await store.save(1, makeGame('Alice'));
    await store.save(2, makeGame('Bob'));
    expect((await store.load(1)).player.name).toBe('Alice');
    expect((await store.load(2)).player.name).toBe('Bob');
  });

  it('supports all 10 slots independently', async () => {
    for (let slot = 1; slot <= 10; slot++) {
      await store.save(slot, makeGame(`Player${slot}`));
    }
    for (let slot = 1; slot <= 10; slot++) {
      expect((await store.load(slot)).player.name).toBe(`Player${slot}`);
    }
  });
});

describe('loadGame — missing slot error', () => {
  it('rejects with SAVE_NOT_FOUND for an empty slot', async () => {
    expect(await codeOf(store.load(1))).toBe(ErrorCodes.SAVE_NOT_FOUND);
    expect(await codeOf(store.load(7))).toBe(ErrorCodes.SAVE_NOT_FOUND);
  });
});

describe('invalid slot errors', () => {
  const bad: [string, unknown][] = [
    ['0', 0],
    ['11', 11],
    ['-1', -1],
    ['1.5', 1.5],
    ['NaN', NaN],
    ['a string', '1'],
    ['undefined', undefined],
  ];

  for (const [label, slot] of bad) {
    it(`save, load, and remove throw INVALID_SLOT for ${label}`, async () => {
      expect(await codeOf(store.save(slot as number, makeGame()))).toBe(ErrorCodes.INVALID_SLOT);
      expect(await codeOf(store.load(slot as number))).toBe(ErrorCodes.INVALID_SLOT);
      expect(await codeOf(store.remove(slot as number))).toBe(ErrorCodes.INVALID_SLOT);
    });
  }

  it('writes nothing for an invalid slot', async () => {
    await codeOf(store.save(0, makeGame()));
    await codeOf(store.save(11, makeGame()));
    expect((await store.list()).every((i) => i.status === 'empty')).toBe(true);
  });

  it('accepts the boundary slots 1 and 10', async () => {
    await store.save(1, makeGame('Low'));
    await store.save(10, makeGame('High'));
    expect((await store.load(1)).player.name).toBe('Low');
    expect((await store.load(10)).player.name).toBe('High');
  });

  it("the store's INVALID_SLOT message names the slot and range", async () => {
    expect(await messageOf(store.load(11))).toBe(
      'Invalid slot number: 11. Must be between 1 and 10.'
    );
  });

  it("differs from the session route's message", async () => {
    const { createGameSession } = await import('../session/index.js');
    const session = createGameSession({ saves: store });
    const res = await session.loadFromSlot(11);
    expect(res.ok).toBe(false);
    const sessionMessage = res.ok ? '' : res.error.message;
    expect(sessionMessage).toBe('Slot must be an integer between 1 and 10');
    expect(sessionMessage).not.toBe(await messageOf(store.load(11)));
  });
});

describe('listSaves', () => {
  it('returns 10 entries ordered 1-10', async () => {
    const infos = await store.list();
    expect(infos).toHaveLength(10);
    expect(infos.map((i) => i.slot)).toEqual([1, 2, 3, 4, 5, 6, 7, 8, 9, 10]);
  });

  it('all slots are empty on a fresh backend', async () => {
    const infos = await store.list();
    expect(infos.every((i) => !i.exists && i.status === 'empty')).toBe(true);
  });

  it('reflects saved slots with status and metadata', async () => {
    await store.save(1, makeGame('Player1'));
    await store.save(5, makeGame('Player5'));
    const infos = await store.list();
    expect(infos[0]).toMatchObject({
      slot: 1,
      exists: true,
      status: 'saved',
      metadata: { slot: 1, playerName: 'Player1' },
    });
    expect(typeof infos[0].metadata?.timestamp).toBe('number');
    expect(infos[4].metadata?.playerName).toBe('Player5');
    expect(infos[2].exists).toBe(false);
    expect(infos[2].status).toBe('empty');
    expect(infos[2].metadata).toBeUndefined();
  });

  it('lets a backend STORAGE_ERROR fail the whole call', async () => {
    const broken = createSaveStore(failingBackend(backend, { read: true }));
    expect(await codeOf(broken.list())).toBe(ErrorCodes.STORAGE_ERROR);
  });
});

describe('deleteSave', () => {
  it('deletes an existing save', async () => {
    await store.save(1, makeGame());
    await store.remove(1);
    expect(await codeOf(store.load(1))).toBe(ErrorCodes.SAVE_NOT_FOUND);
  });

  it('throws SAVE_NOT_FOUND when deleting an empty slot', async () => {
    expect(await codeOf(store.remove(1))).toBe(ErrorCodes.SAVE_NOT_FOUND);
  });

  it('does not affect other slots when one is deleted', async () => {
    await store.save(1, makeGame('A'));
    await store.save(2, makeGame('B'));
    await store.remove(1);
    expect((await store.load(2)).player.name).toBe('B');
  });

  it('deleted slot shows as empty in the listing', async () => {
    await store.save(3, makeGame());
    await store.remove(3);
    expect((await store.list())[2].status).toBe('empty');
  });

  it('allows deleting a corrupted slot', async () => {
    await backend.write(keyFor(2), '{not json');
    await store.remove(2);
    expect((await store.list())[1].status).toBe('empty');
  });
});

describe('corrupted saves', () => {
  it('invalid JSON: load is VALIDATION_ERROR, listing is corrupted', async () => {
    await backend.write(keyFor(1), '{not json');
    expect(await codeOf(store.load(1))).toBe(ErrorCodes.VALIDATION_ERROR);
    expect(await messageOf(store.load(1))).toContain('invalid JSON');
    expect((await store.list())[0]).toEqual({ slot: 1, exists: false, status: 'corrupted' });
  });

  it('non-object JSON (array, null, number) is corrupted', async () => {
    for (const raw of ['[]', 'null', '42']) {
      await backend.write(keyFor(1), raw);
      expect(await codeOf(store.load(1))).toBe(ErrorCodes.VALIDATION_ERROR);
    }
  });

  it('wrong format tag is corrupted', async () => {
    await backend.write(keyFor(1), envelopeJson(makeGame(), { format: 'something-else' }));
    expect(await codeOf(store.load(1))).toBe(ErrorCodes.VALIDATION_ERROR);
    expect((await store.list())[0].status).toBe('corrupted');
  });

  it('unsupported version is corrupted', async () => {
    await backend.write(keyFor(1), envelopeJson(makeGame(), { version: 99 }));
    expect(await codeOf(store.load(1))).toBe(ErrorCodes.VALIDATION_ERROR);
    expect(await messageOf(store.load(1))).toContain('unsupported version 99');
    expect((await store.list())[0].status).toBe('corrupted');
  });

  it('invalid game is corrupted', async () => {
    await backend.write(keyFor(1), envelopeJson({ player: null }));
    expect(await codeOf(store.load(1))).toBe(ErrorCodes.VALIDATION_ERROR);
    expect((await store.list())[0].status).toBe('corrupted');
  });

  it('a bad kind, slot, savedAt, or playerName is corrupted', async () => {
    const cases: Record<string, unknown>[] = [
      { kind: 'auto' },
      { slot: 0 },
      { slot: 11 },
      { slot: 1.5 },
      { savedAt: 'yesterday' },
      { savedAt: null },
      { playerName: 5 },
    ];
    for (const override of cases) {
      await backend.write(keyFor(1), envelopeJson(makeGame(), override));
      expect(await codeOf(store.load(1))).toBe(ErrorCodes.VALIDATION_ERROR);
      expect((await store.list())[0].status).toBe('corrupted');
    }
  });

  it('one corrupted slot does not hide the others in the listing', async () => {
    await store.save(1, makeGame('Good'));
    await backend.write(keyFor(2), '{not json');
    const infos = await store.list();
    expect(infos[0].status).toBe('saved');
    expect(infos[1].status).toBe('corrupted');
    expect(infos[2].status).toBe('empty');
  });

  it('a game that makes validation throw is corrupted, not a crash', async () => {
    const game = {
      ...makeGame(),
      narrativeState: {
        currentSceneId: 's',
        visitedSceneIds: [],
        choiceFlags: {},
        sceneHistory: [null],
      },
    };
    await backend.write(keyFor(1), envelopeJson(game));
    await store.save(2, makeGame('Fine'));
    expect(await codeOf(store.load(1))).toBe(ErrorCodes.VALIDATION_ERROR);
    expect(await messageOf(store.load(1))).toContain('invalid game data');
    const infos = await store.list();
    expect(infos[0].status).toBe('corrupted');
    expect(infos[1].status).toBe('saved');
  });

  it('a raw save longer than 2,000,000 characters is corrupted as too large', async () => {
    await backend.write(keyFor(1), envelopeJson(makeGame(), { padding: 'x'.repeat(2_000_001) }));
    expect(await codeOf(store.load(1))).toBe(ErrorCodes.VALIDATION_ERROR);
    expect(await messageOf(store.load(1))).toContain('save too large');
    expect((await store.list())[0].status).toBe('corrupted');
  });

  it('caps corrupted reasons at 10, each at most 200 characters', async () => {
    const game = { ...makeGame(), player: { id: 'p', name: 'n', personality: {} } };
    const huge = 'x'.repeat(5000);
    const bad = {
      ...game,
      npcs: Object.fromEntries(
        Array.from({ length: 30 }, (_, i) => [`${huge}${i}`, { id: 1 }])
      ),
    };
    await backend.write(keyFor(1), envelopeJson(bad, { kind: 'auto', slot: 0 }));
    const message = await messageOf(store.load(1));
    const reasons = message.replace(/^.*is corrupted: /, '').split('; ');
    expect(reasons.length).toBeLessThanOrEqual(10);
    for (const reason of reasons) expect(reason.length).toBeLessThanOrEqual(200);
  });

  it('a crafted save with __proto__ / constructor keys does not pollute Object.prototype', async () => {
    const raw = envelopeJson(makeGame()).replace(
      /^\{/,
      '{"__proto__":{"polluted":"envelope"},"constructor":{"prototype":{"polluted":"ctor"}},'
    );
    const gameRaw = JSON.stringify(makeGame()).replace(
      /^\{/,
      '{"__proto__":{"polluted":"game"},"constructor":{"prototype":{"polluted":"gameCtor"}},'
    );
    const full = raw.replace(/"game":\{.*\}\}$/, `"game":${gameRaw}}`);
    await backend.write(keyFor(1), full);

    const loaded = await store.load(1);
    await store.list();

    expect(({} as Record<string, unknown>).polluted).toBeUndefined();
    expect((Object.prototype as unknown as Record<string, unknown>).polluted).toBeUndefined();
    expect((loaded as unknown as Record<string, unknown>).polluted).toBeUndefined();
    expect(Object.getPrototypeOf(loaded)).toBe(Object.prototype);
  });
});

describe('older-save normalization', () => {
  async function loadOlder(drop: string[]): Promise<GameState> {
    const game = { ...makeGame() } as Record<string, unknown>;
    for (const key of drop) delete game[key];
    await backend.write(keyFor(1), envelopeJson(game));
    return store.load(1);
  }

  it('missing narrativeState loads as null', async () => {
    expect((await loadOlder(['narrativeState'])).narrativeState).toBeNull();
  });

  it('missing team loads as []', async () => {
    expect((await loadOlder(['team'])).team).toEqual([]);
  });

  it('missing reactionProgress loads as {}', async () => {
    expect((await loadOlder(['reactionProgress'])).reactionProgress).toEqual({});
  });

  it('all three missing together load with defaults', async () => {
    const loaded = await loadOlder(['narrativeState', 'team', 'reactionProgress']);
    expect(loaded.narrativeState).toBeNull();
    expect(loaded.team).toEqual([]);
    expect(loaded.reactionProgress).toEqual({});
  });

  it('an older save still lists as saved', async () => {
    await loadOlder(['narrativeState', 'team', 'reactionProgress']);
    expect((await store.list())[0].status).toBe('saved');
  });
});

describe('validation on save', () => {
  it('rejects an invalid game with VALIDATION_ERROR and writes nothing', async () => {
    const bad = { ...makeGame(), timestamp: 'nope' } as unknown as GameState;
    expect(await codeOf(store.save(1, bad))).toBe(ErrorCodes.VALIDATION_ERROR);
    expect(await backend.read(keyFor(1))).toBeNull();
    expect((await store.list())[0].status).toBe('empty');
  });

  it('does not overwrite an existing save when the new game is invalid', async () => {
    await store.save(1, makeGame('Keep'));
    const bad = { ...makeGame(), player: undefined } as unknown as GameState;
    await codeOf(store.save(1, bad));
    expect((await store.load(1)).player.name).toBe('Keep');
  });
});

describe('storage failures', () => {
  it('a write that throws surfaces as STORAGE_ERROR', async () => {
    const broken = createSaveStore(failingBackend(backend, { write: true }));
    expect(await codeOf(broken.save(1, makeGame()))).toBe(ErrorCodes.STORAGE_ERROR);
  });

  it('a read that throws surfaces as STORAGE_ERROR on load and remove', async () => {
    const broken = createSaveStore(failingBackend(backend, { read: true }));
    expect(await codeOf(broken.load(1))).toBe(ErrorCodes.STORAGE_ERROR);
    expect(await codeOf(broken.remove(1))).toBe(ErrorCodes.STORAGE_ERROR);
  });

  it('a remove that throws surfaces as STORAGE_ERROR', async () => {
    await store.save(1, makeGame());
    const broken = createSaveStore(failingBackend(backend, { remove: true }));
    expect(await codeOf(broken.remove(1))).toBe(ErrorCodes.STORAGE_ERROR);
    expect((await store.load(1)).player.name).toBe('Rin');
  });
});
