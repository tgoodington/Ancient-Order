import styles from './EnergyDisplay.module.css';

interface EnergyDisplayProps {
  current: number;
  max: number;
  label: string;
}

export function EnergyDisplay({ current, max, label }: EnergyDisplayProps) {
  const segments = Array.from({ length: max }, (_, i) =>
    i < current ? 'filled' : 'empty'
  );

  return (
    <div
      className={styles.container}
      role="meter"
      aria-valuenow={current}
      aria-valuemin={0}
      aria-valuemax={max}
      aria-label={`${label} energy: ${current} of ${max}`}
    >
      {segments.map((state, i) => (
        <div
          key={i}
          className={styles.segment}
          data-filled={state === 'filled'}
        />
      ))}
    </div>
  );
}
