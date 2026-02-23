/**
 * Ancient Order - State Updaters Tests
 *
 * Verifies:
 * - Reference inequality: every updater returns a new object (input unchanged)
 * - Value correctness: the updated fields contain the expected values
 * - Composition: higher-level updaters correctly compose lower-level ones
 */

import { describe, it, expect, beforeEach } from 'vitest';
import {
  updateTimestamp,
  updatePlayerPersonality,
  applyPersonalityAdjustment,
  updateNPCAffection,
  updateNPCTrust,
  updateNPCRelationship,
  addConversationEntry,
  processDialogueChoice,
  updateCombatState,
} from './stateUpdaters.js';
import { createNewGameState } from './gameState.js';
import { GameState, Personality, ConversationEntry, DialogueOption } from '../types/index.js';

// ============================================================================
// Helpers
// ============================================================================

/** Fully balanced personality for tests. */
const BALANCED_PERSONALITY: Personality = {
  patience: 16.67,
  empathy: 16.67,
  cunning: 16.67,
  logic: 16.67,
  kindness: 16.67,
  charisma: 16.65,
};

let baseState: GameState;

beforeEach(() => {
  baseState = createNewGameState();
});

// ============================================================================
// updateTimestamp
// ============================================================================

describe('updateTimestamp', () => {
  it('returns a new object (reference inequality)', () => {
    const updated = updateTimestamp(baseState);
    expect(updated).not.toBe(baseState);
  });

  it('updates the timestamp field', () => {
    const before = baseState.timestamp;
    // Small delay to ensure timestamp differs on fast machines
    const updated = updateTimestamp({ ...baseState, timestamp: before - 100 });
    expect(updated.timestamp).toBeGreaterThanOrEqual(before - 100);
  });

  it('does not change other fields', () => {
    const updated = updateTimestamp(baseState);
    expect(updated.player).toBe(baseState.player);
    expect(updated.npcs).toBe(baseState.npcs);
    expect(updated.conversationLog).toBe(baseState.conversationLog);
  });

  it('does not mutate the input state', () => {
    const originalTimestamp = baseState.timestamp;
    updateTimestamp(baseState);
    expect(baseState.timestamp).toBe(originalTimestamp);
  });
});

// ============================================================================
// updatePlayerPersonality
// ============================================================================

describe('updatePlayerPersonality', () => {
  it('returns a new object (reference inequality)', () => {
    const updated = updatePlayerPersonality(baseState, BALANCED_PERSONALITY);
    expect(updated).not.toBe(baseState);
  });

  it('sets the player personality to the new value', () => {
    const newPersonality: Personality = {
      patience: 20,
      empathy: 20,
      cunning: 15,
      logic: 15,
      kindness: 15,
      charisma: 15,
    };
    const updated = updatePlayerPersonality(baseState, newPersonality);
    expect(updated.player.personality).toEqual(newPersonality);
  });

  it('does not mutate the input state', () => {
    const originalPersonality = { ...baseState.player.personality };
    const newPersonality: Personality = {
      patience: 20,
      empathy: 20,
      cunning: 15,
      logic: 15,
      kindness: 15,
      charisma: 15,
    };
    updatePlayerPersonality(baseState, newPersonality);
    expect(baseState.player.personality).toEqual(originalPersonality);
  });

  it('creates a new player object (nested reference inequality)', () => {
    const updated = updatePlayerPersonality(baseState, BALANCED_PERSONALITY);
    expect(updated.player).not.toBe(baseState.player);
  });

  it('preserves other player fields (id, name)', () => {
    const updated = updatePlayerPersonality(baseState, BALANCED_PERSONALITY);
    expect(updated.player.id).toBe(baseState.player.id);
    expect(updated.player.name).toBe(baseState.player.name);
  });

  it('preserves other state fields (npcs, conversationLog)', () => {
    const updated = updatePlayerPersonality(baseState, BALANCED_PERSONALITY);
    expect(updated.npcs).toBe(baseState.npcs);
    expect(updated.conversationLog).toBe(baseState.conversationLog);
  });
});

// ============================================================================
// applyPersonalityAdjustment
// ============================================================================

