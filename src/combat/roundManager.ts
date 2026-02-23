/**
 * roundManager.ts — 5-Phase Round Orchestrator for Ancient Order.
 *
 * Coordinates a full combat round through 5 phases in sequence:
 *
 *   Phase 1 — AI Decision:  real behavior tree evaluator produces CombatAction for each
 *                           non-KO'd enemy. Falls back to stubBehaviorTree for unknown
 *                           archetypes (enables test fixtures with archetype: 'test').
 *   Phase 2 — Visual Info:  assemble public payload (stances, stamina, targeting) — no AI leakage
 *   Phase 3 — PC Declaration: validate player declarations; GROUP ally override applied
 *   Phase 4 — Action Queue: merge AI + player actions, sort via sortByPriority()
 *   Phase 5 — Per-Attack:   iterate sorted queue, call resolvePerAttack() for each action
 *
 * After Phase 5, victory/defeat conditions are checked and round result recorded.
 *
 * Task 19: stubBehaviorTree replaced by real evaluate() from behaviorTree/evaluator.ts.
 * GROUP: resolvePerAttack delegates to resolveGroup() from groupAction.ts (Task 18).
 * EvaluatorConfig: groupActionsEnabled set to true now that GROUP is fully implemented.
 *
 * Pure function: runRound(state, playerDeclarations, rollFn?) → CombatState
 * Roll injection: rollFn defaults to () => Math.random() * 20 (0–20 range).
 */

import type {
  CombatAction,
  CombatState,
  Combatant,
  RoundResult,
  ActionResult,
  EvaluatorConfig,
} from '../types/combat.js';
import { sortByPriority, resolvePerAttack } from './pipeline.js';
import { validateDeclaration } from './declaration.js';
import { evaluate } from './behaviorTree/evaluator.js';

// ============================================================================
// VisualInfo Type
// ============================================================================

/**
 * Phase 2 payload: public combat state visible to the player.
 * Intentionally excludes Phase 1 AI decisions (information asymmetry — ADR-008).
 */
export interface VisualInfo {
  readonly combatants: readonly {
    readonly id: string;
    readonly stamina: number;
    readonly staminaPct: number;
    readonly stance: string; // "active" or "KO"
    readonly targeting: string | null; // targetId from existing player declarations, null if unknown
  }[];
}

// ============================================================================
// Evaluator Config
// ============================================================================

/**
 * EvaluatorConfig passed to the behavior tree evaluator in Phase 1.
 * groupActionsEnabled is true now that GROUP is fully implemented (Task 18/19).
 */
const EVALUATOR_CONFIG: EvaluatorConfig = {
  groupActionsEnabled: true,
};

// ============================================================================
// AI Stub (kept for backward compatibility — used by existing unit tests)
// ============================================================================

/**
 * Fallback behavior tree stub: returns a simple ATTACK action targeting the
 * first non-KO'd player combatant. Used when the real evaluator cannot find an
 * archetype profile (e.g., combatants with archetype: 'test' in unit tests).
 *
 * @param combatant - The AI combatant making a decision
 * @param state     - Current CombatState (used to find valid targets)
 * @returns A CombatAction for the AI combatant
 */
export function stubBehaviorTree(combatant: Combatant, state: CombatState): CombatAction {
  // AI enemies attack the first non-KO'd player combatant
  const target = state.playerParty.find((c) => !c.isKO);

  return {
    combatantId: combatant.id,
    type: 'ATTACK',
    targetId: target?.id ?? null,
  };
}

// ============================================================================
// Phase 1: AI Decision
// ============================================================================

/**
 * Phase 1: Generate AI actions for all non-KO'd enemy combatants.
 *
 * Uses the real behavior tree evaluate() for each enemy. Falls back to
 * stubBehaviorTree() if no archetype profile is registered (enables test
 * fixtures that use archetype: 'test' to continue working).
 *
 * @param state - Current CombatState
 * @returns Array of CombatActions for all non-KO'd enemies
 */
function _runPhase1AIDecision(state: CombatState): CombatAction[] {
  return state.enemyParty
    .filter((c) => !c.isKO)
    .map((enemy) => {
      try {
        return evaluate(enemy, state, EVALUATOR_CONFIG);
      } catch {
        // Unknown archetype (e.g., 'test' in unit tests) — fall back to stub
        return stubBehaviorTree(enemy, state);
      }
    });
}

// ============================================================================
// Phase 2: Visual Info
// ============================================================================

/**
 * Phase 2: Assemble the VisualInfo payload from the current state.
 *
 * Includes all combatants (both parties) with stamina, staminaPct, and stance.
 * Does NOT include AI decisions from Phase 1 (information asymmetry — ADR-008).
 * Targeting shows declared player targets only (AI targeting is hidden).
 *
 * @param state              - Current CombatState
 * @param playerDeclarations - Already-validated player declarations (targeting visible)
 * @returns VisualInfo object for Phase 2 display
 */
