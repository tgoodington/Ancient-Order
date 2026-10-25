/**
 * Ancient Order - Save format
 *
 * Versioned, labeled save envelope; kind-namespaced keys; parse and older-save normalization.
 * Parsed JSON is untrusted: it only becomes a GameState after validateGameState passes.
 */

import type { GameState } from '../types/index.js';
import { validateGameState } from './validation.js';

export const SAVE_FORMAT = 'ancient-order-save';
export const SAVE_VERSION = 1;
export const MIN_SLOT = 1;
export const MAX_SLOT = 10;
export const MAX_SAVE_BYTES = 2_000_000;
const MAX_REASONS = 10;
const MAX_REASON_LENGTH = 200;

// 'auto' is reserved for future autosaves; only 'manual' is used this sprint.
export type SaveKind = 'manual';

export interface SaveEnvelope {
  format: typeof SAVE_FORMAT;
  version: number;
  kind: SaveKind;
  slot: number;
  savedAt: number;
  playerName: string;
  game: GameState;
}

export type ParseResult =
  | { ok: true; envelope: SaveEnvelope }
  | { ok: false; reasons: string[] };

export function isValidSlot(slot: unknown): slot is number {
  return typeof slot === 'number' && Number.isInteger(slot) && slot >= MIN_SLOT && slot <= MAX_SLOT;
}

// Callers must have passed isValidSlot first; the key is never built from an unchecked slot.
export function saveKey(kind: SaveKind, slot: number): string {
  return `ancient-order/saves/${kind}/${slot}`;
}

function own(obj: object, key: string): unknown {
  return Object.prototype.hasOwnProperty.call(obj, key)
    ? (obj as Record<string, unknown>)[key]
    : undefined;
}

// Reasons can echo attacker-controlled keys, so cap their count and length.
function failure(reasons: string[]): ParseResult {
  return {
    ok: false,
    reasons: reasons.slice(0, MAX_REASONS).map((r) => r.slice(0, MAX_REASON_LENGTH)),
  };
}

// Never throws: validateGameState can throw on crafted saves, which counts as corrupted.
export function parseSave(raw: string): ParseResult {
  if (raw.length > MAX_SAVE_BYTES) return failure(['save too large']);
  try {
    return parseSaveUnchecked(raw);
  } catch {
    return failure(['invalid game data']);
  }
}

function parseSaveUnchecked(raw: string): ParseResult {
  let data: unknown;
  try {
    data = JSON.parse(raw);
  } catch {
    return failure(['invalid JSON']);
  }
  if (data === null || typeof data !== 'object' || Array.isArray(data)) {
    return failure(['save envelope must be an object']);
  }
  if (own(data, 'format') !== SAVE_FORMAT) {
    return failure(['wrong format tag']);
  }
  const version = own(data, 'version');
  // Future migrations hook in here: versions below SAVE_VERSION would be upgraded.
  if (version !== SAVE_VERSION) {
    return failure([`unsupported version ${String(version)}`]);
  }
  const kind = own(data, 'kind');
  const slot = own(data, 'slot');
  const savedAt = own(data, 'savedAt');
  const playerName = own(data, 'playerName');
  const reasons: string[] = [];
  if (kind !== 'manual') reasons.push('kind: unsupported');
  if (!isValidSlot(slot)) reasons.push('slot: expected integer between 1 and 10');
  if (typeof savedAt !== 'number' || !Number.isFinite(savedAt)) reasons.push('savedAt: expected number');
  if (typeof playerName !== 'string') reasons.push('playerName: expected string');
  const game = own(data, 'game');
  const validation = validateGameState(game);
  if (!validation.valid) reasons.push(...validation.errors);
  if (reasons.length > 0) return failure(reasons);
  return {
    ok: true,
    envelope: {
      format: SAVE_FORMAT,
      version: SAVE_VERSION,
      kind: 'manual',
      slot: slot as number,
      savedAt: savedAt as number,
      playerName: playerName as string,
      game: normalizeOlderSave(game as GameState),
    },
  };
}

// Today's defaults for saves written before these fields existed. Applied after validation.
// Builds a new object (spread defines own properties, so a "__proto__" key cannot reach a prototype).
export function normalizeOlderSave(game: GameState): GameState {
  const g = game as unknown as Record<string, unknown>;
  return {
    ...game,
    narrativeState: g.narrativeState === undefined ? null : g.narrativeState,
    team: g.team === undefined ? [] : g.team,
    reactionProgress: g.reactionProgress === undefined ? {} : g.reactionProgress,
  } as GameState;
}
