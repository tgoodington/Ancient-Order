/**
 * Ancient Order - GameState-CombatState Synchronization Tests
 *
 * Tests for the three sync functions:
 *   1. initCombatState — produces valid CombatState with correct round/phase/status
 *   2. syncToGameState — preserves non-combat fields unchanged (personality, npcs, etc.)
 *   3. endCombat       — clears combatState and records result
 *   4. Save/load round-trip during combat preserves CombatState
 */

import { describe, it, expect, beforeEach, afterEach } from 'vitest';
import * as fs from 'fs/promises';
import * as os from 'os';
import * as path from 'path';
import { initCombatState, syncToGameState, endCombat } from './sync.js';
import { saveGame, loadGame } from '../persistence/saveLoad.js';
import { createNewGameState } from '../state/gameState.js';
import type { GameState } from '../types/index.js';
import type {
  CombatState,
  Combatant,
  CombatantConfig,
  EncounterConfig,
} from '../types/combat.js';

// ============================================================================
// Test Fixture Builders
// ============================================================================

/**
 * Builds a minimal CombatantConfig for use in EncounterConfig.
 */
function makeCombatantConfig(overrides: Partial<CombatantConfig> = {}): CombatantConfig {
  return {
    id: 'combatant_1',
    name: 'Test Fighter',
    archetype: 'scout',
    rank: 2.0,
    stamina: 100,
    power: 10,
    speed: 10,
    elementalPath: 'Fire',
    reactionSkills: {
      block: { SR: 0.6, SMR: 0.3, FMR: 0.1 },
      dodge: { SR: 0.5, FMR: 0.1 },
      parry: { SR: 0.4, FMR: 0.1 },
    },
    ascensionLevel: 0,
    ...overrides,
  };
}

/**
 * Builds a minimal valid EncounterConfig with 1 player and 1 enemy.
 */
function makeEncounterConfig(overrides: Partial<EncounterConfig> = {}): EncounterConfig {
  return {
    id: 'encounter_test',
    name: 'Test Encounter',
    playerParty: [
      makeCombatantConfig({ id: 'player_1', name: 'Hero', archetype: 'scout' }),
    ],
    enemyParty: [
      makeCombatantConfig({ id: 'enemy_1', name: 'Foe', archetype: 'raider' }),
    ],
    ...overrides,
  };
}

/**
 * Builds a 3v3 EncounterConfig for multi-combatant tests.
 */
function make3v3EncounterConfig(): EncounterConfig {
  return {
    id: 'encounter_3v3',
    name: '3v3 Encounter',
    playerParty: [
      makeCombatantConfig({ id: 'player_1', name: 'Hero',   archetype: 'scout',    rank: 2.0, stamina: 100, power: 12, speed: 10 }),
      makeCombatantConfig({ id: 'player_2', name: 'Mage',   archetype: 'scholar',  rank: 1.5, stamina: 80,  power: 14, speed: 8  }),
      makeCombatantConfig({ id: 'player_3', name: 'Brute',  archetype: 'warrior',  rank: 3.0, stamina: 120, power: 16, speed: 6  }),
    ],
    enemyParty: [
      makeCombatantConfig({ id: 'enemy_1', name: 'Raider 1', archetype: 'raider',   rank: 2.0, stamina: 90,  power: 11, speed: 9  }),
      makeCombatantConfig({ id: 'enemy_2', name: 'Raider 2', archetype: 'raider',   rank: 2.0, stamina: 85,  power: 10, speed: 11 }),
      makeCombatantConfig({ id: 'enemy_3', name: 'Raider 3', archetype: 'enforcer', rank: 2.5, stamina: 110, power: 13, speed: 7  }),
    ],
  };
}

/**
 * Builds a minimal valid CombatState for use in syncToGameState / endCombat tests.
 */
function makeCombatState(overrides: Partial<CombatState> = {}): CombatState {
  const player: Combatant = {
    id: 'player_1',
    name: 'Hero',
    archetype: 'scout',
    rank: 2.0,
    stamina: 75, // reduced from 100 — simulates combat damage
    maxStamina: 100,
    power: 12,
    speed: 10,
    energy: 1,
    maxEnergy: 3,
    ascensionLevel: 0,
    activeBuffs: [],
    elementalPath: 'Fire',
    reactionSkills: {
      block: { SR: 0.6, SMR: 0.3, FMR: 0.1 },
      dodge: { SR: 0.5, FMR: 0.1 },
      parry: { SR: 0.4, FMR: 0.1 },
    },
    isKO: false,
  };

  const enemy: Combatant = {
    id: 'enemy_1',
    name: 'Foe',
    archetype: 'raider',
    rank: 2.0,
    stamina: 50,
    maxStamina: 90,
    power: 11,
    speed: 9,
    energy: 0,
    maxEnergy: 3,
    ascensionLevel: 0,
    activeBuffs: [],
    elementalPath: 'Water',
    reactionSkills: {
      block: { SR: 0.5, SMR: 0.25, FMR: 0.1 },
      dodge: { SR: 0.4, FMR: 0.1 },
      parry: { SR: 0.3, FMR: 0.1 },
    },
    isKO: false,
  };

  return {
    round: 2,
    phase: 'AI_DECISION',
    playerParty: [player],
    enemyParty: [enemy],
    actionQueue: [],
    roundHistory: [],
    status: 'active',
    ...overrides,
  };
}

