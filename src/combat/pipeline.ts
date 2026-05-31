/**
 * pipeline.ts — Action Priority & Resolution Pipeline for Ancient Order.
 *
 * Two main exports:
 *
 *   sortByPriority(actions, state, rollFn) → CombatAction[]
 *     Sorts the action queue by priority (GROUP=0, DEFEND=1, ATTACK/SPECIAL=2, EVADE=3).
 *     Within the same priority bracket, higher Speed goes first with a random factor
 *     to break exact ties.
 *
 *   resolvePerAttack(state, action, rollFn) → CombatState
 *     7-step per-attack resolution pipeline:
 *       1. Identify true target (check for DEFEND interceptors)
 *       2. Rank KO roll
 *       3. Blindside roll
 *       4. Reaction selection (Defenseless if Blindsided; skip damage if KO'd)
 *       5. Defense roll and damage calculation
 *       6. Counter chain resolution (if Parry succeeded)
 *       7. Stamina/energy updates, buff/debuff application
 *
 * Pure functions — no side effects, no mutations.
 * Roll injection: rollFn defaults to () => Math.random() * 20 (0–20 range).
 *
 * GROUP: resolvePerAttack delegates to resolveGroup() from groupAction.ts (Task 18).
 */

import type {
  CombatAction,
  CombatState,
  Combatant,
  ActionResult,
  AttackResult,
  DefenseType,
  ReactionSkills,
} from '../types/combat.js';
import { ACTION_PRIORITY } from '../types/combat.js';
import {
  calculateRankKOThreshold,
  checkRankKO,
  calculateBlindsideThreshold,
  checkBlindside,
  calculateCrushingBlowThreshold,
  checkCrushingBlow,
  calculateBaseDamage,
  calculateSpecialDamageBonus,
  calculateEvadeRegen,
  applyDynamicModifiers,
  type ModifiedStats,
} from './formulas.js';
import { resolveDefense } from './defense.js';
import { resolveCounterChain } from './counterChain.js';
import {
  applyPathBuff,
  applyPathDebuff,
  getSpecialForceDefense,
  getPreferredDefense,
} from './elementalPaths.js';
import { addEnergySegments, checkAscensionAdvance } from './energy.js';
import { resolveGroupAction, GROUP_ACTION_CONFIG } from './groupAction.js';

// ============================================================================
// Internal Helpers
// ============================================================================

/**
 * Finds a combatant by ID across both parties.
 * Returns undefined if not found.
 */
function _findCombatant(state: CombatState, id: string): Combatant | undefined {
  return (
    state.playerParty.find((c) => c.id === id) ??
    state.enemyParty.find((c) => c.id === id)
  );
}

/**
 * Returns true if the combatant is on the player party.
 */
function _isPlayerCombatant(state: CombatState, id: string): boolean {
  return state.playerParty.some((c) => c.id === id);
}

/**
 * Applies a stamina change to a combatant in the combat state.
 * Negative delta = damage, positive delta = healing (regen).
 * Clamps stamina to [0, maxStamina]. Sets isKO when stamina reaches 0.
 * Returns a new CombatState — input is never mutated.
 */
function _applyStaminaDelta(
  state: CombatState,
  targetId: string,
  delta: number,
): CombatState {
  const updateCombatant = (c: Combatant): Combatant => {
    if (c.id !== targetId) return c;
    const newStamina = Math.min(c.maxStamina, Math.max(0, c.stamina + delta));
    return { ...c, stamina: newStamina, isKO: newStamina <= 0 };
  };

  if (_isPlayerCombatant(state, targetId)) {
    return { ...state, playerParty: state.playerParty.map(updateCombatant) };
  }
  return { ...state, enemyParty: state.enemyParty.map(updateCombatant) };
}

/**
 * Replaces a single combatant in the state with a new version.
 * Used for energy / buff updates that produce a whole new Combatant.
 */
function _replaceCombatant(state: CombatState, updated: Combatant): CombatState {
  if (_isPlayerCombatant(state, updated.id)) {
    return {
      ...state,
      playerParty: state.playerParty.map((c) => (c.id === updated.id ? updated : c)),
    };
  }
  return {
    ...state,
    enemyParty: state.enemyParty.map((c) => (c.id === updated.id ? updated : c)),
  };
}

