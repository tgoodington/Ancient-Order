/**
 * OwnStamina factor — Self-preservation scoring.
 *
 * Models the combatant's drive to protect itself based on current stamina level.
 * Low stamina pushes toward EVADE; high stamina enables aggressive plays.
 *
 * Bracket table (from design spec):
 *   selfStaminaPct < 0.3  : { ATTACK: -0.5, DEFEND: 0.1, EVADE: 0.9, SPECIAL: -0.3, GROUP: 0 }
 *   selfStaminaPct 0.3-0.6: { ATTACK:  0.0, DEFEND: 0.0, EVADE: 0.2, SPECIAL:  0.0, GROUP: 0 }
 *   selfStaminaPct > 0.6  : { ATTACK:  0.2, DEFEND: 0.0, EVADE:-0.3, SPECIAL:  0.1, GROUP: 0 }
 *
 * The spec says "scales linearly within each bracket."
 * We interpolate between bracket boundary values as stamina moves across a bracket.
 *
 * Interpolation endpoints:
 *   Low bracket   (0.0 → 0.3): LOW_SCORES → MID_SCORES
 *   Mid bracket   (0.3 → 0.6): MID_SCORES → HIGH_SCORES (using mid/high bracket values)
 *   High bracket  (0.6 → 1.0): HIGH_SCORES remains constant at top end
 */

import type { ActionScores, ScoringFactor, CombatPerception, TargetPerception } from '../../../types/combat.js';

/** Linearly interpolate between two ActionScores by factor t (0.0 – 1.0). */
function lerpScores(a: ActionScores, b: ActionScores, t: number): ActionScores {
  return {
    ATTACK:  a.ATTACK  + (b.ATTACK  - a.ATTACK)  * t,
    DEFEND:  a.DEFEND  + (b.DEFEND  - a.DEFEND)  * t,
    EVADE:   a.EVADE   + (b.EVADE   - a.EVADE)   * t,
    SPECIAL: a.SPECIAL + (b.SPECIAL - a.SPECIAL) * t,
    GROUP:   a.GROUP   + (b.GROUP   - a.GROUP)   * t,
  };
}

// Bracket anchor scores (bracket center/representative values from design spec)
const LOW_SCORES: ActionScores  = { ATTACK: -0.5, DEFEND: 0.1, EVADE: 0.9, SPECIAL: -0.3, GROUP: 0 };
const MID_SCORES: ActionScores  = { ATTACK:  0.0, DEFEND: 0.0, EVADE: 0.2, SPECIAL:  0.0, GROUP: 0 };
const HIGH_SCORES: ActionScores = { ATTACK:  0.2, DEFEND: 0.0, EVADE:-0.3, SPECIAL:  0.1, GROUP: 0 };

export const ownStaminaFactor: ScoringFactor = {
  name: 'ownStamina',

  evaluate(self: CombatPerception, _target: TargetPerception | null): ActionScores {
    const pct = self.selfStaminaPct;

    if (pct < 0.3) {
      // Low bracket: interpolate from LOW (at 0.0) toward MID (at 0.3)
      const t = pct / 0.3;
      return lerpScores(LOW_SCORES, MID_SCORES, t);
    } else if (pct <= 0.6) {
      // Mid bracket: interpolate from MID (at 0.3) toward HIGH (at 0.6)
      const t = (pct - 0.3) / 0.3;
      return lerpScores(MID_SCORES, HIGH_SCORES, t);
    } else {
      // High bracket: return HIGH_SCORES (spec gives a single value for > 0.6)
      return HIGH_SCORES;
    }
  },
};
