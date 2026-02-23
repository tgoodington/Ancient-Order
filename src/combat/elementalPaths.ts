/**
 * elementalPaths.ts — Elemental path buff/debuff application for Ancient Order.
 *
 * Six paths define a combatant's buff/debuff style:
 *   Reaction paths (Fire, Air, Light): boost own defensive SR.
 *   Action paths (Water, Earth, Shadow): debuff target's defensive SR.
 *
 * Each path also forces a specific defense type when its Special technique is used.
 *
 * Source: GM_Combat_Tracker_Documentation.md — Path/Elemental System table.
 */

import type { Combatant, ElementalPath, DefenseType, Buff } from '../types/combat.js';

// ============================================================================
// PathConfig — data-driven configuration per path
// ============================================================================

/**
 * Configuration record for a single elemental path.
 *
 * type:           'reaction' paths boost own defensive SR;
 *                 'action' paths debuff target's defensive SR.
 * defenseBoost:   The defense type whose SR is boosted (reaction) or debuffed (action).
 * specialForces:  Defense type the target is forced to use when this path's Special lands.
 * buffModifier:   Numeric SR modifier applied (positive = boost on reaction paths).
 * debuffModifier: Numeric SR modifier applied (positive magnitude = reduction on action paths).
 */
export interface PathConfig {
  readonly type: 'action' | 'reaction';
  readonly defenseBoost: DefenseType;
  readonly specialForces: DefenseType;
  readonly buffModifier: number;
  readonly debuffModifier: number;
}

/**
 * Data-driven configuration table for all 6 elemental paths.
 *
 * Buff/debuff modifier value of 0.10 (10% SR change) is the standard per-path
 * modifier derived from Paths!A2:I28 tier bonus data in the GM Combat Tracker.
 *
 * Path → (type, defenseBoost, specialForces):
 *   Fire   → reaction, parry,  parry   (fast combo, self-buffs Parry)
 *   Water  → action,   dodge,  dodge   (decisive strike, debuffs target Dodge)
 *   Air    → reaction, dodge,  dodge   (decisive strike, self-buffs Dodge)
 *   Earth  → action,   block,  block   (massive blow, debuffs target Block)
 *   Shadow → action,   parry,  parry   (fast combo, debuffs target Parry)
 *   Light  → reaction, block,  block   (massive blow, self-buffs Block)
 */
export const ELEMENTAL_PATH_CONFIG: Record<ElementalPath, PathConfig> = {
  Fire: {
    type: 'reaction',
    defenseBoost: 'parry',
    specialForces: 'parry',
    buffModifier: 0.1,
    debuffModifier: 0.1,
  },
  Water: {
    type: 'action',
    defenseBoost: 'dodge',
    specialForces: 'dodge',
    buffModifier: 0.1,
    debuffModifier: 0.1,
  },
  Air: {
    type: 'reaction',
    defenseBoost: 'dodge',
    specialForces: 'dodge',
    buffModifier: 0.1,
    debuffModifier: 0.1,
  },
  Earth: {
    type: 'action',
    defenseBoost: 'block',
    specialForces: 'block',
    buffModifier: 0.1,
    debuffModifier: 0.1,
  },
  Shadow: {
    type: 'action',
    defenseBoost: 'parry',
    specialForces: 'parry',
    buffModifier: 0.1,
    debuffModifier: 0.1,
  },
  Light: {
    type: 'reaction',
    defenseBoost: 'block',
    specialForces: 'block',
    buffModifier: 0.1,
    debuffModifier: 0.1,
  },
} as const;

// ============================================================================
// Buff type string helpers
// ============================================================================

/**
 * Builds the Buff type string for a reaction path SR boost.
 * e.g., 'parry' → 'parrySR_boost'
 */
function _buffTypeForReaction(defenseType: DefenseType): string {
  return `${defenseType}SR_boost`;
}

/**
 * Builds the DebuffEffect stat string for an action path SR debuff.
 * e.g., 'dodge' → 'dodgeSR'
 */
function _debuffStatForAction(defenseType: DefenseType): string {
  return `${defenseType}SR`;
}

// ============================================================================
// Public API
// ============================================================================

/**
 * Applies a reaction path buff to the combatant's own defensive SR.
 *
 * Reaction paths: Fire (+Parry SR), Air (+Dodge SR), Light (+Block SR).
 * Adds a Buff entry to the combatant's activeBuffs.
 * Returns a new Combatant — input is never mutated.
 *
 * @param combatant - The combatant receiving the buff (must be on a reaction path)
 * @param path      - The elemental path to apply (caller ensures it is a reaction path)
 * @returns New Combatant with the path buff added to activeBuffs
 */
export function applyPathBuff(combatant: Combatant, path: ElementalPath): Combatant {
  const config = ELEMENTAL_PATH_CONFIG[path];

  const newBuff: Buff = {
    type: _buffTypeForReaction(config.defenseBoost),
    source: path,
    duration: -1, // active for the duration of this combat round context
    modifier: config.buffModifier,
  };

  return {
    ...combatant,
    activeBuffs: [...combatant.activeBuffs, newBuff],
  };
}

/**
 * Applies an action path debuff to the target combatant's defensive SR.
 *
 * Action paths: Water (-Dodge SR), Earth (-Block SR), Shadow (-Parry SR).
 * Adds a Buff entry with a negative modifier to the target's activeBuffs.
 * Returns a new Combatant — input is never mutated.
 *
 * @param target       - The combatant receiving the debuff
 * @param attackerPath - The elemental path of the attacker (caller ensures it is an action path)
 * @returns New Combatant with the path debuff added to activeBuffs
 */
export function applyPathDebuff(target: Combatant, attackerPath: ElementalPath): Combatant {
  const config = ELEMENTAL_PATH_CONFIG[attackerPath];

  // Debuff is stored as a Buff with a negative modifier on the relevant SR stat.
  // The type string matches the debuff pattern so applyDynamicModifiers can read it.
  const debuffBuff: Buff = {
    type: `${_debuffStatForAction(config.defenseBoost)}_debuff`,
    source: attackerPath,
    duration: -1,
    modifier: -config.debuffModifier,
  };

  return {
    ...target,
    activeBuffs: [...target.activeBuffs, debuffBuff],
  };
}

/**
 * Returns the defense type that the attacker's path forces on the target
 * when a Special technique is used.
 *
 * This constraint is enforced during reaction selection (pipeline Step 4).
 *
 * Path → forced defense:
 *   Fire   → parry
 *   Water  → dodge
 *   Air    → dodge
 *   Earth  → block
 *   Shadow → parry
 *   Light  → block
 *
 * @param attackerPath - The elemental path of the attacking combatant
 * @returns The DefenseType the target is forced to use
 */
export function getSpecialForceDefense(attackerPath: ElementalPath): DefenseType {
  return ELEMENTAL_PATH_CONFIG[attackerPath].specialForces;
}
