import { describe, it, expect } from 'vitest';
import { createGameSession } from './index.js';
import { defaultContent } from './content.js';
import { ENCOUNTER_REGISTRY } from './encounters.js';

describe('session content isolation', () => {
  it('returned scene and dialogue node are frozen', () => {
    const session = createGameSession();
    session.newGame();
    session.startScene('scene_opening');
    const scene = session.getCurrentScene();
    expect(scene.ok).toBe(true);
    if (scene.ok && scene.data.scene) {
      expect(Object.isFrozen(scene.data.scene)).toBe(true);
    }
    const npcId = Object.keys(defaultContent.dialogues)[0];
    const d = session.startDialogue(npcId);
    expect(d.ok).toBe(true);
    if (d.ok) {
      expect(Object.isFrozen(d.data.node)).toBe(true);
      expect(Object.isFrozen(d.data.node.options)).toBe(true);
    }
  });

  it('encounter content is frozen, engine runs on it, originals stay unfrozen', () => {
    const session = createGameSession();
    session.newGame();
    expect(session.startEncounter('encounter_test').ok).toBe(true);
    expect(session.advanceRound().ok).toBe(true);
    expect(Object.isFrozen(ENCOUNTER_REGISTRY)).toBe(false);
    expect(Object.isFrozen(ENCOUNTER_REGISTRY.encounter_test)).toBe(false);
  });

  it('mutating source objects after creation does not affect an existing session', () => {
    const encounters = structuredCloneLike(ENCOUNTER_REGISTRY);
    const session = createGameSession({ content: { encounters } });
    session.newGame();
    (encounters.encounter_test as { id: string }).id = 'mutated';
    delete (encounters as Record<string, unknown>).encounter_test;
    const r = session.startEncounter('encounter_test');
    expect(r.ok && r.data.encounterId).toBe('encounter_test');
  });

  it('two sessions do not share content', () => {
    const a = createGameSession();
    const b = createGameSession();
    a.newGame();
    b.newGame();
    const ra = a.startEncounter('encounter_test');
    const rb = b.startEncounter('encounter_test');
    expect(ra.ok && rb.ok).toBe(true);
    const npcId = Object.keys(defaultContent.dialogues)[0];
    const da = a.startDialogue(npcId);
    const db = b.startDialogue(npcId);
    expect(da.ok && db.ok && da.data.node !== db.data.node).toBe(true);
  });
});

function structuredCloneLike<T>(v: T): T {
  return JSON.parse(JSON.stringify(v)) as T;
}
