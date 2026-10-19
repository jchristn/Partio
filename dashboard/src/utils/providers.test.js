import { describe, it, expect } from 'vitest';
import {
  AZURE_DEFAULT_API_VERSION,
  OLLAMA_CLOUD_ENDPOINT,
  PROBE_HEALTH_INTERVAL_MS,
  PROVIDERS,
  apiKeyApplies,
  apiKeyLabel,
  apiKeyRequired,
  applyProviderChange,
  applySettingsChange,
  buildConnectionPayload,
  buildProviderSettingsPayload,
  defaultProviderSettings,
  derivedEndpoint,
  getHealthCheckDefaults,
  getProviderDefaults,
  hasEndpointOverride,
  hasMaskedSecrets,
  isMaskedSecret,
  isOllamaDialect,
  mergeRevealedSecrets,
  normalizeEndpoint,
  providerLabel,
  providerSettingsToForm,
  providersForKind,
  validateConnection,
} from './providers';

const SERVER_API_FORMATS = ['Ollama', 'OpenAI', 'Gemini', 'vLLM', 'OllamaCloud', 'xAI', 'AzureOpenAI', 'Anthropic', 'Cohere', 'Bedrock', 'VertexAI', 'TEI', 'VoyageAI'];
const SERVICE_ACCOUNT = JSON.stringify({ type: 'service_account', client_email: 'sa@p.iam.gserviceaccount.com', private_key: '-----BEGIN PRIVATE KEY-----\nAAA\n-----END PRIVATE KEY-----\n' });

function formFor(apiFormat, kind, overrides = {}) {
  const settings = defaultProviderSettings(apiFormat);
  return { ApiFormat: apiFormat, ApiKey: '', ProviderSettings: settings, EndpointOverride: false, ...getProviderDefaults(apiFormat, kind, settings), ...overrides };
}

describe('provider catalog', () => {
  it('covers every server ApiFormat exactly once', () => {
    expect([...PROVIDERS.map(p => p.value)].sort()).toEqual([...SERVER_API_FORMATS].sort());
  });

  it('filters providers by endpoint kind', () => {
    const completion = providersForKind('completion').map(p => p.value);
    const embedding = providersForKind('embedding').map(p => p.value);
    expect(completion).not.toContain('TEI');
    expect(completion).not.toContain('VoyageAI');
    expect(completion).toContain('Anthropic');
    expect(embedding).not.toContain('Anthropic');
    expect(embedding).toContain('TEI');
    expect(embedding).toContain('VoyageAI');
  });

  it('labels providers and recognizes the Ollama dialect', () => {
    expect(providerLabel('VertexAI')).toBe('Google Vertex AI');
    expect(providerLabel('SomethingNew')).toBe('SomethingNew');
    expect(isOllamaDialect('OllamaCloud')).toBe(true);
    expect(isOllamaDialect('xAI')).toBe(false);
  });

  it('derives Bedrock and Vertex AI endpoints from the region', () => {
    expect(derivedEndpoint('Bedrock', { Region: 'eu-west-1' })).toBe('https://bedrock-runtime.eu-west-1.amazonaws.com');
    expect(derivedEndpoint('VertexAI', { Region: 'us-east4' })).toBe('https://us-east4-aiplatform.googleapis.com');
    expect(derivedEndpoint('Bedrock', { Region: '' })).toBe('');
    expect(getProviderDefaults('Bedrock', 'embedding').Endpoint).toBe('https://bedrock-runtime.us-east-1.amazonaws.com');
  });

  it('normalizes endpoints the way the server stores them', () => {
    expect(normalizeEndpoint('OllamaCloud', 'http://ignored')).toBe(OLLAMA_CLOUD_ENDPOINT);
    expect(normalizeEndpoint('Anthropic', 'https://api.anthropic.com/v1/')).toBe('https://api.anthropic.com');
    expect(normalizeEndpoint('Cohere', 'https://api.cohere.com/v2')).toBe('https://api.cohere.com');
    expect(normalizeEndpoint('AzureOpenAI', 'https://r.openai.azure.com/openai')).toBe('https://r.openai.azure.com');
    expect(normalizeEndpoint('VoyageAI', '')).toBe('https://api.voyageai.com');
    expect(normalizeEndpoint('vLLM', 'http://h:8000/v1')).toBe('http://h:8000/v1');
  });
});

