/**
 * Ancient Order - Scene Graph Engine
 *
 * Handles scene graph traversal, personality gate evaluation,
 * prerequisite checking, and dead-end validation.
 *
 * Scene graph data is passed as a parameter to every function (stateless).
 * Independent from the dialogue engine [A5].
 *
 * Functions:
 *   evaluateScenePersonalityGate — evaluate a single personality gate
 *   evaluatePrerequisite         — evaluate a single prerequisite condition
 *   evaluateAllPrerequisites     — evaluate all prerequisites (implicit AND) [D1]
 *   getAvailableChoices          — filter scene choices by gates
 *   findScene                    — look up a scene by ID
 *   getCurrentScene              — get current scene with filtered choices
 *   validateSceneGraph           — detect dead-end scenes
 *   getAccessibleScenes          — filter scenes by prerequisites
 */

import type { Personality } from '../types/index.js';
import type {
  Scene,
  SceneGraph,
  SceneChoice,
  ScenePrerequisite,
  ScenePersonalityGate,
  NarrativeState,
  CurrentSceneResult,
} from '../types/narrative.js';

// ============================================================================
// Gate Evaluation
// ============================================================================

/**
 * Evaluates a scene personality gate against player personality.
 *
 * Compares personality[gate.trait] against gate.value using gate.operator.
 * Unknown operator returns false. Unknown trait key returns false (value is
 * undefined, which fails all numeric comparisons).
 *
 * @param gate        - The personality gate to evaluate
 * @param personality - The player's current personality
 * @returns True if the gate passes (choice is available)
 */
export function evaluateScenePersonalityGate(
  gate: ScenePersonalityGate,
  personality: Readonly<Personality>
): boolean {
  const traitValue = personality[gate.trait as keyof Personality];

  // Unknown trait key: traitValue is undefined, which fails all comparisons
  if (traitValue === undefined) return false;

  switch (gate.operator) {
    case 'gte':
      return traitValue >= gate.value;
    case 'lte':
      return traitValue <= gate.value;
    case 'eq':
      return Math.abs(traitValue - gate.value) < 0.1;
    default:
      return false;
  }
}

// ============================================================================
// Prerequisite Evaluation
// ============================================================================

/**
 * Evaluates a single prerequisite condition.
 *
 * @param prerequisite   - The prerequisite to evaluate
 * @param personality    - The player's current personality
 * @param narrativeState - The current narrative state (for flag and visited checks)
 * @returns True if the prerequisite is satisfied
 */
export function evaluatePrerequisite(
  prerequisite: ScenePrerequisite,
  personality: Readonly<Personality>,
  narrativeState: Readonly<NarrativeState>
): boolean {
  switch (prerequisite.type) {
    case 'trait':
      if (
        prerequisite.trait === undefined ||
        prerequisite.operator === undefined ||
        prerequisite.value === undefined
      ) {
        return false;
      }
      return evaluateScenePersonalityGate(
        {
          trait: prerequisite.trait,
          operator: prerequisite.operator,
          value: prerequisite.value,
        },
        personality
      );

    case 'flag':
      if (prerequisite.flag === undefined) return false;
      return narrativeState.choiceFlags[prerequisite.flag] === true;

    case 'visited_scene':
      if (prerequisite.sceneId === undefined) return false;
      return narrativeState.visitedSceneIds.includes(prerequisite.sceneId);

    default:
      return false;
  }
}

/**
 * Evaluates all prerequisites for a scene (implicit AND logic). [D1]
 *
 * An empty prerequisites array means no conditions are required and always
 * returns true.
 *
 * @param prerequisites  - Array of prerequisites (all must pass)
 * @param personality    - The player's current personality
 * @param narrativeState - The current narrative state
 * @returns True if all prerequisites are satisfied (or array is empty)
 */
export function evaluateAllPrerequisites(
  prerequisites: readonly ScenePrerequisite[],
  personality: Readonly<Personality>,
  narrativeState: Readonly<NarrativeState>
): boolean {
  return prerequisites.every(p => evaluatePrerequisite(p, personality, narrativeState));
}

// ============================================================================
// Choice Filtering
// ============================================================================

/**
 * Gets all available choices for a scene given the current personality.
 *
 * Choices without a gate (gate === undefined) are always available.
 * Choices with a gate are available only if the gate evaluates true.
 *
 * @param scene       - The scene containing choices to filter
 * @param personality - The player's current personality
 * @returns Array of choices available to the player
 */
export function getAvailableChoices(
  scene: Scene,
  personality: Readonly<Personality>
): SceneChoice[] {
  return scene.choices.filter(
    choice => choice.gate === undefined || evaluateScenePersonalityGate(choice.gate, personality)
  );
}

// ============================================================================
// Scene Lookup
// ============================================================================

/**
 * Finds a scene by ID within a scene graph.
 *
 * @param sceneId   - The ID of the scene to find
 * @param sceneGraph - The complete scene graph to search
 * @returns The found Scene or undefined if not found
 */
export function findScene(
  sceneId: string,
  sceneGraph: SceneGraph
): Scene | undefined {
  return sceneGraph.find(s => s.id === sceneId);
}

/**
 * Gets the current scene with available choices filtered by personality gates.
 *
 * @param narrativeState - The current narrative state (provides currentSceneId)
 * @param personality    - The player's current personality
 * @param sceneGraph     - The complete scene graph
 * @returns CurrentSceneResult or null if current scene is not found
 */
export function getCurrentScene(
  narrativeState: Readonly<NarrativeState>,
  personality: Readonly<Personality>,
  sceneGraph: SceneGraph
): CurrentSceneResult | null {
  const scene = findScene(narrativeState.currentSceneId, sceneGraph);
  if (scene === undefined) return null;

  return {
    scene,
    availableChoices: getAvailableChoices(scene, personality),
  };
}

// ============================================================================
// Tree Validation
// ============================================================================

/**
 * Validates a scene graph for dead ends.
 *
 * A dead end is any scene where ALL choices have a gate — meaning there is
 * no ungated fallback. The game design constraint requires every scene to
 * have at least one always-available choice so players can never get stuck.
 *
 * Mirrors validateDialogueTree pattern [RF-3].
 *
 * @param sceneGraph - The complete scene graph to validate
 * @returns Validation result with list of problematic scene IDs
 */
export function validateSceneGraph(sceneGraph: SceneGraph): {
  valid: boolean;
  problematicScenes: string[];
} {
  const problematicScenes: string[] = [];

  for (const scene of sceneGraph) {
    // A scene is problematic if it has no ungated choice (gate === undefined)
    const hasUngatedChoice = scene.choices.some(choice => choice.gate === undefined);

    if (!hasUngatedChoice) {
      problematicScenes.push(scene.id);
    }
  }

  return {
    valid: problematicScenes.length === 0,
    problematicScenes,
  };
}

// ============================================================================
// Accessible Scene Filtering
// ============================================================================

/**
 * Returns all scenes from the scene graph that the player can currently access.
 *
 * A scene is accessible if all of its prerequisites are satisfied given the
 * current personality and narrative state.
 *
 * @param sceneGraph     - The complete scene graph
 * @param personality    - The player's current personality
 * @param narrativeState - The current narrative state
 * @returns Array of accessible scenes
 */
export function getAccessibleScenes(
  sceneGraph: SceneGraph,
  personality: Readonly<Personality>,
  narrativeState: Readonly<NarrativeState>
): Scene[] {
  return sceneGraph.filter(scene =>
    evaluateAllPrerequisites(scene.prerequisites, personality, narrativeState)
  );
}
