import { afterEach, describe, expect, it, vi } from 'vitest';
import { cleanup, render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import LoadModelModal from './LoadModelModal';

const endpoint = {
  Id: 'completion-1',
  ApiFormat: 'Ollama',
  Model: 'gemma3:4b',
  Endpoint: 'http://localhost:11434',
  MaximumTimeoutMs: 45000
};

function renderModal(overrides = {}) {
  const props = {
    isOpen: true,
    endpoint,
    endpointType: 'Completion',
    onClose: vi.fn(),
    onLoad: vi.fn().mockResolvedValue({
      Success: true,
      StatusCode: 200,
      Outcome: 'Loaded',
      ResponseTimeMs: 42.5,
      CompletionCalls: [{}],
      Message: 'Model loaded.'
    }),
    onComplete: vi.fn(),
    onLoadingChange: vi.fn(),
    ...overrides
  };

  render(<LoadModelModal {...props} />);
  return props;
}

describe('LoadModelModal', () => {
  afterEach(() => {
    cleanup();
    vi.restoreAllMocks();
  });

  it('submits the configured load request and renders a success result', async () => {
    const user = userEvent.setup();
    const props = renderModal();

    await user.selectOptions(screen.getByLabelText('Strategy'), 'WarmRequest');
    await user.clear(screen.getByLabelText('Keep alive'));
    await user.type(screen.getByLabelText('Keep alive'), '45m');
    await user.clear(screen.getByLabelText('Sample input'));
    await user.type(screen.getByLabelText('Sample input'), 'warm this model');
    await user.click(screen.getByLabelText('Require native load'));

    await user.click(screen.getByRole('button', { name: 'Load' }));

    await waitFor(() => expect(props.onLoad).toHaveBeenCalledTimes(1));
    expect(props.onLoad).toHaveBeenCalledWith('completion-1', {
      Strategy: 'WarmRequest',
      KeepAlive: '45m',
      TimeoutMs: 45000,
      SampleInput: 'warm this model',
      MaxTokens: 1,
      RecordRequestHistory: true,
      RequireNativeLoad: true
    });
    await screen.findByText('Success');
    expect(screen.getByText('Loaded')).toBeTruthy();
    expect(props.onComplete).toHaveBeenCalledTimes(1);
  });

  it('renders provider-safe failure messages and keeps actions available', async () => {
    const user = userEvent.setup();
    renderModal({
      onLoad: vi.fn().mockRejectedValue({
        statusCode: 502,
        response: {
          Success: false,
          StatusCode: 502,
          Outcome: 'Failed',
          Message: 'Provider rejected warm request.'
        }
      })
    });

    await user.click(screen.getByRole('button', { name: 'Load' }));

    expect((await screen.findAllByText('Failed')).length).toBeGreaterThanOrEqual(1);
    expect(screen.getByText('Provider rejected warm request.')).toBeTruthy();
    expect(screen.getByRole('button', { name: 'Close' })).toBeTruthy();
    expect(screen.getByRole('button', { name: 'Load' })).toBeTruthy();
  });
});