describe('credentials', () => {
  it('knows which providers need, accept, or ignore an API key', () => {
    expect(apiKeyRequired('Anthropic')).toBe(true);
    expect(apiKeyRequired('Ollama')).toBe(false);
    expect(apiKeyApplies('Bedrock')).toBe(false);
    expect(apiKeyApplies('VertexAI', { CredentialSource: 'ServiceAccountJson' })).toBe(false);
    expect(apiKeyApplies('VertexAI', { CredentialSource: 'AccessToken' })).toBe(true);
    expect(apiKeyRequired('VertexAI', { CredentialSource: 'AccessToken' })).toBe(true);
    expect(apiKeyLabel('AzureOpenAI', { AuthMode: 'BearerToken' })).toBe('Entra ID Access Token');
    expect(apiKeyLabel('VertexAI', { CredentialSource: 'AccessToken' })).toBe('Access Token');
  });

  it('detects masked secrets and merges revealed values', () => {
    const form = { ApiFormat: 'Bedrock', ApiKey: '', ProviderSettings: { Region: 'us-east-1', SecretAccessKey: '****WXYZ', SessionToken: '' } };
    expect(isMaskedSecret('****WXYZ')).toBe(true);
    expect(isMaskedSecret('sk-live')).toBe(false);
    expect(hasMaskedSecrets(form)).toBe(true);

    const merged = mergeRevealedSecrets(form, { ProviderSettings: { Bedrock: { SecretAccessKey: 'real/secret/WXYZ', SessionToken: null } } });
    expect(merged.ProviderSettings.SecretAccessKey).toBe('real/secret/WXYZ');
    expect(merged.ProviderSettings.SessionToken).toBe('');
    expect(hasMaskedSecrets(merged)).toBe(false);
  });
});

describe('health check defaults', () => {
  it('builds provider-specific URLs', () => {
    expect(getHealthCheckDefaults('AzureOpenAI', 'https://r.openai.azure.com', { ApiVersion: '' }).HealthCheckUrl)
      .toBe('https://r.openai.azure.com/openai/models?api-version=' + AZURE_DEFAULT_API_VERSION);
    expect(getHealthCheckDefaults('TEI', 'http://tei:8080/').HealthCheckUrl).toBe('http://tei:8080/health');
    expect(getHealthCheckDefaults('Anthropic', 'https://api.anthropic.com').HealthCheckUrl).toBe('https://api.anthropic.com/v1/models');
    expect(getHealthCheckDefaults('OllamaCloud', 'x').HealthCheckUrl).toBe('https://ollama.com/api/tags');
    expect(getHealthCheckDefaults('Ollama', 'http://localhost:11434').HealthCheckUseAuth).toBe(false);
  });

  it('uses connectivity-probe timings and no URL for probe providers', () => {
    const bedrock = getHealthCheckDefaults('Bedrock', 'https://bedrock-runtime.us-east-1.amazonaws.com');
    expect(bedrock.HealthCheckUrl).toBe('');
    expect(bedrock.HealthCheckIntervalMs).toBe(PROBE_HEALTH_INTERVAL_MS);
  });
});

