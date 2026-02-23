/**
 * Ancient Order - Combat Plugin
 *
 * Fastify plugin for combat encounter management:
 *   POST /encounter  — initialize a combat encounter
 *   POST /declare    — submit player action declarations
 *   POST /round      — advance one round of combat
 *   GET  /state      — return Phase 2 VisualInfo (no AI decisions exposed)
 *   GET  /history    — return round history
 *   GET  /result     — return combat status (active/victory/defeat)
 *
 * Registered with prefix /api/combat in src/api/index.ts.
 *
 * Pending declarations are stored on the plugin's local scope inside a
 * mutable `pendingDeclarations` array. This array is cleared after each
 * round advances. The combatState itself is persisted on the shared
 * gameStateContainer so it survives across all plugin scopes.
 *
 * Error codes:
 *   GAME_NOT_FOUND — no active game state, or no active combat
 *   COMBAT_INVALID_DECLARATION — one or more declarations failed validation
 */

import { FastifyInstance } from 'fastify';
import type { CombatAction } from '../types/combat.js';
import { ApiResponse, ErrorCodes } from '../types/index.js';
import { initCombatState, syncToGameState, endCombat } from '../combat/sync.js';
import { runRound, buildVisualInfo, VisualInfo } from '../combat/roundManager.js';
import { validateDeclaration } from '../combat/declaration.js';
import type { EncounterConfig } from '../types/combat.js';
import encounterDemoConfig from '../fixtures/encounter.json' with { type: 'json' };

// ============================================================================
// Error Codes (combat-specific)
// ============================================================================

/**
 * Combat-specific error code added locally.
 * Not in the root ErrorCodes object to keep it focused on Sprint 1 codes.
 */
const COMBAT_ERROR_CODES = {
  NO_ACTIVE_COMBAT: 'NO_ACTIVE_COMBAT',
  COMBAT_INVALID_DECLARATION: 'COMBAT_INVALID_DECLARATION',
  ENCOUNTER_NOT_FOUND: 'ENCOUNTER_NOT_FOUND',
} as const;

// ============================================================================
// In-Memory Encounter Registry
// ============================================================================

/**
 * Static in-memory registry of available encounter configurations.
 * At least one test encounter is registered: 'encounter_test'.
 *
 * Each encounter has a 3-player party and a 3-enemy party with sensible stats.
 * The reaction skill rates are representative values for a mid-rank combatant.
 */
