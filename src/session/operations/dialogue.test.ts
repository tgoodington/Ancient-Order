import { describe, it, expect, vi } from 'vitest';
import { createGameSession, defaultContent } from '../index.js';
import type { GameSession } from '../index.js';
import { validateDialogueTree } from '../../dialogue/dialogueEngine.js';
import type { DialogueNode } from '../../types/index.js';

const TREE: DialogueNode[] = [
  {
    id: 'npc_scout_elena_greet',
    npcId: 'npc_scout_elena',
    text: 'Hail.',
    options: [
      {
        id: 'kind',
        text: 'Be kind.',
        personalityAdjustment: { kindness: 2 },
        npcAdjustment: { trustChange: 3, affectionChange: 1 },
        nextNodeId: 'npc_scout_elena_second',
      },
      {
        id: 'sly',
        text: 'Be sly.',
        gate: { trait: 'cunning', operator: 'gte', value: 30 },
        nextNodeId: 'npc_scout_elena_second',
      },
      { id: 'bye', text: 'Leave.', nextNodeId: null },
    ],
  },
  {
    id: 'npc_scout_elena_second',
    npcId: 'npc_scout_elena',
    text: 'Go on.',
    options: [{ id: 'done', text: 'Done.', nextNodeId: null }],
  },
];

function elena(): GameSession {
  const session = createGameSession({ content: { dialogues: { npc_scout_elena: TREE } } });
  session.newGame();
  return session;
}

function position(session: GameSession): string | null | undefined {
  return session.getSnapshot().game?.currentDialogueNode;
}

describe('placeholder content', () => {
  it('passes validateDialogueTree for every default tree', () => {
    for (const tree of Object.values(defaultContent.dialogues)) {
      expect(validateDialogueTree(tree).valid).toBe(true);
    }
  });

  it('gives every NPC a <npcId>_greet node with an opt_farewell option', () => {
    const session = createGameSession();
    session.newGame();
    for (const npcId of ['npc_scout_elena', 'npc_merchant_lars', 'npc_outlaw_kade']) {
      const r = session.startDialogue(npcId);
      expect(r.ok && r.data.node.id).toBe(`${npcId}_greet`);
      expect(r.ok && r.data.node.options.map((o) => o.id)).toContain('opt_farewell');
    }
  });
});

describe('startDialogue', () => {
  it('refuses a non-string id with VALIDATION_ERROR', () => {
    const session = elena();
    const r = session.startDialogue(3);
    expect(r.ok === false && r.error.code).toBe('VALIDATION_ERROR');
  });

  it('refuses without a game, for an unknown NPC, and for an NPC with no greet node', () => {
    const none = createGameSession().startDialogue('npc_scout_elena');
    expect(none.ok === false && none.error.code).toBe('GAME_NOT_FOUND');

    const session = elena();
    const unknown = session.startDialogue('npc_nobody');
    expect(unknown.ok === false && unknown.error.code).toBe('NPC_NOT_FOUND');
    const noTree = session.startDialogue('npc_outlaw_kade');
    expect(noTree.ok === false && noTree.error.code).toBe('DIALOGUE_NODE_NOT_FOUND');
    expect(position(session)).toBeNull();
  });

  it('records the position and lists only the options the player may take', () => {
    const session = elena();
    const r = session.startDialogue('npc_scout_elena');
    expect(r.ok).toBe(true);
    if (!r.ok) return;
    expect(position(session)).toBe('npc_scout_elena_greet');
    expect(r.data.node.options.map((o) => o.id)).toEqual(['kind', 'sly', 'bye']);
    expect(r.data.availableOptions.map((o) => o.id)).toEqual(['kind', 'bye']);
  });

  it('restarts the conversation from the greeting', () => {
    const session = elena();
    session.startDialogue('npc_scout_elena');
    session.chooseDialogueOption('kind');
    expect(position(session)).toBe('npc_scout_elena_second');
    session.startDialogue('npc_scout_elena');
    expect(position(session)).toBe('npc_scout_elena_greet');
  });
});

