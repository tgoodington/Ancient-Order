/**
 * Ancient Order - Game State Factory
 *
 * Creates initial game state and provides state creation utilities.
 */

import { v4 as uuidv4 } from 'uuid';
import {
  GameState,
  PlayerCharacter,
  NPC,
  Personality,
  NewGameRequest
} from '../types';
import { createDefaultPersonality } from '../personality/personalitySystem';
import { createNPCs } from './npcs';

const GAME_VERSION = '0.1.0';

/**
 * Creates a new player character with default values.
 */
export function createPlayerCharacter(name: string): PlayerCharacter {
  return {
    id: 'player_1',
    name,
    title: 'Seeker',
    currentRank: 1.0,

    stamina: { current: 100, max: 100 },
    power: 15,
    speed: 12,

    personality: {
      patience: 18,
      empathy: 16,
      cunning: 12,
      logic: 18,
      kindness: 18,
      charisma: 18,
    },

    coreTrainingPoints: 0,
    reactionSkills: { block: 0, dodge: 0, parry: 0 },

    elementalPath: 'Light',
    pathSegments: { current: 0, max: 35 },
    pathAscensionLevel: 0,

    team: [],
  };
}

/**
 * Creates a new game state with all initial data.
 */
export function createNewGameState(request: NewGameRequest): GameState {
  const player = createPlayerCharacter(request.playerName);
  const npcs = createNPCs();

  return {
    id: `game_${uuidv4()}`,
    timestamp: Date.now(),
    version: GAME_VERSION,

    currentLocation: 'Harbor',
    conversationLog: [],

    player,
    npcs,

    activeCombat: null,
    combatHistory: [],

    questFlags: {},
    relationshipFlags: {},
  };
}

/**
 * Gets the current game version.
 */
export function getGameVersion(): string {
  return GAME_VERSION;
}
