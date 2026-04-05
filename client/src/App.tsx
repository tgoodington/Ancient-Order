import { GameStateProvider } from './context/GameStateContext';
import { EquinoxHUD } from './components/EquinoxHUD';
import styles from './App.module.css';

export function App() {
  return (
    <div className={styles.app}>
      <GameStateProvider>
        <EquinoxHUD />
      </GameStateProvider>
    </div>
  );
}
