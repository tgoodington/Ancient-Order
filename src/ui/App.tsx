import { GameStateProvider } from './context/GameStateContext';
import { EquinoxHUD } from './components/EquinoxHUD';
import { SceneStage } from './components/SceneStage';
import { createPhaserRenderer } from '../render/phaserRenderer';
import type { CreateRenderer } from '../render/renderer';
import type { GameSession } from '../session/index';
import styles from './App.module.css';

interface AppProps {
  session: GameSession;
  createRenderer?: CreateRenderer;
}

export function App({ session, createRenderer = createPhaserRenderer }: AppProps) {
  return (
    <div className={styles.app}>
      <div className={styles.stageLayer} data-testid="stage-layer" aria-hidden="true">
        <SceneStage scene="stage" createRenderer={createRenderer} />
      </div>
      <div className={styles.hudLayer} data-testid="hud-layer">
        <GameStateProvider session={session}>
          <EquinoxHUD />
        </GameStateProvider>
      </div>
    </div>
  );
}
