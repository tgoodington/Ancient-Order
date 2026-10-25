import { describe, it, expect, vi, beforeEach } from 'vitest';

const game = { showScene: vi.fn(), destroy: vi.fn() };
const createPhaserGame = vi.fn(() => game);

vi.mock('./phaserGame', () => ({ createPhaserGame }));

const { createPhaserRenderer } = await import('./phaserRenderer');
const flush = () => new Promise((resolve) => setTimeout(resolve, 0));

describe('createPhaserRenderer', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    createPhaserGame.mockImplementation(() => game);
  });

  const options = { backgroundColor: '#000', initialScene: 'stage' } as const;

  it('does not restart the scene that is already showing', async () => {
    const renderer = createPhaserRenderer(document.createElement('div'), options);
    renderer.showScene('stage');
    await flush();
    renderer.showScene('stage');

    expect(createPhaserGame).toHaveBeenCalledTimes(1);
    expect(game.showScene).not.toHaveBeenCalled();
  });

  it('never creates the game when destroyed before it loads', async () => {
    const renderer = createPhaserRenderer(document.createElement('div'), options);
    renderer.destroy();
    await flush();

    expect(createPhaserGame).not.toHaveBeenCalled();
  });

  it('destroys the game after it is ready', async () => {
    const renderer = createPhaserRenderer(document.createElement('div'), options);
    await flush();
    renderer.destroy();

    expect(game.destroy).toHaveBeenCalledTimes(1);
  });

  it('logs a failed boot without rejecting and stays destroyable', async () => {
    const error = vi.spyOn(console, 'error').mockImplementation(() => {});
    createPhaserGame.mockImplementation(() => {
      throw new Error('no webgl');
    });
    const renderer = createPhaserRenderer(document.createElement('div'), options);
    await flush();

    expect(error).toHaveBeenCalledTimes(1);
    expect(() => renderer.destroy()).not.toThrow();
    error.mockRestore();
  });
});