export function buildVisualInfo(
  state: CombatState,
  playerDeclarations: CombatAction[],
): VisualInfo {
  const playerTargets = new Map<string, string | null>(
    playerDeclarations.map((a) => [a.combatantId, a.targetId]),
  );

  const allCombatants = [...state.playerParty, ...state.enemyParty];

  const combatants = allCombatants.map((c) => ({
    id: c.id,
    stamina: c.stamina,
    staminaPct: c.maxStamina > 0 ? c.stamina / c.maxStamina : 0,
    stance: c.isKO ? 'KO' : 'active',
    targeting: playerTargets.get(c.id) ?? null,
  }));

  return { combatants };
}

// ============================================================================
// Phase 3: PC Declaration Validation
// ============================================================================

/**
 * Phase 3 result: validated player actions plus a list of overridden combatant IDs.
 * When a GROUP declaration is accepted, all other allies' declarations are removed
 * (they participate implicitly through GROUP).
 */
interface Phase3Result {
  readonly validatedActions: CombatAction[];
  readonly overriddenIds: Set<string>;
}

/**
 * Phase 3: Validate player declarations and apply GROUP ally override.
 *
 * For each player declaration:
 *  - Run validateDeclaration(). Invalid declarations are replaced with the
 *    fallback action if one exists, or dropped entirely.
 *  - If a GROUP declaration passes, remove all other ally declarations from
 *    the queue (they participate through GROUP, not individually).
 *
 * @param state              - Current CombatState
 * @param playerDeclarations - Raw player-submitted declarations
 * @returns Phase3Result with validated actions and set of overridden combatant IDs
 */
function _runPhase3PCDeclaration(
  state: CombatState,
  playerDeclarations: CombatAction[],
): Phase3Result {
  const validatedActions: CombatAction[] = [];
  let groupLeaderId: string | null = null;

  for (const declaration of playerDeclarations) {
    const result = validateDeclaration(state, declaration);

    if (result.valid) {
      validatedActions.push(declaration);
      // Track if a GROUP declaration was accepted
      if (declaration.type === 'GROUP') {
        groupLeaderId = declaration.combatantId;
      }
    } else if (!result.valid && result.fallback !== undefined) {
      // Use the fallback action (e.g., GROUP → ATTACK on same target)
      validatedActions.push(result.fallback);
    }
    // If invalid with no fallback, the declaration is dropped
  }

  // GROUP ally override: if GROUP was accepted, remove all other ally declarations
  const overriddenIds = new Set<string>();
  if (groupLeaderId !== null) {
    const leaderId = groupLeaderId;
    // Find the leader's party
    const leaderParty = state.playerParty.some((c) => c.id === leaderId)
      ? state.playerParty
      : state.enemyParty;

    // All non-KO'd allies (excluding the leader) are overridden
    for (const ally of leaderParty) {
      if (!ally.isKO && ally.id !== leaderId) {
        overriddenIds.add(ally.id);
      }
    }

    // Remove overridden allies from validatedActions
    const filtered = validatedActions.filter((a) => !overriddenIds.has(a.combatantId));
    return { validatedActions: filtered, overriddenIds };
  }

  return { validatedActions, overriddenIds };
}

// ============================================================================
// Phase 5: Per-Attack Execution
// ============================================================================

/**
 * Phase 5: Iterate the sorted action queue, resolve each action through the
 * pipeline, and accumulate state changes.
 *
 * Each call to resolvePerAttack() produces a new CombatState. Actions against
 * already-KO'd targets are passed through (pipeline handles gracefully).
 *
 * Also collects ActionResults to build the round's RoundResult entry.
 *
 * @param state      - CombatState after Phase 4 (with actionQueue populated)
 * @param rollFn     - Roll injection function
 * @returns Updated CombatState after all actions resolve
 */
function _runPhase5PerAttack(
  state: CombatState,
  rollFn: () => number,
): { finalState: CombatState; actionResults: ActionResult[] } {
  const actionResults: ActionResult[] = [];
  let currentState = state;

  for (const action of state.actionQueue) {
    const previousState = currentState;
    currentState = resolvePerAttack(currentState, action, rollFn);

    // Record a minimal ActionResult for each action processed
    // (pipeline's _appendActionResult is a stub; we collect here)
    const actionResult: ActionResult = {
      combatantId: action.combatantId,
      type: action.type,
    };

    // Detect if an attack produced an AttackResult by checking stamina deltas
    // The pipeline builds AttackResult internally but doesn't return it separately.
    // We capture what we can observe from state changes.
    if (action.type === 'ATTACK' || action.type === 'SPECIAL') {
      const targetId = action.targetId;
      if (targetId !== null) {
        const targetBefore = _findCombatantInState(previousState, targetId);
        const targetAfter = _findCombatantInState(currentState, targetId);
        if (targetBefore && targetAfter) {
          const damage = Math.max(0, targetBefore.stamina - targetAfter.stamina);
          const rankKO = !targetBefore.isKO && targetAfter.isKO && targetBefore.stamina > 0;

          // We don't have full AttackResult data from the pipeline stub,
          // but we record what we can observe for the round history.
          actionResults.push({
            ...actionResult,
            attackResult: {
              attackerId: action.combatantId,
              targetId,
              damage,
              defenseType: 'block', // best-effort; pipeline resolves the real type
              defenseOutcome: {
                type: 'block',
                success: damage < targetBefore.stamina,
                damageMultiplier: 1,
              },
              rankKO,
              blindside: false,
              crushingBlow: false,
              counterChain: false,
            },
          });
          continue;
        }
      }
    }

    actionResults.push(actionResult);
  }

  return { finalState: currentState, actionResults };
}