/**
 * Returns the party that `combatantId` belongs to, as a mutable-safe array.
 * Used to find allies (same party) and enemies (opposing party).
 */
function _getOwnParty(state: CombatState, combatantId: string): readonly Combatant[] {
  return _isPlayerCombatant(state, combatantId)
    ? state.playerParty
    : state.enemyParty;
}

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
 * is written directly to base reactionSkills.block (see _applyCrushingBlowDebuff)
 * and would double-count if it also rode through activeBuffs.
 *
 * Each rate is clamped to [0, 1].
 */
function _effectiveReactionSkills(combatant: Combatant): ReactionSkills {
  const base: ModifiedStats = {
    power: combatant.power,
    speed: combatant.speed,
    blockSR: combatant.reactionSkills.block.SR,
    blockSMR: combatant.reactionSkills.block.SMR,
    blockFMR: combatant.reactionSkills.block.FMR,
    dodgeSR: combatant.reactionSkills.dodge.SR,
    dodgeFMR: combatant.reactionSkills.dodge.FMR,
    parrySR: combatant.reactionSkills.parry.SR,
    parryFMR: combatant.reactionSkills.parry.FMR,
  };
  // Path debuffs are stored as buffs with negative modifiers, so the dedicated
  // debuffs array is empty here; both fold through the buffs argument.
  const m = applyDynamicModifiers(base, combatant.activeBuffs, []);
  return {
    block: { SR: _clamp01(m.blockSR), SMR: _clamp01(m.blockSMR), FMR: _clamp01(m.blockFMR) },
    dodge: { SR: _clamp01(m.dodgeSR), FMR: _clamp01(m.dodgeFMR) },
    parry: { SR: _clamp01(m.parrySR), FMR: _clamp01(m.parryFMR) },
  };
}

// ============================================================================
// Priority Sort
// ============================================================================

/**
 * Sorts an array of CombatActions into resolution order.
 *
 * Primary sort: ACTION_PRIORITY[action.type] ascending
 *   GROUP=0 first, DEFEND=1, ATTACK/SPECIAL=2, EVADE=3 last.
 *
 * Secondary sort (tie within same priority bracket):
 *   - For normal ties: higher combatant Speed goes first.
 *     Exact speed ties are broken by the random factor: rollFn() - rollFn()
 *     (a positive difference puts the first item later).
 *   - For GROUP ties at priority 0: team average speed replaces individual speed.
 *     Exact average ties broken by the same random factor.
 *
 * The rollFn parameter defaults to () => Math.random() * 20.
 * Tests inject a deterministic function for reproducibility.
 *
 * @param actions - Actions to sort (input is not mutated)
 * @param state   - Current CombatState (used to look up combatant speeds)
 * @param rollFn  - Roll injection function (default: random 0–20)
 * @returns New array sorted into resolution order
 */
export function sortByPriority(
  actions: CombatAction[],
  state: CombatState,
  rollFn: () => number = () => Math.random() * 20,
): CombatAction[] {
  return [...actions].sort((a, b) => {
    const priorityA = ACTION_PRIORITY[a.type];
    const priorityB = ACTION_PRIORITY[b.type];

    // Primary: lower priority number resolves first
    if (priorityA !== priorityB) {
      return priorityA - priorityB;
    }

    // Secondary: speed-based tie-breaking within the same priority bracket
    const speedA = _getEffectiveSpeed(a, state);
    const speedB = _getEffectiveSpeed(b, state);

    if (speedA !== speedB) {
      // Higher speed goes first (descending)
      return speedB - speedA;
    }

    // Exact speed tie: random factor
    return rollFn() - rollFn();
  });
}

/**
 * Returns the effective speed value for an action used in priority sorting.
 *
 * For GROUP actions at priority 0: team average speed (non-KO'd allies in same party).
 * For all other actions: the combatant's own speed stat.
 *
 * Returns 0 if the combatant cannot be found.
 */
