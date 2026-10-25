import { describe, it, expect, vi } from 'vitest';
import { render, screen, act } from '@testing-library/react';
import { GameStateProvider, useGameState } from './GameStateContext';
import { createGameSession } from '../../session/index';

function TestConsumer() {
  const { gameState, npcs, loading, error } = useGameState();
  return (
    <div>
      <span data-testid="loading">{String(loading)}</span>
      <span data-testid="error">{error ?? 'none'}</span>
      <span data-testid="player-name">{gameState?.player.name ?? 'null'}</span>
      <span data-testid="team">{gameState?.team.join(',') ?? 'null'}</span>
      <span data-testid="npc-count">{npcs.length}</span>
    </div>
  );
}

function TestConsumerWithActions({ team }: { team: [string, string] }) {
  const { error, setTeam, dismissError } = useGameState();
  return (
    <div>
      <span data-testid="error">{error ?? 'none'}</span>
      <button data-testid="set-team-btn" onClick={() => setTeam(team)}>
        Set Team
      </button>
      <button data-testid="dismiss-btn" onClick={() => dismissError()}>
        Dismiss
      </button>
    </div>
  );
}

function startedSession() {
  const session = createGameSession();
  session.newGame();
  return session;
}

describe('GameStateProvider', () => {
  it('shows loading state when no game has started', () => {
    render(
      <GameStateProvider session={createGameSession()}>
        <TestConsumer />
      </GameStateProvider>
    );

    expect(screen.getByTestId('loading').textContent).toBe('true');
    expect(screen.getByTestId('npc-count').textContent).toBe('0');
  });

  it('exposes the started game from the session', () => {
    const session = startedSession();
    render(
      <GameStateProvider session={session}>
        <TestConsumer />
      </GameStateProvider>
    );

    expect(screen.getByTestId('loading').textContent).toBe('false');
    expect(screen.getByTestId('player-name').textContent).toBe(
      session.getSnapshot().game!.player.name
    );
  });

  it('exposes npcs via hook', () => {
    const session = startedSession();
    render(
      <GameStateProvider session={session}>
        <TestConsumer />
      </GameStateProvider>
    );

    const expected = Object.keys(session.getSnapshot().game!.npcs).length;
    expect(expected).toBeGreaterThan(0);
    expect(screen.getByTestId('npc-count').textContent).toBe(String(expected));
  });

  it('leaves loading when the session starts a game later', () => {
    const session = createGameSession();
    render(
      <GameStateProvider session={session}>
        <TestConsumer />
      </GameStateProvider>
    );
    expect(screen.getByTestId('loading').textContent).toBe('true');

    act(() => {
      session.newGame();
    });

    expect(screen.getByTestId('loading').textContent).toBe('false');
  });

  it('setTeam updates gameState from the session', async () => {
    const session = startedSession();
    const [a, b] = Object.keys(session.getSnapshot().game!.npcs);

    render(
      <GameStateProvider session={session}>
        <TestConsumer />
        <TestConsumerWithActions team={[a, b]} />
      </GameStateProvider>
    );

    await act(async () => {
      screen.getByTestId('set-team-btn').click();
    });

    expect(screen.getByTestId('team').textContent).toBe(`${a},${b}`);
    expect(screen.getAllByTestId('error')[0].textContent).toBe('none');
  });

  it('setTeam refusal sets error state', async () => {
    const session = startedSession();

    render(
      <GameStateProvider session={session}>
        <TestConsumerWithActions team={['npc_missing_one', 'npc_missing_two']} />
      </GameStateProvider>
    );

    await act(async () => {
      screen.getByTestId('set-team-btn').click();
    });

    expect(screen.getByTestId('error').textContent).not.toBe('none');
  });

  it('dismissError clears error', async () => {
    const session = startedSession();

    render(
      <GameStateProvider session={session}>
        <TestConsumerWithActions team={['npc_missing_one', 'npc_missing_two']} />
      </GameStateProvider>
    );

    await act(async () => {
      screen.getByTestId('set-team-btn').click();
    });
    expect(screen.getByTestId('error').textContent).not.toBe('none');

    await act(async () => {
      screen.getByTestId('dismiss-btn').click();
    });

    expect(screen.getByTestId('error').textContent).toBe('none');
  });

  it('setTeam refused during combat surfaces the session message', async () => {
    const session = startedSession();
    expect(session.startEncounter('encounter_demo').ok).toBe(true);

    render(
      <GameStateProvider session={session}>
        <TestConsumer />
        <TestConsumerWithActions team={['npc_scout_elena', 'npc_merchant_lars']} />
      </GameStateProvider>
    );

    await act(async () => {
      screen.getByTestId('set-team-btn').click();
    });

    expect(screen.getAllByTestId('error')[0].textContent).toBe('Cannot change team during combat');
    expect(screen.getByTestId('team').textContent).toBe('');
  });

  it('dismissError clears a combat refusal', async () => {
    const session = startedSession();
    session.startEncounter('encounter_demo');

    render(
      <GameStateProvider session={session}>
        <TestConsumerWithActions team={['npc_scout_elena', 'npc_merchant_lars']} />
      </GameStateProvider>
    );

    await act(async () => {
      screen.getByTestId('set-team-btn').click();
    });
    expect(screen.getByTestId('error').textContent).toBe('Cannot change team during combat');

    await act(async () => {
      screen.getByTestId('dismiss-btn').click();
    });
    expect(screen.getByTestId('error').textContent).toBe('none');
  });

  it('reflects session changes made outside the provider', () => {
    const session = startedSession();
    render(
      <GameStateProvider session={session}>
        <TestConsumer />
      </GameStateProvider>
    );

    act(() => {
      session.setTeam(['npc_scout_elena', 'npc_outlaw_kade']);
    });

    expect(screen.getByTestId('team').textContent).toBe('npc_scout_elena,npc_outlaw_kade');
  });

  it('useGameState throws outside the provider', () => {
    const spy = vi.spyOn(console, 'error').mockImplementation(() => {});
    try {
      expect(() => render(<TestConsumer />)).toThrow(
        'useGameState must be used within a GameStateProvider'
      );
    } finally {
      spy.mockRestore();
    }
  });
});
