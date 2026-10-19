import React, { useState } from 'react';
import FormFieldLabel from './FormFieldLabel';
import SecretInput from './SecretInput';
import {
  ANTHROPIC_DEFAULT_VERSION,
  AUTH_MODES,
  AWS_REGIONS,
  AZURE_DEFAULT_API_VERSION,
  GCP_REGIONS,
  apiKeyApplies,
  apiKeyLabel,
  apiKeyRequired,
  applyAuthChange,
  applySettingsChange,
  derivedEndpoint,
  describeAuth,
  getHealthCheckDefaults,
  getProvider,
  supportsSelectableAuth,
} from '../utils/providers';
import './ProviderForm.css';

function Field({ id, label, tooltip, required = false, error, hint, children }) {
  return (
    <div className={`form-group provider-field${error ? ' has-error' : ''}`}>
      <FormFieldLabel htmlFor={id} text={required ? `${label} *` : label} tooltip={tooltip || label} />
      {children}
      {error ? <div className="field-error" role="alert">{error}</div> : hint ? <div className="field-hint">{hint}</div> : null}
    </div>
  );
}

function Segmented({ name, value, options, onChange }) {
  return (
    <div className="segmented" role="radiogroup" aria-label={name}>
      {options.map(option => (
        <button
          type="button"
          key={option.value}
          role="radio"
          aria-checked={value === option.value}
          className={value === option.value ? 'active' : ''}
          onClick={() => onChange(option.value)}
          title={option.tooltip}
        >
          {option.label}
        </button>
      ))}
    </div>
  );
}

function RegionInput({ id, value, onChange, regions, invalid }) {
  const listId = id + '-options';
  return (
    <>
      <input id={id} list={listId} className={invalid ? 'invalid' : undefined} value={value || ''} onChange={e => onChange(e.target.value.trim())} placeholder={regions[0]} />
      <datalist id={listId}>
        {regions.map(r => <option key={r} value={r} />)}
      </datalist>
    </>
  );
}

/**
 * Provider-specific connection fields for an endpoint form: model (labelled per provider), endpoint URL
 * (fixed, required, defaulted, or derived from the region), credentials (API key, Azure auth mode, AWS
 * credentials, or Google credentials), and provider options. Field visibility, labels, and required markers
 * come from the provider catalog in utils/providers.js.
 */
