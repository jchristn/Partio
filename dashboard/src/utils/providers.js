// Provider catalog for the embedding and inference endpoint forms. Each entry describes a PolyPrompt
// provider (an endpoint ApiFormat): what it can serve, how it authenticates, which connection fields it
// needs, and its defaults. The forms are generated from this catalog, so adding a provider here is enough
// to expose it. Mirrors Partio.Core.ThirdParty.ProviderDefaults and ProviderDefaults.Validate on the server.

export const MASK_PREFIX = '****';

export const OLLAMA_CLOUD_ENDPOINT = 'https://ollama.com';
export const XAI_ENDPOINT = 'https://api.x.ai';
export const AZURE_DEFAULT_API_VERSION = '2024-10-21';
export const ANTHROPIC_DEFAULT_VERSION = '2023-06-01';

export const PROBE_HEALTH_INTERVAL_MS = 60000;
export const PROBE_HEALTH_TIMEOUT_MS = 10000;

export const AWS_REGIONS = [
  'us-east-1', 'us-east-2', 'us-west-2', 'ca-central-1', 'eu-central-1', 'eu-west-1', 'eu-west-2', 'eu-west-3',
  'ap-northeast-1', 'ap-northeast-2', 'ap-south-1', 'ap-southeast-1', 'ap-southeast-2', 'sa-east-1',
];

export const GCP_REGIONS = [
  'us-central1', 'us-east1', 'us-east4', 'us-west1', 'us-west4', 'northamerica-northeast1', 'europe-west1',
  'europe-west2', 'europe-west3', 'europe-west4', 'asia-northeast1', 'asia-southeast1', 'australia-southeast1',
];

/**
 * Provider catalog. Field notes:
 * - kinds: endpoint kinds the provider serves ('completion', 'embedding').
 * - group: 'selfhosted' or 'cloud' (picker grouping).
 * - auth: badge text describing how requests authenticate.
 * - endpoint.mode: 'fixed' (host pinned), 'required' (no default), 'default' (prefilled, editable),
 *   or 'derived' (computed from the region, overridable under Advanced).
 * - apiKey.mode: 'required', 'optional', or 'none'.
 * - settings: ProviderSettings section name, when the provider has one.
 * - health: 'http' (probe a URL) or 'probe' (PolyPrompt connectivity probe).
 */
