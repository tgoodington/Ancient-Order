/**
 * Ancient Order - Game Management Plugin
 *
 * Fastify plugin for game session management:
 *   POST /new       — create a new game
 *   GET  /state     — get current game state
 *   POST /save/:slot — save to a slot (1-10)
 *   POST /load/:slot — load from a slot (1-10)
 *
 * Registered with prefix /api/game in src/api/index.ts.
 *
 * Session state is read/written via fastify.gameStateContainer.state rather
 * than fastify.gameState. The container object is shared across all plugin
 * scopes, so mutations here are visible to all sibling plugins.
 */

import { FastifyInstance } from 'fastify';
import { ApiResponse, ErrorCodes, GameState } from '../types/index.js';
import { createNewGameState } from '../state/gameState.js';
import { saveGame, loadGame } from '../persistence/saveLoad.js';

// ============================================================================
// Internal helpers
// ============================================================================

/**
 * Returns true if the active game state is populated.
 * Reads from the shared container to ensure cross-plugin visibility.
 */
function hasActiveGame(fastify: FastifyInstance): boolean {
  return fastify.gameStateContainer.state !== null;
}

// ============================================================================
// Plugin
// ============================================================================

export async function gamePlugin(fastify: FastifyInstance): Promise<void> {
  // --------------------------------------------------------------------------
  // POST /new
  // Create a fresh game state and store it as the active session.
  // --------------------------------------------------------------------------
  fastify.post<{ Body: Record<string, unknown> }>('/new', {
    schema: {
      body: {
        type: 'object',
        properties: {
          playerName: { type: 'string' },
          difficulty: { type: 'string' },
        },
        additionalProperties: true,
      },
    },
  }, async (_request, reply): Promise<ApiResponse<GameState>> => {
    const state = createNewGameState();
    fastify.gameStateContainer.state = state;

    reply.code(200);
    return { success: true, data: state };
  });

  // --------------------------------------------------------------------------
  // GET /state
  // Return the active game state wrapped in an ApiResponse envelope.
  // --------------------------------------------------------------------------
  fastify.get('/state', async (_request, reply): Promise<ApiResponse<GameState>> => {
    if (!hasActiveGame(fastify)) {
      reply.code(404);
      return {
        success: false,
        error: { code: ErrorCodes.GAME_NOT_FOUND, message: 'No active game' },
      };
    }

    reply.code(200);
    return { success: true, data: fastify.gameStateContainer.state! };
  });

  // --------------------------------------------------------------------------
  // POST /save/:slot
  // Save the current game state to the given slot number.
  // --------------------------------------------------------------------------
  fastify.post<{ Params: { slot: string } }>('/save/:slot', {
    schema: {
      params: {
        type: 'object',
        properties: {
          slot: { type: 'string' },
        },
        required: ['slot'],
      },
    },
  }, async (request, reply): Promise<ApiResponse<{ slot: number; timestamp: number; playerName: string }>> => {
    if (!hasActiveGame(fastify)) {
      reply.code(404);
      return {
        success: false,
        error: { code: ErrorCodes.GAME_NOT_FOUND, message: 'No active game' },
      };
    }

    const slot = parseInt(request.params.slot, 10);
    if (isNaN(slot) || slot < 1 || slot > 10) {
      reply.code(400);
      return {
        success: false,
        error: { code: ErrorCodes.INVALID_SLOT, message: 'Slot must be an integer between 1 and 10' },
      };
    }

    const metadata = await saveGame(fastify.gameStateContainer.state!, slot);
    reply.code(200);
    return { success: true, data: metadata };
  });

  // --------------------------------------------------------------------------
  // POST /load/:slot
  // Load a game state from the given slot and replace the active session.
  // --------------------------------------------------------------------------
  fastify.post<{ Params: { slot: string } }>('/load/:slot', {
    schema: {
      params: {
        type: 'object',
        properties: {
          slot: { type: 'string' },
        },
        required: ['slot'],
      },
    },
  }, async (request, reply): Promise<ApiResponse<GameState>> => {
    const slot = parseInt(request.params.slot, 10);
    if (isNaN(slot) || slot < 1 || slot > 10) {
      reply.code(400);
      return {
        success: false,
        error: { code: ErrorCodes.INVALID_SLOT, message: 'Slot must be an integer between 1 and 10' },
      };
    }

    const state = await loadGame(slot);
    fastify.gameStateContainer.state = state;

    reply.code(200);
    return { success: true, data: state };
  });
}
