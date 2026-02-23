/**
 * Ancient Order - GameState ↔ CombatState Synchronization
 *
 * Bidirectional sync between the independent GameState and CombatState objects
 * per ADR-013 (clean separation with explicit sync boundaries).
 *
 * Functions:
 *   initCombatState   — Create a CombatState from an EncounterConfig
 *   syncToGameState   — Apply combat results back to GameState
 *   endCombat         — Clear combatState and record the result
 *
 * All functions are pure: same inputs always produce the same output.
 * No mutations to input states.
 */

import type { GameState } from '../types/index.js';
import type {
  CombatState,
  Combatant,
  CombatantConfig,
  EncounterConfig,
} from '../types/combat.js';
import { ASCENSION_STARTING_SEGMENTS } from '../types/combat.js';
import { updateCombatState } from '../state/stateUpdaters.js';

// ============================================================================
// Internal Helpers
// ============================================================================

/**
 * Converts a CombatantConfig into a live Combatant object ready for combat.
 *
 * maxEnergy is set to the starting segments for the combatant's ascension level.
 * energy starts at the ascension-level starting value.
 * isKO starts as false.
 */
function _configToCombatant(config: CombatantConfig): Combatant {
  const ascensionLevel = config.ascensionLevel ?? 0;
  const startingEnergy = ASCENSION_STARTING_SEGMENTS[ascensionLevel] ?? 0;

  // maxEnergy reflects the number of segments available at the start of a round.
  // For simplicity, set maxEnergy to the starting segments unless they are 0,
  // in which case use a reasonable default of 3 segments.
  const maxEnergy = startingEnergy > 0 ? startingEnergy : 3;

  return {
    id: config.id,
    name: config.name,
    archetype: config.archetype,
    rank: config.rank,
    stamina: config.stamina,
    maxStamina: config.stamina,
    power: config.power,
    speed: config.speed,
    energy: startingEnergy,
    maxEnergy,
    ascensionLevel,
    activeBuffs: [],
    elementalPath: config.elementalPath,
    reactionSkills: config.reactionSkills,
    isKO: false,
  };
}

// ============================================================================
// Public API
// ============================================================================

/**
 * Creates a fresh CombatState for the given encounter.
 *
 * Both player and enemy parties are built entirely from the EncounterConfig —
 * encounter config defines the specific party composition and combat stats.
 * The resulting CombatState starts at round 1, phase AI_DECISION, with empty
 * action queue and history, and status 'active'.
 *
 * @param _gameState  Current game state (passed for future stat-sync extensibility)
 * @param encounter   Static encounter configuration with both party configs
 * @returns           A fresh CombatState ready to begin
 */
export function initCombatState(
  _gameState: Readonly<GameState>,
  encounter: Readonly<EncounterConfig>
): CombatState {
  const playerParty: Combatant[] = encounter.playerParty.map(_configToCombatant);
  const enemyParty: Combatant[] = encounter.enemyParty.map(_configToCombatant);

  return {
    round: 1,
    phase: 'AI_DECISION',
    playerParty,
    enemyParty,
    actionQueue: [],
    roundHistory: [],
    status: 'active',
  };
}

/**
 * Applies combat results back to GameState.
 *
 * Stamina changes from the active CombatState are synced back to the
 * GameState by storing the current CombatState on the gameState.combatState
 * field. Non-combat GameState fields (personality, NPCs, dialogue, etc.)
 * are never modified.
 *
 * @param gameState   Current game state
 * @param combatState Resolved combat state to sync back
 * @returns           New GameState with combatState updated; all other fields unchanged
 */
export function syncToGameState(
  gameState: Readonly<GameState>,
  combatState: Readonly<CombatState>
): GameState {
  // updateCombatState already creates a new GameState with combatState set
  // and all other fields preserved via spread.
  return updateCombatState(gameState, combatState);
}

/**
 * Clears the active combat and records the result in the GameState.
 *
 * Sets combatState to null and appends a record to the conversationLog
 * (reusing the existing log as a lightweight audit trail until a dedicated
 * combatHistory field is added in a future sprint).
 *
 * @param gameState  Current game state (must have an active combatState)
 * @param result     'victory' or 'defeat'
 * @returns          New GameState with combatState cleared
 */
export function endCombat(
  gameState: Readonly<GameState>,
  result: 'victory' | 'defeat'
): GameState {
  // Clear combatState from the game state
  const clearedState = updateCombatState(gameState, null);

  // Record the combat result in the conversation log as a lightweight
  // audit trail. A dedicated combatHistory field will be added in a
  // future sprint; for now this preserves the result without requiring
  // a GameState schema change.
  const resultEntry = {
    npcId: '_combat_system',
    nodeId: `combat_result_round_${gameState.combatState?.round ?? 0}`,
    optionId: result,
    timestamp: Date.now(),
  };

  return {
    ...clearedState,
    conversationLog: [...clearedState.conversationLog, resultEntry],
  };
}