export const PROVIDERS = [
  {
    value: 'Ollama',
    label: 'Ollama',
    group: 'selfhosted',
    summary: 'Local or self-hosted Ollama server. Supports model pull and keep-alive loading.',
    kinds: ['completion', 'embedding'],
    auth: 'Optional key',
    endpoint: { mode: 'required', label: 'Server URL', placeholder: 'http://localhost:11434', defaultValue: 'http://localhost:11434', help: 'Base URL of the Ollama server.' },
    apiKey: { mode: 'optional', label: 'API Key', help: 'Only needed when Ollama sits behind an authenticating proxy.' },
    model: { label: 'Model', defaults: { completion: 'gemma3:4b', embedding: 'nomic-embed-text' }, placeholder: 'gemma3:4b', help: 'Ollama model tag, for example gemma3:4b or nomic-embed-text.' },
    health: 'http',
    proxy: true,
  },
  {
    value: 'vLLM',
    label: 'vLLM',
    group: 'selfhosted',
    summary: 'Self-hosted vLLM server speaking the OpenAI-compatible API.',
    kinds: ['completion', 'embedding'],
    auth: 'Optional key',
    endpoint: { mode: 'required', label: 'Server URL', placeholder: 'http://localhost:8000', defaultValue: 'http://localhost:8000', help: 'Base URL of the vLLM server (without /v1).' },
    apiKey: { mode: 'optional', label: 'API Key', help: 'Required when vLLM was started with --api-key.' },
    model: { label: 'Model', defaults: { completion: 'Qwen/Qwen2.5-7B-Instruct', embedding: 'intfloat/e5-small-v2' }, placeholder: 'Qwen/Qwen2.5-7B-Instruct', help: 'The model name vLLM is serving.' },
    health: 'http',
    proxy: true,
  },
  {
    value: 'TEI',
    label: 'Hugging Face TEI',
    group: 'selfhosted',
    summary: 'Text Embeddings Inference server. Hosts exactly one embedding model.',
    kinds: ['embedding'],
    auth: 'Optional key',
    endpoint: { mode: 'required', label: 'Server URL', placeholder: 'http://localhost:8080', defaultValue: 'http://localhost:8080', help: 'Base URL of the TEI server.' },
    apiKey: { mode: 'optional', label: 'API Key', help: 'Only needed when TEI was started with --api-key.' },
    model: { label: 'Model', defaults: { embedding: 'BAAI/bge-small-en-v1.5' }, placeholder: 'BAAI/bge-small-en-v1.5', help: 'Informational: the TEI server decides the model. Use the model ID it hosts.' },
    health: 'http',
    proxy: false,
  },
  {
    value: 'OpenAI',
    label: 'OpenAI',
    group: 'cloud',
    summary: 'OpenAI API, or any OpenAI-compatible service.',
    kinds: ['completion', 'embedding'],
    auth: 'API key',
    endpoint: { mode: 'default', label: 'Endpoint URL', placeholder: 'https://api.openai.com', defaultValue: 'https://api.openai.com', help: 'API root without /v1. Change it for OpenAI-compatible services.' },
    apiKey: { mode: 'optional', label: 'API Key', help: 'Your OpenAI API key (sk-...). Required by api.openai.com.' },
    model: { label: 'Model', defaults: { completion: 'gpt-4.1-mini', embedding: 'text-embedding-3-small' }, placeholder: 'gpt-4.1-mini', help: 'OpenAI model name.' },
    health: 'http',
    proxy: true,
  },
  {
    value: 'AzureOpenAI',
    label: 'Azure OpenAI',
    group: 'cloud',
    summary: 'OpenAI models deployed in your Azure resource. Requests target a deployment.',
    kinds: ['completion', 'embedding'],
    auth: 'API key or Entra ID',
    settings: 'AzureOpenAI',
    endpoint: { mode: 'required', label: 'Resource URL', placeholder: 'https://my-resource.openai.azure.com', defaultValue: '', help: 'Your Azure OpenAI resource endpoint, from the Azure portal (Keys and Endpoint).' },
    apiKey: { mode: 'required', label: 'API Key', help: 'Azure OpenAI key 1 or key 2.' },
    model: { label: 'Deployment Name', defaults: { completion: '', embedding: '' }, placeholder: 'gpt-4o-prod', help: 'The name you gave the deployment in Azure AI Foundry, not the base model name.' },
    health: 'http',
    proxy: true,
  },
  {
    value: 'Anthropic',
    label: 'Anthropic',
    group: 'cloud',
    summary: 'Claude models through the Anthropic Messages API.',
    kinds: ['completion'],
    auth: 'API key',
    settings: 'Anthropic',
    endpoint: { mode: 'default', label: 'Endpoint URL', placeholder: 'https://api.anthropic.com', defaultValue: 'https://api.anthropic.com', help: 'API root without /v1.' },
    apiKey: { mode: 'required', label: 'API Key', help: 'Anthropic API key (sk-ant-...). Sent as x-api-key.' },
    model: { label: 'Model', defaults: { completion: 'claude-sonnet-4-5' }, placeholder: 'claude-sonnet-4-5', help: 'Claude model ID.' },
    health: 'http',
    proxy: true,
  },
  {
    value: 'Gemini',
    label: 'Google Gemini',
    group: 'cloud',
    summary: 'Gemini models through the Google AI Studio (Generative Language) API.',
    kinds: ['completion', 'embedding'],
    auth: 'API key',
    endpoint: { mode: 'default', label: 'Endpoint URL', placeholder: 'https://generativelanguage.googleapis.com', defaultValue: 'https://generativelanguage.googleapis.com', help: 'Gemini API root.' },
    apiKey: { mode: 'optional', label: 'API Key', help: 'Google AI Studio API key. Sent as x-goog-api-key.' },
    model: { label: 'Model', defaults: { completion: 'gemini-2.5-flash', embedding: 'gemini-embedding-001' }, placeholder: 'gemini-2.5-flash', help: 'Gemini model name.' },
    health: 'http',
    proxy: true,
  },
  {
    value: 'VertexAI',
    label: 'Google Vertex AI',
    group: 'cloud',
    summary: 'Gemini and embedding models in your Google Cloud project, authenticated with OAuth.',
    kinds: ['completion', 'embedding'],
    auth: 'GCP OAuth',
    settings: 'VertexAI',
    endpoint: { mode: 'derived', label: 'Endpoint URL', placeholder: 'https://us-central1-aiplatform.googleapis.com', defaultValue: '', help: 'Derived from the region. Override only for private endpoints.' },
    apiKey: { mode: 'none', label: 'Access Token', help: 'A short-lived OAuth access token, for example from gcloud auth print-access-token. Tokens expire after about an hour.' },
    model: { label: 'Model', defaults: { completion: 'gemini-2.5-flash', embedding: 'text-embedding-005' }, placeholder: 'gemini-2.5-flash', help: 'Vertex AI publisher model ID.' },
    health: 'probe',
    proxy: false,
  },
  {
    value: 'Bedrock',
    label: 'AWS Bedrock',
    group: 'cloud',
    summary: 'Claude, Llama, Titan, Cohere and more through Bedrock, signed with AWS SigV4.',
    kinds: ['completion', 'embedding'],
    auth: 'AWS SigV4',
    settings: 'Bedrock',
    endpoint: { mode: 'derived', label: 'Endpoint URL', placeholder: 'https://bedrock-runtime.us-east-1.amazonaws.com', defaultValue: '', help: 'Derived from the region. Override for VPC endpoints or local emulators.' },
    apiKey: { mode: 'none' },
    model: { label: 'Model ID', defaults: { completion: 'anthropic.claude-3-5-sonnet-20240620-v1:0', embedding: 'amazon.titan-embed-text-v2:0' }, placeholder: 'anthropic.claude-3-5-sonnet-20240620-v1:0', help: 'Bedrock model ID or inference profile ID. The model must be enabled in your account and region.' },
    health: 'probe',
    proxy: false,
  },
  {
    value: 'Cohere',
    label: 'Cohere',
    group: 'cloud',
    summary: 'Command chat models and Embed models through the Cohere v2 API.',
    kinds: ['completion', 'embedding'],
    auth: 'API key',
    settings: 'Cohere',
    endpoint: { mode: 'default', label: 'Endpoint URL', placeholder: 'https://api.cohere.com', defaultValue: 'https://api.cohere.com', help: 'API root without /v1 or /v2.' },
    apiKey: { mode: 'required', label: 'API Key', help: 'Cohere API key.' },
    model: { label: 'Model', defaults: { completion: 'command-a-03-2025', embedding: 'embed-v4.0' }, placeholder: 'command-a-03-2025', help: 'Cohere model name.' },
    health: 'http',
    proxy: true,
  },
  {
    value: 'VoyageAI',
    label: 'VoyageAI',
    group: 'cloud',
    summary: 'Voyage embedding models for retrieval.',
    kinds: ['embedding'],
    auth: 'API key',
    settings: 'VoyageAI',
    endpoint: { mode: 'default', label: 'Endpoint URL', placeholder: 'https://api.voyageai.com', defaultValue: 'https://api.voyageai.com', help: 'API root without /v1.' },
    apiKey: { mode: 'required', label: 'API Key', help: 'VoyageAI API key (pa-...).' },
    model: { label: 'Model', defaults: { embedding: 'voyage-3.5' }, placeholder: 'voyage-3.5', help: 'Voyage model name.' },
    health: 'probe',
    proxy: false,
  },
  {
    value: 'xAI',
    label: 'xAI (Grok)',
    group: 'cloud',
    summary: 'Grok models through the native xAI Responses API.',
    kinds: ['completion', 'embedding'],
    auth: 'API key',
    endpoint: { mode: 'default', label: 'Endpoint URL', placeholder: XAI_ENDPOINT, defaultValue: XAI_ENDPOINT, help: 'API root. A trailing /v1 is removed.' },
    apiKey: { mode: 'required', label: 'API Key', help: 'xAI API key (xai-...).' },
    model: { label: 'Model', defaults: { completion: 'grok-4', embedding: '' }, placeholder: 'grok-4', help: 'Grok model name.' },
    health: 'http',
    proxy: true,
  },
  {
    value: 'OllamaCloud',
    label: 'Ollama Cloud',
    group: 'cloud',
    summary: 'Hosted models on ollama.com through the native Ollama API.',
    kinds: ['completion', 'embedding'],
    auth: 'API key',
    endpoint: { mode: 'fixed', label: 'Endpoint URL', placeholder: OLLAMA_CLOUD_ENDPOINT, defaultValue: OLLAMA_CLOUD_ENDPOINT, help: 'Ollama Cloud always uses https://ollama.com.' },
    apiKey: { mode: 'required', label: 'API Key', help: 'Ollama Cloud API key from ollama.com/settings/keys.' },
    model: { label: 'Model', defaults: { completion: 'gpt-oss:20b', embedding: 'embeddinggemma' }, placeholder: 'gpt-oss:20b', help: 'Ollama Cloud model tag.' },
    health: 'http',
    proxy: true,
  },
];

