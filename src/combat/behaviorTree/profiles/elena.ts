/**
 * Elena — Loyal Scout / Healer-Support archetype profile.
 *
 * Path: Light (Reaction path — defensive-leaning tie-breaks)
 *
 * Character: Protective instincts. Strongly responds to ally danger.
 * Uses SPECIAL for support. Avoids aggressive play unless team is winning.
 *
 * Base scores lean toward DEFEND and SPECIAL.
 * High allyInDanger weight (1.8) and teamBalance weight (1.4) make her
 * acutely aware of the team's health status. Low targetVulnerability weight
 * (0.5) means she won't press weakened enemies as hard as Kade.
 */

import type { ArchetypeProfile } from '../../../types/combat.js';

export const elenaProfile: ArchetypeProfile = {
  name: 'elena_loyal_scout',
  baseScores: {
    ATTACK:  0.3,
    DEFEND:  0.5,
    EVADE:   0.3,
    SPECIAL: 0.4,
    GROUP:   0.2,
  },
  factorWeights: {
    ownStamina:          1.0,
    allyInDanger:        1.8,
    targetVulnerability: 0.5,
    energyAvailability:  1.2,
    speedAdvantage:      0.3,
    roundPhase:          0.8,
    teamBalance:         1.4,
  },
  elementalPath: 'Light',
};
