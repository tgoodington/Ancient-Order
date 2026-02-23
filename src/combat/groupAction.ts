/**
 * groupAction.ts — GROUP Action Resolution for Ancient Order.
 *
 * Implements the GROUP action type: a coordinated team attack where the leader
 * conscripts all non-KO'd allies into a single synchronized strike.
 *
 * Key properties (from design_spec_group_action_type.md):
 *   - Priority 0: GROUP resolves before any other action type.
 *   - Leader-initiated: one combatant declares GROUP; all non-KO'd allies participate.
 *   - Energy gate: all participants must have full energy at declaration time.
 *   - 1.5x synergy multiplier on total damage (regardless of participant count).
 *   - Block-only defense: target is forced to Block; no Dodge, Parry, or counter chain.
 *   - Energy consumption: all participants' energy is reset to 0 on execution.
 *   - Flexible participant count: fires with whoever is still non-KO'd at resolution time.
 *
 * Pure function — returns new CombatState, never mutates input (ADR-012).
 */

import type {
  CombatState,
  Combatant,
  GroupActionDeclaration,
  GroupActionConfig,
  GroupResolutionResult,
  BlockDefenseResult,
  ActionResult,
} from '../types/combat.js';
import { calculateBaseDamage } from './formulas.js';
import { resolveBlock } from './defense.js';

// ============================================================================
// Exported POC Default Config
// ============================================================================

/**
 * Default GROUP action configuration for POC.
 * Exported for use by evaluator config checks and tests.
 */
export const GROUP_ACTION_CONFIG: GroupActionConfig = {
  damageMultiplier: 1.5,
  energyRequirement: 'full',
};

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
 * Returns the party that `combatantId` belongs to.
 */
function _getOwnParty(state: CombatState, combatantId: string): readonly Combatant[] {
  return _isPlayerCombatant(state, combatantId) ? state.playerParty : state.enemyParty;
}

/**
 * Applies a stamina delta to a target combatant, returning a new CombatState.
 * Clamps stamina to [0, maxStamina]. Sets isKO when stamina reaches 0.
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

// ============================================================================
// GROUP Resolution
// ============================================================================

/**
 * Resolves a GROUP action through the full GROUP pipeline.
 *
 * Algorithm (from design_spec_group_action_type.md Section 4):
 *   1. Identify participants: leader + all non-KO'd allies in leader's party
 *   2. Calculate individual damage per participant using calculateBaseDamage()
 *   3. Sum individual damages → multiply by config.damageMultiplier (1.5)
 *   4. Resolve Block defense on target (forced Block — no Dodge, Parry, or counter)
 *   5. Apply final damage to target's stamina
 *   6. Set all participants' energy to 0
 *   7. Record result in roundHistory via ActionResult, return new CombatState
 *
 * Edge cases:
 *   - If target is KO'd at resolution time: GROUP no-ops (damage not applied)
 *   - If allies were KO'd between declaration and resolution (opposing GROUP went
 *     first): fires with remaining non-KO'd participants — multiplier unchanged.
 *   - Solo GROUP (all allies KO'd, leader only) is valid: 1 participant × 1.5x.
 *   - Energy of all participants is zeroed regardless of whether GROUP dealt damage.
 *
 * @param state       - Current CombatState
 * @param declaration - GROUP leader's declaration (leaderId + targetId)
 * @param config      - GroupActionConfig (damageMultiplier, energyRequirement)
 * @param rollFn      - Roll injection function (default: random 0–20)
 * @returns New CombatState after GROUP resolves
 */
