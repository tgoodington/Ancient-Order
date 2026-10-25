export const SCENE_KEYS = ['stage'] as const;

export type SceneKey = (typeof SCENE_KEYS)[number];
