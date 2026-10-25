/**
 * Session port of the save/load portions of tests/sprint1.integration.test.ts and
 * tests/sprint4.integration.test.ts.
 *
 * Drives createGameSession with an in-memory save store, so nothing is written to disk.
 * Describe names match the originals.
 *
 * Changed because the HTTP layer is gone:
 *   - 'DELETE ... returns 400 for non-numeric slot' is asserted as INVALID_SLOT on
 *     deleteSlot('abc'); there is no URL parsing to fail first.
 *   - 'load/9 returns error for a slot with no save file' is pinned to SAVE_NOT_FOUND
 *     (the HTTP version could only assert a non-200 status).
 */

import { describe, it, expect } from 'vitest';
import { createGameSession } from '../src/session/index.js';
import type { GameSession } from '../src/session/index.js';
import { createMemoryBackend, createSaveStore } from '../src/persistence/index.js';
import type { DialogueNode } from '../src/types/index.js';

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

const ELENA_DIALOGUE_TREE: DialogueNode[] = [
  {
    id: 'npc_scout_elena_greet',
    npcId: 'npc_scout_elena',
    text: 'Hail, traveller. I am Elena, a scout for DEUS.',
    options: [
      {
        id: 'elena_opt_humble',
        text: 'Tell me about your mission.',
        personalityAdjustment: { empathy: 2 },
        npcAdjustment: { trustChange: 3 },
        nextNodeId: 'npc_scout_elena_mission',
      },
    ],
  },
  {
    id: 'npc_scout_elena_mission',
    npcId: 'npc_scout_elena',
    text: 'We protect the innocent from rogue elements in the region.',
    options: [{ id: 'elena_opt_join', text: 'I want to help.', nextNodeId: null }],
  },
];

function newSession(): GameSession {
  return createGameSession({
    saves: createSaveStore(createMemoryBackend()),
    content: { dialogues: { npc_scout_elena: ELENA_DIALOGUE_TREE } },
  });
}

function startedSession(): GameSession {
  const session = newSession();
  expect(session.newGame().ok).toBe(true);
  return session;
}

function errorOf(res: { ok: boolean; error?: { code: string } }): string | undefined {
  return res.ok ? undefined : res.error?.code;
}

function currentGame(session: GameSession) {
  const res = session.getState();
  if (!res.ok) throw new Error(`getState failed: ${res.error.code}`);
  return res.data;
}

// ============================================================================
// Sprint 1: full session flow
// ============================================================================

describe('full session flow', () => {
  it('save → load round-trip preserves state', async () => {
    const session = startedSession();
    session.adjustPersonality({ patience: 8 });
    expect(session.startDialogue('npc_scout_elena').ok).toBe(true);
    expect(session.chooseDialogueOption('elena_opt_humble').ok).toBe(true);

    const preSave = currentGame(session);

    const saved = await session.saveToSlot(1);
    expect(saved.ok).toBe(true);
    expect(saved.ok && saved.data.slot).toBe(1);

    session.newGame();
    expect(currentGame(session).conversationLog).toHaveLength(0);

    const loadRes = await session.loadFromSlot(1);
    expect(loadRes.ok).toBe(true);
    const loaded = loadRes.ok ? loadRes.data : undefined;

    expect(loaded?.player.personality).toEqual(preSave.player.personality);
    expect(loaded?.conversationLog).toHaveLength(preSave.conversationLog.length);
    expect(loaded?.conversationLog[0].npcId).toBe(preSave.conversationLog[0].npcId);
    expect(loaded?.combatState).toBeNull();
    assertPersonalitySum(loaded!.player.personality);
  });

  it('NPC trust changes persist across save/load', async () => {
    const session = startedSession();
    session.startDialogue('npc_scout_elena');
    session.chooseDialogueOption('elena_opt_humble');

    expect(currentGame(session).npcs['npc_scout_elena']!.trust).toBe(3);

    await session.saveToSlot(2);
    session.newGame();
    await session.loadFromSlot(2);

    expect(currentGame(session).npcs['npc_scout_elena']!.trust).toBe(3);
  });
});

// ============================================================================
// Sprint 1: error paths
// ============================================================================

describe('error paths', () => {
  it('saveToSlot returns GAME_NOT_FOUND when no active game exists', async () => {
    const res = await newSession().saveToSlot(1);
    expect(res.ok).toBe(false);
    expect(errorOf(res)).toBe('GAME_NOT_FOUND');
  });

  it('saveToSlot(0) returns INVALID_SLOT for slot 0 (below minimum)', async () => {
    expect(errorOf(await startedSession().saveToSlot(0))).toBe('INVALID_SLOT');
  });

  it('saveToSlot(11) returns INVALID_SLOT for slot 11 (above maximum)', async () => {
    expect(errorOf(await startedSession().saveToSlot(11))).toBe('INVALID_SLOT');
  });

  it('loadFromSlot(0) returns INVALID_SLOT for slot 0', async () => {
    expect(errorOf(await newSession().loadFromSlot(0))).toBe('INVALID_SLOT');
  });

  it('loadFromSlot(11) returns INVALID_SLOT for slot 11', async () => {
    expect(errorOf(await newSession().loadFromSlot(11))).toBe('INVALID_SLOT');
  });

  it('loadFromSlot(9) returns SAVE_NOT_FOUND for a slot with no save', async () => {
    const res = await newSession().loadFromSlot(9);
    expect(res.ok).toBe(false);
    expect(errorOf(res)).toBe('SAVE_NOT_FOUND');
  });
});

// ============================================================================
// Sprint 4: saves list
// ============================================================================

describe('saves list', () => {
  it('returns 10 save slot entries', async () => {
    const res = await newSession().listSlots();
    expect(res.ok).toBe(true);
    expect(res.ok && res.data).toHaveLength(10);
  });

  it('all slots empty on fresh session', async () => {
    const res = await newSession().listSlots();
    expect(res.ok).toBe(true);
    if (res.ok) {
      for (const slot of res.data) expect(slot.exists).toBe(false);
    }
  });

  it('reflects saved slot after save', async () => {
    const session = startedSession();
    expect((await session.saveToSlot(1)).ok).toBe(true);

    const res = await session.listSlots();
    expect(res.ok).toBe(true);
    const slot1 = res.ok ? res.data.find((s) => s.slot === 1) : undefined;
    expect(slot1).toBeDefined();
    expect(slot1!.exists).toBe(true);
    expect(slot1!.metadata).toBeDefined();
    expect(slot1!.metadata!.slot).toBe(1);
    expect(slot1!.metadata!.playerName).toBeDefined();
    expect(typeof slot1!.metadata!.timestamp).toBe('number');
  });
});

// ============================================================================
// Sprint 4: saves delete
// ============================================================================

describe('saves delete', () => {
  it('returns deleted: true for existing save', async () => {
    const session = startedSession();
    await session.saveToSlot(1);

    const res = await session.deleteSlot(1);
    expect(res.ok).toBe(true);
    expect(res.ok && res.data.deleted).toBe(true);
    expect(res.ok && res.data.slot).toBe(1);
  });

  it('returns INVALID_SLOT for invalid slot (slot 0 is below minimum)', async () => {
    expect(errorOf(await newSession().deleteSlot(0))).toBe('INVALID_SLOT');
  });

  it('returns INVALID_SLOT for non-numeric slot', async () => {
    const res = await newSession().deleteSlot('abc');
    expect(res.ok).toBe(false);
    expect(errorOf(res)).toBe('INVALID_SLOT');
  });
});
