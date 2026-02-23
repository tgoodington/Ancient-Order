/**
 * Lars — Scheming Merchant / Tank-Defender archetype profile.
 *
 * Path: Earth (Action path — DEFEND-first tie-breaks among action paths)
 *
 * Character: Efficient and self-preserving. Manages energy carefully.
 * Punishes weak targets when safe. Adapts strategy to round progression.
 *
 * High ownStamina weight (1.5) makes him strongly self-preserving.
 * High energyAvailability weight (1.4) means he carefully times SPECIAL use.
 * High roundPhase weight (1.2) means his strategy shifts meaningfully over time.
 * Balanced ATTACK/DEFEND base scores reflect the tank-but-opportunistic role.
 */

import type { ArchetypeProfile } from '../../../types/combat.js';

export const larsProfile: ArchetypeProfile = {
  name: 'lars_scheming_merchant',
  baseScores: {
    ATTACK:  0.4,
    DEFEND:  0.5,
    EVADE:   0.4,
    SPECIAL: 0.3,
    GROUP:   0.2,
  },
  factorWeights: {
    ownStamina:          1.5,
    allyInDanger:        1.0,
    targetVulnerability: 0.8,
    energyAvailability:  1.4,
    speedAdvantage:      0.6,
    roundPhase:          1.2,
    teamBalance:         1.0,
  },
  elementalPath: 'Earth',
};
