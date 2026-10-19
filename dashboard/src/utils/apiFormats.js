// Provider (ApiFormat) rules shared by the embedding and completion endpoint views.
// Mirrors Partio.Core.ThirdParty.ProviderDefaults on the server.

export const OLLAMA_CLOUD_ENDPOINT = 'https://ollama.com';
export const XAI_ENDPOINT = 'https://api.x.ai';

export const API_FORMAT_OPTIONS = [
  { value: 'Ollama', label: 'Ollama' },
  { value: 'OllamaCloud', label: 'Ollama Cloud' },
  { value: 'OpenAI', label: 'OpenAI' },
  { value: 'Gemini', label: 'Gemini' },
  { value: 'vLLM', label: 'vLLM' },
  { value: 'xAI', label: 'xAI (Grok)' },
];

export const API_FORMAT_TOOLTIP =
  'Protocol format. Ollama for local Ollama servers, Ollama Cloud for ollama.com, OpenAI and vLLM for OpenAI-compatible APIs, Gemini for Google Gemini, xAI for Grok.';

export const API_KEY_TOOLTIP_SUFFIX =
  'Required for Ollama Cloud, xAI, OpenAI, Gemini, and most vLLM deployments; optional for local Ollama.';

/** Whether the provider rejects endpoints without an API key (enforced by the server). */
export function requiresApiKey(apiFormat) {
  return apiFormat === 'OllamaCloud' || apiFormat === 'xAI';
}

/** Whether the provider's host is fixed, so the endpoint URL is not editable. */
export function isFixedEndpoint(apiFormat) {
  return apiFormat === 'OllamaCloud';
}

/** Whether the provider speaks the native Ollama API (local Ollama or Ollama Cloud). */
export function isOllamaDialect(apiFormat) {
  return apiFormat === 'Ollama' || apiFormat === 'OllamaCloud';
}

export function getHealthCheckDefaults(apiFormat, endpoint) {
  const baseUrl = (endpoint || '').replace(/\/+$/, '');
  if (apiFormat === 'Ollama') {
    return {
      HealthCheckUrl: baseUrl ? baseUrl + '/api/tags' : '',
      HealthCheckMethod: 'GET',
      HealthCheckIntervalMs: 5000,
      HealthCheckTimeoutMs: 2000,
      HealthCheckExpectedStatusCode: 200,
      HealthyThreshold: 2,
      UnhealthyThreshold: 2,
      HealthCheckUseAuth: false,
    };
  }
  if (apiFormat === 'OllamaCloud') {
    return {
      HealthCheckUrl: OLLAMA_CLOUD_ENDPOINT + '/api/tags',
      HealthCheckMethod: 'GET',
      HealthCheckIntervalMs: 15000,
      HealthCheckTimeoutMs: 5000,
      HealthCheckExpectedStatusCode: 200,
      HealthyThreshold: 2,
      UnhealthyThreshold: 2,
      HealthCheckUseAuth: true,
    };
  }
  if (apiFormat === 'Gemini') {
    return {
      HealthCheckUrl: baseUrl ? baseUrl + '/v1beta/models' : '',
      HealthCheckMethod: 'GET',
      HealthCheckIntervalMs: 15000,
      HealthCheckTimeoutMs: 5000,
      HealthCheckExpectedStatusCode: 200,
      HealthyThreshold: 2,
      UnhealthyThreshold: 2,
      HealthCheckUseAuth: true,
    };
  }
  // OpenAI, vLLM, and xAI list models at /v1/models. xAI accepts a /v1 base, so avoid /v1/v1/models.
  const modelsBase = apiFormat === 'xAI' ? baseUrl.replace(/\/v1$/i, '') : baseUrl;
  return {
    HealthCheckUrl: modelsBase ? modelsBase + '/v1/models' : '',
    HealthCheckMethod: 'GET',
    HealthCheckIntervalMs: 15000,
    HealthCheckTimeoutMs: 5000,
    HealthCheckExpectedStatusCode: 200,
    HealthyThreshold: 2,
    UnhealthyThreshold: 2,
    HealthCheckUseAuth: true,
  };
}

/**
 * Switch a form to a new ApiFormat, replacing the model and endpoint only when they are still the
 * previous format's defaults. Fixed-host formats always take their fixed endpoint.
 */
export function applyApiFormatDefaults(currentForm, newFormat, healthFieldsEdited, getApiFormatDefaults) {
  const previousDefaults = getApiFormatDefaults(currentForm.ApiFormat);
  const newDefaults = getApiFormatDefaults(newFormat);
  const nextForm = { ...currentForm, ApiFormat: newFormat };

  if (!currentForm.Model || currentForm.Model === previousDefaults.Model) {
    nextForm.Model = newDefaults.Model;
  }

  if (isFixedEndpoint(newFormat) || !currentForm.Endpoint || currentForm.Endpoint === previousDefaults.Endpoint) {
    nextForm.Endpoint = newDefaults.Endpoint;
  }

  if (!healthFieldsEdited) {
    Object.assign(nextForm, getHealthCheckDefaults(newFormat, nextForm.Endpoint));
  }

  return nextForm;
}
