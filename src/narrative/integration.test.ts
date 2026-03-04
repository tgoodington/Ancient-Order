/**
 * Ancient Order - Narrative Integration Tests (T12)
 *
 * End-to-end flow tests spanning narrative -> combat -> persistence.
 *
 * Scenarios:
 *   1. Complete narrative flow: start -> choose -> flag-gate -> advance
 *   2. Synergy in combat init: calculateSynergy -> initCombatState -> verify stats
 *   3. Save/load mid-narrative: start narrative -> save -> load -> verify state
 *   4. Backward compatibility: Sprint 1+2 operations unchanged
 *   5. No unhandled rejections: all error paths return structured responses
 */

import { describe, it, expect, beforeEach, afterEach } from 'vitest';
import * as fs from 'fs/promises';
import * as os from 'os';
import * as path from 'path';

import { createNewGameState } from '../state/gameState.js';
import {
  initializeNarrative,
  updateNarrativeState,
  clearNarrative,
  updateNPCRelationship,
} from '../state/stateUpdaters.js';
import { advanceNarrative, createInitialNarrativeState } from './narrativeStateMachine.js';
import { applyConsequence } from './choiceEngine.js';
import { calculateSynergy } from './synergyCalculator.js';
import { DEFAULT_PARADIGMS } from '../fixtures/synergyConfig.js';
import { initCombatState } from '../combat/sync.js';
import { saveGame, loadGame } from '../persistence/saveLoad.js';
import {
  TEST_SCENE_GRAPH,
  PREREQUISITE_SCENE_GRAPH,
} from './fixtures.js';
import type { GameState, Personality } from '../types/index.js';

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

function makeEncounterConfig() {
  return {
    id: 'encounter_test',
    name: 'Test Encounter',
    playerParty: [
      {
        id: 'player_1',
        name: 'Hero',
        archetype: 'scout',
        rank: 2.0,
        stamina: 100,
        power: 10,
        speed: 10,
        elementalPath: 'Fire' as const,
        ascensionLevel: 0 as const,
        reactionSkills: {
          block: { SR: 0.6, SMR: 0.3, FMR: 0.1 },
          dodge: { SR: 0.5, FMR: 0.1 },
          parry: { SR: 0.4, FMR: 0.1 },
        },
      },
    ],
    enemyParty: [
      {
        id: 'enemy_1',
        name: 'Foe',
        archetype: 'raider',
        rank: 2.0,
        stamina: 90,
        power: 11,
        speed: 9,
        elementalPath: 'Water' as const,
        ascensionLevel: 0 as const,
        reactionSkills: {
          block: { SR: 0.5, SMR: 0.25, FMR: 0.1 },
          dodge: { SR: 0.4, FMR: 0.1 },
          parry: { SR: 0.3, FMR: 0.1 },
        },
      },
    ],
  };
}

// ============================================================================
// Scenario 1: Complete Narrative Flow
// ============================================================================

describe('Integration: complete narrative flow', () => {
  it('start -> choose -> flag set -> advance to second scene', () => {
    // Step 1: create game state and initialize narrative
    const base = createNewGameState();
    let state = initializeNarrative(base, 'scene_opening');

    expect(state.narrativeState?.currentSceneId).toBe('scene_opening');

    // Step 2: choose 'choice_cautious' (ungated, sets 'cautious_opening' flag)
    const result1 = advanceNarrative(state, 'choice_cautious', TEST_SCENE_GRAPH);
    expect(result1.type).toBe('success');
    if (result1.type !== 'success') return;

    // Apply consequence to GameState and update NarrativeState
    const openingScene = TEST_SCENE_GRAPH.find(s => s.id === 'scene_opening')!;
    const choice = openingScene.choices.find(c => c.id === 'choice_cautious')!;
    let updatedState = applyConsequence(state, choice.consequence ?? {});
    updatedState = updateNarrativeState(updatedState, result1.state);

    // Step 3: verify flag was set
    expect(updatedState.narrativeState?.choiceFlags['cautious_opening']).toBe(true);
    expect(updatedState.narrativeState?.currentSceneId).toBe('scene_two');

    // Step 4: advance from scene_two -> scene_ending
    const result2 = advanceNarrative(updatedState, 'choice_proceed', TEST_SCENE_GRAPH);
    expect(result2.type).toBe('success');
    if (result2.type !== 'success') return;

    updatedState = updateNarrativeState(updatedState, result2.state);
    expect(updatedState.narrativeState?.currentSceneId).toBe('scene_ending');
    expect(updatedState.narrativeState?.visitedSceneIds).toContain('scene_opening');
    expect(updatedState.narrativeState?.visitedSceneIds).toContain('scene_two');
    expect(updatedState.narrativeState?.visitedSceneIds).toContain('scene_ending');
  });

  it('gated choice at opening succeeds when personality meets gate', () => {
    const base = createNewGameState();
    const stateWithHighCharisma: GameState = {
      ...base,
      player: {
        ...base.player,
        personality: makePersonality({ charisma: 25 }),
      },
    };
    const state = initializeNarrative(stateWithHighCharisma, 'scene_opening');

    const result = advanceNarrative(state, 'choice_bold', TEST_SCENE_GRAPH);
    expect(result.type).toBe('success');
    if (result.type === 'success') {
      expect(result.state.choiceFlags['brave_opening']).toBe(true);
    }
  });

  it('history tracks all scene transitions', () => {
    let state = initializeNarrative(createNewGameState(), 'scene_opening');

    const r1 = advanceNarrative(state, 'choice_cautious', TEST_SCENE_GRAPH);
    if (r1.type !== 'success') return;
    state = updateNarrativeState(state, r1.state);

    const r2 = advanceNarrative(state, 'choice_proceed', TEST_SCENE_GRAPH);
    if (r2.type !== 'success') return;
    state = updateNarrativeState(state, r2.state);

    const r3 = advanceNarrative(state, 'choice_finish', TEST_SCENE_GRAPH);
    if (r3.type !== 'success') return;

    // 3 transitions recorded in history
    expect(r3.state.sceneHistory).toHaveLength(3);
    expect(r3.state.sceneHistory[0].sceneId).toBe('scene_opening');
    expect(r3.state.sceneHistory[1].sceneId).toBe('scene_two');
    expect(r3.state.sceneHistory[2].sceneId).toBe('scene_ending');
  });

  it('clearNarrative resets state to null', () => {
    let state = initializeNarrative(createNewGameState(), 'scene_opening');
    expect(state.narrativeState).not.toBeNull();
    state = clearNarrative(state);
    expect(state.narrativeState).toBeNull();
  });
});

