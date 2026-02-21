/**
 * Ancient Order - Save/Load System
 *
 * File-based persistence for game state.
 * Saves to JSON files in the saves/ directory.
 */

import * as fs from 'fs';
import * as path from 'path';
import { GameState, SaveMetadata, SavedGame } from '../types';
import { validatePersonality } from '../personality/personalitySystem';

// Constants
const SAVES_DIRECTORY = path.join(process.cwd(), 'saves');
const MIN_SLOT = 1;
const MAX_SLOT = 10;

/**
 * Ensures the saves directory exists.
 */
function ensureSavesDirectory(): void {
  if (!fs.existsSync(SAVES_DIRECTORY)) {
    fs.mkdirSync(SAVES_DIRECTORY, { recursive: true });
  }
}

/**
 * Gets the file path for a save slot.
 */
function getSaveFilePath(slot: number): string {
  return path.join(SAVES_DIRECTORY, `slot_${slot}.json`);
}

/**
 * Validates a slot number.
 */
export function isValidSlot(slot: number): boolean {
  return Number.isInteger(slot) && slot >= MIN_SLOT && slot <= MAX_SLOT;
}

/**
 * Checks if a save slot exists.
 */
export function saveExists(slot: number): boolean {
  if (!isValidSlot(slot)) return false;
  return fs.existsSync(getSaveFilePath(slot));
}

/**
 * Validates a loaded game state.
 */
function validateGameState(state: unknown): state is GameState {
  if (!state || typeof state !== 'object') return false;

  const s = state as Record<string, unknown>;

  // Check required fields exist
  if (typeof s.id !== 'string') return false;
  if (typeof s.timestamp !== 'number') return false;
  if (typeof s.version !== 'string') return false;
  if (typeof s.currentLocation !== 'string') return false;
  if (!s.player || typeof s.player !== 'object') return false;
  if (!s.npcs || typeof s.npcs !== 'object') return false;

  // Validate player personality
  const player = s.player as Record<string, unknown>;
  if (!player.personality || typeof player.personality !== 'object') return false;

  const personality = player.personality as Record<string, number>;
  const validation = validatePersonality({
    patience: personality.patience ?? 0,
    empathy: personality.empathy ?? 0,
    cunning: personality.cunning ?? 0,
    logic: personality.logic ?? 0,
    kindness: personality.kindness ?? 0,
    charisma: personality.charisma ?? 0,
  });

  if (!validation.valid) {
    console.warn('Personality validation failed:', validation.errors);
    return false;
  }

  return true;
}

/**
 * Calculates playtime string from timestamps.
 */
function calculatePlaytime(startTimestamp: number, endTimestamp: number): string {
  const diffMs = endTimestamp - startTimestamp;
  const totalMinutes = Math.floor(diffMs / 60000);
  const hours = Math.floor(totalMinutes / 60);
  const minutes = totalMinutes % 60;
  return `${hours}:${minutes.toString().padStart(2, '0')}`;
}

/**
 * Saves the game state to a slot.
 *
 * @param gameState - The current game state to save
 * @param slot - The slot number (1-10)
 * @param slotName - Optional custom name for the save
 * @returns The save metadata
 */
export function saveGame(
  gameState: GameState,
  slot: number,
  slotName?: string
): SaveMetadata {
  if (!isValidSlot(slot)) {
    throw new Error(`Invalid slot number: ${slot}. Must be between ${MIN_SLOT} and ${MAX_SLOT}.`);
  }

  ensureSavesDirectory();

  const savedGame: SavedGame = {
    ...gameState,
    slotName: slotName ?? undefined,
    timestamp: Date.now(),
  };

  const filePath = getSaveFilePath(slot);
  fs.writeFileSync(filePath, JSON.stringify(savedGame, null, 2), 'utf-8');

  // Return metadata
  const metadata: SaveMetadata = {
    slot,
    slotName: slotName ?? null,
    playerName: gameState.player.name,
    location: gameState.currentLocation,
    playtime: calculatePlaytime(
      gameState.conversationLog[0]?.timestamp ?? gameState.timestamp,
      savedGame.timestamp
    ),
    savedAt: new Date(savedGame.timestamp).toISOString(),
    timestamp: savedGame.timestamp,
  };

  return metadata;
}

