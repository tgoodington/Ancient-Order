/**
 * Ancient Order - Player Declaration Validation
 *
 * Validates player-submitted combat action declarations before they enter the
 * resolution pipeline. All validation checks are performed in a strict order
 * and the function returns a typed result (never throws).
 *
 * Pure function — no side effects, no state mutation.
 */

import type { CombatAction, CombatState, Combatant } from '../types/combat.js';

// ============================================================================
// Validation Result Type
// ============================================================================

/**
 * Result of a declaration validation check.
 * On failure, `error` describes the reason and `fallback` provides a safe
 * replacement action when one can be determined (e.g., GROUP → ATTACK).
 */
export type ValidationResult =
  | { valid: true }
  | { valid: false; error: string; fallback?: CombatAction };

// ============================================================================
// Valid Action Types Set
// ============================================================================

/**
 * The set of all valid ActionType string literals.
 * Used for runtime type checking of incoming action declarations.
 */
const VALID_ACTION_TYPES = new Set<string>([
  'ATTACK',
  'DEFEND',
  'EVADE',
  'SPECIAL',
  'GROUP',
]);

// ============================================================================
// Party Lookup Helpers
// ============================================================================

/**
 * Finds the party (player or enemy) that the given combatant belongs to.
 * Returns null if the combatant is not found in either party.
 */
function _findParty(
  state: CombatState,
  combatantId: string,
): readonly Combatant[] | null {
  if (state.playerParty.some((c) => c.id === combatantId)) {
    return state.playerParty;
  }
  if (state.enemyParty.some((c) => c.id === combatantId)) {
    return state.enemyParty;
  }
  return null;
}

/**
 * Returns the opposing party relative to the given combatant.
 * Returns null if the combatant is not found in either party.
 */
function _findOpposingParty(
  state: CombatState,
  combatantId: string,
): readonly Combatant[] | null {
  if (state.playerParty.some((c) => c.id === combatantId)) {
    return state.enemyParty;
  }
  if (state.enemyParty.some((c) => c.id === combatantId)) {
    return state.playerParty;
  }
  return null;
}

/**
 * Finds a combatant by ID across both parties.
 * Returns undefined if not found.
 */
function _findCombatant(
  state: CombatState,
  combatantId: string,
): Combatant | undefined {
  return (
    state.playerParty.find((c) => c.id === combatantId) ??
    state.enemyParty.find((c) => c.id === combatantId)
  );
}

// ============================================================================
// Energy Utility
// ============================================================================

/**
 * Returns the maximum energy segments for a combatant at their current
 * ascension level. Uses the combatant's own `maxEnergy` field as the
 * authoritative value — this field is set at encounter initialization based
 * on ascension level and is the canonical "full energy" threshold.
 *
 * Exported for use by the energy system (Task 13) and GROUP resolution (Task 18).
 */
export function maxEnergyForAscensionLevel(combatant: Combatant): number {
  return combatant.maxEnergy;
}

// ============================================================================
// Individual Validation Steps
// ============================================================================

/**
 * Check 1: Combatant exists in the combat state and is not KO'd.
 */
function _validateCombatantActive(
  state: CombatState,
  action: CombatAction,
): ValidationResult {
  const combatant = _findCombatant(state, action.combatantId);
  if (combatant === undefined) {
    return {
      valid: false,
      error: `Combatant '${action.combatantId}' not found in combat`,
    };
  }
  if (combatant.isKO) {
    return {
      valid: false,
      error: `Combatant '${action.combatantId}' is KO'd and cannot act`,
    };
  }
  return { valid: true };
}

/**
 * Check 2: Action type is a member of the ActionType union.
 */
function _validateActionType(action: CombatAction): ValidationResult {
  if (!VALID_ACTION_TYPES.has(action.type)) {
    return {
      valid: false,
      error: `'${action.type}' is not a valid action type`,
    };
  }
  return { valid: true };
}

/**
 * Check 3a: ATTACK — target must be a non-KO'd enemy.
 */
function _validateAttackTarget(
  state: CombatState,
  action: CombatAction,
): ValidationResult {
  if (action.targetId === null) {
    return { valid: false, error: 'ATTACK requires a target' };
  }
  const opposingParty = _findOpposingParty(state, action.combatantId);
  if (opposingParty === null) {
    return {
      valid: false,
      error: `Combatant '${action.combatantId}' is not in a valid party`,
    };
  }
  const target = opposingParty.find((c) => c.id === action.targetId);
  if (target === undefined) {
    return {
      valid: false,
      error: `Target '${action.targetId}' is not a valid enemy target`,
    };
  }
  if (target.isKO) {
    return {
      valid: false,
      error: `Target '${action.targetId}' is already KO'd`,
    };
  }
  return { valid: true };
}

/**
 * Check 3b: DEFEND — target must be a non-KO'd ally.
 */
function _validateDefendTarget(
  state: CombatState,
  action: CombatAction,
): ValidationResult {
  if (action.targetId === null) {
    return { valid: false, error: 'DEFEND requires a target ally' };
  }
  const ownParty = _findParty(state, action.combatantId);
  if (ownParty === null) {
    return {
      valid: false,
      error: `Combatant '${action.combatantId}' is not in a valid party`,
    };
  }
  const target = ownParty.find((c) => c.id === action.targetId);
  if (target === undefined) {
    return {
      valid: false,
      error: `Target '${action.targetId}' is not a valid ally target`,
    };
  }
  if (target.isKO) {
    return {
      valid: false,
      error: `Target '${action.targetId}' is already KO'd and cannot be defended`,
    };
  }
  return { valid: true };
}

