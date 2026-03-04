/**
 * Ancient Order - Team Synergy Calculator
 *
 * Pure, deterministic synergy calculation based on paradigm configurations.
 *
 * Paradigms:
 *   well_rounded — party collectively covers all 6 traits above threshold
 *   bond         — player's top traits align with an NPC's dominant traits
 *
 * Functions:
 *   evaluateWellRounded — evaluate the Well Rounded paradigm
 *   evaluateBond        — evaluate the Bond paradigm
 *   calculateSynergy    — evaluate all paradigms, return highest-quality result
 */

import type { Personality, PersonalityTrait } from '../types/index.js';
import type { SynergyBonus, SynergyResult, ParadigmConfig } from '../types/narrative.js';

// ============================================================================
// Constants
// ============================================================================

const PERSONALITY_TRAITS: PersonalityTrait[] = [
  'patience', 'empathy', 'cunning', 'logic', 'kindness', 'charisma',
];

// ============================================================================
// Well Rounded Paradigm
// ============================================================================

/**
 * Evaluates the Well Rounded synergy paradigm.
 *
 * Algorithm:
 *   1. Collect all personalities: [player, ...partyNpcs].
 *   2. For each of 6 traits, find the max value across all personalities.
 *   3. If any max < config.threshold (integer, e.g., 25): return null.
 *   4. matchQuality = sum(maxValues) / (6 * config.threshold).
 *   5. Return SynergyBonus.
 *
 * @param playerPersonality     - Player's current personality
 * @param partyNpcPersonalities - Personalities of all NPCs in the party
 * @param config                - Paradigm configuration
 * @returns SynergyBonus if paradigm is satisfied, null otherwise
 */
export function evaluateWellRounded(
  playerPersonality: Readonly<Personality>,
  partyNpcPersonalities: ReadonlyArray<Readonly<Personality>>,
  config: ParadigmConfig
): SynergyBonus | null {
  const allPersonalities = [playerPersonality, ...partyNpcPersonalities];

  const maxValues: number[] = [];

  for (const trait of PERSONALITY_TRAITS) {
    const maxForTrait = Math.max(...allPersonalities.map(p => p[trait]));
    if (maxForTrait < config.threshold) {
      return null; // Threshold not met for this trait
    }
    maxValues.push(maxForTrait);
  }

  const matchQuality = maxValues.reduce((sum, v) => sum + v, 0) / (6 * config.threshold);

  return {
    paradigmName: config.name,
    stat: config.stat,
    multiplier: config.multiplier,
    matchQuality,
  };
}

// ============================================================================
// Bond Paradigm
// ============================================================================

/**
 * Evaluates the Bond synergy paradigm.
 *
 * Algorithm (per NPC):
 *   1. Sort the 6 traits by value descending. Take top 2 (dominant traits).
 *   2. npcDominantSum = npc[trait1] + npc[trait2].
 *   3. Division-by-zero guard: if npcDominantSum === 0, skip this NPC.
 *   4. playerAlignmentSum = player[trait1] + player[trait2] (same trait keys).
 *   5. alignmentRatio = playerAlignmentSum / npcDominantSum.
 *
 * bestRatio = max(all alignmentRatios).
 * If bestRatio < config.threshold / 100 (e.g., < 0.80): return null.
 *
 * @param playerPersonality     - Player's current personality
 * @param partyNpcPersonalities - Personalities of all NPCs in the party
 * @param config                - Paradigm configuration
 * @returns SynergyBonus if paradigm is satisfied, null otherwise
 */
export function evaluateBond(
  playerPersonality: Readonly<Personality>,
  partyNpcPersonalities: ReadonlyArray<Readonly<Personality>>,
  config: ParadigmConfig
): SynergyBonus | null {
  if (partyNpcPersonalities.length === 0) return null;

  const alignmentRatios: number[] = [];

  for (const npc of partyNpcPersonalities) {
    // Sort traits by NPC value descending to find dominant top-2
    const sortedTraits = PERSONALITY_TRAITS.slice().sort((a, b) => npc[b] - npc[a]);
    const [trait1, trait2] = sortedTraits as [PersonalityTrait, PersonalityTrait];

    const npcDominantSum = npc[trait1] + npc[trait2];

    // Division-by-zero guard
    if (npcDominantSum === 0) continue;

    const playerAlignmentSum = playerPersonality[trait1] + playerPersonality[trait2];
    const alignmentRatio = playerAlignmentSum / npcDominantSum;
    alignmentRatios.push(alignmentRatio);
  }

  if (alignmentRatios.length === 0) return null;

  const bestRatio = Math.max(...alignmentRatios);
  const threshold = config.threshold / 100; // convert integer (e.g., 80) to decimal (0.80)

  if (bestRatio < threshold) return null;

  return {
    paradigmName: config.name,
    stat: config.stat,
    multiplier: config.multiplier,
    matchQuality: bestRatio,
  };
}

// ============================================================================
// Main Synergy Calculator
// ============================================================================

/**
 * Calculates the best active synergy bonus from all configured paradigms.
 *
 * Algorithm:
 *   1. Evaluate each paradigm in the list.
 *   2. Collect non-null results.
 *   3. If no results: return null.
 *   4. Sort by matchQuality descending.
 *   5. Tiebreak (equal matchQuality): well_rounded wins over bond.
 *   6. Return results[0].
 *
 * @param playerPersonality     - Player's current personality
 * @param partyNpcPersonalities - Personalities of all NPCs in the party
 * @param paradigms             - List of paradigm configurations to evaluate
 * @returns The best SynergyBonus or null if no paradigm is satisfied
 */
export function calculateSynergy(
  playerPersonality: Readonly<Personality>,
  partyNpcPersonalities: ReadonlyArray<Readonly<Personality>>,
  paradigms: ReadonlyArray<ParadigmConfig>
): SynergyResult {
  const results: SynergyBonus[] = [];

  for (const paradigm of paradigms) {
    let result: SynergyBonus | null = null;

    if (paradigm.type === 'well_rounded') {
      result = evaluateWellRounded(playerPersonality, partyNpcPersonalities, paradigm);
    } else if (paradigm.type === 'bond') {
      result = evaluateBond(playerPersonality, partyNpcPersonalities, paradigm);
    }

    if (result !== null) {
      results.push(result);
    }
  }

  if (results.length === 0) return null;

  // Tiebreak priority: well_rounded (0) wins over bond (1)
  const typePriority: Record<string, number> = { well_rounded: 0, bond: 1 };

  // Sort by matchQuality descending, then by paradigm type priority ascending
  results.sort((a, b) => {
    if (b.matchQuality !== a.matchQuality) {
      return b.matchQuality - a.matchQuality;
    }
    const aPriority = typePriority[
      paradigms.find(p => p.name === a.paradigmName)?.type ?? 'bond'
    ] ?? 1;
    const bPriority = typePriority[
      paradigms.find(p => p.name === b.paradigmName)?.type ?? 'bond'
    ] ?? 1;
    return aPriority - bPriority;
  });

  return results[0] ?? null;
}
