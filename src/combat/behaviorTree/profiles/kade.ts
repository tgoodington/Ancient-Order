/**
 * Kade — Rogue Outlaw / Striker-Aggressive archetype profile.
 *
 * Path: Fire (Action path — ATTACK-first tie-breaks)
 *
 * Character: Aggressive opportunist. Exploits weak and slow targets.
 * High risk tolerance. Ignores team needs. Presses advantages relentlessly.
 *
 * High targetVulnerability weight (1.6) and speedAdvantage weight (1.5) make
 * him laser-focused on exploiting every positional and health-based opening.
 * Low ownStamina weight (0.6) means he won't pull back even when hurt.
 * Low allyInDanger weight (0.4) confirms his selfish, individualistic style.
 * High ATTACK base score (0.6) with low DEFEND (0.2) reflects his raw aggression.
 */

import type { ArchetypeProfile } from '../../../types/combat.js';

export const kadeProfile: ArchetypeProfile = {
  name: 'kade_rogue_outlaw',
  baseScores: {
    ATTACK:  0.6,
    DEFEND:  0.2,
    EVADE:   0.3,
    SPECIAL: 0.4,
    GROUP:   0.1,
  },
  factorWeights: {
    ownStamina:          0.6,
    allyInDanger:        0.4,
    targetVulnerability: 1.6,
    energyAvailability:  1.0,
    speedAdvantage:      1.5,
    roundPhase:          0.5,
    teamBalance:         0.6,
  },
  elementalPath: 'Fire',
};
