/**
 * Ancient Order - Game State Factory
 *
 * Provides the factory function for creating a fresh GameState at the start
 * of a new game session. All fields are populated with valid initial values.
 */

import { v4 as uuidv4 } from 'uuid';
import { GameState } from '../types/index.js';
import { createDefaultPersonality } from '../personality/personalitySystem.js';
import { NPC_TEMPLATES } from './npcs.js';

// ============================================================================
// Factory
// ============================================================================

/**
 * Creates a brand-new GameState with default values:
 * - A unique session ID
 * - A default balanced personality (all traits ~16.67%, sum = 100)
 * - All 3 NPC templates loaded into `npcs`
 * - combatState: null (no active combat)
 * - currentDialogueNode: null (no active dialogue)
 * - An empty conversation log
 * - saveSlot: null (not yet saved)
 * - timestamp: current epoch milliseconds
 */
export function createNewGameState(): GameState {
  return {
    player: {
      id: `player_${uuidv4()}`,
      name: 'Kael',
      personality: createDefaultPersonality(),
    },
    npcs: { ...NPC_TEMPLATES },
    currentDialogueNode: null,
    saveSlot: null,
    combatState: null,
    conversationLog: [],
    timestamp: Date.now(),
  };
}