function _getEffectiveSpeed(action: CombatAction, state: CombatState): number {
  if (action.type === 'GROUP') {
    // GROUP: use team average speed
    const party = _getOwnParty(state, action.combatantId);
    const active = party.filter((c) => !c.isKO);
    if (active.length === 0) return 0;
    const totalSpeed = active.reduce((sum, c) => sum + c.speed, 0);
    return totalSpeed / active.length;
  }

  const combatant = _findCombatant(state, action.combatantId);
  return combatant?.speed ?? 0;
}

// ============================================================================
// Per-Attack Resolution Pipeline
// ============================================================================

/**
 * Resolves a single combat action, returning both the new CombatState and the
 * ActionResult describing what happened (for the round history).
 *
 * This is the single source of truth for per-action resolution. The Round
 * Manager calls it directly to collect accurate ActionResults; `resolvePerAttack`
 * is a thin wrapper that returns only the state for callers/tests that don't
 * need the result.
 *
 * For GROUP actions, delegates to resolveGroupAction() (Task 18 implementation).
 * For EVADE actions, applies stamina regen.
 * For DEFEND actions, the action is a marker used in Step 1 (DEFEND intercept);
 * resolution itself is a no-op for DEFEND.
 * For ATTACK and SPECIAL, runs the full 7-step resolution.
 *
 * @param state   - Current CombatState
 * @param action  - The action being resolved
 * @param rollFn  - Roll injection function (default: random 0–20)
 * @returns { state, result } — new CombatState and the action's ActionResult
 */
export function resolveAction(
  state: CombatState,
  action: CombatAction,
  rollFn: () => number = () => Math.random() * 20,
): { state: CombatState; result: ActionResult } {
  // GROUP — delegate to resolveGroupAction (Task 18 implementation)
  if (action.type === 'GROUP') {
    const declaration = {
      leaderId: action.combatantId,
      targetId: action.targetId ?? '',
    };
    return resolveGroupAction(state, declaration, GROUP_ACTION_CONFIG, rollFn);
  }

  // DEFEND — marker only; used in Step 1 intercept logic. No active effect here.
  if (action.type === 'DEFEND') {
    return { state, result: { combatantId: action.combatantId, type: 'DEFEND' } };
  }

  // EVADE — stamina regen, energy gain, no attack resolves
  if (action.type === 'EVADE') {
    return _resolveEvade(state, action, rollFn);
  }

  // ATTACK or SPECIAL — full 7-step pipeline
  return _resolveAttack(state, action, rollFn);
}

/**
 * Thin wrapper returning only the CombatState after an action resolves.
 * Retained for callers/tests that do not need the ActionResult.
 *
 * @param state   - Current CombatState
 * @param action  - The action being resolved
 * @param rollFn  - Roll injection function (default: random 0–20)
 * @returns New CombatState after the action resolves
 */
export function resolvePerAttack(
  state: CombatState,
  action: CombatAction,
  rollFn: () => number = () => Math.random() * 20,
): CombatState {
  return resolveAction(state, action, rollFn).state;
}

// ============================================================================
// EVADE Resolution
// ============================================================================

/**
 * Resolves an EVADE action: restore 30% of maxStamina, gain energy.
 */
function _resolveEvade(
  state: CombatState,
  action: CombatAction,
  rollFn: () => number,
): { state: CombatState; result: ActionResult } {
  void rollFn; // not used for evade, but kept for consistent signature
  const combatant = _findCombatant(state, action.combatantId);
  if (!combatant || combatant.isKO) {
    return { state, result: { combatantId: action.combatantId, type: 'EVADE' } };
  }

  const regen = calculateEvadeRegen(combatant.maxStamina);
  let currentState = _applyStaminaDelta(state, combatant.id, regen);

  // Energy gain for evade (counts as action success)
  const freshCombatant = _findCombatant(currentState, combatant.id)!;
  const withEnergy = addEnergySegments(freshCombatant, 'actionSuccess', 'success');
  const withAscension = checkAscensionAdvance(withEnergy);
  currentState = _replaceCombatant(currentState, withAscension);

  // Record the action result
  const actionResult: ActionResult = {
    combatantId: action.combatantId,
    type: 'EVADE',
  };
  return { state: currentState, result: actionResult };
}

// ============================================================================
// Step 1: DEFEND Intercept
// ============================================================================

