import React, { useEffect, useMemo, useState } from 'react';
import { useApp } from '../context/AppContext';
import { PartioApi } from '../utils/api';
import { copyToClipboard } from '../utils/clipboard';
import CopyableId from './CopyableId';
import './EndpointExplorerView.css';

function formatHeaders(headers) {
  if (!headers || typeof headers !== 'object') return null;
  const entries = Object.entries(headers);
  if (entries.length === 0) return null;
  return entries.map(([k, v]) => `${k}: ${v}`).join('\n');
}

function statusClass(code) {
  if (!code) return '';
  if (code >= 200 && code < 300) return 'status-2xx';
  if (code >= 300 && code < 400) return 'status-3xx';
  if (code >= 400 && code < 500) return 'status-4xx';
  return 'status-5xx';
}

function CollapsibleSection({ title, content, defaultExpanded = false }) {
  const [expanded, setExpanded] = useState(defaultExpanded);
  const [copied, setCopied] = useState(false);
  const [formatJson, setFormatJson] = useState(false);

  const isEmpty = !content || content === 'null' || content === 'undefined';
  let displayContent = isEmpty ? '(empty)' : content;

  if (formatJson && !isEmpty) {
    try {
      displayContent = JSON.stringify(JSON.parse(content), null, 2);
    } catch {
      displayContent = content;
    }
  }

  let isJson = false;
  if (!isEmpty) {
    try {
      JSON.parse(content);
      isJson = true;
    } catch {
      isJson = false;
    }
  }

  const handleCopy = async (e) => {
    e.stopPropagation();
    if (isEmpty) return;
    const success = await copyToClipboard(typeof content === 'string' ? content : JSON.stringify(content, null, 2));
    if (success) {
      setCopied(true);
      setTimeout(() => setCopied(false), 1500);
    }
  };

  return (
    <div className="collapsible-section">
      <div className="collapsible-header" onClick={() => setExpanded(!expanded)}>
        <div className="collapsible-header-left">
          <svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className={expanded ? 'expanded' : ''}>
            <polyline points="9 18 15 12 9 6" />
          </svg>
          <span className="collapsible-title">{title}</span>
          {isEmpty && <span className="collapsible-meta">(empty)</span>}
        </div>
        <div className="collapsible-actions">
          {isJson && (
            <button
              className={`format-btn ${formatJson ? 'active' : ''}`}
              onClick={(e) => { e.stopPropagation(); setFormatJson(!formatJson); }}
              title="Format JSON"
            >
              <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                <polyline points="16 18 22 12 16 6" /><polyline points="8 6 2 12 8 18" />
              </svg>
            </button>
          )}
          <button className={`copy-btn ${copied ? 'copied' : ''}`} onClick={handleCopy} title="Copy">
            {copied ? (
              <svg width="14" height="14" viewBox="0 0 20 20" fill="currentColor">
                <path fillRule="evenodd" d="M16.707 5.293a1 1 0 010 1.414l-8 8a1 1 0 01-1.414 0l-4-4a1 1 0 011.414-1.414L8 12.586l7.293-7.293a1 1 0 011.414 0z" clipRule="evenodd" />
              </svg>
            ) : (
              <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                <rect x="9" y="9" width="13" height="13" rx="2" ry="2" /><path d="M5 15H4a2 2 0 01-2-2V4a2 2 0 012-2h9a2 2 0 012 2v1" />
              </svg>
            )}
          </button>
        </div>
      </div>
      {expanded && (
        <div className="collapsible-content">
          <pre>{displayContent}</pre>
        </div>
      )}
    </div>
  );
}

