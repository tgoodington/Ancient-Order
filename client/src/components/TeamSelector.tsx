import { useCallback, useEffect, useState } from 'react';
import type { NPC } from '../types/index';
import styles from './TeamSelector.module.css';

interface TeamSelectorProps {
  currentTeam: readonly string[];
  npcs: NPC[];
  onSetTeam: (npcIds: [string, string]) => Promise<void>;
  disabled: boolean;
}

function setsEqual(a: Set<string>, b: Set<string>): boolean {
  return a.size === b.size && [...a].every(x => b.has(x));
}

function displayName(npc: NPC): string {
  const parts = npc.id.split('_');
  const name = parts[parts.length - 1];
  return name.charAt(0).toUpperCase() + name.slice(1);
}

export function TeamSelector({ currentTeam, npcs, onSetTeam, disabled }: TeamSelectorProps) {
  const [selectedIds, setSelectedIds] = useState<Set<string>>(() => new Set(currentTeam));
  const [submitting, setSubmitting] = useState(false);

  useEffect(() => {
    setSelectedIds(new Set(currentTeam));
  }, [currentTeam]);

  const canSubmit =
    selectedIds.size === 2 &&
    !submitting &&
    !disabled &&
    !setsEqual(selectedIds, new Set(currentTeam));

  const toggleNpc = useCallback((id: string) => {
    setSelectedIds(prev => {
      if (prev.has(id)) {
        const next = new Set(prev);
        next.delete(id);
        return next;
      }
      if (prev.size < 2) {
        return new Set([...prev, id]);
      }
      return prev;
    });
  }, []);

  const handleSubmit = useCallback(async () => {
    setSubmitting(true);
    try {
      await onSetTeam([...selectedIds] as [string, string]);
    } finally {
      setSubmitting(false);
    }
  }, [onSetTeam, selectedIds]);

  return (
    <div className={styles.container}>
      <h3 className={styles.heading}>Team</h3>
      <div className={styles.npcList} role="group" aria-label="Select team members">
        {npcs.map(npc => (
          <button
            key={npc.id}
            className={styles.npcButton}
            data-selected={selectedIds.has(npc.id)}
            onClick={() => toggleNpc(npc.id)}
            disabled={disabled || (selectedIds.size >= 2 && !selectedIds.has(npc.id))}
            aria-pressed={selectedIds.has(npc.id)}
          >
            <span className={styles.npcName}>{displayName(npc)}</span>
            <span className={styles.npcArchetype}>{npc.archetype}</span>
          </button>
        ))}
      </div>
      <button
        className={styles.confirmButton}
        onClick={handleSubmit}
        disabled={!canSubmit}
      >
        {submitting ? 'Setting...' : 'Confirm Team'}
      </button>
    </div>
  );
}
