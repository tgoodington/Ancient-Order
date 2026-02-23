/**
 * formulas.ts — Combat formula suite for Ancient Order.
 *
 * All functions are pure: no side effects, no randomness, no state mutation.
 * Roll values (0–20 range) are passed as parameters — callers inject rolls.
 *
 * Source of truth: GM Combat Tracker Excel (Math sheet, Paths sheet).
 * Ported via TDD per ADR-015: tests written first against Excel values.
 *
 * Shared utility: calculateBaseDamage() is used by both the per-attack
 * pipeline (Task 15) and GROUP resolution (Task 18) — single source of truth.
 */

import {
  ENERGY_GAINS,
  ASCENSION_THRESHOLDS,
  ASCENSION_ACCUMULATION_BONUS,
} from '../types/combat.js';
import type { AscensionLevel, Buff, DebuffEffect } from '../types/combat.js';

// ============================================================================
// ModifiedStats — the output shape of applyDynamicModifiers
// ============================================================================

/**
 * The full set of stats that can be modified by buffs and debuffs.
 * Maps to dynamic stat columns in Math!A13:AM18.
 *
 * Buff `type` strings map to stat keys via the BUFF_STAT_MAP below.
 * Debuff `stat` strings map directly to keys in this interface.
 */
export interface ModifiedStats {
  power: number;
  speed: number;
  blockSR: number;
  blockSMR: number;
  blockFMR: number;
  dodgeSR: number;
  dodgeFMR: number;
  parrySR: number;
  parryFMR: number;
}

/**
 * Maps buff `type` strings (as applied by elemental paths and special techniques)
 * to the corresponding field in ModifiedStats.
 *
 * Buff types follow the pattern: `${stat}_boost` or `${stat}_debuff`.
 * This map handles the boost variants; debuffs use DebuffEffect.stat directly.
 */
const BUFF_STAT_MAP: Partial<Record<string, keyof ModifiedStats>> = {
  power_boost: 'power',
  speed_boost: 'speed',
  blockSR_boost: 'blockSR',
  blockSMR_boost: 'blockSMR',
  blockFMR_boost: 'blockFMR',
  dodgeSR_boost: 'dodgeSR',
  dodgeFMR_boost: 'dodgeFMR',
  parrySR_boost: 'parrySR',
  parryFMR_boost: 'parryFMR',
};

// ============================================================================
// 1. Rank KO
// ============================================================================

/**
 * Calculates the Rank KO threshold for an attacker vs. a target.
 *
 * Formula: ((attackerRank - targetRank) * 3) / 10
 *
 * Condition for eligibility: attackerRank > targetRank by at least 0.5.
 * The caller is responsible for checking the eligibility condition before
 * calling this function.
 *
 * @param attackerRank - Decimal rank of the attacker (e.g., 2.5 = 5th Degree Iron)
 * @param targetRank   - Decimal rank of the target
 * @returns Threshold value (0.0+); used in checkRankKO
 */
export function calculateRankKOThreshold(attackerRank: number, targetRank: number): number {
  return ((attackerRank - targetRank) * 3) / 10;
}

/**
 * Checks whether a Rank KO succeeds given a threshold and roll.
 *
 * Roll check: (roll / 20) >= (1 - threshold)
 *
 * @param threshold - From calculateRankKOThreshold
 * @param roll      - A value in the 0–20 range (injected by caller)
 * @returns true if the Rank KO lands
 */
export function checkRankKO(threshold: number, roll: number): boolean {
  return roll / 20 >= 1 - threshold;
}

// ============================================================================
// 2. Blindside
// ============================================================================

/**
 * Calculates the Blindside threshold for an attacker vs. a target.
 *
 * Formula: (attackerSpeed - targetSpeed) / targetSpeed
 *
 * Condition for eligibility: attackerSpeed > targetSpeed.
 * The caller is responsible for the eligibility check.
 *
 * @param attackerSpeed - Speed stat of the attacker
 * @param targetSpeed   - Speed stat of the target
 * @returns Threshold value (0.0+); used in checkBlindside
 */
