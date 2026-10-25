/**
 * Tests for validateGameState (persistence/validation.ts).
 */

import { describe, it, expect } from 'vitest';

import { validateGameState } from './validation.js';
import type { GameState } from '../types/index.js';

// ============================================================================
// Fixture: minimal valid GameState for round-trip tests
// ============================================================================

function makeGameState(playerName: string = 'Rin'): GameState {
  return {
    player: {
      id: 'player-1',
      name: playerName,
      personality: {
        patience: 16.67,
        empathy: 16.67,
        cunning: 16.67,
        logic: 16.67,
        kindness: 16.67,
        charisma: 16.65, // slight rounding — real normalization handles this
      },
    },
    npcs: {
      npc_scout_elena: {
        id: 'npc_scout_elena',
        archetype: 'Loyal Scout',
        personality: {
          patience: 25,
          empathy: 30,
          cunning: 10,
          logic: 15,
          kindness: 15,
          charisma: 5,
        },
        affection: 0,
        trust: 0,
      },
    },
    team: [],
    currentDialogueNode: null,
    saveSlot: null,
    combatState: null,
    narrativeState: null,
    conversationLog: [],
    reactionProgress: {},
    timestamp: 1_700_000_000_000,
  };
}

// ============================================================================
// validateGameState
// ============================================================================

describe('validateGameState', () => {
  it('accepts a valid GameState', () => {
    const result = validateGameState(makeGameState());
    expect(result.valid).toBe(true);
  });

  it('rejects null', () => {
    const result = validateGameState(null);
    expect(result.valid).toBe(false);
  });

  it('rejects a primitive', () => {
    expect(validateGameState(42).valid).toBe(false);
    expect(validateGameState('string').valid).toBe(false);
  });

  it('rejects missing player field', () => {
    const bad = { ...makeGameState(), player: undefined };
    expect(validateGameState(bad).valid).toBe(false);
  });

  it('rejects missing personality trait', () => {
    const state = makeGameState();
    const bad = {
      ...state,
      player: {
        ...state.player,
        personality: { patience: 16.67, empathy: 16.67, cunning: 16.67, logic: 16.67 },
      },
    };
    expect(validateGameState(bad).valid).toBe(false);
  });

  it('rejects missing timestamp', () => {
    const { timestamp: _ts, ...bad } = makeGameState() as unknown as Record<string, unknown>;
    expect(validateGameState(bad).valid).toBe(false);
  });

  it('accepts combatState: null', () => {
    expect(validateGameState({ ...makeGameState(), combatState: null }).valid).toBe(true);
  });

  it('accepts a valid combatState shape', () => {
    const validCombat = {
      round: 1,
      phase: 'PC_DECLARATION',
      status: 'active',
      playerParty: [],
      enemyParty: [],
      actionQueue: [],
      roundHistory: [],
    };
    expect(validateGameState({ ...makeGameState(), combatState: validCombat }).valid).toBe(true);
  });

  it('accepts npcs as empty Record', () => {
    expect(validateGameState({ ...makeGameState(), npcs: {} }).valid).toBe(true);
  });

  it('rejects npcs as an array', () => {
    expect(validateGameState({ ...makeGameState(), npcs: [] }).valid).toBe(false);
  });

  it('rejects an NPC missing the affection field', () => {
    const state = makeGameState();
    const bad = {
      ...state,
      npcs: {
        npc_scout_elena: {
          id: 'npc_scout_elena',
          archetype: 'Loyal Scout',
          personality: state.npcs['npc_scout_elena']!.personality,
          // affection intentionally omitted
          trust: 0,
        },
      },
    };
    expect(validateGameState(bad).valid).toBe(false);
  });
});

// ============================================================================
// Narrative State Persistence (validation checks)
// ============================================================================

