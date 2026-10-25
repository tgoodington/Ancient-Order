import { describe, it, expect, vi, afterEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { App } from './App';
import { createAppSession } from './appSession';
import type { CreateRenderer } from '../render/renderer';

function fakeRenderer() {
  return vi.fn<CreateRenderer>(() => ({ showScene: vi.fn(), destroy: vi.fn() }));
}

afterEach(() => {
  vi.unstubAllGlobals();
});

describe('App', () => {
  it('opens a new game with the player in the HUD', () => {
    render(<App session={createAppSession()} createRenderer={fakeRenderer()} />);

    expect(screen.getByRole('heading', { name: 'Kael' })).toBeInTheDocument();
    expect(screen.queryByRole('status')).not.toBeInTheDocument();
  });

  it('updates the HUD team after picking two companions and confirming', async () => {
    const user = userEvent.setup();
    const session = createAppSession();
    render(<App session={session} createRenderer={fakeRenderer()} />);

    await user.click(screen.getByRole('button', { name: /elena/i }));
    await user.click(screen.getByRole('button', { name: /kade/i }));
    await user.click(screen.getByRole('button', { name: /confirm team/i }));

    expect(session.getSnapshot().game!.team).toEqual(['npc_scout_elena', 'npc_outlaw_kade']);
    expect(screen.getByRole('button', { name: /elena/i })).toHaveAttribute('aria-pressed', 'true');
    expect(screen.getByRole('button', { name: /kade/i })).toHaveAttribute('aria-pressed', 'true');
    expect(screen.getByRole('button', { name: /lars/i })).toHaveAttribute('aria-pressed', 'false');
    expect(screen.getByRole('button', { name: /confirm team/i })).toBeDisabled();
  });

  it('makes zero fetch calls while running', async () => {
    const fetchSpy = vi.fn();
    vi.stubGlobal('fetch', fetchSpy);
    const user = userEvent.setup();
    render(<App session={createAppSession()} createRenderer={fakeRenderer()} />);

    await user.click(screen.getByRole('button', { name: /elena/i }));
    await user.click(screen.getByRole('button', { name: /lars/i }));
    await user.click(screen.getByRole('button', { name: /confirm team/i }));

    expect(fetchSpy).not.toHaveBeenCalled();
  });

  it('renders the stage layer and the HUD layer, with the renderer mounted in the stage', () => {
    const createRenderer = fakeRenderer();
    render(<App session={createAppSession()} createRenderer={createRenderer} />);

    const stageLayer = screen.getByTestId('stage-layer');
    const hudLayer = screen.getByTestId('hud-layer');
    expect(createRenderer).toHaveBeenCalledTimes(1);
    expect(stageLayer).toContainElement(createRenderer.mock.calls[0][0]);
    expect(stageLayer).toHaveAttribute('aria-hidden', 'true');
    expect(hudLayer).toContainElement(screen.getByRole('heading', { name: 'Kael' }));
    expect(stageLayer).not.toContainElement(hudLayer);
  });

  it('keeps HUD controls responsive to keyboard', async () => {
    const user = userEvent.setup();
    render(<App session={createAppSession()} createRenderer={fakeRenderer()} />);

    const elena = screen.getByRole('button', { name: /elena/i });
    elena.focus();
    await user.keyboard('{Enter}');

    expect(elena).toHaveAttribute('aria-pressed', 'true');
  });
});
