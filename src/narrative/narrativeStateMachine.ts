/**
 * Ancient Order - Narrative State Machine
 *
 * Thin orchestrator managing scene transitions and NarrativeState bookkeeping.
 * Returns a discriminated union result type (NarrativeTransitionResult). [D4]
 *
 * Functions:
 *   createInitialNarrativeState — factory for fresh NarrativeState
 *   advanceNarrative            — process a choice and transition to next scene
 *   applyFlags                  — internal: apply setFlags/clearFlags to flag map
 */

import type { GameState } from '../types/index.js';
import type {
  Scene,
  SceneGraph,
  NarrativeState,
  NarrativeTransitionResult,
  SceneHistoryEntry,
  ChoiceConsequence,
} from '../types/narrative.js';
import { findScene, evaluateAllPrerequisites } from './sceneEngine.js';
import { processSceneChoice } from './choiceEngine.js';

// ============================================================================
// Initial State Factory
// ============================================================================

/**
 * Creates a fresh NarrativeState for the given starting scene.
 *
 * The starting scene is added to visitedSceneIds immediately.
 * choiceFlags and sceneHistory start empty.
 *
 * @param startingSceneId - The ID of the first scene to load
 * @returns A new NarrativeState ready for the first scene
 */
export function createInitialNarrativeState(startingSceneId: string): NarrativeState {
  return {
    currentSceneId: startingSceneId,
    visitedSceneIds: [startingSceneId],
    choiceFlags: {},
    sceneHistory: [],
  };
}

// ============================================================================
// Scene Transition
// ============================================================================

/**
 * Advances the narrative by processing a player choice and transitioning
 * to the next scene.
 *
 * Steps:
 *   1. Guard: narrativeState must not be null.
 *   2. Find current scene in graph.
 *   3. Process choice (validate + apply consequence).
 *   4. Resolve next scene (null = narrative ends).
 *   5. Validate next scene prerequisites against updated state.
 *   6. Build updated NarrativeState.
 *   7. Return NarrativeTransitionSuccess.
 *
 * @param gameState  - Current game state (must have narrativeState)
 * @param choiceId   - ID of the choice the player made
 * @param sceneGraph - The complete scene graph
 * @returns NarrativeTransitionResult (success | error)
 */
export function advanceNarrative(
  gameState: Readonly<GameState>,
  choiceId: string,
  sceneGraph: SceneGraph
): NarrativeTransitionResult {
  // Step 1: Guard — narrative must be started
  if (gameState.narrativeState === null) {
    return {
      type: 'error',
      code: 'NARRATIVE_NOT_STARTED',
      message: 'Narrative has not been started',
    };
  }

  const currentNarrativeState = gameState.narrativeState;

  // Step 2: Find the current scene
  const currentScene = findScene(currentNarrativeState.currentSceneId, sceneGraph);
  if (currentScene === undefined) {
    return {
      type: 'error',
      code: 'SCENE_NOT_FOUND',
      message: `Scene "${currentNarrativeState.currentSceneId}" not found in scene graph`,
    };
  }

  // Step 3: Process the choice (validate + apply consequence to GameState)
  const choiceResult = processSceneChoice(gameState, currentScene, choiceId);
  if (choiceResult.type === 'error') {
    return {
      type: 'error',
      code: choiceResult.code,
      message: choiceResult.message,
    };
  }

  const { choice, state: updatedGameState } = choiceResult;

  // Step 4: Resolve next scene
  const nextSceneId = choice.nextSceneId;

  // Build history entry
  const historyEntry: SceneHistoryEntry = {
    sceneId: currentNarrativeState.currentSceneId,
    choiceId,
    timestamp: Date.now(),
  };

  // Narrative ending (nextSceneId === null)
  if (nextSceneId === null) {
    const updatedNarrativeState: NarrativeState = {
      currentSceneId: currentNarrativeState.currentSceneId,
      visitedSceneIds: currentNarrativeState.visitedSceneIds,
      choiceFlags: applyFlags(currentNarrativeState.choiceFlags, choice.consequence),
      sceneHistory: [...currentNarrativeState.sceneHistory, historyEntry],
    };

    return {
      type: 'success',
      state: updatedNarrativeState,
      nextScene: null,
      choiceId,
    };
  }

  // Step 5: Validate next scene
  const nextScene = findScene(nextSceneId, sceneGraph);
  if (nextScene === undefined) {
    return {
      type: 'error',
      code: 'SCENE_NOT_FOUND',
      message: `Next scene "${nextSceneId}" not found in scene graph`,
    };
  }

  // Evaluate prerequisites against the updated game state (post-consequence)
  const prerequisitesMet = evaluateAllPrerequisites(
    nextScene.prerequisites,
    updatedGameState.player.personality,
    currentNarrativeState
  );

  if (!prerequisitesMet) {
    return {
      type: 'error',
      code: 'PREREQUISITE_NOT_MET',
      message: `Prerequisites for scene "${nextSceneId}" are not satisfied`,
    };
  }

  // Step 6: Build updated NarrativeState
  const updatedNarrativeState: NarrativeState = {
    currentSceneId: nextScene.id,
    visitedSceneIds: [...currentNarrativeState.visitedSceneIds, nextScene.id],
    choiceFlags: applyFlags(currentNarrativeState.choiceFlags, choice.consequence),
    sceneHistory: [...currentNarrativeState.sceneHistory, historyEntry],
  };

  // Step 7: Return success
  return {
    type: 'success',
    state: updatedNarrativeState,
    nextScene,
    choiceId,
  };
}

// ============================================================================
// Internal Helpers
// ============================================================================

/**
 * Applies setFlags and clearFlags from a consequence to the current flag map.
 *
 * setFlags: sets each named flag to true.
 * clearFlags: removes each named flag from the map.
 *
 * @param currentFlags - The current flag map (immutable)
 * @param consequence  - Optional consequence containing flag mutations
 * @returns New flag map with flags applied
 */
function applyFlags(
  currentFlags: Readonly<Record<string, boolean>>,
  consequence?: ChoiceConsequence
): Readonly<Record<string, boolean>> {
  const result: Record<string, boolean> = { ...currentFlags };

  if (consequence?.setFlags) {
    for (const flag of consequence.setFlags) {
      result[flag] = true;
    }
  }

  if (consequence?.clearFlags) {
    for (const flag of consequence.clearFlags) {
      delete result[flag];
    }
  }

  return result;
}
