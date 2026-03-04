/**
 * Ancient Order - Choice Engine Tests
 *
 * Coverage:
 *   - validateChoice: found/not-found, gated pass/fail
 *   - applyConsequence: personality effect, NPC effects, flags, no effects, pure function
 *   - processSceneChoice: valid choice, not-found error, gated error, state updated
 */

import { describe, it, expect, beforeEach } from 'vitest';

import {
  validateChoice,
  applyConsequence,
  processSceneChoice,
} from './choiceEngine.js';

import { TEST_SCENE_GRAPH } from './fixtures.js';
import { createNewGameState } from '../state/gameState.js';
import type { GameState, Personality } from '../types/index.js';
import type { Scene, ChoiceConsequence } from '../types/narrative.js';

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

let baseState: GameState;

beforeEach(() => {
  baseState = createNewGameState();
});

// The opening scene has: choice_bold (gate: charisma gte 20) and choice_cautious (ungated)
const openingScene: Scene = TEST_SCENE_GRAPH[0]!;

// ============================================================================
// validateChoice
// ============================================================================

describe('validateChoice', () => {
  it('returns valid: true for an ungated choice', () => {
    const p = makePersonality({ charisma: 5 }); // low charisma, still ungated passes
    const result = validateChoice(openingScene, 'choice_cautious', p);
    expect(result.valid).toBe(true);
    if (result.valid) {
      expect(result.choice.id).toBe('choice_cautious');
    }
  });

  it('returns valid: true for a gated choice when gate passes', () => {
    const p = makePersonality({ charisma: 25 });
    const result = validateChoice(openingScene, 'choice_bold', p);
    expect(result.valid).toBe(true);
  });

  it('returns valid: false with CHOICE_NOT_FOUND for a nonexistent choice', () => {
    const p = makePersonality({});
    const result = validateChoice(openingScene, 'choice_nonexistent', p);
    expect(result.valid).toBe(false);
    if (!result.valid) {
      expect(result.code).toBe('CHOICE_NOT_FOUND');
    }
  });

  it('returns valid: false with CHOICE_NOT_AVAILABLE when gate fails', () => {
    const p = makePersonality({ charisma: 10 }); // below gate: gte 20
    const result = validateChoice(openingScene, 'choice_bold', p);
    expect(result.valid).toBe(false);
    if (!result.valid) {
      expect(result.code).toBe('CHOICE_NOT_AVAILABLE');
    }
  });

  it('error message includes choice id on CHOICE_NOT_FOUND', () => {
    const p = makePersonality({});
    const result = validateChoice(openingScene, 'missing_id', p);
    expect(result.valid).toBe(false);
    if (!result.valid) {
      expect(result.message).toContain('missing_id');
    }
  });

  it('error message includes choice id on CHOICE_NOT_AVAILABLE', () => {
    const p = makePersonality({ charisma: 10 });
    const result = validateChoice(openingScene, 'choice_bold', p);
    expect(result.valid).toBe(false);
    if (!result.valid) {
      expect(result.message).toContain('choice_bold');
    }
  });
});

// ============================================================================
// applyConsequence
// ============================================================================

