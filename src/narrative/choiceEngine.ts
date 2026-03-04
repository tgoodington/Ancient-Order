/**
 * Ancient Order - Choice & Consequence Engine
 *
 * Validates player choices against scene gates and applies consequences
 * to GameState by reusing existing state updaters. [D2]
 *
 * Functions:
 *   validateChoice      — check if a choice is available given personality
 *   applyConsequence    — apply a choice's consequence to GameState
 *   processSceneChoice  — validate + apply in one call
 */

import type { GameState, Personality } from '../types/index.js';
import type {
  Scene,
  SceneChoice,
  ChoiceConsequence,
} from '../types/narrative.js';
import {
  applyPersonalityAdjustment,
  updateNPCRelationship,
} from '../state/stateUpdaters.js';
import { evaluateScenePersonalityGate } from './sceneEngine.js';

// ============================================================================
// Choice Validation
// ============================================================================

/**
 * Validates whether a choice is available to the player.
 *
 * 1. Finds the choice in scene.choices by choiceId.
 * 2. If not found: returns invalid with CHOICE_NOT_FOUND.
 * 3. If choice has a gate, evaluates it against personality.
 * 4. If gate fails: returns invalid with CHOICE_NOT_AVAILABLE.
 * 5. Otherwise: returns valid with the choice.
 *
 * @param scene     - The current scene containing choices
 * @param choiceId  - ID of the choice to validate
 * @param personality - The player's current personality
 * @returns Validation result with choice on success or error code on failure
 */
export function validateChoice(
  scene: Scene,
  choiceId: string,
  personality: Readonly<Personality>
): { valid: true; choice: SceneChoice } | { valid: false; code: string; message: string } {
  const choice = scene.choices.find(c => c.id === choiceId);

  if (choice === undefined) {
    return {
      valid: false,
      code: 'CHOICE_NOT_FOUND',
      message: `Choice "${choiceId}" not found in scene "${scene.id}"`,
    };
  }

  if (choice.gate !== undefined) {
    const gatePass = evaluateScenePersonalityGate(choice.gate, personality);
    if (!gatePass) {
      return {
        valid: false,
        code: 'CHOICE_NOT_AVAILABLE',
        message: `Choice "${choiceId}" is not available`,
      };
    }
  }

  return { valid: true, choice };
}

// ============================================================================
// Consequence Application
// ============================================================================

/**
 * Applies a choice consequence to GameState by reusing existing state updaters. [D2]
 *
 * Effect application order (mirrors processDialogueChoice pattern [RF-2]):
 *   1. Personality effect: calls applyPersonalityAdjustment if effect has keys
 *   2. NPC effects: calls updateNPCRelationship for each NPC effect entry
 *
 * Flag updates are handled separately by the state machine (flags are stored
 * on NarrativeState, not directly on GameState).
 *
 * Silent handling: if consequence has no effects, returns input state
 * spread-copied. [A6]
 *
 * @param state       - Current game state
 * @param consequence - The consequence to apply
 * @returns Updated GameState with personality and NPC relationship changes applied
 */
export function applyConsequence(
  state: Readonly<GameState>,
  consequence: ChoiceConsequence
): GameState {
  let newState: GameState = { ...state };

  // Step 1: apply personality effect if present and has keys
  if (
    consequence.personalityEffect !== undefined &&
    Object.keys(consequence.personalityEffect).length > 0
  ) {
    newState = applyPersonalityAdjustment(
      newState,
      consequence.personalityEffect as import('../types/index.js').PersonalityAdjustment
    );
  }

  // Step 2: apply NPC effects if present
  if (consequence.npcEffects !== undefined) {
    for (const npcEffect of consequence.npcEffects) {
      newState = updateNPCRelationship(
        newState,
        npcEffect.npcId,
        npcEffect.affectionChange ?? 0,
        npcEffect.trustChange ?? 0
      );
    }
  }

  return newState;
}

// ============================================================================
// Combined Choice Processing
// ============================================================================

/**
 * Validates a choice and applies its consequence to GameState.
 *
 * 1. Calls validateChoice(scene, choiceId, personality).
 * 2. If invalid: returns the error.
 * 3. If valid: calls applyConsequence(state, choice.consequence ?? {}).
 * 4. Returns { type: 'success', state: updatedState, choice }.
 *
 * @param state    - Current game state
 * @param scene    - The current scene
 * @param choiceId - ID of the choice to process
 * @returns Success with updated state and choice, or error with code
 */
export function processSceneChoice(
  state: Readonly<GameState>,
  scene: Scene,
  choiceId: string
): { type: 'success'; state: GameState; choice: SceneChoice }
 | { type: 'error'; code: string; message: string } {
  const validation = validateChoice(scene, choiceId, state.player.personality);

  if (!validation.valid) {
    return {
      type: 'error',
      code: validation.code,
      message: validation.message,
    };
  }

  const updatedState = applyConsequence(state, validation.choice.consequence ?? {});

  return {
    type: 'success',
    state: updatedState,
    choice: validation.choice,
  };
}