export const PROVIDER_GROUPS = [
  { key: 'selfhosted', label: 'Self-hosted' },
  { key: 'cloud', label: 'Cloud' },
];

const UNKNOWN_PROVIDER = PROVIDERS[0];

/** Catalog entry for an ApiFormat (falls back to Ollama for unknown values). */
export function getProvider(apiFormat) {
  return PROVIDERS.find(p => p.value === apiFormat) || UNKNOWN_PROVIDER;
}

/** Display label for an ApiFormat, or the raw value when it is not in the catalog. */
export function providerLabel(apiFormat) {
  const provider = PROVIDERS.find(p => p.value === apiFormat);
  return provider ? provider.label : (apiFormat || '');
}

/** Providers that can back the given endpoint kind ('completion' or 'embedding'). */
export function providersForKind(kind) {
  return PROVIDERS.filter(p => p.kinds.includes(kind));
}

/** Whether the provider speaks the native Ollama API (local Ollama or Ollama Cloud). */
export function isOllamaDialect(apiFormat) {
  return apiFormat === 'Ollama' || apiFormat === 'OllamaCloud';
}

/** Whether a value is a masked secret returned by the server. */
export function isMaskedSecret(value) {
  return typeof value === 'string' && value.startsWith(MASK_PREFIX);
}

