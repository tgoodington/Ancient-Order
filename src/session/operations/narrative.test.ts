import { describe, it, expect, vi } from 'vitest';
import { createGameSession } from '../index.js';
import type { SceneGraph } from '../../types/narrative.js';
import { PREREQUISITE_SCENE_GRAPH } from '../../narrative/fixtures.js';

function activeSession(options?: Parameters<typeof createGameSession>[0]) {
  const session = createGameSession(options);
  session.newGame();
  return session;
}

describe('startScene', () => {
  it('refuses without a game', () => {
    const r = createGameSession().startScene('scene_opening');
    expect(r.ok === false && r.error.code).toBe('GAME_NOT_FOUND');
  });

  it('refuses a non-string or over-long id with VALIDATION_ERROR', () => {
    const session = activeSession();
    expect(session.startScene(7).ok === false).toBe(true);
    const bad = session.startScene('s'.repeat(129));
    expect(bad.ok === false && bad.error.code).toBe('VALIDATION_ERROR');
    const num = session.startScene(7);
    expect(num.ok === false && num.error.code).toBe('VALIDATION_ERROR');
  });

  it('refuses an unknown scene and leaves state alone (approved fix)', () => {
    const session = activeSession();
    const snap = session.getSnapshot();
    const listener = vi.fn();
    session.subscribe(listener);

    const r = session.startScene('scene_does_not_exist');

    expect(r.ok === false && r.error.code).toBe('SCENE_NOT_FOUND');
    expect(session.getSnapshot()).toBe(snap);
    expect(session.getSnapshot().game?.narrativeState).toBeNull();
    expect(listener).not.toHaveBeenCalled();
  });

  it('starts a known scene and notifies once', () => {
    const session = activeSession();
    const listener = vi.fn();
    session.subscribe(listener);

    const r = session.startScene('scene_opening');

    expect(r.ok && r.data.narrativeState.currentSceneId).toBe('scene_opening');
    expect(listener).toHaveBeenCalledTimes(1);
  });

  it('accepts a scene from content injected into the session', () => {
    const graph: SceneGraph = [
      { id: 'only', title: 'Only', text: 't', prerequisites: [], choices: [{ id: 'end', text: 'End', nextSceneId: null }] },
    ];
    const session = activeSession({ content: { sceneGraph: graph } });
    expect(session.startScene('only').ok).toBe(true);
    expect(session.startScene('scene_opening').ok).toBe(false);
  });
});

describe('getCurrentScene and getNarrativeState', () => {
  it('refuse without a game', () => {
    const session = createGameSession();
    const scene = session.getCurrentScene();
    const state = session.getNarrativeState();
    expect(scene.ok === false && scene.error.code).toBe('GAME_NOT_FOUND');
    expect(state.ok === false && state.error.code).toBe('GAME_NOT_FOUND');
  });

  it('getCurrentScene refuses before a scene is started', () => {
    const r = activeSession().getCurrentScene();
    expect(r.ok === false && r.error.code).toBe('NARRATIVE_NOT_STARTED');
  });

  it('getNarrativeState is null before a scene and set after', () => {
    const session = activeSession();
    const before = session.getNarrativeState();
    expect(before.ok && before.data.narrativeState).toBeNull();
    session.startScene('scene_opening');
    const after = session.getNarrativeState();
    expect(after.ok && after.data.narrativeState?.currentSceneId).toBe('scene_opening');
  });

  it('getCurrentScene lists the choices available to the player', () => {
    const session = activeSession();
    session.startScene('scene_opening');
    const r = session.getCurrentScene();
    expect(r.ok).toBe(true);
    if (!r.ok) return;
    expect(r.data.scene?.id).toBe('scene_opening');
    // charisma gate on choice_bold is not met by the default personality
    const ids = r.data.availableChoices.map((c) => c.id);
    expect(ids).toContain('choice_cautious');
    expect(ids).not.toContain('choice_bold');
  });
});