export function calculateBlindsideThreshold(attackerSpeed: number, targetSpeed: number): number {
  return (attackerSpeed - targetSpeed) / targetSpeed;
}

/**
 * Checks whether a Blindside succeeds given a threshold and roll.
 *
 * Roll check: (roll / 20) >= (1 - threshold)
 *
 * @param threshold - From calculateBlindsideThreshold
 * @param roll      - A value in the 0–20 range (injected by caller)
 * @returns true if the Blindside lands (target forced Defenseless)
 */
export function checkBlindside(threshold: number, roll: number): boolean {
  return roll / 20 >= 1 - threshold;
}

// ============================================================================
// 3. Crushing Blow
// ============================================================================

/**
 * Calculates the Crushing Blow threshold.
 *
 * Formula: (actionPower - targetPower) / targetPower
 *
 * Conditions for eligibility: defense was Block AND actionPower > targetPower.
 * The caller is responsible for the eligibility check.
 *
 * @param actionPower  - The attacker's effective power for this action
 * @param targetPower  - The target's power stat
 * @returns Threshold value (0.0+); used in checkCrushingBlow
 */
export function calculateCrushingBlowThreshold(
  actionPower: number,
  targetPower: number,
): number {
  return (actionPower - targetPower) / targetPower;
}

/**
 * Checks whether a Crushing Blow succeeds given a threshold and roll.
 *
 * Roll check: (roll / 20) >= (1 - threshold)
 *
 * @param threshold - From calculateCrushingBlowThreshold
 * @param roll      - A value in the 0–20 range (injected by caller)
 * @returns true if the Crushing Blow lands (applies debuffs to target Block rates)
 */
export function checkCrushingBlow(threshold: number, roll: number): boolean {
  return roll / 20 >= 1 - threshold;
}

// ============================================================================
// 4. Damage formulas — per defense type
// ============================================================================

/**
 * Calculates damage taken when the defender uses Block.
 *
 * Success: damage * (1 - SMR)  — partial mitigation via Success Mitigation Rate
 * Failure: damage * (1 - FMR)  — lesser mitigation via Fail Mitigation Rate
 *
 * @param damage  - Raw incoming damage
 * @param SMR     - Success Mitigation Rate (0.0–1.0)
 * @param FMR     - Fail Mitigation Rate (0.0–1.0)
 * @param success - true if the Block roll succeeded
 * @returns Final damage applied to the defender's stamina
 */
export function calculateBlockDamage(
  damage: number,
  SMR: number,
  FMR: number,
  success: boolean,
): number {
  if (success) {
    return damage * (1 - SMR);
  }
  return damage * (1 - FMR);
}

/**
 * Calculates damage taken when the defender uses Dodge.
 *
 * Success: 0 (full evasion — no damage)
 * Failure: damage * (1 - FMR)
 *
 * @param damage  - Raw incoming damage
 * @param FMR     - Fail Mitigation Rate (0.0–1.0)
 * @param success - true if the Dodge roll succeeded
 * @returns Final damage applied to the defender's stamina
 */
export function calculateDodgeDamage(damage: number, FMR: number, success: boolean): number {
  if (success) {
    return 0;
  }
  return damage * (1 - FMR);
}

/**
 * Calculates damage taken when the defender uses Parry.
 *
 * Success: 0 (counter-attack triggered — caller inserts counter into queue)
 * Failure: damage * (1 - FMR)
 *
 * @param damage  - Raw incoming damage
 * @param FMR     - Fail Mitigation Rate (0.0–1.0)
 * @param success - true if the Parry roll succeeded
 * @returns Final damage applied to the defender's stamina
 */
export function calculateParryDamage(damage: number, FMR: number, success: boolean): number {
  if (success) {
    return 0;
  }
  return damage * (1 - FMR);
}