/**
 * Check 3c: EVADE — no target needed (targetId must be null).
 */
function _validateEvadeTarget(action: CombatAction): ValidationResult {
  if (action.targetId !== null) {
    return {
      valid: false,
      error: 'EVADE does not take a target; targetId must be null',
    };
  }
  return { valid: true };
}

/**
 * Check 3d: GROUP — target must be a non-KO'd enemy (same as ATTACK).
 * Also validates that all non-KO'd allies have full energy.
 * On energy failure, provides a fallback ATTACK action on the same target.
 */
function _validateGroupTarget(
  state: CombatState,
  action: CombatAction,
): ValidationResult {
  // Target must be a non-KO'd enemy
  const targetResult = _validateAttackTarget(state, action);
  if (!targetResult.valid) {
    return targetResult;
  }

  // All non-KO'd allies (including leader) must have full energy
  const ownParty = _findParty(state, action.combatantId);
  if (ownParty === null) {
    return {
      valid: false,
      error: `Combatant '${action.combatantId}' is not in a valid party`,
    };
  }

  const fallbackAction: CombatAction = {
    combatantId: action.combatantId,
    type: 'ATTACK',
    targetId: action.targetId,
  };

  for (const ally of ownParty) {
    if (ally.isKO) continue; // KO'd allies are excluded from GROUP
    const maxEnergy = maxEnergyForAscensionLevel(ally);
    if (ally.energy < maxEnergy) {
      return {
        valid: false,
        error: `GROUP rejected: '${ally.id}' does not have full energy (${ally.energy}/${maxEnergy})`,
        fallback: fallbackAction,
      };
    }
  }

  return { valid: true };
}

/**
 * Check 4: Stamina check — combatant has stamina > 0.
 * (Any action requires the combatant to be alive, stamina 0 = KO)
 */
function _validateStamina(
  state: CombatState,
  action: CombatAction,
): ValidationResult {
  const combatant = _findCombatant(state, action.combatantId);
  // Combatant existence already verified in check 1; this is a type-safe guard
  if (combatant === undefined) {
    return {
      valid: false,
      error: `Combatant '${action.combatantId}' not found`,
    };
  }
  if (combatant.stamina <= 0) {
    return {
      valid: false,
      error: `Combatant '${action.combatantId}' has no stamina and cannot act`,
    };
  }
  return { valid: true };
}

/**
 * Check 5: SPECIAL — combatant must have energy segments > 0.
 */
function _validateSpecialEnergy(
  state: CombatState,
  action: CombatAction,
): ValidationResult {
  const combatant = _findCombatant(state, action.combatantId);
  if (combatant === undefined) {
    return {
      valid: false,
      error: `Combatant '${action.combatantId}' not found`,
    };
  }
  if (combatant.energy <= 0) {
    return {
      valid: false,
      error: `Combatant '${action.combatantId}' has no energy segments for SPECIAL`,
    };
  }
  return { valid: true };
}

// ============================================================================
// Main Validation Function
// ============================================================================

/**
 * Validates a player-submitted combat action declaration against the current
 * combat state. Checks are performed in order; the first failure is returned.
 *
 * Validation order:
 *  1. Combatant exists and is not KO'd
 *  2. Action type is valid (ActionType union)
 *  3. Target validation (rules vary by action type)
 *  4. Stamina check: combatant has stamina > 0
 *  5. SPECIAL: combatant has energy segments > 0
 *  6. GROUP: all non-KO'd allies have full energy (fallback: ATTACK on same target)
 *
 * @param state  Current CombatState
 * @param action The declared CombatAction to validate
 * @returns      ValidationResult — { valid: true } or { valid: false; error; fallback? }
 */
export function validateDeclaration(
  state: CombatState,
  action: CombatAction,
): ValidationResult {
  // Check 1: Combatant exists and is not KO'd
  const combatantCheck = _validateCombatantActive(state, action);
  if (!combatantCheck.valid) return combatantCheck;

  // Check 2: Action type is valid
  const typeCheck = _validateActionType(action);
  if (!typeCheck.valid) return typeCheck;

  // Check 3: Target validation (rules differ by action type)
  let targetCheck: ValidationResult;
  switch (action.type) {
    case 'ATTACK':
      targetCheck = _validateAttackTarget(state, action);
      break;
    case 'SPECIAL':
      targetCheck = _validateAttackTarget(state, action);
      break;
    case 'DEFEND':
      targetCheck = _validateDefendTarget(state, action);
      break;
    case 'EVADE':
      targetCheck = _validateEvadeTarget(action);
      break;
    case 'GROUP':
      // GROUP target validation includes the energy check for all allies.
      // Run target check first (via _validateAttackTarget inside _validateGroupTarget),
      // then the energy gate. The GROUP-specific energy failure is check 6 logically,
      // but must come before the generic stamina check to produce a fallback action.
      targetCheck = _validateGroupTarget(state, action);
      break;
    default: {
      // TypeScript exhaustiveness: action.type is narrowed to never here
      const _exhaustive: never = action.type;
      return { valid: false, error: `Unhandled action type: ${String(_exhaustive)}` };
    }
  }
  if (!targetCheck.valid) return targetCheck;

  // Check 4: Stamina check
  const staminaCheck = _validateStamina(state, action);
  if (!staminaCheck.valid) return staminaCheck;

  // Check 5: SPECIAL energy check
  if (action.type === 'SPECIAL') {
    const energyCheck = _validateSpecialEnergy(state, action);
    if (!energyCheck.valid) return energyCheck;
  }

  return { valid: true };
}
