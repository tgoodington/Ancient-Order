/**
 * energy.ts — Energy segment tracking and ascension level management.
 *
 * Energy is accumulated through combat actions and reactions.
 * Ascension levels unlock starting segments and accumulation bonuses.
 *
 * All functions are pure: return new Combatant objects, never mutate inputs.
 *
 * Source: GM_Combat_Tracker_Documentation.md — Ascension Levels and Energy tables.
 */

import {
  ASCENSION_STARTING_SEGMENTS,
} from '../types/combat.js';
import type { Combatant, AscensionLevel } from '../types/combat.js';
import { calculateEnergyGain, calculateAscensionLevel } from './formulas.js';

// ============================================================================
// Energy accumulation
// ============================================================================

/**
 * Adds energy segments to a combatant after a combat event.
 *
 * Calculates the gain using the energy table and ascension accumulation bonus,
 * then returns a new Combatant with energy incremented.
 *
 * The gain formula (from formulas.calculateEnergyGain):
 *   baseGain * (1 + ASCENSION_ACCUMULATION_BONUS[ascensionLevel])
 *
 * @param combatant - The combatant gaining energy
 * @param eventType - 'actionSuccess' | 'reactionSuccess' (base event category)
 * @param result    - 'success' | 'failure' (outcome of the event)
 * @returns New Combatant with energy incremented by the calculated gain
 */
export function addEnergySegments(
  combatant: Combatant,
  eventType: 'actionSuccess' | 'reactionSuccess',
  result: 'success' | 'failure',
): Combatant {
  const gain = calculateEnergyGain(eventType, result, combatant.ascensionLevel);
  return {
    ...combatant,
    energy: combatant.energy + gain,
  };
}

// ============================================================================
// Ascension advancement
// ============================================================================

/**
 * Checks whether a combatant has accumulated enough total energy to advance
 * their ascension level, and advances if so.
 *
 * Thresholds (cumulative total energy, from ASCENSION_THRESHOLDS):
 *   < 35  → level 0
 *   >= 35 → level 1
 *   >= 95 → level 2
 *   >= 180 → level 3
 *
 * The combatant's current `energy` field is treated as the total accumulated
 * segments for ascension threshold comparison purposes.
 *
 * If the ascension level changes, returns a new Combatant with the updated
 * ascensionLevel. Otherwise returns the same combatant reference.
 *
 * @param combatant - The combatant to check for ascension advancement
 * @returns New Combatant with updated ascensionLevel (or same if unchanged)
 */
export function checkAscensionAdvance(combatant: Combatant): Combatant {
  const newLevel = calculateAscensionLevel(combatant.energy);
  if (newLevel === combatant.ascensionLevel) {
    return combatant;
  }
  return {
    ...combatant,
    ascensionLevel: newLevel,
  };
}

// ============================================================================
// Starting segments lookup
// ============================================================================

/**
 * Returns the number of energy segments a combatant starts with at the
 * beginning of each round, based on their ascension level.
 *
 * From ASCENSION_STARTING_SEGMENTS:
 *   Level 0 → 0 segments
 *   Level 1 → 0 segments
 *   Level 2 → 1 segment
 *   Level 3 → 2 segments
 *
 * @param ascensionLevel - The combatant's current ascension level (0–3)
 * @returns Starting energy segments for the round
 */
export function getStartingSegments(ascensionLevel: AscensionLevel): number {
  return ASCENSION_STARTING_SEGMENTS[ascensionLevel] ?? 0;
}

// ============================================================================
// Round reset
// ============================================================================

/**
 * Resets a combatant's per-round energy to the starting segments for their
 * current ascension level.
 *
 * Called at the start of each round (after ascension check).
 * Does not reset total accumulated energy — only the current round energy pool.
 *
 * Note: In this system, `energy` serves as the current round's segment count.
 * Total accumulated segments for ascension checks are tracked separately by
 * the pipeline (or callers manage the distinction). This function sets energy
 * to the round-start value.
 *
 * @param combatant - The combatant being reset
 * @returns New Combatant with energy set to starting segments for their ascension level
 */
export function resetRoundEnergy(combatant: Combatant): Combatant {
  const starting = getStartingSegments(combatant.ascensionLevel);
  return {
    ...combatant,
    energy: starting,
  };
}
