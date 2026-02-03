/**
 * Ancient Order - Game Management API Routes
 *
 * Express router for game lifecycle management:
 * - New game initialization
 * - Game state retrieval
 * - Save/load operations
 */

import { Router, Request, Response } from 'express';
import {
  GameState,
  NewGameRequest,
  SaveGameRequest,
  SaveMetadata,
  ApiResponse,
  ErrorCodes,
} from '../types';
import { createNewGameState } from '../state/gameState';
import {
  saveGame,
  loadGame,
  listSaves,
  deleteSave,
  isValidSlot,
  saveExists,
} from '../persistence/saveLoad';

// ============================================================================
// Module-Level State
// ============================================================================

/**
 * The currently active game state for this session.
 * null when no game is loaded.
 */
let activeGameState: GameState | null = null;

/**
 * Gets the current active game state.
 */
export function getActiveGameState(): GameState | null {
  return activeGameState;
}

/**
 * Sets the active game state.
 * Used by other routers to update state after modifications.
 */
export function setActiveGameState(state: GameState | null): void {
  activeGameState = state;
}

// ============================================================================
// Route Handlers
// ============================================================================

/**
 * POST /api/game/new
 * Initialize a new game with the provided player name.
 */
function handleNewGame(req: Request, res: Response): void {
  const body = req.body as NewGameRequest;

  // Validate request
  if (!body.playerName || typeof body.playerName !== 'string') {
    const response: ApiResponse = {
      success: false,
      error: {
        code: ErrorCodes.VALIDATION_ERROR,
        message: 'Player name is required',
        details: { field: 'playerName' },
      },
    };
    res.status(400).json(response);
    return;
  }

  const trimmedName = body.playerName.trim();
  if (trimmedName.length === 0) {
    const response: ApiResponse = {
      success: false,
      error: {
        code: ErrorCodes.VALIDATION_ERROR,
        message: 'Player name cannot be empty',
        details: { field: 'playerName' },
      },
    };
    res.status(400).json(response);
    return;
  }

  // Create new game state
  const newGameRequest: NewGameRequest = {
    playerName: trimmedName,
    difficulty: body.difficulty,
  };

  activeGameState = createNewGameState(newGameRequest);

  const response: ApiResponse<GameState> = {
    success: true,
    data: activeGameState,
  };

  res.status(201).json(response);
}

/**
 * GET /api/game/state
 * Get the current game state.
 */
function handleGetState(req: Request, res: Response): void {
  if (!activeGameState) {
    const response: ApiResponse = {
      success: false,
      error: {
        code: ErrorCodes.GAME_NOT_FOUND,
        message: 'No active game. Start a new game or load a save.',
      },
    };
    res.status(404).json(response);
    return;
  }

  const response: ApiResponse<GameState> = {
    success: true,
    data: activeGameState,
  };

  res.json(response);
}

/**
 * POST /api/game/save/:slot
 * Save the current game to the specified slot (1-10).
 */
function handleSaveGame(req: Request, res: Response): void {
  const slot = parseInt(req.params.slot, 10);
  const body = req.body as SaveGameRequest;

  // Validate slot
  if (!isValidSlot(slot)) {
    const response: ApiResponse = {
      success: false,
      error: {
        code: ErrorCodes.INVALID_SLOT,
        message: 'Invalid save slot. Must be between 1 and 10.',
        details: { slot, validRange: { min: 1, max: 10 } },
      },
    };
    res.status(400).json(response);
    return;
  }

  // Check for active game
  if (!activeGameState) {
    const response: ApiResponse = {
      success: false,
      error: {
        code: ErrorCodes.GAME_NOT_FOUND,
        message: 'No active game to save. Start a new game or load a save first.',
      },
    };
    res.status(404).json(response);
    return;
  }

  try {
    const metadata = saveGame(activeGameState, slot, body.slotName);

    const response: ApiResponse<SaveMetadata> = {
      success: true,
      data: metadata,
    };

    res.json(response);
  } catch (error) {
    const response: ApiResponse = {
      success: false,
      error: {
        code: ErrorCodes.VALIDATION_ERROR,
        message: error instanceof Error ? error.message : 'Failed to save game',
      },
    };
    res.status(500).json(response);
  }
}

/**
 * GET /api/game/load/:slot
 * Load a game from the specified slot (1-10).
 */
