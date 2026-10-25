import type { GameState } from '../../types/index.js';
import { ErrorCodes } from '../../types/index.js';
import { isValidSlot } from '../../persistence/index.js';
import type { SaveMetadata, SaveSlotInfo, SaveStore } from '../../persistence/index.js';
import { fail, fromThrown, ok } from '../result.js';
import { startFresh } from '../sessionState.js';
import type { SessionState } from '../sessionState.js';
import { noActiveGame, refuse } from './outcome.js';
import type { Outcome } from './outcome.js';

function invalidSlot<T>(s: SessionState): Outcome<T> {
  return refuse(s, fail(ErrorCodes.INVALID_SLOT, 'Slot must be an integer between 1 and 10'));
}

export async function saveToSlot(
  s: SessionState,
  store: SaveStore,
  slot: unknown
): Promise<Outcome<SaveMetadata>> {
  if (!isValidSlot(slot)) return invalidSlot(s);
  if (s.game === null) return noActiveGame(s);
  try {
    return { next: s, result: ok(await store.save(slot, s.game)) };
  } catch (err) {
    return refuse(s, fromThrown(err));
  }
}

// Load replaces the whole game, so it commits startFresh(loaded) even if other operations
// committed while the read was in flight: the loaded save wins over those in-between changes.
export async function loadFromSlot(
  s: SessionState,
  store: SaveStore,
  slot: unknown
): Promise<Outcome<GameState>> {
  if (!isValidSlot(slot)) return invalidSlot(s);
  try {
    const game = await store.load(slot);
    return { next: startFresh(game), result: ok(game) };
  } catch (err) {
    return refuse(s, fromThrown(err));
  }
}

export async function listSlots(s: SessionState, store: SaveStore): Promise<Outcome<SaveSlotInfo[]>> {
  try {
    return { next: s, result: ok(await store.list()) };
  } catch (err) {
    return refuse(s, fromThrown(err));
  }
}

export async function deleteSlot(
  s: SessionState,
  store: SaveStore,
  slot: unknown
): Promise<Outcome<{ slot: number; deleted: true }>> {
  if (!isValidSlot(slot)) return invalidSlot(s);
  try {
    await store.remove(slot);
    return { next: s, result: ok({ slot, deleted: true as const }) };
  } catch (err) {
    return refuse(s, fromThrown(err));
  }
}
