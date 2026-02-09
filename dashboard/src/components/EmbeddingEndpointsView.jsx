import React, { useState, useEffect, useCallback, useRef } from 'react';
import { useApp } from '../context/AppContext';
import { PartioApi } from '../utils/api';
import Modal from './Modal';
import CopyableId from './CopyableId';
import ActionMenu from './ActionMenu';
import DataTable from './DataTable';
import AlertModal from './modals/AlertModal';
import DeleteConfirmModal from './modals/DeleteConfirmModal';
import JsonViewModal from './modals/JsonViewModal';
import './EmbeddingEndpointsView.css';

function getHealthCheckDefaults(apiFormat, endpoint) {
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
  return {
    HealthCheckUrl: baseUrl ? baseUrl + '/v1/models' : '',
    HealthCheckMethod: 'GET',
    HealthCheckIntervalMs: 30000,
    HealthCheckTimeoutMs: 10000,
    HealthCheckExpectedStatusCode: 200,
    HealthyThreshold: 2,
    UnhealthyThreshold: 2,
    HealthCheckUseAuth: true,
  };
}

function HealthHistogram({ history, width = 120, height = 24 }) {
  if (!history || history.length === 0) return <span className="text-muted">No data</span>;

  const now = new Date();
  const sorted = [...history].sort((a, b) => new Date(a.TimestampUtc) - new Date(b.TimestampUtc));
  const oldest = new Date(sorted[0].TimestampUtc);
  const spanMs = now - oldest;
  const spanHours = spanMs / (1000 * 60 * 60);

  let buckets = [];
  if (spanHours < 1) {
    buckets = sorted.map(r => ({ success: r.Success ? 1 : 0, fail: r.Success ? 0 : 1, time: r.TimestampUtc }));
  } else {
    const bucketMs = spanHours <= 6 ? 60000 : 300000;
    const bucketMap = new Map();
    for (const r of sorted) {
      const t = new Date(r.TimestampUtc).getTime();
      const key = Math.floor(t / bucketMs);
      if (!bucketMap.has(key)) bucketMap.set(key, { success: 0, fail: 0 });
      const b = bucketMap.get(key);
      if (r.Success) b.success++; else b.fail++;
    }
    for (const [key, val] of bucketMap) {
      buckets.push({ ...val, time: new Date(key * bucketMs).toISOString() });
    }
  }

  const maxBars = Math.floor(width / 6);
  if (buckets.length > maxBars) {
    buckets = buckets.slice(-maxBars);
  }
  const barWidth = Math.max(4, Math.floor(width / buckets.length) - 2);

  return (
    <div style={{ display: 'flex', alignItems: 'flex-end', gap: '2px', height: height + 'px', maxWidth: width + 'px', overflow: 'hidden' }}>
      {buckets.map((b, i) => {
        let color = '#4caf50';
        if (b.fail > 0 && b.success === 0) color = '#f44336';
        else if (b.fail > 0 && b.success > 0) color = '#ff9800';
        const title = `${new Date(b.time).toLocaleTimeString()} - ${b.success} ok, ${b.fail} fail`;
        return <div key={i} title={title} style={{ width: barWidth + 'px', height: height + 'px', backgroundColor: color, borderRadius: '1px' }} />;
      })}
    </div>
  );
}

