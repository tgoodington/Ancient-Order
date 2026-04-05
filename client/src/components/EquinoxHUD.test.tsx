import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import { EquinoxHUD } from './EquinoxHUD';

const mockUseGameState = vi.fn();

vi.mock('../context/GameStateContext', () => ({
  useGameState: (...args: unknown[]) => mockUseGameState(...args),
}));

const mockPersonality = {
  patience: 20,
  empathy: 15,
  cunning: 25,
  logic: 15,
  kindness: 10,
  charisma: 15,
};

const mockGameState = {
  player: {
    id: 'player_1',
    name: 'TestHero',
    personality: mockPersonality,
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
  team: [] as readonly string[],
  combatState: null,
  narrativeState: null,
  timestamp: Date.now(),
};

const mockCombatState = {
  round: 1,
  phase: 'PC_DECLARATION' as const,
  playerParty: [
    {
      id: 'player_abc',
      name: 'TestHero',
      archetype: 'Player',
      rank: 1,
      stamina: 80,
      maxStamina: 100,
      power: 10,
      speed: 10,
      energy: 4,
      maxEnergy: 6,
      ascensionLevel: 0 as const,
      elementalPath: 'Fire' as const,
      isKO: false,
    },
  ],
  enemyParty: [],
  status: 'active' as const,
};

const mockNpcs = [
  {
    id: 'npc_scout_elena',
    archetype: 'Scout',
    personality: { patience: 10, empathy: 20, cunning: 25, logic: 15, kindness: 15, charisma: 15 },
    affection: 50,
    trust: 50,
  },
];

const baseContextValue = {
  gameState: mockGameState,
  npcs: mockNpcs,
  loading: false,
  error: null,
  setTeam: vi.fn(),
  refreshState: vi.fn(),
  dismissError: vi.fn(),
};

beforeEach(() => {
  mockUseGameState.mockReturnValue(baseContextValue);
});

describe('EquinoxHUD', () => {
  it('shows loading screen when loading', () => {
    mockUseGameState.mockReturnValue({
      ...baseContextValue,
      gameState: null,
      loading: true,
    });

    render(<EquinoxHUD />);

    // blueprint:frontend-component:S5.5.9 "{loading && <LoadingScreen />}"
    // LoadingScreen renders role="status" per blueprint 5.5.4
    expect(screen.getByRole('status')).toBeInTheDocument();
  });

  it('shows error banner when error present', () => {
    mockUseGameState.mockReturnValue({
      ...baseContextValue,
      error: 'Something went wrong',
    });

    render(<EquinoxHUD />);

    // blueprint:frontend-component:S5.5.9 "{error && <ErrorBanner message={error} onDismiss={dismissError} />}"
    expect(screen.getByText('Something went wrong')).toBeInTheDocument();
  });

  it('renders player name and personality when loaded', () => {
    render(<EquinoxHUD />);

    // blueprint:frontend-component:S5.5.9 "{gameState.player.name}" + PersonalityBreakdown renders
    expect(screen.getByRole('heading', { name: 'TestHero' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /personality/i })).toBeInTheDocument();
  });

  it('shows combat section when combatState present', () => {
    mockUseGameState.mockReturnValue({
      ...baseContextValue,
      gameState: { ...mockGameState, combatState: mockCombatState },
    });

    render(<EquinoxHUD />);

    // blueprint:frontend-component:S5.5.9 "{playerCombatant && <section>...StaminaBar...EnergyDisplay}</section>}"
    // StaminaBar renders role="progressbar" per blueprint 5.5.5
    expect(screen.getByRole('progressbar')).toBeInTheDocument();
  });

  it('hides combat section when no combatState', () => {
    mockUseGameState.mockReturnValue({
      ...baseContextValue,
      gameState: { ...mockGameState, combatState: null },
    });

    render(<EquinoxHUD />);

    // blueprint:frontend-component:S5.5.9 "Combat section -- conditional (D1)"
    // combatState is null outside combat so progressbar must not appear
    expect(screen.queryByRole('progressbar')).not.toBeInTheDocument();
  });

  it('disables team selector during combat', () => {
    mockUseGameState.mockReturnValue({
      ...baseContextValue,
      gameState: { ...mockGameState, combatState: mockCombatState },
      npcs: mockNpcs,
    });

    render(<EquinoxHUD />);

    // blueprint:frontend-component:S5.5.9 "disabled={isInCombat || isInNarrative}"
    expect(screen.getByRole('button', { name: /elena/i })).toBeDisabled();
  });

  it('renders team selector section', () => {
    render(<EquinoxHUD />);

    // blueprint:frontend-component:S5.5.9 "TeamSelector always visible"
    expect(screen.getByRole('heading', { name: /team/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /confirm team/i })).toBeInTheDocument();
  });
});
