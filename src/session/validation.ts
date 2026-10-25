import { ErrorCodes } from '../types/index.js';
import { PERSONALITY_TRAITS } from '../personality/personalitySystem.js';
import type { SessionError } from './result.js';

function invalid(message: string): SessionError {
  return { code: ErrorCodes.VALIDATION_ERROR, message };
}

export function validatePersonalityAdjustment(input: unknown): SessionError | null {
  if (typeof input !== 'object' || input === null || Array.isArray(input)) {
    return invalid('body must be object');
  }
  const keys = Object.keys(input);
  if (keys.length < 1) {
    return invalid('body must NOT have fewer than 1 properties');
  }
  const record = input as Record<string, unknown>;
  for (const key of keys) {
    if (!(PERSONALITY_TRAITS as readonly string[]).includes(key)) {
      return invalid('body must NOT have additional properties');
    }
    const value = record[key];
    if (typeof value !== 'number' || !Number.isFinite(value)) {
      return invalid(`body/${key} must be number`);
    }
  }
  return null;
}

export function validateIdString(name: string, input: unknown, maxLength?: number): SessionError | null {
  if (typeof input !== 'string') {
    return invalid(`${name} must be string`);
  }
  if (maxLength !== undefined && input.length > maxLength) {
    return invalid(`${name} must NOT have more than ${maxLength} characters`);
  }
  return null;
}

const ACTION_KEYS: ReadonlySet<string> = new Set(['combatantId', 'type', 'targetId', 'energySegments']);

// Mirrors the old /declare body schema (actions array, max 10, strict item shape).
export function validateCombatActions(input: unknown): SessionError | null {
  if (!Array.isArray(input)) {
    return invalid('body/actions must be array');
  }
  if (input.length > 10) {
    return invalid('body/actions must NOT have more than 10 items');
  }
  for (let i = 0; i < input.length; i++) {
    const item: unknown = input[i];
    const at = `body/actions/${i}`;
    if (typeof item !== 'object' || item === null || Array.isArray(item)) {
      return invalid(`${at} must be object`);
    }
    const record = item as Record<string, unknown>;
    for (const required of ['combatantId', 'type']) {
      if (!Object.hasOwn(record, required)) {
        return invalid(`${at} must have required property '${required}'`);
      }
    }
    for (const key of Object.keys(record)) {
      if (!ACTION_KEYS.has(key)) {
        return invalid(`${at} must NOT have additional properties`);
      }
    }
    if (typeof record.combatantId !== 'string') return invalid(`${at}/combatantId must be string`);
    if (typeof record.type !== 'string') return invalid(`${at}/type must be string`);
    const target = record.targetId;
    if (target !== undefined && target !== null && typeof target !== 'string') {
      return invalid(`${at}/targetId must be string,null`);
    }
    const energy = record.energySegments;
    if (energy !== undefined && (typeof energy !== 'number' || !Number.isFinite(energy))) {
      return invalid(`${at}/energySegments must be number`);
    }
  }
  return null;
}

export function validateTeamIds(input: unknown): SessionError | null {
  if (!Array.isArray(input)) {
    return invalid('npcIds must be array');
  }
  if (input.length !== 2) {
    return invalid('npcIds must contain exactly 2 items');
  }
  for (let i = 0; i < input.length; i++) {
    const id: unknown = input[i];
    if (typeof id !== 'string') {
      return invalid(`npcIds/${i} must be string`);
    }
    if (id.length > 64) {
      return invalid(`npcIds/${i} must NOT have more than 64 characters`);
    }
  }
  return null;
}
