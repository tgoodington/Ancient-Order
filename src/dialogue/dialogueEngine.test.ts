/**
 * Ancient Order - Dialogue Engine Tests
 *
 * Coverage:
 *  - evaluatePersonalityGate: gte/lte/eq pass and fail
 *  - getAvailableOptions: ungated always visible, gated conditional
 *  - getStartingNode: convention `${npcId}_greet`, missing node throws
 *  - processDialogueSelection: valid choice, gated choice unavailable, state updated immutably,
 *    next node resolved, end-of-conversation (nextNodeId null), personality/NPC adjustments applied
 *  - validateDialogueTree: valid tree returns { valid: true }, dead-end returns problematic node IDs
 */

import { describe, it, expect } from 'vitest';

import {
  evaluatePersonalityGate,
  getAvailableOptions,
  getStartingNode,
  processDialogueSelection,
  validateDialogueTree,
} from './dialogueEngine.js';

import {
  TEST_NPC,
  TEST_DIALOGUE_TREE,
  VALID_DIALOGUE_TREE,
  DEAD_END_DIALOGUE_TREE,
} from './fixtures.js';

import { createNewGameState } from '../state/gameState.js';
import { Personality } from '../types/index.js';

// ============================================================================
// Helpers
// ============================================================================

/**
 * Creates a personality with specific trait values.
 * Remaining budget is distributed evenly across unspecified traits,
 * allowing tests to set targeted trait values without worrying about
 * the sum constraint (tests rely on explicitly constructed personalities
 * rather than the normalization algorithm for clarity).
 *
 * NOTE: These helpers bypass normalisation intentionally.
 * The dialogue engine only reads personality values — it does not enforce the
 * sum = 100 constraint itself. Sum-enforcement is the personality system's job.
 * Using raw objects here keeps gate tests readable and focused.
 */
function makePersonality(overrides: Partial<Personality>): Personality {
  const defaults: Personality = {
    patience: 16.67,
    empathy: 16.67,
    cunning: 16.67,
    logic: 16.67,
    kindness: 16.67,
    charisma: 16.65,
  };
  return { ...defaults, ...overrides };
}

// ============================================================================
// evaluatePersonalityGate
// ============================================================================

describe('evaluatePersonalityGate', () => {
  it('returns true when gte operator and trait meets the threshold (exact)', () => {
    const personality = makePersonality({ kindness: 25 });
    expect(
      evaluatePersonalityGate({ trait: 'kindness', operator: 'gte', value: 25 }, personality)
    ).toBe(true);
  });

  it('returns true when gte operator and trait exceeds the threshold', () => {
    const personality = makePersonality({ kindness: 30 });
    expect(
      evaluatePersonalityGate({ trait: 'kindness', operator: 'gte', value: 25 }, personality)
    ).toBe(true);
  });

  it('returns false when gte operator and trait is below the threshold', () => {
    const personality = makePersonality({ kindness: 20 });
    expect(
      evaluatePersonalityGate({ trait: 'kindness', operator: 'gte', value: 25 }, personality)
    ).toBe(false);
  });

  it('returns true when lte operator and trait is at the threshold (exact)', () => {
    const personality = makePersonality({ cunning: 15 });
    expect(
      evaluatePersonalityGate({ trait: 'cunning', operator: 'lte', value: 15 }, personality)
    ).toBe(true);
  });

  it('returns true when lte operator and trait is below the threshold', () => {
    const personality = makePersonality({ cunning: 10 });
    expect(
      evaluatePersonalityGate({ trait: 'cunning', operator: 'lte', value: 15 }, personality)
    ).toBe(true);
  });

  it('returns false when lte operator and trait exceeds the threshold', () => {
    const personality = makePersonality({ cunning: 20 });
    expect(
      evaluatePersonalityGate({ trait: 'cunning', operator: 'lte', value: 15 }, personality)
    ).toBe(false);
  });

  it('returns true when eq operator and trait matches within float tolerance', () => {
    const personality = makePersonality({ patience: 20.05 }); // within 0.1 of 20
    expect(
      evaluatePersonalityGate({ trait: 'patience', operator: 'eq', value: 20 }, personality)
    ).toBe(true);
  });

  it('returns false when eq operator and trait is outside float tolerance', () => {
    const personality = makePersonality({ patience: 20.15 }); // beyond 0.1 of 20
    expect(
      evaluatePersonalityGate({ trait: 'patience', operator: 'eq', value: 20 }, personality)
    ).toBe(false);
  });
});

// ============================================================================
// getAvailableOptions
// ============================================================================