describe('narrativeState persistence', () => {
  it('validateGameState rejects reactionProgress with a non-number rate', () => {
    const bad = {
      ...makeGameState(),
      reactionProgress: { player_1: { block: 'lots', dodge: 0, parry: 0 } },
    } as unknown;
    const result = validateGameState(bad);
    expect(result.valid).toBe(false);
  });

  it('validateGameState accepts narrativeState: null', () => {
    const state = makeGameState(); // narrativeState: null
    expect(validateGameState(state).valid).toBe(true);
  });

  it('validateGameState accepts a valid NarrativeState structure', () => {
    const state: GameState = {
      ...makeGameState(),
      narrativeState: {
        currentSceneId: 'scene_two',
        visitedSceneIds: ['scene_opening', 'scene_two'],
        choiceFlags: { test_flag: true },
        sceneHistory: [],
      },
    };
    expect(validateGameState(state).valid).toBe(true);
  });

  it('validateGameState rejects narrativeState with non-string currentSceneId', () => {
    const bad = {
      ...makeGameState(),
      narrativeState: {
        currentSceneId: 123,
        visitedSceneIds: [],
        choiceFlags: {},
        sceneHistory: [],
      },
    };
    expect(validateGameState(bad).valid).toBe(false);
  });

  it('validateGameState rejects narrativeState with non-array visitedSceneIds', () => {
    const bad = {
      ...makeGameState(),
      narrativeState: {
        currentSceneId: 'scene_opening',
        visitedSceneIds: 'not_an_array',
        choiceFlags: {},
        sceneHistory: [],
      },
    };
    expect(validateGameState(bad).valid).toBe(false);
  });

  it('validateGameState rejects narrativeState with non-object choiceFlags', () => {
    const bad = {
      ...makeGameState(),
      narrativeState: {
        currentSceneId: 'scene_opening',
        visitedSceneIds: [],
        choiceFlags: 'not_an_object',
        sceneHistory: [],
      },
    };
    expect(validateGameState(bad).valid).toBe(false);
  });
});

// ============================================================================
// validateGameState — personality depth (Phase 2)
// ============================================================================

describe('validateGameState — personality depth (Phase 2)', () => {
  it('rejects personality trait below minimum 5', () => {
    const state = makeGameState();
    const bad = {
      ...state,
      player: {
        ...state.player,
        personality: {
          patience: 3,
          empathy: 30,
          cunning: 17,
          logic: 17,
          kindness: 17,
          charisma: 16,
        },
      },
    };
    expect(validateGameState(bad).valid).toBe(false);
  });

  it('rejects personality trait above maximum 35', () => {
    const state = makeGameState();
    const bad = {
      ...state,
      player: {
        ...state.player,
        personality: {
          patience: 40,
          empathy: 15,
          cunning: 15,
          logic: 10,
          kindness: 10,
          charisma: 10,
        },
      },
    };
    expect(validateGameState(bad).valid).toBe(false);
  });

  it('rejects personality sum not equal to 100', () => {
    const state = makeGameState();
    const bad = {
      ...state,
      player: {
        ...state.player,
        personality: {
          patience: 15,
          empathy: 15,
          cunning: 15,
          logic: 15,
          kindness: 15,
          charisma: 15,
        },
      },
    };
    // Sum = 90, not 100
    expect(validateGameState(bad).valid).toBe(false);
  });

  it('returns specific error messages for personality failures', () => {
    const state = makeGameState();
    const bad = {
      ...state,
      player: {
        ...state.player,
        personality: {
          patience: 3,
          empathy: 30,
          cunning: 17,
          logic: 17,
          kindness: 17,
          charisma: 16,
        },
      },
    };
    const result = validateGameState(bad);
    expect(result.valid).toBe(false);
    if (!result.valid) {
      expect(result.errors.some((e) => e.includes('patience'))).toBe(true);
    }
  });

  it('accepts personality at boundary values (5 and 35)', () => {
    const state = makeGameState();
    const boundary = {
      ...state,
      player: {
        ...state.player,
        personality: {
          patience: 5,
          empathy: 35,
          cunning: 15,
          logic: 15,
          kindness: 15,
          charisma: 15,
        },
      },
    };
    // Sum = 100 exactly, all traits within [5, 35]
    expect(validateGameState(boundary).valid).toBe(true);
  });
});

// ============================================================================
// validateGameState — combat state depth (Phase 5)
// ============================================================================

describe('validateGameState — combat state depth (Phase 5)', () => {
  const validCombatBase = {
    round: 1,
    phase: 'PC_DECLARATION',
    status: 'active',
    playerParty: [],
    enemyParty: [],
    actionQueue: [],
    roundHistory: [],
  };

  it('rejects combatState with invalid phase value', () => {
    const bad = {
      ...makeGameState(),
      combatState: { ...validCombatBase, phase: 'INVALID_PHASE' },
    };
    expect(validateGameState(bad).valid).toBe(false);
  });

  it('rejects combatState with invalid status value', () => {
    const bad = {
      ...makeGameState(),
      combatState: { ...validCombatBase, status: 'unknown' },
    };
    expect(validateGameState(bad).valid).toBe(false);
  });

  it('rejects combatState with missing playerParty array', () => {
    const { playerParty: _pp, ...combatWithoutParty } = validCombatBase;
    const bad = {
      ...makeGameState(),
      combatState: combatWithoutParty,
    };
    expect(validateGameState(bad).valid).toBe(false);
  });

  it('rejects combatState with non-number round', () => {
    const bad = {
      ...makeGameState(),
      combatState: { ...validCombatBase, round: 'one' },
    };
    expect(validateGameState(bad).valid).toBe(false);
  });

  it('accepts combatState with all 5 valid CombatPhase values', () => {
    const phases = ['AI_DECISION', 'VISUAL_INFO', 'PC_DECLARATION', 'ACTION_RESOLUTION', 'PER_ATTACK'];
    for (const phase of phases) {
      const state = {
        ...makeGameState(),
        combatState: { ...validCombatBase, phase },
      };
      expect(validateGameState(state).valid).toBe(true);
    }
  });
});