function handleLoadGame(req: Request, res: Response): void {
  const slot = parseInt(req.params.slot, 10);

  // Validate slot
  if (!isValidSlot(slot)) {
    const response: ApiResponse = {
      success: false,
      error: {
        code: ErrorCodes.INVALID_SLOT,
        message: 'Invalid save slot. Must be between 1 and 10.',
        details: { slot, validRange: { min: 1, max: 10 } },
      },
    };
    res.status(400).json(response);
    return;
  }

  // Check if save exists
  if (!saveExists(slot)) {
    const response: ApiResponse = {
      success: false,
      error: {
        code: ErrorCodes.SAVE_NOT_FOUND,
        message: `No save found in slot ${slot}.`,
        details: { slot },
      },
    };
    res.status(404).json(response);
    return;
  }

  try {
    const loadedState = loadGame(slot);

    if (!loadedState) {
      const response: ApiResponse = {
        success: false,
        error: {
          code: ErrorCodes.SAVE_NOT_FOUND,
          message: `Failed to load save from slot ${slot}.`,
          details: { slot },
        },
      };
      res.status(404).json(response);
      return;
    }

    // Set as active game state
    activeGameState = loadedState;

    const response: ApiResponse<GameState> = {
      success: true,
      data: activeGameState,
    };

    res.json(response);
  } catch (error) {
    const response: ApiResponse = {
      success: false,
      error: {
        code: ErrorCodes.VALIDATION_ERROR,
        message: error instanceof Error ? error.message : 'Failed to load game',
        details: { slot },
      },
    };
    res.status(500).json(response);
  }
}

/**
 * GET /api/game/saves
 * List all saved games with their metadata.
 */
function handleListSaves(req: Request, res: Response): void {
  try {
    const saves = listSaves();

    const response: ApiResponse<SaveMetadata[]> = {
      success: true,
      data: saves,
    };

    res.json(response);
  } catch (error) {
    const response: ApiResponse = {
      success: false,
      error: {
        code: ErrorCodes.VALIDATION_ERROR,
        message: error instanceof Error ? error.message : 'Failed to list saves',
      },
    };
    res.status(500).json(response);
  }
}

/**
 * DELETE /api/game/saves/:slot
 * Delete a saved game from the specified slot.
 */
function handleDeleteSave(req: Request, res: Response): void {
  const slot = parseInt(req.params.slot, 10);

  // Validate slot
  if (!isValidSlot(slot)) {
    const response: ApiResponse = {
      success: false,
      error: {
        code: ErrorCodes.INVALID_SLOT,
        message: 'Invalid save slot. Must be between 1 and 10.',
        details: { slot, validRange: { min: 1, max: 10 } },
      },
    };
    res.status(400).json(response);
    return;
  }

  // Check if save exists
  if (!saveExists(slot)) {
    const response: ApiResponse = {
      success: false,
      error: {
        code: ErrorCodes.SAVE_NOT_FOUND,
        message: `No save found in slot ${slot}.`,
        details: { slot },
      },
    };
    res.status(404).json(response);
    return;
  }

  try {
    const deleted = deleteSave(slot);

    if (!deleted) {
      const response: ApiResponse = {
        success: false,
        error: {
          code: ErrorCodes.SAVE_NOT_FOUND,
          message: `Failed to delete save from slot ${slot}.`,
          details: { slot },
        },
      };
      res.status(404).json(response);
      return;
    }

    const response: ApiResponse<{ slot: number; deleted: boolean }> = {
      success: true,
      data: { slot, deleted: true },
    };

    res.json(response);
  } catch (error) {
    const response: ApiResponse = {
      success: false,
      error: {
        code: ErrorCodes.VALIDATION_ERROR,
        message: error instanceof Error ? error.message : 'Failed to delete save',
        details: { slot },
      },
    };
    res.status(500).json(response);
  }
}

// ============================================================================
// Router Factory
// ============================================================================

/**
 * Creates and configures the game management router.
 */
export function createGameRouter(): Router {
  const router = Router();

  // Game lifecycle
  router.post('/new', handleNewGame);
  router.get('/state', handleGetState);

  // Save/Load operations
  router.post('/save/:slot', handleSaveGame);
  router.get('/load/:slot', handleLoadGame);
  router.get('/saves', handleListSaves);
  router.delete('/saves/:slot', handleDeleteSave);

  return router;
}