// ============================================================================
// 1. initCombatState Tests
// ============================================================================

describe('initCombatState', () => {
  it('produces a CombatState with round=1, phase=AI_DECISION, status=active', () => {
    const gameState = createNewGameState();
    const encounter = makeEncounterConfig();

    const combat = initCombatState(gameState, encounter);

    expect(combat.round).toBe(1);
    expect(combat.phase).toBe('AI_DECISION');
    expect(combat.status).toBe('active');
  });

  it('produces empty actionQueue and roundHistory', () => {
    const gameState = createNewGameState();
    const encounter = makeEncounterConfig();

    const combat = initCombatState(gameState, encounter);

    expect(combat.actionQueue).toHaveLength(0);
    expect(combat.roundHistory).toHaveLength(0);
  });

  it('maps playerParty from encounter config (1 player)', () => {
    const gameState = createNewGameState();
    const encounter = makeEncounterConfig();

    const combat = initCombatState(gameState, encounter);

    expect(combat.playerParty).toHaveLength(1);
    expect(combat.playerParty[0].id).toBe('player_1');
    expect(combat.playerParty[0].name).toBe('Hero');
    expect(combat.playerParty[0].isKO).toBe(false);
  });

  it('maps enemyParty from encounter config (1 enemy)', () => {
    const gameState = createNewGameState();
    const encounter = makeEncounterConfig();

    const combat = initCombatState(gameState, encounter);

    expect(combat.enemyParty).toHaveLength(1);
    expect(combat.enemyParty[0].id).toBe('enemy_1');
    expect(combat.enemyParty[0].name).toBe('Foe');
    expect(combat.enemyParty[0].isKO).toBe(false);
  });

  it('sets stamina equal to config stamina (maxStamina matches stamina)', () => {
    const gameState = createNewGameState();
    const encounter = makeEncounterConfig();

    const combat = initCombatState(gameState, encounter);

    const hero = combat.playerParty[0];
    expect(hero.stamina).toBe(100);
    expect(hero.maxStamina).toBe(100);
  });

  it('correctly maps 3v3 encounter with all combatants', () => {
    const gameState = createNewGameState();
    const encounter = make3v3EncounterConfig();

    const combat = initCombatState(gameState, encounter);

    expect(combat.playerParty).toHaveLength(3);
    expect(combat.enemyParty).toHaveLength(3);

    const ids = combat.playerParty.map((c) => c.id);
    expect(ids).toContain('player_1');
    expect(ids).toContain('player_2');
    expect(ids).toContain('player_3');
  });

  it('preserves combatant stats from config (rank, power, speed, archetype)', () => {
    const gameState = createNewGameState();
    const encounter = makeEncounterConfig({
      playerParty: [
        makeCombatantConfig({ id: 'p1', rank: 3.5, power: 20, speed: 15, archetype: 'warrior' }),
      ],
    });

    const combat = initCombatState(gameState, encounter);
    const hero = combat.playerParty[0];

    expect(hero.rank).toBe(3.5);
    expect(hero.power).toBe(20);
    expect(hero.speed).toBe(15);
    expect(hero.archetype).toBe('warrior');
  });

  it('initialises combatants with ascensionLevel defaulting to 0', () => {
    const gameState = createNewGameState();
    const config = makeCombatantConfig({ id: 'p1' });
    // Omit ascensionLevel — should default to 0
    const { ascensionLevel: _removed, ...configWithoutAscension } = config;
    const encounter = makeEncounterConfig({
      playerParty: [configWithoutAscension as CombatantConfig],
    });

    const combat = initCombatState(gameState, encounter);
    expect(combat.playerParty[0].ascensionLevel).toBe(0);
  });

  it('is a pure function: same inputs produce identical output', () => {
    const gameState = createNewGameState();
    const encounter = make3v3EncounterConfig();

    const combat1 = initCombatState(gameState, encounter);
    const combat2 = initCombatState(gameState, encounter);

    expect(combat1).toEqual(combat2);
  });

  it('does not mutate the input gameState', () => {
    const gameState = createNewGameState();
    const encounter = makeEncounterConfig();
    const originalTimestamp = gameState.timestamp;

    initCombatState(gameState, encounter);

    expect(gameState.timestamp).toBe(originalTimestamp);
    expect(gameState.combatState).toBeNull();
  });
});

