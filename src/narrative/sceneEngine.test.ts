/**
 * Ancient Order - Scene Engine Tests
 *
 * Coverage:
 *   - evaluateScenePersonalityGate: gte/lte/eq pass and fail, unknown trait, unknown operator
 *   - evaluatePrerequisite: trait/flag/visited_scene types, missing fields, unknown type
 *   - evaluateAllPrerequisites: empty array, all pass, one fail
 *   - getAvailableChoices: ungated always visible, gated conditional
 *   - findScene: found and not found
 *   - getCurrentScene: found, not found, filtered choices
 *   - validateSceneGraph: valid graph, dead-end graph, mixed
 *   - getAccessibleScenes: prerequisite filtering
 */

import { describe, it, expect } from 'vitest';

import {
  evaluateScenePersonalityGate,
  evaluatePrerequisite,
  evaluateAllPrerequisites,
  getAvailableChoices,
  findScene,
  getCurrentScene,
  validateSceneGraph,
  getAccessibleScenes,
} from './sceneEngine.js';

import {
  TEST_SCENE_GRAPH,
  DEAD_END_SCENE_GRAPH,
  PREREQUISITE_SCENE_GRAPH,
} from './fixtures.js';

import type { Personality } from '../types/index.js';
import type { NarrativeState, ScenePrerequisite } from '../types/narrative.js';

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

function makeNarrativeState(overrides: Partial<NarrativeState> = {}): NarrativeState {
  return {
    currentSceneId: 'scene_opening',
    visitedSceneIds: ['scene_opening'],
    choiceFlags: {},
    sceneHistory: [],
    ...overrides,
  };
}

// ============================================================================
// evaluateScenePersonalityGate
// ============================================================================

describe('evaluateScenePersonalityGate', () => {
  it('returns true when gte operator and trait meets threshold (exact)', () => {
    const p = makePersonality({ charisma: 20 });
    expect(evaluateScenePersonalityGate({ trait: 'charisma', operator: 'gte', value: 20 }, p)).toBe(true);
  });

  it('returns true when gte operator and trait exceeds threshold', () => {
    const p = makePersonality({ charisma: 25 });
    expect(evaluateScenePersonalityGate({ trait: 'charisma', operator: 'gte', value: 20 }, p)).toBe(true);
  });

  it('returns false when gte operator and trait below threshold', () => {
    const p = makePersonality({ charisma: 15 });
    expect(evaluateScenePersonalityGate({ trait: 'charisma', operator: 'gte', value: 20 }, p)).toBe(false);
  });

  it('returns true when lte operator and trait at threshold (exact)', () => {
    const p = makePersonality({ cunning: 15 });
    expect(evaluateScenePersonalityGate({ trait: 'cunning', operator: 'lte', value: 15 }, p)).toBe(true);
  });

  it('returns true when lte operator and trait below threshold', () => {
    const p = makePersonality({ cunning: 10 });
    expect(evaluateScenePersonalityGate({ trait: 'cunning', operator: 'lte', value: 15 }, p)).toBe(true);
  });

  it('returns false when lte operator and trait above threshold', () => {
    const p = makePersonality({ cunning: 20 });
    expect(evaluateScenePersonalityGate({ trait: 'cunning', operator: 'lte', value: 15 }, p)).toBe(false);
  });

  it('returns true when eq operator and trait within float tolerance (< 0.1)', () => {
    const p = makePersonality({ patience: 20.05 });
    expect(evaluateScenePersonalityGate({ trait: 'patience', operator: 'eq', value: 20 }, p)).toBe(true);
  });

  it('returns false when eq operator and trait outside float tolerance (>= 0.1)', () => {
    const p = makePersonality({ patience: 20.15 });
    expect(evaluateScenePersonalityGate({ trait: 'patience', operator: 'eq', value: 20 }, p)).toBe(false);
  });

  it('returns false for unknown operator', () => {
    const p = makePersonality({ charisma: 25 });
    expect(
      evaluateScenePersonalityGate({ trait: 'charisma', operator: 'unknown' as 'gte', value: 20 }, p)
    ).toBe(false);
  });

  it('returns false for unknown trait key', () => {
    const p = makePersonality({});
    expect(
      evaluateScenePersonalityGate({ trait: 'nonexistent_trait', operator: 'gte', value: 10 }, p)
    ).toBe(false);
  });
});

