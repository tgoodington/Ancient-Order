/**
 * Tests for the persistence layer (saveLoad.ts).
 *
 * Uses a temp directory per test to ensure isolation.
 * fs.mkdtemp creates a unique dir; afterEach removes it.
 */

import * as fs from 'fs/promises';
import * as os from 'os';
import * as path from 'path';
import { describe, it, expect, beforeEach, afterEach } from 'vitest';

import {
  saveGame,
  loadGame,
  listSaves,
  deleteSave,
  validateGameState,
  SAVES_DIRECTORY,
  MIN_SLOT,
  MAX_SLOT,
} from './saveLoad.js';
import { ErrorCodes, GameState } from '../types/index.js';

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
    timestamp: 1_700_000_000_000,
  };
}

// ============================================================================
// Test Helpers
// ============================================================================

let tempDir: string;

beforeEach(async () => {
  // Create a fresh temp directory for each test
  tempDir = await fs.mkdtemp(path.join(os.tmpdir(), 'ao-test-'));
});

afterEach(async () => {
  // Remove the temp directory and all contents after each test
  await fs.rm(tempDir, { recursive: true, force: true });
});

// ============================================================================
// Constants
// ============================================================================

describe('constants', () => {
  it('exports SAVES_DIRECTORY as "saves"', () => {
    expect(SAVES_DIRECTORY).toBe('saves');
  });

  it('exports MIN_SLOT as 1', () => {
    expect(MIN_SLOT).toBe(1);
  });

  it('exports MAX_SLOT as 10', () => {
    expect(MAX_SLOT).toBe(10);
  });
});

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
// saveGame / loadGame — Round-Trip Fidelity
// ============================================================================

describe('saveGame + loadGame (round-trip)', () => {
  it('saves and loads state with deep equality', async () => {
    const state = makeGameState();
    await saveGame(state, 1, tempDir);
    const loaded = await loadGame(1, tempDir);
    expect(loaded).toEqual(state);
  });

  it('returned SaveMetadata contains correct slot and playerName', async () => {
    const state = makeGameState('Kira');
    const meta = await saveGame(state, 3, tempDir);
    expect(meta.slot).toBe(3);
    expect(meta.playerName).toBe('Kira');
    expect(typeof meta.timestamp).toBe('number');
  });

  it('save file is valid JSON', async () => {
    const state = makeGameState();
    await saveGame(state, 2, tempDir);
    const raw = await fs.readFile(path.join(tempDir, 'slot_2.json'), 'utf-8');
    expect(() => JSON.parse(raw)).not.toThrow();
  });

  it('overwrites an existing save in the same slot', async () => {
    const state1 = makeGameState('First');
    const state2 = makeGameState('Second');
    await saveGame(state1, 1, tempDir);
    await saveGame(state2, 1, tempDir);
    const loaded = await loadGame(1, tempDir);
    expect(loaded.player.name).toBe('Second');
  });
});

// ============================================================================
// Slot Independence
// ============================================================================

describe('slot independence', () => {
  it('saves to different slots independently', async () => {
    const stateA = makeGameState('Alice');
    const stateB = makeGameState('Bob');
    await saveGame(stateA, 1, tempDir);
    await saveGame(stateB, 2, tempDir);

    const loadedA = await loadGame(1, tempDir);
    const loadedB = await loadGame(2, tempDir);

    expect(loadedA.player.name).toBe('Alice');
    expect(loadedB.player.name).toBe('Bob');
  });

  it('saving slot 5 does not affect slot 6', async () => {
    const stateA = makeGameState('Slot5');
    const stateB = makeGameState('Slot6');
    await saveGame(stateA, 5, tempDir);
    await saveGame(stateB, 6, tempDir);

    const loaded5 = await loadGame(5, tempDir);
    const loaded6 = await loadGame(6, tempDir);

    expect(loaded5.player.name).toBe('Slot5');
    expect(loaded6.player.name).toBe('Slot6');
  });

  it('supports all 10 slots independently', async () => {
    const states: GameState[] = [];
    for (let slot = 1; slot <= 10; slot++) {
      const s = makeGameState(`Player${slot}`);
      states.push(s);
      await saveGame(s, slot, tempDir);
    }
    for (let slot = 1; slot <= 10; slot++) {
      const loaded = await loadGame(slot, tempDir);
      expect(loaded.player.name).toBe(`Player${slot}`);
    }
  });
});

// ============================================================================
// Error Handling — Missing Slot
// ============================================================================

