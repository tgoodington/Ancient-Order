import type { CreateRenderer, StageRenderer } from './renderer';

export const createPhaserRenderer: CreateRenderer = (parent, options) => {
  let inner: StageRenderer | null = null;
  let destroyed = false;
  let current = options.initialScene;

  import('./phaserGame')
    .then(({ createPhaserGame }) => {
      if (destroyed) return;
      inner = createPhaserGame(parent, options);
      if (current !== options.initialScene) inner.showScene(current);
    })
    .catch((error: unknown) => {
      console.error('Failed to start the Phaser stage', error);
    });

  return {
    showScene(key) {
      if (destroyed || key === current) return;
      current = key;
      inner?.showScene(key);
    },
    destroy() {
      if (destroyed) return;
      destroyed = true;
      inner?.destroy();
      inner = null;
    },
  };
};