describe('getAvailableOptions', () => {
  const greetNode = TEST_DIALOGUE_TREE.find(n => n.id === 'npc_test_elder_greet')!;

  it('always includes ungated options regardless of personality', () => {
    // Low stats — no gates pass
    const lowPersonality = makePersonality({ kindness: 5, cunning: 5 });
    const options = getAvailableOptions(greetNode, lowPersonality);
    const ids = options.map(o => o.id);
    expect(ids).toContain('opt_greet_humble'); // ungated
    expect(ids).not.toContain('opt_greet_kind');
    expect(ids).not.toContain('opt_greet_sly');
  });

  it('includes a gated option when the personality gate passes (kindness gte 25)', () => {
    const highKindness = makePersonality({ kindness: 30 });
    const options = getAvailableOptions(greetNode, highKindness);
    const ids = options.map(o => o.id);
    expect(ids).toContain('opt_greet_humble');
    expect(ids).toContain('opt_greet_kind');
    expect(ids).not.toContain('opt_greet_sly');
  });

  it('includes multiple gated options when multiple gates pass', () => {
    const highStats = makePersonality({ kindness: 30, cunning: 30 });
    const options = getAvailableOptions(greetNode, highStats);
    const ids = options.map(o => o.id);
    expect(ids).toContain('opt_greet_humble');
    expect(ids).toContain('opt_greet_kind');
    expect(ids).toContain('opt_greet_sly');
  });

  it('returns only the ungated option when all gates fail', () => {
    const baseline = makePersonality({});
    const options = getAvailableOptions(greetNode, baseline);
    expect(options).toHaveLength(1);
    expect(options[0].id).toBe('opt_greet_humble');
  });
});

// ============================================================================
// getStartingNode
// ============================================================================

describe('getStartingNode', () => {
  it('returns the node with id matching `${npcId}_greet`', () => {
    const node = getStartingNode(TEST_NPC, TEST_DIALOGUE_TREE);
    expect(node.id).toBe('npc_test_elder_greet');
  });

  it('throws when the starting node is not present in the tree', () => {
    // Provide a different NPC id so the greet node doesn't exist in the tree
    const differentNpc = { ...TEST_NPC, id: 'npc_unknown_stranger' };
    expect(() => getStartingNode(differentNpc, TEST_DIALOGUE_TREE)).toThrow(
      'npc_unknown_stranger'
    );
  });
});

// ============================================================================
// processDialogueSelection
// ============================================================================

