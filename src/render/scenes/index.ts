import type Phaser from 'phaser';
import type { SceneKey } from '../sceneKeys';
import { StageScene } from './StageScene';

export const SCENES: Record<SceneKey, typeof Phaser.Scene> = {
  stage: StageScene,
};