const ENCOUNTER_REGISTRY: Record<string, EncounterConfig> = {
  // Demo encounter: 3v3 pitch encounter loaded from src/fixtures/encounter.json.
  // Stats are representative — verify against GM Combat Tracker.xlsx "Battle Scenarios"
  // sheet before the investor/publisher pitch demo.
  encounter_demo: encounterDemoConfig as EncounterConfig,
  encounter_test: {
    id: 'encounter_test',
    name: 'Test Encounter (3v3)',
    playerParty: [
      {
        id: 'player_warrior',
        name: 'Warrior',
        archetype: 'warrior',
        rank: 2.0,
        stamina: 120,
        power: 18,
        speed: 14,
        elementalPath: 'Fire',
        ascensionLevel: 0,
        reactionSkills: {
          block: { SR: 0.6, SMR: 0.5, FMR: 0.2 },
          dodge: { SR: 0.4, FMR: 0.3 },
          parry: { SR: 0.3, FMR: 0.25 },
        },
      },
      {
        id: 'player_scout',
        name: 'Scout',
        archetype: 'scout',
        rank: 1.5,
        stamina: 90,
        power: 14,
        speed: 20,
        elementalPath: 'Air',
        ascensionLevel: 0,
        reactionSkills: {
          block: { SR: 0.4, SMR: 0.4, FMR: 0.15 },
          dodge: { SR: 0.65, FMR: 0.2 },
          parry: { SR: 0.2, FMR: 0.2 },
        },
      },
      {
        id: 'player_mage',
        name: 'Mage',
        archetype: 'mage',
        rank: 1.5,
        stamina: 80,
        power: 22,
        speed: 12,
        elementalPath: 'Shadow',
        ascensionLevel: 0,
        reactionSkills: {
          block: { SR: 0.3, SMR: 0.35, FMR: 0.1 },
          dodge: { SR: 0.35, FMR: 0.15 },
          parry: { SR: 0.15, FMR: 0.15 },
        },
      },
    ],
    enemyParty: [
      {
        id: 'enemy_brute',
        name: 'Iron Brute',
        archetype: 'elena_loyal_scout',
        rank: 2.0,
        stamina: 110,
        power: 20,
        speed: 10,
        elementalPath: 'Earth',
        ascensionLevel: 0,
        reactionSkills: {
          block: { SR: 0.65, SMR: 0.55, FMR: 0.25 },
          dodge: { SR: 0.2, FMR: 0.3 },
          parry: { SR: 0.2, FMR: 0.2 },
        },
      },
      {
        id: 'enemy_archer',
        name: 'Shadow Archer',
        archetype: 'kade_bold_outlaw',
        rank: 1.5,
        stamina: 85,
        power: 16,
        speed: 18,
        elementalPath: 'Shadow',
        ascensionLevel: 0,
        reactionSkills: {
          block: { SR: 0.35, SMR: 0.35, FMR: 0.15 },
          dodge: { SR: 0.6, FMR: 0.2 },
          parry: { SR: 0.25, FMR: 0.2 },
        },
      },
      {
        id: 'enemy_shaman',
        name: 'Bog Shaman',
        archetype: 'lars_cautious_merchant',
        rank: 1.5,
        stamina: 75,
        power: 19,
        speed: 11,
        elementalPath: 'Water',
        ascensionLevel: 0,
        reactionSkills: {
          block: { SR: 0.45, SMR: 0.4, FMR: 0.15 },
          dodge: { SR: 0.35, FMR: 0.2 },
          parry: { SR: 0.3, FMR: 0.2 },
        },
      },
    ],
  },
};

// ============================================================================
// Validation Result Type (for the declare endpoint response)
// ============================================================================

interface DeclarationValidationEntry {
  combatantId: string;
  type: string;
  targetId: string | null;
  valid: boolean;
  error?: string;
}

// ============================================================================
// Plugin
// ============================================================================