function CallGroups({ title, calls }) {
  if (!calls || calls.length === 0) return null;
  return (
    <div className="detail-section">
      <h3>{title}</h3>
      {calls.map((call, idx) => (
        <div key={idx} className="embedding-call-group">
          <div className="embedding-call-header">
            <span className="embedding-call-index">#{idx + 1}</span>
            {call.StatusCode != null && (
              <span className={`http-status ${statusClass(call.StatusCode)}`}>{call.StatusCode}</span>
            )}
            <span className="embedding-call-method">{call.Method || 'POST'}</span>
            <code className="embedding-call-url">{call.Url || '-'}</code>
            {call.ResponseTimeMs != null && (
              <span className="embedding-call-time">{call.ResponseTimeMs} ms</span>
            )}
            {!call.Success && <span className="embedding-call-failed">FAILED</span>}
          </div>
          {call.Error && <div className="embedding-call-error">{call.Error}</div>}
          <CollapsibleSection title="Request Headers" content={formatHeaders(call.RequestHeaders)} />
          <CollapsibleSection title="Request Body" content={typeof call.RequestBody === 'string' ? call.RequestBody : call.RequestBody != null ? JSON.stringify(call.RequestBody) : null} />
          <CollapsibleSection title="Response Headers" content={formatHeaders(call.ResponseHeaders)} />
          <CollapsibleSection title="Response Body" content={typeof call.ResponseBody === 'string' ? call.ResponseBody : call.ResponseBody != null ? JSON.stringify(call.ResponseBody) : null} />
        </div>
      ))}
    </div>
  );
}

