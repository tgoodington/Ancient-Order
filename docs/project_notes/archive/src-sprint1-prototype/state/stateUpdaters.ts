/**
 * Ancient Order - Immutable State Updaters
 *
 * All functions create new state objects without mutating the original.
 * This ensures predictable state management and reliable save/load.
 */

import {
  GameState,
  PlayerCharacter,
  NPC,
  Personality,
  ConversationEntry,
  PersonalityAdjustment,
} from '../types';
import { adjustPersonality } from '../personality/personalitySystem';

/**
 * Updates the timestamp on a game state.
 */
export function updateTimestamp(state: GameState): GameState {
  return {
    ...state,
    timestamp: Date.now(),
  };
}

/**
 * Updates the player's personality.
 */
export function updatePlayerPersonality(
  state: GameState,
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
 * Applies a personality adjustment to the player.
 */
export function applyPersonalityAdjustment(
  state: GameState,
  adjustment: PersonalityAdjustment
): GameState {
  const newPersonality = adjustPersonality(state.player.personality, adjustment);
  return updatePlayerPersonality(state, newPersonality);
}

/**
 * Updates an NPC's affection value.
 */
export function updateNPCAffection(
  state: GameState,
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
 * Updates an NPC's trust value.
 */
export function updateNPCTrust(
  state: GameState,
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
 * Updates both affection and trust for an NPC.
 */
export function updateNPCRelationship(
  state: GameState,
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

/**
 * Adds a conversation entry to the log.
 */
export function addConversationEntry(
  state: GameState,
  entry: ConversationEntry
): GameState {
  return updateTimestamp({
    ...state,
    conversationLog: [...state.conversationLog, entry],
  });
}

/**
 * Updates the player's current location.
 */
export function updateLocation(
  state: GameState,
  newLocation: string
): GameState {
  return updateTimestamp({
    ...state,
    currentLocation: newLocation,
  });
}

/**
 * Updates the player's team composition.
 */
export function updateTeam(
  state: GameState,
  npcIds: string[]
): GameState {
  return updateTimestamp({
    ...state,
    player: {
      ...state.player,
      team: [...npcIds],
    },
  });
}

/**
 * Sets a quest flag.
 */
export function setQuestFlag(
  state: GameState,
  flag: string,
  value: boolean
): GameState {
  return updateTimestamp({
    ...state,
    questFlags: {
      ...state.questFlags,
      [flag]: value,
    },
  });
}

/**
 * Sets a relationship flag.
 */
export function setRelationshipFlag(
  state: GameState,
  flag: string,
  value: boolean
): GameState {
  return updateTimestamp({
    ...state,
    relationshipFlags: {
      ...state.relationshipFlags,
      [flag]: value,
    },
  });
}

/**
 * Processes a dialogue choice and returns updated state.
 * This is the main function for handling dialogue interactions.
 */
export function processDialogueChoice(
  state: GameState,
  npcId: string,
  nodeId: string,
  optionId: string,
  optionText: string,
  personalityAdjustment: PersonalityAdjustment,
  affectionChange: number,
  trustChange: number
): GameState {
  const personalityBefore = { ...state.player.personality };

  // Apply personality adjustment
  let newState = applyPersonalityAdjustment(state, personalityAdjustment);

  // Update NPC relationship
  newState = updateNPCRelationship(newState, npcId, affectionChange, trustChange);

  // Create conversation entry
  const entry: ConversationEntry = {
    timestamp: Date.now(),
    npcId,
    nodeId,
    optionChosen: {
      id: optionId,
      text: optionText,
    },
    personalityBefore,
    personalityAfter: { ...newState.player.personality },
    affectionChange,
    trustChange,
  };

  // Add to conversation log
  newState = addConversationEntry(newState, entry);

  return newState;
}

/**
 * Updates player stamina.
 */
export function updatePlayerStamina(
  state: GameState,
  current: number,
  max?: number
): GameState {
  return updateTimestamp({
    ...state,
    player: {
      ...state.player,
      stamina: {
        current: Math.max(0, Math.min(current, max ?? state.player.stamina.max)),
        max: max ?? state.player.stamina.max,
      },
    },
  });
}

/**
 * Updates player stats (power, speed).
 */
export function updatePlayerStats(
  state: GameState,
  updates: { power?: number; speed?: number }
): GameState {
  return updateTimestamp({
    ...state,
    player: {
      ...state.player,
      power: updates.power ?? state.player.power,
      speed: updates.speed ?? state.player.speed,
    },
  });
}
