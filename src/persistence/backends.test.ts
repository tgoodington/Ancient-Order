/**
 * Tests for the web-storage backend over a Map-based fake StorageLike.
 */

import { describe, it, expect } from 'vitest';

import { createMemoryBackend, createWebStorageBackend } from './index.js';
import type { StorageLike } from './index.js';
import { ErrorCodes } from '../types/index.js';

function fakeStorage(overrides: Partial<StorageLike> = {}): StorageLike & { map: Map<string, string> } {
  const map = new Map<string, string>();
  return {
    map,
    getItem: (k) => map.get(k) ?? null,
    setItem: (k, v) => {
      map.set(k, v);
    },
    removeItem: (k) => {
      map.delete(k);
    },
    ...overrides,
  };
}

async function rejection(promise: Promise<unknown>): Promise<Error & { code?: string; cause?: unknown }> {
  try {
    await promise;
  } catch (err) {
    return err as Error & { code?: string };
  }
  throw new Error('Expected a rejection');
}

describe('createMemoryBackend', () => {
  it('reads null for a missing key, then returns what was written', async () => {
    const b = createMemoryBackend();
    expect(await b.read('k')).toBeNull();
    await b.write('k', 'v');
    expect(await b.read('k')).toBe('v');
  });

  it('removes keys and ignores removing a missing key', async () => {
    const b = createMemoryBackend();
    await b.write('k', 'v');
    await b.remove('k');
    await b.remove('k');
    expect(await b.read('k')).toBeNull();
  });

  it('keeps separate instances isolated', async () => {
    const a = createMemoryBackend();
    const b = createMemoryBackend();
    await a.write('k', 'v');
    expect(await b.read('k')).toBeNull();
  });
});

describe('createWebStorageBackend', () => {
  it('writes, reads, and removes through the storage', async () => {
    const storage = fakeStorage();
    const b = createWebStorageBackend(storage);
    expect(await b.read('k')).toBeNull();
    await b.write('k', 'v');
    expect(storage.map.get('k')).toBe('v');
    expect(await b.read('k')).toBe('v');
    await b.remove('k');
    expect(storage.map.has('k')).toBe(false);
  });

  it('maps a thrown read to STORAGE_ERROR', async () => {
    const cause = new Error('denied');
    const b = createWebStorageBackend(
      fakeStorage({
        getItem: () => {
          throw cause;
        },
      })
    );
    const err = await rejection(b.read('k'));
    expect(err.code).toBe(ErrorCodes.STORAGE_ERROR);
    expect(err.cause).toBe(cause);
  });

  it('maps a thrown remove to STORAGE_ERROR', async () => {
    const b = createWebStorageBackend(
      fakeStorage({
        removeItem: () => {
          throw new Error('denied');
        },
      })
    );
    expect((await rejection(b.remove('k'))).code).toBe(ErrorCodes.STORAGE_ERROR);
  });

  it('maps a generic write failure to STORAGE_ERROR as unavailable', async () => {
    const b = createWebStorageBackend(
      fakeStorage({
        setItem: () => {
          throw new Error('denied');
        },
      })
    );
    const err = await rejection(b.write('k', 'v'));
    expect(err.code).toBe(ErrorCodes.STORAGE_ERROR);
    expect(err.message).toBe('Save storage is unavailable');
  });

  const quotaCases: [string, unknown][] = [
    ['QuotaExceededError name', Object.assign(new Error('full'), { name: 'QuotaExceededError' })],
    ['Firefox name', Object.assign(new Error('full'), { name: 'NS_ERROR_DOM_QUOTA_REACHED' })],
    ['legacy code 22', Object.assign(new Error('full'), { code: 22 })],
    ['Firefox code 1014', Object.assign(new Error('full'), { code: 1014 })],
  ];

  for (const [label, thrown] of quotaCases) {
    it(`maps a quota error (${label}) to STORAGE_ERROR with the space message`, async () => {
      const b = createWebStorageBackend(
        fakeStorage({
          setItem: () => {
            throw thrown;
          },
        })
      );
      const err = await rejection(b.write('k', 'v'));
      expect(err.code).toBe(ErrorCodes.STORAGE_ERROR);
      expect(err.message).toBe('Not enough storage space to save');
      expect(err.cause).toBe(thrown);
    });
  }

  it('treats a non-object throw as unavailable, not quota', async () => {
    const b = createWebStorageBackend(
      fakeStorage({
        setItem: () => {
          throw 'boom';
        },
      })
    );
    const err = await rejection(b.write('k', 'v'));
    expect(err.message).toBe('Save storage is unavailable');
  });
});
