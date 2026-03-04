/**
 * Ancient Order - Sprint 3 Narrative Type Definitions
 *
 * Standalone leaf file with zero imports from types/index.ts.
 * The one-way dependency is: index.ts -> narrative.ts
 *
 * Contains all narrative scene types, choice/consequence types,
 * NarrativeState, result types, and synergy types.
 */

// ============================================================================
// Synergy Types
// ============================================================================

/**
 * The stat types synergy can modify on combatants.
 */
export type SynergyStatType = 'power' | 'speed';

/**
 * Output of a satisfied paradigm -- the bonus to apply.
 */
export interface SynergyBonus {
  readonly paradigmName: string;    // e.g., "Well Rounded", "Bond"
  readonly stat: SynergyStatType;   // which combatant stat gets modified
  readonly multiplier: number;      // e.g., 1.10 for +10%
  readonly matchQuality: number;    // 0.0-1.0+, used for highest-only comparison
}

/**
 * Result of the synergy calculation. null = no paradigm satisfied.
 */
export type SynergyResult = SynergyBonus | null;

/**
 * Data-driven configuration for a single synergy paradigm.
 */
export interface ParadigmConfig {
  readonly name: string;                          // display name
  readonly type: 'well_rounded' | 'bond';         // evaluation algorithm selector
  readonly threshold: number;                      // activation threshold (integer: 25 = 25%)
  readonly stat: SynergyStatType;                  // stat to modify
  readonly multiplier: number;                     // stat multiplier (e.g., 1.10)
}

// ============================================================================
// Scene Prerequisite Types
// ============================================================================

/**
 * The kind of prerequisite condition to evaluate.
 */
export type PrerequisiteType = 'trait' | 'flag' | 'visited_scene';

/**
 * Comparison operators for trait-based prerequisites.
 */
export type PrerequisiteOperator = 'gte' | 'lte' | 'eq';

/**
 * A single prerequisite condition for a scene.
 * Scenes have an array of these; ALL must pass (implicit AND). [D1]
 *
 * Discriminated by `type` field:
 * - trait: checks player personality trait against threshold
 * - flag: checks if a choice flag is set (true) in NarrativeState.choiceFlags
 * - visited_scene: checks if a scene ID is in NarrativeState.visitedSceneIds
 */
export interface ScenePrerequisite {
  readonly type: PrerequisiteType;
  readonly trait?: string;                // required when type === 'trait'
  readonly operator?: PrerequisiteOperator; // required when type === 'trait'
  readonly value?: number;                 // required when type === 'trait' (integer, e.g., 25)
  readonly flag?: string;                  // required when type === 'flag'
  readonly sceneId?: string;               // required when type === 'visited_scene'
}

// ============================================================================
// Choice & Consequence Types
// ============================================================================

/**
 * NPC relationship adjustment triggered by a choice.
 */
export interface NpcEffect {
  readonly npcId: string;
  readonly affectionChange?: number;  // delta applied to NPC affection (-100 to +100 range)
  readonly trustChange?: number;      // delta applied to NPC trust (-100 to +100 range)
}

/**
 * Personality adjustment triggered by a choice.
 * Keys are PersonalityTrait names; values are integer deltas.
 */
export type PersonalityEffect = Readonly<Record<string, number>>;

/**
 * The full consequence specification for a choice.
 */
export interface ChoiceConsequence {
  readonly personalityEffect?: PersonalityEffect;  // trait adjustments
  readonly npcEffects?: readonly NpcEffect[];       // NPC relationship changes
  readonly setFlags?: readonly string[];            // flag names to set to true
  readonly clearFlags?: readonly string[];          // flag names to set to false (remove)
}

/**
 * Personality gate for a scene choice -- mirrors the dialogue system's PersonalityGate.
 * Defined here to avoid importing from index.ts (leaf file constraint).
 */
export interface ScenePersonalityGate {
  readonly trait: string;                   // PersonalityTrait name
  readonly operator: 'gte' | 'lte' | 'eq'; // comparison operator
  readonly value: number;                   // threshold (integer, 5-35 range)
}

/**
 * A single choice available within a scene.
 */
export interface SceneChoice {
  readonly id: string;                      // unique within the scene
  readonly text: string;                    // display text for the choice
  readonly gate?: ScenePersonalityGate;     // undefined = always available (ungated)
  readonly consequence?: ChoiceConsequence;  // effects applied when chosen
  readonly nextSceneId: string | null;      // scene to transition to (null = end narrative)
}

// ============================================================================
// Scene Types
// ============================================================================

/**
 * A single node in the scene graph.
 */
export interface Scene {
  readonly id: string;                              // unique scene identifier
  readonly title: string;                           // scene title for display
  readonly text: string;                            // narrative text content
  readonly choices: readonly SceneChoice[];          // available player choices
  readonly prerequisites: readonly ScenePrerequisite[]; // ALL must pass for scene access [D1]
}

/**
 * A complete scene graph: an array of Scene nodes.
 * Passed as a parameter to engine functions (not stored on state). [A2]
 */
export type SceneGraph = readonly Scene[];

// ============================================================================
// Narrative State
// ============================================================================

/**
 * Runtime narrative state tracked on GameState.
 * Stored on GameState as `narrativeState: NarrativeState | null`.
 */
export interface NarrativeState {
  readonly currentSceneId: string;                              // ID of the active scene
  readonly visitedSceneIds: readonly string[];                   // scenes the player has visited [A3]
  readonly choiceFlags: Readonly<Record<string, boolean>>;       // consequence flags [A4]
  readonly sceneHistory: readonly SceneHistoryEntry[];            // ordered log of scene transitions
}

/**
 * A single entry in the scene transition history log.
 */
export interface SceneHistoryEntry {
  readonly sceneId: string;
  readonly choiceId: string;
  readonly timestamp: number;
}

// ============================================================================
// State Machine Result Types [D4]
// ============================================================================

/**
 * Successful scene transition result.
 */
export interface NarrativeTransitionSuccess {
  readonly type: 'success';
  readonly state: NarrativeState;         // updated NarrativeState
  readonly nextScene: Scene | null;       // the scene transitioned to (null = narrative ended)
  readonly choiceId: string;              // the choice that was made
}

/**
 * Failed scene transition result with typed error.
 */
export interface NarrativeTransitionError {
  readonly type: 'error';
  readonly code: string;                  // ErrorCodes constant value
  readonly message: string;              // human-readable error description
}

/**
 * Discriminated union for state machine transition results.
 */
export type NarrativeTransitionResult = NarrativeTransitionSuccess | NarrativeTransitionError;

// ============================================================================
// Scene Engine Result Types
// ============================================================================

/**
 * Result of getting the current scene with available choices filtered.
 */
export interface CurrentSceneResult {
  readonly scene: Scene;
  readonly availableChoices: readonly SceneChoice[];  // filtered by gates + prerequisites
}
