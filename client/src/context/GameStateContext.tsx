import React, { createContext, useCallback, useContext, useEffect, useMemo, useState } from 'react';
import { apiGet, apiPost, ApiClientError } from '../api/apiClient';
import type { GameState, NPC } from '../types/index';

interface GameStateProviderProps {
  children: React.ReactNode;
}

interface GameStateContextValue {
  gameState: GameState | null;
  npcs: NPC[];
  loading: boolean;
  error: string | null;
  setTeam: (npcIds: [string, string]) => Promise<void>;
  refreshState: () => Promise<void>;
  dismissError: () => void;
}

const GameStateContext = createContext<GameStateContextValue | null>(null);

export function GameStateProvider({ children }: GameStateProviderProps) {
  const [gameState, setGameState] = useState<GameState | null>(null);
  const [loading, setLoading] = useState<boolean>(true);
  const [error, setError] = useState<string | null>(null);

  const dismissError = useCallback(() => {
    setError(null);
  }, []);

  const refreshState = useCallback(async () => {
    try {
      const state = await apiGet<GameState>('/api/game/state');
      setGameState(state);
    } catch (err) {
      const message = err instanceof ApiClientError ? err.message : 'Failed to refresh game state';
      setError(message);
    }
  }, []);

  const setTeam = useCallback(async (npcIds: [string, string]) => {
    try {
      await apiPost<{ team: readonly string[] }>('/api/player/team', { npcIds });
      await refreshState();
    } catch (err) {
      const message = err instanceof ApiClientError ? err.message : 'Failed to set team';
      setError(message);
    }
  }, [refreshState]);

  useEffect(() => {
    async function initGame() {
      try {
        const state = await apiGet<GameState>('/api/game/state');
        setGameState(state);
        setLoading(false);
      } catch (err) {
        if (err instanceof ApiClientError && err.code === 'GAME_NOT_FOUND') {
          try {
            await apiPost<GameState>('/api/game/new', {});
            const state = await apiGet<GameState>('/api/game/state');
            setGameState(state);
            setLoading(false);
          } catch (createErr) {
            const message = createErr instanceof ApiClientError
              ? createErr.message
              : 'Failed to create game';
            setError(message);
            setLoading(false);
          }
        } else {
          const message = err instanceof ApiClientError ? err.message : 'Failed to load game state';
          setError(message);
          setLoading(false);
        }
      }
    }

    void initGame();
  }, []);

  const npcs = useMemo<NPC[]>(() => {
    if (!gameState) return [];
    return Object.values(gameState.npcs);
  }, [gameState]);

  const contextValue = useMemo<GameStateContextValue>(
    () => ({ gameState, npcs, loading, error, setTeam, refreshState, dismissError }),
    [gameState, npcs, loading, error, setTeam, refreshState, dismissError]
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