export default function ProviderConnectionFields({
  kind,
  form,
  setForm,
  errors = {},
  healthFieldsEdited,
  editing = false,
  onReveal,
  revealing = false,
}) {
  const [showAdvanced, setShowAdvanced] = useState(!!form.EndpointOverride);
  const provider = getProvider(form.ApiFormat);
  const settings = form.ProviderSettings || {};
  const fieldId = (name) => `ep-${kind}-${name}`;

  const setField = (field, value) => {
    const next = { ...form, [field]: value };
    if (field === 'Endpoint' && !healthFieldsEdited) {
      next.HealthCheckUrl = getHealthCheckDefaults(form.ApiFormat, value, settings, form.Auth).HealthCheckUrl;
    }
    setForm(next);
  };

  const setSetting = (field, value) => setForm(applySettingsChange(form, field, value, healthFieldsEdited));
  const setAuth = (field, value) => setForm(applyAuthChange(form, field, value, healthFieldsEdited));
  const selectable = supportsSelectableAuth(form.ApiFormat);
  const auth = form.Auth || { Mode: 'None' };

  const setOverride = (enabled) => {
    const next = { ...form, EndpointOverride: enabled };
    if (!enabled) next.Endpoint = derivedEndpoint(form.ApiFormat, settings);
    if (!healthFieldsEdited) next.HealthCheckUrl = getHealthCheckDefaults(form.ApiFormat, next.Endpoint, settings, form.Auth).HealthCheckUrl;
    setForm(next);
  };

  const revealProps = editing && onReveal ? { onReveal, revealing } : {};

  // ----- Endpoint URL -----
  let endpointField = null;
  const endpointHelp = provider.endpoint.help;
  if (provider.endpoint.mode === 'fixed') {
    endpointField = (
      <Field id={fieldId('endpoint')} label={provider.endpoint.label} tooltip={endpointHelp} hint={endpointHelp}>
        <div className="provider-readonly" id={fieldId('endpoint')}>{provider.endpoint.defaultValue}</div>
      </Field>
    );
  } else if (provider.endpoint.mode === 'derived') {
    endpointField = (
      <div className="provider-derived">
        <Field id={fieldId('endpoint')} label={provider.endpoint.label} tooltip={endpointHelp} error={errors.Endpoint}
          hint={form.EndpointOverride ? 'Custom endpoint. Requests are still signed for the region above.' : 'Derived from the region.'}>
          {form.EndpointOverride ? (
            <input id={fieldId('endpoint')} className={errors.Endpoint ? 'invalid' : undefined} value={form.Endpoint || ''} onChange={e => setField('Endpoint', e.target.value)} placeholder={provider.endpoint.placeholder} />
          ) : (
            <div className="provider-readonly" id={fieldId('endpoint')}>{derivedEndpoint(form.ApiFormat, settings) || 'Choose a region'}</div>
          )}
        </Field>
      </div>
    );
  } else {
    const isDefault = provider.endpoint.mode === 'default';
    endpointField = (
      <Field id={fieldId('endpoint')} label={provider.endpoint.label} tooltip={endpointHelp} required={!isDefault} error={errors.Endpoint}
        hint={isDefault ? `${endpointHelp} Default: ${provider.endpoint.defaultValue}` : endpointHelp}>
        <input id={fieldId('endpoint')} className={errors.Endpoint ? 'invalid' : undefined} value={form.Endpoint || ''} onChange={e => setField('Endpoint', e.target.value)} placeholder={provider.endpoint.placeholder} />
      </Field>
    );
  }

  // ----- API key -----
  const keyLabel = apiKeyLabel(form.ApiFormat, settings, form.Auth);
  const keyRequired = apiKeyRequired(form.ApiFormat, settings, form.Auth);
  let keyHelp = provider.apiKey.help;
  if (selectable) keyHelp = describeAuth(form.ApiFormat, settings, auth) + ' Applied to inference, health checks, and the proxy.';
  else if (form.ApiFormat === 'AzureOpenAI' && settings.AuthMode === 'BearerToken')
    keyHelp = 'Entra ID (Azure AD) access token for the Cognitive Services scope. Tokens expire, so prefer API keys for long-lived endpoints.';
  const apiKeyField = apiKeyApplies(form.ApiFormat, settings, form.Auth) ? (
    <Field id={fieldId('apikey')} label={keyLabel} tooltip={keyHelp} required={keyRequired} error={errors.ApiKey}
      hint={keyHelp + (keyRequired || selectable ? '' : ' Optional.')}>
      <SecretInput id={fieldId('apikey')} ariaLabel={keyLabel} value={form.ApiKey} onChange={v => setField('ApiKey', v)}
        placeholder={keyRequired ? 'Required' : 'Optional'} invalid={!!errors.ApiKey} {...revealProps} />
    </Field>
  ) : null;

  return (
    <div className="provider-connection">
      <div className="endpoint-form-row">
        <Field id={fieldId('model')} label={provider.model.label} tooltip={provider.model.help} required error={errors.Model} hint={provider.model.help}>
          <input id={fieldId('model')} className={errors.Model ? 'invalid' : undefined} value={form.Model || ''} onChange={e => setField('Model', e.target.value)} placeholder={provider.model.placeholder} />
        </Field>
        {form.ApiFormat === 'AzureOpenAI' ? (
          <Field id={fieldId('apiversion')} label="API Version" tooltip="Azure OpenAI api-version query value sent with every request." hint={`Leave blank for ${AZURE_DEFAULT_API_VERSION}.`}>
            <input id={fieldId('apiversion')} value={settings.ApiVersion || ''} onChange={e => setSetting('ApiVersion', e.target.value)} placeholder={AZURE_DEFAULT_API_VERSION} />
          </Field>
        ) : <div />}
      </div>

      {provider.endpoint.mode !== 'derived' && endpointField}

      {/* ----- Credentials ----- */}
      <div className="provider-subsection">
        <div className="provider-subsection-header">Credentials</div>

        {selectable ? (
          <>
            <Field id={fieldId('authmode')} label="Authentication" tooltip="How Partio sends the credential. Self-hosted servers and gateways vary, so pick what yours expects.">
              <Segmented name="Authentication" value={auth.Mode || 'None'} onChange={v => setAuth('Mode', v)} options={AUTH_MODES} />
            </Field>
            {auth.Mode === 'Header' && (
              <Field id={fieldId('authheader')} label="Header Name" tooltip="Name of the header that carries the credential." required error={errors['Auth.HeaderName']}>
                <input id={fieldId('authheader')} className={errors['Auth.HeaderName'] ? 'invalid' : undefined} value={auth.HeaderName || ''} onChange={e => setAuth('HeaderName', e.target.value)} placeholder="X-API-Key" />
              </Field>
            )}
            {auth.Mode === 'QueryParameter' && (
              <Field id={fieldId('authquery')} label="Parameter Name" tooltip="Query-string parameter that carries the credential." required error={errors['Auth.QueryParameterName']}
                hint="The value is added to every request URL. Partio does not record it in request history.">
                <input id={fieldId('authquery')} className={errors['Auth.QueryParameterName'] ? 'invalid' : undefined} value={auth.QueryParameterName || ''} onChange={e => setAuth('QueryParameterName', e.target.value)} placeholder="api_key" />
              </Field>
            )}
            {auth.Mode === 'Basic' && (
              <Field id={fieldId('authuser')} label="Username" tooltip="User name for HTTP basic authentication." required error={errors['Auth.Username']}>
                <input id={fieldId('authuser')} className={errors['Auth.Username'] ? 'invalid' : undefined} value={auth.Username || ''} onChange={e => setAuth('Username', e.target.value)} placeholder="partio" autoComplete="off" />
              </Field>
            )}
            {auth.Mode === 'None' && (
              <div className="provider-note">No credential is sent. Choose another mode if the server or a gateway in front of it requires one.</div>
            )}
          </>
        ) : (
          <Field id={fieldId('authscheme')} label="Authentication" tooltip={`${provider.label} uses one documented authentication scheme.`}>
            <div className="provider-readonly" id={fieldId('authscheme')}>{describeAuth(form.ApiFormat, settings, null)}</div>
          </Field>
        )}

        {form.ApiFormat === 'AzureOpenAI' && (
          <Field id={fieldId('authmode')} label="Authentication" tooltip="How Partio authenticates to Azure OpenAI.">
            <Segmented name="Authentication" value={settings.AuthMode || 'ApiKey'} onChange={v => setSetting('AuthMode', v)} options={[
              { value: 'ApiKey', label: 'API key', tooltip: 'Sent in the api-key header.' },
              { value: 'BearerToken', label: 'Entra ID token', tooltip: 'Sent as a bearer token.' },
            ]} />
          </Field>
        )}

        {form.ApiFormat === 'Bedrock' && (
          <>
            <div className="endpoint-form-row">
              <Field id={fieldId('region')} label="Region" tooltip="AWS region that hosts the model. Selects the runtime host and the signing region." required error={errors['Settings.Region']}>
                <RegionInput id={fieldId('region')} value={settings.Region} onChange={v => setSetting('Region', v)} regions={AWS_REGIONS} invalid={!!errors['Settings.Region']} />
              </Field>
              <Field id={fieldId('credsource')} label="Credential Source" tooltip="Where Partio gets the AWS credentials used to sign requests.">
                <Segmented name="Credential Source" value={settings.CredentialSource || 'Static'} onChange={v => setSetting('CredentialSource', v)} options={[
                  { value: 'Static', label: 'Access keys', tooltip: 'Stored on this endpoint.' },
                  { value: 'Environment', label: 'Server environment', tooltip: 'AWS_ACCESS_KEY_ID, AWS_SECRET_ACCESS_KEY, and AWS_SESSION_TOKEN on the Partio server.' },
                ]} />
              </Field>
            </div>
            {(settings.CredentialSource || 'Static') === 'Static' ? (
              <>
                <div className="endpoint-form-row">
                  <Field id={fieldId('akid')} label="Access Key ID" tooltip="IAM access key ID (AKIA... or ASIA... for temporary credentials)." required error={errors['Settings.AccessKeyId']}>
                    <input id={fieldId('akid')} className={errors['Settings.AccessKeyId'] ? 'invalid' : undefined} value={settings.AccessKeyId || ''} onChange={e => setSetting('AccessKeyId', e.target.value.trim())} placeholder="AKIA..." autoComplete="off" />
                  </Field>
                  <Field id={fieldId('secret')} label="Secret Access Key" tooltip="IAM secret access key. Never sent upstream; used only to sign requests." required error={errors['Settings.SecretAccessKey']}>
                    <SecretInput id={fieldId('secret')} ariaLabel="Secret Access Key" value={settings.SecretAccessKey} onChange={v => setSetting('SecretAccessKey', v)} placeholder="Required" invalid={!!errors['Settings.SecretAccessKey']} {...revealProps} />
                  </Field>
                </div>
                <Field id={fieldId('session')} label="Session Token" tooltip="Only for temporary credentials from STS or SSO." hint="Optional. Temporary credentials expire; use long-lived keys or the server environment for permanent endpoints.">
                  <SecretInput id={fieldId('session')} ariaLabel="Session Token" value={settings.SessionToken} onChange={v => setSetting('SessionToken', v)} placeholder="Optional" {...revealProps} />
                </Field>
              </>
            ) : (
              <div className="provider-note">Partio reads <code>AWS_ACCESS_KEY_ID</code>, <code>AWS_SECRET_ACCESS_KEY</code>, and optionally <code>AWS_SESSION_TOKEN</code> from its own environment on every request, so rotated credentials are picked up automatically.</div>
            )}
          </>
        )}

        {form.ApiFormat === 'VertexAI' && (
          <>
            <div className="endpoint-form-row">
              <Field id={fieldId('project')} label="Project ID" tooltip="Google Cloud project ID that has the Vertex AI API enabled." required error={errors['Settings.ProjectId']}>
                <input id={fieldId('project')} className={errors['Settings.ProjectId'] ? 'invalid' : undefined} value={settings.ProjectId || ''} onChange={e => setSetting('ProjectId', e.target.value.trim())} placeholder="my-gcp-project" />
              </Field>
              <Field id={fieldId('region')} label="Region" tooltip="Vertex AI location, for example us-central1. Selects the regional API host." required error={errors['Settings.Region']}>
                <RegionInput id={fieldId('region')} value={settings.Region} onChange={v => setSetting('Region', v)} regions={GCP_REGIONS} invalid={!!errors['Settings.Region']} />
              </Field>
            </div>
            <Field id={fieldId('gcpsource')} label="Credential Source" tooltip="Where Partio gets Google OAuth tokens.">
              <Segmented name="Credential Source" value={settings.CredentialSource || 'ServiceAccountJson'} onChange={v => setSetting('CredentialSource', v)} options={[
                { value: 'ServiceAccountJson', label: 'Service account key', tooltip: 'Tokens are minted and refreshed automatically.' },
                { value: 'ApplicationDefault', label: 'Application Default', tooltip: 'GOOGLE_APPLICATION_CREDENTIALS or the metadata server where Partio runs.' },
                { value: 'AccessToken', label: 'Access token', tooltip: 'A static token that expires after about an hour.' },
              ]} />
            </Field>
            {(settings.CredentialSource || 'ServiceAccountJson') === 'ServiceAccountJson' && (
              <Field id={fieldId('sajson')} label="Service Account Key JSON" tooltip="The JSON key file downloaded from IAM > Service Accounts > Keys. The account needs the Vertex AI User role." required error={errors['Settings.ServiceAccountJson']}
                hint="Paste the key or load the downloaded .json file.">
                <SecretInput id={fieldId('sajson')} ariaLabel="Service Account Key JSON" multiline fileAccept=".json,application/json" value={settings.ServiceAccountJson} onChange={v => setSetting('ServiceAccountJson', v)}
                  placeholder='{"type": "service_account", "client_email": "...", "private_key": "..."}' invalid={!!errors['Settings.ServiceAccountJson']} {...revealProps} />
              </Field>
            )}
            {settings.CredentialSource === 'ApplicationDefault' && (
              <div className="provider-note">Partio uses the service account of the machine it runs on (GCE, GKE, Cloud Run) or the key file named by <code>GOOGLE_APPLICATION_CREDENTIALS</code>.</div>
            )}
          </>
        )}

        {apiKeyField}
      </div>

      {provider.endpoint.mode === 'derived' && (
        <div className="provider-subsection">
          <button type="button" className="provider-advanced-toggle" onClick={() => setShowAdvanced(!showAdvanced)} aria-expanded={showAdvanced}>
            {showAdvanced ? '▾' : '▸'} Endpoint
          </button>
          {showAdvanced ? (
            <>
              <label className="checkbox-label provider-override">
                <input type="checkbox" checked={!!form.EndpointOverride} onChange={e => setOverride(e.target.checked)} />
                {' '}Override the endpoint URL
              </label>
              {endpointField}
            </>
          ) : (
            <div className="field-hint">{derivedEndpoint(form.ApiFormat, settings) || 'Choose a region'}{form.EndpointOverride ? ' (overridden)' : ''}</div>
          )}
        </div>
      )}

      {/* ----- Provider options ----- */}
      {form.ApiFormat === 'Cohere' && kind === 'embedding' && (
        <div className="provider-subsection">
          <div className="provider-subsection-header">Embedding Options</div>
          <Field id={fieldId('inputtype')} label="Input Type" tooltip="Cohere embed input_type. Use search_document when indexing content and search_query for queries.">
            <select id={fieldId('inputtype')} value={settings.InputType || ''} onChange={e => setSetting('InputType', e.target.value)}>
              <option value="">search_document (default)</option>
              <option value="search_query">search_query</option>
              <option value="classification">classification</option>
              <option value="clustering">clustering</option>
            </select>
          </Field>
        </div>
      )}

      {form.ApiFormat === 'VoyageAI' && (
        <div className="provider-subsection">
          <div className="provider-subsection-header">Embedding Options</div>
          <div className="endpoint-form-row">
            <Field id={fieldId('inputtype')} label="Input Type" tooltip="Voyage input_type. Use document when indexing content and query for queries.">
              <select id={fieldId('inputtype')} value={settings.InputType || ''} onChange={e => setSetting('InputType', e.target.value)}>
                <option value="">None (raw input)</option>
                <option value="document">document</option>
                <option value="query">query</option>
              </select>
            </Field>
            <Field id={fieldId('outdim')} label="Output Dimension" tooltip="Matryoshka output dimension for models that support it." error={errors['Settings.OutputDimension']}>
              <select id={fieldId('outdim')} value={settings.OutputDimension || ''} onChange={e => setSetting('OutputDimension', e.target.value)}>
                <option value="">Model default</option>
                {['256', '512', '1024', '2048'].map(d => <option key={d} value={d}>{d}</option>)}
              </select>
            </Field>
          </div>
        </div>
      )}

      {form.ApiFormat === 'Anthropic' && (
        <div className="provider-subsection">
          <button type="button" className="provider-advanced-toggle" onClick={() => setShowAdvanced(!showAdvanced)} aria-expanded={showAdvanced}>
            {showAdvanced ? '▾' : '▸'} Advanced
          </button>
          {showAdvanced && (
            <div className="endpoint-form-row">
              <Field id={fieldId('anthver')} label="anthropic-version" tooltip="Anthropic API version header." hint={`Leave blank for ${ANTHROPIC_DEFAULT_VERSION}.`}>
                <input id={fieldId('anthver')} value={settings.AnthropicVersion || ''} onChange={e => setSetting('AnthropicVersion', e.target.value)} placeholder={ANTHROPIC_DEFAULT_VERSION} />
              </Field>
              <Field id={fieldId('workspace')} label="Workspace ID" tooltip="anthropic-workspace-id header, required for identity-linked API keys." hint="Optional.">
                <input id={fieldId('workspace')} value={settings.WorkspaceId || ''} onChange={e => setSetting('WorkspaceId', e.target.value)} placeholder="wrkspc_..." />
              </Field>
            </div>
          )}
        </div>
      )}

      {kind === 'completion' && !provider.proxy && (
        <div className="provider-note">{provider.label} requests are signed per request, so this endpoint works with completion, summarization, and validation but not with the transparent proxy or Chat.</div>
      )}
    </div>
  );
}
