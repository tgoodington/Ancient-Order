/**
 * Ancient Order - Save store
 *
 * Slot rules and save/load/list/remove over a SaveBackend. Throws coded Errors
 * (INVALID_SLOT, SAVE_NOT_FOUND, VALIDATION_ERROR, STORAGE_ERROR); the session maps them with fromThrown.
 */

import type { GameState } from '../types/index.js';
import { ErrorCodes } from '../types/index.js';
import type { SaveBackend } from './backends.js';
import { codedError } from './errors.js';
import { MAX_SLOT, MIN_SLOT, SAVE_FORMAT, SAVE_VERSION, isValidSlot, parseSave, saveKey } from './saveFormat.js';
import type { SaveEnvelope } from './saveFormat.js';
import { validateGameState } from './validation.js';

export { MAX_SLOT, MIN_SLOT };

export interface SaveMetadata {
  slot: number;
  timestamp: number;
  playerName: string;
}

export interface SaveSlotInfo {
  slot: number;
  // True only for loadable saves.
  exists: boolean;
  status: 'empty' | 'saved' | 'corrupted';
  metadata?: SaveMetadata;
}

export interface SaveStore {
  save(slot: number, game: GameState): Promise<SaveMetadata>;
  load(slot: number): Promise<GameState>;
  list(): Promise<SaveSlotInfo[]>;
  remove(slot: number): Promise<void>;
}

function assertSlot(slot: unknown): asserts slot is number {
  if (!isValidSlot(slot)) {
    throw codedError(
      ErrorCodes.INVALID_SLOT,
      `Invalid slot number: ${String(slot)}. Must be between ${MIN_SLOT} and ${MAX_SLOT}.`
    );
  }
}

export function createSaveStore(backend: SaveBackend): SaveStore {
  return {
    async save(slot, game) {
      assertSlot(slot);
      const validation = validateGameState(game);
      if (!validation.valid) {
        throw codedError(
          ErrorCodes.VALIDATION_ERROR,
          `Game state is invalid: ${validation.errors.join('; ')}`
        );
      }
      const envelope: SaveEnvelope = {
        format: SAVE_FORMAT,
        version: SAVE_VERSION,
        kind: 'manual',
        slot,
        savedAt: Date.now(),
        playerName: game.player.name,
        game,
      };
      await backend.write(saveKey('manual', slot), JSON.stringify(envelope));
      return { slot, timestamp: envelope.savedAt, playerName: envelope.playerName };
    },

    async load(slot) {
      assertSlot(slot);
      const raw = await backend.read(saveKey('manual', slot));
      if (raw === null) {
        throw codedError(ErrorCodes.SAVE_NOT_FOUND, `No save found in slot ${slot}.`);
      }
      const parsed = parseSave(raw);
      if (!parsed.ok) {
        throw codedError(
          ErrorCodes.VALIDATION_ERROR,
          `Save file in slot ${slot} is corrupted: ${parsed.reasons.join('; ')}`
        );
      }
      return parsed.envelope.game;
    },

    async list() {
      const results: SaveSlotInfo[] = [];
      for (let slot = MIN_SLOT; slot <= MAX_SLOT; slot++) {
        const raw = await backend.read(saveKey('manual', slot));
        if (raw === null) {
          results.push({ slot, exists: false, status: 'empty' });
          continue;
        }
        const parsed = parseSave(raw);
        if (!parsed.ok) {
          results.push({ slot, exists: false, status: 'corrupted' });
          continue;
        }
        const { savedAt, playerName } = parsed.envelope;
        results.push({
          slot,
          exists: true,
          status: 'saved',
          metadata: { slot, timestamp: savedAt, playerName },
        });
      }
      return results;
    },

    async remove(slot) {
      assertSlot(slot);
      const key = saveKey('manual', slot);
      if ((await backend.read(key)) === null) {
        throw codedError(ErrorCodes.SAVE_NOT_FOUND, `No save found in slot ${slot}.`);
      }
      await backend.remove(key);
    },
  };
}
