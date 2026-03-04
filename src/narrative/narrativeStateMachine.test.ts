/**
 * Ancient Order - Narrative State Machine Tests
 *
 * Coverage:
 *   - createInitialNarrativeState: structure, visitedSceneIds, empty flags/history
 *   - advanceNarrative: happy path, null narrativeState guard, scene not found,
 *     invalid choice, gated choice, prerequisite not met, null nextSceneId (narrative end),
 *     multi-step traversal, flag setting, visited scene tracking
 */

import { describe, it, expect, beforeEach } from 'vitest';

import {
  createInitialNarrativeState,
  advanceNarrative,
} from './narrativeStateMachine.js';

import { TEST_SCENE_GRAPH, PREREQUISITE_SCENE_GRAPH } from './fixtures.js';
import { createNewGameState } from '../state/gameState.js';
import { initializeNarrative } from '../state/stateUpdaters.js';
import type { GameState, Personality } from '../types/index.js';
import type { NarrativeState } from '../types/narrative.js';

// ============================================================================
// Helpers
// ============================================================================

function makePersonality(overrides: Partial<Personality> = {}): Personality {
  const defaults: Personality = {
    patience: 16,
    empathy: 16,
    cunning: 16,
    logic: 17,
    kindness: 17,
    charisma: 18,
  };
  return { ...defaults, ...overrides };
}

function makeGameStateWithNarrative(
  sceneId: string,
  personalityOverrides: Partial<Personality> = {}
): GameState {
  const base = createNewGameState();
  const state = {
    ...base,
    player: {
      ...base.player,
      personality: makePersonality(personalityOverrides),
    },
  };
  return initializeNarrative(state, sceneId);
}

// ============================================================================
// createInitialNarrativeState
// ============================================================================

describe('createInitialNarrativeState', () => {
  it('sets currentSceneId to the provided starting scene ID', () => {
    const ns = createInitialNarrativeState('scene_opening');
    expect(ns.currentSceneId).toBe('scene_opening');
  });

  it('includes starting scene in visitedSceneIds', () => {
    const ns = createInitialNarrativeState('scene_opening');
    expect(ns.visitedSceneIds).toContain('scene_opening');
    expect(ns.visitedSceneIds).toHaveLength(1);
  });

  it('initializes choiceFlags as empty object', () => {
    const ns = createInitialNarrativeState('scene_opening');
    expect(Object.keys(ns.choiceFlags)).toHaveLength(0);
  });

  it('initializes sceneHistory as empty array', () => {
    const ns = createInitialNarrativeState('scene_opening');
    expect(ns.sceneHistory).toHaveLength(0);
  });
});

// ============================================================================
// advanceNarrative — guard conditions
// ============================================================================

describe('advanceNarrative — guard conditions', () => {
  it('returns error NARRATIVE_NOT_STARTED when narrativeState is null', () => {
    const state = createNewGameState(); // narrativeState: null
    const result = advanceNarrative(state, 'choice_cautious', TEST_SCENE_GRAPH);
    expect(result.type).toBe('error');
    if (result.type === 'error') {
      expect(result.code).toBe('NARRATIVE_NOT_STARTED');
    }
  });

  it('returns error SCENE_NOT_FOUND when current scene is not in graph', () => {
    const base = createNewGameState();
    const state: GameState = {
      ...base,
      narrativeState: {
        currentSceneId: 'scene_nonexistent',
        visitedSceneIds: ['scene_nonexistent'],
        choiceFlags: {},
        sceneHistory: [],
      },
    };
    const result = advanceNarrative(state, 'choice_cautious', TEST_SCENE_GRAPH);
    expect(result.type).toBe('error');
    if (result.type === 'error') {
      expect(result.code).toBe('SCENE_NOT_FOUND');
    }
  });

  it('returns error CHOICE_NOT_FOUND for a nonexistent choice ID', () => {
    const state = makeGameStateWithNarrative('scene_opening');
    const result = advanceNarrative(state, 'choice_nonexistent', TEST_SCENE_GRAPH);
    expect(result.type).toBe('error');
    if (result.type === 'error') {
      expect(result.code).toBe('CHOICE_NOT_FOUND');
    }
  });

  it('returns error CHOICE_NOT_AVAILABLE when gate fails', () => {
    // Default personality: charisma 18, gate requires charisma gte 20
    const state = makeGameStateWithNarrative('scene_opening', { charisma: 10 });
    const result = advanceNarrative(state, 'choice_bold', TEST_SCENE_GRAPH);
    expect(result.type).toBe('error');
    if (result.type === 'error') {
      expect(result.code).toBe('CHOICE_NOT_AVAILABLE');
    }
  });
});

// ============================================================================
// advanceNarrative — happy path
// ============================================================================

