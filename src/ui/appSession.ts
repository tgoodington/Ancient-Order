import { createGameSession } from '../session/index';
import type { GameSession } from '../session/index';
import { createMemoryBackend, createSaveStore, createWebStorageBackend } from '../persistence/index';

function createBrowserSaveStore() {
  try {
    return createSaveStore(createWebStorageBackend(window.localStorage));
  } catch {
    // Accessing localStorage throws when storage is disabled; saves then live in memory only.
    return createSaveStore(createMemoryBackend());
  }
}

export function createAppSession(): GameSession {
  const session = createGameSession({ saves: createBrowserSaveStore() });
  session.newGame();
  return session;
}
