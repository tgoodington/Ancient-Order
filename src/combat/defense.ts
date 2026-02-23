/**
 * Ancient Order - Defense Resolution System
 *
 * Resolves defense outcomes for Block, Dodge, Parry, and Defenseless.
 * All functions are pure: given the same inputs, they always produce the same output.
 * Roll values are injected by the caller for full testability.
 *
 * Roll check convention: roll <= SR * 20 → success
 *   SR is a 0.0–1.0 probability. Multiplied by 20 converts it to the 0–20 roll range.
 *   e.g., SR = 0.6 → success threshold = 12 → any roll <= 12 succeeds.
 *
 * Damage formula functions are imported from formulas.ts (single source of truth).
 */

import {
  calculateBlockDamage,
  calculateDodgeDamage,
  calculateParryDamage,
  calculateDefenselessDamage,
} from './formulas.js';
import type { DefenseType, DefenseResult, ReactionSkills } from '../types/combat.js';

// ============================================================================
// Individual Defense Handlers
// ============================================================================

/**
 * Resolves a Block defense attempt.
 *
 * Success threshold: roll <= SR * 20
 * Success damage:    damage * (1 - SMR)  (partial mitigation via Success Mitigation Rate)
 * Failure damage:    damage * (1 - FMR)  (lesser mitigation via Fail Mitigation Rate)
 *
 * crushingBlowEligible is always true for Block — the pipeline (Task 15) is
 * responsible for the Crushing Blow eligibility pre-condition check
 * (actionPower > targetPower).
 *
 * @param damage - Raw incoming damage before mitigation
 * @param SR     - Success Rate (0.0–1.0): probability of a successful block
 * @param SMR    - Success Mitigation Rate (0.0–1.0): damage reduction on success
 * @param FMR    - Fail Mitigation Rate (0.0–1.0): damage reduction on failure
 * @param roll   - Injected roll value in the 0–20 range
 * @returns Block outcome: success flag, final damage, Crushing Blow eligibility flag
 */
export function resolveBlock(
  damage: number,
  SR: number,
  SMR: number,
  FMR: number,
  roll: number,
): { success: boolean; damage: number; crushingBlowEligible: boolean } {
  const success = roll <= SR * 20;
  const finalDamage = calculateBlockDamage(damage, SMR, FMR, success);
  return {
    success,
    damage: finalDamage,
    crushingBlowEligible: true, // Block is always eligible; power check is caller's responsibility
  };
}

/**
 * Resolves a Dodge defense attempt.
 *
 * Success threshold: roll <= SR * 20
 * Success damage:    0 (full evasion)
 * Failure damage:    damage * (1 - FMR)
 *
 * @param damage - Raw incoming damage before mitigation
 * @param SR     - Success Rate (0.0–1.0): probability of a successful dodge
 * @param FMR    - Fail Mitigation Rate (0.0–1.0): damage reduction on failure
 * @param roll   - Injected roll value in the 0–20 range
 * @returns Dodge outcome: success flag and final damage
 */
export function resolveDodge(
  damage: number,
  SR: number,
  FMR: number,
  roll: number,
): { success: boolean; damage: number } {
  const success = roll <= SR * 20;
  const finalDamage = calculateDodgeDamage(damage, FMR, success);
  return { success, damage: finalDamage };
}

/**
 * Resolves a Parry defense attempt.
 *
 * Success threshold: roll <= SR * 20
 * Success damage:    0 (counter attack triggered — caller inserts counter into queue)
 * Failure damage:    damage * (1 - FMR)
 *
 * counterTriggered is true only on success — the pipeline/counter chain is
 * responsible for actually constructing and queuing the counter CombatAction.
 *
 * @param damage - Raw incoming damage before mitigation
 * @param SR     - Success Rate (0.0–1.0): probability of a successful parry
 * @param FMR    - Fail Mitigation Rate (0.0–1.0): damage reduction on failure
 * @param roll   - Injected roll value in the 0–20 range
 * @returns Parry outcome: success flag, final damage, counter trigger flag
 */
export function resolveParry(
  damage: number,
  SR: number,
  FMR: number,
  roll: number,
): { success: boolean; damage: number; counterTriggered: boolean } {
  const success = roll <= SR * 20;
  const finalDamage = calculateParryDamage(damage, FMR, success);
  return { success, damage: finalDamage, counterTriggered: success };
}

/**
 * Resolves a Defenseless outcome (no active defense).
 *
 * Defenseless is forced by Blindside or when all reactions are unavailable.
 * Full damage is always taken — no mitigation, no success chance.
 *
 * @param damage - Raw incoming damage
 * @returns Defenseless outcome: success is always false, damage is always the full amount
 */
export function resolveDefenseless(damage: number): { success: false; damage: number } {
  return { success: false, damage: calculateDefenselessDamage(damage) };
}

// ============================================================================
// Defense Dispatcher
// ============================================================================

/**
 * Dispatches to the appropriate defense handler based on defenseType and
 * returns a normalized DefenseResult.
 *
 * The DefenseResult interface (from types/combat.ts) uses a damageMultiplier
 * field for the pipeline. This function converts handler outputs to that shape:
 *   damageMultiplier = finalDamage / rawDamage  (or 1.0 if rawDamage is 0)
 *
 * @param defenseType    - The type of defense being resolved
 * @param damage         - Raw incoming damage before mitigation
 * @param reactionSkills - Defender's reaction skill rates (SR, SMR, FMR)
 * @param roll           - Injected roll value in the 0–20 range
 * @returns DefenseResult with type, success flag, and damageMultiplier
 */
export function resolveDefense(
  defenseType: DefenseType,
  damage: number,
  reactionSkills: ReactionSkills,
  roll: number,
): DefenseResult {
  switch (defenseType) {
    case 'block': {
      const result = resolveBlock(
        damage,
        reactionSkills.block.SR,
        reactionSkills.block.SMR,
        reactionSkills.block.FMR,
        roll,
      );
      const damageMultiplier = damage > 0 ? result.damage / damage : result.success ? 1 - reactionSkills.block.SMR : 1 - reactionSkills.block.FMR;
      return {
        type: 'block',
        success: result.success,
        damageMultiplier,
      };
    }

    case 'dodge': {
      const result = resolveDodge(
        damage,
        reactionSkills.dodge.SR,
        reactionSkills.dodge.FMR,
        roll,
      );
      const damageMultiplier = damage > 0 ? result.damage / damage : result.success ? 0 : 1 - reactionSkills.dodge.FMR;
      return {
        type: 'dodge',
        success: result.success,
        damageMultiplier,
      };
    }

    case 'parry': {
      const result = resolveParry(
        damage,
        reactionSkills.parry.SR,
        reactionSkills.parry.FMR,
        roll,
      );
      const damageMultiplier = damage > 0 ? result.damage / damage : result.success ? 0 : 1 - reactionSkills.parry.FMR;
      return {
        type: 'parry',
        success: result.success,
        damageMultiplier,
      };
    }

    case 'defenseless': {
      // resolveDefenseless always returns full damage (no mitigation).
      // damageMultiplier is 1.0 by definition.
      return {
        type: 'defenseless',
        success: false,
        damageMultiplier: 1.0,
      };
    }

    default: {
      // TypeScript exhaustiveness guard
      const _exhaustive: never = defenseType;
      throw new Error(`Unhandled defense type: ${String(_exhaustive)}`);
    }
  }
}
