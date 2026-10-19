import React, { useState } from 'react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { cleanup, render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import ProviderPicker from './ProviderPicker';
import ProviderConnectionFields from './ProviderConnectionFields';
import SecretInput from './SecretInput';
import { applyProviderChange, defaultAuth, defaultProviderSettings, getProviderDefaults } from '../utils/providers';

function initialForm(apiFormat, kind, overrides = {}) {
  const settings = defaultProviderSettings(apiFormat);
  return { ApiFormat: apiFormat, ApiKey: '', ProviderSettings: settings, Auth: defaultAuth(apiFormat), EndpointOverride: false, HealthCheckUrl: '', ...getProviderDefaults(apiFormat, kind, settings), ...overrides };
}

function Harness({ kind, start, onForm, editing = false, onReveal }) {
  const [form, setForm] = useState(start);
  const update = (next) => { setForm(next); onForm?.(next); };
  return (
    <>
      <ProviderPicker kind={kind} value={form.ApiFormat} onChange={v => update(applyProviderChange(form, v, kind, false))} confirmChange={editing} />
      {form.ApiFormat && <ProviderConnectionFields kind={kind} form={form} setForm={update} healthFieldsEdited={false} editing={editing} onReveal={onReveal} />}
    </>
  );
}

describe('ProviderPicker', () => {
  afterEach(() => { cleanup(); vi.restoreAllMocks(); });

  it('offers only providers that can serve the endpoint kind', () => {
    render(<ProviderPicker kind="completion" value="" onChange={() => {}} />);
    expect(screen.getByRole('radio', { name: /Anthropic/ })).toBeTruthy();
    expect(screen.queryByRole('radio', { name: /Hugging Face TEI/ })).toBeNull();
    expect(screen.queryByRole('radio', { name: /VoyageAI/ })).toBeNull();
    cleanup();

    render(<ProviderPicker kind="embedding" value="" onChange={() => {}} />);
    expect(screen.getByRole('radio', { name: /Hugging Face TEI/ })).toBeTruthy();
    expect(screen.queryByRole('radio', { name: /Anthropic/ })).toBeNull();
  });

  it('collapses to a summary after a choice and expands on Change', async () => {
    const user = userEvent.setup();
    const onChange = vi.fn();
    render(<ProviderPicker kind="embedding" value="" onChange={onChange} />);
    await user.click(screen.getByRole('radio', { name: /VoyageAI/ }));
    expect(onChange).toHaveBeenCalledWith('VoyageAI');
    cleanup();

    render(<ProviderPicker kind="embedding" value="VoyageAI" onChange={onChange} />);
    expect(screen.getByTestId('provider-summary').textContent).toContain('VoyageAI');
    await user.click(screen.getByRole('button', { name: 'Change' }));
    expect(screen.getByRole('radio', { name: /^Cohere/ })).toBeTruthy();
  });

  it('asks before switching providers on an existing endpoint', async () => {
    const user = userEvent.setup();
    const onChange = vi.fn();
    render(<ProviderPicker kind="completion" value="OpenAI" onChange={onChange} confirmChange />);
    await user.click(screen.getByRole('button', { name: 'Change' }));
    await user.click(screen.getByRole('radio', { name: /AWS Bedrock/ }));
    expect(onChange).not.toHaveBeenCalled();
    expect(screen.getByRole('alert').textContent).toContain('Switch to AWS Bedrock?');
    await user.click(screen.getByRole('button', { name: 'Switch' }));
    expect(onChange).toHaveBeenCalledWith('Bedrock');
  });
});

