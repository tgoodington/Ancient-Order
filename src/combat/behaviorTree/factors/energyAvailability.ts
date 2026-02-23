/**
 * EnergyAvailability factor — Resource management scoring.
 *
 * Models the combatant's motivation to spend accumulated energy on SPECIAL actions.
 * High energy reserves create strong pressure toward SPECIAL.
 * Zero energy relies on ATTACK/EVADE (SPECIAL is already filtered from candidates
 * when energy == 0, so its score in that case is moot but returned as 0).
 *
 * Bracket table (from design spec):
 *   selfEnergy >= 3 : { ATTACK: -0.1, DEFEND: 0, EVADE: -0.2, SPECIAL: 0.7, GROUP: 0 }
 *   selfEnergy 1-2  : { ATTACK:  0.0, DEFEND: 0, EVADE:  0.0, SPECIAL: 0.3, GROUP: 0 }
 *   selfEnergy == 0 : { ATTACK:  0.1, DEFEND: 0, EVADE:  0.1, SPECIAL: 0.0, GROUP: 0 }
 *
 * Note: selfEnergy is an integer segment count, not a continuous pct.
 * No linear interpolation within brackets — brackets map directly to segment counts.
 */

import type { ActionScores, ScoringFactor, CombatPerception, TargetPerception } from '../../../types/combat.js';

const HIGH_ENERGY_SCORES: ActionScores = { ATTACK: -0.1, DEFEND: 0, EVADE: -0.2, SPECIAL: 0.7, GROUP: 0 };
const MID_ENERGY_SCORES: ActionScores  = { ATTACK:  0.0, DEFEND: 0, EVADE:  0.0, SPECIAL: 0.3, GROUP: 0 };
const NO_ENERGY_SCORES: ActionScores   = { ATTACK:  0.1, DEFEND: 0, EVADE:  0.1, SPECIAL: 0.0, GROUP: 0 };

export const energyAvailabilityFactor: ScoringFactor = {
  name: 'energyAvailability',

  evaluate(self: CombatPerception, _target: TargetPerception | null): ActionScores {
    const energy = self.selfEnergy;

    if (energy >= 3) {
      return HIGH_ENERGY_SCORES;
    } else if (energy >= 1) {
      // 1 or 2 segments: mid bracket
      return MID_ENERGY_SCORES;
    } else {
      // 0 segments
      return NO_ENERGY_SCORES;
    }
  },
};