/**
 * Step 1: Identify the true target of an attack.
 *
 * Scans the actionQueue for a DEFEND action targeting the same combatant as
 * the current attack's targetId. If found, the DEFEND-er intercepts and
 * becomes the true target.
 *
 * @param state    - Current CombatState (actionQueue checked for DEFEND actions)
 * @param action   - The ATTACK or SPECIAL action being resolved
 * @returns The ID of the true target (either original target or intercepting DEFEND-er)
 */
function _resolveDefendIntercept(state: CombatState, action: CombatAction): string {
  if (action.targetId === null) return ''; // EVADE has no target; shouldn't reach here

  // Look for a DEFEND action whose targetId matches this attack's targetId
  const interceptor = state.actionQueue.find(
    (q) => q.type === 'DEFEND' && q.targetId === action.targetId,
  );

  if (interceptor) {
    return interceptor.combatantId; // The DEFEND-er takes the hit
  }

  return action.targetId;
}

// ============================================================================
// Full Attack Resolution (ATTACK / SPECIAL)
// ============================================================================

/**
 * Runs the full 7-step resolution pipeline for ATTACK and SPECIAL actions.
 */
function _resolveAttack(
  state: CombatState,
  action: CombatAction,
  rollFn: () => number,
): { state: CombatState; result: ActionResult } {
  const minimalResult: ActionResult = { combatantId: action.combatantId, type: action.type };

  if (action.targetId === null) return { state, result: minimalResult }; // Malformed — no target

  const attacker = _findCombatant(state, action.combatantId);
  if (!attacker || attacker.isKO) return { state, result: minimalResult };

  // ------------------------------------------------------------------
  // Step 1: Identify true target (DEFEND intercept)
  // ------------------------------------------------------------------
  const trueTargetId = _resolveDefendIntercept(state, action);
  const target = _findCombatant(state, trueTargetId);
  if (!target) return { state, result: minimalResult };

  // If the true target is already KO'd, skip damage resolution entirely
  if (target.isKO) {
    return { state, result: minimalResult };
  }

  // ------------------------------------------------------------------
  // Step 2: Rank KO roll
  // ------------------------------------------------------------------
  let rankKO = false;
  if (attacker.rank - target.rank >= 0.5) {
    // Eligibility: attacker rank exceeds target rank by at least 0.5 (Excel Math!D).
    const rankKOThreshold = calculateRankKOThreshold(attacker.rank, target.rank);
    rankKO = checkRankKO(rankKOThreshold, rollFn());
  }

  // ------------------------------------------------------------------
  // Step 3: Blindside roll
  // ------------------------------------------------------------------
  let blindside = false;
  if (attacker.speed > target.speed) {
    const blindsideThreshold = calculateBlindsideThreshold(attacker.speed, target.speed);
    blindside = checkBlindside(blindsideThreshold, rollFn());
  }

  // ------------------------------------------------------------------
  // Step 4: Reaction selection
  // ------------------------------------------------------------------
  // Blindsided → Defenseless (forced, cannot react)
  // If Rank KO landed → target still reacts (KO is an additional effect, not a skip)
  // Forced defense from SPECIAL + elemental path
  let selectedDefense: DefenseType;

  if (blindside) {
    selectedDefense = 'defenseless';
  } else if (action.type === 'SPECIAL') {
    // SPECIAL: the attacker's elemental path forces the target's defense type.
    selectedDefense = getSpecialForceDefense(attacker.elementalPath);
  } else {
    // Normal ATTACK: the defender reacts with its own path's preferred defense
    // (reaction paths use their signature defense; action paths default to block).
    selectedDefense = getPreferredDefense(target.elementalPath);
  }

  // ------------------------------------------------------------------
  // Step 5: Defense roll and damage calculation
  // ------------------------------------------------------------------
  // Action Power — Excel Math!O = MROUND(L + M + N, 0.25), a SINGLE rounding step.
  // For a SPECIAL, M = Power × (1 + 0.1·segments); compute the raw boosted power
  // first, then round once via calculateBaseDamage. Rounding the base power before
  // applying the Special multiplier (the old order) diverges from the sheet for
  // any power whose boosted product is not already 0.25-aligned.
  let actionPower = attacker.power;
  if (action.type === 'SPECIAL') {
    const segments = action.energySegments ?? attacker.energy;
    actionPower = calculateSpecialDamageBonus(actionPower, segments);
  }
  const rawDamage = calculateBaseDamage(actionPower, target.power);

  // Resolve against the target's *effective* reaction skills: base rates folded
  // with accumulated elemental-path buffs/debuffs (Crushing Blow excluded — it is
  // already baked into base block rates).
  const effectiveSkills = _effectiveReactionSkills(target);
  const defenseRoll = rollFn();
  const defenseResult = resolveDefense(selectedDefense, rawDamage, effectiveSkills, defenseRoll);

  // Crushing Blow check: only applies when Block was used AND attacker power > target power
  let crushingBlow = false;
  if (selectedDefense === 'block' && attacker.power > target.power) {
    const cbThreshold = calculateCrushingBlowThreshold(attacker.power, target.power);
    crushingBlow = checkCrushingBlow(cbThreshold, rollFn());
  }

  // Final damage applied to target stamina
  const finalDamage = rawDamage * defenseResult.damageMultiplier;
  let currentState = _applyStaminaDelta(state, trueTargetId, -finalDamage);

  // ------------------------------------------------------------------
  // Step 6: Counter chain resolution (if Parry succeeded)
  // ------------------------------------------------------------------
  let counterChainTriggered = false;

  if (selectedDefense === 'parry' && defenseResult.success) {
    // Parry succeeded — resolve the counter chain.
    // We need the current attacker and target from the updated state.
    const freshAttacker = _findCombatant(currentState, attacker.id)!;
    const freshTarget = _findCombatant(currentState, trueTargetId)!;

    const chainResult = resolveCounterChain(
      currentState,
      freshAttacker,  // original attacker receives the first counter
      freshTarget,    // parrier initiates the counter chain
      rollFn,
    );
    currentState = chainResult.state;
    counterChainTriggered = chainResult.chainLength > 0;
  } else if (selectedDefense === 'block' && defenseResult.success) {
    // Block succeeded — check Parry within the pipeline via a separate roll.
    // Per the spec, this is the crushing blow eligible path. No counter chain for block.
    // (Counter chain only triggers from Parry.)
  }

  // ------------------------------------------------------------------
  // Step 7: Stamina/energy updates, buff/debuff application
  // ------------------------------------------------------------------

  // Attacker energy gain
  const freshAttackerAfterDamage = _findCombatant(currentState, attacker.id)!;
  const attackerEventResult: 'success' | 'failure' = finalDamage > 0 ? 'success' : 'failure';
  const attackerWithEnergy = addEnergySegments(
    freshAttackerAfterDamage,
    'actionSuccess',
    attackerEventResult,
  );
  const attackerWithAscension = checkAscensionAdvance(attackerWithEnergy);
  currentState = _replaceCombatant(currentState, attackerWithAscension);

  // Target energy gain (for reacting)
  const freshTargetAfterDamage = _findCombatant(currentState, trueTargetId)!;
  if (!freshTargetAfterDamage.isKO) {
    const targetEventResult: 'success' | 'failure' = defenseResult.success ? 'success' : 'failure';
    const targetWithEnergy = addEnergySegments(
      freshTargetAfterDamage,
      'reactionSuccess',
      targetEventResult,
    );
    const targetWithAscension = checkAscensionAdvance(targetWithEnergy);
    currentState = _replaceCombatant(currentState, targetWithAscension);
  }

  // Elemental path buff/debuff application
  // Only applied when attacker has an action path (debuffs target's SR)
  // or target has a reaction path (buffs own SR when defending successfully)
  const finalAttacker = _findCombatant(currentState, attacker.id)!;
  const finalTarget = _findCombatant(currentState, trueTargetId)!;

  // Action paths: debuff target on a successful attack (damage dealt)
  if (finalDamage > 0) {
    const attackerPathConfig = _getPathType(finalAttacker.elementalPath);
    if (attackerPathConfig === 'action') {
      const targetWithDebuff = applyPathDebuff(finalTarget, finalAttacker.elementalPath);
      currentState = _replaceCombatant(currentState, targetWithDebuff);
    }
  }

  // Reaction paths: buff own SR when the target successfully defends
  if (defenseResult.success) {
    const freshTargetForBuff = _findCombatant(currentState, trueTargetId)!;
    const targetPathConfig = _getPathType(freshTargetForBuff.elementalPath);
    if (targetPathConfig === 'reaction') {
      const targetWithBuff = applyPathBuff(freshTargetForBuff, freshTargetForBuff.elementalPath);
      currentState = _replaceCombatant(currentState, targetWithBuff);
    }
  }

  // SPECIAL: consume energy segments
  if (action.type === 'SPECIAL') {
    const freshAttackerForSpecial = _findCombatant(currentState, attacker.id)!;
    const segmentsUsed = action.energySegments ?? freshAttackerForSpecial.energy;
    const updatedAttacker: Combatant = {
      ...freshAttackerForSpecial,
      energy: Math.max(0, freshAttackerForSpecial.energy - segmentsUsed),
    };
    currentState = _replaceCombatant(currentState, updatedAttacker);
  }

  // Crushing Blow effect: degrade the target's Block rates (SR/SMR/FMR) for the
  // remainder of combat (GM doc: "Applies debuffs to target's Block SR, SMR, and FMR").
  if (crushingBlow) {
    currentState = _applyCrushingBlowDebuff(currentState, trueTargetId);
  }

  // Rank KO effect: force target to KO if the Rank KO roll succeeded
  if (rankKO) {
    currentState = _applyStaminaDelta(currentState, trueTargetId, -Infinity);
  }

  // ------------------------------------------------------------------
  // Build AttackResult and ActionResult for the round history
  // ------------------------------------------------------------------
  const attackResult: AttackResult = {
    attackerId: action.combatantId,
    targetId: trueTargetId,
    damage: finalDamage,
    defenseType: selectedDefense,
    defenseOutcome: defenseResult,
    rankKO,
    blindside,
    crushingBlow,
    counterChain: counterChainTriggered,
  };

  const actionResult: ActionResult = {
    combatantId: action.combatantId,
    type: action.type,
    attackResult,
  };

  return { state: currentState, result: actionResult };
}

