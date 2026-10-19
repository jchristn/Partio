import { describe, it, expect } from 'vitest';
import {
  API_FORMAT_OPTIONS,
  OLLAMA_CLOUD_ENDPOINT,
  XAI_ENDPOINT,
  applyApiFormatDefaults,
  getHealthCheckDefaults,
  isFixedEndpoint,
  isOllamaDialect,
  requiresApiKey,
} from './apiFormats';

const defaultsFor = (format) => {
  switch (format) {
    case 'OllamaCloud': return { Model: 'gpt-oss:20b', Endpoint: OLLAMA_CLOUD_ENDPOINT };
    case 'xAI': return { Model: 'grok-4', Endpoint: XAI_ENDPOINT };
    case 'OpenAI': return { Model: 'gpt-4.1-mini', Endpoint: 'https://api.openai.com' };
    default: return { Model: 'gemma3:4b', Endpoint: 'http://localhost:11434' };
  }
};

describe('apiFormats', () => {
  it('offers every server ApiFormat value', () => {
    expect(API_FORMAT_OPTIONS.map(o => o.value)).toEqual(['Ollama', 'OllamaCloud', 'OpenAI', 'Gemini', 'vLLM', 'xAI']);
  });

  it('flags providers that require an API key or a fixed host', () => {
    expect(requiresApiKey('OllamaCloud')).toBe(true);
    expect(requiresApiKey('xAI')).toBe(true);
    expect(requiresApiKey('Ollama')).toBe(false);
    expect(requiresApiKey('OpenAI')).toBe(false);
    expect(isFixedEndpoint('OllamaCloud')).toBe(true);
    expect(isFixedEndpoint('xAI')).toBe(false);
    expect(isOllamaDialect('OllamaCloud')).toBe(true);
    expect(isOllamaDialect('xAI')).toBe(false);
  });

  it('builds authenticated health checks for Ollama Cloud and xAI', () => {
    const cloud = getHealthCheckDefaults('OllamaCloud', 'http://ignored:11434');
    expect(cloud.HealthCheckUrl).toBe('https://ollama.com/api/tags');
    expect(cloud.HealthCheckUseAuth).toBe(true);
    expect(cloud.HealthCheckIntervalMs).toBe(15000);

    expect(getHealthCheckDefaults('xAI', 'https://api.x.ai').HealthCheckUrl).toBe('https://api.x.ai/v1/models');
    expect(getHealthCheckDefaults('xAI', 'https://api.x.ai/v1/').HealthCheckUrl).toBe('https://api.x.ai/v1/models');
    expect(getHealthCheckDefaults('xAI', 'https://api.x.ai').HealthCheckUseAuth).toBe(true);
    expect(getHealthCheckDefaults('Ollama', 'http://localhost:11434').HealthCheckUseAuth).toBe(false);
  });

  it('switches to Ollama Cloud with its fixed host even after the endpoint was edited', () => {
    const form = { ApiFormat: 'Ollama', Model: 'gemma3:4b', Endpoint: 'http://my-gpu-box:11434' };
    const next = applyApiFormatDefaults(form, 'OllamaCloud', false, defaultsFor);
    expect(next.ApiFormat).toBe('OllamaCloud');
    expect(next.Endpoint).toBe(OLLAMA_CLOUD_ENDPOINT);
    expect(next.Model).toBe('gpt-oss:20b');
    expect(next.HealthCheckUrl).toBe('https://ollama.com/api/tags');
  });

  it('switches to xAI defaults and keeps a custom endpoint for other formats', () => {
    const fresh = applyApiFormatDefaults({ ApiFormat: 'Ollama', Model: 'gemma3:4b', Endpoint: 'http://localhost:11434' }, 'xAI', false, defaultsFor);
    expect(fresh.Endpoint).toBe(XAI_ENDPOINT);
    expect(fresh.Model).toBe('grok-4');

    const custom = applyApiFormatDefaults({ ApiFormat: 'Ollama', Model: 'custom', Endpoint: 'http://gateway:9000' }, 'OpenAI', true, defaultsFor);
    expect(custom.Endpoint).toBe('http://gateway:9000');
    expect(custom.Model).toBe('custom');
    expect(custom.HealthCheckUrl).toBeUndefined();
  });

  it('leaves Ollama Cloud when switching back to a format with an editable endpoint', () => {
    const next = applyApiFormatDefaults({ ApiFormat: 'OllamaCloud', Model: 'gpt-oss:20b', Endpoint: OLLAMA_CLOUD_ENDPOINT }, 'Ollama', false, defaultsFor);
    expect(next.Endpoint).toBe('http://localhost:11434');
    expect(next.Model).toBe('gemma3:4b');
  });
});