describe('switching providers', () => {
  it('resets the model, endpoint, credentials, and settings to the new defaults', () => {
    const start = formFor('Ollama', 'completion', { ApiKey: 'ollama-key' });
    const next = applyProviderChange(start, 'Bedrock', 'completion', false);
    expect(next.ApiFormat).toBe('Bedrock');
    expect(next.Model).toBe('anthropic.claude-3-5-sonnet-20240620-v1:0');
    expect(next.Endpoint).toBe('https://bedrock-runtime.us-east-1.amazonaws.com');
    expect(next.ApiKey).toBe('');
    expect(next.ProviderSettings).toEqual(defaultProviderSettings('Bedrock'));
    expect(next.HealthCheckUrl).toBe('');
  });

  it('keeps a custom model and a custom URL when the new provider takes a URL', () => {
    const start = formFor('OpenAI', 'completion', { Model: 'my-model', Endpoint: 'https://gateway.example.com' });
    const next = applyProviderChange(start, 'vLLM', 'completion', false);
    expect(next.Model).toBe('my-model');
    expect(next.Endpoint).toBe('https://gateway.example.com');
  });

  it('refreshes the provider-specific health URL even after health fields were edited', () => {
    const start = formFor('Ollama', 'embedding', { HealthCheckUrl: 'http://localhost:11434/api/tags', HealthCheckIntervalMs: 1234 });
    const next = applyProviderChange(start, 'TEI', 'embedding', true);
    expect(next.HealthCheckUrl).toBe('http://localhost:8080/health');
    expect(next.HealthCheckIntervalMs).toBe(1234);
  });

  it('re-derives the endpoint when the region changes unless it is overridden', () => {
    const form = formFor('VertexAI', 'embedding');
    const moved = applySettingsChange(form, 'Region', 'europe-west4', false);
    expect(moved.Endpoint).toBe('https://europe-west4-aiplatform.googleapis.com');

    const overridden = applySettingsChange({ ...form, EndpointOverride: true, Endpoint: 'https://private.example.com' }, 'Region', 'europe-west4', false);
    expect(overridden.Endpoint).toBe('https://private.example.com');
  });

  it('clears a Vertex access token when switching to another credential source', () => {
    const form = formFor('VertexAI', 'completion', { ApiKey: 'ya29.token', ProviderSettings: { ...defaultProviderSettings('VertexAI'), CredentialSource: 'AccessToken' } });
    expect(applySettingsChange(form, 'CredentialSource', 'ApplicationDefault', false).ApiKey).toBe('');
  });
});

describe('validation', () => {
  it('requires the fields each provider needs', () => {
    expect(validateConnection(formFor('Anthropic', 'completion'), 'completion').ApiKey).toMatch(/required/);
    expect(validateConnection(formFor('AzureOpenAI', 'completion', { ApiKey: 'k' }), 'completion').Model).toMatch(/Deployment Name/);
    expect(validateConnection(formFor('AzureOpenAI', 'completion', { ApiKey: 'k', Model: 'dep' }), 'completion').Endpoint).toMatch(/Resource URL/);

    const bedrock = validateConnection(formFor('Bedrock', 'completion'), 'completion');
    expect(bedrock['Settings.AccessKeyId']).toBeDefined();
    expect(bedrock['Settings.SecretAccessKey']).toBeDefined();
    const bedrockEnv = formFor('Bedrock', 'completion', { ProviderSettings: { ...defaultProviderSettings('Bedrock'), CredentialSource: 'Environment' } });
    expect(validateConnection(bedrockEnv, 'completion')).toEqual({});
  });

  it('checks the Vertex AI project, region, and service-account key', () => {
    const missing = validateConnection(formFor('VertexAI', 'embedding'), 'embedding');
    expect(missing['Settings.ProjectId']).toBeDefined();
    expect(missing['Settings.ServiceAccountJson']).toMatch(/Paste or upload/);

    const bad = formFor('VertexAI', 'embedding', { ProviderSettings: { ...defaultProviderSettings('VertexAI'), ProjectId: 'p', ServiceAccountJson: '{"client_email":"x"}' } });
    expect(validateConnection(bad, 'embedding')['Settings.ServiceAccountJson']).toMatch(/client_email and private_key/);

    const good = formFor('VertexAI', 'embedding', { ProviderSettings: { ...defaultProviderSettings('VertexAI'), ProjectId: 'p', ServiceAccountJson: SERVICE_ACCOUNT } });
    expect(validateConnection(good, 'embedding')).toEqual({});

    const stored = formFor('VertexAI', 'embedding', { ProviderSettings: { ...defaultProviderSettings('VertexAI'), ProjectId: 'p', ServiceAccountJson: '****' } });
    expect(validateConnection(stored, 'embedding')).toEqual({});
  });

  it('rejects a provider that cannot serve the endpoint kind', () => {
    expect(validateConnection(formFor('TEI', 'embedding'), 'completion').ApiFormat).toMatch(/cannot serve completion/);
  });

  it('accepts working configurations for every provider', () => {
    for (const provider of PROVIDERS) {
      for (const kind of provider.kinds) {
        const form = formFor(provider.value, kind, {
          ApiKey: 'key-1234567890',
          Model: getProviderDefaults(provider.value, kind).Model || 'model',
          Endpoint: getProviderDefaults(provider.value, kind).Endpoint || 'https://resource.example.com',
        });
        if (provider.value === 'Bedrock') Object.assign(form.ProviderSettings, { AccessKeyId: 'AKIA', SecretAccessKey: 'secret' });
        if (provider.value === 'VertexAI') Object.assign(form.ProviderSettings, { ProjectId: 'p', ServiceAccountJson: SERVICE_ACCOUNT });
        expect(validateConnection(form, kind), provider.value + ' ' + kind).toEqual({});
      }
    }
  });
});

