import { ErrorCodes } from '../../types/index.js';
import type { NPC } from '../../types/index.js';
import { getAllNPCs, getNPC } from '../../state/npcs.js';
import type { SessionContent } from '../content.js';
import { fail, fromThrown, ok } from '../result.js';
import type { SessionState } from '../sessionState.js';
import { refuse } from './outcome.js';
import type { Outcome } from './outcome.js';

export function listNpcs(s: SessionState, _content: SessionContent): Outcome<NPC[]> {
  try {
    return { next: s, result: ok(getAllNPCs()) };
  } catch (err) {
    return refuse(s, fromThrown(err));
  }
}

export function getNpc(s: SessionState, _content: SessionContent, id: string): Outcome<NPC> {
  try {
    if (typeof id !== 'string') {
      return refuse(s, fail(ErrorCodes.VALIDATION_ERROR, 'id must be string'));
    }
    if (s.game !== null && Object.hasOwn(s.game.npcs, id)) {
      return { next: s, result: ok(s.game.npcs[id]) };
    }
    const template = getAllNPCs().some((n) => n.id === id) ? getNPC(id) : undefined;
    if (template === undefined) {
      return refuse(s, fail(ErrorCodes.NPC_NOT_FOUND, `NPC not found: "${id}"`));
    }
    return { next: s, result: ok(template) };
  } catch (err) {
    return refuse(s, fromThrown(err));
  }
}
