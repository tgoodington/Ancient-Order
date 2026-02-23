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
    currentDialogueNode: null,
    saveSlot: null,
    combatState: null,
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
    expect(validateGameState(makeGameState())).toBe(true);
  });

  it('rejects null', () => {
    expect(validateGameState(null)).toBe(false);
  });

  it('rejects a primitive', () => {
    expect(validateGameState(42)).toBe(false);
    expect(validateGameState('string')).toBe(false);
  });

  it('rejects missing player field', () => {
    const bad = { ...makeGameState(), player: undefined };
    expect(validateGameState(bad)).toBe(false);
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
    expect(validateGameState(bad)).toBe(false);
  });

  it('rejects missing timestamp', () => {
    const { timestamp: _ts, ...bad } = makeGameState() as unknown as Record<string, unknown>;
    expect(validateGameState(bad)).toBe(false);
  });

  it('accepts combatState: null', () => {
    expect(validateGameState({ ...makeGameState(), combatState: null })).toBe(true);
  });

  it('accepts combatState as arbitrary unknown value (unknown | null)', () => {
    // combatState typed as unknown | null — any value should pass
    expect(validateGameState({ ...makeGameState(), combatState: { round: 1 } })).toBe(true);
  });

  it('accepts npcs as empty Record', () => {
    expect(validateGameState({ ...makeGameState(), npcs: {} })).toBe(true);
  });

  it('rejects npcs as an array', () => {
    expect(validateGameState({ ...makeGameState(), npcs: [] })).toBe(false);
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
    expect(validateGameState(bad)).toBe(false);
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
