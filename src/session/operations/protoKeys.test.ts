import { describe, it, expect } from 'vitest';
import { createGameSession } from '../index.js';

const KEYS = ['constructor', '__proto__', 'toString'];

describe('prototype keys', () => {
  for (const withGame of [false, true]) {
    for (const key of KEYS) {
      it(`getNpc('${key}') is NPC_NOT_FOUND ${withGame ? 'with' : 'without'} a game`, () => {
        const session = createGameSession();
        if (withGame) session.newGame();
        const r = session.getNpc(key);
        expect(r.ok === false && r.error.code).toBe('NPC_NOT_FOUND');
      });
    }
  }

  it('getNpc refuses a non-string id', () => {
    const r = createGameSession().getNpc(5 as unknown as string);
    expect(r.ok === false && r.error.code).toBe('VALIDATION_ERROR');
  });

  it('setTeam refuses prototype keys and leaves the snapshot unchanged', () => {
    const session = createGameSession();
    session.newGame();
    const snap = session.getSnapshot();
    const r = session.setTeam(['constructor', 'toString']);
    expect(r.ok === false && r.error.code).toBe('TEAM_COMPOSITION_INVALID');
    expect(r.ok === false && r.error.message).toBe('Invalid NPC ID: constructor');
    expect(session.getSnapshot()).toBe(snap);
  });
});