describe('payloads', () => {
  it('sends only the matching provider-settings section with blanks as null', () => {
    expect(buildProviderSettingsPayload('AzureOpenAI', { ApiVersion: ' ', AuthMode: 'BearerToken' }))
      .toEqual({ AzureOpenAI: { ApiVersion: null, AuthMode: 'BearerToken' } });
    expect(buildProviderSettingsPayload('Cohere', { InputType: '' })).toBeNull();
    expect(buildProviderSettingsPayload('VoyageAI', { InputType: 'query', OutputDimension: '512' }))
      .toEqual({ VoyageAI: { InputType: 'query', OutputDimension: 512 } });
    expect(buildProviderSettingsPayload('Bedrock', { Region: 'us-east-1', CredentialSource: 'Environment', AccessKeyId: 'AKIA', SecretAccessKey: 's' }))
      .toEqual({ Bedrock: { Region: 'us-east-1', CredentialSource: 'Environment', AccessKeyId: null, SecretAccessKey: null, SessionToken: null } });
    expect(buildProviderSettingsPayload('Ollama', {})).toBeNull();
  });

  it('builds the connection payload with derived endpoints and no stray API key', () => {
    const form = formFor('Bedrock', 'completion', { ApiKey: 'left-over', ProviderSettings: { ...defaultProviderSettings('Bedrock'), Region: 'us-west-2', AccessKeyId: 'AKIA', SecretAccessKey: '****abcd' } });
    const payload = buildConnectionPayload(form);
    expect(payload.Endpoint).toBe('https://bedrock-runtime.us-west-2.amazonaws.com');
    expect(payload.ApiKey).toBeNull();
    expect(payload.ProviderSettings.Bedrock.SecretAccessKey).toBe('****abcd');

    const override = buildConnectionPayload({ ...form, EndpointOverride: true, Endpoint: 'http://localhost:4566/' });
    expect(override.Endpoint).toBe('http://localhost:4566');
  });

  it('loads stored settings into the form and detects overrides', () => {
    const settings = providerSettingsToForm('VoyageAI', { VoyageAI: { InputType: 'document', OutputDimension: 1024 } });
    expect(settings).toEqual({ InputType: 'document', OutputDimension: '1024' });
    expect(providerSettingsToForm('Ollama', null)).toBeNull();
    expect(hasEndpointOverride('Bedrock', 'http://localhost:4566', { Region: 'us-east-1' })).toBe(true);
    expect(hasEndpointOverride('Bedrock', 'https://bedrock-runtime.us-east-1.amazonaws.com', { Region: 'us-east-1' })).toBe(false);
  });
});

