import type { SessionState } from '../sessionState.js';
import type { SessionResult } from '../result.js';
import { fail } from '../result.js';
import { ErrorCodes } from '../../types/index.js';

export interface Outcome<T> {
  next: SessionState;
  result: SessionResult<T>;
}

// A refusal or read keeps the same session state reference so the holder does not notify.
export function refuse<T>(s: SessionState, result: SessionResult<never>): Outcome<T> {
  return { next: s, result };
}

export function noActiveGame<T>(s: SessionState): Outcome<T> {
  return refuse(s, fail(ErrorCodes.GAME_NOT_FOUND, 'No active game'));
}