describe('loadGame — missing slot error', () => {
  it('throws when loading a slot that has no file', async () => {
    await expect(loadGame(1, tempDir)).rejects.toThrow();
  });

  it('thrown error has code SAVE_NOT_FOUND', async () => {
    try {
      await loadGame(1, tempDir);
      expect.fail('Expected an error to be thrown');
    } catch (err) {
      expect((err as { code: string }).code).toBe(ErrorCodes.SAVE_NOT_FOUND);
    }
  });

  it('throws SAVE_NOT_FOUND for any missing slot (slot 7)', async () => {
    try {
      await loadGame(7, tempDir);
      expect.fail('Expected an error to be thrown');
    } catch (err) {
      expect((err as { code: string }).code).toBe(ErrorCodes.SAVE_NOT_FOUND);
    }
  });
});

// ============================================================================
// Error Handling — Invalid Slot
// ============================================================================

describe('invalid slot errors', () => {
  it('saveGame throws INVALID_SLOT for slot 0', async () => {
    try {
      await saveGame(makeGameState(), 0, tempDir);
      expect.fail('Expected an error to be thrown');
    } catch (err) {
      expect((err as { code: string }).code).toBe(ErrorCodes.INVALID_SLOT);
    }
  });

  it('saveGame throws INVALID_SLOT for slot 11', async () => {
    try {
      await saveGame(makeGameState(), 11, tempDir);
      expect.fail('Expected an error to be thrown');
    } catch (err) {
      expect((err as { code: string }).code).toBe(ErrorCodes.INVALID_SLOT);
    }
  });

  it('saveGame throws INVALID_SLOT for negative slots', async () => {
    try {
      await saveGame(makeGameState(), -1, tempDir);
      expect.fail('Expected an error to be thrown');
    } catch (err) {
      expect((err as { code: string }).code).toBe(ErrorCodes.INVALID_SLOT);
    }
  });

  it('saveGame throws INVALID_SLOT for fractional slots', async () => {
    try {
      await saveGame(makeGameState(), 1.5, tempDir);
      expect.fail('Expected an error to be thrown');
    } catch (err) {
      expect((err as { code: string }).code).toBe(ErrorCodes.INVALID_SLOT);
    }
  });

  it('loadGame throws INVALID_SLOT for slot 0', async () => {
    try {
      await loadGame(0, tempDir);
      expect.fail('Expected an error to be thrown');
    } catch (err) {
      expect((err as { code: string }).code).toBe(ErrorCodes.INVALID_SLOT);
    }
  });

  it('deleteSave throws INVALID_SLOT for slot 11', async () => {
    try {
      await deleteSave(11, tempDir);
      expect.fail('Expected an error to be thrown');
    } catch (err) {
      expect((err as { code: string }).code).toBe(ErrorCodes.INVALID_SLOT);
    }
  });
});

// ============================================================================
// listSaves
// ============================================================================

describe('listSaves', () => {
  it('returns 10 entries — one per slot', async () => {
    const infos = await listSaves(tempDir);
    expect(infos).toHaveLength(10);
  });

  it('all slots are empty when directory is freshly created', async () => {
    const infos = await listSaves(tempDir);
    expect(infos.every((i) => !i.exists)).toBe(true);
  });

  it('reflects saved slots correctly', async () => {
    await saveGame(makeGameState('Player1'), 1, tempDir);
    await saveGame(makeGameState('Player5'), 5, tempDir);

    const infos = await listSaves(tempDir);
    const slot1 = infos.find((i) => i.slot === 1);
    const slot5 = infos.find((i) => i.slot === 5);
    const slot3 = infos.find((i) => i.slot === 3);

    expect(slot1?.exists).toBe(true);
    expect(slot1?.metadata?.playerName).toBe('Player1');
    expect(slot5?.exists).toBe(true);
    expect(slot5?.metadata?.playerName).toBe('Player5');
    expect(slot3?.exists).toBe(false);
    expect(slot3?.metadata).toBeUndefined();
  });

  it('slot entries are ordered 1–10', async () => {
    const infos = await listSaves(tempDir);
    const slots = infos.map((i) => i.slot);
    expect(slots).toEqual([1, 2, 3, 4, 5, 6, 7, 8, 9, 10]);
  });
});

// ============================================================================
// deleteSave
// ============================================================================

describe('deleteSave', () => {
  it('deletes an existing save', async () => {
    await saveGame(makeGameState(), 1, tempDir);
    await deleteSave(1, tempDir);

    // Slot should now be missing
    await expect(loadGame(1, tempDir)).rejects.toThrow();
  });

  it('throws SAVE_NOT_FOUND when deleting a non-existent slot', async () => {
    try {
      await deleteSave(1, tempDir);
      expect.fail('Expected an error to be thrown');
    } catch (err) {
      expect((err as { code: string }).code).toBe(ErrorCodes.SAVE_NOT_FOUND);
    }
  });

  it('does not affect other slots when one is deleted', async () => {
    await saveGame(makeGameState('A'), 1, tempDir);
    await saveGame(makeGameState('B'), 2, tempDir);
    await deleteSave(1, tempDir);

    const loaded2 = await loadGame(2, tempDir);
    expect(loaded2.player.name).toBe('B');
  });

  it('deleted slot shows as missing in listSaves', async () => {
    await saveGame(makeGameState(), 3, tempDir);
    await deleteSave(3, tempDir);

    const infos = await listSaves(tempDir);
    const slot3 = infos.find((i) => i.slot === 3);
    expect(slot3?.exists).toBe(false);
  });
});