// ============================================================================
// 2. syncToGameState Tests
// ============================================================================

describe('syncToGameState', () => {
  it('returns a new GameState with combatState set', () => {
    const gameState = createNewGameState();
    const combatState = makeCombatState();

    const synced = syncToGameState(gameState, combatState);

    expect(synced.combatState).not.toBeNull();
    expect(synced.combatState).toBe(combatState);
  });

  it('preserves player personality unchanged', () => {
    const gameState = createNewGameState();
    const combatState = makeCombatState();
    const originalPersonality = { ...gameState.player.personality };

    const synced = syncToGameState(gameState, combatState);

    expect(synced.player.personality).toEqual(originalPersonality);
  });

  it('preserves NPC data unchanged', () => {
    const gameState = createNewGameState();
    const combatState = makeCombatState();
    const npcIds = Object.keys(gameState.npcs);

    const synced = syncToGameState(gameState, combatState);

    for (const npcId of npcIds) {
      expect(synced.npcs[npcId]).toEqual(gameState.npcs[npcId]);
    }
  });

  it('preserves conversationLog unchanged', () => {
    const gameState = createNewGameState();
    const combatState = makeCombatState();

    const synced = syncToGameState(gameState, combatState);

    expect(synced.conversationLog).toEqual(gameState.conversationLog);
  });

  it('preserves player id and name unchanged', () => {
    const gameState = createNewGameState();
    const combatState = makeCombatState();

    const synced = syncToGameState(gameState, combatState);

    expect(synced.player.id).toBe(gameState.player.id);
    expect(synced.player.name).toBe(gameState.player.name);
  });

  it('preserves currentDialogueNode unchanged', () => {
    const gameState = createNewGameState();
    const combatState = makeCombatState();

    const synced = syncToGameState(gameState, combatState);

    expect(synced.currentDialogueNode).toBe(gameState.currentDialogueNode);
  });

  it('preserves saveSlot unchanged', () => {
    const gameState = createNewGameState();
    const combatState = makeCombatState();

    const synced = syncToGameState(gameState, combatState);

    expect(synced.saveSlot).toBe(gameState.saveSlot);
  });

  it('returns a different object reference (immutable)', () => {
    const gameState = createNewGameState();
    const combatState = makeCombatState();

    const synced = syncToGameState(gameState, combatState);

    expect(synced).not.toBe(gameState);
  });

  it('does not mutate the input gameState', () => {
    const gameState = createNewGameState();
    const combatState = makeCombatState();

    syncToGameState(gameState, combatState);

    expect(gameState.combatState).toBeNull();
  });

  it('reflects updated round number from combatState', () => {
    const gameState = createNewGameState();
    const combatState = makeCombatState({ round: 5 });

    const synced = syncToGameState(gameState, combatState);

    expect(synced.combatState?.round).toBe(5);
  });
});

// ============================================================================
// 3. endCombat Tests
// ============================================================================

describe('endCombat', () => {
  it('clears combatState to null on victory', () => {
    const gameState = createNewGameState();
    const withCombat = syncToGameState(gameState, makeCombatState());

    const ended = endCombat(withCombat, 'victory');

    expect(ended.combatState).toBeNull();
  });

  it('clears combatState to null on defeat', () => {
    const gameState = createNewGameState();
    const withCombat = syncToGameState(gameState, makeCombatState());

    const ended = endCombat(withCombat, 'defeat');

    expect(ended.combatState).toBeNull();
  });

  it('records the result in conversationLog', () => {
    const gameState = createNewGameState();
    const withCombat = syncToGameState(gameState, makeCombatState());

    const ended = endCombat(withCombat, 'victory');

    // The result should be traceable in the log
    const lastEntry = ended.conversationLog[ended.conversationLog.length - 1];
    expect(lastEntry).toBeDefined();
    expect(lastEntry.optionId).toBe('victory');
  });

  it('records defeat result in conversationLog', () => {
    const gameState = createNewGameState();
    const withCombat = syncToGameState(gameState, makeCombatState());

    const ended = endCombat(withCombat, 'defeat');

    const lastEntry = ended.conversationLog[ended.conversationLog.length - 1];
    expect(lastEntry).toBeDefined();
    expect(lastEntry.optionId).toBe('defeat');
  });

  it('preserves all non-combat fields after endCombat', () => {
    const gameState = createNewGameState();
    const withCombat = syncToGameState(gameState, makeCombatState());
    const originalPersonality = { ...gameState.player.personality };

    const ended = endCombat(withCombat, 'victory');

    expect(ended.player.personality).toEqual(originalPersonality);
    expect(ended.player.id).toBe(gameState.player.id);
    expect(ended.player.name).toBe(gameState.player.name);
  });

  it('returns a new object reference (immutable)', () => {
    const gameState = createNewGameState();
    const withCombat = syncToGameState(gameState, makeCombatState());

    const ended = endCombat(withCombat, 'victory');

    expect(ended).not.toBe(withCombat);
  });

  it('does not mutate the input gameState', () => {
    const gameState = createNewGameState();
    const withCombat = syncToGameState(gameState, makeCombatState());
    const origCombatState = withCombat.combatState;

    endCombat(withCombat, 'victory');

    expect(withCombat.combatState).toBe(origCombatState);
  });
});

