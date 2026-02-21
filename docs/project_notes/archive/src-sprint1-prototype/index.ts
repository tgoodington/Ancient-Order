/**
 * Ancient Order - Main Export
 *
 * Re-exports all public modules for the game backend.
 */

// Types
export * from './types';

// Personality System
export {
  adjustPersonality,
  validatePersonality,
  getPersonalitySum,
  createDefaultPersonality,
  createPersonality,
  getPersonalityCategories,
  getPersonalityDiff,
} from './personality/personalitySystem';

// State Management
export { createNewGameState, createPlayerCharacter, getGameVersion } from './state/gameState';
export { createNPCs, getNPCTemplate, getAllNPCIds } from './state/npcs';
export * from './state/stateUpdaters';

// Dialogue Engine
export {
  evaluatePersonalityGate,
  isOptionAvailable,
  getDialogueNode,
  getStartingNode,
  getDialogueOption,
  createDialogueNodeResponse,
  validateNoDeadEnds,
  getAvailableOptions,
  processDialogueSelection,
  getFullDialogueTree,
  validateDialogueTree,
} from './dialogue/dialogueEngine';

// Persistence
export {
  saveGame,
  loadGame,
  listSaves,
  deleteSave,
  isValidSlot,
  saveExists,
  getSaveMetadata,
} from './persistence/saveLoad';

// API (for programmatic use)
export { createApp, startServer } from './api';