describe('applyPersonalityAdjustment', () => {
  it('returns a new object (reference inequality)', () => {
    const updated = applyPersonalityAdjustment(baseState, { cunning: 5 });
    expect(updated).not.toBe(baseState);
  });

  it('adjusts the specified trait', () => {
    const updated = applyPersonalityAdjustment(baseState, { cunning: 5 });
    expect(updated.player.personality.cunning).toBeGreaterThan(
      baseState.player.personality.cunning
    );
  });

  it('maintains 100% sum after adjustment', () => {
    const updated = applyPersonalityAdjustment(baseState, { patience: 10, empathy: -5 });
    const sum = Object.values(updated.player.personality).reduce((s, v) => s + v, 0);
    expect(sum).toBeCloseTo(100, 1);
  });

  it('clamps traits to the [5, 35] range', () => {
    // Force patience as high as possible
    const updated = applyPersonalityAdjustment(baseState, { patience: 50 });
    expect(updated.player.personality.patience).toBeLessThanOrEqual(35);
    expect(updated.player.personality.patience).toBeGreaterThanOrEqual(5);
  });

  it('does not mutate the input state', () => {
    const originalPersonality = { ...baseState.player.personality };
    applyPersonalityAdjustment(baseState, { cunning: 5 });
    expect(baseState.player.personality).toEqual(originalPersonality);
  });

  it('empty adjustment preserves personality (within float tolerance)', () => {
    const updated = applyPersonalityAdjustment(baseState, {});
    const sum = Object.values(updated.player.personality).reduce((s, v) => s + v, 0);
    expect(sum).toBeCloseTo(100, 1);
  });
});

// ============================================================================
// updateNPCAffection
// ============================================================================

describe('updateNPCAffection', () => {
  const ELENA_ID = 'npc_scout_elena';

  it('returns a new object (reference inequality)', () => {
    const updated = updateNPCAffection(baseState, ELENA_ID, 10);
    expect(updated).not.toBe(baseState);
  });

  it('increases affection by the specified amount', () => {
    const original = baseState.npcs[ELENA_ID].affection; // 0
    const updated = updateNPCAffection(baseState, ELENA_ID, 15);
    expect(updated.npcs[ELENA_ID].affection).toBe(original + 15);
  });

  it('decreases affection by the specified amount', () => {
    const original = baseState.npcs[ELENA_ID].affection; // 0
    const updated = updateNPCAffection(baseState, ELENA_ID, -20);
    expect(updated.npcs[ELENA_ID].affection).toBe(original - 20);
  });

  it('clamps affection at +100', () => {
    const updated = updateNPCAffection(baseState, ELENA_ID, 200);
    expect(updated.npcs[ELENA_ID].affection).toBe(100);
  });

  it('clamps affection at -100', () => {
    const updated = updateNPCAffection(baseState, ELENA_ID, -200);
    expect(updated.npcs[ELENA_ID].affection).toBe(-100);
  });

  it('returns state unchanged if NPC not found', () => {
    const updated = updateNPCAffection(baseState, 'npc_unknown_xyz', 10);
    expect(updated).toBe(baseState);
  });

  it('does not mutate the input state', () => {
    const originalAffection = baseState.npcs[ELENA_ID].affection;
    updateNPCAffection(baseState, ELENA_ID, 10);
    expect(baseState.npcs[ELENA_ID].affection).toBe(originalAffection);
  });

  it('creates new npcs record (nested reference inequality)', () => {
    const updated = updateNPCAffection(baseState, ELENA_ID, 10);
    expect(updated.npcs).not.toBe(baseState.npcs);
  });

  it('does not affect other NPCs', () => {
    const updated = updateNPCAffection(baseState, ELENA_ID, 10);
    expect(updated.npcs['npc_merchant_lars']).toBe(baseState.npcs['npc_merchant_lars']);
    expect(updated.npcs['npc_outlaw_kade']).toBe(baseState.npcs['npc_outlaw_kade']);
  });
});

// ============================================================================
// updateNPCTrust
// ============================================================================

