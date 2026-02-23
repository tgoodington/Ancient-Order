/**
 * Ancient Order - Persistence Layer
 *
 * JSON file-based save/load for game state.
 * Saves to `saves/slot_N.json` files (slots 1–10).
 * All I/O is async (fs/promises).
 */

import * as fs from 'fs/promises';
import * as path from 'path';
import { GameState, ErrorCodes } from '../types/index.js';

// ============================================================================
// Constants
// ============================================================================

export const SAVES_DIRECTORY = 'saves';
export const MIN_SLOT = 1;
export const MAX_SLOT = 10;

// ============================================================================
// Local Types
// ============================================================================

/**
 * Metadata returned after a successful save operation.
 */
export interface SaveMetadata {
  slot: number;
  timestamp: number;
  playerName: string;
}

/**
 * Slot status as returned by listSaves().
 */
export interface SaveSlotInfo {
  slot: number;
  exists: boolean;
  metadata?: SaveMetadata;
}

// ============================================================================
// Internal Helpers
// ============================================================================

/**
 * Resolves the absolute file path for a given slot, relative to cwd.
 * The saves directory base can be overridden via the `savesDir` parameter
 * to support temp-directory testing.
 */
function getSaveFilePath(slot: number, savesDir: string = SAVES_DIRECTORY): string {
  return path.join(savesDir, `slot_${slot}.json`);
}

/**
 * Throws a typed error for an out-of-range slot number.
 */
function throwInvalidSlot(slot: number): never {
  const err = new Error(
    `Invalid slot number: ${slot}. Must be between ${MIN_SLOT} and ${MAX_SLOT}.`
  );
  (err as NodeJS.ErrnoException & { code: string }).code = ErrorCodes.INVALID_SLOT;
  throw err;
}

/**
 * Throws a typed error when a save file does not exist.
 */
function throwSaveNotFound(slot: number): never {
  const err = new Error(`No save found in slot ${slot}.`);
  (err as NodeJS.ErrnoException & { code: string }).code = ErrorCodes.SAVE_NOT_FOUND;
  throw err;
}

// ============================================================================
// validateGameState — Runtime Type Guard
// ============================================================================

/**
 * Runtime type guard for data loaded from disk.
 * Validates structural shape; does not re-run personality math.
 * GameState.combatState is unknown | null, so any value (including null) passes.
 */
export function validateGameState(data: unknown): data is GameState {
  if (data === null || typeof data !== 'object') return false;

  const s = data as Record<string, unknown>;

  // Top-level required fields
  if (typeof s.timestamp !== 'number') return false;
  if (s.currentDialogueNode !== null && typeof s.currentDialogueNode !== 'string') return false;
  if (s.saveSlot !== null && typeof s.saveSlot !== 'number') return false;
  if (!Array.isArray(s.conversationLog)) return false;

  // player sub-object
  if (!s.player || typeof s.player !== 'object') return false;
  const player = s.player as Record<string, unknown>;
  if (typeof player.id !== 'string') return false;
  if (typeof player.name !== 'string') return false;
  if (!player.personality || typeof player.personality !== 'object') return false;

  // personality traits
  const p = player.personality as Record<string, unknown>;
  const traits = ['patience', 'empathy', 'cunning', 'logic', 'kindness', 'charisma'];
  for (const trait of traits) {
    if (typeof p[trait] !== 'number') return false;
  }

  // npcs — Record<string, NPC>
  if (!s.npcs || typeof s.npcs !== 'object' || Array.isArray(s.npcs)) return false;
  const npcs = s.npcs as Record<string, unknown>;
  for (const npcEntry of Object.values(npcs)) {
    if (!npcEntry || typeof npcEntry !== 'object') return false;
    const npc = npcEntry as Record<string, unknown>;
    if (typeof npc.id !== 'string') return false;
    if (typeof npc.archetype !== 'string') return false;
    if (typeof npc.affection !== 'number') return false;
    if (typeof npc.trust !== 'number') return false;
    if (!npc.personality || typeof npc.personality !== 'object') return false;
    const np = npc.personality as Record<string, unknown>;
    for (const trait of traits) {
      if (typeof np[trait] !== 'number') return false;
    }
  }

  // combatState: unknown | null — any value is acceptable
  return true;
}