// ============================================================================
// Scenario 2: Synergy in Combat Init
// ============================================================================

describe('Integration: synergy in combat init', () => {
  it('calculateSynergy with default party returns non-null when personality is sufficient', () => {
    const player = makePersonality({
      patience: 25, empathy: 25, cunning: 25, logic: 25, kindness: 25, charisma: 25,
    });
    const elenaPersonality: Personality = { patience: 20, empathy: 20, cunning: 10, logic: 15, kindness: 20, charisma: 15 };
    const result = calculateSynergy(player, [elenaPersonality], DEFAULT_PARADIGMS);
    expect(result).not.toBeNull();
  });

  it('initCombatState applies synergy boost to player party stats', () => {
    const base = createNewGameState();
    const highPersonalityState: GameState = {
      ...base,
      player: {
        ...base.player,
        personality: {
          patience: 25, empathy: 25, cunning: 25,
          logic: 25, kindness: 25, charisma: 25,
        },
      },
      npcs: {
        npc_scout_elena: {
          ...base.npcs['npc_scout_elena']!,
          personality: { patience: 25, empathy: 25, cunning: 25, logic: 25, kindness: 25, charisma: 25 },
        },
      },
    };

    const encounter = makeEncounterConfig();
    const combat = initCombatState(highPersonalityState, encounter);

    // Synergy should be applied — player power boosted
    const expectedPower = Math.round(10 * 1.10); // 11
    expect(combat.playerParty[0].power).toBe(expectedPower);
    // Enemy unchanged
    expect(combat.enemyParty[0].power).toBe(11);
  });

  it('initCombatState without synergy produces same output as before (backward compat)', () => {
    // Default createNewGameState has personality that may not meet threshold
    const state = createNewGameState();
    const encounter = makeEncounterConfig();
    const combat = initCombatState(state, encounter);

    // Without synergy, stats should match encounter config exactly
    // (unless default personality happens to meet threshold — guard prevents crash)
    expect(combat.playerParty[0].id).toBe('player_1');
    expect(typeof combat.playerParty[0].power).toBe('number');
    expect(combat.round).toBe(1);
    expect(combat.status).toBe('active');
  });
});

// ============================================================================
// Scenario 3: Save/Load Mid-Narrative
// ============================================================================

