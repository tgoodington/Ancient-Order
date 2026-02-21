/**
 * Ancient Order - Player API Routes
 *
 * Express route handlers for player-related endpoints.
 */

import { Router, Request, Response } from 'express';
import {
  ApiResponse,
  PlayerCharacter,
  PersonalityResponse,
  SetTeamRequest,
  SetTeamResponse,
  TeamMember,
  ErrorCodes,
} from '../types';
import { getPersonalityCategories } from '../personality/personalitySystem';
import { updateTeam } from '../state/stateUpdaters';
import { getActiveGameState, setActiveGameState } from './game';

const MAX_TEAM_SIZE = 2;

/**
 * Creates and returns the Player router with all player endpoints.
 */
export function createPlayerRouter(): Router {
  const router = Router();

  /**
   * GET /api/player
   * Get player character data
   */
  router.get('/', (req: Request, res: Response) => {
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

    const response: ApiResponse<PlayerCharacter> = {
      success: true,
      data: gameState.player,
    };

    return res.json(response);
  });

  /**
   * GET /api/player/personality
   * Get player personality with categories (wisdom, intelligence, charisma)
   */
  router.get('/personality', (req: Request, res: Response) => {
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

    const personality = gameState.player.personality;
    const categories = getPersonalityCategories(personality);

    const personalityResponse: PersonalityResponse = {
      personality,
      categories,
    };

    const response: ApiResponse<PersonalityResponse> = {
      success: true,
      data: personalityResponse,
    };

    return res.json(response);
  });

  /**
   * POST /api/player/team
   * Set team composition (max 2 NPCs)
   * Body: SetTeamRequest { npcIds: string[] }
   */
  router.post('/team', (req: Request, res: Response) => {
    const teamRequest = req.body as SetTeamRequest;

    // Validate request body
    if (!teamRequest.npcIds || !Array.isArray(teamRequest.npcIds)) {
      const response: ApiResponse = {
        success: false,
        error: {
          code: ErrorCodes.VALIDATION_ERROR,
          message: 'Missing required field: npcIds must be an array of NPC IDs',
          details: {
            received: typeof teamRequest.npcIds,
          },
        },
      };
      return res.status(400).json(response);
    }

    // Validate team size
    if (teamRequest.npcIds.length > MAX_TEAM_SIZE) {
      const response: ApiResponse = {
        success: false,
        error: {
          code: ErrorCodes.TEAM_COMPOSITION_INVALID,
          message: `Team cannot exceed ${MAX_TEAM_SIZE} NPCs`,
          details: {
            maxTeamSize: MAX_TEAM_SIZE,
            requestedSize: teamRequest.npcIds.length,
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

    // Validate all NPCs exist and are joinable
    const invalidNpcs: string[] = [];
    const unjoinableNpcs: string[] = [];

    for (const npcId of teamRequest.npcIds) {
      const npc = gameState.npcs[npcId];
      if (!npc) {
        invalidNpcs.push(npcId);
      } else if (!npc.joinableInTeam) {
        unjoinableNpcs.push(npcId);
      }
    }

    if (invalidNpcs.length > 0) {
      const response: ApiResponse = {
        success: false,
        error: {
          code: ErrorCodes.NPC_NOT_FOUND,
          message: `One or more NPCs not found: ${invalidNpcs.join(', ')}`,
          details: {
            invalidNpcs,
          },
        },
      };
      return res.status(404).json(response);
    }

    if (unjoinableNpcs.length > 0) {
      const response: ApiResponse = {
        success: false,
        error: {
          code: ErrorCodes.TEAM_COMPOSITION_INVALID,
          message: `One or more NPCs cannot join the team: ${unjoinableNpcs.join(', ')}`,
          details: {
            unjoinableNpcs,
          },
        },
      };
      return res.status(400).json(response);
    }

    // Update team composition
    const newGameState = updateTeam(gameState, teamRequest.npcIds);
    setActiveGameState(newGameState);

    // Build team member response
    const teamMembers: TeamMember[] = teamRequest.npcIds.map((npcId) => {
      const npc = newGameState.npcs[npcId];
      return {
        id: npc.id,
        name: npc.name,
        archetype: npc.archetype,
      };
    });

    const teamResponse: SetTeamResponse = {
      success: true,
      team: teamMembers,
      gameState: newGameState,
    };

    const response: ApiResponse<SetTeamResponse> = {
      success: true,
      data: teamResponse,
    };

    return res.json(response);
  });

  return router;
}
