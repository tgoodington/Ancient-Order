import Phaser from 'phaser';
import type { CreateRenderer, StageRenderer } from './renderer';
import { SCENE_KEYS, type SceneKey } from './sceneKeys';
import { SCENES } from './scenes';

export const createPhaserGame: CreateRenderer = (parent, options) => {
  const game = new Phaser.Game({
    type: Phaser.AUTO,
    parent,
    backgroundColor: options.backgroundColor,
    scale: { mode: Phaser.Scale.RESIZE },
    banner: false,
    audio: { noAudio: true },
    scene: [],
  });

  for (const key of SCENE_KEYS) {
    game.scene.add(key, SCENES[key], key === options.initialScene);
  }

  const renderer: StageRenderer = {
    showScene(key: SceneKey) {
      for (const other of SCENE_KEYS) {
        if (other !== key && game.scene.isActive(other)) game.scene.stop(other);
      }
      game.scene.start(key);
    },
    destroy() {
      game.destroy(true);
    },
  };
  return renderer;
};