// ============================================================================
// 4. Save/Load Round-Trip During Combat
// ============================================================================

describe('save/load round-trip during active combat', () => {
  let tempDir: string;

  beforeEach(async () => {
    tempDir = await fs.mkdtemp(path.join(os.tmpdir(), 'ancient-order-sync-test-'));
  });

  afterEach(async () => {
    await fs.rm(tempDir, { recursive: true, force: true });
  });

  it('preserves CombatState across a save/load cycle', async () => {
    // 1. Create a fresh game state and initialise an encounter
    const gameState = createNewGameState();
    const encounter = make3v3EncounterConfig();
    const combatState = initCombatState(gameState, encounter);

    // 2. Sync combat state back to game state
    const gameWithCombat = syncToGameState(gameState, combatState);

    // 3. Save to slot 1
    await saveGame(gameWithCombat, 1, tempDir);

    // 4. Load from slot 1
    const loaded = await loadGame(1, tempDir);

    // 5. Verify combatState is preserved
    expect(loaded.combatState).not.toBeNull();
    expect(loaded.combatState?.round).toBe(combatState.round);
    expect(loaded.combatState?.phase).toBe(combatState.phase);
    expect(loaded.combatState?.status).toBe(combatState.status);
  });

  it('preserves all combatant data across a save/load cycle', async () => {
    const gameState = createNewGameState();
    const encounter = make3v3EncounterConfig();
    const combatState = initCombatState(gameState, encounter);
    const gameWithCombat = syncToGameState(gameState, combatState);

    await saveGame(gameWithCombat, 2, tempDir);
    const loaded = await loadGame(2, tempDir);

    expect(loaded.combatState?.playerParty).toHaveLength(3);
    expect(loaded.combatState?.enemyParty).toHaveLength(3);

    // Spot-check a combatant's core stats
    const loadedHero = loaded.combatState?.playerParty[0];
    const originalHero = combatState.playerParty[0];
    expect(loadedHero?.id).toBe(originalHero.id);
    expect(loadedHero?.stamina).toBe(originalHero.stamina);
    expect(loadedHero?.maxStamina).toBe(originalHero.maxStamina);
    expect(loadedHero?.power).toBe(originalHero.power);
    expect(loadedHero?.speed).toBe(originalHero.speed);
  });

  it('preserves non-combat GameState fields across a save/load cycle', async () => {
    const gameState = createNewGameState();
    const encounter = make3v3EncounterConfig();
    const combatState = initCombatState(gameState, encounter);
    const gameWithCombat = syncToGameState(gameState, combatState);

    await saveGame(gameWithCombat, 3, tempDir);
    const loaded = await loadGame(3, tempDir);

    // Personality should be unchanged
    expect(loaded.player.personality).toEqual(gameState.player.personality);
    // NPCs should be unchanged
    expect(Object.keys(loaded.npcs).sort()).toEqual(Object.keys(gameState.npcs).sort());
  });

  it('allows endCombat after a save/load cycle', async () => {
    const gameState = createNewGameState();
    const encounter = makeEncounterConfig();
    const combatState = initCombatState(gameState, encounter);
    const gameWithCombat = syncToGameState(gameState, combatState);

    await saveGame(gameWithCombat, 4, tempDir);
    const loaded = await loadGame(4, tempDir);

    // End combat on the loaded state
    const ended = endCombat(loaded, 'victory');

    expect(ended.combatState).toBeNull();
    expect(ended.player.personality).toEqual(gameState.player.personality);
  });
});