// ============================================================================
// evaluatePrerequisite
// ============================================================================

describe('evaluatePrerequisite', () => {
  it('trait type: returns true when personality meets the gate', () => {
    const p = makePersonality({ patience: 25 });
    const ns = makeNarrativeState();
    const prereq: ScenePrerequisite = { type: 'trait', trait: 'patience', operator: 'gte', value: 20 };
    expect(evaluatePrerequisite(prereq, p, ns)).toBe(true);
  });

  it('trait type: returns false when personality does not meet the gate', () => {
    const p = makePersonality({ patience: 15 });
    const ns = makeNarrativeState();
    const prereq: ScenePrerequisite = { type: 'trait', trait: 'patience', operator: 'gte', value: 20 };
    expect(evaluatePrerequisite(prereq, p, ns)).toBe(false);
  });

  it('trait type: returns false when trait/operator/value are missing', () => {
    const p = makePersonality({});
    const ns = makeNarrativeState();
    const prereq: ScenePrerequisite = { type: 'trait' };
    expect(evaluatePrerequisite(prereq, p, ns)).toBe(false);
  });

  it('flag type: returns true when flag is set to true', () => {
    const p = makePersonality({});
    const ns = makeNarrativeState({ choiceFlags: { unlocked: true } });
    const prereq: ScenePrerequisite = { type: 'flag', flag: 'unlocked' };
    expect(evaluatePrerequisite(prereq, p, ns)).toBe(true);
  });

  it('flag type: returns false when flag is absent', () => {
    const p = makePersonality({});
    const ns = makeNarrativeState({ choiceFlags: {} });
    const prereq: ScenePrerequisite = { type: 'flag', flag: 'unlocked' };
    expect(evaluatePrerequisite(prereq, p, ns)).toBe(false);
  });

  it('flag type: returns false when flag name is missing from prerequisite', () => {
    const p = makePersonality({});
    const ns = makeNarrativeState({ choiceFlags: { unlocked: true } });
    const prereq: ScenePrerequisite = { type: 'flag' };
    expect(evaluatePrerequisite(prereq, p, ns)).toBe(false);
  });

  it('visited_scene type: returns true when scene has been visited', () => {
    const p = makePersonality({});
    const ns = makeNarrativeState({ visitedSceneIds: ['scene_opening', 'scene_two'] });
    const prereq: ScenePrerequisite = { type: 'visited_scene', sceneId: 'scene_two' };
    expect(evaluatePrerequisite(prereq, p, ns)).toBe(true);
  });

  it('visited_scene type: returns false when scene has not been visited', () => {
    const p = makePersonality({});
    const ns = makeNarrativeState({ visitedSceneIds: ['scene_opening'] });
    const prereq: ScenePrerequisite = { type: 'visited_scene', sceneId: 'scene_two' };
    expect(evaluatePrerequisite(prereq, p, ns)).toBe(false);
  });

  it('visited_scene type: returns false when sceneId is missing from prerequisite', () => {
    const p = makePersonality({});
    const ns = makeNarrativeState({ visitedSceneIds: ['scene_opening'] });
    const prereq: ScenePrerequisite = { type: 'visited_scene' };
    expect(evaluatePrerequisite(prereq, p, ns)).toBe(false);
  });

  it('unknown type: returns false', () => {
    const p = makePersonality({});
    const ns = makeNarrativeState();
    const prereq = { type: 'unknown_type' } as unknown as ScenePrerequisite;
    expect(evaluatePrerequisite(prereq, p, ns)).toBe(false);
  });
});

// ============================================================================
// evaluateAllPrerequisites
// ============================================================================

