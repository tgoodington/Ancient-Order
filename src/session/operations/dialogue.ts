import { ErrorCodes } from '../../types/index.js';
import type { DialogueNode, DialogueOption, GameState } from '../../types/index.js';
import { setCurrentDialogueNode } from '../../state/stateUpdaters.js';
import { getAllNPCs, getNPC } from '../../state/npcs.js';
import {
  evaluatePersonalityGate,
  findNode,
  getAvailableOptions,
  getStartingNode,
  processDialogueSelection,
} from '../../dialogue/dialogueEngine.js';
import type { SessionContent } from '../content.js';
import { fail, fromThrown, ok } from '../result.js';
import type { SessionState } from '../sessionState.js';
import { validateIdString } from '../validation.js';
import { noActiveGame, refuse } from './outcome.js';
import type { Outcome } from './outcome.js';

export interface StartDialogueResult {
  node: DialogueNode;
  availableOptions: DialogueOption[];
}

export interface ChooseDialogueResult {
  state: GameState;
  nextNode: DialogueNode | null;
  availableOptions: DialogueOption[];
  selectedOptionId: string;
}

function findNodeOwner(
  content: SessionContent,
  nodeId: string
): { npcId: string; tree: DialogueNode[] } | undefined {
  for (const [npcId, tree] of Object.entries(content.dialogues)) {
    if (findNode(nodeId, tree) !== undefined) return { npcId, tree };
  }
  return undefined;
}

export function startDialogue(
  s: SessionState,
  content: SessionContent,
  npcId: unknown
): Outcome<StartDialogueResult> {
  const invalid = validateIdString('npcId', npcId, undefined);
  if (invalid) return refuse(s, { ok: false, error: invalid });
  if (s.game === null) return noActiveGame(s);
  const id = npcId as string;
  try {
    const npc = getAllNPCs().some((n) => n.id === id) ? getNPC(id) : undefined;
    if (npc === undefined) {
      return refuse(s, fail(ErrorCodes.NPC_NOT_FOUND, `NPC not found: "${id}"`));
    }
    const tree = Object.hasOwn(content.dialogues, id) ? content.dialogues[id] : undefined;
    const greetNodeId = `${npc.id}_greet`;
    if (tree === undefined || findNode(greetNodeId, tree) === undefined) {
      return refuse(
        s,
        fail(
          ErrorCodes.DIALOGUE_NODE_NOT_FOUND,
          `Starting node not found for NPC "${npc.id}". Expected node id: "${greetNodeId}"`
        )
      );
    }
    const node = getStartingNode(npc, tree);
    const game = setCurrentDialogueNode(s.game, node.id);
    return {
      next: { ...s, game },
      result: ok({
        node,
        availableOptions: getAvailableOptions(node, game.player.personality),
      }),
    };
  } catch (err) {
    return refuse(s, fromThrown(err));
  }
}

export function chooseDialogueOption(
  s: SessionState,
  content: SessionContent,
  optionId: unknown
): Outcome<ChooseDialogueResult> {
  const invalid = validateIdString('optionId', optionId, undefined);
  if (invalid) return refuse(s, { ok: false, error: invalid });
  if (s.game === null) return noActiveGame(s);
  const id = optionId as string;
  const nodeId = s.game.currentDialogueNode;
  if (nodeId === null) {
    return refuse(s, fail(ErrorCodes.DIALOGUE_NOT_ACTIVE, 'No active dialogue'));
  }
  try {
    const owner = findNodeOwner(content, nodeId);
    const node = owner && findNode(nodeId, owner.tree);
    if (!owner || !node) {
      return refuse(s, fail(ErrorCodes.DIALOGUE_NODE_NOT_FOUND, `Dialogue node not found: "${nodeId}"`));
    }
    const option = node.options.find((o) => o.id === id);
    if (!option) {
      return refuse(
        s,
        fail(
          ErrorCodes.DIALOGUE_OPTION_NOT_FOUND,
          `Dialogue option not found: "${id}" on node "${nodeId}"`
        )
      );
    }
    if (option.gate !== undefined && !evaluatePersonalityGate(option.gate, s.game.player.personality)) {
      return refuse(
        s,
        fail(
          ErrorCodes.DIALOGUE_OPTION_NOT_AVAILABLE,
          `Dialogue option "${id}" is not available. ` +
            `Gate: ${option.gate.trait} ${option.gate.operator} ${option.gate.value}`
        )
      );
    }

    const selection = processDialogueSelection(s.game, owner.npcId, nodeId, id, owner.tree);
    const game = setCurrentDialogueNode(selection.state, selection.nextNode?.id ?? null);
    return {
      next: { ...s, game },
      result: ok({
        state: game,
        nextNode: selection.nextNode,
        availableOptions: selection.nextNode
          ? getAvailableOptions(selection.nextNode, game.player.personality)
          : [],
        selectedOptionId: selection.selectedOption.id,
      }),
    };
  } catch (err) {
    return refuse(s, fromThrown(err));
  }
}
