/**
 * Ancient Order - Dialogue Engine
 *
 * Handles dialogue tree traversal, personality gate evaluation,
 * and dialogue option filtering. Dialogue tree data is passed as a
 * parameter to every function (not stored on NPC) — enables fixture-based testing.
 */

import {
  GameState,
  NPC,
  DialogueNode,
  DialogueOption,
  PersonalityGate,
  Personality,
  DialogueResult,
} from '../types/index.js';
import { processDialogueChoice } from '../state/stateUpdaters.js';

// ============================================================================
// Gate Evaluation
// ============================================================================

/**
 * Evaluates a personality gate against player personality.
 *
 * @param gate - The personality gate to evaluate (always a gate, not nullable)
 * @param personality - The player's current personality
 * @returns True if the gate passes (option is available)
 */
export function evaluatePersonalityGate(gate: PersonalityGate, personality: Personality): boolean {
  const traitValue = personality[gate.trait];

  switch (gate.operator) {
    case 'gte':
      return traitValue >= gate.value;
    case 'lte':
      return traitValue <= gate.value;
    case 'eq':
      return Math.abs(traitValue - gate.value) < 0.1; // Float tolerance
    default:
      return false;
  }
}

// ============================================================================
// Option Filtering
// ============================================================================

/**
 * Gets all available dialogue options for a node given the current personality.
 * Options without a gate (gate === undefined) are always available.
 * Options with a gate are available only if the gate evaluates true.
 *
 * @param node - The dialogue node containing options to filter
 * @param personality - The player's current personality
 * @returns Array of options available to the player
 */
export function getAvailableOptions(
  node: DialogueNode,
  personality: Personality
): DialogueOption[] {
  return node.options.filter(opt => {
    if (opt.gate === undefined) {
      return true; // Ungated options always available
    }
    return evaluatePersonalityGate(opt.gate, personality);
  });
}

// ============================================================================
// Tree Traversal
// ============================================================================

/**
 * Gets the starting dialogue node for an NPC from a dialogue tree.
 * Convention: starting node ID is `${npcId}_greet`.
 *
 * @param npc - The NPC to start dialogue with
 * @param dialogueTree - The full dialogue tree to search
 * @returns The starting DialogueNode
 * @throws Error if the starting node is not found
 */
export function getStartingNode(npc: NPC, dialogueTree: DialogueNode[]): DialogueNode {
  const greetNodeId = `${npc.id}_greet`;
  const node = dialogueTree.find(n => n.id === greetNodeId);

  if (!node) {
    throw new Error(
      `Starting node not found for NPC "${npc.id}". Expected node id: "${greetNodeId}"`
    );
  }

  return node;
}

/**
 * Finds a dialogue node by ID within a dialogue tree.
 *
 * @param nodeId - The ID of the node to find
 * @param dialogueTree - The full dialogue tree to search
 * @returns The found DialogueNode or undefined if not found
 */
export function findNode(nodeId: string, dialogueTree: DialogueNode[]): DialogueNode | undefined {
  return dialogueTree.find(n => n.id === nodeId);
}

// ============================================================================
// Dialogue Selection Processing
// ============================================================================

/**
 * Processes a player's dialogue option selection.
 *
 * Validates the chosen option is available given current personality,
 * delegates to stateUpdaters.processDialogueChoice() for immutable state update,
 * and returns a DialogueResult with the updated state and next node.
 *
 * @param state - Current game state
 * @param npcId - ID of the NPC being spoken with
 * @param nodeId - ID of the current dialogue node
 * @param optionId - ID of the selected option
 * @param dialogueTree - The full dialogue tree for this NPC
 * @returns DialogueResult with updated state, next node, and selected option
 * @throws Error if node, option, or availability check fails
 */
export function processDialogueSelection(
  state: GameState,
  npcId: string,
  nodeId: string,
  optionId: string,
  dialogueTree: DialogueNode[]
): DialogueResult {
  // Locate the current node
  const currentNode = findNode(nodeId, dialogueTree);
  if (!currentNode) {
    throw new Error(`Dialogue node not found: "${nodeId}"`);
  }

  // Locate the selected option on that node
  const selectedOption = currentNode.options.find(opt => opt.id === optionId);
  if (!selectedOption) {
    throw new Error(`Dialogue option not found: "${optionId}" on node "${nodeId}"`);
  }

  // Confirm the option is actually available to the player
  const isAvailable =
    selectedOption.gate === undefined ||
    evaluatePersonalityGate(selectedOption.gate, state.player.personality);

  if (!isAvailable) {
    throw new Error(
      `Dialogue option "${optionId}" is not available. ` +
        `Gate: ${selectedOption.gate?.trait} ${selectedOption.gate?.operator} ${selectedOption.gate?.value}`
    );
  }

  // Delegate state updates to stateUpdaters (immutable)
  const newState = processDialogueChoice(state, npcId, nodeId, optionId, selectedOption);

  // Resolve the next node (null if conversation ends)
  let nextNode: DialogueNode | null = null;
  if (selectedOption.nextNodeId !== null) {
    nextNode = findNode(selectedOption.nextNodeId, dialogueTree) ?? null;
  }

  return {
    state: newState,
    nextNode,
    selectedOption,
  };
}

// ============================================================================
// Tree Validation
// ============================================================================

/**
 * Validates a dialogue tree for dead ends.
 *
 * A dead end is any node where ALL options have a gate — meaning there is
 * no ungated fallback. The game design constraint requires every node to
 * have at least one always-available option so players can never get stuck.
 *
 * @param nodes - The complete dialogue tree to validate
 * @returns Validation result with list of problematic node IDs
 */
export function validateDialogueTree(nodes: DialogueNode[]): {
  valid: boolean;
  problematicNodes: string[];
} {
  const problematicNodes: string[] = [];

  for (const node of nodes) {
    // A node is problematic if it has no ungated option (gate === undefined)
    const hasUngatedOption = node.options.some(opt => opt.gate === undefined);

    if (!hasUngatedOption) {
      problematicNodes.push(node.id);
    }
  }

  return {
    valid: problematicNodes.length === 0,
    problematicNodes,
  };
}