export async function combatPlugin(fastify: FastifyInstance): Promise<void> {
  /**
   * Pending player declarations accumulated via POST /declare.
   * Cleared after each POST /round call.
   * Stored locally in plugin scope — no need for a separate container because
   * the combat plugin owns this state exclusively.
   */
  let pendingDeclarations: CombatAction[] = [];

  // --------------------------------------------------------------------------
  // Shared guard: require an active game state
  // --------------------------------------------------------------------------
  function requireGame() {
    return fastify.gameStateContainer.state;
  }

  // --------------------------------------------------------------------------
  // Shared guard: require an active combat state on the game
  // --------------------------------------------------------------------------
  function requireCombat() {
    const gameState = fastify.gameStateContainer.state;
    if (gameState === null) return null;
    return gameState.combatState ?? null;
  }

  // --------------------------------------------------------------------------
  // POST /encounter
  // Initialize a combat encounter from the registry. Resets pending declarations.
  // Body: { encounterId: string }
  // --------------------------------------------------------------------------
  fastify.post<{ Body: { encounterId: string } }>('/encounter', {
    schema: {
      body: {
        type: 'object',
        required: ['encounterId'],
        properties: {
          encounterId: { type: 'string' },
        },
        additionalProperties: false,
      },
    },
  }, async (request, reply): Promise<ApiResponse<{ encounterId: string; round: number; status: string }>> => {
    const gameState = requireGame();
    if (gameState === null) {
      reply.code(404);
      return {
        success: false,
        error: { code: ErrorCodes.GAME_NOT_FOUND, message: 'No active game' },
      };
    }

    const { encounterId } = request.body;
    const encounter = ENCOUNTER_REGISTRY[encounterId];
    if (encounter === undefined) {
      reply.code(404);
      return {
        success: false,
        error: {
          code: COMBAT_ERROR_CODES.ENCOUNTER_NOT_FOUND,
          message: `Encounter '${encounterId}' not found in registry`,
        },
      };
    }

    // Initialize CombatState and attach it to GameState
    const combatState = initCombatState(gameState, encounter);
    const newGameState = syncToGameState(gameState, combatState);
    fastify.gameStateContainer.state = newGameState;

    // Clear any stale pending declarations from a previous encounter
    pendingDeclarations = [];

    reply.code(200);
    return {
      success: true,
      data: {
        encounterId: encounter.id,
        round: combatState.round,
        status: combatState.status,
      },
    };
  });

  // --------------------------------------------------------------------------
  // POST /declare
  // Submit player action declarations. Validates each action and stores valid
  // ones (plus fallbacks) in pendingDeclarations for the next POST /round call.
  // Body: { actions: CombatAction[] }
  // --------------------------------------------------------------------------
  fastify.post<{
    Body: { actions: CombatAction[] };
  }>('/declare', {
    schema: {
      body: {
        type: 'object',
        required: ['actions'],
        properties: {
          actions: {
            type: 'array',
            maxItems: 10,
            items: {
              type: 'object',
              required: ['combatantId', 'type'],
              properties: {
                combatantId: { type: 'string' },
                type: { type: 'string' },
                targetId: { type: ['string', 'null'] },
                energySegments: { type: 'number' },
              },
              additionalProperties: false,
            },
          },
        },
        additionalProperties: false,
      },
    },
  }, async (request, reply): Promise<ApiResponse<{ accepted: DeclarationValidationEntry[]; rejected: DeclarationValidationEntry[] }>> => {
    const gameState = requireGame();
    if (gameState === null) {
      reply.code(404);
      return {
        success: false,
        error: { code: ErrorCodes.GAME_NOT_FOUND, message: 'No active game' },
      };
    }

    const combatState = requireCombat();
    if (combatState === null) {
      reply.code(404);
      return {
        success: false,
        error: {
          code: COMBAT_ERROR_CODES.NO_ACTIVE_COMBAT,
          message: 'No active combat encounter',
        },
      };
    }

    const actions = request.body.actions;
    const accepted: DeclarationValidationEntry[] = [];
    const rejected: DeclarationValidationEntry[] = [];

    // Validate each declaration and collect results
    const validatedForStorage: CombatAction[] = [];

    for (const action of actions) {
      // Ensure targetId is null (not undefined) when absent
      const normalizedAction: CombatAction = {
        combatantId: action.combatantId,
        type: action.type,
        targetId: action.targetId ?? null,
        ...(action.energySegments !== undefined ? { energySegments: action.energySegments } : {}),
      };

      const result = validateDeclaration(combatState, normalizedAction);

      if (result.valid) {
        accepted.push({
          combatantId: normalizedAction.combatantId,
          type: normalizedAction.type,
          targetId: normalizedAction.targetId,
          valid: true,
        });
        validatedForStorage.push(normalizedAction);
      } else {
        rejected.push({
          combatantId: normalizedAction.combatantId,
          type: normalizedAction.type,
          targetId: normalizedAction.targetId,
          valid: false,
          error: result.error,
        });

        // If a fallback action was provided (e.g., GROUP → ATTACK), store that instead
        if (result.fallback !== undefined) {
          validatedForStorage.push(result.fallback);
          accepted.push({
            combatantId: result.fallback.combatantId,
            type: result.fallback.type,
            targetId: result.fallback.targetId,
            valid: true,
          });
        }
      }
    }

    // Replace (not append) pending declarations — each POST /declare is authoritative
    pendingDeclarations = validatedForStorage;

    // If every submitted action was rejected with no fallbacks, return 422
    if (actions.length > 0 && accepted.length === 0) {
      reply.code(422);
      return {
        success: false,
        error: {
          code: COMBAT_ERROR_CODES.COMBAT_INVALID_DECLARATION,
          message: 'All declarations were invalid',
        },
      };
    }

    reply.code(200);
    return { success: true, data: { accepted, rejected } };
  });

  // --------------------------------------------------------------------------
  // POST /round
  // Advance one round of combat using the stored pending declarations.
  // Calls runRound(), syncs the result back to GameState, clears pending
  // declarations, and checks for end-of-combat conditions.
  // --------------------------------------------------------------------------
  fastify.post('/round', async (_request, reply): Promise<ApiResponse<VisualInfo & { round: number; status: string }>> => {
    const gameState = requireGame();
    if (gameState === null) {
      reply.code(404);
      return {
        success: false,
        error: { code: ErrorCodes.GAME_NOT_FOUND, message: 'No active game' },
      };
    }

    const combatState = requireCombat();
    if (combatState === null) {
      reply.code(404);
      return {
        success: false,
        error: {
          code: COMBAT_ERROR_CODES.NO_ACTIVE_COMBAT,
          message: 'No active combat encounter',
        },
      };
    }

    // Run the round with current pending declarations
    const resolvedCombatState = runRound(combatState, pendingDeclarations);

    // Clear pending declarations after the round advances
    pendingDeclarations = [];

    // Sync the resolved combat state back to GameState
    let newGameState = syncToGameState(gameState, resolvedCombatState);

    // If combat ended, call endCombat to clear combatState and record result
    if (resolvedCombatState.status !== 'active') {
      newGameState = endCombat(newGameState, resolvedCombatState.status);
    }

    fastify.gameStateContainer.state = newGameState;

    // Build VisualInfo from the resolved state for the response
    const visualInfo = buildVisualInfo(resolvedCombatState, []);

    reply.code(200);
    return {
      success: true,
      data: {
        ...visualInfo,
        round: resolvedCombatState.round,
        status: resolvedCombatState.status,
      },
    };
  });

  // --------------------------------------------------------------------------
  // GET /state
  // Return Phase 2 VisualInfo — stances, stamina, targeting.
  // AI decisions from Phase 1 are NOT included (ADR-008).
  // --------------------------------------------------------------------------
  fastify.get('/state', async (_request, reply): Promise<ApiResponse<VisualInfo>> => {
    const gameState = requireGame();
    if (gameState === null) {
      reply.code(404);
      return {
        success: false,
        error: { code: ErrorCodes.GAME_NOT_FOUND, message: 'No active game' },
      };
    }

    const combatState = requireCombat();
    if (combatState === null) {
      reply.code(404);
      return {
        success: false,
        error: {
          code: COMBAT_ERROR_CODES.NO_ACTIVE_COMBAT,
          message: 'No active combat encounter',
        },
      };
    }

    // Build VisualInfo with current pending declarations as the targeting source
    const visualInfo = buildVisualInfo(combatState, pendingDeclarations);

    reply.code(200);
    return { success: true, data: visualInfo };
  });

  // --------------------------------------------------------------------------
  // GET /history
  // Return the roundHistory from the active CombatState.
  // --------------------------------------------------------------------------
  fastify.get('/history', async (_request, reply): Promise<ApiResponse<unknown>> => {
    const gameState = requireGame();
    if (gameState === null) {
      reply.code(404);
      return {
        success: false,
        error: { code: ErrorCodes.GAME_NOT_FOUND, message: 'No active game' },
      };
    }

    const combatState = requireCombat();
    if (combatState === null) {
      reply.code(404);
      return {
        success: false,
        error: {
          code: COMBAT_ERROR_CODES.NO_ACTIVE_COMBAT,
          message: 'No active combat encounter',
        },
      };
    }

    reply.code(200);
    return { success: true, data: combatState.roundHistory };
  });

  // --------------------------------------------------------------------------
  // GET /result
  // Return the combat status: 'active', 'victory', or 'defeat'.
  // --------------------------------------------------------------------------
  fastify.get('/result', async (_request, reply): Promise<ApiResponse<{ status: string }>> => {
    const gameState = requireGame();
    if (gameState === null) {
      reply.code(404);
      return {
        success: false,
        error: { code: ErrorCodes.GAME_NOT_FOUND, message: 'No active game' },
      };
    }

    const combatState = requireCombat();
    if (combatState === null) {
      reply.code(404);
      return {
        success: false,
        error: {
          code: COMBAT_ERROR_CODES.NO_ACTIVE_COMBAT,
          message: 'No active combat encounter',
        },
      };
    }

    reply.code(200);
    return { success: true, data: { status: combatState.status } };
  });
}