describe('ProviderConnectionFields', () => {
  afterEach(() => { cleanup(); vi.restoreAllMocks(); });

  it('shows AWS credentials instead of an API key for Bedrock', async () => {
    const user = userEvent.setup();
    let latest = null;
    render(<Harness kind="completion" start={initialForm('Bedrock', 'completion')} onForm={f => { latest = f; }} />);

    expect(screen.getByLabelText(/Region/)).toBeTruthy();
    expect(screen.getByLabelText(/Access Key ID/)).toBeTruthy();
    expect(screen.getByLabelText('Secret Access Key')).toBeTruthy();
    expect(screen.queryByLabelText('API Key')).toBeNull();

    await user.click(screen.getByRole('radio', { name: 'Server environment' }));
    expect(screen.queryByLabelText(/Access Key ID/)).toBeNull();
    expect(latest.ProviderSettings.CredentialSource).toBe('Environment');
  });

  it('switches Vertex AI credential inputs by source', async () => {
    const user = userEvent.setup();
    render(<Harness kind="embedding" start={initialForm('VertexAI', 'embedding')} />);
    expect(screen.getByLabelText('Service Account Key JSON')).toBeTruthy();
    expect(screen.queryByLabelText('Access Token')).toBeNull();

    await user.click(screen.getByRole('radio', { name: 'Access token' }));
    expect(screen.getByLabelText('Access Token')).toBeTruthy();
    expect(screen.queryByLabelText('Service Account Key JSON')).toBeNull();

    await user.click(screen.getByRole('radio', { name: 'Application Default' }));
    expect(screen.getByText(/GOOGLE_APPLICATION_CREDENTIALS/)).toBeTruthy();
  });

  it('labels the Azure model as a deployment and the key by auth mode', async () => {
    const user = userEvent.setup();
    render(<Harness kind="completion" start={initialForm('AzureOpenAI', 'completion')} />);
    expect(screen.getByLabelText(/Deployment Name/)).toBeTruthy();
    expect(screen.getByLabelText(/Resource URL/)).toBeTruthy();
    expect(screen.getByLabelText('API Key')).toBeTruthy();
    await user.click(screen.getByRole('radio', { name: 'Entra ID token' }));
    expect(screen.getByLabelText('Entra ID Access Token')).toBeTruthy();
  });

  it('shows the fixed Ollama Cloud host as read-only text', () => {
    render(<Harness kind="embedding" start={initialForm('OllamaCloud', 'embedding')} />);
    expect(screen.getByText('https://ollama.com', { selector: '.provider-readonly' })).toBeTruthy();
    expect(screen.queryByRole('textbox', { name: /Endpoint URL/ })).toBeNull();
  });

  it('reveals stored secrets on request when editing', async () => {
    const user = userEvent.setup();
    const onReveal = vi.fn();
    render(<Harness kind="completion" editing onReveal={onReveal} start={initialForm('Anthropic', 'completion', { ApiKey: '****abcd' })} />);
    expect(screen.getByText(/Stored \(\*\*\*\*abcd\)/)).toBeTruthy();
    await user.click(screen.getByRole('button', { name: 'Reveal' }));
    expect(onReveal).toHaveBeenCalled();
  });

  it('offers an Authentication selector for Ollama and shows mode-specific fields', async () => {
    const user = userEvent.setup();
    let latest = null;
    render(<Harness kind="completion" start={initialForm('Ollama', 'completion', { Auth: { Mode: 'None', HeaderName: '', QueryParameterName: '', Username: '' } })} onForm={f => { latest = f; }} />);
    expect(screen.getByText(/No credential is sent/)).toBeTruthy();
    expect(screen.queryByLabelText(/Bearer Token/)).toBeNull();

    await user.click(screen.getByRole('radio', { name: 'Custom header' }));
    expect(screen.getByLabelText(/Header Name/)).toBeTruthy();
    expect(screen.getByLabelText('Header Value')).toBeTruthy();
    await user.type(screen.getByLabelText(/Header Name/), 'X-Gateway-Key');
    expect(latest.Auth).toMatchObject({ Mode: 'Header', HeaderName: 'X-Gateway-Key' });
    expect(screen.getByText(/Sent as X-Gateway-Key: <value>/)).toBeTruthy();

    await user.click(screen.getByRole('radio', { name: 'Basic auth' }));
    expect(screen.getByLabelText(/Username/)).toBeTruthy();
    expect(screen.getByLabelText('Password')).toBeTruthy();
  });

  it('states the fixed scheme for providers with one documented authentication', () => {
    render(<Harness kind="completion" start={initialForm('Anthropic', 'completion')} />);
    expect(screen.getByText(/x-api-key header/)).toBeTruthy();
    expect(screen.queryByRole('radio', { name: 'Custom header' })).toBeNull();
  });

  it('warns that signed providers cannot use the proxy', () => {
    render(<Harness kind="completion" start={initialForm('VertexAI', 'completion')} />);
    expect(screen.getByText(/not with the transparent proxy or Chat/)).toBeTruthy();
  });
});

describe('SecretInput', () => {
  afterEach(() => { cleanup(); });

  it('replaces a stored secret with an empty editable field', async () => {
    const user = userEvent.setup();
    const onChange = vi.fn();
    render(<SecretInput ariaLabel="Key" value="****wxyz" onChange={onChange} />);
    await user.click(screen.getByRole('button', { name: 'Replace' }));
    expect(onChange).toHaveBeenCalledWith('');
  });

  it('toggles visibility of a plain secret', async () => {
    const user = userEvent.setup();
    render(<SecretInput ariaLabel="Key" value="sk-secret" onChange={() => {}} />);
    const input = screen.getByLabelText('Key');
    expect(input.getAttribute('type')).toBe('password');
    await user.click(screen.getByTitle('Show'));
    expect(input.getAttribute('type')).toBe('text');
  });
});
