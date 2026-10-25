import { ErrorCodes } from '../../types/index.js';
import type { ErrorCode, Personality } from '../../types/index.js';
import type {
  CurrentSceneResult,
  NarrativeState,
  Scene,
  SynergyResult,
} from '../../types/narrative.js';
import {
  clearNarrative,
  initializeNarrative,
  updateNarrativeState,
} from '../../state/stateUpdaters.js';
import { findScene, getCurrentScene as getEngineCurrentScene } from '../../narrative/sceneEngine.js';
import { advanceNarrative } from '../../narrative/narrativeStateMachine.js';
import { applyConsequence } from '../../narrative/choiceEngine.js';
import { calculateSynergy } from '../../narrative/synergyCalculator.js';
import { DEFAULT_PARADIGMS } from '../../fixtures/synergyConfig.js';
import type { SessionContent } from '../content.js';
import { fail, fromThrown, ok } from '../result.js';
import type { SessionState } from '../sessionState.js';
import { validateIdString } from '../validation.js';
import { noActiveGame, refuse } from './outcome.js';
import type { Outcome } from './outcome.js';

export function startScene(
  s: SessionState,
  content: SessionContent,
  startingSceneId: unknown
): Outcome<{ narrativeState: NarrativeState }> {
  const invalid = validateIdString('startingSceneId', startingSceneId, 128);
  if (invalid) return refuse(s, { ok: false, error: invalid });
  if (s.game === null) return noActiveGame(s);
  const sceneId = startingSceneId as string;
  try {
    if (findScene(sceneId, content.sceneGraph) === undefined) {
      return refuse(
        s,
        fail(ErrorCodes.SCENE_NOT_FOUND, `Scene "${sceneId}" not found in scene graph`)
      );
    }
    const game = initializeNarrative(s.game, sceneId);
    return {
      next: { ...s, game },
      result: ok({ narrativeState: game.narrativeState! }),
    };
  } catch (err) {
    return refuse(s, fromThrown(err));
  }
}

export function getCurrentScene(
  s: SessionState,
  content: SessionContent
): Outcome<CurrentSceneResult> {
  if (s.game === null) return noActiveGame(s);
  const { narrativeState, player } = s.game;
  if (narrativeState === null) {
    return refuse(
      s,
      fail(ErrorCodes.NARRATIVE_NOT_STARTED, 'Narrative has not been started')
    );
  }
  try {
    const result = getEngineCurrentScene(narrativeState, player.personality, content.sceneGraph);
    if (result === null) {
      return refuse(
        s,
        fail(
          ErrorCodes.SCENE_NOT_FOUND,
          `Scene "${narrativeState.currentSceneId}" not found in scene graph`
        )
      );
    }
    return { next: s, result: ok(result) };
  } catch (err) {
    return refuse(s, fromThrown(err));
  }
}

export function makeChoice(
  s: SessionState,
  content: SessionContent,
  choiceId: unknown
): Outcome<{ narrativeState: NarrativeState; nextScene: Scene | null }> {
  const invalid = validateIdString('choiceId', choiceId, 128);
  if (invalid) return refuse(s, { ok: false, error: invalid });
  if (s.game === null) return noActiveGame(s);
  const gameState = s.game;
  const id = choiceId as string;
  try {
    const transition = advanceNarrative(gameState, id, content.sceneGraph);
    if (transition.type === 'error') {
      return refuse(s, fail(transition.code as ErrorCode, transition.message));
    }

    // Consequence is applied BEFORE the narrative state advances: choice flags gate later scenes.
    const currentScene = content.sceneGraph.find(
      (sc) => sc.id === gameState.narrativeState!.currentSceneId
    );
    const choice = currentScene?.choices.find((c) => c.id === id);

    let updated = gameState;
    if (choice?.consequence) {
      updated = applyConsequence(gameState, choice.consequence);
    }
    const game = updateNarrativeState(updated, transition.state);
    return {
      next: { ...s, game },
      result: ok({ narrativeState: transition.state, nextScene: transition.nextScene }),
    };
  } catch (err) {
    return refuse(s, fromThrown(err));
  }
}

export function getNarrativeState(
  s: SessionState,
  _content: SessionContent
): Outcome<{ narrativeState: NarrativeState | null }> {
  if (s.game === null) return noActiveGame(s);
  return { next: s, result: ok({ narrativeState: s.game.narrativeState }) };
}

export function resetNarrative(
  s: SessionState,
  _content: SessionContent
): Outcome<{ narrativeState: null }> {
  if (s.game === null) return noActiveGame(s);
  try {
    const game = clearNarrative(s.game);
    return { next: { ...s, game }, result: ok({ narrativeState: null }) };
  } catch (err) {
    return refuse(s, fromThrown(err));
  }
}

export function getSynergy(
  s: SessionState,
  _content: SessionContent
): Outcome<{ synergy: SynergyResult }> {
  if (s.game === null) return noActiveGame(s);
  try {
    const npcPersonalities: Personality[] = Object.values(s.game.npcs)
      .filter((npc) => npc?.personality)
      .map((npc) => npc.personality);
    const synergy = calculateSynergy(s.game.player.personality, npcPersonalities, DEFAULT_PARADIGMS);
    return { next: s, result: ok({ synergy }) };
  } catch (err) {
    return refuse(s, fromThrown(err));
  }
}