function HealthDetailModal({ isOpen, onClose, healthData }) {
  if (!isOpen || !healthData) return null;

  const uptimePct = healthData.UptimePercentage != null ? healthData.UptimePercentage.toFixed(2) + '%' : 'N/A';
  const history = healthData.History || [];
  const spanMs = history.length > 0
    ? new Date() - new Date(history.sort((a, b) => new Date(a.TimestampUtc) - new Date(b.TimestampUtc))[0].TimestampUtc)
    : 0;
  const spanStr = spanMs > 0 ? formatDuration(spanMs) : 'No data';

  return (
    <Modal title={`Health: ${healthData.EndpointName}`} onClose={onClose} className="modal-wide">
      <div className="health-modal">
        <div className="health-stats-row">
          <div className="health-stat-card">
            <div className="health-stat-label">Status</div>
            <div className="health-stat-value">
              <span className={`status-badge ${healthData.IsHealthy ? 'active' : 'inactive'}`}>
                {healthData.IsHealthy ? 'Healthy' : 'Unhealthy'}
              </span>
            </div>
          </div>
          <div className="health-stat-card">
            <div className="health-stat-label">Uptime</div>
            <div className="health-stat-value">{uptimePct}</div>
          </div>
          <div className="health-stat-card">
            <div className="health-stat-label">History Span</div>
            <div className="health-stat-value">{spanStr}</div>
          </div>
          <div className="health-stat-card">
            <div className="health-stat-label">Consecutive OK</div>
            <div className="health-stat-value health-stat-success">{healthData.ConsecutiveSuccesses}</div>
          </div>
          <div className="health-stat-card">
            <div className="health-stat-label">Consecutive Fail</div>
            <div className="health-stat-value health-stat-danger">{healthData.ConsecutiveFailures}</div>
          </div>
        </div>

        {healthData.LastError && (
          <div className="health-error-box">
            <div className="health-error-label">Last Error</div>
            <div className="health-error-message">{healthData.LastError}</div>
          </div>
        )}

        <div className="health-histogram-section">
          <div className="health-section-label">Health History</div>
          <div className="health-histogram-container">
            <HealthHistogram history={history} width={770} height={36} />
          </div>
        </div>

        <div className="health-timestamps">
          <div className="health-timestamp-item">
            <span className="health-timestamp-label">First check</span>
            <span className="health-timestamp-value">{healthData.FirstCheckUtc ? new Date(healthData.FirstCheckUtc).toLocaleString() : 'N/A'}</span>
          </div>
          <div className="health-timestamp-item">
            <span className="health-timestamp-label">Last check</span>
            <span className="health-timestamp-value">{healthData.LastCheckUtc ? new Date(healthData.LastCheckUtc).toLocaleString() : 'N/A'}</span>
          </div>
          <div className="health-timestamp-item">
            <span className="health-timestamp-label">Last healthy</span>
            <span className="health-timestamp-value">{healthData.LastHealthyUtc ? new Date(healthData.LastHealthyUtc).toLocaleString() : 'N/A'}</span>
          </div>
          <div className="health-timestamp-item">
            <span className="health-timestamp-label">Last unhealthy</span>
            <span className="health-timestamp-value">{healthData.LastUnhealthyUtc ? new Date(healthData.LastUnhealthyUtc).toLocaleString() : 'N/A'}</span>
          </div>
        </div>
      </div>
    </Modal>
  );
}

function formatDuration(ms) {
  const hours = Math.floor(ms / 3600000);
  const minutes = Math.floor((ms % 3600000) / 60000);
  if (hours > 0) return `${hours}h ${minutes}m`;
  return `${minutes}m`;
}

function FieldLabel({ text, tooltip }) {
  return (
    <label>
      {text}
      {tooltip && (
        <span className="field-tooltip" data-tooltip={tooltip}>
          <svg className="field-tooltip-icon" viewBox="0 0 16 16" fill="none" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round">
            <circle cx="8" cy="8" r="7" />
            <line x1="8" y1="7" x2="8" y2="11" />
            <circle cx="8" cy="5" r="0.5" fill="currentColor" stroke="none" />
          </svg>
        </span>
      )}
    </label>
  );
}

const defaultHealthFields = {
  HealthCheckEnabled: false,
  HealthCheckUrl: '',
  HealthCheckMethod: 'GET',
  HealthCheckIntervalMs: 5000,
  HealthCheckTimeoutMs: 2000,
  HealthCheckExpectedStatusCode: 200,
  HealthyThreshold: 2,
  UnhealthyThreshold: 2,
  HealthCheckUseAuth: false,
};

