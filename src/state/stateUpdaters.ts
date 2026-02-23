/**
 * Ancient Order - Immutable State Updaters
 *
 * All functions create new state objects without mutating the original.
 * Every updater wraps its result with updateTimestamp() to keep the timestamp
 * current after any state change.
 *
 * Function signature pattern: (state: Readonly<GameState>, ...) => GameState
 */

import {
  GameState,
  Personality,
  ConversationEntry,
  PersonalityAdjustment,
  DialogueOption,
} from '../types/index.js';
import type { CombatState } from '../types/combat.js';
import { adjustPersonality } from '../personality/personalitySystem.js';

// ============================================================================
// Timestamp
// ============================================================================

/**
 * Returns a new GameState with the timestamp updated to now.
 * This is the base wrapper called by every other updater.
 */
export function updateTimestamp(state: Readonly<GameState>): GameState {
  return {
    ...state,
    timestamp: Date.now(),
  };
}

// ============================================================================
// Player Updaters
// ============================================================================

/**
 * Returns a new GameState with the player's personality replaced.
 */
export function updatePlayerPersonality(
  state: Readonly<GameState>,
  newPersonality: Personality
): GameState {
  return updateTimestamp({
    ...state,
    player: {
      ...state.player,
      personality: newPersonality,
    },
  });
}

/**
 * Applies a partial personality adjustment to the player.
 * Calls adjustPersonality() to enforce the [5, 35] range and 100% sum invariant,
 * then delegates to updatePlayerPersonality().
 */
export function applyPersonalityAdjustment(
  state: Readonly<GameState>,
  adjustment: PersonalityAdjustment
): GameState {
  const newPersonality = adjustPersonality(state.player.personality, adjustment);
  return updatePlayerPersonality(state, newPersonality);
}

// ============================================================================
// NPC Relationship Updaters
// ============================================================================

/**
 * Returns a new GameState with the specified NPC's affection changed by `change`.
 * Affection is clamped to [-100, +100]. Returns state unchanged if NPC not found.
 */
export function updateNPCAffection(
  state: Readonly<GameState>,
  npcId: string,
  change: number
): GameState {
  const npc = state.npcs[npcId];
  if (!npc) return state;

  const newAffection = Math.max(-100, Math.min(100, npc.affection + change));

  return updateTimestamp({
    ...state,
    npcs: {
      ...state.npcs,
      [npcId]: {
        ...npc,
        affection: newAffection,
      },
    },
  });
}

/**
 * Returns a new GameState with the specified NPC's trust changed by `change`.
 * Trust is clamped to [-100, +100]. Returns state unchanged if NPC not found.
 */
export function updateNPCTrust(
  state: Readonly<GameState>,
  npcId: string,
  change: number
): GameState {
  const npc = state.npcs[npcId];
  if (!npc) return state;

  const newTrust = Math.max(-100, Math.min(100, npc.trust + change));

  return updateTimestamp({
    ...state,
    npcs: {
      ...state.npcs,
      [npcId]: {
        ...npc,
        trust: newTrust,
      },
    },
  });
}

/**
 * Compound updater: changes both affection and trust for an NPC in a single
 * state transition. Both values are clamped to [-100, +100].
 * Returns state unchanged if NPC not found.
 */
export function updateNPCRelationship(
  state: Readonly<GameState>,
  npcId: string,
  affectionChange: number,
  trustChange: number
): GameState {
  const npc = state.npcs[npcId];
  if (!npc) return state;

  const newAffection = Math.max(-100, Math.min(100, npc.affection + affectionChange));
  const newTrust = Math.max(-100, Math.min(100, npc.trust + trustChange));

  return updateTimestamp({
    ...state,
    npcs: {
      ...state.npcs,
      [npcId]: {
        ...npc,
        affection: newAffection,
        trust: newTrust,
      },
    },
  });
}

// ============================================================================
// Conversation Log
// ============================================================================

/**
 * Appends a ConversationEntry to the conversation log via spread (immutable).
 */
export function addConversationEntry(
  state: Readonly<GameState>,
  entry: ConversationEntry
): GameState {
  return updateTimestamp({
    ...state,
    conversationLog: [...state.conversationLog, entry],
  });
}

// ============================================================================
// Dialogue Processing
// ============================================================================

/**
 * Master function composing personality adjustment + NPC relationship update
 * + conversation log entry for a single dialogue choice.
 *
 * Applies (in order):
 *   1. Personality adjustment from option.personalityAdjustment (if any)
 *   2. NPC affection/trust changes from option.npcAdjustment (if any)
 *   3. ConversationEntry appended to conversationLog
 *
 * A single updateTimestamp() is applied at the end via addConversationEntry().
 * Intermediate state transitions call updateTimestamp() internally, which is
 * acceptable — the final timestamp is the authoritative one.
 */
export function processDialogueChoice(
  state: Readonly<GameState>,
  npcId: string,
  nodeId: string,
  optionId: string,
  option: DialogueOption
): GameState {
  // Step 1: apply personality adjustment if the option has one
  let newState: GameState =
    option.personalityAdjustment && Object.keys(option.personalityAdjustment).length > 0
      ? applyPersonalityAdjustment(state, option.personalityAdjustment)
      : { ...state };

  // Step 2: apply NPC relationship changes if the option has them
  if (option.npcAdjustment) {
    const { affectionChange = 0, trustChange = 0 } = option.npcAdjustment;
    if (affectionChange !== 0 || trustChange !== 0) {
      newState = updateNPCRelationship(newState, npcId, affectionChange, trustChange);
    }
  }

  // Step 3: record the choice in the conversation log
  const entry: ConversationEntry = {
    npcId,
    nodeId,
    optionId,
    timestamp: Date.now(),
  };

  return addConversationEntry(newState, entry);
}

// ============================================================================
// Combat State
// ============================================================================

/**
 * Sets or clears the combatState field on GameState.
 * Pass `null` to clear an active combat; pass a CombatState to start one.
 */
export function updateCombatState(
  state: Readonly<GameState>,
  combatState: CombatState | null
): GameState {
  return updateTimestamp({
    ...state,
    combatState,
  });
}