// ============================================================================
// Directory Creation
// ============================================================================

describe('directory creation', () => {
  it('saveGame creates the saves directory if it does not exist', async () => {
    const nested = path.join(tempDir, 'deep', 'saves-dir');
    await saveGame(makeGameState(), 1, nested);
    const loaded = await loadGame(1, nested);
    expect(loaded.player.name).toBe('Rin');
  });

  it('listSaves creates the saves directory if it does not exist', async () => {
    const nested = path.join(tempDir, 'new-dir');
    const infos = await listSaves(nested);
    expect(infos).toHaveLength(10);
    // Directory should now exist
    const stat = await fs.stat(nested);
    expect(stat.isDirectory()).toBe(true);
  });
});

// ============================================================================
// Narrative State Persistence
// ============================================================================

describe('narrativeState persistence', () => {
  it('saves and loads a state with narrativeState: null', async () => {
    const state = makeGameState();
    await saveGame(state, 1, tempDir);
    const loaded = await loadGame(1, tempDir);
    expect(loaded.narrativeState).toBeNull();
  });

  it('saves and loads a state with a full NarrativeState (round-trip)', async () => {
    const state: GameState = {
      ...makeGameState(),
      narrativeState: {
        currentSceneId: 'scene_two',
        visitedSceneIds: ['scene_opening', 'scene_two'],
        choiceFlags: { cautious_opening: true },
        sceneHistory: [
          { sceneId: 'scene_opening', choiceId: 'choice_cautious', timestamp: 1700000001000 },
        ],
      },
    };
    await saveGame(state, 2, tempDir);
    const loaded = await loadGame(2, tempDir);
    expect(loaded.narrativeState).not.toBeNull();
    expect(loaded.narrativeState?.currentSceneId).toBe('scene_two');
    expect(loaded.narrativeState?.visitedSceneIds).toContain('scene_opening');
    expect(loaded.narrativeState?.visitedSceneIds).toContain('scene_two');
    expect(loaded.narrativeState?.choiceFlags['cautious_opening']).toBe(true);
    expect(loaded.narrativeState?.sceneHistory).toHaveLength(1);
  });

  it('preserves choice flags across save/load', async () => {
    const state: GameState = {
      ...makeGameState(),
      narrativeState: {
        currentSceneId: 'scene_ending',
        visitedSceneIds: ['scene_opening', 'scene_two', 'scene_ending'],
        choiceFlags: { brave_opening: true, flag_two: true },
        sceneHistory: [],
      },
    };
    await saveGame(state, 3, tempDir);
    const loaded = await loadGame(3, tempDir);
    expect(loaded.narrativeState?.choiceFlags['brave_opening']).toBe(true);
    expect(loaded.narrativeState?.choiceFlags['flag_two']).toBe(true);
  });

  it('loads an old save (missing narrativeState) and normalizes to null', async () => {
    // Write a JSON file that does not include narrativeState (simulates Sprint 1/2 save)
    const oldSave = {
      player: {
        id: 'player-old',
        name: 'OldPlayer',
        personality: {
          patience: 16.67, empathy: 16.67, cunning: 16.67,
          logic: 16.67, kindness: 16.67, charisma: 16.65,
        },
      },
      npcs: {
        npc_scout_elena: {
          id: 'npc_scout_elena',
          archetype: 'Loyal Scout',
          personality: { patience: 25, empathy: 30, cunning: 10, logic: 15, kindness: 15, charisma: 5 },
          affection: 0,
          trust: 0,
        },
      },
      currentDialogueNode: null,
      saveSlot: null,
      combatState: null,
      // narrativeState intentionally omitted
      conversationLog: [],
      timestamp: 1_600_000_000_000,
    };
    const filePath = path.join(tempDir, 'slot_4.json');
    await fs.writeFile(filePath, JSON.stringify(oldSave, null, 2), 'utf-8');

    const loaded = await loadGame(4, tempDir);
    // narrativeState should be normalized to null (not undefined)
    expect(loaded.narrativeState).toBeNull();
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
    const { team: _t, ...stateWithoutTeam } = makeGameState() as Record<string, unknown>;
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