describe('evaluateAllPrerequisites', () => {
  it('returns true for an empty prerequisites array', () => {
    const p = makePersonality({});
    const ns = makeNarrativeState();
    expect(evaluateAllPrerequisites([], p, ns)).toBe(true);
  });

  it('returns true when all prerequisites pass', () => {
    const p = makePersonality({ patience: 25 });
    const ns = makeNarrativeState({ choiceFlags: { unlocked: true } });
    const prereqs: ScenePrerequisite[] = [
      { type: 'trait', trait: 'patience', operator: 'gte', value: 20 },
      { type: 'flag', flag: 'unlocked' },
    ];
    expect(evaluateAllPrerequisites(prereqs, p, ns)).toBe(true);
  });

  it('returns false when any prerequisite fails', () => {
    const p = makePersonality({ patience: 15 });
    const ns = makeNarrativeState({ choiceFlags: { unlocked: true } });
    const prereqs: ScenePrerequisite[] = [
      { type: 'trait', trait: 'patience', operator: 'gte', value: 20 },
      { type: 'flag', flag: 'unlocked' },
    ];
    expect(evaluateAllPrerequisites(prereqs, p, ns)).toBe(false);
  });

  it('returns false when all prerequisites fail', () => {
    const p = makePersonality({ patience: 10 });
    const ns = makeNarrativeState({ choiceFlags: {} });
    const prereqs: ScenePrerequisite[] = [
      { type: 'trait', trait: 'patience', operator: 'gte', value: 20 },
      { type: 'flag', flag: 'unlocked' },
    ];
    expect(evaluateAllPrerequisites(prereqs, p, ns)).toBe(false);
  });
});

// ============================================================================
// getAvailableChoices
// ============================================================================

describe('getAvailableChoices', () => {
  const openingScene = TEST_SCENE_GRAPH[0]!;

  it('always includes ungated choices regardless of personality', () => {
    const lowPersonality = makePersonality({ charisma: 5 });
    const choices = getAvailableChoices(openingScene, lowPersonality);
    const ids = choices.map(c => c.id);
    expect(ids).toContain('choice_cautious'); // ungated
    expect(ids).not.toContain('choice_bold'); // gate: charisma gte 20
  });

  it('includes a gated choice when the gate passes', () => {
    const highCharisma = makePersonality({ charisma: 25 });
    const choices = getAvailableChoices(openingScene, highCharisma);
    const ids = choices.map(c => c.id);
    expect(ids).toContain('choice_cautious');
    expect(ids).toContain('choice_bold');
  });

  it('returns only the ungated choice when gate fails', () => {
    const lowPersonality = makePersonality({ charisma: 10 });
    const choices = getAvailableChoices(openingScene, lowPersonality);
    expect(choices).toHaveLength(1);
    expect(choices[0].id).toBe('choice_cautious');
  });
});

// ============================================================================
// findScene
// ============================================================================

describe('findScene', () => {
  it('finds a scene by id', () => {
    const scene = findScene('scene_opening', TEST_SCENE_GRAPH);
    expect(scene).toBeDefined();
    expect(scene?.id).toBe('scene_opening');
  });

  it('returns undefined for a nonexistent scene id', () => {
    const scene = findScene('scene_nonexistent', TEST_SCENE_GRAPH);
    expect(scene).toBeUndefined();
  });

  it('finds scene_two by id', () => {
    const scene = findScene('scene_two', TEST_SCENE_GRAPH);
    expect(scene?.id).toBe('scene_two');
  });
});

// ============================================================================
// getCurrentScene
// ============================================================================

describe('getCurrentScene', () => {
  it('returns CurrentSceneResult for a valid currentSceneId', () => {
    const ns = makeNarrativeState({ currentSceneId: 'scene_opening' });
    const p = makePersonality({});
    const result = getCurrentScene(ns, p, TEST_SCENE_GRAPH);
    expect(result).not.toBeNull();
    expect(result?.scene.id).toBe('scene_opening');
  });

  it('returns null when currentSceneId is not in the graph', () => {
    const ns = makeNarrativeState({ currentSceneId: 'scene_nonexistent' });
    const p = makePersonality({});
    const result = getCurrentScene(ns, p, TEST_SCENE_GRAPH);
    expect(result).toBeNull();
  });

  it('returns filtered availableChoices based on personality', () => {
    const ns = makeNarrativeState({ currentSceneId: 'scene_opening' });
    const lowPersonality = makePersonality({ charisma: 10 });
    const result = getCurrentScene(ns, lowPersonality, TEST_SCENE_GRAPH);
    // Only ungated choice available
    expect(result?.availableChoices).toHaveLength(1);
    expect(result?.availableChoices[0].id).toBe('choice_cautious');
  });

  it('returns all choices when personality passes all gates', () => {
    const ns = makeNarrativeState({ currentSceneId: 'scene_opening' });
    const highPersonality = makePersonality({ charisma: 25 });
    const result = getCurrentScene(ns, highPersonality, TEST_SCENE_GRAPH);
    expect(result?.availableChoices).toHaveLength(2);
  });
});

