/**
 * Session port of tests/sprint1.integration.test.ts.
 *
 * Drives the in-client game session (createGameSession) instead of the HTTP
 * server. Describe names match the original one-to-one. Each test builds its
 * own session.
 *
 * Dropped tests (and why):
 *   'full session flow'
 *     - 'save → load round-trip preserves state'            moved to T9 (save/load)
 *     - 'NPC trust changes persist across save/load'        moved to T9 (save/load)
 *   'error paths'
 *     - 'save to slot returns 404 ... no game' moved to T9 (save/load)
 *     - 'save/0 INVALID_SLOT'                               moved to T9 (save/load)
 *     - 'save/11 INVALID_SLOT'                              moved to T9 (save/load)
 *     - 'load/0 INVALID_SLOT'                               moved to T9 (save/load)
 *     - 'load/11 INVALID_SLOT'                              moved to T9 (save/load)
 *     - 'load/9 error for a slot with no save file'         moved to T9 (save/load)
 *   'health check'
 *     - 'GET /health returns { status: "ok" }'              HTTP-only (no session equivalent)
 *
 * Changed because dialogue is now session-owned (approved):
 *   - The client no longer posts a dialogueTree/nodeId. A test tree is injected
 *     through createGameSession({ content: { dialogues } }), the conversation is
 *     opened with startDialogue(npcId), and chooseDialogueOption takes only the
 *     optionId. 'unknown NPC' is now asserted on startDialogue; 'missing node in
 *     tree' is an NPC whose tree lacks its greet node.
 *   - The 'dialogue GET start' tests expect the placeholder `<npcId>_greet` node
 *     with its Farewell option.
 *   - The 'no properties' validation test asserts VALIDATION_ERROR, no status code.
 */

import { describe, it, expect } from 'vitest';
import { createGameSession } from '../src/session/index.js';
import type { GameSession } from '../src/session/index.js';
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

/**
 * Fixture dialogue tree for npc_scout_elena. One ungated option and one
 * personality-gated option on the greet node.
 */
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
      {
        id: 'elena_opt_cunning',
        text: 'What is the catch?',
        gate: { trait: 'cunning', operator: 'gte', value: 20 },
        personalityAdjustment: { cunning: 2 },
        npcAdjustment: { trustChange: -5, affectionChange: -2 },
        nextNodeId: 'npc_scout_elena_mission',
      },
    ],
  },
  {
    id: 'npc_scout_elena_mission',
    npcId: 'npc_scout_elena',
    text: 'We protect the innocent from rogue elements in the region.',
    options: [
      {
        id: 'elena_opt_join',
        text: 'I want to help. What do I do?',
        nextNodeId: null,
      },
    ],
  },
];

function elenaSession(): GameSession {
  return createGameSession({ content: { dialogues: { npc_scout_elena: ELENA_DIALOGUE_TREE } } });
}

function newElenaGame(): GameSession {
  const session = elenaSession();
  session.newGame();
  return session;
}

function chooseHumble(session: GameSession) {
  expect(session.startDialogue('npc_scout_elena').ok).toBe(true);
  return session.chooseDialogueOption('elena_opt_humble');
}

describe('full session flow', () => {
  it('newGame creates a game with combatState null and personality sum 100', () => {
    const session = createGameSession();
    const res = session.newGame();

    expect(res.ok).toBe(true);
    if (!res.ok) return;
    expect(res.data.combatState).toBeNull();
    assertPersonalitySum(res.data.player.personality);
  });

  it('getState returns the active game after new game is created', () => {
    const session = createGameSession();
    session.newGame();

    const res = session.getState();

    expect(res.ok).toBe(true);
    expect(res.ok && res.data.player).toBeDefined();
  });

  it('adjustPersonality adjusts traits and maintains sum 100', () => {
    const session = createGameSession();
    session.newGame();

    const res = session.adjustPersonality({ patience: 5 });

    expect(res.ok).toBe(true);
    if (!res.ok) return;
    assertPersonalitySum(res.data.player.personality);
  });

  it('chooseDialogueOption updates state via ungated option', () => {
    const session = newElenaGame();

    const res = chooseHumble(session);

    expect(res.ok).toBe(true);
    if (!res.ok) return;
    expect(res.data.selectedOptionId).toBe('elena_opt_humble');
    expect(res.data.nextNode?.id).toBe('npc_scout_elena_mission');
    assertPersonalitySum(res.data.state.player.personality);
  });

  it('dialogue choice appends to conversation log', () => {
    const session = newElenaGame();

    chooseHumble(session);

    const state = session.getState();
    expect(state.ok).toBe(true);
    if (!state.ok) return;
    expect(state.data.conversationLog).toHaveLength(1);
    expect(state.data.conversationLog[0].npcId).toBe('npc_scout_elena');
    expect(state.data.conversationLog[0].optionId).toBe('elena_opt_humble');
  });
});