// ============================================================================
// Victory/Defeat Check
// ============================================================================

/**
 * Checks win/loss conditions after all actions resolve.
 * All enemies KO'd → 'victory'; all players KO'd → 'defeat'; otherwise 'active'.
 *
 * If both sides are KO'd simultaneously, 'defeat' takes priority.
 */
function _checkVictoryDefeat(
  state: CombatState,
): 'active' | 'victory' | 'defeat' {
  const allPlayersKO = state.playerParty.every((c) => c.isKO);
  const allEnemiesKO = state.enemyParty.every((c) => c.isKO);

  if (allPlayersKO) return 'defeat';
  if (allEnemiesKO) return 'victory';
  return 'active';
}

// ============================================================================
// Utility
// ============================================================================

/**
 * Finds a combatant by ID across both parties.
 */
function _findCombatantInState(state: CombatState, id: string): Combatant | undefined {
  return (
    state.playerParty.find((c) => c.id === id) ??
    state.enemyParty.find((c) => c.id === id)
  );
}

// ============================================================================
// Main: runRound
// ============================================================================

/**
 * Executes a complete 5-phase combat round.
 *
 * Phase 1: AI decisions (stub behavior tree for each non-KO'd enemy)
 * Phase 2: Assemble visual info (no AI leakage)
 * Phase 3: Validate player declarations; GROUP ally override
 * Phase 4: Merge AI + player actions, sort by priority
 * Phase 5: Resolve each action through the per-attack pipeline
 *
 * After Phase 5: check victory/defeat, record round in roundHistory.
 *
 * @param state              - Current CombatState (round N before resolution)
 * @param playerDeclarations - Raw player-submitted action declarations
 * @param rollFn             - Optional roll injection (default: Math.random() * 20)
 * @returns New CombatState after the round resolves
 */
export function runRound(
  state: CombatState,
  playerDeclarations: CombatAction[],
  rollFn: () => number = () => Math.random() * 20,
): CombatState {
  // ------------------------------------------------------------------
  // Phase 1: AI Decision
  // ------------------------------------------------------------------
  const aiActions = _runPhase1AIDecision(state);

  // ------------------------------------------------------------------
  // Phase 2: Visual Info
  // ------------------------------------------------------------------
  // Build VisualInfo from current state + raw player declarations (pre-validation).
  // This is assembled but not mutated into state — it's a read-only payload for
  // the API layer. Phase 2 is about information exposure, not state change.
  const _visualInfo = buildVisualInfo(state, playerDeclarations);
  void _visualInfo; // Available for API layer; not stored in CombatState

  // ------------------------------------------------------------------
  // Phase 3: PC Declaration Validation
  // ------------------------------------------------------------------
  const { validatedActions } = _runPhase3PCDeclaration(state, playerDeclarations);

  // ------------------------------------------------------------------
  // Phase 4: Action Queue — merge and sort
  // ------------------------------------------------------------------
  const allActions = [...aiActions, ...validatedActions];
  const sortedActions = sortByPriority(allActions, state, rollFn);

  // Populate the actionQueue in state before Phase 5
  // (pipeline's DEFEND intercept reads from state.actionQueue)
  const stateWithQueue: CombatState = {
    ...state,
    phase: 'PER_ATTACK',
    actionQueue: sortedActions,
  };

  // ------------------------------------------------------------------
  // Phase 5: Per-Attack Resolution
  // ------------------------------------------------------------------
  const { finalState, actionResults } = _runPhase5PerAttack(stateWithQueue, rollFn);

  // ------------------------------------------------------------------
  // Post-round: Victory/Defeat check
  // ------------------------------------------------------------------
  const newStatus = _checkVictoryDefeat(finalState);

  // ------------------------------------------------------------------
  // Record round result in roundHistory
  // ------------------------------------------------------------------
  const roundResult: RoundResult = {
    round: state.round,
    actions: actionResults,
    stateSnapshot: { ...finalState, status: newStatus },
  };

  const updatedRoundHistory = [...finalState.roundHistory, roundResult];

  // Return the final state for this round, advancing round counter
  return {
    ...finalState,
    round: state.round + 1,
    phase: 'AI_DECISION', // reset to Phase 1 for next round
    actionQueue: [], // cleared after resolution
    roundHistory: updatedRoundHistory,
    status: newStatus,
  };
}