// ============================================================================
// validateSceneGraph
// ============================================================================

describe('validateSceneGraph', () => {
  it('returns valid: true for a graph with no dead ends', () => {
    const result = validateSceneGraph(TEST_SCENE_GRAPH);
    expect(result.valid).toBe(true);
    expect(result.problematicScenes).toHaveLength(0);
  });

  it('returns valid: false when a scene has only gated choices', () => {
    const result = validateSceneGraph(DEAD_END_SCENE_GRAPH);
    expect(result.valid).toBe(false);
    expect(result.problematicScenes).toContain('scene_dead_end');
  });

  it('returns valid: true for an empty scene graph', () => {
    const result = validateSceneGraph([]);
    expect(result.valid).toBe(true);
    expect(result.problematicScenes).toHaveLength(0);
  });

  it('identifies exactly the dead-end scenes', () => {
    const result = validateSceneGraph(DEAD_END_SCENE_GRAPH);
    expect(result.problematicScenes).toEqual(['scene_dead_end']);
  });

  it('returns valid: false for multiple dead-end scenes', () => {
    const multiDeadEnd = [
      ...DEAD_END_SCENE_GRAPH,
      {
        id: 'scene_dead_end_2',
        title: 'Another Lock',
        text: 'Another locked chamber.',
        prerequisites: [],
        choices: [
          {
            id: 'choice_locked',
            text: 'Only the worthy...',
            gate: { trait: 'logic' as const, operator: 'gte' as const, value: 35 },
            nextSceneId: null,
          },
        ],
      },
    ];
    const result = validateSceneGraph(multiDeadEnd);
    expect(result.valid).toBe(false);
    expect(result.problematicScenes).toHaveLength(2);
  });
});

// ============================================================================
// getAccessibleScenes
// ============================================================================

describe('getAccessibleScenes', () => {
  it('returns all scenes with no prerequisites when state is fresh', () => {
    const p = makePersonality({});
    const ns = makeNarrativeState({ currentSceneId: 'scene_prereq_start', visitedSceneIds: ['scene_prereq_start'] });
    const accessible = getAccessibleScenes(PREREQUISITE_SCENE_GRAPH, p, ns);
    // scene_prereq_start has no prerequisites -> accessible
    // scene_prereq_trait_gate requires patience gte 20 -> not with patience=16
    // scene_prereq_flag_gate requires flag 'unlocked' -> not set
    // scene_prereq_visited_gate requires visited 'scene_prereq_start' -> visited
    const ids = accessible.map(s => s.id);
    expect(ids).toContain('scene_prereq_start');
    expect(ids).toContain('scene_prereq_visited_gate'); // visited_scene prereq satisfied
    expect(ids).not.toContain('scene_prereq_trait_gate'); // patience too low
    expect(ids).not.toContain('scene_prereq_flag_gate'); // flag not set
  });

  it('returns trait-gated scene when personality meets the threshold', () => {
    const p = makePersonality({ patience: 25 });
    const ns = makeNarrativeState({ currentSceneId: 'scene_prereq_start', visitedSceneIds: ['scene_prereq_start'] });
    const accessible = getAccessibleScenes(PREREQUISITE_SCENE_GRAPH, p, ns);
    const ids = accessible.map(s => s.id);
    expect(ids).toContain('scene_prereq_trait_gate');
  });

  it('returns flag-gated scene when flag is set', () => {
    const p = makePersonality({});
    const ns = makeNarrativeState({
      currentSceneId: 'scene_prereq_start',
      visitedSceneIds: ['scene_prereq_start'],
      choiceFlags: { unlocked: true },
    });
    const accessible = getAccessibleScenes(PREREQUISITE_SCENE_GRAPH, p, ns);
    const ids = accessible.map(s => s.id);
    expect(ids).toContain('scene_prereq_flag_gate');
  });

  it('returns all scenes when all prerequisites are satisfied', () => {
    const p = makePersonality({ patience: 25 });
    const ns = makeNarrativeState({
      currentSceneId: 'scene_prereq_start',
      visitedSceneIds: ['scene_prereq_start'],
      choiceFlags: { unlocked: true },
    });
    const accessible = getAccessibleScenes(PREREQUISITE_SCENE_GRAPH, p, ns);
    expect(accessible).toHaveLength(PREREQUISITE_SCENE_GRAPH.length);
  });
});