describe('processDialogueSelection', () => {
  it('returns a DialogueResult with the selected option', () => {
    const state = createNewGameState();
    const result = processDialogueSelection(
      state,
      'npc_test_elder',
      'npc_test_elder_greet',
      'opt_greet_humble',
      TEST_DIALOGUE_TREE
    );
    expect(result.selectedOption.id).toBe('opt_greet_humble');
  });

  it('resolves the next node when nextNodeId is set', () => {
    const state = createNewGameState();
    const result = processDialogueSelection(
      state,
      'npc_test_elder',
      'npc_test_elder_greet',
      'opt_greet_humble',
      TEST_DIALOGUE_TREE
    );
    expect(result.nextNode).not.toBeNull();
    expect(result.nextNode?.id).toBe('npc_test_elder_humble_reply');
  });

  it('returns nextNode as null when the option ends the conversation', () => {
    const state = createNewGameState();
    // First move to humble_reply, then choose the option that ends conversation
    const result = processDialogueSelection(
      state,
      'npc_test_elder',
      'npc_test_elder_humble_reply',
      'opt_humble_accept',
      TEST_DIALOGUE_TREE
    );
    expect(result.nextNode).toBeNull();
  });

  it('returns a new state object (immutable — input not mutated)', () => {
    const state = createNewGameState();
    const result = processDialogueSelection(
      state,
      'npc_test_elder',
      'npc_test_elder_greet',
      'opt_greet_humble',
      TEST_DIALOGUE_TREE
    );
    expect(result.state).not.toBe(state); // Reference inequality
  });

  it('records the choice in the conversation log', () => {
    const state = createNewGameState();
    expect(state.conversationLog).toHaveLength(0);
    const result = processDialogueSelection(
      state,
      'npc_test_elder',
      'npc_test_elder_greet',
      'opt_greet_humble',
      TEST_DIALOGUE_TREE
    );
    expect(result.state.conversationLog).toHaveLength(1);
    expect(result.state.conversationLog[0].npcId).toBe('npc_test_elder');
    expect(result.state.conversationLog[0].nodeId).toBe('npc_test_elder_greet');
    expect(result.state.conversationLog[0].optionId).toBe('opt_greet_humble');
  });

  it('applies personality adjustments when option has personalityAdjustment', () => {
    // Use a personality that passes the kindness gate (gte 25)
    const state = {
      ...createNewGameState(),
      player: {
        ...createNewGameState().player,
        personality: makePersonality({ kindness: 26 }),
      },
    };

    const kindnessBefore = state.player.personality.kindness;
    const result = processDialogueSelection(
      state,
      'npc_test_elder',
      'npc_test_elder_greet',
      'opt_greet_kind',
      TEST_DIALOGUE_TREE
    );
    // personalityAdjustment: { kindness: 2, empathy: 1 } — kindness should increase
    expect(result.state.player.personality.kindness).toBeGreaterThan(kindnessBefore);
  });

  it('applies NPC relationship adjustments when option has npcAdjustment', () => {
    // Use a personality that passes the kindness gate (gte 25)
    const baseState = createNewGameState();
    // Inject npc_test_elder into npcs so updateNPCRelationship can find it
    const state = {
      ...baseState,
      player: {
        ...baseState.player,
        personality: makePersonality({ kindness: 30 }),
      },
      npcs: {
        ...baseState.npcs,
        npc_test_elder: TEST_NPC,
      },
    };

    const affectionBefore = state.npcs['npc_test_elder'].affection;
    const result = processDialogueSelection(
      state,
      'npc_test_elder',
      'npc_test_elder_greet',
      'opt_greet_kind',
      TEST_DIALOGUE_TREE
    );
    // npcAdjustment: { affectionChange: 5, trustChange: 3 }
    expect(result.state.npcs['npc_test_elder'].affection).toBe(affectionBefore + 5);
    expect(result.state.npcs['npc_test_elder'].trust).toBe(TEST_NPC.trust + 3);
  });

  it('throws when the node does not exist in the tree', () => {
    const state = createNewGameState();
    expect(() =>
      processDialogueSelection(
        state,
        'npc_test_elder',
        'nonexistent_node',
        'opt_greet_humble',
        TEST_DIALOGUE_TREE
      )
    ).toThrow('nonexistent_node');
  });

  it('throws when the option does not exist on the node', () => {
    const state = createNewGameState();
    expect(() =>
      processDialogueSelection(
        state,
        'npc_test_elder',
        'npc_test_elder_greet',
        'nonexistent_option',
        TEST_DIALOGUE_TREE
      )
    ).toThrow('nonexistent_option');
  });

  it('throws when a gated option is selected but the gate fails', () => {
    const state = createNewGameState(); // default personality: kindness ~16.67, well below 25
    expect(() =>
      processDialogueSelection(
        state,
        'npc_test_elder',
        'npc_test_elder_greet',
        'opt_greet_kind', // gate: kindness gte 25
        TEST_DIALOGUE_TREE
      )
    ).toThrow();
  });

  it('allows a gated option when the gate passes (patience gte 20)', () => {
    const baseState = createNewGameState();
    const state = {
      ...baseState,
      player: {
        ...baseState.player,
        personality: makePersonality({ patience: 25 }),
      },
    };
    const result = processDialogueSelection(
      state,
      'npc_test_elder',
      'npc_test_elder_humble_reply',
      'opt_humble_probe', // gate: patience gte 20
      TEST_DIALOGUE_TREE
    );
    expect(result.selectedOption.id).toBe('opt_humble_probe');
    expect(result.nextNode?.id).toBe('npc_test_elder_probe_reply');
  });
});

// ============================================================================
// validateDialogueTree
// ============================================================================

describe('validateDialogueTree', () => {
  it('returns valid: true for a tree with no dead ends', () => {
    const result = validateDialogueTree(VALID_DIALOGUE_TREE);
    expect(result.valid).toBe(true);
    expect(result.problematicNodes).toHaveLength(0);
  });

  it('returns valid: false when a node has only gated options', () => {
    const result = validateDialogueTree(DEAD_END_DIALOGUE_TREE);
    expect(result.valid).toBe(false);
    expect(result.problematicNodes).toContain('npc_test_elder_dead_end');
  });

  it('identifies exactly the dead-end nodes in a mixed tree', () => {
    const result = validateDialogueTree(TEST_DIALOGUE_TREE);
    // Only npc_test_elder_dead_end should be flagged
    expect(result.valid).toBe(false);
    expect(result.problematicNodes).toEqual(['npc_test_elder_dead_end']);
  });

  it('returns valid: true and empty problematicNodes for an empty tree', () => {
    const result = validateDialogueTree([]);
    expect(result.valid).toBe(true);
    expect(result.problematicNodes).toHaveLength(0);
  });

  it('returns valid: false for multiple dead-end nodes', () => {
    const multiDeadEnd = [
      ...DEAD_END_DIALOGUE_TREE,
      {
        id: 'npc_test_elder_dead_end_2',
        npcId: 'npc_test_elder',
        text: 'Another locked door.',
        options: [
          {
            id: 'opt_locked',
            text: 'Only the chosen...',
            gate: { trait: 'logic' as const, operator: 'gte' as const, value: 35 },
            nextNodeId: null,
          },
        ],
      },
    ];
    const result = validateDialogueTree(multiDeadEnd);
    expect(result.valid).toBe(false);
    expect(result.problematicNodes).toHaveLength(2);
    expect(result.problematicNodes).toContain('npc_test_elder_dead_end');
    expect(result.problematicNodes).toContain('npc_test_elder_dead_end_2');
  });
});
