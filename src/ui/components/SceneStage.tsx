import { useEffect, useRef } from 'react';
import type { CreateRenderer, StageRenderer } from '../../render/renderer';
import type { SceneKey } from '../../render/sceneKeys';
import { createPhaserRenderer } from '../../render/phaserRenderer';
import styles from './SceneStage.module.css';

interface SceneStageProps {
  scene: SceneKey;
  createRenderer?: CreateRenderer;
}

const FALLBACK_BACKGROUND = '#1a1410';

function readBackgroundColor(): string {
  const value = getComputedStyle(document.documentElement)
    .getPropertyValue('--color-bg-deep')
    .trim();
  return value || FALLBACK_BACKGROUND;
}

export function SceneStage({
  scene,
  createRenderer = createPhaserRenderer,
}: SceneStageProps) {
  const parentRef = useRef<HTMLDivElement>(null);
  const rendererRef = useRef<StageRenderer | null>(null);
  const sceneRef = useRef(scene);

  useEffect(() => {
    sceneRef.current = scene;
  });

  useEffect(() => {
    const parent = parentRef.current;
    if (!parent) return;
    const renderer = createRenderer(parent, {
      backgroundColor: readBackgroundColor(),
      initialScene: sceneRef.current,
    });
    rendererRef.current = renderer;
    return () => {
      renderer.destroy();
      rendererRef.current = null;
    };
  }, [createRenderer]);

  useEffect(() => {
    rendererRef.current?.showScene(scene);
  }, [scene]);

  return <div ref={parentRef} className={styles.stage} />;
}
