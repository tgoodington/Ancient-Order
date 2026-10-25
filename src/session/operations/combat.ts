import { ErrorCodes } from '../../types/index.js';
import type { CombatAction, CombatState } from '../../types/combat.js';
import { endCombat, initCombatState, syncToGameState } from '../../combat/sync.js';
import { buildVisualInfo, runRound } from '../../combat/roundManager.js';
import type { VisualInfo } from '../../combat/roundManager.js';
import { validateDeclaration } from '../../combat/declaration.js';
import type { SessionContent } from '../content.js';
import { fail, fromThrown, ok } from '../result.js';
import type { SessionState } from '../sessionState.js';
import { validateCombatActions, validateIdString } from '../validation.js';
import { noActiveGame, refuse } from './outcome.js';
import type { Outcome } from './outcome.js';

export interface StartEncounterResult {
  encounterId: string;
  round: number;
  status: string;
}

export interface DeclarationEntry {
  combatantId: string;
  type: string;
  targetId: string | null;
  valid: boolean;
  error?: string;
}

export interface DeclareActionsResult {
  accepted: DeclarationEntry[];
  rejected: DeclarationEntry[];
}

export type AdvanceRoundResult = VisualInfo & { round: number; status: string };

export interface CombatResultView {
  status: 'active' | 'victory' | 'defeat';
  round: number;
}

function noActiveCombat<T>(s: SessionState): Outcome<T> {
  return refuse(s, fail(ErrorCodes.NO_ACTIVE_COMBAT, 'No active combat encounter'));
}

function checkPlayerOrder(combatState: CombatState, action: CombatAction): string | null {
  const member = combatState.playerParty.find((c) => c.id === action.combatantId);
  if (member === undefined) {
    return `Combatant '${action.combatantId}' is not in the player's party`;
  }
  if (action.energySegments !== undefined) {
    if (action.type !== 'SPECIAL') return 'energySegments is only allowed for SPECIAL';
    const n = action.energySegments;
    if (!Number.isInteger(n) || n < 1 || n > member.energy) {
      return `energySegments must be an integer between 1 and ${member.energy}`;
    }
  }
  return null;
}

export function startEncounter(
  s: SessionState,
  content: SessionContent,
  encounterId: unknown
): Outcome<StartEncounterResult> {
  const invalid = validateIdString('encounterId', encounterId, undefined);
  if (invalid) return refuse(s, { ok: false, error: invalid });
  if (s.game === null) return noActiveGame(s);
  const id = encounterId as string;
  try {
    // Own-property check: ids like 'constructor' or '__proto__' must not resolve.
    const encounter = Object.hasOwn(content.encounters, id) ? content.encounters[id] : undefined;
    if (encounter === undefined) {
      return refuse(
        s,
        fail(ErrorCodes.ENCOUNTER_NOT_FOUND, `Encounter '${id}' not found in registry`)
      );
    }
    if (s.game.combatState !== null) {
      return refuse(
        s,
        fail(ErrorCodes.COMBAT_ALREADY_ACTIVE, 'A combat encounter is already active')
      );
    }
    const combatState = initCombatState(s.game, encounter);
    const game = syncToGameState(s.game, combatState);
    return {
      next: { ...s, game, pendingDeclarations: [], lastCombatOutcome: null },
      result: ok({
        encounterId: encounter.id,
        round: combatState.round,
        status: combatState.status,
      }),
    };
  } catch (err) {
    return refuse(s, fromThrown(err));
  }
}

