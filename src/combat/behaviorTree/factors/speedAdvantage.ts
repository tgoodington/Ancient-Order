/**
 * SpeedAdvantage factor — Blindside exploitation scoring (target-aware).
 *
 * Models the combatant's awareness of speed differentials relative to the target.
 * A significant speed advantage creates Blindside opportunities, making ATTACK
 * and SPECIAL far more valuable against that specific target.
 *
 * speedDelta = (self.speed - target.speed) / target.speed
 * Positive delta = combatant is faster than the target.
 *
 * Bracket table (from design spec):
 *   speedDelta > 0.3   (significantly faster): { ATTACK: 0.6, DEFEND: -0.1, EVADE: -0.2, SPECIAL: 0.3, GROUP: 0 }
 *   speedDelta 0.0-0.3 (slight or no advantage): { ATTACK: 0.2, DEFEND:  0.0, EVADE:  0.0, SPECIAL: 0.1, GROUP: 0 }
 *   speedDelta < 0     (slower):                 { ATTACK:-0.1, DEFEND:  0.1, EVADE:  0.1, SPECIAL: 0.0, GROUP: 0 }
 *
 * When target is null (EVADE), returns neutral scores.
 *
 * DEFEND targeting note: When the perception builder constructs a TargetPerception
 * for a DEFEND candidate (an ally), it sets speedDelta=0 because speed relative to
 * an ally is semantically irrelevant for a defensive action. As a result, DEFEND
 * candidates always fall into the neutral (0.0-0.3) bracket and receive NEUTRAL_SCORES.
 * This is intentional — speed advantage is an offensive concept and should not
 * influence which ally to defend.
 */

import type { ActionScores, ScoringFactor, CombatPerception, TargetPerception } from '../../../types/combat.js';

const FAST_SCORES: ActionScores    = { ATTACK: 0.6, DEFEND: -0.1, EVADE: -0.2, SPECIAL: 0.3, GROUP: 0 };
const NEUTRAL_SCORES: ActionScores = { ATTACK: 0.2, DEFEND:  0.0, EVADE:  0.0, SPECIAL: 0.1, GROUP: 0 };
const SLOW_SCORES: ActionScores    = { ATTACK:-0.1, DEFEND:  0.1, EVADE:  0.1, SPECIAL: 0.0, GROUP: 0 };

function lerpScores(a: ActionScores, b: ActionScores, t: number): ActionScores {
  return {
    ATTACK:  a.ATTACK  + (b.ATTACK  - a.ATTACK)  * t,
    DEFEND:  a.DEFEND  + (b.DEFEND  - a.DEFEND)  * t,
    EVADE:   a.EVADE   + (b.EVADE   - a.EVADE)   * t,
    SPECIAL: a.SPECIAL + (b.SPECIAL - a.SPECIAL) * t,
    GROUP:   a.GROUP   + (b.GROUP   - a.GROUP)   * t,
  };
}

export const speedAdvantageFactor: ScoringFactor = {
  name: 'speedAdvantage',

  evaluate(_self: CombatPerception, target: TargetPerception | null): ActionScores {
    // No target (EVADE / self-action): return neutral
    if (target === null) {
      return { ATTACK: 0, DEFEND: 0, EVADE: 0, SPECIAL: 0, GROUP: 0 };
    }

    const delta = target.speedDelta;

    if (delta > 0.3) {
      // Significantly faster: full Blindside exploitation
      return FAST_SCORES;
    } else if (delta >= 0) {
      // Slight advantage or even speed: interpolate from neutral to fast
      const t = delta / 0.3;
      return lerpScores(NEUTRAL_SCORES, FAST_SCORES, t);
    } else {
      // Slower than target: defensive inclination
      return SLOW_SCORES;
    }
  },
};
