import styles from './StaminaBar.module.css';

interface StaminaBarProps {
  current: number;
  max: number;
  label: string;
}

type StaminaState = 'green' | 'yellow' | 'orange' | 'red' | 'black';

export function StaminaBar({ current, max, label }: StaminaBarProps) {
  const percentage = max > 0 ? Math.round((current / max) * 100) : 0;

  let staminaState: StaminaState;
  if (percentage > 60) {
    staminaState = 'green';
  } else if (percentage > 40) {
    staminaState = 'yellow';
  } else if (percentage > 20) {
    staminaState = 'orange';
  } else if (percentage > 0) {
    staminaState = 'red';
  } else {
    staminaState = 'black';
  }

  return (
    <div
      className={styles.container}
      role="progressbar"
      aria-valuenow={current}
      aria-valuemin={0}
      aria-valuemax={max}
      aria-label={`${label} stamina: ${current} of ${max}`}
    >
      <div className={styles.track}>
        <div
          className={styles.fill}
          data-state={staminaState}
          style={{ width: `${percentage}%` }}
        />
      </div>
      <span className={styles.label}>{current}/{max}</span>
    </div>
  );
}
