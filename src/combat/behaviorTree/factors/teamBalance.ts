/**
 * TeamBalance factor — Team stamina comparison scoring.
 *
 * Compares average team stamina against average enemy stamina to determine
 * whether the combatant's side is winning or losing the attrition war.
 * Winning teams press offense; losing teams shift to damage mitigation.
 *
 * Bracket table (from design spec):
 *   teamAvg > enemyAvg + 0.2 (winning):     { ATTACK: 0.3, DEFEND: -0.1, EVADE: -0.2, SPECIAL: 0.2, GROUP: 0 }
 *   roughly even (±0.2):                    { ATTACK: 0.0, DEFEND:  0.0, EVADE:  0.0, SPECIAL: 0.0, GROUP: 0 }
 *   teamAvg < enemyAvg - 0.2 (losing):      { ATTACK:-0.2, DEFEND:  0.4, EVADE:  0.3, SPECIAL: 0.1, GROUP: 0.2 }
 *
 * Not target-aware.
 */

import type { ActionScores, ScoringFactor, CombatPerception, TargetPerception } from '../../../types/combat.js';

const WINNING_SCORES: ActionScores = { ATTACK: 0.3, DEFEND: -0.1, EVADE: -0.2, SPECIAL: 0.2, GROUP: 0 };
const EVEN_SCORES: ActionScores    = { ATTACK: 0.0, DEFEND:  0.0, EVADE:  0.0, SPECIAL: 0.0, GROUP: 0 };
const LOSING_SCORES: ActionScores  = { ATTACK:-0.2, DEFEND:  0.4, EVADE:  0.3, SPECIAL: 0.1, GROUP: 0.2 };

export const teamBalanceFactor: ScoringFactor = {
  name: 'teamBalance',

  evaluate(self: CombatPerception, _target: TargetPerception | null): ActionScores {
    const diff = self.teamAvgStaminaPct - self.enemyAvgStaminaPct;

    if (diff > 0.2) {
      return WINNING_SCORES;
    } else if (diff < -0.2) {
      return LOSING_SCORES;
    } else {
      return EVEN_SCORES;
    }
  },
};
