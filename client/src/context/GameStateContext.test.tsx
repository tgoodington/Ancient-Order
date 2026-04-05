import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor, act } from '@testing-library/react';
import React from 'react';
import { GameStateProvider, useGameState } from './GameStateContext';

const { mockApiGet, mockApiPost } = vi.hoisted(() => ({
  mockApiGet: vi.fn(),
  mockApiPost: vi.fn(),
}));

vi.mock('../api/apiClient', () => {
  class ApiClientError extends Error {
    code: string;
    constructor(code: string, message: string) {
      super(message);
      this.name = 'ApiClientError';
      this.code = code;
    }
  }
  return {
    ApiClientError,
    apiGet: mockApiGet,
    apiPost: mockApiPost,
  };
});

import { ApiClientError as MockApiClientError } from '../api/apiClient';

const MOCK_GAME_STATE = {
  player: {
    id: 'player_1',
    name: 'TestPlayer',
    personality: { patience: 20, empathy: 15, cunning: 15, logic: 20, kindness: 15, charisma: 15 },
  },
  npcs: {
    'npc_scout_elena': {
      id: 'npc_scout_elena',
      archetype: 'Scout',
      personality: { patience: 10, empathy: 20, cunning: 25, logic: 15, kindness: 15, charisma: 15 },
      affection: 50,
      trust: 50,
    },
  },
  team: [],
  combatState: null,
  narrativeState: null,
  timestamp: Date.now(),
};

function TestConsumer() {
  const { gameState, npcs, loading, error } = useGameState();
  return (
    <div>
      <span data-testid="loading">{String(loading)}</span>
      <span data-testid="error">{error ?? 'none'}</span>
      <span data-testid="player-name">{gameState?.player.name ?? 'null'}</span>
      <span data-testid="npc-count">{npcs.length}</span>
    </div>
  );
}

function TestConsumerWithActions() {
  const { gameState, npcs, loading, error, setTeam, dismissError } = useGameState();
  return (
    <div>
      <span data-testid="loading">{String(loading)}</span>
      <span data-testid="error">{error ?? 'none'}</span>
      <span data-testid="player-name">{gameState?.player.name ?? 'null'}</span>
      <span data-testid="npc-count">{npcs.length}</span>
      <button
        data-testid="set-team-btn"
        onClick={() => setTeam(['npc_scout_elena', 'npc_merchant_lars'])}
      >
        Set Team
      </button>
      <button data-testid="dismiss-btn" onClick={() => dismissError()}>
        Dismiss
      </button>
    </div>
  );
}

function TestConsumerWithRefresh() {
  const { gameState, refreshState } = useGameState();
  return (
    <div>
      <span data-testid="player-name">{gameState?.player.name ?? 'null'}</span>
      <button data-testid="refresh-btn" onClick={() => refreshState()}>
        Refresh
      </button>
    </div>
  );
}

beforeEach(() => {
  vi.resetAllMocks();
});