describe('error paths', () => {
  it('getState returns GAME_NOT_FOUND when no game exists', () => {
    const res = createGameSession().getState();
    expect(res.ok).toBe(false);
    expect(res.ok === false && res.error.code).toBe('GAME_NOT_FOUND');
  });

  it('adjustPersonality returns GAME_NOT_FOUND when no game exists', () => {
    const res = createGameSession().adjustPersonality({ patience: 5 });
    expect(res.ok === false && res.error.code).toBe('GAME_NOT_FOUND');
  });

  it('startDialogue returns GAME_NOT_FOUND when no game exists', () => {
    const res = elenaSession().startDialogue('npc_scout_elena');
    expect(res.ok === false && res.error.code).toBe('GAME_NOT_FOUND');
  });

  it('chooseDialogueOption returns GAME_NOT_FOUND when no game exists', () => {
    const res = elenaSession().chooseDialogueOption('elena_opt_humble');
    expect(res.ok === false && res.error.code).toBe('GAME_NOT_FOUND');
  });

  it('adjustPersonality with no properties returns a validation error', () => {
    const session = createGameSession();
    session.newGame();

    const res = session.adjustPersonality({});

    expect(res.ok === false && res.error.code).toBe('VALIDATION_ERROR');
  });

  it('startDialogue with unknown NPC returns NPC_NOT_FOUND', () => {
    const session = newElenaGame();

    const res = session.startDialogue('npc_unknown_xyz');

    expect(res.ok === false && res.error.code).toBe('NPC_NOT_FOUND');
  });

  it('startDialogue with missing greet node in tree returns DIALOGUE_NODE_NOT_FOUND', () => {
    const session = createGameSession({ content: { dialogues: { npc_scout_elena: [] } } });
    session.newGame();

    const res = session.startDialogue('npc_scout_elena');

    expect(res.ok === false && res.error.code).toBe('DIALOGUE_NODE_NOT_FOUND');
  });

  it('chooseDialogueOption with gated option when gate fails returns DIALOGUE_OPTION_NOT_AVAILABLE', () => {
    const session = newElenaGame();
    session.startDialogue('npc_scout_elena');

    // Default personality has cunning ~16.67; the gate requires >= 20.
    const res = session.chooseDialogueOption('elena_opt_cunning');

    expect(res.ok === false && res.error.code).toBe('DIALOGUE_OPTION_NOT_AVAILABLE');
  });
});

