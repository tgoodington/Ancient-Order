/**
 * Ancient Order - Sprint 4 Integration Tests
 *
 * Validates the following endpoints through the Fastify inject() API:
 *   - GET  /api/game/saves         — list all 10 save slots
 *   - DELETE /api/game/saves/:slot — delete a save slot
 *   - GET  /api/player/personality — get player personality
 *   - POST /api/player/team        — set active team composition
 *   - GET  /api/npc/:id            — get NPC (live state fallback + template)
 *
 * Each describe block creates a fresh Fastify instance in beforeEach to
 * ensure no state leaks between tests.
 *
 * Save file cleanup: tests that write to disk (via save/load) remove the
 * slot_N.json file in a finally-style try/catch after the assertion.
 */

import * as fs from 'fs/promises';
import * as path from 'path';
import { describe, it, expect, beforeEach, afterEach } from 'vitest';
import { FastifyInstance } from 'fastify';

import { buildApp } from '../src/api/index.js';
import { ApiResponse, GameState, NPC } from '../src/types/index.js';
import { SaveSlotInfo } from '../src/persistence/saveLoad.js';

// ============================================================================
// Helpers
// ============================================================================

/** Parse the response body from an inject() call as JSON. */
function parseBody<T>(raw: string): T {
  return JSON.parse(raw) as T;
}

/**
 * Assert the personality sum is within floating-point tolerance of 100%.
 * Tolerance is 0.02 to account for floating-point accumulation during
 * multi-pass normalization (see sprint1 test commentary for rationale).
 */
function assertPersonalitySum(personality: {
  patience: number;
  empathy: number;
  cunning: number;
  logic: number;
  kindness: number;
  charisma: number;
}): void {
  const sum =
    personality.patience +
    personality.empathy +
    personality.cunning +
    personality.logic +
    personality.kindness +
    personality.charisma;
  expect(Math.abs(sum - 100)).toBeLessThan(0.02);
}

/**
 * Creates a new game via the API and returns the parsed response.
 * POST /api/game/new requires a JSON body because the route schema
 * defines body as type: 'object'.
 */
async function createNewGame(app: FastifyInstance): Promise<ApiResponse<GameState>> {
  const response = await app.inject({
    method: 'POST',
    url: '/api/game/new',
    payload: {},
  });
  return parseBody<ApiResponse<GameState>>(response.body);
}

/**
 * Saves the current game to the given slot.
 * Returns the raw inject response (caller checks status as needed).
 */
async function saveToSlot(app: FastifyInstance, slot: number) {
  return app.inject({ method: 'POST', url: `/api/game/save/${slot}` });
}

/**
 * Removes a save file from disk if it exists, ignoring errors.
 * Called in test cleanup to avoid cross-test interference.
 */
async function cleanupSaveFile(slot: number): Promise<void> {
  try {
    await fs.unlink(path.join(process.cwd(), 'saves', `slot_${slot}.json`));
  } catch {
    // Ignore — file may not have been created
  }
}

// ============================================================================
// Suite 1: GET /api/game/saves
// ============================================================================

describe('GET /api/game/saves', () => {
  let app: FastifyInstance;

  beforeEach(async () => {
    app = await buildApp();
  });

  afterEach(async () => {
    await app.close();
  });

  it('returns 200 with 10 save slot entries', async () => {
    const response = await app.inject({ method: 'GET', url: '/api/game/saves' });

    expect(response.statusCode).toBe(200);
    const body = parseBody<ApiResponse<SaveSlotInfo[]>>(response.body);
    expect(body.success).toBe(true);
    expect(body.data).toHaveLength(10);
  });

  it('all slots empty on fresh app', async () => {
    const response = await app.inject({ method: 'GET', url: '/api/game/saves' });

    const body = parseBody<ApiResponse<SaveSlotInfo[]>>(response.body);
    expect(body.success).toBe(true);
    for (const slot of body.data!) {
      expect(slot.exists).toBe(false);
    }
  });

  it('reflects saved slot after save', async () => {
    await createNewGame(app);
    const saveResponse = await saveToSlot(app, 1);
    expect(saveResponse.statusCode).toBe(200);

    try {
      const listResponse = await app.inject({ method: 'GET', url: '/api/game/saves' });
      const body = parseBody<ApiResponse<SaveSlotInfo[]>>(listResponse.body);

      expect(body.success).toBe(true);
      const slot1 = body.data!.find(s => s.slot === 1);
      expect(slot1).toBeDefined();
      expect(slot1!.exists).toBe(true);
      expect(slot1!.metadata).toBeDefined();
      expect(slot1!.metadata!.slot).toBe(1);
      expect(slot1!.metadata!.playerName).toBeDefined();
      expect(typeof slot1!.metadata!.timestamp).toBe('number');
    } finally {
      await cleanupSaveFile(1);
    }
  });
});