/** Default provider settings section for a provider, or null when it has none. */
export function defaultProviderSettings(apiFormat) {
  switch (apiFormat) {
    case 'AzureOpenAI': return { ApiVersion: '', AuthMode: 'ApiKey' };
    case 'Anthropic': return { AnthropicVersion: '', WorkspaceId: '' };
    case 'Bedrock': return { Region: 'us-east-1', CredentialSource: 'Static', AccessKeyId: '', SecretAccessKey: '', SessionToken: '' };
    case 'VertexAI': return { ProjectId: '', Region: 'us-central1', CredentialSource: 'ServiceAccountJson', ServiceAccountJson: '' };
    case 'Cohere': return { InputType: '' };
    case 'VoyageAI': return { InputType: '', OutputDimension: '' };
    default: return null;
  }
}

/** Endpoint URL derived from the provider settings (Bedrock and Vertex AI), or ''. */
export function derivedEndpoint(apiFormat, settings) {
  const region = (settings?.Region || '').trim();
  if (!region) return '';
  if (apiFormat === 'Bedrock') return `https://bedrock-runtime.${region}.amazonaws.com`;
  if (apiFormat === 'VertexAI') return `https://${region}-aiplatform.googleapis.com`;
  return '';
}

/** Whether the provider is checked with a PolyPrompt connectivity probe instead of an HTTP URL. */
export function usesConnectivityProbe(apiFormat) {
  return getProvider(apiFormat).health === 'probe';
}

