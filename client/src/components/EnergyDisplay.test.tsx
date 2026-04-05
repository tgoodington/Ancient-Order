import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import { EnergyDisplay } from './EnergyDisplay';

describe('EnergyDisplay', () => {
  it('renders correct number of segments', () => {
    const { container } = render(<EnergyDisplay current={3} max={6} label="Hero" />);

    const segments = container.querySelectorAll('[data-filled]');

    // blueprint:frontend-component:S5.5.6 — "Array of length max"
    expect(segments).toHaveLength(6);
  });

  it('marks filled segments correctly at partial energy', () => {
    const { container } = render(<EnergyDisplay current={3} max={6} label="Hero" />);

    const segments = container.querySelectorAll('[data-filled]');
    const filled = Array.from(segments).filter(s => s.getAttribute('data-filled') === 'true');
    const empty = Array.from(segments).filter(s => s.getAttribute('data-filled') === 'false');

    // blueprint:frontend-component:S5.5.6 — "index < current ? 'filled' : 'empty'"
    expect(filled).toHaveLength(3);
    expect(empty).toHaveLength(3);
  });

  it('all segments empty at 0 energy', () => {
    const { container } = render(<EnergyDisplay current={0} max={6} label="Hero" />);

    const segments = container.querySelectorAll('[data-filled]');
    const empty = Array.from(segments).filter(s => s.getAttribute('data-filled') === 'false');

    // blueprint:frontend-component:S5.5.6 — "segments: Array of length max, each element is index < current ? 'filled' : 'empty'"
    expect(empty).toHaveLength(6);
  });

  it('all segments filled at max energy', () => {
    const { container } = render(<EnergyDisplay current={6} max={6} label="Hero" />);

    const segments = container.querySelectorAll('[data-filled]');
    const filled = Array.from(segments).filter(s => s.getAttribute('data-filled') === 'true');

    // blueprint:frontend-component:S5.5.6 — "index < current ? 'filled' : 'empty'"
    expect(filled).toHaveLength(6);
  });

  it('sets correct ARIA attributes', () => {
    render(<EnergyDisplay current={4} max={6} label="Hero" />);

    const meter = screen.getByRole('meter');

    // blueprint:frontend-component:S5.5.6 — "role='meter' with aria-valuenow, aria-valuemin, aria-valuemax"
    expect(meter).toHaveAttribute('aria-valuenow', '4');
    expect(meter).toHaveAttribute('aria-valuemin', '0');
    expect(meter).toHaveAttribute('aria-valuemax', '6');

    // blueprint:frontend-component:S5.5.6 — "aria-label includes combatant name and fraction"
    expect(meter).toHaveAttribute('aria-label', 'Hero energy: 4 of 6');
  });
});