/**
 * Loads a game state from a slot.
 *
 * @param slot - The slot number (1-10)
 * @returns The loaded game state, or null if slot is empty
 */
export function loadGame(slot: number): GameState | null {
  if (!isValidSlot(slot)) {
    throw new Error(`Invalid slot number: ${slot}. Must be between ${MIN_SLOT} and ${MAX_SLOT}.`);
  }

  const filePath = getSaveFilePath(slot);

  if (!fs.existsSync(filePath)) {
    return null;
  }

  const fileContent = fs.readFileSync(filePath, 'utf-8');
  const savedGame = JSON.parse(fileContent) as SavedGame;

  // Validate the loaded state
  if (!validateGameState(savedGame)) {
    throw new Error(`Invalid save file in slot ${slot}. File may be corrupted.`);
  }

  // Remove the slotName field when returning (it's metadata only)
  const { slotName, ...gameState } = savedGame;

  return gameState as GameState;
}

/**
 * Lists all saved games with their metadata.
 *
 * @returns Array of save metadata for all existing saves
 */
export function listSaves(): SaveMetadata[] {
  ensureSavesDirectory();

  const saves: SaveMetadata[] = [];

  for (let slot = MIN_SLOT; slot <= MAX_SLOT; slot++) {
    const filePath = getSaveFilePath(slot);

    if (fs.existsSync(filePath)) {
      try {
        const fileContent = fs.readFileSync(filePath, 'utf-8');
        const savedGame = JSON.parse(fileContent) as SavedGame;

        if (validateGameState(savedGame)) {
          saves.push({
            slot,
            slotName: savedGame.slotName ?? null,
            playerName: savedGame.player.name,
            location: savedGame.currentLocation,
            playtime: calculatePlaytime(
              savedGame.conversationLog[0]?.timestamp ?? savedGame.timestamp,
              savedGame.timestamp
            ),
            savedAt: new Date(savedGame.timestamp).toISOString(),
            timestamp: savedGame.timestamp,
          });
        }
      } catch (error) {
        // Skip corrupted saves
        console.warn(`Failed to read save in slot ${slot}:`, error);
      }
    }
  }

  return saves;
}

/**
 * Deletes a saved game.
 *
 * @param slot - The slot number (1-10)
 * @returns True if deleted, false if slot was empty
 */
export function deleteSave(slot: number): boolean {
  if (!isValidSlot(slot)) {
    throw new Error(`Invalid slot number: ${slot}. Must be between ${MIN_SLOT} and ${MAX_SLOT}.`);
  }

  const filePath = getSaveFilePath(slot);

  if (!fs.existsSync(filePath)) {
    return false;
  }

  fs.unlinkSync(filePath);
  return true;
}

/**
 * Gets metadata for a single save slot.
 *
 * @param slot - The slot number (1-10)
 * @returns Save metadata, or null if slot is empty
 */
export function getSaveMetadata(slot: number): SaveMetadata | null {
  if (!isValidSlot(slot)) {
    throw new Error(`Invalid slot number: ${slot}. Must be between ${MIN_SLOT} and ${MAX_SLOT}.`);
  }

  const filePath = getSaveFilePath(slot);

  if (!fs.existsSync(filePath)) {
    return null;
  }

  try {
    const fileContent = fs.readFileSync(filePath, 'utf-8');
    const savedGame = JSON.parse(fileContent) as SavedGame;

    if (!validateGameState(savedGame)) {
      return null;
    }

    return {
      slot,
      slotName: savedGame.slotName ?? null,
      playerName: savedGame.player.name,
      location: savedGame.currentLocation,
      playtime: calculatePlaytime(
        savedGame.conversationLog[0]?.timestamp ?? savedGame.timestamp,
        savedGame.timestamp
      ),
      savedAt: new Date(savedGame.timestamp).toISOString(),
      timestamp: savedGame.timestamp,
    };
  } catch {
    return null;
  }
}
