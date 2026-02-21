/**
 * Ancient Order - NPC API Routes
 *
 * Express route handlers for NPC-related endpoints.
 */

import { Router, Request, Response } from 'express';
import { ApiResponse, NPC, ErrorCodes } from '../types';
import { getActiveGameState } from './game';

/**
 * NPC data without full dialogue tree (for API responses)
 */
interface NpcSummary {
  id: string;
  name: string;
  archetype: string;
  faction: NPC['faction'];
  basePersonality: NPC['basePersonality'];
  affection: number;
  trust: number;
  joinableInTeam: boolean;
  availableLocations: string[];
  questsAvailable: string[];
  dialogueNodeCount: number;
}

/**
 * Transforms an NPC to a summary without the full dialogue tree.
 */
function toNpcSummary(npc: NPC): NpcSummary {
  return {
    id: npc.id,
    name: npc.name,
    archetype: npc.archetype,
    faction: npc.faction,
    basePersonality: npc.basePersonality,
    affection: npc.affection,
    trust: npc.trust,
    joinableInTeam: npc.joinableInTeam,
    availableLocations: npc.availableLocations,
    questsAvailable: npc.questsAvailable,
    dialogueNodeCount: npc.dialogueTree.length,
  };
}

/**
 * Creates and returns the NPC router with all NPC endpoints.
 */
export function createNpcRouter(): Router {
  const router = Router();

  /**
   * GET /api/npc/:npcId
   * Get NPC data (returns NPC without full dialogue tree for brevity)
   */
  router.get('/:npcId', (req: Request, res: Response) => {
    const { npcId } = req.params;

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

    // Return NPC summary (without full dialogue tree)
    const response: ApiResponse<NpcSummary> = {
      success: true,
      data: toNpcSummary(npc),
    };

    return res.json(response);
  });

  return router;
}