describe('NPC data integrity', () => {
  it('listNpcs returns exactly 3 NPCs', () => {
    const res = createGameSession().listNpcs();

    expect(res.ok).toBe(true);
    expect(res.ok && res.data).toHaveLength(3);
  });

  it('listNpcs includes all three expected NPC IDs', () => {
    const res = createGameSession().listNpcs();
    expect(res.ok).toBe(true);
    if (!res.ok) return;
    const ids = res.data.map((npc) => npc.id);

    expect(ids).toContain('npc_scout_elena');
    expect(ids).toContain('npc_merchant_lars');
    expect(ids).toContain('npc_outlaw_kade');
  });

  it('getNpc(npc_scout_elena) returns Elena with correct archetype', () => {
    const res = createGameSession().getNpc('npc_scout_elena');

    expect(res.ok).toBe(true);
    if (!res.ok) return;
    expect(res.data.id).toBe('npc_scout_elena');
    expect(res.data.archetype).toBe('Loyal Scout');
  });

  it('getNpc(npc_scout_elena) personality sums to 100%', () => {
    const res = createGameSession().getNpc('npc_scout_elena');
    expect(res.ok).toBe(true);
    if (res.ok) assertPersonalitySum(res.data.personality);
  });

  it('getNpc(npc_merchant_lars) returns Lars with correct archetype', () => {
    const res = createGameSession().getNpc('npc_merchant_lars');

    expect(res.ok).toBe(true);
    if (!res.ok) return;
    expect(res.data.id).toBe('npc_merchant_lars');
    expect(res.data.archetype).toBe('Scheming Merchant');
  });

  it('getNpc(npc_merchant_lars) personality sums to 100%', () => {
    const res = createGameSession().getNpc('npc_merchant_lars');
    expect(res.ok).toBe(true);
    if (res.ok) assertPersonalitySum(res.data.personality);
  });

  it('getNpc(npc_outlaw_kade) returns Kade with correct archetype', () => {
    const res = createGameSession().getNpc('npc_outlaw_kade');

    expect(res.ok).toBe(true);
    if (!res.ok) return;
    expect(res.data.id).toBe('npc_outlaw_kade');
    expect(res.data.archetype).toBe('Rogue Outlaw');
  });

  it('getNpc(npc_outlaw_kade) personality sums to 100%', () => {
    const res = createGameSession().getNpc('npc_outlaw_kade');
    expect(res.ok).toBe(true);
    if (res.ok) assertPersonalitySum(res.data.personality);
  });

  it('getNpc(npc_unknown_xyz) returns NPC_NOT_FOUND', () => {
    const res = createGameSession().getNpc('npc_unknown_xyz');
    expect(res.ok === false && res.error.code).toBe('NPC_NOT_FOUND');
  });

  it('NPC personalities are fixed — consecutive getNpc calls return identical personalities', () => {
    const session = createGameSession();
    const r1 = session.getNpc('npc_scout_elena');
    const r2 = session.getNpc('npc_scout_elena');

    expect(r1.ok && r2.ok).toBe(true);
    if (!r1.ok || !r2.ok) return;
    expect(r1.data.personality).toEqual(r2.data.personality);
  });

  it('NPC data is available without an active game', () => {
    const res = createGameSession().getNpc('npc_scout_elena');
    expect(res.ok).toBe(true);
  });
});

describe('personality constraint invariant', () => {
  function gameSession(): GameSession {
    const session = newElenaGame();
    return session;
  }

  it('default personality after new game sums to 100%', () => {
    const res = gameSession().getState();
    expect(res.ok).toBe(true);
    if (res.ok) assertPersonalitySum(res.data.player.personality);
  });

  it('personality sums to 100% after single positive trait adjustment', () => {
    const res = gameSession().adjustPersonality({ cunning: 10 });
    expect(res.ok).toBe(true);
    if (res.ok) assertPersonalitySum(res.data.player.personality);
  });

  it('personality sums to 100% after single negative trait adjustment', () => {
    const res = gameSession().adjustPersonality({ patience: -5 });
    expect(res.ok).toBe(true);
    if (res.ok) assertPersonalitySum(res.data.player.personality);
  });

  it('personality sums to 100% after multi-trait adjustment', () => {
    const res = gameSession().adjustPersonality({ patience: 5, empathy: 5 });
    expect(res.ok).toBe(true);
    if (res.ok) assertPersonalitySum(res.data.player.personality);
  });

  it('personality sums to 100% after dialogue choice with personality adjustment', () => {
    const res = chooseHumble(gameSession());
    expect(res.ok).toBe(true);
    if (res.ok) assertPersonalitySum(res.data.state.player.personality);
  });

  it('personality sums to 100% after multiple sequential adjustments', () => {
    const session = gameSession();
    session.adjustPersonality({ patience: 8 });
    session.adjustPersonality({ empathy: -3 });
    const res = session.adjustPersonality({ charisma: 6 });

    expect(res.ok).toBe(true);
    if (res.ok) assertPersonalitySum(res.data.player.personality);
  });

  it('all individual traits remain within [5, 35] range after large positive adjustment', () => {
    const res = gameSession().adjustPersonality({ cunning: 30 });

    expect(res.ok).toBe(true);
    if (!res.ok) return;
    const personality = res.data.player.personality;
    for (const value of Object.values(personality)) {
      expect(value).toBeGreaterThanOrEqual(5);
      expect(value).toBeLessThanOrEqual(35);
    }
    assertPersonalitySum(personality);
  });

  it('personality is unchanged after failed validation (empty adjustment)', () => {
    const session = gameSession();
    const before = session.getPersonality();

    session.adjustPersonality({});

    const after = session.getPersonality();
    expect(before.ok && after.ok).toBe(true);
    if (!before.ok || !after.ok) return;
    expect(after.data).toEqual(before.data);
  });
});