export default function EmbeddingEndpointsView() {
  const { serverUrl, bearerToken } = useApp();
  const api = new PartioApi(serverUrl, bearerToken);
  const [data, setData] = useState([]);
  const [loading, setLoading] = useState(false);
  const [showModal, setShowModal] = useState(false);
  const [editing, setEditing] = useState(null);
  const [form, setForm] = useState({ TenantId: 'default', Model: '', Endpoint: '', ApiFormat: 'Ollama', ApiKey: '', EnableRequestHistory: false, ...defaultHealthFields });
  const [alertModal, setAlertModal] = useState({ isOpen: false, message: '', type: 'error' });
  const [deleteModal, setDeleteModal] = useState({ isOpen: false, id: null });
  const [tenants, setTenants] = useState([]);
  const [jsonModal, setJsonModal] = useState({ isOpen: false, data: null });
  const [healthData, setHealthData] = useState({});
  const [healthDetailModal, setHealthDetailModal] = useState({ isOpen: false, data: null });
  const [healthFieldsEdited, setHealthFieldsEdited] = useState(false);

  const loadTenants = useCallback(async () => {
    try {
      const result = await api.enumerateTenants({ MaxResults: 1000 });
      setTenants(result.Data || []);
    } catch (err) { console.error(err); }
  }, [serverUrl, bearerToken]);

  const loadHealth = useCallback(async () => {
    try {
      const result = await api.getAllEndpointHealth();
      const map = {};
      if (Array.isArray(result)) {
        for (const h of result) {
          map[h.EndpointId] = h;
        }
      }
      setHealthData(map);
    } catch (err) { console.error(err); }
  }, [serverUrl, bearerToken]);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const result = await api.enumerateEndpoints({ MaxResults: 1000 });
      setData(result.Data || []);
    } catch (err) { console.error(err); }
    setLoading(false);
  }, [serverUrl, bearerToken]);

  useEffect(() => { load(); loadTenants(); loadHealth(); }, [load, loadTenants, loadHealth]);

  // Refresh health data periodically
  useEffect(() => {
    const interval = setInterval(loadHealth, 15000);
    return () => clearInterval(interval);
  }, [loadHealth]);

  const openCreate = () => {
    setEditing(null);
    const tenantId = tenants.length > 0 ? tenants[0].Id : '';
    const defaults = getHealthCheckDefaults('Ollama', '');
    setForm({ TenantId: tenantId, Model: '', Endpoint: '', ApiFormat: 'Ollama', ApiKey: '', EnableRequestHistory: false, HealthCheckEnabled: false, ...defaults });
    setHealthFieldsEdited(false);
    setShowModal(true);
  };

  const openEdit = (item) => {
    setEditing(item);
    setForm({
      TenantId: item.TenantId,
      Model: item.Model,
      Endpoint: item.Endpoint,
      ApiFormat: item.ApiFormat,
      ApiKey: item.ApiKey || '',
      EnableRequestHistory: item.EnableRequestHistory || false,
      HealthCheckEnabled: item.HealthCheckEnabled || false,
      HealthCheckUrl: item.HealthCheckUrl || '',
      HealthCheckMethod: item.HealthCheckMethod === 1 ? 'HEAD' : 'GET',
      HealthCheckIntervalMs: item.HealthCheckIntervalMs || 5000,
      HealthCheckTimeoutMs: item.HealthCheckTimeoutMs || 2000,
      HealthCheckExpectedStatusCode: item.HealthCheckExpectedStatusCode || 200,
      HealthyThreshold: item.HealthyThreshold || 2,
      UnhealthyThreshold: item.UnhealthyThreshold || 2,
      HealthCheckUseAuth: item.HealthCheckUseAuth || false,
    });
    setHealthFieldsEdited(true);
    setShowModal(true);
  };

  const handleFormatChange = (newFormat) => {
    const newForm = { ...form, ApiFormat: newFormat };
    if (!healthFieldsEdited) {
      const defaults = getHealthCheckDefaults(newFormat, form.Endpoint);
      Object.assign(newForm, defaults);
    }
    setForm(newForm);
  };

  const handleEndpointChange = (newEndpoint) => {
    const newForm = { ...form, Endpoint: newEndpoint };
    if (!healthFieldsEdited) {
      const defaults = getHealthCheckDefaults(form.ApiFormat, newEndpoint);
      newForm.HealthCheckUrl = defaults.HealthCheckUrl;
    }
    setForm(newForm);
  };

  const handleSave = async () => {
    try {
      const payload = {
        TenantId: form.TenantId,
        Model: form.Model,
        Endpoint: form.Endpoint,
        ApiFormat: form.ApiFormat,
        ApiKey: form.ApiKey || null,
        EnableRequestHistory: form.EnableRequestHistory,
        HealthCheckEnabled: form.HealthCheckEnabled,
        HealthCheckUrl: form.HealthCheckUrl || null,
        HealthCheckMethod: form.HealthCheckMethod === 'HEAD' ? 1 : 0,
        HealthCheckIntervalMs: parseInt(form.HealthCheckIntervalMs) || 5000,
        HealthCheckTimeoutMs: parseInt(form.HealthCheckTimeoutMs) || 2000,
        HealthCheckExpectedStatusCode: parseInt(form.HealthCheckExpectedStatusCode) || 200,
        HealthyThreshold: parseInt(form.HealthyThreshold) || 2,
        UnhealthyThreshold: parseInt(form.UnhealthyThreshold) || 2,
        HealthCheckUseAuth: form.HealthCheckUseAuth,
      };
      if (editing) {
        await api.updateEndpoint(editing.Id, { ...editing, ...payload });
      } else {
        await api.createEndpoint(payload);
      }
      setShowModal(false);
      load();
      loadHealth();
    } catch (err) { setAlertModal({ isOpen: true, message: err.message, type: 'error' }); }
  };

  const handleDelete = async () => {
    try {
      await api.deleteEndpoint(deleteModal.id);
      setDeleteModal({ isOpen: false, id: null });
      load();
      loadHealth();
    } catch (err) {
      setDeleteModal({ isOpen: false, id: null });
      setAlertModal({ isOpen: true, message: err.message, type: 'error' });
    }
  };

  const openHealthDetail = async (endpointId) => {
    try {
      const result = await api.getEndpointHealth(endpointId);
      setHealthDetailModal({ isOpen: true, data: result });
    } catch (err) {
      setAlertModal({ isOpen: true, message: 'Health data not available: ' + err.message, type: 'error' });
    }
  };

  const columns = [
    {
      key: 'Id',
      label: 'ID',
      tooltip: 'Unique endpoint identifier (click to copy)',
      width: '280px',
      render: (item) => <CopyableId value={item.Id} />
    },
    {
      key: 'Model',
      label: 'Model',
      tooltip: 'Embedding model name served by this endpoint'
    },
    {
      key: 'Endpoint',
      label: 'Endpoint',
      tooltip: 'Base URL of the embedding API server'
    },
    {
      key: 'ApiFormat',
      label: 'Format',
      tooltip: 'API protocol format: Ollama or OpenAI-compatible'
    },
    {
      key: 'Active',
      label: 'Status',
      tooltip: 'Whether this endpoint is active and accepting requests',
      filterValue: (item) => item.Active ? 'Active' : 'Inactive',
      render: (item) => (
        <span className={`status-badge ${item.Active ? 'active' : 'inactive'}`}>
          {item.Active ? 'Active' : 'Inactive'}
        </span>
      )
    },
    {
      key: 'Health',
      label: 'Health',
      tooltip: 'Live health check status and recent history (click for details)',
      sortable: false,
      filterValue: (item) => {
        if (!item.HealthCheckEnabled) return 'N/A';
        const h = healthData[item.Id];
        return h ? (h.IsHealthy ? 'Healthy' : 'Unhealthy') : 'Pending';
      },
      render: (item) => {
        if (!item.HealthCheckEnabled) return <span className="status-badge" style={{ backgroundColor: '#555', color: '#fff' }}>N/A</span>;
        const h = healthData[item.Id];
        if (!h) return <span className="status-badge" style={{ backgroundColor: '#888' }}>Pending</span>;
        return (
          <div style={{ display: 'flex', alignItems: 'center', gap: '8px', cursor: 'pointer' }} onClick={() => openHealthDetail(item.Id)}>
            <span className={`status-badge ${h.IsHealthy ? 'active' : 'inactive'}`}>
              {h.IsHealthy ? 'Healthy' : 'Unhealthy'}
            </span>
            <HealthHistogram history={h.History || []} width={80} height={18} />
          </div>
        );
      }
    },
    {
      key: 'EnableRequestHistory',
      label: 'History',
      tooltip: 'Whether request/response logging is enabled',
      filterValue: (item) => item.EnableRequestHistory ? 'On' : 'Off',
      render: (item) => (
        <span className={`status-badge ${item.EnableRequestHistory ? 'active' : 'inactive'}`}>
          {item.EnableRequestHistory ? 'On' : 'Off'}
        </span>
      )
    },
    {
      key: 'actions',
      label: 'Actions',
      tooltip: 'Edit, view JSON, or delete this endpoint',
      isAction: true,
      sortable: false,
      render: (item) => (
        <ActionMenu actions={[
          { label: 'Edit', onClick: () => openEdit(item) },
          { label: 'View JSON', onClick: () => setJsonModal({ isOpen: true, data: item }) },
          ...(item.HealthCheckEnabled ? [{ label: 'Health Detail', onClick: () => openHealthDetail(item.Id) }] : []),
          { divider: true },
          { label: 'Delete', danger: true, onClick: () => setDeleteModal({ isOpen: true, id: item.Id }) }
        ]} />
      )
    }
  ];

  return (
    <div>
      <div className="header-row">
        <h2>Embedding Endpoints</h2>
        <div className="header-row-actions">
          <button className="refresh-btn" onClick={() => { load(); loadHealth(); }} title="Refresh">
            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
              <polyline points="23 4 23 10 17 10" /><polyline points="1 20 1 14 7 14" />
              <path d="M3.51 9a9 9 0 0114.85-3.36L23 10M1 14l4.64 4.36A9 9 0 0020.49 15" />
            </svg>
          </button>
          <button className="primary" onClick={openCreate}>Create Endpoint</button>
        </div>
      </div>
      <DataTable data={data} columns={columns} loading={loading} />
      {showModal && (
        <Modal title={editing ? 'Edit Endpoint' : 'Create Endpoint'} onClose={() => setShowModal(false)} className="modal-wide">
          {/* Connection Section */}
          <div className="endpoint-form-section">
            <div className="endpoint-form-section-header">Connection</div>
            <div className="endpoint-form-row">
              {!editing && (
                <div className="form-group">
                  <FieldLabel text="Tenant" tooltip="The tenant this endpoint belongs to. Each tenant has isolated data." />
                  <select value={form.TenantId} onChange={e => setForm({ ...form, TenantId: e.target.value })}>
                    {tenants.map(t => <option key={t.Id} value={t.Id}>{t.Name || t.Id}</option>)}
                  </select>
                </div>
              )}
              <div className="form-group">
                <FieldLabel text="API Format" tooltip="Protocol format. Ollama for local Ollama servers, OpenAI for OpenAI-compatible APIs." />
                <select value={form.ApiFormat} onChange={e => handleFormatChange(e.target.value)}>
                  <option value="Ollama">Ollama</option>
                  <option value="OpenAI">OpenAI</option>
                </select>
              </div>
            </div>
            <div className="endpoint-form-row">
              <div className="form-group">
                <FieldLabel text="Model" tooltip="Embedding model name served by this endpoint. Example: all-minilm, text-embedding-3-small" />
                <input value={form.Model} onChange={e => setForm({ ...form, Model: e.target.value })} placeholder="e.g. all-minilm" />
              </div>
              <div className="form-group">
                <FieldLabel text="API Key" tooltip="Authentication key for the embedding API. Required for OpenAI, optional for Ollama." />
                <input type="password" value={form.ApiKey} onChange={e => setForm({ ...form, ApiKey: e.target.value })} placeholder="Optional" />
              </div>
            </div>
            <div className="form-group">
              <FieldLabel text="Endpoint URL" tooltip="Base URL of the embedding API server. Example: http://localhost:11434 or https://api.openai.com" />
              <input value={form.Endpoint} onChange={e => handleEndpointChange(e.target.value)} placeholder="http://localhost:11434" />
            </div>
          </div>

          {/* Options Section */}
          <div className="endpoint-form-section">
            <div className="endpoint-form-section-header">Options</div>
            <div className="form-group">
              <label className="checkbox-label">
                <input type="checkbox" checked={form.EnableRequestHistory} onChange={e => setForm({ ...form, EnableRequestHistory: e.target.checked })} />
                {' '}Enable Request History
                <span className="field-tooltip" data-tooltip="Log all embedding requests and responses for debugging and auditing.">
                  <svg className="field-tooltip-icon" viewBox="0 0 16 16" fill="none" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round">
                    <circle cx="8" cy="8" r="7" />
                    <line x1="8" y1="7" x2="8" y2="11" />
                    <circle cx="8" cy="5" r="0.5" fill="currentColor" stroke="none" />
                  </svg>
                </span>
              </label>
            </div>
          </div>

          {/* Health Checks Section */}
          <div className="endpoint-form-section">
            <div className="endpoint-form-section-header">Health Checks</div>
            <div className="form-group">
              <label className="checkbox-label">
                <input type="checkbox" checked={form.HealthCheckEnabled} onChange={e => setForm({ ...form, HealthCheckEnabled: e.target.checked })} />
                {' '}Enable Health Checks
                <span className="field-tooltip" data-tooltip="Periodically probe the endpoint to track availability and uptime.">
                  <svg className="field-tooltip-icon" viewBox="0 0 16 16" fill="none" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round">
                    <circle cx="8" cy="8" r="7" />
                    <line x1="8" y1="7" x2="8" y2="11" />
                    <circle cx="8" cy="5" r="0.5" fill="currentColor" stroke="none" />
                  </svg>
                </span>
              </label>
            </div>
            {form.HealthCheckEnabled && (
              <>
                <div className="form-group">
                  <FieldLabel text="Health Check URL" tooltip="Full URL to probe for health. Auto-set from endpoint and format. Example: http://localhost:11434/api/tags" />
                  <input value={form.HealthCheckUrl} onChange={e => { setHealthFieldsEdited(true); setForm({ ...form, HealthCheckUrl: e.target.value }); }} placeholder="Auto-detected from endpoint" />
                </div>
                <div className="endpoint-form-row">
                  <div className="form-group">
                    <FieldLabel text="Method" tooltip="HTTP method for health checks. GET returns a response body, HEAD is lighter." />
                    <select value={form.HealthCheckMethod} onChange={e => { setHealthFieldsEdited(true); setForm({ ...form, HealthCheckMethod: e.target.value }); }}>
                      <option value="GET">GET</option>
                      <option value="HEAD">HEAD</option>
                    </select>
                  </div>
                  <div className="form-group">
                    <FieldLabel text="Expected Status Code" tooltip="HTTP status code that indicates success. Typically 200." />
                    <input type="number" value={form.HealthCheckExpectedStatusCode} onChange={e => { setHealthFieldsEdited(true); setForm({ ...form, HealthCheckExpectedStatusCode: e.target.value }); }} />
                  </div>
                </div>
                <div className="endpoint-form-row">
                  <div className="form-group">
                    <FieldLabel text="Interval (ms)" tooltip="Milliseconds between health checks. Example: 10000 = every 10 seconds." />
                    <input type="number" value={form.HealthCheckIntervalMs} onChange={e => { setHealthFieldsEdited(true); setForm({ ...form, HealthCheckIntervalMs: e.target.value }); }} />
                  </div>
                  <div className="form-group">
                    <FieldLabel text="Timeout (ms)" tooltip="Maximum wait time per health check in milliseconds. Example: 5000 = 5 seconds." />
                    <input type="number" value={form.HealthCheckTimeoutMs} onChange={e => { setHealthFieldsEdited(true); setForm({ ...form, HealthCheckTimeoutMs: e.target.value }); }} />
                  </div>
                </div>
                <div className="endpoint-form-row">
                  <div className="form-group">
                    <FieldLabel text="Healthy Threshold" tooltip="Consecutive successful checks needed to mark endpoint as healthy." />
                    <input type="number" value={form.HealthyThreshold} onChange={e => { setHealthFieldsEdited(true); setForm({ ...form, HealthyThreshold: e.target.value }); }} />
                  </div>
                  <div className="form-group">
                    <FieldLabel text="Unhealthy Threshold" tooltip="Consecutive failed checks needed to mark endpoint as unhealthy." />
                    <input type="number" value={form.UnhealthyThreshold} onChange={e => { setHealthFieldsEdited(true); setForm({ ...form, UnhealthyThreshold: e.target.value }); }} />
                  </div>
                </div>
                <div className="form-group">
                  <label className="checkbox-label">
                    <input type="checkbox" checked={form.HealthCheckUseAuth} onChange={e => { setHealthFieldsEdited(true); setForm({ ...form, HealthCheckUseAuth: e.target.checked }); }} />
                    {' '}Send API Key as Bearer Token
                    <span className="field-tooltip" data-tooltip="Include the API key as a Bearer token in health check requests. Enable for authenticated endpoints.">
                      <svg className="field-tooltip-icon" viewBox="0 0 16 16" fill="none" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round">
                        <circle cx="8" cy="8" r="7" />
                        <line x1="8" y1="7" x2="8" y2="11" />
                        <circle cx="8" cy="5" r="0.5" fill="currentColor" stroke="none" />
                      </svg>
                    </span>
                  </label>
                </div>
              </>
            )}
          </div>

          <div className="btn-group" style={{ marginTop: 16 }}>
            <button className="primary" onClick={handleSave}>Save</button>
            <button className="secondary" onClick={() => setShowModal(false)}>Cancel</button>
          </div>
        </Modal>
      )}
      <AlertModal
        isOpen={alertModal.isOpen}
        onClose={() => setAlertModal({ isOpen: false, message: '', type: 'error' })}
        message={alertModal.message}
        type={alertModal.type}
      />
      <DeleteConfirmModal
        isOpen={deleteModal.isOpen}
        onClose={() => setDeleteModal({ isOpen: false, id: null })}
        onConfirm={handleDelete}
        entityType="endpoint"
      />
      <JsonViewModal
        isOpen={jsonModal.isOpen}
        onClose={() => setJsonModal({ isOpen: false, data: null })}
        title="Endpoint JSON"
        data={jsonModal.data}
      />
      <HealthDetailModal
        isOpen={healthDetailModal.isOpen}
        onClose={() => setHealthDetailModal({ isOpen: false, data: null })}
        healthData={healthDetailModal.data}
      />
    </div>
  );
}
