import { describe, it, expect } from 'vitest';
import { render, screen, act } from '@testing-library/react';
import { EquinoxHUD } from './EquinoxHUD';
import { GameStateProvider } from '../context/GameStateContext';
import { createGameSession } from '../../session/index';
import type { GameSession } from '../../session/index';
import { TEST_SCENE_GRAPH } from '../../narrative/fixtures';

function startedSession() {
  const session = createGameSession();
  session.newGame();
  return session;
}

function renderHud(session: GameSession) {
  return render(
    <GameStateProvider session={session}>
      <EquinoxHUD />
    </GameStateProvider>
  );
}

describe('EquinoxHUD', () => {
  it('shows loading screen when the session has no game', () => {
    renderHud(createGameSession());

    expect(screen.getByRole('status')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /confirm team/i })).not.toBeInTheDocument();
  });

  it('renders player name and personality when a game is running', () => {
    const session = startedSession();
    renderHud(session);

    const name = session.getSnapshot().game!.player.name;
    expect(screen.getByRole('heading', { name })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /personality/i })).toBeInTheDocument();
  });

  it('enables the team selector when there is no fight or scene', () => {
    renderHud(startedSession());

    expect(screen.getByRole('heading', { name: /team/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /confirm team/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /elena/i })).toBeEnabled();
    expect(screen.getByRole('button', { name: /lars/i })).toBeEnabled();
    expect(screen.getByRole('button', { name: /kade/i })).toBeEnabled();
  });

  it('hides the combat section outside combat', () => {
    renderHud(startedSession());

    expect(screen.queryByRole('progressbar')).not.toBeInTheDocument();
  });

  it('shows combat readouts and disables the team selector after an encounter starts', () => {
    const session = startedSession();
    renderHud(session);

    act(() => {
      expect(session.startEncounter('encounter_demo').ok).toBe(true);
    });

    expect(screen.getByRole('progressbar')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /elena/i })).toBeDisabled();
    expect(screen.getByRole('button', { name: /lars/i })).toBeDisabled();
  });

  it('disables the team selector during a narrative scene', () => {
    const session = startedSession();
    renderHud(session);

    act(() => {
      expect(session.startScene(TEST_SCENE_GRAPH[0].id).ok).toBe(true);
    });

    expect(screen.getByRole('button', { name: /elena/i })).toBeDisabled();
    expect(screen.queryByRole('progressbar')).not.toBeInTheDocument();
  });

  it('shows the new team as selected after the session changes it', () => {
    const session = startedSession();
    renderHud(session);

    act(() => {
      session.setTeam(['npc_scout_elena', 'npc_outlaw_kade']);
    });

    expect(screen.getByRole('button', { name: /elena/i })).toHaveAttribute('aria-pressed', 'true');
    expect(screen.getByRole('button', { name: /kade/i })).toHaveAttribute('aria-pressed', 'true');
    expect(screen.getByRole('button', { name: /lars/i })).toHaveAttribute('aria-pressed', 'false');
  });
});
