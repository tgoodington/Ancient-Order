import React, { createContext, useCallback, useContext, useMemo, useState, useSyncExternalStore } from 'react';
import type { GameSession } from '../../session/index';
import type { GameState, NPC } from '../../types/index';

interface GameStateProviderProps {
  session: GameSession;
  children: React.ReactNode;
}

interface GameStateContextValue {
  gameState: GameState | null;
  npcs: NPC[];
  loading: boolean;
  error: string | null;
  setTeam: (npcIds: [string, string]) => Promise<void>;
  dismissError: () => void;
}

const GameStateContext = createContext<GameStateContextValue | null>(null);

export function GameStateProvider({ session, children }: GameStateProviderProps) {
  const snapshot = useSyncExternalStore(session.subscribe, session.getSnapshot);
  const [error, setError] = useState<string | null>(null);

  const gameState = snapshot.game;
  const loading = gameState === null;

  const dismissError = useCallback(() => {
    setError(null);
  }, []);

  const setTeam = useCallback(async (npcIds: [string, string]) => {
    const result = session.setTeam(npcIds);
    if (!result.ok) {
      setError(result.error.message);
    }
  }, [session]);

  const npcs = useMemo<NPC[]>(() => {
    if (!gameState) return [];
    return Object.values(gameState.npcs);
  }, [gameState]);

  const contextValue = useMemo<GameStateContextValue>(
    () => ({ gameState, npcs, loading, error, setTeam, dismissError }),
    [gameState, npcs, loading, error, setTeam, dismissError]
  );

  return (
    <GameStateContext.Provider value={contextValue}>
      {children}
    </GameStateContext.Provider>
  );
}

export function useGameState(): GameStateContextValue {
  const ctx = useContext(GameStateContext);
  if (!ctx) {
    throw new Error('useGameState must be used within a GameStateProvider');
  }
  return ctx;
}