describe('selectable authentication', () => {
  it('is offered for Ollama, vLLM, TEI, and OpenAI with sensible defaults', async () => {
    const { supportsSelectableAuth, defaultAuth } = await import('./providers');
    expect(PROVIDERS.filter(p => supportsSelectableAuth(p.value)).map(p => p.value).sort()).toEqual(['Ollama', 'OpenAI', 'TEI', 'vLLM']);
    expect(defaultAuth('Ollama').Mode).toBe('None');
    expect(defaultAuth('OpenAI').Mode).toBe('Bearer');
    expect(defaultAuth('Anthropic')).toBeNull();
  });

  it('describes fixed schemes exactly and labels the secret by mode', async () => {
    const { describeAuth } = await import('./providers');
    expect(describeAuth('Anthropic')).toMatch(/x-api-key/);
    expect(describeAuth('Gemini')).toMatch(/x-goog-api-key/);
    expect(describeAuth('OllamaCloud')).toMatch(/Authorization: Bearer/);
    expect(describeAuth('AzureOpenAI', { AuthMode: 'BearerToken' })).toMatch(/Entra ID/);
    expect(describeAuth('Ollama', null, { Mode: 'Header', HeaderName: 'X-Gateway-Key' })).toBe('Sent as X-Gateway-Key: <value>.');
    expect(describeAuth('vLLM', null, { Mode: 'QueryParameter', QueryParameterName: 'api_key' })).toMatch(/\?api_key=<value>/);
    expect(apiKeyLabel('Ollama', null, { Mode: 'Basic' })).toBe('Password');
    expect(apiKeyApplies('Ollama', null, { Mode: 'None' })).toBe(false);
    expect(apiKeyRequired('TEI', null, { Mode: 'Header' })).toBe(true);
  });

  it('reads legacy endpoints as bearer or none', async () => {
    const { authFromEndpoint } = await import('./providers');
    expect(authFromEndpoint({ ApiFormat: 'Ollama', ApiKey: '****abcd' }).Mode).toBe('Bearer');
    expect(authFromEndpoint({ ApiFormat: 'Ollama', ApiKey: null }).Mode).toBe('None');
    expect(authFromEndpoint({ ApiFormat: 'vLLM', ProviderSettings: { Auth: { Mode: 'Header', HeaderName: 'X-K' } } }))
      .toEqual({ Mode: 'Header', HeaderName: 'X-K', QueryParameterName: '', Username: '' });
    expect(authFromEndpoint({ ApiFormat: 'Anthropic', ApiKey: 'k' })).toBeNull();
  });

  it('validates names and credentials per mode', () => {
    const base = formFor('Ollama', 'completion');
    expect(validateConnection({ ...base, Auth: { Mode: 'Header', HeaderName: '' }, ApiKey: 'k' }, 'completion')['Auth.HeaderName']).toMatch(/required/);
    expect(validateConnection({ ...base, Auth: { Mode: 'Header', HeaderName: 'X Bad' }, ApiKey: 'k' }, 'completion')['Auth.HeaderName']).toMatch(/valid HTTP header/);
    expect(validateConnection({ ...base, Auth: { Mode: 'Header', HeaderName: 'Host' }, ApiKey: 'k' }, 'completion')['Auth.HeaderName']).toMatch(/managed by the HTTP client/);
    expect(validateConnection({ ...base, Auth: { Mode: 'QueryParameter', QueryParameterName: 'a&b' }, ApiKey: 'k' }, 'completion')['Auth.QueryParameterName']).toBeDefined();
    expect(validateConnection({ ...base, Auth: { Mode: 'Basic', Username: 'a:b' }, ApiKey: 'k' }, 'completion')['Auth.Username']).toMatch(/colon/);
    expect(validateConnection({ ...base, Auth: { Mode: 'Bearer' }, ApiKey: '' }, 'completion').ApiKey).toMatch(/Bearer Token is required/);
    expect(validateConnection({ ...base, Auth: { Mode: 'Header', HeaderName: 'X-Gateway-Key' }, ApiKey: 'k' }, 'completion')).toEqual({});
  });

  it('sends an explicit Auth section with only the fields its mode uses', () => {
    const payload = buildConnectionPayload({ ...formFor('vLLM', 'completion'), ApiKey: 'secret', Auth: { Mode: 'QueryParameter', HeaderName: 'X-Stale', QueryParameterName: ' api_key ', Username: 'stale' } });
    expect(payload.ProviderSettings).toEqual({ Auth: { Mode: 'QueryParameter', HeaderName: null, QueryParameterName: 'api_key', Username: null } });
    expect(payload.ApiKey).toBe('secret');

    const none = buildConnectionPayload({ ...formFor('Ollama', 'embedding'), ApiKey: 'left-over', Auth: { Mode: 'None' } });
    expect(none.ApiKey).toBeNull();
    expect(none.ProviderSettings.Auth.Mode).toBe('None');

    expect(buildConnectionPayload({ ...formFor('Anthropic', 'completion'), ApiKey: 'k', Auth: { Mode: 'Header', HeaderName: 'X' } }).ProviderSettings).toBeNull();
  });

  it('authenticates local Ollama health checks only when a credential is selected', () => {
    expect(getHealthCheckDefaults('Ollama', 'http://localhost:11434', null, { Mode: 'None' }).HealthCheckUseAuth).toBe(false);
    expect(getHealthCheckDefaults('Ollama', 'http://localhost:11434', null, { Mode: 'Header', HeaderName: 'X' }).HealthCheckUseAuth).toBe(true);
  });
});
