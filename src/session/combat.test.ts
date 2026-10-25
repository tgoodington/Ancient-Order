import { describe, it, expect } from 'vitest';
import { createGameSession } from './index.js';
import { ENCOUNTER_REGISTRY } from './encounters.js';

describe('combat smoke', () => {
  it('startEncounter refuses prototype keys with ENCOUNTER_NOT_FOUND', () => {
    const session = createGameSession();
    session.newGame();
    const snap = session.getSnapshot();
    for (const key of ['constructor', '__proto__', 'toString']) {
      const r = session.startEncounter(key);
      expect(r.ok === false && r.error.code).toBe('ENCOUNTER_NOT_FOUND');
    }
    expect(session.getSnapshot()).toBe(snap);
  });

  it('starts, refuses a second start, and reads the view', () => {
    const session = createGameSession();
    expect(session.getCombatView().ok).toBe(false);
    session.newGame();
    const started = session.startEncounter('encounter_test');
    expect(started.ok && started.data.status).toBe('active');
    const again = session.startEncounter('encounter_test');
    expect(again.ok === false && again.error.code).toBe('COMBAT_ALREADY_ACTIVE');
    expect(session.getCombatView().ok).toBe(true);
    const result = session.getCombatResult();
    expect(result.ok && result.data.status).toBe('active');
  });

  it('refuses a fully invalid declaration and keeps previous orders', () => {
    const session = createGameSession();
    session.newGame();
    session.startEncounter('encounter_test');
    const good = session.declareActions([
      { combatantId: 'player_warrior', type: 'ATTACK', targetId: 'enemy_brute' },
    ]);
    expect(good.ok).toBe(true);
    const pending = session.getSnapshot().pendingDeclarations;
    const bad = session.declareActions([{ combatantId: 'nobody', type: 'ATTACK' }]);
    expect(bad.ok === false && bad.error.code).toBe('COMBAT_INVALID_DECLARATION');
    expect(session.getSnapshot().pendingDeclarations).toBe(pending);
  });

  it('refuses malformed declarations', () => {
    const session = createGameSession();
    session.newGame();
    const r = session.declareActions('x');
    expect(r.ok === false && r.error.code).toBe('VALIDATION_ERROR');
  });
});

describe('player order rules', () => {
  // Ascension level 3 gives the mage 2 starting energy segments (ASCENSION_STARTING_SEGMENTS).
  const ENERGY_SEGMENTS = 2;

  function started() {
    const base = ENCOUNTER_REGISTRY.encounter_test;
    const encounter_energy = {
      ...base,
      id: 'encounter_energy',
      playerParty: base.playerParty.map((c) =>
        c.id === 'player_mage' ? { ...c, ascensionLevel: 3 as const } : c
      ),
    };
    const session = createGameSession({
      content: { encounters: { ...ENCOUNTER_REGISTRY, encounter_energy } },
    });
    session.newGame();
    session.startEncounter('encounter_energy');
    const energy =
      session.getSnapshot().game?.combatState?.playerParty.find((c) => c.id === 'player_mage')
        ?.energy ?? 0;
    expect(energy).toBe(ENERGY_SEGMENTS);
    return { session, energy };
  }

  function refusedReason(session: ReturnType<typeof createGameSession>, action: object) {
    const pending = session.getSnapshot().pendingDeclarations;
    const r = session.declareActions([action]);
    expect(r.ok === false && r.error.code).toBe('COMBAT_INVALID_DECLARATION');
    expect(session.getSnapshot().pendingDeclarations).toBe(pending);
    const details = r.ok === false ? (r.error.details as { rejected: { error: string }[] }) : null;
    return details?.rejected[0].error;
  }

  it('rejects an enemy combatant order', () => {
    const { session } = started();
    const reason = refusedReason(session, {
      combatantId: 'enemy_brute',
      type: 'ATTACK',
      targetId: 'player_warrior',
    });
    expect(reason).toBe("Combatant 'enemy_brute' is not in the player's party");
  });

  it('rejects negative, zero, fractional and over-energy segments', () => {
    const { session, energy } = started();
    for (const energySegments of [-1, 0, 1.5, energy + 1]) {
      const reason = refusedReason(session, {
        combatantId: 'player_mage',
        type: 'SPECIAL',
        targetId: 'enemy_brute',
        energySegments,
      });
      expect(reason).toBe(`energySegments must be an integer between 1 and ${energy}`);
    }
  });

  it('rejects segments on an ATTACK', () => {
    const { session } = started();
    const reason = refusedReason(session, {
      combatantId: 'player_mage',
      type: 'ATTACK',
      targetId: 'enemy_brute',
      energySegments: 1,
    });
    expect(reason).toBe('energySegments is only allowed for SPECIAL');
  });

  it('accepts a SPECIAL with in-range segments', () => {
    const { session, energy } = started();
    expect(energy).toBeGreaterThanOrEqual(1);
    const r = session.declareActions([
      { combatantId: 'player_mage', type: 'SPECIAL', targetId: 'enemy_brute', energySegments: energy },
    ]);
    expect(r.ok && r.data.accepted.length).toBe(1);
  });
});
