/**
 * Ancient Order - Dialogue API Routes
 *
 * Express route handlers for dialogue-related endpoints.
 */

import { Router, Request, Response } from 'express';
import {
  ApiResponse,
  DialogueNodeResponse,
  DialogueChoiceRequest,
  DialogueChoiceResponse,
  ErrorCodes,
} from '../types';
import {
  getStartingNode,
  getDialogueNode,
  createDialogueNodeResponse,
  processDialogueSelection,
} from '../dialogue/dialogueEngine';
import { getActiveGameState, setActiveGameState } from './game';

/**
 * Creates and returns the Dialogue router with all dialogue endpoints.
 */
export function createDialogueRouter(): Router {
  const router = Router();

  /**
   * GET /api/dialogue/:npcId
   * Get current/starting dialogue node for NPC (with personality gate evaluation)
   * Query params:
   *   - nodeId (optional): Specific node ID to fetch, defaults to starting node
   */
  router.get('/:npcId', (req: Request, res: Response) => {
    const { npcId } = req.params;
    const nodeId = req.query.nodeId as string | undefined;

    // Get the active game state
    const gameState = getActiveGameState();

    if (!gameState) {
      const response: ApiResponse = {
        success: false,
        error: {
          code: ErrorCodes.GAME_NOT_FOUND,
          message: 'No active game found. Start a new game or load a save.',
        },
      };
      return res.status(404).json(response);
    }

    // Find the NPC
    const npc = gameState.npcs[npcId];

    if (!npc) {
      const response: ApiResponse = {
        success: false,
        error: {
          code: ErrorCodes.NPC_NOT_FOUND,
          message: `NPC not found: ${npcId}`,
          details: { npcId },
        },
      };
      return res.status(404).json(response);
    }

    // Get the dialogue node (specific or starting)
    let dialogueNode;
    if (nodeId) {
      dialogueNode = getDialogueNode(npc, nodeId);
      if (!dialogueNode) {
        const response: ApiResponse = {
          success: false,
          error: {
            code: ErrorCodes.DIALOGUE_NODE_NOT_FOUND,
            message: `Dialogue node not found: ${nodeId}`,
            details: { npcId, nodeId },
          },
        };
        return res.status(404).json(response);
      }
    } else {
      dialogueNode = getStartingNode(npc);
      if (!dialogueNode) {
        const response: ApiResponse = {
          success: false,
          error: {
            code: ErrorCodes.DIALOGUE_NODE_NOT_FOUND,
            message: `No dialogue available for NPC: ${npcId}`,
            details: { npcId },
          },
        };
        return res.status(404).json(response);
      }
    }

    // Create response with personality gate evaluation
    const dialogueResponse = createDialogueNodeResponse(
      dialogueNode,
      gameState.player.personality
    );

    const response: ApiResponse<DialogueNodeResponse> = {
      success: true,
      data: dialogueResponse,
    };

    return res.json(response);
  });

  /**
   * POST /api/dialogue/choose
   * Player selects a dialogue option
   * Body: DialogueChoiceRequest { npcId, optionId, currentNodeId }
   */
  router.post('/choose', (req: Request, res: Response) => {
    const choiceRequest = req.body as DialogueChoiceRequest;

    // Validate request body
    if (!choiceRequest.npcId || !choiceRequest.optionId || !choiceRequest.currentNodeId) {
      const response: ApiResponse = {
        success: false,
        error: {
          code: ErrorCodes.VALIDATION_ERROR,
          message: 'Missing required fields: npcId, optionId, and currentNodeId are required',
          details: {
            received: {
              npcId: !!choiceRequest.npcId,
              optionId: !!choiceRequest.optionId,
              currentNodeId: !!choiceRequest.currentNodeId,
            },
          },
        },
      };
      return res.status(400).json(response);
    }

    // Get the active game state
    const gameState = getActiveGameState();

    if (!gameState) {
      const response: ApiResponse = {
        success: false,
        error: {
          code: ErrorCodes.GAME_NOT_FOUND,
          message: 'No active game found. Start a new game or load a save.',
        },
      };
      return res.status(404).json(response);
    }

    // Process the dialogue selection
    const result = processDialogueSelection(
      gameState,
      choiceRequest.npcId,
      choiceRequest.currentNodeId,
      choiceRequest.optionId
    );

    if (!result.success || !result.result) {
      // Map error codes to HTTP status codes
      let statusCode = 400;
      if (result.errorCode === 'NPC_NOT_FOUND' || result.errorCode === 'DIALOGUE_NODE_NOT_FOUND') {
        statusCode = 404;
      } else if (result.errorCode === 'DIALOGUE_OPTION_NOT_AVAILABLE') {
        statusCode = 403;
      }

      const response: ApiResponse = {
        success: false,
        error: {
          code: result.errorCode || ErrorCodes.VALIDATION_ERROR,
          message: result.error || 'Unknown error occurred',
          details: {
            npcId: choiceRequest.npcId,
            currentNodeId: choiceRequest.currentNodeId,
            optionId: choiceRequest.optionId,
          },
        },
      };
      return res.status(statusCode).json(response);
    }

    // Persist the updated game state
    setActiveGameState(result.result.gameState);

    // Return the dialogue choice response
    const response: ApiResponse<DialogueChoiceResponse> = {
      success: true,
      data: result.result,
    };

    return res.json(response);
  });

  return router;
}
