/**
 * AllyInDanger factor — Team protection scoring.
 *
 * Evaluates the lowest ally stamina to determine urgency of defensive support.
 * High urgency (ally in critical danger) strongly pushes toward DEFEND and SPECIAL.
 *
 * Bracket table (from design spec):
 *   lowestAllyStaminaPct < 0.3  : { ATTACK: -0.2, DEFEND: 0.8, EVADE: -0.1, SPECIAL: 0.4, GROUP: 0.2 }
 *   lowestAllyStaminaPct 0.3-0.6: { ATTACK:  0.0, DEFEND: 0.3, EVADE:  0.0, SPECIAL: 0.1, GROUP: 0.0 }
 *   lowestAllyStaminaPct > 0.6  : { ATTACK:  0.0, DEFEND: 0.0, EVADE:  0.0, SPECIAL: 0.0, GROUP: 0.0 }
 *
 * Note: This factor is NOT target-aware. It uses lowestAllyStaminaPct from
 * self perception regardless of which ally/enemy is the current target.
 */

import type { ActionScores, ScoringFactor, CombatPerception, TargetPerception } from '../../../types/combat.js';

const CRITICAL_SCORES: ActionScores = { ATTACK: -0.2, DEFEND: 0.8, EVADE: -0.1, SPECIAL: 0.4, GROUP: 0.2 };
const MID_SCORES: ActionScores      = { ATTACK:  0.0, DEFEND: 0.3, EVADE:  0.0, SPECIAL: 0.1, GROUP: 0.0 };
const HEALTHY_SCORES: ActionScores  = { ATTACK:  0.0, DEFEND: 0.0, EVADE:  0.0, SPECIAL: 0.0, GROUP: 0.0 };

export const allyInDangerFactor: ScoringFactor = {
  name: 'allyInDanger',

  evaluate(self: CombatPerception, _target: TargetPerception | null): ActionScores {
    // If no allies exist, no ally danger — return zeros
    if (self.allyCount === 0) {
      return HEALTHY_SCORES;
    }

    const pct = self.lowestAllyStaminaPct;

    if (pct < 0.3) {
      return CRITICAL_SCORES;
    } else if (pct <= 0.6) {
      return MID_SCORES;
    } else {
      return HEALTHY_SCORES;
    }
  },
};