/**
 * Calculates damage taken when the defender is Defenseless.
 *
 * Full damage is taken (no mitigation). Defenseless is forced by Blindside
 * or when all reactions are unavailable.
 *
 * @param damage - Raw incoming damage
 * @returns The same damage value (100% taken)
 */
export function calculateDefenselessDamage(damage: number): number {
  return damage;
}

// ============================================================================
// 5. Base Damage (shared utility)
// ============================================================================

/**
 * Calculates the base damage for any attack action.
 *
 * This is the shared damage utility used by both the per-attack pipeline
 * (Task 15) and GROUP resolution (Task 18), preventing duplication.
 *
 * Formula (power differential model):
 *   baseDamage = attackerPower * (attackerPower / targetPower) + modifier
 *
 * Rationale:
 *   - When powers are equal: result = attackerPower (1:1 ratio, no change)
 *   - When attacker is stronger: result > attackerPower (amplified by ratio)
 *   - When attacker is weaker: result < attackerPower (reduced by ratio)
 *
 * This matches the Excel "power dominance" model visible in Math!A40:AM54 where
 * the effective damage scales with the ratio of attacker power to defender power.
 *
 * @param attackerPower - The attacker's effective power for this action
 * @param targetPower   - The target's power stat (used as resistance denominator)
 * @param modifier      - Optional flat modifier (default 0); used for buff/debuff adjustments
 * @returns Base damage value before defense mitigation
 */
export function calculateBaseDamage(
  attackerPower: number,
  targetPower: number,
  modifier: number = 0,
): number {
  return attackerPower * (attackerPower / targetPower) + modifier;
}

// ============================================================================
// 6. Special Damage Bonus
// ============================================================================

/**
 * Applies the Special Technique damage bonus.
 *
 * Formula: baseDamage * (1 + 0.10 * energySegments)
 *
 * Each energy segment spent boosts damage by 10%.
 * Range: 1–5 segments (1.10x–1.50x multiplier).
 *
 * @param baseDamage     - Base damage before Special bonus
 * @param energySegments - Number of energy segments spent (typically 1–5)
 * @returns Boosted damage after applying the Special multiplier
 */
export function calculateSpecialDamageBonus(baseDamage: number, energySegments: number): number {
  return baseDamage * (1 + 0.1 * energySegments);
}

// ============================================================================
// 7. Evade Regen
// ============================================================================

/**
 * Calculates the stamina recovered when a combatant uses Evade.
 *
 * Formula: maxStamina * 0.30
 *
 * Base Evade Regen Rate = 0.30 (Math sheet dynamic stats column).
 * Dynamic buffs/debuffs to this rate are applied before calling this function.
 *
 * @param maxStamina - The combatant's maximum stamina
 * @returns Stamina points recovered (added to current stamina, capped at maxStamina)
 */
export function calculateEvadeRegen(maxStamina: number): number {
  return maxStamina * 0.3;
}

// ============================================================================
// 8. Energy Gain
// ============================================================================

/**
 * Calculates energy segments gained for a combat event.
 *
 * Base gains (from GM_Combat_Tracker_Documentation.md energy table):
 *   actionSuccess:   1.0 segments
 *   actionFailure:   0.5 segments
 *   reactionSuccess: 0.5 segments
 *   reactionFailure: 0.25 segments
 *
 * Accumulation bonus per ascension level (ASCENSION_ACCUMULATION_BONUS):
 *   level 0: +0%   → multiplier 1.00
 *   level 1: +25%  → multiplier 1.25
 *   level 2: +25%  → multiplier 1.25
 *   level 3: +50%  → multiplier 1.50
 *
 * Final gain = baseGain * (1 + accumulationBonus)
 *
 * @param eventType      - 'actionSuccess' | 'reactionSuccess' — the event category key
 * @param result         - 'success' | 'failure' — outcome of the event
 * @param ascensionLevel - Current ascension level of the combatant (0–3)
 * @returns Energy segments gained (may be fractional)
 */
