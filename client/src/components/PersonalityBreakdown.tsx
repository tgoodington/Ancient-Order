import { useState } from 'react';
import type { Personality } from '../types/index';
import { PERSONALITY_TRAITS } from '../types/index';
import styles from './PersonalityBreakdown.module.css';

interface PersonalityBreakdownProps {
  personality: Personality;
}

function capitalize(s: string): string {
  return s.charAt(0).toUpperCase() + s.slice(1);
}

export function PersonalityBreakdown({ personality }: PersonalityBreakdownProps) {
  const [expanded, setExpanded] = useState(false);

  return (
    <div className={styles.container}>
      <button
        className={styles.header}
        onClick={() => setExpanded(!expanded)}
        aria-expanded={expanded}
        aria-controls="personality-details"
      >
        <span className={styles.headerText}>Personality</span>
        <span className={styles.chevron} data-expanded={expanded}>&#9660;</span>
      </button>
      {expanded && (
        <div
          id="personality-details"
          className={styles.details}
          role="region"
          aria-label="Personality traits"
        >
          {PERSONALITY_TRAITS.map(trait => (
            <div key={trait} className={styles.traitRow}>
              <span className={styles.traitName}>{capitalize(trait)}</span>
              <div className={styles.traitBar}>
                <div
                  className={styles.traitFill}
                  style={{
                    width: `${personality[trait]}%`,
                    backgroundColor: `var(--color-trait-${trait})`
                  }}
                />
              </div>
              <span className={styles.traitValue}>{personality[trait]}%</span>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