/** One-line description of the connectivity probe used to health-check a provider, or '' for HTTP checks. */
export function probeDescription(apiFormat, kind) {
  switch (apiFormat) {
    case 'Bedrock': return 'lists foundation models in the region (needs bedrock:ListFoundationModels)';
    case 'VertexAI': return kind === 'completion' ? 'counts tokens for the model, which is not billed' : 'embeds one word, which may be billed';
    case 'VoyageAI': return 'embeds one word, which may be billed';
    default: return '';
  }
}

/** Whether the ApiKey field applies, given the provider and its settings. */
export function apiKeyApplies(apiFormat, settings) {
  const provider = getProvider(apiFormat);
  if (apiFormat === 'VertexAI') return settings?.CredentialSource === 'AccessToken';
  return provider.apiKey.mode !== 'none';
}

/** Whether the ApiKey is required, given the provider and its settings. */
export function apiKeyRequired(apiFormat, settings) {
  if (apiFormat === 'VertexAI') return settings?.CredentialSource === 'AccessToken';
  return getProvider(apiFormat).apiKey.mode === 'required';
}

/** Label for the ApiKey field, which depends on the Azure auth mode. */
export function apiKeyLabel(apiFormat, settings) {
  if (apiFormat === 'AzureOpenAI' && settings?.AuthMode === 'BearerToken') return 'Entra ID Access Token';
  return getProvider(apiFormat).apiKey.label || 'API Key';
}

function trimTrailingSlashes(value) {
  return (value || '').trim().replace(/\/+$/, '');
}

function trimSuffix(value, suffix) {
  const trimmed = trimTrailingSlashes(value);
  return trimmed.toLowerCase().endsWith(suffix) ? trimmed.slice(0, -suffix.length) : trimmed;
}

/** Normalize an endpoint URL the way the server stores it (API root, version suffixes removed). */
export function normalizeEndpoint(apiFormat, endpoint, settings) {
  if (apiFormat === 'OllamaCloud') return OLLAMA_CLOUD_ENDPOINT;
  if (!endpoint || !endpoint.trim()) {
    const provider = getProvider(apiFormat);
    return provider.endpoint.mode === 'derived' ? derivedEndpoint(apiFormat, settings) : (provider.endpoint.mode === 'default' ? provider.endpoint.defaultValue : '');
  }
  switch (apiFormat) {
    case 'xAI':
    case 'Anthropic':
    case 'VoyageAI':
      return trimSuffix(endpoint, '/v1');
    case 'Cohere':
      return trimSuffix(trimSuffix(endpoint, '/v2'), '/v1');
    case 'AzureOpenAI':
      return trimSuffix(endpoint, '/openai');
    case 'Bedrock':
    case 'VertexAI':
    case 'TEI':
      return trimTrailingSlashes(endpoint);
    default:
      return endpoint;
  }
}

/** Health check defaults for a provider and endpoint URL. */
export function getHealthCheckDefaults(apiFormat, endpoint, settings) {
  const probe = usesConnectivityProbe(apiFormat);
  const base = trimTrailingSlashes(normalizeEndpoint(apiFormat, endpoint, settings));
  let url = '';
  if (!probe && base) {
    switch (apiFormat) {
      case 'Ollama':
      case 'OllamaCloud':
        url = base + '/api/tags';
        break;
      case 'Gemini':
        url = base + '/v1beta/models';
        break;
      case 'AzureOpenAI':
        url = base + '/openai/models?api-version=' + ((settings?.ApiVersion || '').trim() || AZURE_DEFAULT_API_VERSION);
        break;
      case 'TEI':
        url = base + '/health';
        break;
      default:
        url = base + '/v1/models';
    }
  }

  const local = apiFormat === 'Ollama';
  return {
    HealthCheckUrl: url,
    HealthCheckMethod: 'GET',
    HealthCheckIntervalMs: probe ? PROBE_HEALTH_INTERVAL_MS : (local ? 5000 : 15000),
    HealthCheckTimeoutMs: probe ? PROBE_HEALTH_TIMEOUT_MS : (local ? 2000 : 5000),
    HealthCheckExpectedStatusCode: 200,
    HealthyThreshold: 2,
    UnhealthyThreshold: 2,
    HealthCheckUseAuth: !local,
  };
}

