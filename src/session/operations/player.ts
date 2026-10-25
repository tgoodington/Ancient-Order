import { ErrorCodes } from '../../types/index.js';
import type {
  GameState,
  Personality,
  PersonalityAdjustment,
  PlayerCharacter,
} from '../../types/index.js';
import { applyPersonalityAdjustment, updateTeamComposition } from '../../state/stateUpdaters.js';
import type { SessionContent } from '../content.js';
import { fail, fromThrown, ok } from '../result.js';
import type { SessionState } from '../sessionState.js';
import { validatePersonalityAdjustment, validateTeamIds } from '../validation.js';
import { noActiveGame, refuse } from './outcome.js';
import type { Outcome } from './outcome.js';

export function getPlayer(s: SessionState, _content: SessionContent): Outcome<PlayerCharacter> {
  if (s.game === null) return noActiveGame(s);
  return { next: s, result: ok(s.game.player) };
}

export function getPersonality(s: SessionState, _content: SessionContent): Outcome<Personality> {
  if (s.game === null) return noActiveGame(s);
  return { next: s, result: ok(s.game.player.personality) };
}

export function adjustPersonality(
  s: SessionState,
  _content: SessionContent,
  adjustment: unknown
): Outcome<GameState> {
  const invalid = validatePersonalityAdjustment(adjustment);
  if (invalid) return refuse(s, { ok: false, error: invalid });
  if (s.game === null) return noActiveGame(s);
  try {
    const game = applyPersonalityAdjustment(s.game, adjustment as PersonalityAdjustment);
    return { next: { ...s, game }, result: ok(game) };
  } catch (err) {
    return refuse(s, fromThrown(err));
  }
}

export function setTeam(
  s: SessionState,
  _content: SessionContent,
  npcIds: unknown
): Outcome<{ team: readonly string[] }> {
  const invalid = validateTeamIds(npcIds);
  if (invalid) return refuse(s, { ok: false, error: invalid });
  if (s.game === null) return noActiveGame(s);
  if (s.game.combatState !== null) {
    return refuse(s, fail(ErrorCodes.TEAM_COMPOSITION_INVALID, 'Cannot change team during combat'));
  }
  if (s.game.narrativeState !== null) {
    return refuse(
      s,
      fail(ErrorCodes.TEAM_COMPOSITION_INVALID, 'Cannot change team during narrative scene')
    );
  }
  for (const id of npcIds as string[]) {
    if (!Object.hasOwn(s.game.npcs, id)) {
      return refuse(s, fail(ErrorCodes.TEAM_COMPOSITION_INVALID, `Invalid NPC ID: ${id}`));
    }
  }
  try {
    const game = updateTeamComposition(s.game, npcIds as string[]);
    return { next: { ...s, game }, result: ok({ team: game.team }) };
  } catch (err) {
    return refuse(s, fromThrown(err));
  }
}
