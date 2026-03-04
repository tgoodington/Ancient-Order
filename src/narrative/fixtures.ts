/**
 * Ancient Order - Narrative Test Fixtures
 *
 * Shared scene graph data for use in narrative engine unit tests.
 * Mirrors src/dialogue/fixtures.ts pattern.
 *
 * Scene graphs:
 * - TEST_SCENE_GRAPH: 3 scenes with valid structure, one ungated fallback each
 * - DEAD_END_SCENE_GRAPH: scene where ALL choices are gated (for validation testing)
 * - PREREQUISITE_SCENE_GRAPH: scenes with flag/trait/visited_scene prerequisites
 */

import type { Scene, SceneGraph } from '../types/narrative.js';

// ============================================================================
// TEST_SCENE_GRAPH: 3 valid scenes
// ============================================================================

/**
 * Minimal valid scene graph for testing.
 *
 * Scene structure:
 *   scene_opening
 *     → [charisma gte 20]  choice_bold   → scene_two
 *     → [ungated]          choice_cautious → scene_two
 *
 *   scene_two
 *     → [ungated]          choice_proceed → scene_ending
 *     → [cunning gte 25]   choice_clever  → scene_ending
 *
 *   scene_ending
 *     → [ungated]          choice_finish  → null (end narrative)
 */
export const TEST_SCENE_GRAPH: SceneGraph = [
  {
    id: 'scene_opening',
    title: 'The Gathering',
    text: 'You stand at the crossroads of fate. The Order awaits your decision.',
    prerequisites: [],
    choices: [
      {
        id: 'choice_bold',
        text: 'Step forward boldly',
        gate: { trait: 'charisma', operator: 'gte', value: 20 },
        consequence: {
          personalityEffect: { charisma: 2 },
          npcEffects: [{ npcId: 'npc_scout_elena', trustChange: 5 }],
          setFlags: ['brave_opening'],
        },
        nextSceneId: 'scene_two',
      },
      {
        id: 'choice_cautious',
        text: 'Observe from the shadows',
        consequence: {
          personalityEffect: { cunning: 2 },
          setFlags: ['cautious_opening'],
        },
        nextSceneId: 'scene_two',
      },
    ],
  },
  {
    id: 'scene_two',
    title: 'The Council',
    text: 'The council members eye you carefully as you enter.',
    prerequisites: [],
    choices: [
      {
        id: 'choice_proceed',
        text: 'State your purpose plainly',
        nextSceneId: 'scene_ending',
      },
      {
        id: 'choice_clever',
        text: 'Play your cards close',
        gate: { trait: 'cunning', operator: 'gte', value: 25 },
        consequence: {
          personalityEffect: { cunning: 1 },
        },
        nextSceneId: 'scene_ending',
      },
    ],
  },
  {
    id: 'scene_ending',
    title: 'The Decision',
    text: 'The time has come to make your final choice.',
    prerequisites: [],
    choices: [
      {
        id: 'choice_finish',
        text: 'Accept your fate',
        nextSceneId: null,
      },
    ],
  },
];

// ============================================================================
// DEAD_END_SCENE_GRAPH: scene with all choices gated
// ============================================================================

/**
 * Scene graph containing a dead-end scene (all choices are gated).
 * Used to test that validateSceneGraph() detects and flags the problematic scene.
 */
export const DEAD_END_SCENE_GRAPH: SceneGraph = [
  {
    id: 'scene_dead_end',
    title: 'Locked Door',
    text: 'Only the worthy may proceed.',
    prerequisites: [],
    choices: [
      {
        id: 'choice_empathy_path',
        text: 'I feel your pain deeply.',
        gate: { trait: 'empathy', operator: 'gte', value: 30 },
        nextSceneId: null,
      },
      {
        id: 'choice_logic_path',
        text: 'Logic demands I proceed.',
        gate: { trait: 'logic', operator: 'gte', value: 30 },
        nextSceneId: null,
      },
    ],
  },
];

// ============================================================================
// PREREQUISITE_SCENE_GRAPH: scenes with flag/trait/visited_scene prerequisites
// ============================================================================

/**
 * Scene graph for testing prerequisite chains.
 *
 * Scene structure:
 *   scene_start (no prerequisites)
 *     → [ungated] choice_start_go → scene_trait_gate
 *     → [ungated] choice_start_flag → scene_flag_gate (sets 'unlocked' flag)
 *
 *   scene_trait_gate (requires: patience gte 20)
 *     → [ungated] choice_trait_proceed → null
 *
 *   scene_flag_gate (requires: flag 'unlocked' = true)
 *     → [ungated] choice_flag_proceed → null
 *
 *   scene_visited_gate (requires: visited_scene 'scene_start')
 *     → [ungated] choice_visited_proceed → null
 */
export const PREREQUISITE_SCENE_GRAPH: SceneGraph = [
  {
    id: 'scene_prereq_start',
    title: 'The Beginning',
    text: 'A journey of a thousand miles begins here.',
    prerequisites: [],
    choices: [
      {
        id: 'choice_go_trait',
        text: 'Head toward the mountain',
        consequence: {
          setFlags: ['unlocked'],
        },
        nextSceneId: 'scene_prereq_trait_gate',
      },
      {
        id: 'choice_go_visited',
        text: 'Explore the valley',
        nextSceneId: 'scene_prereq_visited_gate',
      },
      {
        id: 'choice_go_flag',
        text: 'Try the secret door',
        nextSceneId: 'scene_prereq_flag_gate',
      },
    ],
  },
  {
    id: 'scene_prereq_trait_gate',
    title: 'Mountain Pass',
    text: 'The path requires patience to navigate.',
    prerequisites: [
      { type: 'trait', trait: 'patience', operator: 'gte', value: 20 },
    ],
    choices: [
      {
        id: 'choice_trait_proceed',
        text: 'Continue with patience',
        nextSceneId: null,
      },
    ],
  },
  {
    id: 'scene_prereq_flag_gate',
    title: 'Secret Door',
    text: 'The door responds to those who carry the key.',
    prerequisites: [
      { type: 'flag', flag: 'unlocked' },
    ],
    choices: [
      {
        id: 'choice_flag_proceed',
        text: 'Use the key',
        nextSceneId: null,
      },
    ],
  },
  {
    id: 'scene_prereq_visited_gate',
    title: 'Valley Crossroads',
    text: 'Only those who have been to the beginning may pass.',
    prerequisites: [
      { type: 'visited_scene', sceneId: 'scene_prereq_start' },
    ],
    choices: [
      {
        id: 'choice_visited_proceed',
        text: 'Pass through',
        nextSceneId: null,
      },
    ],
  },
];
