/**
 * Ancient Order - Counter Chain Resolution
 *
 * Resolves the Parry counter chain: a successful Parry triggers a counter attack,
 * and the counter target may Parry in turn, extending the chain.
 *
 * Chain logic:
 *   1. Parrier performs a counter attack on the original attacker.
 *   2. The original attacker may Parry the counter; if successful, they counter back.
 *   3. Chain continues until a termination condition is met.
 *
 * Termination conditions:
 *   - Parry fails (defense roll misses)
 *   - Combatant is KO'd (stamina reaches 0)
 *   - Combatant has insufficient stamina to continue
 *
 * Implementation uses a while-loop (not recursion) to prevent stack overflow
 * for long chains. Safety cap: 10 iterations maximum.
 *
 * All state transitions use spread-operator immutability per the project convention.
 */

import { resolveParry } from './defense.js';
import { calculateBaseDamage } from './formulas.js';
import type { CombatState, Combatant, AttackResult, DefenseResult } from '../types/combat.js';

// ============================================================================
// Constants
// ============================================================================

/** Maximum number of counter chain iterations before forced termination. */
const MAX_CHAIN_DEPTH = 10;

// ============================================================================
// Helpers
// ============================================================================

/**
 * Finds a combatant by ID across both parties of a CombatState.
 * Returns undefined if not found.
 */
function _findCombatant(state: CombatState, id: string): Combatant | undefined {
  return (
    state.playerParty.find((c) => c.id === id) ??
    state.enemyParty.find((c) => c.id === id)
  );
}

/**
 * Applies a stamina reduction to a combatant in the combat state.
 * Returns a new CombatState with the updated combatant (immutable spread).
 * Sets isKO to true if stamina reaches 0 or below.
 *
 * @param state      - Current CombatState
 * @param targetId   - ID of the combatant taking damage
 * @param damage     - Damage amount to apply
 * @returns New CombatState with updated target stamina/KO status
 */
function _applyDamage(state: CombatState, targetId: string, damage: number): CombatState {
  const inPlayer = state.playerParty.some((c) => c.id === targetId);
  const inEnemy = state.enemyParty.some((c) => c.id === targetId);

  if (!inPlayer && !inEnemy) {
    return state; // Target not found — no-op
  }

  const updateCombatant = (c: Combatant): Combatant => {
    if (c.id !== targetId) return c;
    const newStamina = Math.max(0, c.stamina - damage);
    return {
      ...c,
      stamina: newStamina,
      isKO: newStamina <= 0,
    };
  };

  if (inPlayer) {
    return {
      ...state,
      playerParty: state.playerParty.map(updateCombatant),
    };
  }

  return {
    ...state,
    enemyParty: state.enemyParty.map(updateCombatant),
  };
}

// ============================================================================
// Counter Chain Resolution
// ============================================================================

/**
 * Resolves a Parry counter chain after a successful initial Parry.
 *
 * Starting conditions:
 *   - `parrier` has just successfully parried an attack from `originalAttacker`
 *   - The parrier now counter-attacks the original attacker
 *   - The original attacker may Parry the counter, extending the chain
 *
 * Each iteration:
 *   1. The current attacker (starts as parrier) deals base damage to the current target.
 *   2. The current target attempts a Parry using their Parry SR.
 *   3. If Parry fails → chain ends, full damage applied.
 *   4. If Parry succeeds → 0 damage, target becomes the next attacker, roles swap.
 *   5. If target is already KO'd → chain ends immediately.
 *   6. If target stamina is insufficient (≤ 0) → chain ends immediately.
 *
 * Stamina depletion check: after damage application, if target stamina ≤ 0, the
 * chain terminates because the KO'd combatant can no longer respond.
 *
 * @param state            - Current CombatState (before counter chain)
 * @param originalAttacker - Combatant who made the original attack (receives first counter)
 * @param parrier          - Combatant who parried (initiates the counter chain)
 * @param rollFn           - Roll injection function; defaults to () => Math.random() * 20
 * @returns Object containing:
 *   - state: Updated CombatState after all counter chain attacks resolve
 *   - chainLength: Number of counter-counter exchanges that occurred (>= 1)
 *   - actions: Array of AttackResult records for each exchange in the chain
 */
export function resolveCounterChain(
  state: CombatState,
  originalAttacker: Combatant,
  parrier: Combatant,
  rollFn: () => number = () => Math.random() * 20,
): { state: CombatState; chainLength: number; actions: AttackResult[] } {
  let currentState = state;
  const actions: AttackResult[] = [];
  let chainLength = 0;

  // The parrier counter-attacks the original attacker first.
  // Roles alternate: attacker → defender → attacker → ...
  let attackerId = parrier.id;
  let targetId = originalAttacker.id;

  while (chainLength < MAX_CHAIN_DEPTH) {
    // Fetch current combatants from live state (stamina may have changed)
    const attacker = _findCombatant(currentState, attackerId);
    const target = _findCombatant(currentState, targetId);

    // Termination: combatant no longer in state or already KO'd
    if (!attacker || !target || attacker.isKO || target.isKO) {
      break;
    }

    // Termination: target has no stamina (effectively KO even if flag not yet set)
    if (target.stamina <= 0) {
      break;
    }

    // Calculate damage for this counter-attack
    const rawDamage = calculateBaseDamage(attacker.power, target.power);

    // Target attempts a Parry
    const parryRoll = rollFn();
    const parryResult = resolveParry(
      rawDamage,
      target.reactionSkills.parry.SR,
      target.reactionSkills.parry.FMR,
      parryRoll,
    );

    chainLength += 1;

    // Build DefenseResult for AttackResult record
    const defenseOutcome: DefenseResult = {
      type: 'parry',
      success: parryResult.success,
      damageMultiplier: rawDamage > 0 ? parryResult.damage / rawDamage : parryResult.success ? 0 : 1 - target.reactionSkills.parry.FMR,
    };

    // Build AttackResult record for this exchange
    const attackResult: AttackResult = {
      attackerId,
      targetId,
      damage: parryResult.damage,
      defenseType: 'parry',
      defenseOutcome,
      rankKO: false,
      blindside: false,
      crushingBlow: false,
      counterChain: true,
    };

    actions.push(attackResult);

    if (parryResult.success) {
      // Parry succeeded: 0 damage, chain continues with roles swapped
      // (The target becomes the new attacker for the next iteration)
      const nextAttackerId = targetId;
      const nextTargetId = attackerId;
      attackerId = nextAttackerId;
      targetId = nextTargetId;
    } else {
      // Parry failed: apply damage, chain terminates
      currentState = _applyDamage(currentState, targetId, parryResult.damage);
      break;
    }
  }

  return { state: currentState, chainLength, actions };
}
