import type { GameState, NPC, Personality, PlayerCharacter } from '../types/index.js';
import type {
  CurrentSceneResult,
  NarrativeState,
  Scene,
  SynergyResult,
} from '../types/narrative.js';
import { defaultContent } from './content.js';
import type { SessionContent } from './content.js';
import { deepCloneFrozen } from './freeze.js';
import { fromThrown } from './result.js';
import type { SessionResult } from './result.js';
import { initialSessionState } from './sessionState.js';
import type { SessionState } from './sessionState.js';
import { createMemoryBackend, createSaveStore } from '../persistence/index.js';
import type { SaveMetadata, SaveSlotInfo, SaveStore } from '../persistence/index.js';
import { deleteSlot, listSlots, loadFromSlot, saveToSlot } from './operations/saves.js';
import type { Outcome } from './operations/outcome.js';
import { getState, newGame } from './operations/game.js';
import { adjustPersonality, getPersonality, getPlayer, setTeam } from './operations/player.js';
import { getNpc, listNpcs } from './operations/npc.js';
import {
  getCurrentScene,
  getNarrativeState,
  getSynergy,
  makeChoice,
  resetNarrative,
  startScene,
} from './operations/narrative.js';
import { chooseDialogueOption, startDialogue } from './operations/dialogue.js';
import type { ChooseDialogueResult, StartDialogueResult } from './operations/dialogue.js';
import {
  advanceRound,
  declareActions,
  getCombatResult,
  getCombatView,
  getRoundHistory,
  startEncounter,
} from './operations/combat.js';
import type {
  AdvanceRoundResult,
  CombatResultView,
  DeclareActionsResult,
  StartEncounterResult,
} from './operations/combat.js';
import type { CombatState } from '../types/combat.js';
import type { VisualInfo } from '../combat/roundManager.js';

export interface GameSessionOptions {
  content?: Partial<SessionContent>;
  saves?: SaveStore;
}

export interface GameSession {
  newGame(): SessionResult<GameState>;
  getState(): SessionResult<GameState>;
  getPlayer(): SessionResult<PlayerCharacter>;
  getPersonality(): SessionResult<Personality>;
  adjustPersonality(adjustment: unknown): SessionResult<GameState>;
  setTeam(npcIds: unknown): SessionResult<{ team: readonly string[] }>;
  listNpcs(): SessionResult<NPC[]>;
  getNpc(id: string): SessionResult<NPC>;
  startScene(startingSceneId: unknown): SessionResult<{ narrativeState: NarrativeState }>;
  getCurrentScene(): SessionResult<CurrentSceneResult>;
  makeChoice(
    choiceId: unknown
  ): SessionResult<{ narrativeState: NarrativeState; nextScene: Scene | null }>;
  getNarrativeState(): SessionResult<{ narrativeState: NarrativeState | null }>;
  resetNarrative(): SessionResult<{ narrativeState: null }>;
  getSynergy(): SessionResult<{ synergy: SynergyResult }>;
  startDialogue(npcId: unknown): SessionResult<StartDialogueResult>;
  chooseDialogueOption(optionId: unknown): SessionResult<ChooseDialogueResult>;
  startEncounter(encounterId: unknown): SessionResult<StartEncounterResult>;
  declareActions(actions: unknown): SessionResult<DeclareActionsResult>;
  advanceRound(): SessionResult<AdvanceRoundResult>;
  getCombatView(): SessionResult<VisualInfo>;
  getRoundHistory(): SessionResult<CombatState['roundHistory']>;
  getCombatResult(): SessionResult<CombatResultView>;
  saveToSlot(slot: unknown): Promise<SessionResult<SaveMetadata>>;
  loadFromSlot(slot: unknown): Promise<SessionResult<GameState>>;
  listSlots(): Promise<SessionResult<SaveSlotInfo[]>>;
  deleteSlot(slot: unknown): Promise<SessionResult<{ slot: number; deleted: true }>>;
  getSnapshot(): SessionState;
  subscribe(listener: () => void): () => void;
}

export function createGameSession(options: GameSessionOptions = {}): GameSession {
  const content: SessionContent = deepCloneFrozen({ ...defaultContent, ...options.content });
  let snapshot: SessionState = initialSessionState;
  const saves: SaveStore = options.saves ?? createSaveStore(createMemoryBackend());
  const listeners = new Set<() => void>();

  function commit<T>(outcome: Outcome<T>, startedFrom: SessionState): SessionResult<T> {
    if (outcome.next !== startedFrom) {
      snapshot = outcome.next;
      for (const listener of [...listeners]) listener();
    }
    return outcome.result;
  }

  function run<T>(op: (s: SessionState, c: SessionContent) => Outcome<T>): SessionResult<T> {
    const started = snapshot;
    let outcome: Outcome<T>;
    try {
      outcome = op(started, content);
    } catch (err) {
      return fromThrown(err);
    }
    return commit(outcome, started);
  }

  // Async variant of run. Commit is decided against the snapshot the operation started from
  // (not the current one), so a refusal or read never overwrites commits made while it awaited.
  async function runAsync<T>(
    op: (s: SessionState, store: SaveStore) => Promise<Outcome<T>>
  ): Promise<SessionResult<T>> {
    const started = snapshot;
    let outcome: Outcome<T>;
    try {
      outcome = await op(started, saves);
    } catch (err) {
      return fromThrown(err);
    }
    return commit(outcome, started);
  }

  return {
    newGame: () => run(newGame),
    getState: () => run(getState),
    getPlayer: () => run(getPlayer),
    getPersonality: () => run(getPersonality),
    adjustPersonality: (adjustment) => run((s, c) => adjustPersonality(s, c, adjustment)),
    setTeam: (npcIds) => run((s, c) => setTeam(s, c, npcIds)),
    listNpcs: () => run(listNpcs),
    getNpc: (id) => run((s, c) => getNpc(s, c, id)),
    startScene: (id) => run((s, c) => startScene(s, c, id)),
    getCurrentScene: () => run(getCurrentScene),
    makeChoice: (id) => run((s, c) => makeChoice(s, c, id)),
    getNarrativeState: () => run(getNarrativeState),
    resetNarrative: () => run(resetNarrative),
    getSynergy: () => run(getSynergy),
    startDialogue: (npcId) => run((s, c) => startDialogue(s, c, npcId)),
    chooseDialogueOption: (optionId) => run((s, c) => chooseDialogueOption(s, c, optionId)),
    startEncounter: (id) => run((s, c) => startEncounter(s, c, id)),
    declareActions: (actions) => run((s, c) => declareActions(s, c, actions)),
    advanceRound: () => run(advanceRound),
    getCombatView: () => run(getCombatView),
    getRoundHistory: () => run(getRoundHistory),
    getCombatResult: () => run(getCombatResult),
    saveToSlot: (slot) => runAsync((s, store) => saveToSlot(s, store, slot)),
    loadFromSlot: (slot) => runAsync((s, store) => loadFromSlot(s, store, slot)),
    listSlots: () => runAsync(listSlots),
    deleteSlot: (slot) => runAsync((s, store) => deleteSlot(s, store, slot)),
    getSnapshot: () => snapshot,
    subscribe: (listener) => {
      listeners.add(listener);
      return () => {
        listeners.delete(listener);
      };
    },
  };
}
