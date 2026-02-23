/**
 * RoundPhase factor — Temporal strategy scoring.
 *
 * Models how combat strategy should shift as a fight progresses.
 * Early rounds: build energy, assess opponents → favor EVADE/DEFEND.
 * Mid rounds: balanced engagement.
 * Late rounds: press any advantage, spend accumulated energy → favor ATTACK/SPECIAL.
 *
 * Bracket table (from design spec):
 *   Rounds 1-2 (early): { ATTACK: 0.1, DEFEND: 0.2, EVADE: 0.3, SPECIAL: -0.2, GROUP: 0 }
 *   Rounds 3-5 (mid):   { ATTACK: 0.2, DEFEND: 0.0, EVADE: 0.0, SPECIAL:  0.2, GROUP: 0 }
 *   Rounds 6+  (late):  { ATTACK: 0.3, DEFEND:-0.1, EVADE:-0.1, SPECIAL:  0.4, GROUP: 0 }
 *
 * Not target-aware — uses round number from self perception context.
 */

import type { ActionScores, ScoringFactor, CombatPerception, TargetPerception } from '../../../types/combat.js';

const EARLY_SCORES: ActionScores = { ATTACK: 0.1, DEFEND: 0.2, EVADE: 0.3, SPECIAL: -0.2, GROUP: 0 };
const MID_SCORES: ActionScores   = { ATTACK: 0.2, DEFEND: 0.0, EVADE: 0.0, SPECIAL:  0.2, GROUP: 0 };
const LATE_SCORES: ActionScores  = { ATTACK: 0.3, DEFEND:-0.1, EVADE:-0.1, SPECIAL:  0.4, GROUP: 0 };

export const roundPhaseFactor: ScoringFactor = {
  name: 'roundPhase',

  evaluate(self: CombatPerception, _target: TargetPerception | null): ActionScores {
    const round = self.round;

    if (round <= 2) {
      return EARLY_SCORES;
    } else if (round <= 5) {
      return MID_SCORES;
    } else {
      return LATE_SCORES;
    }
  },
};