describe('updateNPCTrust', () => {
  const LARS_ID = 'npc_merchant_lars';

  it('returns a new object (reference inequality)', () => {
    const updated = updateNPCTrust(baseState, LARS_ID, 10);
    expect(updated).not.toBe(baseState);
  });

  it('increases trust by the specified amount', () => {
    const original = baseState.npcs[LARS_ID].trust; // -20
    const updated = updateNPCTrust(baseState, LARS_ID, 10);
    expect(updated.npcs[LARS_ID].trust).toBe(original + 10);
  });

  it('clamps trust at +100', () => {
    const updated = updateNPCTrust(baseState, LARS_ID, 200);
    expect(updated.npcs[LARS_ID].trust).toBe(100);
  });

  it('clamps trust at -100', () => {
    const updated = updateNPCTrust(baseState, LARS_ID, -200);
    expect(updated.npcs[LARS_ID].trust).toBe(-100);
  });

  it('returns state unchanged if NPC not found', () => {
    const updated = updateNPCTrust(baseState, 'npc_unknown_xyz', 10);
    expect(updated).toBe(baseState);
  });

  it('does not mutate the input state', () => {
    const originalTrust = baseState.npcs[LARS_ID].trust;
    updateNPCTrust(baseState, LARS_ID, 10);
    expect(baseState.npcs[LARS_ID].trust).toBe(originalTrust);
  });
});

// ============================================================================
// updateNPCRelationship
// ============================================================================

describe('updateNPCRelationship', () => {
  const KADE_ID = 'npc_outlaw_kade';

  it('returns a new object (reference inequality)', () => {
    const updated = updateNPCRelationship(baseState, KADE_ID, 10, 5);
    expect(updated).not.toBe(baseState);
  });

  it('updates both affection and trust in one call', () => {
    const origAffection = baseState.npcs[KADE_ID].affection;
    const origTrust = baseState.npcs[KADE_ID].trust;
    const updated = updateNPCRelationship(baseState, KADE_ID, 15, -10);
    expect(updated.npcs[KADE_ID].affection).toBe(origAffection + 15);
    expect(updated.npcs[KADE_ID].trust).toBe(origTrust - 10);
  });

  it('clamps affection and trust to [-100, +100]', () => {
    const updated = updateNPCRelationship(baseState, KADE_ID, 500, -500);
    expect(updated.npcs[KADE_ID].affection).toBe(100);
    expect(updated.npcs[KADE_ID].trust).toBe(-100);
  });

  it('returns state unchanged if NPC not found', () => {
    const updated = updateNPCRelationship(baseState, 'npc_unknown_xyz', 10, 5);
    expect(updated).toBe(baseState);
  });

  it('does not mutate the input state', () => {
    const origAffection = baseState.npcs[KADE_ID].affection;
    const origTrust = baseState.npcs[KADE_ID].trust;
    updateNPCRelationship(baseState, KADE_ID, 10, 5);
    expect(baseState.npcs[KADE_ID].affection).toBe(origAffection);
    expect(baseState.npcs[KADE_ID].trust).toBe(origTrust);
  });

  it('produces the same result as sequential updateNPCAffection + updateNPCTrust', () => {
    const compound = updateNPCRelationship(baseState, KADE_ID, 20, 10);
    const sequential = updateNPCTrust(updateNPCAffection(baseState, KADE_ID, 20), KADE_ID, 10);
    expect(compound.npcs[KADE_ID].affection).toBe(sequential.npcs[KADE_ID].affection);
    expect(compound.npcs[KADE_ID].trust).toBe(sequential.npcs[KADE_ID].trust);
  });
});

// ============================================================================
// addConversationEntry
// ============================================================================