/** Default model and endpoint for a provider and endpoint kind. */
export function getProviderDefaults(apiFormat, kind, settings) {
  const provider = getProvider(apiFormat);
  const endpoint = provider.endpoint.mode === 'derived'
    ? derivedEndpoint(apiFormat, settings || defaultProviderSettings(apiFormat))
    : provider.endpoint.defaultValue;
  return { Model: provider.model.defaults[kind] ?? '', Endpoint: endpoint };
}

/**
 * Switch a form to another provider. Provider-specific values (model, endpoint, API key, and provider
 * settings) are reset to the new provider's defaults unless the user changed the model or endpoint away
 * from the previous provider's defaults. Health check fields follow unless the user edited them.
 */
export function applyProviderChange(form, newFormat, kind, healthFieldsEdited) {
  const previous = getProviderDefaults(form.ApiFormat, kind, form.ProviderSettings);
  const settings = defaultProviderSettings(newFormat);
  const next = getProviderDefaults(newFormat, kind, settings);
  const newProvider = getProvider(newFormat);
  const nextForm = { ...form, ApiFormat: newFormat, ProviderSettings: settings, EndpointOverride: false };

  if (!form.Model || form.Model === previous.Model) nextForm.Model = next.Model;

  const keepEndpoint = form.Endpoint && form.Endpoint !== previous.Endpoint
    && (newProvider.endpoint.mode === 'required' || newProvider.endpoint.mode === 'default');
  nextForm.Endpoint = keepEndpoint ? form.Endpoint : next.Endpoint;

  if (!apiKeyApplies(newFormat, settings)) nextForm.ApiKey = '';

  // The health check URL and auth are provider-specific, so they always follow the provider; timings and
  // thresholds keep any values the user edited.
  const health = getHealthCheckDefaults(newFormat, nextForm.Endpoint, settings);
  if (healthFieldsEdited) {
    nextForm.HealthCheckUrl = health.HealthCheckUrl;
    nextForm.HealthCheckUseAuth = health.HealthCheckUseAuth;
  } else {
    Object.assign(nextForm, health);
  }

  return nextForm;
}

/** Update one provider-settings field, re-deriving the endpoint and health URL when they depend on it. */
export function applySettingsChange(form, field, value, healthFieldsEdited) {
  const settings = { ...(form.ProviderSettings || {}), [field]: value };
  const nextForm = { ...form, ProviderSettings: settings };
  const provider = getProvider(form.ApiFormat);

  if (provider.endpoint.mode === 'derived' && field === 'Region' && !form.EndpointOverride) {
    nextForm.Endpoint = derivedEndpoint(form.ApiFormat, settings);
  }
  if (form.ApiFormat === 'VertexAI' && field === 'CredentialSource' && value !== 'AccessToken') {
    nextForm.ApiKey = '';
  }
  if (!healthFieldsEdited) {
    nextForm.HealthCheckUrl = getHealthCheckDefaults(form.ApiFormat, nextForm.Endpoint, settings).HealthCheckUrl;
  }
  return nextForm;
}

function isBlank(value) {
  return value == null || String(value).trim() === '';
}

function isValidServiceAccountJson(value) {
  if (isMaskedSecret(value)) return true;
  try {
    const parsed = JSON.parse(value);
    return parsed && typeof parsed === 'object' && !isBlank(parsed.client_email) && !isBlank(parsed.private_key);
  } catch {
    return false;
  }
}

/**
 * Validate the connection fields of a form for the given endpoint kind. Returns a map of field key to
 * message (empty when valid). Keys are ApiFormat, Model, Endpoint, ApiKey, or Settings.<Field>.
 */
