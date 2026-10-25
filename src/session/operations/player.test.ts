import { describe, it, expect, vi } from 'vitest';
import { createGameSession } from '../index.js';

const PAIR = ['npc_scout_elena', 'npc_merchant_lars'];

function activeSession() {
  const session = createGameSession();
  session.newGame();
  return session;
}

describe('player reads', () => {
  it('refuse without a game', () => {
    const session = createGameSession();
    const player = session.getPlayer();
    const personality = session.getPersonality();
    expect(player.ok === false && player.error.code).toBe('GAME_NOT_FOUND');
    expect(personality.ok === false && personality.error.code).toBe('GAME_NOT_FOUND');
  });
});

describe('adjustPersonality', () => {
  it('refuses without a game', () => {
    const r = createGameSession().adjustPersonality({ patience: 5 });
    expect(r.ok === false && r.error.code).toBe('GAME_NOT_FOUND');
  });

  it('applies the adjustment to a new game state and leaves the earlier one untouched', () => {
    const session = activeSession();
    const before = session.getSnapshot().game!;
    const patienceBefore = before.player.personality.patience;

    const r = session.adjustPersonality({ patience: 5 });

    expect(r.ok).toBe(true);
    const after = session.getSnapshot().game!;
    expect(after).not.toBe(before);
    expect(after.player.personality.patience).toBeGreaterThan(patienceBefore);
    expect(before.player.personality.patience).toBe(patienceBefore);
  });

  it('refuses invalid input without changing or notifying', () => {
    const session = activeSession();
    const snap = session.getSnapshot();
    const listener = vi.fn();
    session.subscribe(listener);
    for (const bad of [{}, { courage: 5 }, { patience: 'a' }, [], null, 'patience']) {
      const r = session.adjustPersonality(bad);
      expect(r.ok === false && r.error.code).toBe('VALIDATION_ERROR');
    }
    expect(session.getSnapshot()).toBe(snap);
    expect(listener).not.toHaveBeenCalled();
  });
});

describe('setTeam', () => {
  it('refuses without a game', () => {
    const r = createGameSession().setTeam(PAIR);
    expect(r.ok === false && r.error.code).toBe('GAME_NOT_FOUND');
  });

  it('sets a team of two companions', () => {
    const session = activeSession();
    const r = session.setTeam(PAIR);
    expect(r.ok && r.data.team).toEqual(PAIR);
    expect(session.getSnapshot().game?.team).toEqual(PAIR);
  });

  it('refuses anything that is not exactly two string ids', () => {
    const session = activeSession();
    const snap = session.getSnapshot();
    const cases: unknown[] = [
      'npc_scout_elena',
      [],
      ['npc_scout_elena'],
      ['npc_scout_elena', 'npc_merchant_lars', 'npc_outlaw_kade'],
      ['npc_scout_elena', 5],
      ['npc_scout_elena', 'x'.repeat(65)],
    ];
    for (const bad of cases) {
      const r = session.setTeam(bad);
      expect(r.ok === false && r.error.code).toBe('VALIDATION_ERROR');
    }
    expect(session.getSnapshot()).toBe(snap);
  });

  it('refuses an unknown companion with TEAM_COMPOSITION_INVALID', () => {
    const session = activeSession();
    const r = session.setTeam(['npc_scout_elena', 'npc_nobody']);
    expect(r.ok === false && r.error.code).toBe('TEAM_COMPOSITION_INVALID');
    expect(r.ok === false && r.error.message).toBe('Invalid NPC ID: npc_nobody');
  });

  it('refuses the same companion twice with TEAM_COMPOSITION_INVALID', () => {
    const session = activeSession();
    const snap = session.getSnapshot();
    const r = session.setTeam(['npc_scout_elena', 'npc_scout_elena']);
    expect(r.ok === false && r.error.code).toBe('TEAM_COMPOSITION_INVALID');
    expect(session.getSnapshot()).toBe(snap);
  });

  it('is locked during combat, and the refusal changes nothing', () => {
    const session = activeSession();
    session.startEncounter('encounter_test');
    const snap = session.getSnapshot();
    const r = session.setTeam(PAIR);
    expect(r.ok === false && r.error.code).toBe('TEAM_COMPOSITION_INVALID');
    expect(r.ok === false && r.error.message).toBe('Cannot change team during combat');
    expect(session.getSnapshot()).toBe(snap);
  });

  it('is locked during a narrative scene, and unlocks when the narrative is reset', () => {
    const session = activeSession();
    session.startScene('scene_opening');
    const r = session.setTeam(PAIR);
    expect(r.ok === false && r.error.code).toBe('TEAM_COMPOSITION_INVALID');
    expect(r.ok === false && r.error.message).toBe('Cannot change team during narrative scene');

    session.resetNarrative();
    expect(session.setTeam(PAIR).ok).toBe(true);
  });
});