// ============================================================================
// Crushing Blow effect
// ============================================================================

/**
 * Standard Crushing Blow debuff magnitude applied to each Block rate.
 * Matches the 0.10 per-effect modifier used by elemental path debuffs.
 */
const CRUSHING_BLOW_DEBUFF = 0.1;

/**
 * Applies the Crushing Blow effect: reduces the target's Block SR, SMR, and FMR
 * by CRUSHING_BLOW_DEBUFF (clamped to >= 0), persisting for the rest of combat.
 *
 * Applied directly to reactionSkills because defense resolution reads those
 * rates directly. (Elemental-path buffs/debuffs are accumulated in activeBuffs
 * but are not yet folded into resolution — a separate known gap. Crushing Blow
 * is applied here so it has a real, observable effect today.)
 *
 * Returns a new CombatState — input is never mutated.
 */
function _applyCrushingBlowDebuff(state: CombatState, targetId: string): CombatState {
  const reduce = (v: number): number => Math.max(0, v - CRUSHING_BLOW_DEBUFF);
  const target = _findCombatant(state, targetId);
  if (!target) return state;

  const debuffed: Combatant = {
    ...target,
    reactionSkills: {
      ...target.reactionSkills,
      block: {
        SR: reduce(target.reactionSkills.block.SR),
        SMR: reduce(target.reactionSkills.block.SMR),
        FMR: reduce(target.reactionSkills.block.FMR),
      },
    },
  };
  return _replaceCombatant(state, debuffed);
}

// ============================================================================
// Path type lookup helper
// ============================================================================

/**
 * Returns whether the given elemental path is an action or reaction path.
 * Avoids importing ELEMENTAL_PATH_CONFIG by using direct mapping.
 */
function _getPathType(path: string): 'action' | 'reaction' {
  const actionPaths = new Set(['Water', 'Earth', 'Shadow']);
  return actionPaths.has(path) ? 'action' : 'reaction';
}

