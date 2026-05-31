/**
 * Ancient Order - Counter Chain Resolution
 *
 * Resolves the counter chain: a successful Parry triggers a counter attack, and
 * each counter is a *new attack* the target reacts to with its own preferred
 * defense (ADR-053/054). The chain extends only while reactions keep landing
 * Parries.
 *
 * Chain logic:
 *   1. Parrier performs a counter attack on the original attacker.
 *   2. The target reacts with its path's preferred defense (getPreferredDefense)
 *      resolved through the normal resolveDefense path against effective skills.
 *   3. The counter continues (roles swap) ONLY if that reaction was a successful
 *      Parry. A Block, a Dodge, or a failed Parry mitigates the counter and ends
 *      the chain. This composes with ADR-053: a Fire defender re-counters; a
 *      Light/Air defender blocks/dodges and stops the chain.
 *
 * Every exchange now applies (mitigated) damage — successful Parry/Dodge deal
 * (1 − SMR) × ActionPower rather than zero (ADR-054 Layer A).
 *
 * Termination conditions:
 *   - The target's reaction was not a successful Parry
 *   - Combatant is KO'd (stamina reaches 0)
 *   - Combatant has insufficient stamina to continue
 *
 * Implementation uses a while-loop (not recursion) to prevent stack overflow
 * for long chains. Safety cap: 10 iterations maximum.
 *
 * All state transitions use spread-operator immutability per the project convention.
 */

import { resolveDefense, effectiveReactionSkills } from './defense.js';
import { getPreferredDefense } from './elementalPaths.js';
import { calculateBaseDamage } from './formulas.js';
import { awardReactionXp } from './reactionProgression.js';
import type { CombatState, Combatant, AttackResult } from '../types/combat.js';

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

/**
 * Replaces a single combatant (by id) in whichever party it belongs to.
 * Returns a new CombatState; input is never mutated. No-op if not found.
 */
function _replaceCombatant(state: CombatState, updated: Combatant): CombatState {
  if (state.playerParty.some((c) => c.id === updated.id)) {
    return {
      ...state,
      playerParty: state.playerParty.map((c) => (c.id === updated.id ? updated : c)),
    };
  }
  if (state.enemyParty.some((c) => c.id === updated.id)) {
    return {
      ...state,
      enemyParty: state.enemyParty.map((c) => (c.id === updated.id ? updated : c)),
    };
  }
  return state;
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
 *   2. The current target reacts with its preferred defense (getPreferredDefense),
 *      resolved via resolveDefense against its effective (buff-folded) skills.
 *   3. Mitigated damage is applied to the target every exchange.
 *   4. If the reaction was a successful Parry → roles swap, chain continues.
 *   5. Otherwise (Block, Dodge, or failed Parry) → chain ends after damage.
 *   6. If target is already KO'd or out of stamina → chain ends immediately.
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

    // Calculate damage for this counter-attack (a new attack from the parrier)
    const rawDamage = calculateBaseDamage(attacker.power, target.power);

    // The target reacts with its path's preferred defense, resolved through the
    // normal defense path against its effective (buff-folded) reaction skills.
    const selectedDefense = getPreferredDefense(target.elementalPath);
    const defenseRoll = rollFn();
    const defenseOutcome = resolveDefense(
      selectedDefense,
      rawDamage,
      effectiveReactionSkills(target),
      defenseRoll,
    );

    const finalDamage = rawDamage * defenseOutcome.damageMultiplier;

    chainLength += 1;

    // Build AttackResult record for this exchange
    const attackResult: AttackResult = {
      attackerId,
      targetId,
      damage: finalDamage,
      defenseType: selectedDefense,
      defenseOutcome,
      rankKO: false,
      blindside: false,
      crushingBlow: false,
      counterChain: true,
    };

    actions.push(attackResult);

    // Mitigated damage applies on every exchange (successful Parry/Dodge no longer
    // negate fully — see ADR-054).
    currentState = _applyDamage(currentState, targetId, finalDamage);

    // Reaction XP (ADR-054 Layer B): the reacting defender trains the reaction it
    // used on this exchange. getPreferredDefense always yields a real reaction;
    // awardReactionXp is a no-op for combatants without reactionProgress (enemies).
    if (selectedDefense !== 'defenseless') {
      const reactor = _findCombatant(currentState, targetId);
      if (reactor) {
        currentState = _replaceCombatant(
          currentState,
          awardReactionXp(reactor, selectedDefense, defenseOutcome.success),
        );
      }
    }

    // The chain continues only on a successful Parry: the target becomes the new
    // attacker and counters back. A Block, Dodge, or failed Parry ends it.
    if (selectedDefense === 'parry' && defenseOutcome.success) {
      const updatedTarget = _findCombatant(currentState, targetId);
      // A KO'd parrier cannot counter back — terminate even on a successful parry.
      if (!updatedTarget || updatedTarget.isKO) {
        break;
      }
      const nextAttackerId = targetId;
      const nextTargetId = attackerId;
      attackerId = nextAttackerId;
      targetId = nextTargetId;
    } else {
      break;
    }
  }

  return { state: currentState, chainLength, actions };
}
