/**
 * Ancient Order - Dialogue Engine
 *
 * Handles dialogue tree traversal, personality gate evaluation,
 * and dialogue option filtering.
 */

import {
  GameState,
  NPC,
  DialogueNode,
  DialogueOption,
  PersonalityGate,
  Personality,
  DialogueNodeResponse,
  DialogueOptionResponse,
  DialogueChoiceResponse,
  ConversationEntry,
} from '../types';
import { processDialogueChoice } from '../state/stateUpdaters';

/**
 * Evaluates a personality gate against player personality.
 *
 * @param gate - The personality gate to evaluate
 * @param personality - The player's current personality
 * @returns True if the gate passes (option is available)
 */
export function evaluatePersonalityGate(
  gate: PersonalityGate | null,
  personality: Personality
): boolean {
  // No gate = always available
  if (gate === null) {
    return true;
  }

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

/**
 * Checks if a dialogue option is available to the player.
 */
export function isOptionAvailable(
  option: DialogueOption,
  personality: Personality
): boolean {
  return evaluatePersonalityGate(option.personalityGate, personality);
}

/**
 * Gets a dialogue node from an NPC's dialogue tree.
 */
export function getDialogueNode(
  npc: NPC,
  nodeId: string
): DialogueNode | undefined {
  return npc.dialogueTree.find(node => node.id === nodeId);
}

/**
 * Gets the starting dialogue node for an NPC.
 * Convention: Starting node ID is `${npcId}_greet` with 'npc_' prefix removed.
 */
export function getStartingNode(npc: NPC): DialogueNode | undefined {
  // Extract the base name (e.g., 'scout_elena' from 'npc_scout_elena')
  const baseName = npc.id.replace(/^npc_/, '');
  const greetNodeId = `${baseName}_greet`;

  // Try to find the greet node
  let node = npc.dialogueTree.find(n => n.id === greetNodeId);

  // Fallback: try with just the last part of the name
  if (!node) {
    const shortName = baseName.split('_').pop();
    node = npc.dialogueTree.find(n => n.id === `${shortName}_greet`);
  }

  // Final fallback: return the first node
  if (!node && npc.dialogueTree.length > 0) {
    node = npc.dialogueTree[0];
  }

  return node;
}

/**
 * Gets a dialogue option from a node.
 */
export function getDialogueOption(
  node: DialogueNode,
  optionId: string
): DialogueOption | undefined {
  return node.options.find(opt => opt.id === optionId);
}

/**
 * Transforms a dialogue node into a response format.
 * Includes availability status for each option based on player personality.
 */
export function createDialogueNodeResponse(
  node: DialogueNode,
  personality: Personality
): DialogueNodeResponse {
  return {
    nodeId: node.id,
    speakerId: node.speakerId,
    text: node.text,
    options: node.options.map(opt => ({
      id: opt.id,
      text: opt.text,
      available: isOptionAvailable(opt, personality),
      personalityGate: opt.personalityGate,
    })),
  };
}

/**
 * Validates that at least one option is available (no dead ends).
 * This is a design constraint check - every node should have at least one ungated option.
 */
export function validateNoDeadEnds(
  node: DialogueNode,
  personality: Personality
): { valid: boolean; availableCount: number } {
  const availableOptions = node.options.filter(opt =>
    isOptionAvailable(opt, personality)
  );

  return {
    valid: availableOptions.length > 0,
    availableCount: availableOptions.length,
  };
}

/**
 * Gets all available dialogue options for a node.
 */
export function getAvailableOptions(
  node: DialogueNode,
  personality: Personality
): DialogueOption[] {
  return node.options.filter(opt => isOptionAvailable(opt, personality));
}

/**
 * Processes a dialogue choice and returns the result.
 *
 * @param state - Current game state
 * @param npcId - The NPC being talked to
 * @param currentNodeId - The current dialogue node ID
 * @param optionId - The selected option ID
 * @returns Result with updated state and next node (if any)
 */
export function processDialogueSelection(
  state: GameState,
  npcId: string,
  currentNodeId: string,
  optionId: string
): {
  success: boolean;
  error?: string;
  errorCode?: string;
  result?: DialogueChoiceResponse;
} {
  // Get the NPC
  const npc = state.npcs[npcId];
  if (!npc) {
    return {
      success: false,
      error: `NPC not found: ${npcId}`,
      errorCode: 'NPC_NOT_FOUND',
    };
  }

  // Get the current node
  const currentNode = getDialogueNode(npc, currentNodeId);
  if (!currentNode) {
    return {
      success: false,
      error: `Dialogue node not found: ${currentNodeId}`,
      errorCode: 'DIALOGUE_NODE_NOT_FOUND',
    };
  }

  // Get the selected option
  const option = getDialogueOption(currentNode, optionId);
  if (!option) {
    return {
      success: false,
      error: `Dialogue option not found: ${optionId}`,
      errorCode: 'DIALOGUE_OPTION_NOT_FOUND',
    };
  }

  // Check if option is available
  if (!isOptionAvailable(option, state.player.personality)) {
    return {
      success: false,
      error: `Dialogue option not available. Gate: ${option.personalityGate?.trait} ${option.personalityGate?.operator} ${option.personalityGate?.value}`,
      errorCode: 'DIALOGUE_OPTION_NOT_AVAILABLE',
    };
  }

  // Store personality before change
  const personalityBefore = { ...state.player.personality };

  // Process the dialogue choice (updates state immutably)
  const newState = processDialogueChoice(
    state,
    npcId,
    currentNodeId,
    optionId,
    option.text,
    option.personalityAdjustment,
    option.affectionChange,
    option.trustChange
  );

  // Get next node (if any)
  let nextNode: DialogueNodeResponse | null = null;
  if (option.nextNodeId) {
    const nextDialogueNode = getDialogueNode(npc, option.nextNodeId);
    if (nextDialogueNode) {
      nextNode = createDialogueNodeResponse(nextDialogueNode, newState.player.personality);
    }
  }

  return {
    success: true,
    result: {
      success: true,
      personalityBefore,
      personalityAfter: newState.player.personality,
      consequenceText: option.consequenceText,
      npcAffectionChange: option.affectionChange,
      npcTrustChange: option.trustChange,
      nextNode,
      gameState: newState,
    },
  };
}

/**
 * Gets the full dialogue tree for an NPC (for debugging/admin).
 */
export function getFullDialogueTree(npc: NPC): DialogueNode[] {
  return npc.dialogueTree;
}

/**
 * Validates an entire dialogue tree for dead ends.
 * Returns any nodes that could trap players.
 */
export function validateDialogueTree(
  dialogueTree: DialogueNode[]
): { valid: boolean; problematicNodes: string[] } {
  const problematicNodes: string[] = [];

  for (const node of dialogueTree) {
    // Check if node has at least one option with no gate (always available)
    const hasUngatedOption = node.options.some(opt => opt.personalityGate === null);

    if (!hasUngatedOption) {
      problematicNodes.push(node.id);
    }
  }

  return {
    valid: problematicNodes.length === 0,
    problematicNodes,
  };
}