export function validateConnection(form, kind) {
  const errors = {};
  const provider = getProvider(form.ApiFormat);
  const settings = form.ProviderSettings || {};

  if (!provider.kinds.includes(kind)) {
    errors.ApiFormat = `${provider.label} cannot serve ${kind} endpoints.`;
  }
  if (isBlank(form.Model)) errors.Model = `${provider.model.label} is required.`;

  const endpoint = normalizeEndpoint(form.ApiFormat, form.Endpoint, settings);
  if (isBlank(endpoint) && provider.endpoint.mode !== 'derived') errors.Endpoint = `${provider.endpoint.label} is required.`;
  else if (!isBlank(endpoint) && !/^https?:\/\/[^\s/]+/i.test(endpoint)) errors.Endpoint = 'Enter a URL starting with http:// or https://.';

  if (apiKeyRequired(form.ApiFormat, settings) && isBlank(form.ApiKey)) errors.ApiKey = `${apiKeyLabel(form.ApiFormat, settings)} is required.`;

  if (form.ApiFormat === 'Bedrock') {
    if (isBlank(settings.Region)) errors['Settings.Region'] = 'Region is required.';
    if (settings.CredentialSource !== 'Environment') {
      if (isBlank(settings.AccessKeyId)) errors['Settings.AccessKeyId'] = 'Access key ID is required.';
      if (isBlank(settings.SecretAccessKey)) errors['Settings.SecretAccessKey'] = 'Secret access key is required.';
    }
  }

  if (form.ApiFormat === 'VertexAI') {
    if (isBlank(settings.ProjectId)) errors['Settings.ProjectId'] = 'Project ID is required.';
    if (isBlank(settings.Region)) errors['Settings.Region'] = 'Region is required.';
    if ((settings.CredentialSource || 'ServiceAccountJson') === 'ServiceAccountJson') {
      if (isBlank(settings.ServiceAccountJson)) errors['Settings.ServiceAccountJson'] = 'Paste or upload a service-account key JSON.';
      else if (!isValidServiceAccountJson(settings.ServiceAccountJson)) errors['Settings.ServiceAccountJson'] = 'Not a service-account key: expected JSON with client_email and private_key.';
    }
  }

  if (form.ApiFormat === 'VoyageAI' && !isBlank(settings.OutputDimension) && !(parseInt(settings.OutputDimension, 10) >= 1)) {
    errors['Settings.OutputDimension'] = 'Output dimension must be a positive number.';
  }

  return errors;
}

function blankToNull(value) {
  return isBlank(value) ? null : String(value).trim();
}

/** Build the ProviderSettings payload for the form's provider, or null when the provider has none. */
export function buildProviderSettingsPayload(apiFormat, settings) {
  const s = settings || {};
  switch (apiFormat) {
    case 'AzureOpenAI':
      return { AzureOpenAI: { ApiVersion: blankToNull(s.ApiVersion), AuthMode: s.AuthMode || 'ApiKey' } };
    case 'Anthropic':
      if (isBlank(s.AnthropicVersion) && isBlank(s.WorkspaceId)) return null;
      return { Anthropic: { AnthropicVersion: blankToNull(s.AnthropicVersion), WorkspaceId: blankToNull(s.WorkspaceId) } };
    case 'Bedrock': {
      const environment = s.CredentialSource === 'Environment';
      return {
        Bedrock: {
          Region: blankToNull(s.Region),
          CredentialSource: environment ? 'Environment' : 'Static',
          AccessKeyId: environment ? null : blankToNull(s.AccessKeyId),
          SecretAccessKey: environment ? null : blankToNull(s.SecretAccessKey),
          SessionToken: environment ? null : blankToNull(s.SessionToken),
        },
      };
    }
    case 'VertexAI': {
      const source = s.CredentialSource || 'ServiceAccountJson';
      return {
        VertexAI: {
          ProjectId: blankToNull(s.ProjectId),
          Region: blankToNull(s.Region),
          CredentialSource: source,
          ServiceAccountJson: source === 'ServiceAccountJson' ? blankToNull(s.ServiceAccountJson) : null,
        },
      };
    }
    case 'Cohere':
      return isBlank(s.InputType) ? null : { Cohere: { InputType: s.InputType } };
    case 'VoyageAI':
      if (isBlank(s.InputType) && isBlank(s.OutputDimension)) return null;
      return { VoyageAI: { InputType: blankToNull(s.InputType), OutputDimension: isBlank(s.OutputDimension) ? null : parseInt(s.OutputDimension, 10) } };
    default:
      return null;
  }
}

