/**
 * Session port of tests/sprint4.integration.test.ts.
 *
 * Drives the in-client game session (createGameSession) instead of the HTTP
 * server. Describe names match the original, with the route paths replaced by
 * the session operation names (one-to-one).
 *
 * Dropped tests (and why):
 *   'saves list'
 *     - 'returns 200 with 10 save slot entries'          moved to T9 (save/load)
 *     - 'all slots empty on fresh app'                   moved to T9 (save/load)
 *     - 'reflects saved slot after save'                 moved to T9 (save/load)
 *   'saves delete'
 *     - 'returns 200 with deleted: true for existing save' moved to T9 (save/load)
 *     - 'returns 400 for invalid slot (slot 0 ...)'      moved to T9 (save/load)
 *     - 'returns 400 for non-numeric slot'               moved to T9 (save/load; the
 *                                                        slot is a route param, HTTP-only)
 *
 * Changed:
 *   - The team-locked tests used to inject combatState / narrativeState straight
 *     into the server's state container. The session has no such back door, so
 *     they enter combat with startEncounter('encounter_test') and narrative with
 *     startScene('scene_opening').
 *   - 'wrong number of NPCs' asserts the session's VALIDATION_ERROR (was schema 400).
 */

import { describe, it, expect } from 'vitest';
import { createGameSession } from '../src/session/index.js';
import type { GameSession } from '../src/session/index.js';

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

function activeSession(): GameSession {
  const session = createGameSession();
  session.newGame();
  return session;
}

describe('player personality', () => {
  it('returns personality when game active', () => {
    const res = activeSession().getPersonality();

    expect(res.ok).toBe(true);
    expect(res.ok && res.data).toBeDefined();
  });

  it('returns GAME_NOT_FOUND when no active game', () => {
    const res = createGameSession().getPersonality();
    expect(res.ok === false && res.error.code).toBe('GAME_NOT_FOUND');
  });

  it('personality has 6 traits summing to ~100', () => {
    const res = activeSession().getPersonality();

    expect(res.ok).toBe(true);
    if (!res.ok) return;
    const p = res.data;

    expect(typeof p.patience).toBe('number');
    expect(typeof p.empathy).toBe('number');
    expect(typeof p.cunning).toBe('number');
    expect(typeof p.logic).toBe('number');
    expect(typeof p.kindness).toBe('number');
    expect(typeof p.charisma).toBe('number');

    assertPersonalitySum(p);
  });
});

describe('player team', () => {
  const PAIR = ['npc_scout_elena', 'npc_merchant_lars'];

  it('returns team when valid 2 NPC IDs provided', () => {
    const res = activeSession().setTeam(PAIR);

    expect(res.ok).toBe(true);
    if (!res.ok) return;
    expect(res.data.team).toContain('npc_scout_elena');
    expect(res.data.team).toContain('npc_merchant_lars');
  });

  it('returns GAME_NOT_FOUND when no active game', () => {
    const res = createGameSession().setTeam(PAIR);
    expect(res.ok === false && res.error.code).toBe('GAME_NOT_FOUND');
  });

  it('refuses when combat is active', () => {
    const session = activeSession();
    expect(session.startEncounter('encounter_test').ok).toBe(true);

    const res = session.setTeam(PAIR);

    expect(res.ok).toBe(false);
    if (res.ok) return;
    expect(res.error.code).toBe('TEAM_COMPOSITION_INVALID');
    expect(res.error.message.toLowerCase()).toContain('combat');
  });

  it('refuses when narrative is active', () => {
    const session = activeSession();
    expect(session.startScene('scene_opening').ok).toBe(true);

    const res = session.setTeam(PAIR);

    expect(res.ok).toBe(false);
    if (res.ok) return;
    expect(res.error.code).toBe('TEAM_COMPOSITION_INVALID');
    expect(res.error.message.toLowerCase()).toContain('narrative');
  });

  it('refuses a wrong number of NPCs — validation rejects a team that is not exactly 2', () => {
    const res = activeSession().setTeam(['npc_scout_elena']);
    expect(res.ok === false && res.error.code).toBe('VALIDATION_ERROR');
  });
});

describe('npc by id', () => {
  it('returns live NPC data when game is active', () => {
    const res = activeSession().getNpc('npc_scout_elena');

    expect(res.ok).toBe(true);
    if (!res.ok) return;
    expect(typeof res.data.affection).toBe('number');
    expect(typeof res.data.trust).toBe('number');
    expect(res.data.id).toBe('npc_scout_elena');
  });

  it('returns template NPC data when no game active', () => {
    const res = createGameSession().getNpc('npc_scout_elena');

    expect(res.ok).toBe(true);
    if (!res.ok) return;
    expect(res.data.id).toBe('npc_scout_elena');
    expect(typeof res.data.affection).toBe('number');
    expect(typeof res.data.trust).toBe('number');
    expect(res.data.archetype).toBeDefined();
  });

  it('returns NPC_NOT_FOUND for unknown NPC ID', () => {
    const res = createGameSession().getNpc('unknown_npc');
    expect(res.ok === false && res.error.code).toBe('NPC_NOT_FOUND');
  });
});
