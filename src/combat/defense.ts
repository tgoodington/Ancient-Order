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
  applyDynamicModifiers,
  type ModifiedStats,
} from './formulas.js';
import type { DefenseType, DefenseResult, ReactionSkills, Combatant } from '../types/combat.js';

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
 * Success damage:    damage * (1 - SMR)  (partial mitigation via Success Mitigation Rate)
 * Failure damage:    damage * (1 - FMR)  (lesser mitigation via Fail Mitigation Rate)
 *
 * @param damage - Raw incoming damage before mitigation
 * @param SR     - Success Rate (0.0–1.0): probability of a successful dodge
 * @param SMR    - Success Mitigation Rate (0.0–1.0): damage reduction on success
 * @param FMR    - Fail Mitigation Rate (0.0–1.0): damage reduction on failure
 * @param roll   - Injected roll value in the 0–20 range
 * @returns Dodge outcome: success flag and final damage
 */
export function resolveDodge(
  damage: number,
  SR: number,
  SMR: number,
  FMR: number,
  roll: number,
): { success: boolean; damage: number } {
  const success = roll <= SR * 20;
  const finalDamage = calculateDodgeDamage(damage, SMR, FMR, success);
  return { success, damage: finalDamage };
}

/**
 * Resolves a Parry defense attempt.
 *
 * Success threshold: roll <= SR * 20
 * Success damage:    damage * (1 - SMR)  (partial mitigation; counter also triggered)
 * Failure damage:    damage * (1 - FMR)
 *
 * counterTriggered is true only on success — the pipeline/counter chain is
 * responsible for actually constructing and queuing the counter CombatAction.
 * Mitigation and the counter are independent: a successful parry both reduces
 * incoming damage by SMR and spawns a counter.
 *
 * @param damage - Raw incoming damage before mitigation
 * @param SR     - Success Rate (0.0–1.0): probability of a successful parry
 * @param SMR    - Success Mitigation Rate (0.0–1.0): damage reduction on success
 * @param FMR    - Fail Mitigation Rate (0.0–1.0): damage reduction on failure
 * @param roll   - Injected roll value in the 0–20 range
 * @returns Parry outcome: success flag, final damage, counter trigger flag
 */
export function resolveParry(
  damage: number,
  SR: number,
  SMR: number,
  FMR: number,
  roll: number,
): { success: boolean; damage: number; counterTriggered: boolean } {
  const success = roll <= SR * 20;
  const finalDamage = calculateParryDamage(damage, SMR, FMR, success);
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
        reactionSkills.dodge.SMR,
        reactionSkills.dodge.FMR,
        roll,
      );
      const damageMultiplier = damage > 0 ? result.damage / damage : result.success ? 1 - reactionSkills.dodge.SMR : 1 - reactionSkills.dodge.FMR;
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
        reactionSkills.parry.SMR,
        reactionSkills.parry.FMR,
        roll,
      );
      const damageMultiplier = damage > 0 ? result.damage / damage : result.success ? 1 - reactionSkills.parry.SMR : 1 - reactionSkills.parry.FMR;
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

// ============================================================================
// Effective Reaction Skills (buff/debuff folding)
// ============================================================================

/** Clamps a rate to the valid [0, 1] probability/mitigation range. */
function _clamp01(v: number): number {
  return Math.min(1, Math.max(0, v));
}

/**
 * Computes a combatant's *effective* reaction skills by folding its accumulated
 * activeBuffs/debuffs (elemental-path self-buffs and attacker-applied debuffs)
 * into its base reactionSkills via applyDynamicModifiers.
 *
 * Defense resolution reads these effective rates so that path buffs/debuffs have
 * a real mechanical effect. Crushing Blow is intentionally NOT folded here — it
 * is written directly to base reactionSkills.block (pipeline's
 * _applyCrushingBlowDebuff) and would double-count if it also rode through
 * activeBuffs.
 *
 * Shared by the per-attack pipeline and the counter chain so both resolve against
 * the same folded rates (ADR-053). Each rate is clamped to [0, 1].
 *
 * @param combatant - The defender whose effective reaction skills to compute
 * @returns ReactionSkills with all active buffs/debuffs folded and clamped
 */
export function effectiveReactionSkills(combatant: Combatant): ReactionSkills {
  const base: ModifiedStats = {
    power: combatant.power,
    speed: combatant.speed,
    blockSR: combatant.reactionSkills.block.SR,
    blockSMR: combatant.reactionSkills.block.SMR,
    blockFMR: combatant.reactionSkills.block.FMR,
    dodgeSR: combatant.reactionSkills.dodge.SR,
    dodgeSMR: combatant.reactionSkills.dodge.SMR,
    dodgeFMR: combatant.reactionSkills.dodge.FMR,
    parrySR: combatant.reactionSkills.parry.SR,
    parrySMR: combatant.reactionSkills.parry.SMR,
    parryFMR: combatant.reactionSkills.parry.FMR,
  };
  // Path debuffs are stored as buffs with negative modifiers, so the dedicated
  // debuffs array is empty here; both fold through the buffs argument.
  const m = applyDynamicModifiers(base, combatant.activeBuffs, []);
  return {
    block: { SR: _clamp01(m.blockSR), SMR: _clamp01(m.blockSMR), FMR: _clamp01(m.blockFMR) },
    dodge: { SR: _clamp01(m.dodgeSR), SMR: _clamp01(m.dodgeSMR), FMR: _clamp01(m.dodgeFMR) },
    parry: { SR: _clamp01(m.parrySR), SMR: _clamp01(m.parrySMR), FMR: _clamp01(m.parryFMR) },
  };
}