// ============================================================================
// validateGameState — team validation (Phase 4)
// ============================================================================

describe('validateGameState — team validation (Phase 4)', () => {
  it('accepts team: [] (empty team)', () => {
    const state = { ...makeGameState(), team: [] };
    expect(validateGameState(state).valid).toBe(true);
  });

  it('accepts team with 2 string entries', () => {
    const state = {
      ...makeGameState(),
      team: ['npc_scout_elena', 'npc_merchant_lars'],
    };
    expect(validateGameState(state).valid).toBe(true);
  });

  it('rejects team with 1 entry', () => {
    const state = { ...makeGameState(), team: ['npc_scout_elena'] };
    expect(validateGameState(state).valid).toBe(false);
  });

  it('rejects team with 3 entries', () => {
    const state = { ...makeGameState(), team: ['a', 'b', 'c'] };
    expect(validateGameState(state).valid).toBe(false);
  });

  it('rejects team with non-string entries', () => {
    const bad = { ...makeGameState(), team: [1, 2] as unknown as string[] };
    expect(validateGameState(bad).valid).toBe(false);
  });

  it('accepts missing team field (backward compat)', () => {
    const { team: _t, ...stateWithoutTeam } = makeGameState() as unknown as Record<string, unknown>;
    expect(validateGameState(stateWithoutTeam).valid).toBe(true);
  });
});

// ============================================================================
// validateGameState — narrative deep validation (Phase 6)
// ============================================================================

describe('validateGameState — narrative deep validation (Phase 6)', () => {
  const validNarrativeBase = {
    currentSceneId: 'scene_opening',
    visitedSceneIds: ['scene_opening'],
    choiceFlags: { cautious_start: true },
    sceneHistory: [{ sceneId: 'scene_opening', choiceId: 'choice_cautious', timestamp: 1700000001000 }],
  };

  it('rejects narrativeState with non-boolean choiceFlags value', () => {
    const bad = {
      ...makeGameState(),
      narrativeState: {
        ...validNarrativeBase,
        choiceFlags: { flag: 'yes' },
      },
    };
    expect(validateGameState(bad).valid).toBe(false);
  });

  it('rejects narrativeState with non-string visitedSceneIds entry', () => {
    const bad = {
      ...makeGameState(),
      narrativeState: {
        ...validNarrativeBase,
        visitedSceneIds: [123] as unknown as string[],
      },
    };
    expect(validateGameState(bad).valid).toBe(false);
  });

  it('rejects narrativeState sceneHistory entry with non-string sceneId', () => {
    const bad = {
      ...makeGameState(),
      narrativeState: {
        ...validNarrativeBase,
        sceneHistory: [{ sceneId: 123, choiceId: 'c', timestamp: 1 }],
      },
    };
    expect(validateGameState(bad).valid).toBe(false);
  });

  it('rejects narrativeState sceneHistory entry with non-number timestamp', () => {
    const bad = {
      ...makeGameState(),
      narrativeState: {
        ...validNarrativeBase,
        sceneHistory: [{ sceneId: 's', choiceId: 'c', timestamp: 'bad' }],
      },
    };
    expect(validateGameState(bad).valid).toBe(false);
  });
});

// ============================================================================
// validateGameState — ValidationResult shape
// ============================================================================

describe('validateGameState — ValidationResult shape', () => {
  it('returns { valid: true } for valid state', () => {
    const result = validateGameState(makeGameState());
    expect(result).toEqual({ valid: true });
  });

  it('returns { valid: false, errors: [...] } with accumulated errors for multiple failures', () => {
    const bad = {
      ...makeGameState(),
      timestamp: 'not-a-number',
      conversationLog: 'not-an-array',
    };
    const result = validateGameState(bad);
    expect(result.valid).toBe(false);
    if (!result.valid) {
      expect(Array.isArray(result.errors)).toBe(true);
      expect(result.errors.length).toBeGreaterThan(1);
    }
  });
});
