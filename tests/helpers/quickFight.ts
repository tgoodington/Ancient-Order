import type { EncounterConfig } from '../../src/types/combat.js';
import { defaultContent } from '../../src/session/index.js';
import type { GameSession } from '../../src/session/index.js';

type Side = 'player' | 'enemy';

/**
 * Builds a one-sided copy of encounter_test: every combatant on the `weak` side
 * has 1 stamina, so the fight ends within a few rounds. Used to reach a real
 * victory or defeat through the session without touching engine internals.
 */
export function lopsidedEncounter(id: string, weak: Side): EncounterConfig {
  const base = defaultContent.encounters.encounter_test;
  const thin = (party: EncounterConfig['playerParty']) =>
    party.map((c) => ({ ...c, stamina: 1, power: 1 }));
  const strong = (party: EncounterConfig['playerParty']) =>
    party.map((c) => ({ ...c, stamina: 5000, power: 200 }));
  return {
    ...base,
    id,
    playerParty: weak === 'player' ? thin(base.playerParty) : strong(base.playerParty),
    enemyParty: weak === 'enemy' ? thin(base.enemyParty) : strong(base.enemyParty),
  };
}

export const WIN_ENCOUNTER_ID = 'encounter_quick_win';
export const LOSS_ENCOUNTER_ID = 'encounter_quick_loss';

export const QUICK_FIGHT_ENCOUNTERS = {
  [WIN_ENCOUNTER_ID]: lopsidedEncounter(WIN_ENCOUNTER_ID, 'enemy'),
  [LOSS_ENCOUNTER_ID]: lopsidedEncounter(LOSS_ENCOUNTER_ID, 'player'),
};

const ATTACKS = [
  { combatantId: 'player_warrior', type: 'ATTACK', targetId: 'enemy_brute' },
  { combatantId: 'player_scout', type: 'ATTACK', targetId: 'enemy_archer' },
  { combatantId: 'player_mage', type: 'ATTACK', targetId: 'enemy_shaman' },
];

/** Advances rounds (re-declaring attacks each round) until the fight ends. */
export function fightToEnd(session: GameSession, maxRounds = 60): 'victory' | 'defeat' | 'active' {
  let status = 'active';
  for (let i = 0; i < maxRounds && status === 'active'; i++) {
    session.declareActions(ATTACKS);
    const res = session.advanceRound();
    if (!res.ok) throw new Error(`advanceRound failed: ${res.error.code}`);
    status = res.data.status;
  }
  return status as 'victory' | 'defeat' | 'active';
}