describe('advanceNarrative — happy path', () => {
  it('returns type: success for a valid ungated choice', () => {
    const state = makeGameStateWithNarrative('scene_opening');
    const result = advanceNarrative(state, 'choice_cautious', TEST_SCENE_GRAPH);
    expect(result.type).toBe('success');
  });

  it('transitions to the next scene (nextScene.id = scene_two)', () => {
    const state = makeGameStateWithNarrative('scene_opening');
    const result = advanceNarrative(state, 'choice_cautious', TEST_SCENE_GRAPH);
    if (result.type === 'success') {
      expect(result.nextScene?.id).toBe('scene_two');
    }
  });

  it('updates currentSceneId to the next scene', () => {
    const state = makeGameStateWithNarrative('scene_opening');
    const result = advanceNarrative(state, 'choice_cautious', TEST_SCENE_GRAPH);
    if (result.type === 'success') {
      expect(result.state.currentSceneId).toBe('scene_two');
    }
  });

  it('adds next scene to visitedSceneIds', () => {
    const state = makeGameStateWithNarrative('scene_opening');
    const result = advanceNarrative(state, 'choice_cautious', TEST_SCENE_GRAPH);
    if (result.type === 'success') {
      expect(result.state.visitedSceneIds).toContain('scene_two');
    }
  });

  it('preserves original visited scenes in visitedSceneIds', () => {
    const state = makeGameStateWithNarrative('scene_opening');
    const result = advanceNarrative(state, 'choice_cautious', TEST_SCENE_GRAPH);
    if (result.type === 'success') {
      expect(result.state.visitedSceneIds).toContain('scene_opening');
      expect(result.state.visitedSceneIds).toContain('scene_two');
    }
  });

  it('adds a history entry for the transition', () => {
    const state = makeGameStateWithNarrative('scene_opening');
    const result = advanceNarrative(state, 'choice_cautious', TEST_SCENE_GRAPH);
    if (result.type === 'success') {
      expect(result.state.sceneHistory).toHaveLength(1);
      expect(result.state.sceneHistory[0].sceneId).toBe('scene_opening');
      expect(result.state.sceneHistory[0].choiceId).toBe('choice_cautious');
    }
  });

  it('returns the choiceId in the result', () => {
    const state = makeGameStateWithNarrative('scene_opening');
    const result = advanceNarrative(state, 'choice_cautious', TEST_SCENE_GRAPH);
    if (result.type === 'success') {
      expect(result.choiceId).toBe('choice_cautious');
    }
  });
});

// ============================================================================
// advanceNarrative — flag handling
// ============================================================================

describe('advanceNarrative — flag handling', () => {
  it('sets flags from choice consequence (setFlags)', () => {
    // choice_cautious sets 'cautious_opening'
    const state = makeGameStateWithNarrative('scene_opening');
    const result = advanceNarrative(state, 'choice_cautious', TEST_SCENE_GRAPH);
    if (result.type === 'success') {
      expect(result.state.choiceFlags['cautious_opening']).toBe(true);
    }
  });

  it('sets flags from gated choice consequence (setFlags)', () => {
    // choice_bold sets 'brave_opening', requires charisma gte 20
    const state = makeGameStateWithNarrative('scene_opening', { charisma: 25 });
    const result = advanceNarrative(state, 'choice_bold', TEST_SCENE_GRAPH);
    if (result.type === 'success') {
      expect(result.state.choiceFlags['brave_opening']).toBe(true);
    }
  });

  it('does not set flags for choices without consequence', () => {
    // scene_two → choice_proceed has no consequence
    const base = makeGameStateWithNarrative('scene_two');
    const result = advanceNarrative(base, 'choice_proceed', TEST_SCENE_GRAPH);
    if (result.type === 'success') {
      expect(Object.keys(result.state.choiceFlags)).toHaveLength(0);
    }
  });

  it('preserves existing flags when adding new ones', () => {
    // Start with an existing flag
    const base = createNewGameState();
    const stateWithFlag: GameState = {
      ...base,
      narrativeState: {
        currentSceneId: 'scene_opening',
        visitedSceneIds: ['scene_opening'],
        choiceFlags: { existing_flag: true },
        sceneHistory: [],
      },
    };
    const result = advanceNarrative(stateWithFlag, 'choice_cautious', TEST_SCENE_GRAPH);
    if (result.type === 'success') {
      expect(result.state.choiceFlags['existing_flag']).toBe(true);
      expect(result.state.choiceFlags['cautious_opening']).toBe(true);
    }
  });
});

// ============================================================================
// advanceNarrative — narrative ending
// ============================================================================

