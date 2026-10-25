import type { SceneKey } from './sceneKeys';

export type StageRenderer = {
  showScene(key: SceneKey): void;
  destroy(): void;
};

export type CreateRenderer = (
  parent: HTMLElement,
  options: { backgroundColor: string; initialScene: SceneKey },
) => StageRenderer;
