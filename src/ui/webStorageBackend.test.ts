/**
 * createWebStorageBackend against jsdom's real window.localStorage.
 * Only ancient-order/ keys are cleared after each test.
 */

import { afterEach, describe, expect, it } from 'vitest';

import { createSaveStore, createWebStorageBackend } from '../persistence/index.js';
import { createNewGameState } from '../state/gameState.js';

const PREFIX = 'ancient-order/';

afterEach(() => {
  const keys: string[] = [];
  for (let i = 0; i < window.localStorage.length; i++) {
    const key = window.localStorage.key(i);
    if (key !== null && key.startsWith(PREFIX)) keys.push(key);
  }
  for (const key of keys) window.localStorage.removeItem(key);
});

describe('createWebStorageBackend over window.localStorage', () => {
  it('writes, reads, and removes a key', async () => {
    const backend = createWebStorageBackend(window.localStorage);
    expect(await backend.read(`${PREFIX}t`)).toBeNull();
    await backend.write(`${PREFIX}t`, 'v');
    expect(window.localStorage.getItem(`${PREFIX}t`)).toBe('v');
    expect(await backend.read(`${PREFIX}t`)).toBe('v');
    await backend.remove(`${PREFIX}t`);
    expect(window.localStorage.getItem(`${PREFIX}t`)).toBeNull();
  });

  it('round-trips a game through the save store and lists it', async () => {
    const store = createSaveStore(createWebStorageBackend(window.localStorage));
    const game = createNewGameState();
    await store.save(2, game);
    expect(window.localStorage.getItem(`${PREFIX}saves/manual/2`)).not.toBeNull();
    expect(await store.load(2)).toEqual(game);
    expect((await store.list())[1].status).toBe('saved');
    await store.remove(2);
    expect((await store.list())[1].status).toBe('empty');
  });
});