describe('dialogue GET start and gate evaluation', () => {
  it('startDialogue returns the placeholder node with id = npcId_greet', () => {
    const session = createGameSession();
    session.newGame();

    const res = session.startDialogue('npc_scout_elena');

    expect(res.ok).toBe(true);
    if (!res.ok) return;
    expect(res.data.node.id).toBe('npc_scout_elena_greet');
    expect(res.data.node.npcId).toBe('npc_scout_elena');
    expect(res.data.node.options.map((o) => o.id)).toEqual(['opt_farewell']);
    expect(res.data.availableOptions.map((o) => o.id)).toEqual(['opt_farewell']);
  });

  it('startDialogue returns correct node id for each NPC', () => {
    const session = createGameSession();
    session.newGame();
    for (const npcId of ['npc_scout_elena', 'npc_merchant_lars', 'npc_outlaw_kade']) {
      const res = session.startDialogue(npcId);

      expect(res.ok).toBe(true);
      expect(res.ok && res.data.node.id).toBe(`${npcId}_greet`);
    }
  });

  it('startDialogue returns NPC_NOT_FOUND for unknown NPC', () => {
    const session = createGameSession();
    session.newGame();

    const res = session.startDialogue('npc_unknown_xyz');

    expect(res.ok === false && res.error.code).toBe('NPC_NOT_FOUND');
  });

  it('startDialogue returns GAME_NOT_FOUND when no game exists', () => {
    const res = createGameSession().startDialogue('npc_scout_elena');
    expect(res.ok === false && res.error.code).toBe('GAME_NOT_FOUND');
  });

  it('gated option is blocked when personality gate is not met', () => {
    const session = newElenaGame();
    session.startDialogue('npc_scout_elena');

    const res = session.chooseDialogueOption('elena_opt_cunning');

    expect(res.ok === false && res.error.code).toBe('DIALOGUE_OPTION_NOT_AVAILABLE');
  });

  it('ungated option always succeeds regardless of personality traits', () => {
    const session = newElenaGame();

    const res = chooseHumble(session);

    expect(res.ok).toBe(true);
  });

  it('gated option succeeds after personality is raised above gate threshold', () => {
    const session = newElenaGame();
    session.adjustPersonality({ cunning: 15 });

    const personality = session.getPersonality();
    expect(personality.ok).toBe(true);
    if (!personality.ok) return;

    session.startDialogue('npc_scout_elena');

    if (personality.data.cunning >= 20) {
      const res = session.chooseDialogueOption('elena_opt_cunning');

      expect(res.ok).toBe(true);
      if (res.ok) assertPersonalitySum(res.data.state.player.personality);
    } else {
      // Clamping kept cunning below the threshold; the invariant must still hold.
      assertPersonalitySum(personality.data);
    }
  });
});

describe('combatState is null throughout Sprint 1', () => {
  it('combatState is null after new game', () => {
    const res = createGameSession().newGame();
    expect(res.ok).toBe(true);
    expect(res.ok && res.data.combatState).toBeNull();
  });

  it('combatState remains null after personality adjustment', () => {
    const session = createGameSession();
    session.newGame();

    const res = session.adjustPersonality({ patience: 5 });

    expect(res.ok).toBe(true);
    expect(res.ok && res.data.combatState).toBeNull();
  });

  it('combatState remains null in game state after dialogue choice', () => {
    const session = newElenaGame();
    chooseHumble(session);

    const state = session.getState();
    expect(state.ok).toBe(true);
    expect(state.ok && state.data.combatState).toBeNull();
  });
});

describe('player endpoint', () => {
  it('getPlayer returns player data', () => {
    const session = createGameSession();
    session.newGame();

    const res = session.getPlayer();

    expect(res.ok).toBe(true);
    expect(res.ok && res.data.name).toBe('Kael');
  });

  it('getPlayer returns GAME_NOT_FOUND when no game exists', () => {
    const res = createGameSession().getPlayer();
    expect(res.ok === false && res.error.code).toBe('GAME_NOT_FOUND');
  });

  it('player personality is accessible and sums to 100%', () => {
    const session = createGameSession();
    session.newGame();

    const res = session.getPlayer();

    expect(res.ok).toBe(true);
    if (res.ok) assertPersonalitySum(res.data.personality);
  });
});