// ============================================================================
// Suite 2: DELETE /api/game/saves/:slot
// ============================================================================

describe('DELETE /api/game/saves/:slot', () => {
  let app: FastifyInstance;

  beforeEach(async () => {
    app = await buildApp();
  });

  afterEach(async () => {
    await app.close();
  });

  it('returns 200 with deleted: true for existing save', async () => {
    await createNewGame(app);
    await saveToSlot(app, 1);

    try {
      const response = await app.inject({ method: 'DELETE', url: '/api/game/saves/1' });

      expect(response.statusCode).toBe(200);
      const body = parseBody<ApiResponse<{ slot: number; deleted: boolean }>>(response.body);
      expect(body.success).toBe(true);
      expect(body.data!.deleted).toBe(true);
      expect(body.data!.slot).toBe(1);
    } finally {
      // File may already be deleted by the route — cleanup is a no-op if so
      await cleanupSaveFile(1);
    }
  });

  it('returns 400 for invalid slot (slot 0 is below minimum)', async () => {
    const response = await app.inject({ method: 'DELETE', url: '/api/game/saves/0' });

    expect(response.statusCode).toBe(400);
    const body = parseBody<ApiResponse<never>>(response.body);
    expect(body.success).toBe(false);
    expect(body.error?.code).toBe('INVALID_SLOT');
  });

  it('returns 400 for non-numeric slot', async () => {
    const response = await app.inject({ method: 'DELETE', url: '/api/game/saves/abc' });

    expect(response.statusCode).toBe(400);
    const body = parseBody<ApiResponse<never>>(response.body);
    expect(body.success).toBe(false);
  });
});

// ============================================================================
// Suite 3: GET /api/player/personality
// ============================================================================

describe('GET /api/player/personality', () => {
  let app: FastifyInstance;

  beforeEach(async () => {
    app = await buildApp();
  });

  afterEach(async () => {
    await app.close();
  });

  it('returns 200 with personality when game active', async () => {
    await createNewGame(app);

    const response = await app.inject({ method: 'GET', url: '/api/player/personality' });

    expect(response.statusCode).toBe(200);
    const body = parseBody<ApiResponse<{
      patience: number;
      empathy: number;
      cunning: number;
      logic: number;
      kindness: number;
      charisma: number;
    }>>(response.body);
    expect(body.success).toBe(true);
    expect(body.data).toBeDefined();
  });

  it('returns 404 when no active game', async () => {
    const response = await app.inject({ method: 'GET', url: '/api/player/personality' });

    expect(response.statusCode).toBe(404);
    const body = parseBody<ApiResponse<never>>(response.body);
    expect(body.success).toBe(false);
    expect(body.error?.code).toBe('GAME_NOT_FOUND');
  });

  it('personality has 6 traits summing to ~100', async () => {
    await createNewGame(app);

    const response = await app.inject({ method: 'GET', url: '/api/player/personality' });
    const body = parseBody<ApiResponse<{
      patience: number;
      empathy: number;
      cunning: number;
      logic: number;
      kindness: number;
      charisma: number;
    }>>(response.body);

    expect(body.success).toBe(true);
    const p = body.data!;

    // All 6 trait names must be present
    expect(typeof p.patience).toBe('number');
    expect(typeof p.empathy).toBe('number');
    expect(typeof p.cunning).toBe('number');
    expect(typeof p.logic).toBe('number');
    expect(typeof p.kindness).toBe('number');
    expect(typeof p.charisma).toBe('number');

    // Sum invariant
    assertPersonalitySum(p);
  });
});

// ============================================================================
// Suite 4: POST /api/player/team
// ============================================================================