export function resolveGroup(
  state: CombatState,
  declaration: GroupActionDeclaration,
  config: GroupActionConfig,
  rollFn: () => number = () => Math.random() * 20,
): CombatState {
  const { leaderId, targetId } = declaration;

  // Locate the leader
  const leader = _findCombatant(state, leaderId);
  if (!leader || leader.isKO) {
    // Leader is gone — GROUP cannot fire
    return state;
  }

  // Locate the target
  const target = _findCombatant(state, targetId);
  if (!target || target.isKO) {
    // Target already KO'd — GROUP no-ops (energy still consumed below)
    const stateWithEnergyDrained = _drainParticipantEnergy(state, leaderId);
    return stateWithEnergyDrained;
  }

  // ------------------------------------------------------------------
  // Step 1: Identify participants — leader + non-KO'd allies
  // ------------------------------------------------------------------
  const ownParty = _getOwnParty(state, leaderId);
  const participants = ownParty.filter((c) => !c.isKO);
  // participants includes the leader (leader is non-KO'd, verified above)

  // ------------------------------------------------------------------
  // Step 2: Calculate individual damage per participant
  // ------------------------------------------------------------------
  const individualDamages: Record<string, number> = {};
  for (const participant of participants) {
    individualDamages[participant.id] = calculateBaseDamage(
      participant.power,
      target.power,
    );
  }

  // ------------------------------------------------------------------
  // Step 3: Sum damages and apply synergy multiplier
  // ------------------------------------------------------------------
  const totalBeforeMultiplier = Object.values(individualDamages).reduce(
    (sum, dmg) => sum + dmg,
    0,
  );
  const groupDamage = totalBeforeMultiplier * config.damageMultiplier;

  // ------------------------------------------------------------------
  // Step 4: Resolve Block defense on target (forced Block — no counter chain)
  // ------------------------------------------------------------------
  const blockRoll = rollFn();
  const blockOutcome = resolveBlock(
    groupDamage,
    target.reactionSkills.block.SR,
    target.reactionSkills.block.SMR,
    target.reactionSkills.block.FMR,
    blockRoll,
  );

  const blockDefenseResult: BlockDefenseResult = {
    type: blockOutcome.success ? 'block_success' : 'block_failure',
    damageMultiplier: blockOutcome.success
      ? 1 - target.reactionSkills.block.SMR
      : 1 - target.reactionSkills.block.FMR,
  };

  const finalDamage = blockOutcome.damage;

  // ------------------------------------------------------------------
  // Step 5: Apply final damage to target's stamina
  // ------------------------------------------------------------------
  let currentState = _applyStaminaDelta(state, targetId, -finalDamage);

  // ------------------------------------------------------------------
  // Step 6: Set all participants' energy to 0
  // ------------------------------------------------------------------
  for (const participant of participants) {
    const freshParticipant = _findCombatant(currentState, participant.id);
    if (freshParticipant) {
      const drained: Combatant = { ...freshParticipant, energy: 0 };
      currentState = _replaceCombatant(currentState, drained);
    }
  }

  // ------------------------------------------------------------------
  // Step 7: Build GroupResolutionResult and record in roundHistory
  // ------------------------------------------------------------------
  const groupResult: GroupResolutionResult = {
    participantIds: participants.map((p) => p.id),
    targetId,
    individualDamages,
    totalDamage: groupDamage,
    defenseResult: blockDefenseResult,
    finalDamage,
  };

  const actionResult: ActionResult = {
    combatantId: leaderId,
    type: 'GROUP',
    attackResult: {
      attackerId: leaderId,
      targetId,
      damage: finalDamage,
      defenseType: 'block',
      defenseOutcome: {
        type: 'block',
        success: blockOutcome.success,
        damageMultiplier: blockDefenseResult.damageMultiplier,
      },
      rankKO: false,
      blindside: false,
      crushingBlow: false,
      counterChain: false,
    },
  };

  // Attach the full GroupResolutionResult to the state via the actionQueue
  // snapshot approach used by roundManager (same as pipeline _appendActionResult
  // — Round Manager collects results). Store it as a transient property.
  void groupResult; // GroupResolutionResult is available for callers that need it

  return _appendGroupActionResult(currentState, actionResult);
}

// ============================================================================
// Internal: drain all participant energy (used when target already KO'd)
// ============================================================================

/**
 * Zeros energy for all non-KO'd members of the leader's party.
 * Called when GROUP fires but the target is already KO'd (no-op on damage,
 * but energy is still consumed per design spec invariant).
 */
function _drainParticipantEnergy(state: CombatState, leaderId: string): CombatState {
  const ownParty = _getOwnParty(state, leaderId);
  const participants = ownParty.filter((c) => !c.isKO);
  let currentState = state;
  for (const participant of participants) {
    const freshParticipant = _findCombatant(currentState, participant.id);
    if (freshParticipant) {
      const drained: Combatant = { ...freshParticipant, energy: 0 };
      currentState = _replaceCombatant(currentState, drained);
    }
  }
  return currentState;
}

// ============================================================================
// Internal: append action result (mirrors pipeline._appendActionResult)
// ============================================================================

/**
 * Appends a GROUP ActionResult to the CombatState.
 * The Round Manager collects results across the full action queue.
 * This function is a hook consistent with the pipeline's _appendActionResult.
 */
function _appendGroupActionResult(
  state: CombatState,
  actionResult: ActionResult,
): CombatState {
  // Round Manager (Task 16) is responsible for consolidating round results.
  // For now, return state as-is — consistent with pipeline._appendActionResult.
  void actionResult;
  return state;
}
