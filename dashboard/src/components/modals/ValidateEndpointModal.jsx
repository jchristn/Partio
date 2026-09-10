import { useEffect, useRef, useState } from 'react';
import Modal from '../Modal';
import CopyableId from '../CopyableId';
import './ValidateEndpointModal.css';

const defaultEmbeddingInput = 'Partio endpoint validation probe';
const defaultCompletionPrompt = 'Reply with the single word: OK';

function formatMs(value) {
  if (value == null) return 'N/A';
  return `${Number(value).toFixed(2)} ms`;
}

function statusClass(code) {
  if (!code) return '';
  if (code >= 200 && code < 300) return 'status-2xx';
  if (code >= 300 && code < 400) return 'status-3xx';
  if (code >= 400 && code < 500) return 'status-4xx';
  return 'status-5xx';
}

export default function ValidateEndpointModal({ isOpen, endpoint, endpointType, onClose, onValidate }) {
  const isEmbedding = endpointType === 'Embedding';
  const [input, setInput] = useState(isEmbedding ? defaultEmbeddingInput : defaultCompletionPrompt);
  const [loading, setLoading] = useState(false);
  const [result, setResult] = useState(null);
  const autoRan = useRef(false);

  const runValidation = async (text) => {
    setLoading(true);
    setResult(null);

    const timeoutMs = endpoint?.MaximumTimeoutMs || 60000;
    const request = isEmbedding
      ? { EndpointId: endpoint.Id, Input: text, L2Normalization: false }
      : {
          EndpointId: endpoint.Id,
          Prompt: text,
          SystemPrompt: '',
          MaxTokens: 16,
          TimeoutMs: timeoutMs
        };

    try {
      const response = await onValidate(request);
      setResult(response);
    } catch (err) {
      setResult(err.response || {
        Success: false,
        StatusCode: err.statusCode || 0,
        Error: err.message
      });
    } finally {
      setLoading(false);
    }
  };

  // Reset and auto-run once each time the modal opens for an endpoint.
  useEffect(() => {
    if (isOpen && endpoint && !autoRan.current) {
      autoRan.current = true;
      const text = isEmbedding ? defaultEmbeddingInput : defaultCompletionPrompt;
      setInput(text);
      runValidation(text);
    }
    if (!isOpen) {
      autoRan.current = false;
      setResult(null);
      setLoading(false);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [isOpen, endpoint?.Id]);

  if (!isOpen || !endpoint) return null;

  const success = result?.Success === true;
  const embeddingPreview = isEmbedding && result?.Embedding ? result.Embedding.slice(0, 8) : [];

  return (
    <Modal title={`Validate ${endpointType} Endpoint`} onClose={onClose} className="modal-wide">
      <div className="validate-endpoint-modal">
        <div className="validate-summary">
          <div>
            <span>Name</span>
            <strong>{endpoint.Name || '-'}</strong>
          </div>
          <div>
            <span>Provider</span>
            <strong>{endpoint.ApiFormat}</strong>
          </div>
          <div>
            <span>Model</span>
            <strong>{endpoint.Model}</strong>
          </div>
          <div>
            <span>Endpoint</span>
            <strong>{endpoint.Endpoint}</strong>
          </div>
        </div>

        <label className="validate-input">
          <span>{isEmbedding ? 'Sample Input' : 'Sample Prompt'}</span>
          <textarea
            rows="2"
            value={input}
            onChange={e => setInput(e.target.value)}
            disabled={loading}
          />
        </label>

        {loading && (
          <div className="validate-loading">
            <span className="validate-spinner" />
            <span>Sending a live request through Partio to {endpoint.Model}...</span>
          </div>
        )}

        {!loading && result && (
          <div className={`validate-result ${success ? 'validate-result-success' : 'validate-result-failed'}`}>
            <div className="validate-result-header">
              <span className={`validate-badge ${success ? 'success' : 'failed'}`}>
                {success ? 'Reachable — model responded' : 'Validation failed'}
              </span>
              {result.StatusCode != null && (
                <span className={`http-status ${statusClass(result.StatusCode)}`}>{result.StatusCode}</span>
              )}
            </div>

            <dl className="validate-metrics">
              <div><dt>Response Time</dt><dd>{formatMs(result.ResponseTimeMs)}</dd></div>
              <div><dt>Model</dt><dd>{result.Model || endpoint.Model || 'N/A'}</dd></div>
              {isEmbedding && (
                <div><dt>Dimensions</dt><dd>{success ? (result.Dimensions || 0) : 'N/A'}</dd></div>
              )}
              {result.RequestHistoryId && (
                <div><dt>History Entry</dt><dd><CopyableId value={result.RequestHistoryId} /></dd></div>
              )}
            </dl>

            {success && isEmbedding && embeddingPreview.length > 0 && (
              <div className="validate-output">
                <div className="validate-output-label">Vector Preview</div>
                <code>[{embeddingPreview.map(v => Number(v).toFixed(6)).join(', ')}{result.Dimensions > embeddingPreview.length ? ', ...' : ''}]</code>
              </div>
            )}

            {success && !isEmbedding && (
              <div className="validate-output">
                <div className="validate-output-label">Model Output</div>
                <pre>{result.Output || '(empty)'}</pre>
              </div>
            )}

            {!success && result.Error && (
              <div className="validate-error">{result.Error}</div>
            )}
          </div>
        )}

        <div className="modal-actions">
          <button className="secondary" onClick={onClose} disabled={loading}>Close</button>
          <button className="primary" onClick={() => runValidation(input)} disabled={loading || !input.trim()}>
            {loading ? 'Validating...' : (result ? 'Re-run' : 'Validate')}
          </button>
        </div>
      </div>
    </Modal>
  );
}