describe('advanceNarrative — narrative ending', () => {
  it('returns nextScene: null when choice.nextSceneId is null', () => {
    // scene_ending → choice_finish → nextSceneId: null
    const state = makeGameStateWithNarrative('scene_ending');
    const result = advanceNarrative(state, 'choice_finish', TEST_SCENE_GRAPH);
    if (result.type === 'success') {
      expect(result.nextScene).toBeNull();
    }
  });

  it('returns type: success on narrative end', () => {
    const state = makeGameStateWithNarrative('scene_ending');
    const result = advanceNarrative(state, 'choice_finish', TEST_SCENE_GRAPH);
    expect(result.type).toBe('success');
  });

  it('preserves currentSceneId when narrative ends', () => {
    const state = makeGameStateWithNarrative('scene_ending');
    const result = advanceNarrative(state, 'choice_finish', TEST_SCENE_GRAPH);
    if (result.type === 'success') {
      // stays at scene_ending since no transition happened
      expect(result.state.currentSceneId).toBe('scene_ending');
    }
  });
});

// ============================================================================
// advanceNarrative — multi-step traversal
// ============================================================================

describe('advanceNarrative — multi-step traversal', () => {
  it('completes a full 3-step traversal through the test graph', () => {
    // Step 1: opening -> choice_cautious -> scene_two
    let state = makeGameStateWithNarrative('scene_opening');
    let result = advanceNarrative(state, 'choice_cautious', TEST_SCENE_GRAPH);
    expect(result.type).toBe('success');

    if (result.type !== 'success') return;

    // Apply updated narrative state to game state for next step
    state = {
      ...state,
      narrativeState: result.state,
    };

    // Step 2: scene_two -> choice_proceed -> scene_ending
    result = advanceNarrative(state, 'choice_proceed', TEST_SCENE_GRAPH);
    expect(result.type).toBe('success');

    if (result.type !== 'success') return;

    state = {
      ...state,
      narrativeState: result.state,
    };

    // Step 3: scene_ending -> choice_finish -> null
    result = advanceNarrative(state, 'choice_finish', TEST_SCENE_GRAPH);
    expect(result.type).toBe('success');
    if (result.type === 'success') {
      expect(result.nextScene).toBeNull();
      expect(result.state.visitedSceneIds).toContain('scene_opening');
      expect(result.state.visitedSceneIds).toContain('scene_two');
      expect(result.state.visitedSceneIds).toContain('scene_ending');
      expect(result.state.sceneHistory).toHaveLength(3);
    }
  });
});

// ============================================================================
// advanceNarrative — prerequisite checking
// ============================================================================

describe('advanceNarrative — prerequisite checking', () => {
  it('returns error PREREQUISITE_NOT_MET when next scene prerequisites fail', () => {
    // scene_prereq_start -> choice_go_trait -> scene_prereq_trait_gate
    // scene_prereq_trait_gate requires patience gte 20, default patience=16
    const state = makeGameStateWithNarrative('scene_prereq_start', { patience: 10 });
    const result = advanceNarrative(state, 'choice_go_trait', PREREQUISITE_SCENE_GRAPH);
    expect(result.type).toBe('error');
    if (result.type === 'error') {
      expect(result.code).toBe('PREREQUISITE_NOT_MET');
    }
  });

  it('returns success when next scene prerequisites pass', () => {
    // patience gte 20 satisfies scene_prereq_trait_gate
    const state = makeGameStateWithNarrative('scene_prereq_start', { patience: 25 });
    const result = advanceNarrative(state, 'choice_go_trait', PREREQUISITE_SCENE_GRAPH);
    expect(result.type).toBe('success');
  });
});

// ============================================================================
// advanceNarrative — flag-gated scene prerequisites
// ============================================================================

describe('flag-gated scene prerequisites', () => {
  it('returns PREREQUISITE_NOT_MET when required flag is absent', () => {
    // choice_go_flag is available (no gate), routes to scene_prereq_flag_gate
    // but scene_prereq_flag_gate requires flag 'unlocked' which is not set
    const state = makeGameStateWithNarrative('scene_prereq_start');
    const result = advanceNarrative(state, 'choice_go_flag', PREREQUISITE_SCENE_GRAPH);
    expect(result.type).toBe('error');
    if (result.type === 'error') {
      expect(result.code).toBe('PREREQUISITE_NOT_MET');
    }
  });

  it('succeeds when required flag is present', () => {
    // Build a GameState at scene_prereq_start with the 'unlocked' flag already set
    const base = createNewGameState();
    const stateWithFlag: GameState = {
      ...base,
      narrativeState: {
        currentSceneId: 'scene_prereq_start',
        visitedSceneIds: ['scene_prereq_start'],
        choiceFlags: { unlocked: true },
        sceneHistory: [],
      },
    };
    const result = advanceNarrative(stateWithFlag, 'choice_go_flag', PREREQUISITE_SCENE_GRAPH);
    expect(result.type).toBe('success');
  });
});
