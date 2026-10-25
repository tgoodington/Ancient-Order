import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import { StaminaBar } from './StaminaBar';

describe('StaminaBar', () => {
  it('renders stamina percentage label', () => {
    render(<StaminaBar current={75} max={100} label="Hero" />);

    // blueprint:frontend-component:S5.5.5 — "Label: {current}/{max}"
    expect(screen.getByText('75/100')).toBeInTheDocument();
  });

  it('sets data-state to green when percentage > 60', () => {
    const { container } = render(<StaminaBar current={61} max={100} label="Hero" />);

    const fill = container.querySelector('[data-state]');

    // blueprint:frontend-component:S5.5.5 — "percentage > 60 -> 'green'"
    expect(fill).toHaveAttribute('data-state', 'green');
  });

  it('sets data-state to yellow when percentage 41-60', () => {
    const { container } = render(<StaminaBar current={50} max={100} label="Hero" />);

    const fill = container.querySelector('[data-state]');

    // blueprint:frontend-component:S5.5.5 — "percentage > 40 -> 'yellow'"
    expect(fill).toHaveAttribute('data-state', 'yellow');
  });

  it('sets data-state to orange when percentage 21-40', () => {
    const { container } = render(<StaminaBar current={30} max={100} label="Hero" />);

    const fill = container.querySelector('[data-state]');

    // blueprint:frontend-component:S5.5.5 — "percentage > 20 -> 'orange'"
    expect(fill).toHaveAttribute('data-state', 'orange');
  });

  it('sets data-state to red when percentage 1-20', () => {
    const { container } = render(<StaminaBar current={10} max={100} label="Hero" />);

    const fill = container.querySelector('[data-state]');

    // blueprint:frontend-component:S5.5.5 — "percentage > 0 -> 'red'"
    expect(fill).toHaveAttribute('data-state', 'red');
  });

  it('sets data-state to black at 0% (KO)', () => {
    const { container } = render(<StaminaBar current={0} max={100} label="Hero" />);

    const fill = container.querySelector('[data-state]');

    // blueprint:frontend-component:S5.5.5 — "percentage === 0 -> 'black'"
    expect(fill).toHaveAttribute('data-state', 'black');
  });

  it('handles max=0 gracefully (percentage=0)', () => {
    const { container } = render(<StaminaBar current={0} max={0} label="Hero" />);

    const fill = container.querySelector('[data-state]');

    // blueprint:frontend-component:S5.5.5 — "max > 0 ? Math.round((current / max) * 100) : 0"
    expect(fill).toHaveAttribute('data-state', 'black');
  });

  it('sets correct ARIA attributes', () => {
    render(<StaminaBar current={80} max={100} label="Hero" />);

    const progressbar = screen.getByRole('progressbar');

    // blueprint:frontend-component:S5.5.5 — "role='progressbar' with aria-valuenow, aria-valuemin, aria-valuemax"
    expect(progressbar).toHaveAttribute('aria-valuenow', '80');
    expect(progressbar).toHaveAttribute('aria-valuemin', '0');
    expect(progressbar).toHaveAttribute('aria-valuemax', '100');

    // blueprint:frontend-component:S5.5.5 — "aria-label includes combatant name and fraction"
    expect(progressbar).toHaveAttribute('aria-label', 'Hero stamina: 80 of 100');
  });
});
