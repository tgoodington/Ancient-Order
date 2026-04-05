import styles from './LoadingScreen.module.css';

export function LoadingScreen() {
  return (
    <div className={styles.container} role="status" aria-label="Loading game state">
      <span className={styles.text}>Loading...</span>
    </div>
  );
}