export function declareActions(
  s: SessionState,
  _content: SessionContent,
  actions: unknown
): Outcome<DeclareActionsResult> {
  const invalid = validateCombatActions(actions);
  if (invalid) return refuse(s, { ok: false, error: invalid });
  if (s.game === null) return noActiveGame(s);
  const combatState = s.game.combatState ?? null;
  if (combatState === null) return noActiveCombat(s);
  try {
    const input = actions as CombatAction[];
    const accepted: DeclarationEntry[] = [];
    const rejected: DeclarationEntry[] = [];
    const validatedForStorage: CombatAction[] = [];

    for (const action of input) {
      // Ensure targetId is null (not undefined) when absent
      const normalizedAction: CombatAction = {
        combatantId: action.combatantId,
        type: action.type,
        targetId: action.targetId ?? null,
        ...(action.energySegments !== undefined ? { energySegments: action.energySegments } : {}),
      };

      // Designer rules (2026-10-24): player-party only, bounded integer SPECIAL energy.
      const preError = checkPlayerOrder(combatState, normalizedAction);
      if (preError !== null) {
        rejected.push({
          combatantId: normalizedAction.combatantId,
          type: normalizedAction.type,
          targetId: normalizedAction.targetId,
          valid: false,
          error: preError,
        });
        continue;
      }

      const result = validateDeclaration(combatState, normalizedAction);

      if (result.valid) {
        accepted.push({
          combatantId: normalizedAction.combatantId,
          type: normalizedAction.type,
          targetId: normalizedAction.targetId,
          valid: true,
        });
        validatedForStorage.push(normalizedAction);
      } else {
        rejected.push({
          combatantId: normalizedAction.combatantId,
          type: normalizedAction.type,
          targetId: normalizedAction.targetId,
          valid: false,
          error: result.error,
        });

        // If a fallback action was provided (e.g., GROUP -> ATTACK), store that instead
        if (result.fallback !== undefined) {
          validatedForStorage.push(result.fallback);
          accepted.push({
            combatantId: result.fallback.combatantId,
            type: result.fallback.type,
            targetId: result.fallback.targetId,
            valid: true,
          });
        }
      }
    }

    // Every submitted action rejected with no fallbacks: refuse and keep previous orders.
    if (input.length > 0 && accepted.length === 0) {
      return refuse(
        s,
        fail(ErrorCodes.COMBAT_INVALID_DECLARATION, 'All declarations were invalid', { rejected })
      );
    }

    // Replace (not append): each declaration is authoritative. An empty array clears.
    return {
      next: { ...s, pendingDeclarations: validatedForStorage },
      result: ok({ accepted, rejected }),
    };
  } catch (err) {
    return refuse(s, fromThrown(err));
  }
}

export function advanceRound(
  s: SessionState,
  _content: SessionContent
): Outcome<AdvanceRoundResult> {
  if (s.game === null) return noActiveGame(s);
  const combatState = s.game.combatState ?? null;
  if (combatState === null) return noActiveCombat(s);
  try {
    const resolved = runRound(combatState, [...s.pendingDeclarations]);
    let game = syncToGameState(s.game, resolved);
    let lastCombatOutcome = s.lastCombatOutcome;
    if (resolved.status !== 'active') {
      game = endCombat(game, resolved.status);
      lastCombatOutcome = { status: resolved.status, round: resolved.round };
    }
    const visualInfo = buildVisualInfo(resolved, []);
    return {
      next: { ...s, game, pendingDeclarations: [], lastCombatOutcome },
      result: ok({ ...visualInfo, round: resolved.round, status: resolved.status }),
    };
  } catch (err) {
    return refuse(s, fromThrown(err));
  }
}

export function getCombatView(s: SessionState, _content: SessionContent): Outcome<VisualInfo> {
  if (s.game === null) return noActiveGame(s);
  const combatState = s.game.combatState ?? null;
  if (combatState === null) return noActiveCombat(s);
  try {
    // Enemy decisions stay hidden: that logic lives inside buildVisualInfo.
    return { next: s, result: ok(buildVisualInfo(combatState, [...s.pendingDeclarations])) };
  } catch (err) {
    return refuse(s, fromThrown(err));
  }
}

export function getRoundHistory(
  s: SessionState,
  _content: SessionContent
): Outcome<CombatState['roundHistory']> {
  if (s.game === null) return noActiveGame(s);
  const combatState = s.game.combatState ?? null;
  if (combatState === null) return noActiveCombat(s);
  return { next: s, result: ok(combatState.roundHistory) };
}

export function getCombatResult(
  s: SessionState,
  _content: SessionContent
): Outcome<CombatResultView> {
  if (s.game === null) return noActiveGame(s);
  const combatState = s.game.combatState ?? null;
  if (combatState !== null) {
    return { next: s, result: ok({ status: 'active', round: combatState.round }) };
  }
  if (s.lastCombatOutcome !== null) {
    return { next: s, result: ok(s.lastCombatOutcome) };
  }
  return noActiveCombat(s);
}
