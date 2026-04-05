import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { PersonalityBreakdown } from './PersonalityBreakdown';

const mockPersonality = {
  patience: 20,
  empathy: 15,
  cunning: 25,
  logic: 15,
  kindness: 10,
  charisma: 15,
};

describe('PersonalityBreakdown', () => {
  it('renders collapsed by default', () => {
    render(<PersonalityBreakdown personality={mockPersonality} />);

    // blueprint:frontend-component:S5.5.7 "expanded: boolean initial: false"
    const button = screen.getByRole('button', { name: /personality/i });
    expect(button).toBeInTheDocument();
    expect(button).toHaveAttribute('aria-expanded', 'false');

    // Trait names must not be visible when collapsed
    expect(screen.queryByText('Patience')).not.toBeInTheDocument();
    expect(screen.queryByText('Empathy')).not.toBeInTheDocument();
    expect(screen.queryByText('Cunning')).not.toBeInTheDocument();
    expect(screen.queryByText('Logic')).not.toBeInTheDocument();
    expect(screen.queryByText('Kindness')).not.toBeInTheDocument();
    expect(screen.queryByText('Charisma')).not.toBeInTheDocument();
  });

  it('expands on click showing all 6 traits', async () => {
    const user = userEvent.setup();
    render(<PersonalityBreakdown personality={mockPersonality} />);

    const button = screen.getByRole('button', { name: /personality/i });
    await user.click(button);

    // blueprint:frontend-component:S5.5.7 "PERSONALITY_TRAITS.map(trait => ...)"
    expect(screen.getByText('Patience')).toBeInTheDocument();
    expect(screen.getByText('Empathy')).toBeInTheDocument();
    expect(screen.getByText('Cunning')).toBeInTheDocument();
    expect(screen.getByText('Logic')).toBeInTheDocument();
    expect(screen.getByText('Kindness')).toBeInTheDocument();
    expect(screen.getByText('Charisma')).toBeInTheDocument();

    // blueprint:frontend-component:S5.5.7 "Toggled by clicking the header button"
    expect(button).toHaveAttribute('aria-expanded', 'true');
  });

  it('collapses on second click', async () => {
    const user = userEvent.setup();
    render(<PersonalityBreakdown personality={mockPersonality} />);

    const button = screen.getByRole('button', { name: /personality/i });
    await user.click(button);
    await user.click(button);

    // blueprint:frontend-component:S5.5.7 "onClick={() => setExpanded(!expanded)}"
    expect(screen.queryByText('Patience')).not.toBeInTheDocument();
    expect(button).toHaveAttribute('aria-expanded', 'false');
  });

  it('shows percentage values for each trait', async () => {
    const user = userEvent.setup();
    render(<PersonalityBreakdown personality={mockPersonality} />);

    await user.click(screen.getByRole('button', { name: /personality/i }));

    // blueprint:frontend-component:S5.5.7 "{personality[trait]}%"
    expect(screen.getByText('20%')).toBeInTheDocument();
    expect(screen.getAllByText('15%')).toHaveLength(3); // empathy, logic, charisma
    expect(screen.getByText('25%')).toBeInTheDocument();
    expect(screen.getByText('10%')).toBeInTheDocument();
  });

  it('details region has correct ARIA attributes', async () => {
    const user = userEvent.setup();
    render(<PersonalityBreakdown personality={mockPersonality} />);

    await user.click(screen.getByRole('button', { name: /personality/i }));

    // blueprint:frontend-component:S5.5.7 'role="region" aria-label="Personality traits"'
    const region = screen.getByRole('region', { name: 'Personality traits' });
    expect(region).toBeInTheDocument();
    expect(region).toHaveAttribute('id', 'personality-details');
  });
});
