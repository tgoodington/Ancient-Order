/**
 * Tests for the game state factory (src/state/gameState.ts)
 */

import { describe, it, expect } from 'vitest';
import { createNewGameState } from './gameState.js';
import { validatePersonality } from '../personality/personalitySystem.js';

// ============================================================================
// createNewGameState()
// ============================================================================

describe('createNewGameState()', () => {
  it('returns a valid GameState object', () => {
    const state = createNewGameState();
    expect(state).toBeDefined();
    expect(typeof state).toBe('object');
  });

  it('each call returns a new object (no shared references)', () => {
    const s1 = createNewGameState();
    const s2 = createNewGameState();
    expect(s1).not.toBe(s2);
  });

  // ---- Player ---------------------------------------------------------------

  it('player has a non-empty id', () => {
    const { player } = createNewGameState();
    expect(typeof player.id).toBe('string');
    expect(player.id.length).toBeGreaterThan(0);
  });

  it('each call generates a unique player id', () => {
    const s1 = createNewGameState();
    const s2 = createNewGameState();
    expect(s1.player.id).not.toBe(s2.player.id);
  });

  it('player name is set to a non-empty string', () => {
    const { player } = createNewGameState();
    expect(typeof player.name).toBe('string');
    expect(player.name.length).toBeGreaterThan(0);
  });

  it('player personality sums to 100%', () => {
    const { player } = createNewGameState();
    const result = validatePersonality(player.personality);
    expect(result.valid).toBe(true);
    expect(result.errors).toHaveLength(0);
  });

  it('player personality traits are all within [5, 35]', () => {
    const { player } = createNewGameState();
    const p = player.personality;
    for (const trait of ['patience', 'empathy', 'cunning', 'logic', 'kindness', 'charisma'] as const) {
      expect(p[trait]).toBeGreaterThanOrEqual(5);
      expect(p[trait]).toBeLessThanOrEqual(35);
    }
  });

  // ---- NPCs -----------------------------------------------------------------

  it('npcs record contains exactly 3 NPCs', () => {
    const { npcs } = createNewGameState();
    expect(Object.keys(npcs)).toHaveLength(3);
  });

  it('Elena is retrievable by id from npcs', () => {
    const { npcs } = createNewGameState();
    expect(npcs['npc_scout_elena']).toBeDefined();
    expect(npcs['npc_scout_elena'].archetype).toBe('Loyal Scout');
  });

  it('Lars is retrievable by id from npcs', () => {
    const { npcs } = createNewGameState();
    expect(npcs['npc_merchant_lars']).toBeDefined();
    expect(npcs['npc_merchant_lars'].archetype).toBe('Scheming Merchant');
  });

  it('Kade is retrievable by id from npcs', () => {
    const { npcs } = createNewGameState();
    expect(npcs['npc_outlaw_kade']).toBeDefined();
    expect(npcs['npc_outlaw_kade'].archetype).toBe('Rogue Outlaw');
  });

  // ---- Combat & dialogue state ----------------------------------------------

  it('combatState is null', () => {
    expect(createNewGameState().combatState).toBeNull();
  });

  it('currentDialogueNode is null', () => {
    expect(createNewGameState().currentDialogueNode).toBeNull();
  });

  // ---- Persistence metadata -------------------------------------------------

  it('saveSlot is null', () => {
    expect(createNewGameState().saveSlot).toBeNull();
  });

  it('timestamp is a positive number close to now', () => {
    const before = Date.now();
    const state = createNewGameState();
    const after = Date.now();
    expect(state.timestamp).toBeGreaterThanOrEqual(before);
    expect(state.timestamp).toBeLessThanOrEqual(after);
  });

  // ---- Conversation log -----------------------------------------------------

  it('conversationLog starts as an empty array', () => {
    const { conversationLog } = createNewGameState();
    expect(Array.isArray(conversationLog)).toBe(true);
    expect(conversationLog).toHaveLength(0);
  });

  // ---- NPC personality validity -------------------------------------------

  it('all NPC personalities in the initial state sum to 100%', () => {
    const { npcs } = createNewGameState();
    for (const [_id, npc] of Object.entries(npcs)) {
      const result = validatePersonality(npc.personality);
      expect(result.valid).toBe(true);
    }
  });
});
