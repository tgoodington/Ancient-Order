/**
 * Ancient Order - Dialogue Plugin
 *
 * Fastify plugin for dialogue tree traversal:
 *   GET  /:npcId/start       — get starting dialogue node for an NPC
 *   POST /:npcId/choose      — process a player's dialogue option choice
 *
 * Dialogue trees are attached to NPC objects in the game state (gameState.npcs).
 * Registered with prefix /api/dialogue in src/api/index.ts.
 *
 * Session state is read/written via fastify.gameStateContainer.state rather
 * than fastify.gameState. The container object is shared across all plugin
 * scopes, so mutations here are visible to all sibling plugins.
 */

import { FastifyInstance } from 'fastify';
import { ApiResponse, ErrorCodes, DialogueNode, GameState } from '../types/index.js';
import { getNPC } from '../state/npcs.js';
import { processDialogueSelection } from '../dialogue/dialogueEngine.js';

// ============================================================================
// Request body type for /choose
// ============================================================================

interface ChooseBody {
  nodeId: string;
  optionId: string;
}

// ============================================================================
// Response type for /choose
// ============================================================================

interface ChooseResponse {
  state: GameState;
  nextNode: DialogueNode | null;
  selectedOptionId: string;
}

// ============================================================================
// Plugin
// ============================================================================

export async function dialoguePlugin(fastify: FastifyInstance): Promise<void> {
  // --------------------------------------------------------------------------
  // GET /:npcId/start
  // Returns the starting dialogue node for the given NPC.
  // The NPC must exist in the template registry.
  // --------------------------------------------------------------------------
  fastify.get<{ Params: { npcId: string } }>('/:npcId/start', {
    schema: {
      params: {
        type: 'object',
        properties: {
          npcId: { type: 'string' },
        },
        required: ['npcId'],
      },
    },
  }, async (request, reply): Promise<ApiResponse<DialogueNode>> => {
    if (fastify.gameStateContainer.state === null) {
      reply.code(404);
      return {
        success: false,
        error: { code: ErrorCodes.GAME_NOT_FOUND, message: 'No active game' },
      };
    }

    const npc = getNPC(request.params.npcId);
    if (npc === undefined) {
      reply.code(404);
      return {
        success: false,
        error: {
          code: ErrorCodes.NPC_NOT_FOUND,
          message: `NPC not found: "${request.params.npcId}"`,
        },
      };
    }

    // Retrieve the dialogue tree stored on the NPC entry in game state.
    // GameState.npcs is Record<string, NPC> but NPC doesn't have dialogueTree —
    // dialogue trees live in fixtures and are passed to the engine per-call.
    // For Sprint 1 the NPC templates don't ship with a full dialogue tree,
    // so we return the greet node ID convention as a pointer back to the client.
    // If the NPC has no attached dialogue tree, return an empty-options node
    // so callers know the greet node ID to use with external fixtures.
    const greetNodeId = `${npc.id}_greet`;

    reply.code(200);
    return {
      success: true,
      data: {
        id: greetNodeId,
        npcId: npc.id,
        text: '',
        options: [],
      },
    };
  });

  // --------------------------------------------------------------------------
  // POST /:npcId/choose
  // Processes a player dialogue choice.
  // Requires the client to supply the dialogue tree in the request body
  // because NPC templates don't embed full dialogue trees in Sprint 1.
  //
  // Body:
  //   nodeId      — current node ID
  //   optionId    — chosen option ID
  //   dialogueTree — the full DialogueNode[] for this NPC (provided by client)
  // --------------------------------------------------------------------------
  fastify.post<{
    Params: { npcId: string };
    Body: ChooseBody & { dialogueTree: DialogueNode[] };
  }>('/:npcId/choose', {
    schema: {
      params: {
        type: 'object',
        properties: {
          npcId: { type: 'string' },
        },
        required: ['npcId'],
      },
      body: {
        type: 'object',
        properties: {
          nodeId:       { type: 'string' },
          optionId:     { type: 'string' },
          dialogueTree: { type: 'array' },
        },
        required: ['nodeId', 'optionId', 'dialogueTree'],
        additionalProperties: false,
      },
    },
  }, async (request, reply): Promise<ApiResponse<ChooseResponse>> => {
    if (fastify.gameStateContainer.state === null) {
      reply.code(404);
      return {
        success: false,
        error: { code: ErrorCodes.GAME_NOT_FOUND, message: 'No active game' },
      };
    }

    const { npcId } = request.params;
    const { nodeId, optionId, dialogueTree } = request.body;

    const npc = getNPC(npcId);
    if (npc === undefined) {
      reply.code(404);
      return {
        success: false,
        error: {
          code: ErrorCodes.NPC_NOT_FOUND,
          message: `NPC not found: "${npcId}"`,
        },
      };
    }

    const result = processDialogueSelection(
      fastify.gameStateContainer.state,
      npcId,
      nodeId,
      optionId,
      dialogueTree
    );

    fastify.gameStateContainer.state = result.state;

    reply.code(200);
    return {
      success: true,
      data: {
        state: result.state,
        nextNode: result.nextNode,
        selectedOptionId: result.selectedOption.id,
      },
    };
  });
}