export default function EndpointExplorerView() {
  const { serverUrl, bearerToken } = useApp();
  const api = new PartioApi(serverUrl, bearerToken);
  const [mode, setMode] = useState('embedding');
  const [embeddingEndpoints, setEmbeddingEndpoints] = useState([]);
  const [completionEndpoints, setCompletionEndpoints] = useState([]);
  const [loadingEndpoints, setLoadingEndpoints] = useState(true);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState(null);
  const [response, setResponse] = useState(null);
  const [embeddingForm, setEmbeddingForm] = useState({
    EndpointId: '',
    Input: 'Partio explorer test input',
    L2Normalization: false
  });
  const [completionForm, setCompletionForm] = useState({
    EndpointId: '',
    Prompt: "Explain artificial intelligence like I'm five in a single paragraph",
    SystemPrompt: 'You are a helpful, friendly AI assistant.',
    MaxTokens: 512,
    TimeoutMs: 60000
  });

  useEffect(() => {
    let cancelled = false;
    (async () => {
      try {
        const [embRes, compRes] = await Promise.all([
          api.enumerateEndpoints({ MaxResults: 1000 }),
          api.enumerateCompletionEndpoints({ MaxResults: 1000 })
        ]);
        if (cancelled) return;
        const emb = (embRes?.Data || []).filter(endpoint => endpoint.Active !== false);
        const comp = (compRes?.Data || []).filter(endpoint => endpoint.Active !== false);
        setEmbeddingEndpoints(emb);
        setCompletionEndpoints(comp);
        setEmbeddingForm(prev => ({
          ...prev,
          EndpointId: prev.EndpointId || emb[0]?.Id || ''
        }));
        setCompletionForm(prev => ({
          ...prev,
          EndpointId: prev.EndpointId || comp[0]?.Id || ''
        }));
      } catch (err) {
        if (!cancelled) {
          setEmbeddingEndpoints([]);
          setCompletionEndpoints([]);
          setError(err.message);
        }
      } finally {
        if (!cancelled) setLoadingEndpoints(false);
      }
    })();
    return () => { cancelled = true; };
  }, [serverUrl, bearerToken]);

  const activeEmbedding = useMemo(
    () => embeddingEndpoints.find(endpoint => endpoint.Id === embeddingForm.EndpointId) || null,
    [embeddingEndpoints, embeddingForm.EndpointId]
  );

  const activeCompletion = useMemo(
    () => completionEndpoints.find(endpoint => endpoint.Id === completionForm.EndpointId) || null,
    [completionEndpoints, completionForm.EndpointId]
  );

  const handleRun = async () => {
    setSubmitting(true);
    setError(null);
    setResponse(null);
    try {
      if (mode === 'embedding') {
        const result = await api.exploreEmbeddingEndpoint(embeddingForm);
        setResponse(result);
      } else {
        const result = await api.exploreCompletionEndpoint({
          ...completionForm,
          MaxTokens: parseInt(completionForm.MaxTokens, 10) || 512,
          TimeoutMs: parseInt(completionForm.TimeoutMs, 10) || 60000
        });
        setResponse(result);
      }
    } catch (err) {
      setError(err.message);
    } finally {
      setSubmitting(false);
    }
  };

  const resultBody = response ? JSON.stringify(response, null, 2) : null;
  const embeddingPreview = response?.Embedding ? response.Embedding.slice(0, 16) : [];

  return (
    <div className="endpoint-explorer">
      <div className="header-row">
        <div className="page-title-block">
          <h2>Endpoint Explorer</h2>
          <p className="view-subtitle">Run a request through Partio itself to validate a configured embedding or inference endpoint and inspect the upstream exchange.</p>
        </div>
      </div>

      <div className="endpoint-explorer-layout">
        <div className="endpoint-explorer-form card">
          <div className="explorer-mode-toggle">
            <button className={mode === 'embedding' ? 'active' : ''} onClick={() => { setMode('embedding'); setResponse(null); setError(null); }}>Embedding</button>
            <button className={mode === 'completion' ? 'active' : ''} onClick={() => { setMode('completion'); setResponse(null); setError(null); }}>Inference</button>
          </div>

          {mode === 'embedding' ? (
            <>
              <div className="form-group">
                <label>Embedding Endpoint</label>
                <select
                  value={embeddingForm.EndpointId}
                  onChange={e => setEmbeddingForm(prev => ({ ...prev, EndpointId: e.target.value }))}
                  disabled={loadingEndpoints}
                >
                  <option value="">{loadingEndpoints ? 'Loading...' : '-- Select endpoint --'}</option>
                  {embeddingEndpoints.map(endpoint => (
                    <option key={endpoint.Id} value={endpoint.Id}>
                      {endpoint.Name || endpoint.Model} ({endpoint.ApiFormat})
                    </option>
                  ))}
                </select>
              </div>
              {activeEmbedding && (
                <div className="explorer-endpoint-meta">
                  <div><span>Model</span><strong>{activeEmbedding.Model}</strong></div>
                  <div><span>Provider</span><strong>{activeEmbedding.ApiFormat}</strong></div>
                  <div><span>Base URL</span><code>{activeEmbedding.Endpoint}</code></div>
                </div>
              )}
              <div className="form-group">
                <label>Input Text</label>
                <textarea
                  rows={10}
                  value={embeddingForm.Input}
                  onChange={e => setEmbeddingForm(prev => ({ ...prev, Input: e.target.value }))}
                  placeholder="Enter text to send through the selected embedding endpoint..."
                />
              </div>
              <div className="checkbox-group">
                <input
                  id="explorer-l2"
                  type="checkbox"
                  checked={embeddingForm.L2Normalization}
                  onChange={e => setEmbeddingForm(prev => ({ ...prev, L2Normalization: e.target.checked }))}
                />
                <label htmlFor="explorer-l2">Apply L2 normalization after embedding</label>
              </div>
            </>
          ) : (
            <>
              <div className="form-group">
                <label>Inference Endpoint</label>
                <select
                  value={completionForm.EndpointId}
                  onChange={e => setCompletionForm(prev => ({ ...prev, EndpointId: e.target.value }))}
                  disabled={loadingEndpoints}
                >
                  <option value="">{loadingEndpoints ? 'Loading...' : '-- Select endpoint --'}</option>
                  {completionEndpoints.map(endpoint => (
                    <option key={endpoint.Id} value={endpoint.Id}>
                      {endpoint.Name || endpoint.Model} ({endpoint.ApiFormat})
                    </option>
                  ))}
                </select>
              </div>
              {activeCompletion && (
                <div className="explorer-endpoint-meta">
                  <div><span>Model</span><strong>{activeCompletion.Model}</strong></div>
                  <div><span>Provider</span><strong>{activeCompletion.ApiFormat}</strong></div>
                  <div><span>Base URL</span><code>{activeCompletion.Endpoint}</code></div>
                </div>
              )}
              <div className="form-group">
                <label>System Prompt</label>
                <textarea
                  rows={4}
                  value={completionForm.SystemPrompt}
                  onChange={e => setCompletionForm(prev => ({ ...prev, SystemPrompt: e.target.value }))}
                  placeholder="Optional system prompt..."
                />
              </div>
              <div className="form-group">
                <label>Prompt</label>
                <textarea
                  rows={10}
                  value={completionForm.Prompt}
                  onChange={e => setCompletionForm(prev => ({ ...prev, Prompt: e.target.value }))}
                  placeholder="Enter the prompt to send through the selected inference endpoint..."
                />
              </div>
              <div className="form-row">
                <div className="form-group">
                  <label>Max Tokens</label>
                  <input
                    type="number"
                    min="1"
                    value={completionForm.MaxTokens}
                    onChange={e => setCompletionForm(prev => ({ ...prev, MaxTokens: e.target.value }))}
                  />
                </div>
                <div className="form-group">
                  <label>Timeout (ms)</label>
                  <input
                    type="number"
                    min="1000"
                    step="1000"
                    value={completionForm.TimeoutMs}
                    onChange={e => setCompletionForm(prev => ({ ...prev, TimeoutMs: e.target.value }))}
                  />
                </div>
              </div>
            </>
          )}

          <button
            className="primary explorer-run-btn"
            onClick={handleRun}
            disabled={submitting || loadingEndpoints || (mode === 'embedding' ? !embeddingForm.EndpointId || !embeddingForm.Input.trim() : !completionForm.EndpointId || !completionForm.Prompt.trim())}
          >
            {submitting ? 'Running...' : 'Run Through Partio'}
          </button>
        </div>

        <div className="endpoint-explorer-results">
          {error && <div className="card explorer-error">Error: {error}</div>}
          {!error && !response && (
            <div className="card explorer-empty">
              Pick an endpoint, provide sample input, and run the request through Partio to inspect the end-to-end behavior.
            </div>
          )}
          {response && (
            <div className="card explorer-response-card">
              <div className="detail-section">
                <h3>Overview</h3>
                <div className="detail-grid">
                  <div className="detail-item">
                    <label>Result</label>
                    <span className={response.Success ? 'explorer-success' : 'explorer-failure'}>
                      {response.Success ? 'Success' : 'Failed'}
                    </span>
                  </div>
                  <div className="detail-item">
                    <label>Status</label>
                    <span className={`http-status ${statusClass(response.StatusCode)}`}>{response.StatusCode}</span>
                  </div>
                  <div className="detail-item">
                    <label>Response Time</label>
                    <span>{response.ResponseTimeMs != null ? `${response.ResponseTimeMs} ms` : '-'}</span>
                  </div>
                  <div className="detail-item">
                    <label>Endpoint ID</label>
                    <span>{response.EndpointId ? <CopyableId value={response.EndpointId} /> : '-'}</span>
                  </div>
                  <div className="detail-item">
                    <label>Model</label>
                    <span>{response.Model || '-'}</span>
                  </div>
                  <div className="detail-item">
                    <label>History Entry</label>
                    <span>{response.RequestHistoryId ? <CopyableId value={response.RequestHistoryId} /> : 'Not recorded'}</span>
                  </div>
                </div>
                {response.Error && <div className="explorer-response-error">{response.Error}</div>}
              </div>

              {mode === 'embedding' ? (
                <div className="detail-section">
                  <h3>Embedding Result</h3>
                  {response.Success ? (
                    <>
                      <div className="detail-grid">
                        <div className="detail-item">
                          <label>Dimensions</label>
                          <span>{response.Dimensions || 0}</span>
                        </div>
                        <div className="detail-item detail-item-wide">
                          <label>Vector Preview</label>
                          <code>{embeddingPreview.length > 0 ? `[${embeddingPreview.map(value => Number(value).toFixed(6)).join(', ')}${response.Dimensions > embeddingPreview.length ? ', ...' : ''}]` : '(empty)'}</code>
                        </div>
                      </div>
                      <CollapsibleSection title="Full Embedding Vector" content={response.Embedding ? JSON.stringify(response.Embedding) : null} />
                    </>
                  ) : (
                    <div className="detail-empty">No embedding vector was produced.</div>
                  )}
                </div>
              ) : (
                <div className="detail-section">
                  <h3>Inference Result</h3>
                  {response.Success ? (
                    <div className="explorer-output">{response.Output || '(empty)'}</div>
                  ) : (
                    <div className="detail-empty">No model output was produced.</div>
                  )}
                </div>
              )}

              <div className="detail-section">
                <h3>Explorer Response</h3>
                <CollapsibleSection title="Response JSON" content={resultBody} defaultExpanded />
              </div>

              <CallGroups
                title={mode === 'embedding' ? 'Upstream Embedding Calls' : 'Upstream Inference Calls'}
                calls={mode === 'embedding' ? response.EmbeddingCalls : response.CompletionCalls}
              />
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