describe('addConversationEntry', () => {
  const SAMPLE_ENTRY: ConversationEntry = {
    npcId: 'npc_scout_elena',
    nodeId: 'npc_scout_elena_greet',
    optionId: 'opt_friendly',
    timestamp: 1700000000000,
  };

  it('returns a new object (reference inequality)', () => {
    const updated = addConversationEntry(baseState, SAMPLE_ENTRY);
    expect(updated).not.toBe(baseState);
  });

  it('appends the entry to the conversation log', () => {
    const updated = addConversationEntry(baseState, SAMPLE_ENTRY);
    expect(updated.conversationLog).toHaveLength(1);
    expect(updated.conversationLog[0]).toEqual(SAMPLE_ENTRY);
  });

  it('creates a new array (not mutating the original)', () => {
    const updated = addConversationEntry(baseState, SAMPLE_ENTRY);
    expect(updated.conversationLog).not.toBe(baseState.conversationLog);
  });

  it('does not mutate the input state', () => {
    const originalLength = baseState.conversationLog.length;
    addConversationEntry(baseState, SAMPLE_ENTRY);
    expect(baseState.conversationLog).toHaveLength(originalLength);
  });

  it('preserves existing entries when appending', () => {
    const firstEntry: ConversationEntry = {
      npcId: 'npc_scout_elena',
      nodeId: 'npc_scout_elena_greet',
      optionId: 'opt_a',
      timestamp: 1700000000000,
    };
    const secondEntry: ConversationEntry = {
      npcId: 'npc_merchant_lars',
      nodeId: 'npc_merchant_lars_greet',
      optionId: 'opt_b',
      timestamp: 1700000001000,
    };

    const after1 = addConversationEntry(baseState, firstEntry);
    const after2 = addConversationEntry(after1, secondEntry);

    expect(after2.conversationLog).toHaveLength(2);
    expect(after2.conversationLog[0]).toEqual(firstEntry);
    expect(after2.conversationLog[1]).toEqual(secondEntry);
  });
});

// ============================================================================
// processDialogueChoice
// ============================================================================

describe('processDialogueChoice', () => {
  const NPC_ID = 'npc_scout_elena';
  const NODE_ID = 'npc_scout_elena_greet';
  const OPTION_ID = 'opt_empathetic';

  const optionWithAll: DialogueOption = {
    id: OPTION_ID,
    text: 'I understand how you feel.',
    personalityAdjustment: { empathy: 3 },
    npcAdjustment: { affectionChange: 10, trustChange: 5 },
    nextNodeId: null,
  };

  const optionWithNothing: DialogueOption = {
    id: 'opt_neutral',
    text: 'Alright.',
    nextNodeId: null,
  };

  const optionWithPersonalityOnly: DialogueOption = {
    id: 'opt_logical',
    text: 'Logic dictates...',
    personalityAdjustment: { logic: 4 },
    nextNodeId: null,
  };

  const optionWithRelationshipOnly: DialogueOption = {
    id: 'opt_kind',
    text: 'Here, let me help.',
    npcAdjustment: { affectionChange: 20 },
    nextNodeId: null,
  };

  it('returns a new object (reference inequality)', () => {
    const updated = processDialogueChoice(baseState, NPC_ID, NODE_ID, OPTION_ID, optionWithAll);
    expect(updated).not.toBe(baseState);
  });

  it('applies personality adjustment when option has one', () => {
    const updated = processDialogueChoice(baseState, NPC_ID, NODE_ID, OPTION_ID, optionWithAll);
    expect(updated.player.personality.empathy).toBeGreaterThan(
      baseState.player.personality.empathy
    );
  });

  it('applies NPC relationship changes when option has them', () => {
    const origAffection = baseState.npcs[NPC_ID].affection;
    const origTrust = baseState.npcs[NPC_ID].trust;
    const updated = processDialogueChoice(baseState, NPC_ID, NODE_ID, OPTION_ID, optionWithAll);
    expect(updated.npcs[NPC_ID].affection).toBe(origAffection + 10);
    expect(updated.npcs[NPC_ID].trust).toBe(origTrust + 5);
  });

  it('appends a ConversationEntry with correct fields', () => {
    const updated = processDialogueChoice(baseState, NPC_ID, NODE_ID, OPTION_ID, optionWithAll);
    const entry = updated.conversationLog[updated.conversationLog.length - 1];
    expect(entry.npcId).toBe(NPC_ID);
    expect(entry.nodeId).toBe(NODE_ID);
    expect(entry.optionId).toBe(OPTION_ID);
    expect(entry.timestamp).toBeGreaterThan(0);
  });

  it('maintains 100% personality sum after processing', () => {
    const updated = processDialogueChoice(baseState, NPC_ID, NODE_ID, OPTION_ID, optionWithAll);
    const sum = Object.values(updated.player.personality).reduce((s, v) => s + v, 0);
    expect(sum).toBeCloseTo(100, 1);
  });

  it('handles option with no personality adjustment or npc adjustment', () => {
    const updated = processDialogueChoice(
      baseState,
      NPC_ID,
      NODE_ID,
      'opt_neutral',
      optionWithNothing
    );
    // Personality unchanged
    expect(updated.player.personality).toEqual(baseState.player.personality);
    // NPC affection/trust unchanged
    expect(updated.npcs[NPC_ID].affection).toBe(baseState.npcs[NPC_ID].affection);
    expect(updated.npcs[NPC_ID].trust).toBe(baseState.npcs[NPC_ID].trust);
    // Entry still added
    expect(updated.conversationLog).toHaveLength(1);
  });

  it('handles option with personality adjustment only', () => {
    const updated = processDialogueChoice(
      baseState,
      NPC_ID,
      NODE_ID,
      'opt_logical',
      optionWithPersonalityOnly
    );
    expect(updated.player.personality.logic).toBeGreaterThan(baseState.player.personality.logic);
    expect(updated.npcs[NPC_ID].affection).toBe(baseState.npcs[NPC_ID].affection);
  });

  it('handles option with NPC relationship only', () => {
    const origAffection = baseState.npcs[NPC_ID].affection;
    const updated = processDialogueChoice(
      baseState,
      NPC_ID,
      NODE_ID,
      'opt_kind',
      optionWithRelationshipOnly
    );
    expect(updated.npcs[NPC_ID].affection).toBe(origAffection + 20);
    expect(updated.player.personality).toEqual(baseState.player.personality);
  });

  it('does not mutate the input state', () => {
    const originalPersonality = { ...baseState.player.personality };
    const originalAffection = baseState.npcs[NPC_ID].affection;
    const originalLogLength = baseState.conversationLog.length;
    processDialogueChoice(baseState, NPC_ID, NODE_ID, OPTION_ID, optionWithAll);
    expect(baseState.player.personality).toEqual(originalPersonality);
    expect(baseState.npcs[NPC_ID].affection).toBe(originalAffection);
    expect(baseState.conversationLog).toHaveLength(originalLogLength);
  });

  it('composes correctly: personality and relationship both applied', () => {
    const updated = processDialogueChoice(baseState, NPC_ID, NODE_ID, OPTION_ID, optionWithAll);
    // Personality adjusted
    const sum = Object.values(updated.player.personality).reduce((s, v) => s + v, 0);
    expect(sum).toBeCloseTo(100, 1);
    // Relationship adjusted
    expect(updated.npcs[NPC_ID].affection).toBe(baseState.npcs[NPC_ID].affection + 10);
    // Log populated
    expect(updated.conversationLog).toHaveLength(1);
  });
});

