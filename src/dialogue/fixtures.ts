/**
 * Ancient Order - Dialogue Test Fixtures
 *
 * Sample dialogue tree for use in unit tests. Includes:
 * - A greeting node with both gated and ungated options
 * - A follow-up node reachable through an ungated choice
 * - A gated-path node reachable only via a personality gate
 * - A dead-end node (all options gated) for validation testing
 * - A terminal node (conversation ends: nextNodeId = null)
 *
 * Convention: all node IDs follow `${npcId}_${descriptor}` naming.
 * The NPC used here is `npc_test_elder` so the starting node is
 * `npc_test_elder_greet`.
 */

import { DialogueNode, NPC } from '../types/index.js';

// ============================================================================
// Test NPC fixture
// ============================================================================

/**
 * A minimal NPC used only in dialogue engine tests.
 * Personality values are not used by the engine itself (gates compare against
 * the player's personality), but the NPC id is required for getStartingNode().
 */
export const TEST_NPC: NPC = {
  id: 'npc_test_elder',
  archetype: 'Wise Elder',
  personality: {
    patience: 30,
    empathy: 25,
    cunning: 10,
    logic: 15,
    kindness: 12,
    charisma: 8,
  },
  affection: 0,
  trust: 0,
};

// ============================================================================
// Dialogue tree fixture
// ============================================================================

/**
 * Sample dialogue tree for npc_test_elder.
 *
 * Node structure:
 *
 *   npc_test_elder_greet
 *     → [ungated]  opt_greet_humble  → npc_test_elder_humble_reply
 *     → [kindness gte 25] opt_greet_kind → npc_test_elder_kind_reply
 *     → [cunning gte 28]  opt_greet_sly  → npc_test_elder_kind_reply  (reuses node)
 *
 *   npc_test_elder_humble_reply
 *     → [ungated]  opt_humble_accept  → null (end conversation)
 *     → [patience gte 20] opt_humble_probe → npc_test_elder_probe_reply
 *
 *   npc_test_elder_kind_reply
 *     → [ungated]  opt_kind_thanks → null (end conversation)
 *
 *   npc_test_elder_probe_reply
 *     → [ungated]  opt_probe_farewell → null (end conversation)
 *
 *   npc_test_elder_dead_end   ← ALL options gated → dead end for validation tests
 *     → [empathy gte 30] opt_dead_empathy → null
 *     → [cunning gte 30] opt_dead_cunning → null
 */
export const TEST_DIALOGUE_TREE: DialogueNode[] = [
  // ---- Greeting node -------------------------------------------------------
  {
    id: 'npc_test_elder_greet',
    npcId: 'npc_test_elder',
    text: 'Traveller, you have finally arrived. What brings you here?',
    options: [
      {
        id: 'opt_greet_humble',
        text: 'I seek your wisdom, elder.',
        // No gate — always available
        nextNodeId: 'npc_test_elder_humble_reply',
      },
      {
        id: 'opt_greet_kind',
        text: 'I heard you needed someone with a gentle heart.',
        gate: { trait: 'kindness', operator: 'gte', value: 25 },
        personalityAdjustment: { kindness: 2, empathy: 1 },
        npcAdjustment: { affectionChange: 5, trustChange: 3 },
        nextNodeId: 'npc_test_elder_kind_reply',
      },
      {
        id: 'opt_greet_sly',
        text: 'Word travels fast about your little problem.',
        gate: { trait: 'cunning', operator: 'gte', value: 28 },
        nextNodeId: 'npc_test_elder_kind_reply',
      },
    ],
  },

  // ---- Humble reply node ---------------------------------------------------
  {
    id: 'npc_test_elder_humble_reply',
    npcId: 'npc_test_elder',
    text: 'Wisdom is earned through patience, young one.',
    options: [
      {
        id: 'opt_humble_accept',
        text: 'I understand. Thank you, elder.',
        // No gate — ungated fallback
        nextNodeId: null, // Ends conversation
      },
      {
        id: 'opt_humble_probe',
        text: 'I have time. Tell me more.',
        gate: { trait: 'patience', operator: 'gte', value: 20 },
        nextNodeId: 'npc_test_elder_probe_reply',
      },
    ],
  },

  // ---- Kind reply node -----------------------------------------------------
  {
    id: 'npc_test_elder_kind_reply',
    npcId: 'npc_test_elder',
    text: 'Your heart speaks well for you. I will share what I know.',
    options: [
      {
        id: 'opt_kind_thanks',
        text: 'Thank you for trusting me.',
        // No gate — ungated fallback
        npcAdjustment: { affectionChange: 2 },
        nextNodeId: null, // Ends conversation
      },
    ],
  },

  // ---- Probe reply node ---------------------------------------------------
  {
    id: 'npc_test_elder_probe_reply',
    npcId: 'npc_test_elder',
    text: 'Long ago, before the Order was founded...',
    options: [
      {
        id: 'opt_probe_farewell',
        text: 'I will remember this. Farewell.',
        // No gate — ungated fallback
        nextNodeId: null, // Ends conversation
      },
    ],
  },

  // ---- Dead-end node (for validateDialogueTree tests) ---------------------
  {
    id: 'npc_test_elder_dead_end',
    npcId: 'npc_test_elder',
    text: 'Only the worthy may proceed.',
    options: [
      {
        id: 'opt_dead_empathy',
        text: 'I feel your pain deeply.',
        gate: { trait: 'empathy', operator: 'gte', value: 30 },
        nextNodeId: null,
      },
      {
        id: 'opt_dead_cunning',
        text: 'I can see through your test.',
        gate: { trait: 'cunning', operator: 'gte', value: 30 },
        nextNodeId: null,
      },
    ],
  },
];

/**
 * A dialogue tree that is fully valid — no dead ends.
 * Used to test that validateDialogueTree() returns valid: true.
 */
export const VALID_DIALOGUE_TREE: DialogueNode[] = TEST_DIALOGUE_TREE.filter(
  node => node.id !== 'npc_test_elder_dead_end'
);

/**
 * A dialogue tree containing only the dead-end node.
 * Used to isolate validation failure testing.
 */
export const DEAD_END_DIALOGUE_TREE: DialogueNode[] = [
  TEST_DIALOGUE_TREE.find(n => n.id === 'npc_test_elder_dead_end')!,
];
