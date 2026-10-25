import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { ErrorBanner } from './ErrorBanner';

describe('ErrorBanner', () => {
  it('renders error message', () => {
    render(<ErrorBanner message="Network error" onDismiss={vi.fn()} />);

    // blueprint:frontend-component:S5.5.3 "containing the message text"
    expect(screen.getByText('Network error')).toBeInTheDocument();
  });

  it('calls onDismiss when dismiss button clicked', async () => {
    const user = userEvent.setup();
    const onDismiss = vi.fn();
    render(<ErrorBanner message="Network error" onDismiss={onDismiss} />);

    // blueprint:frontend-component:S5.5.3 "callback when user dismisses"
    await user.click(screen.getByRole('button', { name: 'Dismiss error' }));

    expect(onDismiss).toHaveBeenCalledOnce();
  });

  it('has role alert for accessibility', () => {
    render(<ErrorBanner message="Something failed" onDismiss={vi.fn()} />);

    // blueprint:frontend-component:S5.5.3 "div role='alert'"
    expect(screen.getByRole('alert')).toBeInTheDocument();
  });
});
