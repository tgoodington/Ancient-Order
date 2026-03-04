/**
 * Ancient Order - Narrative Plugin
 *
 * Fastify plugin for narrative scene management:
 *   GET  /start/:startingSceneId — initialize narrative state
 *   GET  /current                — get current scene with available choices
 *   POST /choose                 — process a scene choice and advance narrative
 *   GET  /state                  — get current narrative state
 *   POST /reset                  — clear narrative state
 *   GET  /synergy                — get current party synergy bonus
 *
 * Registered with prefix /api/narrative in src/api/index.ts.
 *
 * Scene graphs are loaded from the TEST_SCENE_GRAPH fixture at module level
 * for Sprint 3. The engine reads scene data as SceneGraph passed as parameter. [A2]
 */

import { FastifyInstance } from 'fastify';
import { ApiResponse, ErrorCodes } from '../types/index.js';
import type { NarrativeState, Scene, CurrentSceneResult, SynergyResult } from '../types/narrative.js';
import { initializeNarrative, updateNarrativeState, clearNarrative } from '../state/stateUpdaters.js';
import { getCurrentScene } from '../narrative/sceneEngine.js';
import { advanceNarrative } from '../narrative/narrativeStateMachine.js';
import { applyConsequence } from '../narrative/choiceEngine.js';
import { calculateSynergy } from '../narrative/synergyCalculator.js';
import { DEFAULT_PARADIGMS } from '../fixtures/synergyConfig.js';
import { TEST_SCENE_GRAPH } from '../narrative/fixtures.js';

// ============================================================================
// Module-level scene graph (Sprint 3: use test fixture)
// ============================================================================

/**
 * Scene graph loaded at module registration time.
 * Sprint 3 uses the test fixture graph.
 * Task 11 (narrative content authoring) will replace this with authored JSON.
 */
const SCENE_GRAPH = TEST_SCENE_GRAPH;

// ============================================================================
// Plugin
// ============================================================================