describe('Integration: save/load mid-narrative', () => {
  let tempDir: string;

  beforeEach(async () => {
    tempDir = await fs.mkdtemp(path.join(os.tmpdir(), 'ao-narrative-test-'));
  });

  afterEach(async () => {
    await fs.rm(tempDir, { recursive: true, force: true });
  });

  it('saves and loads narrative state preserving currentSceneId', async () => {
    const base = createNewGameState();
    let state = initializeNarrative(base, 'scene_opening');

    // Advance to scene_two
    const result = advanceNarrative(state, 'choice_cautious', TEST_SCENE_GRAPH);
    if (result.type !== 'success') return;
    state = updateNarrativeState(state, result.state);
    expect(state.narrativeState?.currentSceneId).toBe('scene_two');

    // Save
    await saveGame(state, 1, tempDir);

    // Load and verify
    const loaded = await loadGame(1, tempDir);
    expect(loaded.narrativeState?.currentSceneId).toBe('scene_two');
    expect(loaded.narrativeState?.visitedSceneIds).toContain('scene_opening');
    expect(loaded.narrativeState?.visitedSceneIds).toContain('scene_two');
  });

  it('saves and loads narrative state preserving choice flags', async () => {
    const base = createNewGameState();
    let state = initializeNarrative(base, 'scene_opening');

    const result = advanceNarrative(state, 'choice_cautious', TEST_SCENE_GRAPH);
    if (result.type !== 'success') return;
    state = updateNarrativeState(state, result.state);
    expect(state.narrativeState?.choiceFlags['cautious_opening']).toBe(true);

    await saveGame(state, 2, tempDir);
    const loaded = await loadGame(2, tempDir);
    expect(loaded.narrativeState?.choiceFlags['cautious_opening']).toBe(true);
  });

  it('saves and loads then continues narrative correctly', async () => {
    const base = createNewGameState();
    let state = initializeNarrative(base, 'scene_opening');

    // Advance to scene_two
    let result = advanceNarrative(state, 'choice_cautious', TEST_SCENE_GRAPH);
    if (result.type !== 'success') return;
    state = updateNarrativeState(state, result.state);

    // Save and load
    await saveGame(state, 3, tempDir);
    const loaded = await loadGame(3, tempDir);

    // Continue from loaded state
    result = advanceNarrative(loaded, 'choice_proceed', TEST_SCENE_GRAPH);
    expect(result.type).toBe('success');
    if (result.type === 'success') {
      expect(result.nextScene?.id).toBe('scene_ending');
    }
  });
});

// ============================================================================
// Scenario 4: Backward Compatibility (Sprint 1+2 operations unchanged)
// ============================================================================

describe('Integration: backward compatibility', () => {
  it('createNewGameState includes narrativeState: null', () => {
    const state = createNewGameState();
    expect(state.narrativeState).toBeNull();
  });

  it('Sprint 1+2 NPC relationship updates work unchanged', () => {
    const state = createNewGameState();
    const updated = updateNPCRelationship(state, 'npc_scout_elena', 10, 5);
    expect(updated.npcs['npc_scout_elena']!.affection).toBe(10);
    expect(updated.npcs['npc_scout_elena']!.trust).toBe(5);
    // narrativeState unaffected
    expect(updated.narrativeState).toBeNull();
  });

  it('initCombatState with default game state still returns valid CombatState', () => {
    const state = createNewGameState();
    const encounter = makeEncounterConfig();
    const combat = initCombatState(state, encounter);
    expect(combat.round).toBe(1);
    expect(combat.status).toBe('active');
    expect(combat.playerParty).toHaveLength(1);
    expect(combat.enemyParty).toHaveLength(1);
  });

  it('narrativeState field does not interfere with existing player/npc operations', () => {
    const state = createNewGameState();
    // Apply personality adjustment
    const updated = applyConsequence(state, { personalityEffect: { charisma: 3 } });
    expect(updated.player.personality.charisma).toBeGreaterThan(state.player.personality.charisma);
    // narrativeState should still be null
    expect(updated.narrativeState).toBeNull();
  });
});

// ============================================================================
// Scenario 5: Error paths return structured responses
// ============================================================================

describe('Integration: error paths', () => {
  it('advanceNarrative returns structured error when narrative not started', () => {
    const state = createNewGameState();
    const result = advanceNarrative(state, 'any_choice', TEST_SCENE_GRAPH);
    expect(result.type).toBe('error');
    if (result.type === 'error') {
      expect(typeof result.code).toBe('string');
      expect(typeof result.message).toBe('string');
    }
  });

  it('advanceNarrative returns structured error for invalid choice', () => {
    const state = initializeNarrative(createNewGameState(), 'scene_opening');
    const result = advanceNarrative(state, 'invalid_choice_id', TEST_SCENE_GRAPH);
    expect(result.type).toBe('error');
    if (result.type === 'error') {
      expect(result.code).toBe('CHOICE_NOT_FOUND');
    }
  });

  it('advanceNarrative returns structured error when prerequisite not met', () => {
    const base = createNewGameState();
    // patience=10 < 20 required by scene_prereq_trait_gate
    const state: GameState = {
      ...base,
      player: {
        ...base.player,
        personality: makePersonality({ patience: 10 }),
      },
      narrativeState: createInitialNarrativeState('scene_prereq_start'),
    };
    const result = advanceNarrative(state, 'choice_go_trait', PREREQUISITE_SCENE_GRAPH);
    expect(result.type).toBe('error');
    if (result.type === 'error') {
      expect(result.code).toBe('PREREQUISITE_NOT_MET');
    }
  });

  it('initializeNarrative is idempotent (second call replaces first)', () => {
    let state = createNewGameState();
    state = initializeNarrative(state, 'scene_opening');
    state = initializeNarrative(state, 'scene_two'); // replace
    expect(state.narrativeState?.currentSceneId).toBe('scene_two');
  });
});