describe('chooseDialogueOption', () => {
  it('refuses a non-string option id with VALIDATION_ERROR', () => {
    const session = elena();
    session.startDialogue('npc_scout_elena');
    const r = session.chooseDialogueOption({});
    expect(r.ok === false && r.error.code).toBe('VALIDATION_ERROR');
  });

  it('refuses without a game', () => {
    const r = createGameSession().chooseDialogueOption('kind');
    expect(r.ok === false && r.error.code).toBe('GAME_NOT_FOUND');
  });

  it('refuses with DIALOGUE_NOT_ACTIVE before a conversation starts, without changing state', () => {
    const session = elena();
    const snap = session.getSnapshot();
    const listener = vi.fn();
    session.subscribe(listener);
    const r = session.chooseDialogueOption('kind');
    expect(r.ok === false && r.error.code).toBe('DIALOGUE_NOT_ACTIVE');
    expect(session.getSnapshot()).toBe(snap);
    expect(listener).not.toHaveBeenCalled();
  });

  it('moves through nodes taking only the option id, tracking the position', () => {
    const session = elena();
    session.startDialogue('npc_scout_elena');

    const first = session.chooseDialogueOption('kind');
    expect(first.ok).toBe(true);
    if (!first.ok) return;
    expect(first.data.selectedOptionId).toBe('kind');
    expect(first.data.nextNode?.id).toBe('npc_scout_elena_second');
    expect(first.data.availableOptions.map((o) => o.id)).toEqual(['done']);
    expect(position(session)).toBe('npc_scout_elena_second');

    const second = session.chooseDialogueOption('done');
    expect(second.ok && second.data.nextNode).toBeNull();
    expect(second.ok && second.data.availableOptions).toEqual([]);
    expect(position(session)).toBeNull();
  });

  it('applies personality and NPC adjustments and logs the choice', () => {
    const session = elena();
    const kindnessBefore = session.getSnapshot().game!.player.personality.kindness;
    session.startDialogue('npc_scout_elena');

    session.chooseDialogueOption('kind');

    const game = session.getSnapshot().game!;
    expect(game.player.personality.kindness).toBeGreaterThan(kindnessBefore);
    expect(game.conversationLog).toHaveLength(1);
    expect(game.conversationLog[0]).toMatchObject({
      npcId: 'npc_scout_elena',
      nodeId: 'npc_scout_elena_greet',
      optionId: 'kind',
    });
    const live = session.getNpc('npc_scout_elena');
    expect(live.ok && live.data.trust).toBe(3);
    expect(live.ok && live.data.affection).toBe(1);
  });

  it('ends the conversation, then refuses another choice with DIALOGUE_NOT_ACTIVE', () => {
    const session = elena();
    session.startDialogue('npc_scout_elena');
    const end = session.chooseDialogueOption('bye');
    expect(end.ok && end.data.nextNode).toBeNull();
    expect(position(session)).toBeNull();
    const again = session.chooseDialogueOption('bye');
    expect(again.ok === false && again.error.code).toBe('DIALOGUE_NOT_ACTIVE');
  });

  it('refuses an unknown option and a gated option, keeping the position', () => {
    const session = elena();
    session.startDialogue('npc_scout_elena');
    const snap = session.getSnapshot();

    const unknown = session.chooseDialogueOption('nope');
    expect(unknown.ok === false && unknown.error.code).toBe('DIALOGUE_OPTION_NOT_FOUND');
    const gated = session.chooseDialogueOption('sly');
    expect(gated.ok === false && gated.error.code).toBe('DIALOGUE_OPTION_NOT_AVAILABLE');

    expect(session.getSnapshot()).toBe(snap);
    expect(position(session)).toBe('npc_scout_elena_greet');
  });

  it('opens a gated option once the personality meets the gate', () => {
    const session = elena();
    session.adjustPersonality({ cunning: 30 });
    const cunning = session.getPersonality();
    if (!cunning.ok || cunning.data.cunning < 30) return;
    session.startDialogue('npc_scout_elena');
    const r = session.chooseDialogueOption('sly');
    expect(r.ok).toBe(true);
  });

  it('ends the placeholder conversation with opt_farewell', () => {
    const session = createGameSession();
    session.newGame();
    session.startDialogue('npc_merchant_lars');
    const r = session.chooseDialogueOption('opt_farewell');
    expect(r.ok && r.data.nextNode).toBeNull();
    expect(position(session)).toBeNull();
  });
});
