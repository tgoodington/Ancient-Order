import type { GameState } from '../../types/index.js';
import { createNewGameState } from '../../state/gameState.js';
import type { SessionContent } from '../content.js';
import { fromThrown, ok } from '../result.js';
import { startFresh } from '../sessionState.js';
import type { SessionState } from '../sessionState.js';
import { noActiveGame, refuse } from './outcome.js';
import type { Outcome } from './outcome.js';

// The old POST /new accepted playerName/difficulty but ignored them; still ignored.
export function newGame(s: SessionState, _content: SessionContent): Outcome<GameState> {
  try {
    const game = createNewGameState();
    return { next: startFresh(game), result: ok(game) };
  } catch (err) {
    return refuse(s, fromThrown(err));
  }
}

export function getState(s: SessionState, _content: SessionContent): Outcome<GameState> {
  if (s.game === null) return noActiveGame(s);
  return { next: s, result: ok(s.game) };
}
