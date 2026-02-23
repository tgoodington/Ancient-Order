/**
 * Ancient Order - Player Management Plugin
 *
 * Fastify plugin for player data and personality:
 *   GET  /          — get player character data
 *   POST /personality — apply a personality adjustment
 *
 * Registered with prefix /api/player in src/api/index.ts.
 *
 * Session state is read/written via fastify.gameStateContainer.state rather
 * than fastify.gameState. The container object is shared across all plugin
 * scopes, so mutations here are visible to all sibling plugins.
 */

import { FastifyInstance } from 'fastify';
import { ApiResponse, ErrorCodes, GameState, PersonalityAdjustment, PlayerCharacter } from '../types/index.js';
import { applyPersonalityAdjustment } from '../state/stateUpdaters.js';

// ============================================================================
// Plugin
// ============================================================================

export async function playerPlugin(fastify: FastifyInstance): Promise<void> {
  // --------------------------------------------------------------------------
  // GET /
  // Return the player character from the active game state.
  // --------------------------------------------------------------------------
  fastify.get('/', async (_request, reply): Promise<ApiResponse<PlayerCharacter>> => {
    if (fastify.gameStateContainer.state === null) {
      reply.code(404);
      return {
        success: false,
        error: { code: ErrorCodes.GAME_NOT_FOUND, message: 'No active game' },
      };
    }

    reply.code(200);
    return { success: true, data: fastify.gameStateContainer.state.player };
  });

  // --------------------------------------------------------------------------
  // POST /personality
  // Apply a partial personality adjustment to the player.
  // Body: a Partial<Personality> object with numeric trait deltas.
  // --------------------------------------------------------------------------
  fastify.post<{ Body: Record<string, unknown> }>('/personality', {
    schema: {
      body: {
        type: 'object',
        properties: {
          patience:  { type: 'number' },
          empathy:   { type: 'number' },
          cunning:   { type: 'number' },
          logic:     { type: 'number' },
          kindness:  { type: 'number' },
          charisma:  { type: 'number' },
        },
        additionalProperties: false,
        minProperties: 1,
      },
    },
  }, async (request, reply): Promise<ApiResponse<GameState>> => {
    if (fastify.gameStateContainer.state === null) {
      reply.code(404);
      return {
        success: false,
        error: { code: ErrorCodes.GAME_NOT_FOUND, message: 'No active game' },
      };
    }

    const adjustment = request.body as PersonalityAdjustment;
    const newState = applyPersonalityAdjustment(fastify.gameStateContainer.state, adjustment);
    fastify.gameStateContainer.state = newState;

    reply.code(200);
    return { success: true, data: newState };
  });
}