export function calculateEnergyGain(
  eventType: 'actionSuccess' | 'reactionSuccess',
  result: 'success' | 'failure',
  ascensionLevel: AscensionLevel,
): number {
  // Resolve the ENERGY_GAINS key: actionSuccess, actionFailure, reactionSuccess, reactionFailure
  const gainKey = result === 'success' ? eventType : _toFailureKey(eventType);
  const baseGain = ENERGY_GAINS[gainKey] ?? 0;
  const bonus = ASCENSION_ACCUMULATION_BONUS[ascensionLevel] ?? 0;
  return baseGain * (1 + bonus);
}

/**
 * Maps a success-keyed event type to its failure-keyed counterpart.
 * e.g., 'actionSuccess' → 'actionFailure'
 */
function _toFailureKey(eventType: 'actionSuccess' | 'reactionSuccess'): string {
  return eventType === 'actionSuccess' ? 'actionFailure' : 'reactionFailure';
}

// ============================================================================
// 9. Ascension Level
// ============================================================================

/**
 * Determines the current ascension level from total accumulated segments.
 *
 * Thresholds (cumulative, from ASCENSION_THRESHOLDS):
 *   < 35  → level 0
 *   >= 35 → level 1
 *   >= 95 → level 2
 *   >= 180 → level 3
 *
 * @param totalSegments - Total energy segments accumulated over all rounds
 * @returns AscensionLevel (0 | 1 | 2 | 3)
 */
export function calculateAscensionLevel(totalSegments: number): AscensionLevel {
  // Iterate thresholds in reverse order (highest first) for early return
  if (totalSegments >= ASCENSION_THRESHOLDS[2]) return 3;
  if (totalSegments >= ASCENSION_THRESHOLDS[1]) return 2;
  if (totalSegments >= ASCENSION_THRESHOLDS[0]) return 1;
  return 0;
}

// ============================================================================
// 10. Dynamic Modifiers
// ============================================================================

/**
 * Applies all active buffs and debuffs to a set of base stats.
 *
 * Returns a new ModifiedStats object — the input baseStats is never mutated.
 * All buffs and debuffs are stacked additively.
 *
 * Buff application:
 *   The buff's `type` field is matched against BUFF_STAT_MAP to identify which
 *   stat to modify. The `modifier` value is added to that stat.
 *   e.g., { type: 'power_boost', modifier: 20 } → stats.power += 20
 *
 * Debuff application:
 *   The debuff's `stat` field directly names the ModifiedStats key.
 *   The `amount` value is subtracted from that stat.
 *   e.g., { stat: 'blockSR', amount: 0.1 } → stats.blockSR -= 0.1
 *
 * Buff types are defined by elemental paths and special techniques (Task 13).
 * The BUFF_STAT_MAP handles the currently known buff type strings.
 *
 * @param baseStats - The combatant's current unmodified stats
 * @param buffs     - Active buff effects
 * @param debuffs   - Active debuff effects
 * @returns New ModifiedStats with all buffs and debuffs applied
 */
export function applyDynamicModifiers(
  baseStats: ModifiedStats,
  buffs: readonly Buff[],
  debuffs: readonly DebuffEffect[],
): ModifiedStats {
  // Start with a spread copy — immutable, new reference
  const stats: ModifiedStats = { ...baseStats };

  // Apply buffs: look up stat key from buff type, add modifier additively
  for (const buff of buffs) {
    const statKey = BUFF_STAT_MAP[buff.type];
    if (statKey !== undefined) {
      stats[statKey] = stats[statKey] + buff.modifier;
    }
  }

  // Apply debuffs: stat field names the key directly, amount is subtracted
  for (const debuff of debuffs) {
    const statKey = debuff.stat as keyof ModifiedStats;
    if (statKey in stats) {
      stats[statKey] = stats[statKey] - debuff.amount;
    }
  }

  return stats;
}