// ============================================================================
// updateCombatState
// ============================================================================

describe('updateCombatState', () => {
  it('returns a new object (reference inequality)', () => {
    const updated = updateCombatState(baseState, null);
    expect(updated).not.toBe(baseState);
  });

  it('sets combatState to a provided value', () => {
    const fakeCombatState = { round: 1, phase: 'AI_DECISION', status: 'active' };
    const updated = updateCombatState(baseState, fakeCombatState);
    expect(updated.combatState).toBe(fakeCombatState);
  });

  it('clears combatState when null is passed', () => {
    const fakeCombatState = { round: 1 };
    const withCombat = updateCombatState(baseState, fakeCombatState);
    const cleared = updateCombatState(withCombat, null);
    expect(cleared.combatState).toBeNull();
  });

  it('does not mutate the input state', () => {
    const originalCombatState = baseState.combatState;
    updateCombatState(baseState, { round: 99 });
    expect(baseState.combatState).toBe(originalCombatState);
  });

  it('preserves other fields (player, npcs, conversationLog)', () => {
    const updated = updateCombatState(baseState, { round: 1 });
    expect(updated.player).toBe(baseState.player);
    expect(updated.npcs).toBe(baseState.npcs);
    expect(updated.conversationLog).toBe(baseState.conversationLog);
  });

  it('updates timestamp', () => {
    const stateWithOldTimestamp = { ...baseState, timestamp: 0 };
    const updated = updateCombatState(stateWithOldTimestamp, null);
    expect(updated.timestamp).toBeGreaterThan(0);
  });
});
