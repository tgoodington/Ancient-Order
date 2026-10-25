import type { GameState } from '../types/index.js';
import type { CombatAction } from '../types/combat.js';

export interface CombatOutcome {
  status: 'victory' | 'defeat';
  round: number;
}

export interface SessionState {
  readonly game: GameState | null;
  readonly pendingDeclarations: readonly CombatAction[];
  readonly lastCombatOutcome: CombatOutcome | null;
}

export const initialSessionState: SessionState = {
  game: null,
  pendingDeclarations: [],
  lastCombatOutcome: null,
};

export function startFresh(game: GameState): SessionState {
  return { game, pendingDeclarations: [], lastCombatOutcome: null };
}