describe('makeChoice', () => {
  it('refuses without a game', () => {
    const r = createGameSession().makeChoice('choice_cautious');
    expect(r.ok === false && r.error.code).toBe('GAME_NOT_FOUND');
  });

  it('refuses a non-string id with VALIDATION_ERROR', () => {
    const session = activeSession();
    session.startScene('scene_opening');
    const r = session.makeChoice(1);
    expect(r.ok === false && r.error.code).toBe('VALIDATION_ERROR');
  });

  it('refuses before a scene is started', () => {
    const r = activeSession().makeChoice('choice_cautious');
    expect(r.ok === false && r.error.code).toBe('NARRATIVE_NOT_STARTED');
  });

  it('refuses an unknown choice and a gated choice, leaving state alone', () => {
    const session = activeSession();
    session.startScene('scene_opening');
    const snap = session.getSnapshot();
    const unknown = session.makeChoice('choice_nope');
    expect(unknown.ok === false && unknown.error.code).toBe('CHOICE_NOT_FOUND');
    const gated = session.makeChoice('choice_bold');
    expect(gated.ok === false && gated.error.code).toBe('CHOICE_NOT_AVAILABLE');
    expect(session.getSnapshot()).toBe(snap);
  });

  it('applies the consequence and advances to the next scene', () => {
    const session = activeSession();
    session.startScene('scene_opening');
    const cunningBefore = session.getSnapshot().game!.player.personality.cunning;

    const r = session.makeChoice('choice_cautious');

    expect(r.ok).toBe(true);
    if (!r.ok) return;
    expect(r.data.nextScene?.id).toBe('scene_two');
    expect(r.data.narrativeState.currentSceneId).toBe('scene_two');
    expect(session.getSnapshot().game!.player.personality.cunning).toBeGreaterThan(cunningBefore);
  });

  it('keeps the consequence and the advanced narrative state together, so a flag it sets opens a later scene', () => {
    const graph: SceneGraph = [
      {
        id: 'a',
        title: 'A',
        text: 'a',
        prerequisites: [],
        choices: [
          {
            id: 'open',
            text: 'Open',
            consequence: { setFlags: ['key'], personalityEffect: { logic: 2 } },
            nextSceneId: 'b',
          },
        ],
      },
      {
        id: 'b',
        title: 'B',
        text: 'b',
        prerequisites: [],
        choices: [{ id: 'go', text: 'Go', nextSceneId: 'c' }],
      },
      {
        id: 'c',
        title: 'C',
        text: 'c',
        prerequisites: [{ type: 'flag', flag: 'key' }],
        choices: [{ id: 'end', text: 'End', nextSceneId: null }],
      },
    ];
    const session = activeSession({ content: { sceneGraph: graph } });
    session.startScene('a');
    const logicBefore = session.getSnapshot().game!.player.personality.logic;

    const first = session.makeChoice('open');
    expect(first.ok && first.data.narrativeState.choiceFlags.key).toBe(true);
    expect(session.getSnapshot().game!.narrativeState?.currentSceneId).toBe('b');
    expect(session.getSnapshot().game!.player.personality.logic).toBeGreaterThan(logicBefore);

    const second = session.makeChoice('go');
    expect(second.ok && second.data.nextScene?.id).toBe('c');
  });

  it('refuses a move into a scene whose prerequisites are unmet, leaving state alone', () => {
    const session = activeSession({ content: { sceneGraph: PREREQUISITE_SCENE_GRAPH } });
    session.startScene('scene_prereq_start');
    const snap = session.getSnapshot();

    const blocked = session.makeChoice('choice_go_flag');
    expect(blocked.ok === false && blocked.error.code).toBe('PREREQUISITE_NOT_MET');
    expect(session.getSnapshot()).toBe(snap);
  });

  it('ends the narrative when the last choice has no next scene', () => {
    const session = activeSession();
    session.startScene('scene_opening');
    session.makeChoice('choice_cautious');
    session.makeChoice('choice_proceed');
    const r = session.makeChoice('choice_finish');
    expect(r.ok && r.data.nextScene).toBeNull();
  });
});

describe('resetNarrative and getSynergy', () => {
  it('refuse without a game', () => {
    const session = createGameSession();
    const reset = session.resetNarrative();
    const synergy = session.getSynergy();
    expect(reset.ok === false && reset.error.code).toBe('GAME_NOT_FOUND');
    expect(synergy.ok === false && synergy.error.code).toBe('GAME_NOT_FOUND');
  });

  it('resetNarrative clears the narrative state', () => {
    const session = activeSession();
    session.startScene('scene_opening');
    const r = session.resetNarrative();
    expect(r.ok && r.data.narrativeState).toBeNull();
    expect(session.getSnapshot().game?.narrativeState).toBeNull();
  });

  it('getSynergy reads without changing the session', () => {
    const session = activeSession();
    const snap = session.getSnapshot();
    const r = session.getSynergy();
    expect(r.ok && r.data.synergy).toBeDefined();
    expect(session.getSnapshot()).toBe(snap);
  });
});
