import { describe, expect, it } from 'vitest';
import { createNewGameState } from '../state/gameState.js';
import { createMemoryBackend, createSaveStore } from './index.js';

describe('save store smoke', () => {
  it('round-trips a game through a slot and lists it', async () => {
    const store = createSaveStore(createMemoryBackend());
    const game = createNewGameState();
    const meta = await store.save(3, game);
    expect(await store.load(3)).toEqual(game);
    const list = await store.list();
    expect(list).toHaveLength(10);
    expect(list[2].metadata?.timestamp).toBe(meta.timestamp);
  });

  it('reports corrupted JSON as VALIDATION_ERROR and lists it as corrupted', async () => {
    const backend = createMemoryBackend();
    const store = createSaveStore(backend);
    await backend.write('ancient-order/saves/manual/2', '{not json');
    await expect(store.load(2)).rejects.toMatchObject({ code: 'VALIDATION_ERROR' });
    expect((await store.list())[1].status).toBe('corrupted');
  });

  it('lists a save that makes validation throw as corrupted without failing the other slots', async () => {
    const backend = createMemoryBackend();
    const store = createSaveStore(backend);
    const game = createNewGameState();
    await store.save(1, game);
    const crafted = {
      format: 'ancient-order-save',
      version: 1,
      kind: 'manual',
      slot: 2,
      savedAt: 1,
      playerName: 'x',
      game: { ...game, narrativeState: { currentSceneId: 's', visitedSceneIds: [], choiceFlags: {}, sceneHistory: [null] } },
    };
    await backend.write('ancient-order/saves/manual/2', JSON.stringify(crafted));
    const list = await store.list();
    expect(list).toHaveLength(10);
    expect(list[0].status).toBe('saved');
    expect(list[1].status).toBe('corrupted');
    await expect(store.load(2)).rejects.toMatchObject({ code: 'VALIDATION_ERROR' });
  });

  it('reports an oversized save as corrupted', async () => {
    const backend = createMemoryBackend();
    const store = createSaveStore(backend);
    await backend.write('ancient-order/saves/manual/4', 'x'.repeat(2_000_001));
    expect((await store.list())[3].status).toBe('corrupted');
    await expect(store.load(4)).rejects.toMatchObject({
      code: 'VALIDATION_ERROR',
      message: expect.stringContaining('save too large'),
    });
  });
});
