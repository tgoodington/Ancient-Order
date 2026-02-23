/**
 * Ancient Order - NPC Plugin
 *
 * Fastify plugin for NPC data access:
 *   GET /       — list all NPCs
 *   GET /:id    — get a specific NPC by ID
 *
 * NPC data is served from the NPC template registry (getAllNPCs / getNPC).
 * Registered with prefix /api/npc in src/api/index.ts.
 */

import { FastifyInstance } from 'fastify';
import { ApiResponse, ErrorCodes, NPC } from '../types/index.js';
import { getAllNPCs, getNPC } from '../state/npcs.js';

// ============================================================================
// Plugin
// ============================================================================

export async function npcPlugin(fastify: FastifyInstance): Promise<void> {
  // --------------------------------------------------------------------------
  // GET /
  // Return all NPC templates as an array.
  // --------------------------------------------------------------------------
  fastify.get('/', async (_request, reply): Promise<ApiResponse<NPC[]>> => {
    reply.code(200);
    return { success: true, data: getAllNPCs() };
  });

  // --------------------------------------------------------------------------
  // GET /:id
  // Return a specific NPC template by ID, or 404 if not found.
  // --------------------------------------------------------------------------
  fastify.get<{ Params: { id: string } }>('/:id', {
    schema: {
      params: {
        type: 'object',
        properties: {
          id: { type: 'string' },
        },
        required: ['id'],
      },
    },
  }, async (request, reply): Promise<ApiResponse<NPC>> => {
    const npc = getNPC(request.params.id);

    if (npc === undefined) {
      reply.code(404);
      return {
        success: false,
        error: {
          code: ErrorCodes.NPC_NOT_FOUND,
          message: `NPC not found: "${request.params.id}"`,
        },
      };
    }

    reply.code(200);
    return { success: true, data: npc };
  });
}
