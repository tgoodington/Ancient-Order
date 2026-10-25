/**
 * Ancient Order - Save backends
 *
 * Platform-neutral key/value storage behind an async interface. The real
 * window.localStorage is injected by the app; a desktop build can add a file backend.
 */

import { storageError } from './errors.js';

export interface SaveBackend {
  read(key: string): Promise<string | null>;
  write(key: string, value: string): Promise<void>;
  remove(key: string): Promise<void>;
}

// Declared locally so persistence needs no DOM types.
export interface StorageLike {
  getItem(key: string): string | null;
  setItem(key: string, value: string): void;
  removeItem(key: string): void;
}

export function createMemoryBackend(): SaveBackend {
  const entries = new Map<string, string>();
  return {
    read: async (key) => entries.get(key) ?? null,
    write: async (key, value) => {
      entries.set(key, value);
    },
    remove: async (key) => {
      entries.delete(key);
    },
  };
}

function isQuotaError(err: unknown): boolean {
  if (typeof err !== 'object' || err === null) return false;
  const { name, code } = err as { name?: unknown; code?: unknown };
  return (
    name === 'QuotaExceededError' ||
    name === 'NS_ERROR_DOM_QUOTA_REACHED' ||
    code === 22 ||
    code === 1014
  );
}

const UNAVAILABLE = 'Save storage is unavailable';

export function createWebStorageBackend(storage: StorageLike): SaveBackend {
  return {
    read: async (key) => {
      try {
        return storage.getItem(key);
      } catch (err) {
        throw storageError(UNAVAILABLE, err);
      }
    },
    write: async (key, value) => {
      try {
        storage.setItem(key, value);
      } catch (err) {
        throw storageError(
          isQuotaError(err) ? 'Not enough storage space to save' : UNAVAILABLE,
          err
        );
      }
    },
    remove: async (key) => {
      try {
        storage.removeItem(key);
      } catch (err) {
        throw storageError(UNAVAILABLE, err);
      }
    },
  };
}
