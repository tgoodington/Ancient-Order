import { describe, it, expect } from 'vitest';
import { createGameSession } from './index.js';
import { defaultContent } from './content.js';
import { validateDialogueTree } from '../dialogue/dialogueEngine.js';

describe('placeholder dialogue content', () => {
  it('has no dead-end nodes', () => {
    for (const tree of Object.values(defaultContent.dialogues)) {
      expect(validateDialogueTree(tree).valid).toBe(true);
    }
  });
});

describe('narrative and dialogue smoke', () => {
  it('refuses an unknown scene and leaves the game unchanged', () => {
    const session = createGameSession();
    session.newGame();
    const snap = session.getSnapshot();
    const r = session.startScene('nope');
    expect(r.ok === false && r.error.code).toBe('SCENE_NOT_FOUND');
    expect(session.getSnapshot()).toBe(snap);
  });

  it('starts a scene and makes a choice', () => {
    const session = createGameSession();
    session.newGame();
    expect(session.startScene('scene_opening').ok).toBe(true);
    expect(session.getCurrentScene().ok).toBe(true);
    const r = session.makeChoice('choice_cautious');
    expect(r.ok).toBe(true);
    expect(session.getSynergy().ok).toBe(true);
  });

  it('runs a placeholder conversation and ends it', () => {
    const session = createGameSession();
    expect(session.startDialogue('npc_scout_elena').ok).toBe(false);
    session.newGame();
    const none = session.chooseDialogueOption('opt_farewell');
    expect(none.ok === false && none.error.code).toBe('DIALOGUE_NOT_ACTIVE');
    const start = session.startDialogue('npc_scout_elena');
    expect(start.ok && start.data.node.id).toBe('npc_scout_elena_greet');
    expect(session.getSnapshot().game?.currentDialogueNode).toBe('npc_scout_elena_greet');
    const end = session.chooseDialogueOption('opt_farewell');
    expect(end.ok && end.data.nextNode).toBeNull();
    expect(session.getSnapshot().game?.currentDialogueNode).toBeNull();
  });
});

describe('startDialogue prototype-key ids', () => {
  it.each(['constructor', '__proto__', 'toString'])('refuses %s as NPC_NOT_FOUND', (id) => {
    const session = createGameSession();
    session.newGame();
    const snap = session.getSnapshot();
    const r = session.startDialogue(id);
    expect(r.ok === false && r.error.code).toBe('NPC_NOT_FOUND');
    expect(session.getSnapshot()).toBe(snap);
  });
});