describe('GameStateProvider', () => {
  it('shows loading state initially', async () => {
    // Keep apiGet pending so loading stays true
    mockApiGet.mockReturnValue(new Promise(() => {}));

    render(
      <GameStateProvider>
        <TestConsumer />
      </GameStateProvider>
    );

    // blueprint:frontend-component:S5.5.2 — "loading: true during fetch/create"
    expect(screen.getByTestId('loading').textContent).toBe('true');
  });

  it('fetches game state on mount', async () => {
    mockApiGet.mockResolvedValue(MOCK_GAME_STATE);

    render(
      <GameStateProvider>
        <TestConsumer />
      </GameStateProvider>
    );

    await waitFor(() => {
      // blueprint:frontend-component:S5.5.2 — "Call apiGet('/api/game/state')" + "On success: set gameState"
      expect(screen.getByTestId('loading').textContent).toBe('false');
    });

    // blueprint:frontend-component:S5.5.2 — "On success: set gameState"
    expect(screen.getByTestId('player-name').textContent).toBe('TestPlayer');

    // blueprint:frontend-component:S5.5.2 — "Call apiGet('/api/game/state')"
    expect(mockApiGet).toHaveBeenCalledWith('/api/game/state');
  });

  it('auto-creates game on 404 then re-fetches', async () => {
    const notFoundError = new MockApiClientError('GAME_NOT_FOUND', 'No game found');

    mockApiGet
      .mockRejectedValueOnce(notFoundError)
      .mockResolvedValueOnce(MOCK_GAME_STATE);

    mockApiPost.mockResolvedValue(MOCK_GAME_STATE);

    render(
      <GameStateProvider>
        <TestConsumer />
      </GameStateProvider>
    );

    await waitFor(() => {
      // blueprint:frontend-component:S5.5.2 — "On error with code GAME_NOT_FOUND: call apiPost('/api/game/new', {}), then apiGet again"
      expect(screen.getByTestId('player-name').textContent).toBe('TestPlayer');
    });

    // blueprint:frontend-component:S5.5.2 — "call apiPost('/api/game/new', {})"
    expect(mockApiPost).toHaveBeenCalledWith('/api/game/new', {});

    // blueprint:frontend-component:S5.5.2 — "then apiGet again"
    expect(mockApiGet).toHaveBeenCalledTimes(2);
    expect(mockApiGet).toHaveBeenNthCalledWith(2, '/api/game/state');
  });

  it('exposes error on fetch failure', async () => {
    const serverError = new MockApiClientError('SERVER_ERROR', 'Internal error');

    mockApiGet.mockRejectedValue(serverError);

    render(
      <GameStateProvider>
        <TestConsumer />
      </GameStateProvider>
    );

    await waitFor(() => {
      // blueprint:frontend-component:S5.5.2 — "On any other error: set error message"
      expect(screen.getByTestId('error').textContent).not.toBe('none');
    });

    // blueprint:frontend-component:S5.5.2 — "On any other error: set error message"
    expect(screen.getByTestId('loading').textContent).toBe('false');
  });

  it('sets error on auto-create failure', async () => {
    const notFoundError = new MockApiClientError('GAME_NOT_FOUND', 'No game found');
    const createError = new MockApiClientError('SERVER_ERROR', 'Create failed');

    mockApiGet.mockRejectedValue(notFoundError);
    mockApiPost.mockRejectedValue(createError);

    render(
      <GameStateProvider>
        <TestConsumer />
      </GameStateProvider>
    );

    await waitFor(() => {
      // blueprint:frontend-component:S5.5.2 — "On auto-create failure: set error message"
      expect(screen.getByTestId('error').textContent).not.toBe('none');
    });

    // blueprint:frontend-component:S5.5.2 — "On auto-create failure: set error message"
    expect(screen.getByTestId('loading').textContent).toBe('false');
  });

  it('exposes gameState and npcs via hook', async () => {
    mockApiGet.mockResolvedValue(MOCK_GAME_STATE);

    render(
      <GameStateProvider>
        <TestConsumer />
      </GameStateProvider>
    );

    await waitFor(() => {
      // blueprint:frontend-component:S5.5.2 — "npcs: NPC[] — all available NPCs from gameState.npcs"
      expect(Number(screen.getByTestId('npc-count').textContent)).toBeGreaterThan(0);
    });

    // blueprint:frontend-component:S5.5.2 — "npcs: NPC[] — all available NPCs from gameState.npcs"
    expect(screen.getByTestId('npc-count').textContent).toBe('1');
  });

  it('setTeam calls API and refreshes state', async () => {
    const updatedState = {
      ...MOCK_GAME_STATE,
      team: ['npc_scout_elena', 'npc_merchant_lars'],
      player: { ...MOCK_GAME_STATE.player, name: 'UpdatedPlayer' },
    };

    mockApiGet
      .mockResolvedValueOnce(MOCK_GAME_STATE)
      .mockResolvedValueOnce(updatedState);

    mockApiPost.mockResolvedValue({ team: ['npc_scout_elena', 'npc_merchant_lars'] });

    render(
      <GameStateProvider>
        <TestConsumerWithActions />
      </GameStateProvider>
    );

    await waitFor(() => {
      expect(screen.getByTestId('loading').textContent).toBe('false');
    });

    await act(async () => {
      screen.getByTestId('set-team-btn').click();
    });

    await waitFor(() => {
      // blueprint:frontend-component:S5.5.2 — "Call apiPost('/api/player/team', { npcIds })" + "On success: call refreshState()"
      expect(screen.getByTestId('player-name').textContent).toBe('UpdatedPlayer');
    });

    // blueprint:frontend-component:S5.5.2 — "Call apiPost('/api/player/team', { npcIds })"
    expect(mockApiPost).toHaveBeenCalledWith('/api/player/team', {
      npcIds: ['npc_scout_elena', 'npc_merchant_lars'],
    });

    // blueprint:frontend-component:S5.5.2 — "On success: call refreshState()"
    expect(mockApiGet).toHaveBeenCalledTimes(2);
  });

  it('setTeam error sets error state', async () => {
    mockApiGet.mockResolvedValue(MOCK_GAME_STATE);

    const teamError = new MockApiClientError('TEAM_LOCKED', 'Team locked during combat');
    mockApiPost.mockRejectedValue(teamError);

    render(
      <GameStateProvider>
        <TestConsumerWithActions />
      </GameStateProvider>
    );

    await waitFor(() => {
      expect(screen.getByTestId('loading').textContent).toBe('false');
    });

    await act(async () => {
      screen.getByTestId('set-team-btn').click();
    });

    await waitFor(() => {
      // blueprint:frontend-component:S5.5.2 — "On error: set error with message from API"
      expect(screen.getByTestId('error').textContent).not.toBe('none');
    });
  });

  it('dismissError clears error', async () => {
    const serverError = new MockApiClientError('SERVER_ERROR', 'Internal error');

    mockApiGet.mockRejectedValue(serverError);

    render(
      <GameStateProvider>
        <TestConsumerWithActions />
      </GameStateProvider>
    );

    await waitFor(() => {
      expect(screen.getByTestId('error').textContent).not.toBe('none');
    });

    await act(async () => {
      screen.getByTestId('dismiss-btn').click();
    });

    // blueprint:frontend-component:S5.5.2 — "Set error to null"
    expect(screen.getByTestId('error').textContent).toBe('none');
  });

  it('refreshState updates gameState', async () => {
    const refreshedState = {
      ...MOCK_GAME_STATE,
      player: { ...MOCK_GAME_STATE.player, name: 'RefreshedPlayer' },
    };

    mockApiGet
      .mockResolvedValueOnce(MOCK_GAME_STATE)
      .mockResolvedValueOnce(refreshedState);

    render(
      <GameStateProvider>
        <TestConsumerWithRefresh />
      </GameStateProvider>
    );

    await waitFor(() => {
      expect(screen.getByTestId('player-name').textContent).toBe('TestPlayer');
    });

    await act(async () => {
      screen.getByTestId('refresh-btn').click();
    });

    await waitFor(() => {
      // blueprint:frontend-component:S5.5.2 — "Call apiGet('/api/game/state')" + "On success: set gameState"
      expect(screen.getByTestId('player-name').textContent).toBe('RefreshedPlayer');
    });

    // blueprint:frontend-component:S5.5.2 — "Call apiGet('/api/game/state')"
    expect(mockApiGet).toHaveBeenCalledTimes(2);
    expect(mockApiGet).toHaveBeenNthCalledWith(2, '/api/game/state');
  });
});