describe('applyConsequence', () => {
  it('returns a new object (reference inequality)', () => {
    const consequence: ChoiceConsequence = {};
    const updated = applyConsequence(baseState, consequence);
    expect(updated).not.toBe(baseState);
  });

  it('applies personality effect when present', () => {
    const consequence: ChoiceConsequence = {
      personalityEffect: { charisma: 3 },
    };
    const updated = applyConsequence(baseState, consequence);
    expect(updated.player.personality.charisma).toBeGreaterThan(baseState.player.personality.charisma);
  });

  it('does not change personality when personalityEffect is absent', () => {
    const consequence: ChoiceConsequence = {};
    const updated = applyConsequence(baseState, consequence);
    expect(updated.player.personality).toEqual(baseState.player.personality);
  });

  it('does not change personality when personalityEffect has no keys', () => {
    const consequence: ChoiceConsequence = {
      personalityEffect: {},
    };
    const updated = applyConsequence(baseState, consequence);
    expect(updated.player.personality).toEqual(baseState.player.personality);
  });

  it('applies NPC affection change when npcEffects is present', () => {
    const npcId = 'npc_scout_elena';
    const originalAffection = baseState.npcs[npcId]!.affection;
    const consequence: ChoiceConsequence = {
      npcEffects: [{ npcId, affectionChange: 10 }],
    };
    const updated = applyConsequence(baseState, consequence);
    expect(updated.npcs[npcId]!.affection).toBe(originalAffection + 10);
  });

  it('applies NPC trust change when npcEffects is present', () => {
    const npcId = 'npc_scout_elena';
    const originalTrust = baseState.npcs[npcId]!.trust;
    const consequence: ChoiceConsequence = {
      npcEffects: [{ npcId, trustChange: 5 }],
    };
    const updated = applyConsequence(baseState, consequence);
    expect(updated.npcs[npcId]!.trust).toBe(originalTrust + 5);
  });

  it('applies both affection and trust changes', () => {
    const npcId = 'npc_scout_elena';
    const originalAffection = baseState.npcs[npcId]!.affection;
    const originalTrust = baseState.npcs[npcId]!.trust;
    const consequence: ChoiceConsequence = {
      npcEffects: [{ npcId, affectionChange: 10, trustChange: 5 }],
    };
    const updated = applyConsequence(baseState, consequence);
    expect(updated.npcs[npcId]!.affection).toBe(originalAffection + 10);
    expect(updated.npcs[npcId]!.trust).toBe(originalTrust + 5);
  });

  it('applies multiple NPC effects', () => {
    const consequence: ChoiceConsequence = {
      npcEffects: [
        { npcId: 'npc_scout_elena', trustChange: 5 },
        { npcId: 'npc_merchant_lars', affectionChange: -3 },
      ],
    };
    const updated = applyConsequence(baseState, consequence);
    expect(updated.npcs['npc_scout_elena']!.trust).toBe(baseState.npcs['npc_scout_elena']!.trust + 5);
    expect(updated.npcs['npc_merchant_lars']!.affection).toBe(baseState.npcs['npc_merchant_lars']!.affection - 3);
  });

  it('applies both personality effect and NPC effects together', () => {
    const npcId = 'npc_scout_elena';
    const consequence: ChoiceConsequence = {
      personalityEffect: { charisma: 2 },
      npcEffects: [{ npcId, trustChange: 5 }],
    };
    const updated = applyConsequence(baseState, consequence);
    expect(updated.player.personality.charisma).toBeGreaterThan(baseState.player.personality.charisma);
    expect(updated.npcs[npcId]!.trust).toBeGreaterThan(baseState.npcs[npcId]!.trust);
  });

  it('does not mutate the input state', () => {
    const originalPersonality = { ...baseState.player.personality };
    const originalAffection = baseState.npcs['npc_scout_elena']!.affection;
    const consequence: ChoiceConsequence = {
      personalityEffect: { charisma: 5 },
      npcEffects: [{ npcId: 'npc_scout_elena', affectionChange: 20 }],
    };
    applyConsequence(baseState, consequence);
    expect(baseState.player.personality).toEqual(originalPersonality);
    expect(baseState.npcs['npc_scout_elena']!.affection).toBe(originalAffection);
  });

  it('maintains personality sum of 100 after applying personality effect', () => {
    const consequence: ChoiceConsequence = {
      personalityEffect: { charisma: 5, patience: -2 },
    };
    const updated = applyConsequence(baseState, consequence);
    const sum = Object.values(updated.player.personality).reduce((s, v) => s + v, 0);
    expect(sum).toBeCloseTo(100, 1);
  });
});

// ============================================================================
// processSceneChoice
// ============================================================================

describe('processSceneChoice', () => {
  it('returns type: success for a valid ungated choice', () => {
    const result = processSceneChoice(baseState, openingScene, 'choice_cautious');
    expect(result.type).toBe('success');
  });

  it('returns type: success for a gated choice when personality passes', () => {
    const state = {
      ...baseState,
      player: {
        ...baseState.player,
        personality: makePersonality({ charisma: 25 }),
      },
    };
    const result = processSceneChoice(state, openingScene, 'choice_bold');
    expect(result.type).toBe('success');
  });

  it('returns the choice on success', () => {
    const result = processSceneChoice(baseState, openingScene, 'choice_cautious');
    if (result.type === 'success') {
      expect(result.choice.id).toBe('choice_cautious');
    }
  });

  it('returns updated state on success', () => {
    const result = processSceneChoice(baseState, openingScene, 'choice_cautious');
    if (result.type === 'success') {
      expect(result.state).not.toBe(baseState);
    }
  });

  it('returns type: error with CHOICE_NOT_FOUND for nonexistent choice', () => {
    const result = processSceneChoice(baseState, openingScene, 'nonexistent');
    expect(result.type).toBe('error');
    if (result.type === 'error') {
      expect(result.code).toBe('CHOICE_NOT_FOUND');
    }
  });

  it('returns type: error with CHOICE_NOT_AVAILABLE when gate fails', () => {
    const state = {
      ...baseState,
      player: {
        ...baseState.player,
        personality: makePersonality({ charisma: 10 }),
      },
    };
    const result = processSceneChoice(state, openingScene, 'choice_bold');
    expect(result.type).toBe('error');
    if (result.type === 'error') {
      expect(result.code).toBe('CHOICE_NOT_AVAILABLE');
    }
  });

  it('applies personality effects from choice consequence', () => {
    // choice_cautious has personalityEffect: { cunning: 2 }
    const result = processSceneChoice(baseState, openingScene, 'choice_cautious');
    if (result.type === 'success') {
      expect(result.state.player.personality.cunning).toBeGreaterThan(baseState.player.personality.cunning);
    }
  });

  it('does not mutate the input state', () => {
    const originalPersonality = { ...baseState.player.personality };
    processSceneChoice(baseState, openingScene, 'choice_cautious');
    expect(baseState.player.personality).toEqual(originalPersonality);
  });
});