export async function narrativePlugin(fastify: FastifyInstance): Promise<void> {
  // --------------------------------------------------------------------------
  // GET /start/:startingSceneId
  // Initialize narrative state with the given starting scene.
  // --------------------------------------------------------------------------
  fastify.get<{ Params: { startingSceneId: string } }>('/start/:startingSceneId', {
    schema: {
      params: {
        type: 'object',
        properties: {
          startingSceneId: { type: 'string', maxLength: 128 },
        },
        required: ['startingSceneId'],
      },
    },
  }, async (request, reply): Promise<ApiResponse<{ narrativeState: NarrativeState }>> => {
    if (fastify.gameStateContainer.state === null) {
      reply.code(404);
      return {
        success: false,
        error: { code: ErrorCodes.GAME_NOT_FOUND, message: 'No active game' },
      };
    }

    const { startingSceneId } = request.params;
    const newState = initializeNarrative(fastify.gameStateContainer.state, startingSceneId);
    fastify.gameStateContainer.state = newState;

    reply.code(200);
    return {
      success: true,
      data: { narrativeState: newState.narrativeState! },
    };
  });

  // --------------------------------------------------------------------------
  // GET /current
  // Get current scene with available choices filtered by player personality.
  // --------------------------------------------------------------------------
  fastify.get('/current', async (_request, reply): Promise<ApiResponse<CurrentSceneResult>> => {
    if (fastify.gameStateContainer.state === null) {
      reply.code(404);
      return {
        success: false,
        error: { code: ErrorCodes.GAME_NOT_FOUND, message: 'No active game' },
      };
    }

    const gameState = fastify.gameStateContainer.state;

    if (gameState.narrativeState === null) {
      reply.code(400);
      return {
        success: false,
        error: {
          code: ErrorCodes.NARRATIVE_NOT_STARTED,
          message: 'Narrative has not been started',
        },
      };
    }

    const result = getCurrentScene(
      gameState.narrativeState,
      gameState.player.personality,
      SCENE_GRAPH
    );

    if (result === null) {
      reply.code(404);
      return {
        success: false,
        error: {
          code: ErrorCodes.SCENE_NOT_FOUND,
          message: `Scene "${gameState.narrativeState.currentSceneId}" not found in scene graph`,
        },
      };
    }

    reply.code(200);
    return { success: true, data: result };
  });

  // --------------------------------------------------------------------------
  // POST /choose
  // Process a scene choice and advance narrative state.
  // Body: { choiceId: string }
  // --------------------------------------------------------------------------
  fastify.post<{ Body: { choiceId: string } }>('/choose', {
    schema: {
      body: {
        type: 'object',
        required: ['choiceId'],
        properties: {
          choiceId: { type: 'string', maxLength: 128 },
        },
        additionalProperties: false,
      },
    },
  }, async (request, reply): Promise<ApiResponse<{ narrativeState: NarrativeState; nextScene: Scene | null }>> => {
    if (fastify.gameStateContainer.state === null) {
      reply.code(404);
      return {
        success: false,
        error: { code: ErrorCodes.GAME_NOT_FOUND, message: 'No active game' },
      };
    }

    const gameState = fastify.gameStateContainer.state;
    const { choiceId } = request.body;

    // Run state machine to validate and transition
    const transitionResult = advanceNarrative(gameState, choiceId, SCENE_GRAPH);

    if (transitionResult.type === 'error') {
      // Map error code to appropriate HTTP status
      const code = transitionResult.code;
      const is404 = code === ErrorCodes.SCENE_NOT_FOUND || code === ErrorCodes.CHOICE_NOT_FOUND;
      reply.code(is404 ? 404 : 400);
      return {
        success: false,
        error: { code, message: transitionResult.message },
      };
    }

    // Apply GameState-level consequences (personality, NPC relationships)
    // The state machine handles NarrativeState; choiceEngine handles GameState
    const currentScene = SCENE_GRAPH.find(s => s.id === gameState.narrativeState!.currentSceneId);
    const choice = currentScene?.choices.find(c => c.id === choiceId);

    let updatedGameState = gameState;
    if (choice?.consequence) {
      updatedGameState = applyConsequence(gameState, choice.consequence);
    }

    // Integrate updated NarrativeState back into GameState
    const finalGameState = updateNarrativeState(updatedGameState, transitionResult.state);
    fastify.gameStateContainer.state = finalGameState;

    reply.code(200);
    return {
      success: true,
      data: {
        narrativeState: transitionResult.state,
        nextScene: transitionResult.nextScene,
      },
    };
  });

  // --------------------------------------------------------------------------
  // GET /state
  // Get current narrative state.
  // --------------------------------------------------------------------------
  fastify.get('/state', async (_request, reply): Promise<ApiResponse<{ narrativeState: NarrativeState | null }>> => {
    if (fastify.gameStateContainer.state === null) {
      reply.code(404);
      return {
        success: false,
        error: { code: ErrorCodes.GAME_NOT_FOUND, message: 'No active game' },
      };
    }

    reply.code(200);
    return {
      success: true,
      data: { narrativeState: fastify.gameStateContainer.state.narrativeState },
    };
  });

  // --------------------------------------------------------------------------
  // POST /reset
  // Clear narrative state.
  // --------------------------------------------------------------------------
  fastify.post('/reset', async (_request, reply): Promise<ApiResponse<{ narrativeState: null }>> => {
    if (fastify.gameStateContainer.state === null) {
      reply.code(404);
      return {
        success: false,
        error: { code: ErrorCodes.GAME_NOT_FOUND, message: 'No active game' },
      };
    }

    const cleared = clearNarrative(fastify.gameStateContainer.state);
    fastify.gameStateContainer.state = cleared;

    reply.code(200);
    return {
      success: true,
      data: { narrativeState: null },
    };
  });

  // --------------------------------------------------------------------------
  // GET /synergy
  // Get current synergy bonus for the party.
  // --------------------------------------------------------------------------
  fastify.get('/synergy', async (_request, reply): Promise<ApiResponse<{ synergy: SynergyResult }>> => {
    if (fastify.gameStateContainer.state === null) {
      reply.code(404);
      return {
        success: false,
        error: { code: ErrorCodes.GAME_NOT_FOUND, message: 'No active game' },
      };
    }

    const gameState = fastify.gameStateContainer.state;
    const npcPersonalities = Object.values(gameState.npcs)
      .filter(npc => npc?.personality)
      .map(npc => npc.personality);

    const synergy = calculateSynergy(
      gameState.player.personality,
      npcPersonalities,
      DEFAULT_PARADIGMS
    );

    reply.code(200);
    return {
      success: true,
      data: { synergy },
    };
  });
}