// ============================================================================
// Public API
// ============================================================================

/**
 * Saves the current game state to the given slot.
 *
 * @param state   The current game state.
 * @param slot    Save slot (1–10).
 * @param savesDir  Directory override for testing (defaults to SAVES_DIRECTORY).
 * @returns       Metadata for the saved game.
 */
export async function saveGame(
  state: GameState,
  slot: number,
  savesDir: string = SAVES_DIRECTORY
): Promise<SaveMetadata> {
  if (slot < MIN_SLOT || slot > MAX_SLOT || !Number.isInteger(slot)) {
    throwInvalidSlot(slot);
  }

  // Ensure directory exists
  await fs.mkdir(savesDir, { recursive: true });

  const timestamp = Date.now();
  const filePath = getSaveFilePath(slot, savesDir);

  await fs.writeFile(filePath, JSON.stringify(state, null, 2), 'utf-8');

  return {
    slot,
    timestamp,
    playerName: state.player.name,
  };
}

/**
 * Loads a game state from the given slot.
 *
 * @param slot     Save slot (1–10).
 * @param savesDir  Directory override for testing.
 * @returns        The deserialized GameState.
 * @throws         ErrorCodes.INVALID_SLOT if slot is out of range.
 * @throws         ErrorCodes.SAVE_NOT_FOUND if the file does not exist.
 */
export async function loadGame(
  slot: number,
  savesDir: string = SAVES_DIRECTORY
): Promise<GameState> {
  if (slot < MIN_SLOT || slot > MAX_SLOT || !Number.isInteger(slot)) {
    throwInvalidSlot(slot);
  }

  const filePath = getSaveFilePath(slot, savesDir);

  let raw: string;
  try {
    raw = await fs.readFile(filePath, 'utf-8');
  } catch {
    throwSaveNotFound(slot);
  }

  const data: unknown = JSON.parse(raw!);

  if (!validateGameState(data)) {
    const err = new Error(`Save file in slot ${slot} is corrupted or has an invalid format.`);
    (err as NodeJS.ErrnoException & { code: string }).code = ErrorCodes.SAVE_NOT_FOUND;
    throw err;
  }

  return data;
}

/**
 * Returns the status and metadata for all 10 save slots.
 *
 * @param savesDir  Directory override for testing.
 * @returns         Array of SaveSlotInfo for slots 1–10.
 */
export async function listSaves(savesDir: string = SAVES_DIRECTORY): Promise<SaveSlotInfo[]> {
  // Ensure directory exists before listing
  await fs.mkdir(savesDir, { recursive: true });

  const results: SaveSlotInfo[] = [];

  for (let slot = MIN_SLOT; slot <= MAX_SLOT; slot++) {
    const filePath = getSaveFilePath(slot, savesDir);

    try {
      const raw = await fs.readFile(filePath, 'utf-8');
      const data: unknown = JSON.parse(raw);

      if (validateGameState(data)) {
        const metadata: SaveMetadata = {
          slot,
          timestamp: data.timestamp,
          playerName: data.player.name,
        };
        results.push({ slot, exists: true, metadata });
      } else {
        // File exists but is corrupted — treat as absent
        results.push({ slot, exists: false });
      }
    } catch {
      results.push({ slot, exists: false });
    }
  }

  return results;
}

/**
 * Deletes a save file from the given slot.
 *
 * @param slot     Save slot (1–10).
 * @param savesDir  Directory override for testing.
 * @throws         ErrorCodes.INVALID_SLOT if slot is out of range.
 * @throws         ErrorCodes.SAVE_NOT_FOUND if the file does not exist.
 */
export async function deleteSave(
  slot: number,
  savesDir: string = SAVES_DIRECTORY
): Promise<void> {
  if (slot < MIN_SLOT || slot > MAX_SLOT || !Number.isInteger(slot)) {
    throwInvalidSlot(slot);
  }

  const filePath = getSaveFilePath(slot, savesDir);

  try {
    await fs.unlink(filePath);
  } catch {
    throwSaveNotFound(slot);
  }
}