/** Build the connection part of a save payload (Endpoint, ApiFormat, ApiKey, Model, ProviderSettings). */
export function buildConnectionPayload(form) {
  const settings = form.ProviderSettings || {};
  const provider = getProvider(form.ApiFormat);
  let endpoint = normalizeEndpoint(form.ApiFormat, form.Endpoint, settings);
  if (provider.endpoint.mode === 'derived' && !form.EndpointOverride) endpoint = derivedEndpoint(form.ApiFormat, settings);
  return {
    ApiFormat: form.ApiFormat,
    Model: (form.Model || '').trim(),
    Endpoint: endpoint,
    ApiKey: apiKeyApplies(form.ApiFormat, settings) && !isBlank(form.ApiKey) ? form.ApiKey : null,
    ProviderSettings: buildProviderSettingsPayload(form.ApiFormat, settings),
  };
}

/** Load a stored endpoint's provider settings into form state (secrets stay masked until revealed). */
export function providerSettingsToForm(apiFormat, providerSettings) {
  const defaults = defaultProviderSettings(apiFormat);
  if (!defaults) return null;
  const stored = providerSettings?.[getProvider(apiFormat).settings] || {};
  const result = { ...defaults };
  for (const key of Object.keys(defaults)) {
    if (stored[key] !== undefined && stored[key] !== null) result[key] = String(stored[key]);
  }
  return result;
}

/** Whether a stored endpoint's endpoint URL differs from the one its region implies. */
export function hasEndpointOverride(apiFormat, endpoint, settings) {
  if (getProvider(apiFormat).endpoint.mode !== 'derived') return false;
  return !!endpoint && endpoint !== derivedEndpoint(apiFormat, settings);
}

/** Merge revealed (unmasked) secrets from the reveal API into form state. */
export function mergeRevealedSecrets(form, revealed) {
  const next = { ...form };
  if (isMaskedSecret(form.ApiKey) && revealed?.ApiKey) next.ApiKey = revealed.ApiKey;
  const section = getProvider(form.ApiFormat).settings;
  const stored = section ? revealed?.ProviderSettings?.[section] : null;
  if (stored && form.ProviderSettings) {
    const settings = { ...form.ProviderSettings };
    for (const key of ['SecretAccessKey', 'SessionToken', 'ServiceAccountJson']) {
      if (isMaskedSecret(settings[key]) && stored[key]) settings[key] = stored[key];
    }
    next.ProviderSettings = settings;
  }
  return next;
}

/** Whether the form holds any masked secret that the reveal API could fill in. */
export function hasMaskedSecrets(form) {
  if (isMaskedSecret(form.ApiKey)) return true;
  const s = form.ProviderSettings || {};
  return ['SecretAccessKey', 'SessionToken', 'ServiceAccountJson'].some(k => isMaskedSecret(s[k]));
}

/** Copy of a stored endpoint with every masked secret removed (used when secrets cannot be revealed). */
export function withoutMaskedSecrets(endpoint) {
  const copy = { ...endpoint };
  if (isMaskedSecret(copy.ApiKey)) copy.ApiKey = null;
  if (copy.ProviderSettings) {
    const settings = {};
    for (const [section, values] of Object.entries(copy.ProviderSettings)) {
      if (!values) { settings[section] = values; continue; }
      const cleaned = { ...values };
      for (const key of Object.keys(cleaned)) {
        if (isMaskedSecret(cleaned[key])) cleaned[key] = null;
      }
      settings[section] = cleaned;
    }
    copy.ProviderSettings = settings;
  }
  return copy;
}
