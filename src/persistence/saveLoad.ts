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
// validateGameState — Runtime Validation
// ============================================================================

export type ValidationResult =
  | { valid: true }
  | { valid: false; errors: string[] };

/**
 * Validates data loaded from disk against the GameState shape.
 * Accumulates all errors and returns them in a ValidationResult.
 * Returns { valid: true } if data passes all checks.
 */
export function validateGameState(data: unknown): ValidationResult {
  const errors: string[] = [];

  // Phase 1: Structural checks
  if (data === null || typeof data !== 'object') {
    errors.push('GameState must be a non-null object');
    return { valid: false, errors };
  }

  const s = data as Record<string, unknown>;

  if (typeof s.timestamp !== 'number') {
    errors.push(`timestamp: expected number, got ${typeof s.timestamp}`);
  }
  if (s.currentDialogueNode !== null && typeof s.currentDialogueNode !== 'string') {
    errors.push('currentDialogueNode: expected string or null');
  }
  if (s.saveSlot !== null && typeof s.saveSlot !== 'number') {
    errors.push('saveSlot: expected number or null');
  }
  if (!Array.isArray(s.conversationLog)) {
    errors.push('conversationLog: expected array');
  }
  if (!s.player || typeof s.player !== 'object') {
    errors.push('player: expected object');
  }

  const traits = ['patience', 'empathy', 'cunning', 'logic', 'kindness', 'charisma'];

  if (s.player && typeof s.player === 'object') {
    const player = s.player as Record<string, unknown>;
    if (typeof player.id !== 'string') {
      errors.push('player.id: expected string');
    }
    if (typeof player.name !== 'string') {
      errors.push('player.name: expected string');
    }
    if (!player.personality || typeof player.personality !== 'object') {
      errors.push('player.personality: expected object');
    }

    // Phase 2: Personality deep validation
    if (player.personality && typeof player.personality === 'object') {
      const p = player.personality as Record<string, unknown>;
      let allTraitsValid = true;
      let sum = 0;
      for (const trait of traits) {
        if (typeof p[trait] !== 'number') {
          errors.push(`player.personality.${trait}: expected number, got ${typeof p[trait]}`);
          allTraitsValid = false;
        } else {
          const val = p[trait] as number;
          if (val < 5) {
            errors.push(`player.personality.${trait}: value ${val} is below minimum 5`);
            allTraitsValid = false;
          } else if (val > 35) {
            errors.push(`player.personality.${trait}: value ${val} is above maximum 35`);
            allTraitsValid = false;
          } else {
            sum += val;
          }
        }
      }
      if (allTraitsValid && Math.abs(sum - 100) > 0.01) {
        errors.push(`player.personality: sum ${sum} does not equal 100 (tolerance 0.01)`);
      }
    }
  }

  // Phase 3: NPC validation
  if (!s.npcs || typeof s.npcs !== 'object' || Array.isArray(s.npcs)) {
    errors.push('npcs: expected object');
  } else {
    const npcs = s.npcs as Record<string, unknown>;
    for (const [key, npcEntry] of Object.entries(npcs)) {
      if (!npcEntry || typeof npcEntry !== 'object') {
        errors.push(`npcs.${key}: expected object`);
        continue;
      }
      const npc = npcEntry as Record<string, unknown>;
      if (typeof npc.id !== 'string') {
        errors.push(`npcs.${key}.id: expected string`);
      }
      if (typeof npc.archetype !== 'string') {
        errors.push(`npcs.${key}.archetype: expected string`);
      }
      if (typeof npc.affection !== 'number') {
        errors.push(`npcs.${key}.affection: expected number`);
      }
      if (typeof npc.trust !== 'number') {
        errors.push(`npcs.${key}.trust: expected number`);
      }
      if (!npc.personality || typeof npc.personality !== 'object') {
        errors.push(`npcs.${key}.personality: expected object`);
      } else {
        const np = npc.personality as Record<string, unknown>;
        for (const trait of traits) {
          if (typeof np[trait] !== 'number') {
            errors.push(`npcs.${key}.personality.${trait}: expected number, got ${typeof np[trait]}`);
          }
        }
      }
    }
  }

  // Phase 4: Team validation
  if (s.team !== undefined) {
    if (!Array.isArray(s.team)) {
      errors.push('team: expected array');
    } else {
      const team = s.team as unknown[];
      if (team.length !== 0 && team.length !== 2) {
        errors.push(`team: must contain exactly 0 or 2 NPC IDs, got ${team.length}`);
      } else if (team.length === 2) {
        for (let i = 0; i < team.length; i++) {
          if (typeof team[i] !== 'string') {
            errors.push(`team[${i}]: expected string`);
          }
        }
      }
    }
  }

  // Phase 5: Combat state validation
  if (s.combatState !== null && s.combatState !== undefined) {
    if (typeof s.combatState !== 'object') {
      errors.push('combatState: expected object or null');
    } else {
      const cs = s.combatState as Record<string, unknown>;
      if (typeof cs.round !== 'number') {
        errors.push('combatState.round: expected number');
      }
      const validPhases = ['AI_DECISION', 'VISUAL_INFO', 'PC_DECLARATION', 'ACTION_RESOLUTION', 'PER_ATTACK'];
      if (!validPhases.includes(cs.phase as string)) {
        errors.push(`combatState.phase: invalid value '${cs.phase}', expected one of AI_DECISION, VISUAL_INFO, PC_DECLARATION, ACTION_RESOLUTION, PER_ATTACK`);
      }
      const validStatuses = ['active', 'victory', 'defeat'];
      if (!validStatuses.includes(cs.status as string)) {
        errors.push(`combatState.status: invalid value '${cs.status}', expected one of active, victory, defeat`);
      }
      if (!Array.isArray(cs.playerParty)) {
        errors.push('combatState.playerParty: expected array');
      }
      if (!Array.isArray(cs.enemyParty)) {
        errors.push('combatState.enemyParty: expected array');
      }
      if (!Array.isArray(cs.actionQueue)) {
        errors.push('combatState.actionQueue: expected array');
      }
      if (!Array.isArray(cs.roundHistory)) {
        errors.push('combatState.roundHistory: expected array');
      }
    }
  }

  // Phase 6: Narrative state validation
  if (s.narrativeState !== null && s.narrativeState !== undefined) {
    if (typeof s.narrativeState !== 'object') {
      errors.push('narrativeState: expected object or null');
    } else {
      const ns = s.narrativeState as Record<string, unknown>;
      if (typeof ns.currentSceneId !== 'string') {
        errors.push('narrativeState.currentSceneId: expected string');
      }
      if (!Array.isArray(ns.visitedSceneIds)) {
        errors.push('narrativeState.visitedSceneIds: expected array');
      } else {
        const visited = ns.visitedSceneIds as unknown[];
        for (let i = 0; i < visited.length; i++) {
          if (typeof visited[i] !== 'string') {
            errors.push(`narrativeState.visitedSceneIds[${i}]: expected string`);
          }
        }
      }
      if (!ns.choiceFlags || typeof ns.choiceFlags !== 'object' || Array.isArray(ns.choiceFlags)) {
        errors.push('narrativeState.choiceFlags: expected object');
      } else {
        const flags = ns.choiceFlags as Record<string, unknown>;
        for (const [flagKey, flagVal] of Object.entries(flags)) {
          if (typeof flagVal !== 'boolean') {
            errors.push(`narrativeState.choiceFlags.${flagKey}: expected boolean, got ${typeof flagVal}`);
          }
        }
      }
      if (!Array.isArray(ns.sceneHistory)) {
        errors.push('narrativeState.sceneHistory: expected array');
      } else {
        const history = ns.sceneHistory as unknown[];
        for (let i = 0; i < history.length; i++) {
          const entry = history[i] as Record<string, unknown>;
          if (typeof entry.sceneId !== 'string') {
            errors.push(`narrativeState.sceneHistory[${i}].sceneId: expected string`);
          }
          if (typeof entry.choiceId !== 'string') {
            errors.push(`narrativeState.sceneHistory[${i}].choiceId: expected string`);
          }
          if (typeof entry.timestamp !== 'number') {
            errors.push(`narrativeState.sceneHistory[${i}].timestamp: expected number`);
          }
        }
      }
    }
  }

  if (errors.length === 0) {
    return { valid: true };
  }
  return { valid: false, errors };
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

  const validation = validateGameState(data);
  if (!validation.valid) {
    const err = new Error(`Save file in slot ${slot} is corrupted: ${validation.errors.join('; ')}`);
    (err as NodeJS.ErrnoException & { code: string }).code = ErrorCodes.VALIDATION_ERROR;
    throw err;
  }

  // Normalize missing narrativeState to null for backward compatibility
  if ((data as Record<string, unknown>).narrativeState === undefined) {
    (data as Record<string, unknown>).narrativeState = null;
  }

  // Normalize missing team to [] for backward compatibility
  if ((data as Record<string, unknown>).team === undefined) {
    (data as Record<string, unknown>).team = [];
  }

  return data as GameState;
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

      const validation = validateGameState(data);
      if (validation.valid) {
        const validData = data as GameState;
        const metadata: SaveMetadata = {
          slot,
          timestamp: validData.timestamp,
          playerName: validData.player.name,
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