describe('POST /api/player/team', () => {
  let app: FastifyInstance;

  beforeEach(async () => {
    app = await buildApp();
  });

  afterEach(async () => {
    await app.close();
  });

  it('returns 200 with team when valid 2 NPC IDs provided', async () => {
    await createNewGame(app);

    const response = await app.inject({
      method: 'POST',
      url: '/api/player/team',
      payload: JSON.stringify({ npcIds: ['npc_scout_elena', 'npc_merchant_lars'] }),
      headers: { 'content-type': 'application/json' },
    });

    expect(response.statusCode).toBe(200);
    const body = parseBody<ApiResponse<{ team: readonly string[] }>>(response.body);
    expect(body.success).toBe(true);
    expect(body.data!.team).toContain('npc_scout_elena');
    expect(body.data!.team).toContain('npc_merchant_lars');
  });

  it('returns 404 when no active game', async () => {
    const response = await app.inject({
      method: 'POST',
      url: '/api/player/team',
      payload: JSON.stringify({ npcIds: ['npc_scout_elena', 'npc_merchant_lars'] }),
      headers: { 'content-type': 'application/json' },
    });

    expect(response.statusCode).toBe(404);
    const body = parseBody<ApiResponse<never>>(response.body);
    expect(body.success).toBe(false);
    expect(body.error?.code).toBe('GAME_NOT_FOUND');
  });

  it('returns 400 when combat is active', async () => {
    await createNewGame(app);

    // Directly inject a non-null combatState to simulate an active encounter.
    // gameStateContainer is decorated on the Fastify instance and shared
    // across all plugin scopes.
    (app as any).gameStateContainer.state = {
      ...(app as any).gameStateContainer.state,
      combatState: {
        round: 1,
        phase: 'AI_DECISION',
        status: 'active',
        playerParty: [],
        enemyParty: [],
        actionQueue: [],
        roundHistory: [],
      },
    };

    const response = await app.inject({
      method: 'POST',
      url: '/api/player/team',
      payload: JSON.stringify({ npcIds: ['npc_scout_elena', 'npc_merchant_lars'] }),
      headers: { 'content-type': 'application/json' },
    });

    expect(response.statusCode).toBe(400);
    const body = parseBody<ApiResponse<never>>(response.body);
    expect(body.success).toBe(false);
    expect(body.error?.code).toBe('TEAM_COMPOSITION_INVALID');
    expect(body.error?.message.toLowerCase()).toContain('combat');
  });

  it('returns 400 when narrative is active', async () => {
    await createNewGame(app);

    // Inject a non-null narrativeState to simulate an active narrative scene.
    (app as any).gameStateContainer.state = {
      ...(app as any).gameStateContainer.state,
      narrativeState: {
        currentSceneId: 'scene_intro',
        visitedSceneIds: [],
        choiceFlags: {},
        sceneHistory: [],
      },
    };

    const response = await app.inject({
      method: 'POST',
      url: '/api/player/team',
      payload: JSON.stringify({ npcIds: ['npc_scout_elena', 'npc_merchant_lars'] }),
      headers: { 'content-type': 'application/json' },
    });

    expect(response.statusCode).toBe(400);
    const body = parseBody<ApiResponse<never>>(response.body);
    expect(body.success).toBe(false);
    expect(body.error?.code).toBe('TEAM_COMPOSITION_INVALID');
    expect(body.error?.message.toLowerCase()).toContain('narrative');
  });

  it('returns 400 for wrong number of NPCs — schema validation rejects minItems: 2', async () => {
    await createNewGame(app);

    // Only 1 NPC ID — schema requires minItems: 2
    const response = await app.inject({
      method: 'POST',
      url: '/api/player/team',
      payload: JSON.stringify({ npcIds: ['npc_scout_elena'] }),
      headers: { 'content-type': 'application/json' },
    });

    expect(response.statusCode).toBe(400);
    const body = parseBody<ApiResponse<never>>(response.body);
    expect(body.success).toBe(false);
  });
});

// ============================================================================
// Suite 5: GET /api/npc/:id
// ============================================================================

describe('GET /api/npc/:id', () => {
  let app: FastifyInstance;

  beforeEach(async () => {
    app = await buildApp();
  });

  afterEach(async () => {
    await app.close();
  });

  it('returns live NPC data when game is active', async () => {
    await createNewGame(app);

    const response = await app.inject({ method: 'GET', url: '/api/npc/npc_scout_elena' });

    expect(response.statusCode).toBe(200);
    const body = parseBody<ApiResponse<NPC>>(response.body);
    expect(body.success).toBe(true);

    const elena = body.data!;
    // The NPC type includes affection and trust fields
    expect(typeof elena.affection).toBe('number');
    expect(typeof elena.trust).toBe('number');
    expect(elena.id).toBe('npc_scout_elena');
  });

  it('returns template NPC data when no game active', async () => {
    // No game created — route falls back to template registry
    const response = await app.inject({ method: 'GET', url: '/api/npc/npc_scout_elena' });

    expect(response.statusCode).toBe(200);
    const body = parseBody<ApiResponse<NPC>>(response.body);
    expect(body.success).toBe(true);

    const elena = body.data!;
    expect(elena.id).toBe('npc_scout_elena');
    // Template data must still carry the NPC fields
    expect(typeof elena.affection).toBe('number');
    expect(typeof elena.trust).toBe('number');
    expect(elena.archetype).toBeDefined();
  });

  it('returns 404 for unknown NPC ID', async () => {
    const response = await app.inject({ method: 'GET', url: '/api/npc/unknown_npc' });

    expect(response.statusCode).toBe(404);
    const body = parseBody<ApiResponse<never>>(response.body);
    expect(body.success).toBe(false);
    expect(body.error?.code).toBe('NPC_NOT_FOUND');
  });
});
