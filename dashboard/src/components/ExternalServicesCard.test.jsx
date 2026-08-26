import { afterEach, describe, expect, it, vi } from 'vitest';
import { cleanup, render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

// Mock the clipboard utility so the copy path is deterministic in jsdom.
const copyMock = vi.fn(() => Promise.resolve(true));
vi.mock('../utils/clipboard', () => ({
  copyToClipboard: (text) => copyMock(text),
}));

import ExternalServicesCard from './ExternalServicesCard';

describe('ExternalServicesCard', () => {
  afterEach(() => {
    cleanup();
    vi.clearAllMocks();
  });

  it('renders every bundled observability tool with host-based URLs', () => {
    render(<ExternalServicesCard />);

    // jsdom default hostname is "localhost"; link text is "<name> <host>:<port>".
    expect(screen.getByRole('link', { name: /^Grafana/ }).getAttribute('href')).toBe('http://localhost:3000');
    expect(screen.getByRole('link', { name: /^Prometheus/ }).getAttribute('href')).toBe('http://localhost:9090');
    expect(screen.getByRole('link', { name: /^Tempo/ }).getAttribute('href')).toBe('http://localhost:3200');
    expect(screen.getByRole('link', { name: /^Loki/ }).getAttribute('href')).toBe('http://localhost:3100');
    expect(screen.getByRole('link', { name: /^Ollama/ }).getAttribute('href')).toBe('http://localhost:11434');
  });

  it('shows Grafana default credentials with a copy button and no-auth services without one', () => {
    render(<ExternalServicesCard />);

    expect(screen.getByText('admin / admin')).toBeTruthy();
    // Five services, but only Grafana carries copyable credentials.
    expect(screen.getAllByRole('button', { name: 'Copy' })).toHaveLength(1);
    // Prometheus/Tempo/Loki/Ollama render a plain "no auth" token, not a copy button.
    expect(screen.getAllByText('no auth')).toHaveLength(4);
  });

  it('copies the credentials and shows a confirmation on click', async () => {
    const user = userEvent.setup();
    render(<ExternalServicesCard />);

    await user.click(screen.getByRole('button', { name: 'Copy' }));

    expect(copyMock).toHaveBeenCalledWith('admin / admin');
    expect(await screen.findByText(/Copied/)).toBeTruthy();
  });
});
