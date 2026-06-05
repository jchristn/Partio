import { useEffect, useMemo, useState } from 'react';
import Modal from '../Modal';
import CopyableId from '../CopyableId';
import { t } from '../../i18n';
import './LoadModelModal.css';

const defaultRequest = {
  Strategy: 'Auto',
  KeepAlive: '30m',
  TimeoutMs: '',
  SampleInput: 'Partio model load probe',
  MaxTokens: '1',
  RecordRequestHistory: true,
  RequireNativeLoad: false
};

function formatDuration(value) {
  if (value == null) return 'N/A';
  return `${Number(value).toLocaleString()} ms`;
}

function countCalls(result) {
  const embeddingCount = Array.isArray(result?.EmbeddingCalls) ? result.EmbeddingCalls.length : 0;
  const completionCount = Array.isArray(result?.CompletionCalls) ? result.CompletionCalls.length : 0;
  return embeddingCount + completionCount;
}

export default function LoadModelModal({ isOpen, endpoint, endpointType, onClose, onLoad, onComplete, onLoadingChange }) {
  const [form, setForm] = useState(defaultRequest);
  const [loading, setLoading] = useState(false);
  const [result, setResult] = useState(null);

  useEffect(() => {
    if (isOpen) {
      setForm(defaultRequest);
      setResult(null);
      setLoading(false);
    }
  }, [isOpen, endpoint?.Id]);

  const effectiveTimeout = useMemo(() => {
    if (form.TimeoutMs !== '') return form.TimeoutMs;
    return endpoint?.MaximumTimeoutMs || 60000;
  }, [form.TimeoutMs, endpoint]);

  if (!isOpen || !endpoint) return null;

  const handleSubmit = async () => {
    setLoading(true);
    setResult(null);
    onLoadingChange?.(endpoint.Id, true);

    const request = {
      Strategy: form.Strategy,
      KeepAlive: form.KeepAlive || null,
      TimeoutMs: parseInt(effectiveTimeout, 10) || 60000,
      SampleInput: form.SampleInput || 'Partio model load probe',
      MaxTokens: parseInt(form.MaxTokens, 10) || 1,
      RecordRequestHistory: form.RecordRequestHistory,
      RequireNativeLoad: form.RequireNativeLoad
    };

    try {
      const response = await onLoad(endpoint.Id, request);
      setResult(response);
      onComplete?.();
    } catch (err) {
      setResult(err.response || {
        Success: false,
        StatusCode: err.statusCode || 0,
        Outcome: 'Failed',
        Message: err.message
      });
    } finally {
      setLoading(false);
      onLoadingChange?.(endpoint.Id, false);
    }
  };

  const strategyOptions = [
    { value: 'Auto', label: t('loadModel.auto') },
    { value: 'NativeProviderLoad', label: t('loadModel.native') },
    { value: 'WarmRequest', label: t('loadModel.warm') }
  ];

  return (
    <Modal title={t('loadModel.title')} onClose={onClose} className="modal-wide">
      <div className="load-model-modal">
        <div className="load-model-summary">
          <div>
            <span>{t('loadModel.endpointType')}</span>
            <strong>{endpointType}</strong>
          </div>
          <div>
            <span>{t('loadModel.provider')}</span>
            <strong>{endpoint.ApiFormat}</strong>
          </div>
          <div>
            <span>{t('loadModel.model')}</span>
            <strong>{endpoint.Model}</strong>
          </div>
          <div>
            <span>{t('loadModel.endpoint')}</span>
            <strong>{endpoint.Endpoint}</strong>
          </div>
        </div>

        <div className="load-model-warning">{t('loadModel.warning')}</div>

        <div className="load-model-form">
          <label>
            <span>{t('loadModel.strategy')}</span>
            <select value={form.Strategy} onChange={e => setForm({ ...form, Strategy: e.target.value })}>
              {strategyOptions.map(option => (
                <option key={option.value} value={option.value}>{option.label}</option>
              ))}
            </select>
          </label>
          <label>
            <span>{t('loadModel.keepAlive')}</span>
            <input value={form.KeepAlive} onChange={e => setForm({ ...form, KeepAlive: e.target.value })} />
          </label>
          <label>
            <span>{t('loadModel.timeout')}</span>
            <input type="number" min="1" value={effectiveTimeout} onChange={e => setForm({ ...form, TimeoutMs: e.target.value })} />
          </label>
          <label>
            <span>{t('loadModel.maxTokens')}</span>
            <input type="number" min="1" max="16" value={form.MaxTokens} onChange={e => setForm({ ...form, MaxTokens: e.target.value })} />
          </label>
          <label className="load-model-form-wide">
            <span>{t('loadModel.sampleInput')}</span>
            <textarea rows="3" value={form.SampleInput} onChange={e => setForm({ ...form, SampleInput: e.target.value })} />
          </label>
          <label className="load-model-checkbox">
            <input type="checkbox" checked={form.RequireNativeLoad} onChange={e => setForm({ ...form, RequireNativeLoad: e.target.checked })} />
            <span>{t('loadModel.requireNative')}</span>
          </label>
          <label className="load-model-checkbox">
            <input type="checkbox" checked={form.RecordRequestHistory} onChange={e => setForm({ ...form, RecordRequestHistory: e.target.checked })} />
            <span>{t('loadModel.recordHistory')}</span>
          </label>
        </div>

        {result && (
          <div className={`load-model-result ${result.Success ? 'load-model-result-success' : 'load-model-result-failed'}`}>
            <div className="load-model-result-header">
              <strong>{t('loadModel.result')}</strong>
              <span>{result.Success ? t('loadModel.success') : t('loadModel.failed')}</span>
            </div>
            <dl>
              <div><dt>{t('loadModel.outcome')}</dt><dd>{result.Outcome || 'N/A'}</dd></div>
              <div><dt>{t('loadModel.status')}</dt><dd>{result.StatusCode || 'N/A'}</dd></div>
              <div><dt>{t('loadModel.duration')}</dt><dd>{formatDuration(result.ResponseTimeMs)}</dd></div>
              <div><dt>{t('loadModel.upstreamCalls')}</dt><dd>{countCalls(result)}</dd></div>
              {result.RequestHistoryId && (
                <div><dt>{t('loadModel.requestHistory')}</dt><dd><CopyableId value={result.RequestHistoryId} /></dd></div>
              )}
            </dl>
            {result.Message && (
              <p className="load-model-message">{result.Message}</p>
            )}
          </div>
        )}

        <div className="modal-actions">
          <button className="secondary" onClick={onClose} disabled={loading}>{t('loadModel.close')}</button>
          <button className="primary" onClick={handleSubmit} disabled={loading}>
            {loading ? t('loadModel.loading') : t('loadModel.submit')}
          </button>
        </div>
      </div>
    </Modal>
  );
}
