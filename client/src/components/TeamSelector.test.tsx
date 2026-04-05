import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { TeamSelector } from './TeamSelector';

const mockNpcs = [
  {
    id: 'npc_scout_elena',
    archetype: 'Scout',
    personality: { patience: 10, empathy: 20, cunning: 25, logic: 15, kindness: 15, charisma: 15 },
    affection: 50,
    trust: 50,
  },
  {
    id: 'npc_merchant_lars',
    archetype: 'Merchant',
    personality: { patience: 25, empathy: 15, cunning: 15, logic: 20, kindness: 15, charisma: 10 },
    affection: 50,
    trust: 50,
  },
  {
    id: 'npc_outlaw_kade',
    archetype: 'Outlaw',
    personality: { patience: 15, empathy: 10, cunning: 20, logic: 15, kindness: 10, charisma: 30 },
    affection: 50,
    trust: 50,
  },
];

describe('TeamSelector', () => {
  it('renders all 3 NPCs', () => {
    render(
      <TeamSelector
        currentTeam={[]}
        npcs={mockNpcs}
        onSetTeam={vi.fn()}
        disabled={false}
      />
    );

    // blueprint:frontend-component:S5.5.8 "npcs.map(npc => ...)"
    expect(screen.getByRole('button', { name: /elena/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /lars/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /kade/i })).toBeInTheDocument();
  });

  it('allows selecting 2 NPCs', async () => {
    const user = userEvent.setup();
    render(
      <TeamSelector
        currentTeam={[]}
        npcs={mockNpcs}
        onSetTeam={vi.fn()}
        disabled={false}
      />
    );

    await user.click(screen.getByRole('button', { name: /elena/i }));
    await user.click(screen.getByRole('button', { name: /lars/i }));

    // blueprint:frontend-component:S5.5.8 "toggleNpc: size<2 and !has → add"
    expect(screen.getByRole('button', { name: /elena/i })).toHaveAttribute('aria-pressed', 'true');
    expect(screen.getByRole('button', { name: /lars/i })).toHaveAttribute('aria-pressed', 'true');
  });

  it('cannot select 3rd NPC when 2 already selected', async () => {
    const user = userEvent.setup();
    render(
      <TeamSelector
        currentTeam={[]}
        npcs={mockNpcs}
        onSetTeam={vi.fn()}
        disabled={false}
      />
    );

    await user.click(screen.getByRole('button', { name: /elena/i }));
    await user.click(screen.getByRole('button', { name: /lars/i }));

    const kadeButton = screen.getByRole('button', { name: /kade/i });

    // blueprint:frontend-component:S5.5.8 "size===2 and !has → no-op"
    // disabled when selectedIds.size >= 2 && !selectedIds.has(npc.id)
    expect(kadeButton).toBeDisabled();
    expect(kadeButton).toHaveAttribute('aria-pressed', 'false');
  });

  it('deselect then select different NPC', async () => {
    const user = userEvent.setup();
    render(
      <TeamSelector
        currentTeam={[]}
        npcs={mockNpcs}
        onSetTeam={vi.fn()}
        disabled={false}
      />
    );

    await user.click(screen.getByRole('button', { name: /elena/i }));
    await user.click(screen.getByRole('button', { name: /lars/i }));

    // blueprint:frontend-component:S5.5.8 "has id → remove"
    await user.click(screen.getByRole('button', { name: /elena/i }));
    await user.click(screen.getByRole('button', { name: /kade/i }));

    expect(screen.getByRole('button', { name: /elena/i })).toHaveAttribute('aria-pressed', 'false');
    expect(screen.getByRole('button', { name: /lars/i })).toHaveAttribute('aria-pressed', 'true');
    expect(screen.getByRole('button', { name: /kade/i })).toHaveAttribute('aria-pressed', 'true');
  });

  it('confirm button calls onSetTeam with selected IDs', async () => {
    const user = userEvent.setup();
    const onSetTeam = vi.fn().mockResolvedValue(undefined);
    render(
      <TeamSelector
        currentTeam={[]}
        npcs={mockNpcs}
        onSetTeam={onSetTeam}
        disabled={false}
      />
    );

    await user.click(screen.getByRole('button', { name: /elena/i }));
    await user.click(screen.getByRole('button', { name: /lars/i }));
    await user.click(screen.getByRole('button', { name: /confirm team/i }));

    // blueprint:frontend-component:S5.5.8 "call onSetTeam([...selectedIds] as [string, string])"
    expect(onSetTeam).toHaveBeenCalledOnce();
    const [calledWith] = onSetTeam.mock.calls[0];
    expect(calledWith).toContain('npc_scout_elena');
    expect(calledWith).toContain('npc_merchant_lars');
  });

  it('confirm button disabled when fewer than 2 selected', async () => {
    const user = userEvent.setup();
    render(
      <TeamSelector
        currentTeam={[]}
        npcs={mockNpcs}
        onSetTeam={vi.fn()}
        disabled={false}
      />
    );

    await user.click(screen.getByRole('button', { name: /elena/i }));

    // blueprint:frontend-component:S5.5.8 "canSubmit: selectedIds.size === 2"
    expect(screen.getByRole('button', { name: /confirm team/i })).toBeDisabled();
  });

  it('confirm button disabled when selection matches current team', () => {
    render(
      <TeamSelector
        currentTeam={['npc_scout_elena', 'npc_merchant_lars']}
        npcs={mockNpcs}
        onSetTeam={vi.fn()}
        disabled={false}
      />
    );

    // blueprint:frontend-component:S5.5.8 "canSubmit: !setsEqual(selectedIds, new Set(currentTeam))"
    expect(screen.getByRole('button', { name: /confirm team/i })).toBeDisabled();
  });

  it('all buttons disabled when disabled prop is true', () => {
    render(
      <TeamSelector
        currentTeam={[]}
        npcs={mockNpcs}
        onSetTeam={vi.fn()}
        disabled={true}
      />
    );

    // blueprint:frontend-component:S5.5.8 "disabled: true during combat or narrative"
    expect(screen.getByRole('button', { name: /elena/i })).toBeDisabled();
    expect(screen.getByRole('button', { name: /lars/i })).toBeDisabled();
    expect(screen.getByRole('button', { name: /kade/i })).toBeDisabled();
  });

  it('shows Setting... text during submit', async () => {
    const user = userEvent.setup();
    const onSetTeam = vi.fn(() => new Promise<void>(() => {}));
    render(
      <TeamSelector
        currentTeam={[]}
        npcs={mockNpcs}
        onSetTeam={onSetTeam}
        disabled={false}
      />
    );

    await user.click(screen.getByRole('button', { name: /elena/i }));
    await user.click(screen.getByRole('button', { name: /lars/i }));
    await user.click(screen.getByRole('button', { name: /confirm team/i }));

    // blueprint:frontend-component:S5.5.8 "submitting ? 'Setting...' : 'Confirm Team'"
    expect(screen.getByRole('button', { name: /setting\.\.\./i })).toBeInTheDocument();
  });
});
