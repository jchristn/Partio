import React, { useState, useEffect, useCallback } from 'react';
import { useApp } from '../context/AppContext';
import { PartioApi } from '../utils/api';
import Modal from './Modal';
import CopyableId from './CopyableId';
import ActionMenu from './ActionMenu';
import DataTable from './DataTable';
import AlertModal from './modals/AlertModal';
import DeleteConfirmModal from './modals/DeleteConfirmModal';
import JsonViewModal from './modals/JsonViewModal';
import LoadModelModal from './modals/LoadModelModal';
import ValidateEndpointModal from './modals/ValidateEndpointModal';
import FormFieldLabel from './FormFieldLabel';
import Tooltip from './Tooltip';
import EndpointMetadataEditor, { createLabelRows, createTagRows, normalizeLabelRows, normalizeTagRows } from './EndpointMetadataEditor';
import { t } from '../i18n';
import ProviderPicker from './ProviderPicker';
import ProviderConnectionFields from './ProviderConnectionFields';
import {
  applyProviderChange,
  buildConnectionPayload,
  getProvider,
  hasEndpointOverride,
  hasMaskedSecrets,
  mergeRevealedSecrets,
  probeDescription,
  providerLabel,
  providerSettingsToForm,
  usesConnectivityProbe,
  validateConnection,
} from '../utils/providers';
import './EmbeddingEndpointsView.css';

function formatTenantOption(tenant) {
  const id = tenant.Id || '';
  const name = tenant.Name || id;
  return name && name !== id ? `${name} (${id})` : id;
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

const defaultHealthFields = {
  HealthCheckEnabled: true,
  HealthCheckUrl: '',
  HealthCheckMethod: 'GET',
  HealthCheckIntervalMs: 5000,
  HealthCheckTimeoutMs: 2000,
  HealthCheckExpectedStatusCode: 200,
  HealthyThreshold: 2,
  UnhealthyThreshold: 2,
  HealthCheckUseAuth: false,
};

const defaultMaximumTimeoutMs = 60000;
const defaultMaxConcurrentRequests = 2;
const defaultMaxQueueDepth = 0;
const defaultContextSize = 0;

function parsePositiveInteger(value, fallback) {
  const parsed = parseInt(value, 10);
  if (Number.isNaN(parsed) || parsed < 1) return fallback;
  return parsed;
}

function parseNonNegativeInteger(value, fallback) {
  const parsed = parseInt(value, 10);
  if (Number.isNaN(parsed) || parsed < 0) return fallback;
  return parsed;
}

function formatMaximumTimeout(value) {
  return `${parsePositiveInteger(value, defaultMaximumTimeoutMs)} ms`;
}

function formatMaxConcurrentRequests(value) {
  return parsePositiveInteger(value, defaultMaxConcurrentRequests).toString();
}

function formatMaxQueueDepth(value) {
  return parseNonNegativeInteger(value, defaultMaxQueueDepth).toString();
}

function formatContextSize(value) {
  return parseNonNegativeInteger(value, defaultContextSize).toString();
}

export default function CompletionEndpointsView() {
  const { serverUrl, bearerToken } = useApp();
  const api = new PartioApi(serverUrl, bearerToken);
  const [data, setData] = useState([]);
  const [loading, setLoading] = useState(false);
  const [showModal, setShowModal] = useState(false);
  const [editing, setEditing] = useState(null);
  const [form, setForm] = useState({ TenantId: 'default', Name: '', Model: '', Endpoint: '', ApiFormat: '', ApiKey: '', ProviderSettings: null, EndpointOverride: false, Active: true, EnableRequestHistory: true, MaximumTimeoutMs: defaultMaximumTimeoutMs.toString(), MaxConcurrentRequests: defaultMaxConcurrentRequests.toString(), MaxQueueDepth: defaultMaxQueueDepth.toString(), ContextSize: defaultContextSize.toString(), Labels: createLabelRows(), Tags: createTagRows(), ...defaultHealthFields });
  const [alertModal, setAlertModal] = useState({ isOpen: false, message: '', type: 'error' });
  const [deleteModal, setDeleteModal] = useState({ isOpen: false, id: null });
  const [tenants, setTenants] = useState([]);
  const [jsonModal, setJsonModal] = useState({ isOpen: false, data: null });
  const [healthData, setHealthData] = useState({});
  const [healthDetailModal, setHealthDetailModal] = useState({ isOpen: false, data: null });
  const [healthFieldsEdited, setHealthFieldsEdited] = useState(false);
  const [revealing, setRevealing] = useState(false);
  const [formErrors, setFormErrors] = useState({});
  const [loadModal, setLoadModal] = useState({ isOpen: false, endpoint: null });
  const [loadingModelId, setLoadingModelId] = useState(null);
  const [validateModal, setValidateModal] = useState({ isOpen: false, endpoint: null });

  const loadTenants = useCallback(async () => {
    try {
      const result = await api.enumerateTenants({ MaxResults: 1000 });
      setTenants(result.Data || []);
    } catch (err) { console.error(err); }
  }, [serverUrl, bearerToken]);

  const loadHealth = useCallback(async () => {
    try {
      const result = await api.getAllCompletionEndpointHealth();
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
      const result = await api.enumerateCompletionEndpoints({ MaxResults: 1000 });
      setData(result.Data || []);
    } catch (err) { console.error(err); }
    setLoading(false);
  }, [serverUrl, bearerToken]);

  useEffect(() => { load(); loadTenants(); loadHealth(); }, [load, loadTenants, loadHealth]);

  useEffect(() => {
    const interval = setInterval(loadHealth, 15000);
    return () => clearInterval(interval);
  }, [loadHealth]);

  const openCreate = () => {
    setEditing(null);
    const tenantId = tenants.length > 0 ? tenants[0].Id : '';
    setForm({ TenantId: tenantId, Name: '', Model: '', Endpoint: '', ApiFormat: '', ApiKey: '', ProviderSettings: null, EndpointOverride: false, Active: true, EnableRequestHistory: true, MaximumTimeoutMs: defaultMaximumTimeoutMs.toString(), MaxConcurrentRequests: defaultMaxConcurrentRequests.toString(), MaxQueueDepth: defaultMaxQueueDepth.toString(), ContextSize: defaultContextSize.toString(), Labels: createLabelRows(), Tags: createTagRows(), HealthCheckEnabled: true });
    setHealthFieldsEdited(false);
    setFormErrors({});
    setShowModal(true);
  };

  const openEdit = (item) => {
    setEditing(item);
    setFormErrors({});
    const providerSettings = providerSettingsToForm(item.ApiFormat, item.ProviderSettings);
    setForm({
      TenantId: item.TenantId,
      Name: item.Name || '',
      Model: item.Model,
      Endpoint: item.Endpoint,
      ApiFormat: item.ApiFormat,
      ApiKey: item.ApiKey || '',
      ProviderSettings: providerSettings,
      EndpointOverride: hasEndpointOverride(item.ApiFormat, item.Endpoint, providerSettings),
      Active: item.Active !== false,
      EnableRequestHistory: item.EnableRequestHistory || false,
      MaximumTimeoutMs: (item.MaximumTimeoutMs || defaultMaximumTimeoutMs).toString(),
      MaxConcurrentRequests: (item.MaxConcurrentRequests || defaultMaxConcurrentRequests).toString(),
      MaxQueueDepth: (item.MaxQueueDepth ?? defaultMaxQueueDepth).toString(),
      ContextSize: (item.ContextSize ?? defaultContextSize).toString(),
      Labels: createLabelRows(item.Labels),
      Tags: createTagRows(item.Tags),
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
    setFormErrors({});
    setForm(applyProviderChange(form, newFormat, 'completion', healthFieldsEdited));
  };

  const handleReveal = async () => {
    if (!editing) return;
    setRevealing(true);
    try {
      const revealed = await api.revealCompletionEndpoint(editing.Id);
      setForm(current => mergeRevealedSecrets(current, revealed));
    } catch (err) {
      setAlertModal({ isOpen: true, message: 'Could not reveal secrets: ' + err.message, type: 'error' });
    }
    setRevealing(false);
  };

  const handleSave = async () => {
    const errors = validateConnection(form, 'completion');
    setFormErrors(errors);
    if (Object.keys(errors).length > 0) return;
    try {
      const payload = {
        TenantId: form.TenantId,
        Name: form.Name || null,
        ...buildConnectionPayload(form),
        Active: form.Active,
        EnableRequestHistory: form.EnableRequestHistory,
        MaximumTimeoutMs: parsePositiveInteger(form.MaximumTimeoutMs, defaultMaximumTimeoutMs),
        MaxConcurrentRequests: parsePositiveInteger(form.MaxConcurrentRequests, defaultMaxConcurrentRequests),
        MaxQueueDepth: parseNonNegativeInteger(form.MaxQueueDepth, defaultMaxQueueDepth),
        ContextSize: parseNonNegativeInteger(form.ContextSize, defaultContextSize),
        Labels: normalizeLabelRows(form.Labels),
        Tags: normalizeTagRows(form.Tags),
        HealthCheckEnabled: form.HealthCheckEnabled,
        HealthCheckUrl: usesConnectivityProbe(form.ApiFormat) ? null : (form.HealthCheckUrl || null),
        HealthCheckMethod: form.HealthCheckMethod === 'HEAD' ? 1 : 0,
        HealthCheckIntervalMs: parseInt(form.HealthCheckIntervalMs) || 5000,
        HealthCheckTimeoutMs: parseInt(form.HealthCheckTimeoutMs) || 2000,
        HealthCheckExpectedStatusCode: parseInt(form.HealthCheckExpectedStatusCode) || 200,
        HealthyThreshold: parseInt(form.HealthyThreshold) || 2,
        UnhealthyThreshold: parseInt(form.UnhealthyThreshold) || 2,
        HealthCheckUseAuth: form.HealthCheckUseAuth,
      };
      if (editing) {
        await api.updateCompletionEndpoint(editing.Id, { ...editing, ...payload });
      } else {
        await api.createCompletionEndpoint(payload);
      }
      setShowModal(false);
      load();
      loadHealth();
    } catch (err) { setAlertModal({ isOpen: true, message: err.message, type: 'error' }); }
  };

  const handleDelete = async () => {
    try {
      await api.deleteCompletionEndpoint(deleteModal.id);
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
      const result = await api.getCompletionEndpointHealth(endpointId);
      setHealthDetailModal({ isOpen: true, data: result });
    } catch (err) {
      setAlertModal({ isOpen: true, message: 'Health data not available: ' + err.message, type: 'error' });
    }
  };

  const openLoadModel = (item) => {
    setLoadModal({ isOpen: true, endpoint: item });
  };

  const tenantOptions = form.TenantId && !tenants.some(t => t.Id === form.TenantId)
    ? [{ Id: form.TenantId, Name: '' }, ...tenants]
    : tenants;

  const columns = [
    {
      key: 'Id',
      label: 'ID',
      tooltip: 'Unique endpoint identifier (click to copy)',
      width: '18%',
      render: (item) => <CopyableId value={item.Id} />
    },
    {
      key: 'TenantId',
      label: 'Tenant ID',
      tooltip: 'Tenant identifier that owns this inference endpoint (click to copy)',
      width: '14%',
      render: (item) => item.TenantId ? <CopyableId value={item.TenantId} /> : '-'
    },
    {
      key: 'Name',
      label: 'Name',
      tooltip: 'Display name for this inference endpoint'
    },
    {
      key: 'Model',
      label: 'Model',
      tooltip: 'Completion model name served by this endpoint'
    },
    {
      key: 'Endpoint',
      label: 'Endpoint',
      tooltip: 'Base URL of the completion API server'
    },
    {
      key: 'ApiFormat',
      label: 'Provider',
      tooltip: 'Provider (ApiFormat) that serves this endpoint',
      filterValue: (item) => providerLabel(item.ApiFormat),
      render: (item) => providerLabel(item.ApiFormat)
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
      preventRowClick: true,
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
      key: 'actions',
      label: 'Actions',
      tooltip: 'Edit, view JSON, or delete this endpoint',
      width: '76px',
      isAction: true,
      preventRowClick: true,
      sortable: false,
      render: (item) => (
        <ActionMenu actions={[
          { label: 'Validate', onClick: () => setValidateModal({ isOpen: true, endpoint: item }) },
          { label: t('loadModel.action'), onClick: () => openLoadModel(item), disabled: loadingModelId === item.Id },
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
        <div className="page-title-block">
          <h2>Inference Endpoints</h2>
          <p className="view-subtitle">Configure completion and generation backends used for inference, summarization, and endpoint validation.</p>
        </div>
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
      <DataTable data={data} columns={columns} loading={loading} onRowClick={openEdit} />
      {showModal && (
        <Modal title={editing ? 'Edit Inference Endpoint' : 'Create Inference Endpoint'} onClose={() => setShowModal(false)} className="modal-wide">
          {/* Provider Section */}
          <div className="endpoint-form-section">
            <div className="endpoint-form-section-header">Provider</div>
            <ProviderPicker
              key={editing ? editing.Id : 'new'}
              kind="completion"
              value={form.ApiFormat}
              onChange={handleFormatChange}
              confirmChange={!!editing}
            />
            {formErrors.ApiFormat && <div className="field-error" role="alert">{formErrors.ApiFormat}</div>}
          </div>

          {!form.ApiFormat ? (
            <p className="text-muted provider-empty-hint">Choose a provider to configure the connection.</p>
          ) : (
          <>
          {/* Connection Section */}
          <div className="endpoint-form-section">
            <div className="endpoint-form-section-header">Connection</div>
            <div className="endpoint-form-row">
              <div className="form-group">
                <FormFieldLabel text="Tenant ID" tooltip="The tenant ID this endpoint belongs to. Each tenant has isolated data." />
                <Tooltip content="The tenant ID this endpoint belongs to. Each tenant has isolated data." block>
                  <select value={form.TenantId} onChange={e => setForm({ ...form, TenantId: e.target.value })}>
                    {tenantOptions.map(t => <option key={t.Id} value={t.Id}>{formatTenantOption(t)}</option>)}
                  </select>
                </Tooltip>
              </div>
              <div className="form-group">
                <FormFieldLabel text="Name" tooltip="Optional display name for this inference endpoint." />
                <Tooltip content="Optional display name for this inference endpoint." block>
                  <input value={form.Name} onChange={e => setForm({ ...form, Name: e.target.value })} placeholder="e.g. My Inference Endpoint" />
                </Tooltip>
              </div>
            </div>
            <ProviderConnectionFields
              kind="completion"
              form={form}
              setForm={setForm}
              errors={formErrors}
              healthFieldsEdited={healthFieldsEdited}
              editing={!!editing && hasMaskedSecrets(form)}
              onReveal={handleReveal}
              revealing={revealing}
            />
          </div>

          <div className="endpoint-form-section">
            <div className="endpoint-form-section-header">Metadata</div>
            <EndpointMetadataEditor
              labels={form.Labels}
              tags={form.Tags}
              onLabelsChange={Labels => setForm({ ...form, Labels })}
              onTagsChange={Tags => setForm({ ...form, Tags })}
            />
          </div>

          {/* Options Section */}
          <div className="endpoint-form-section">
            <div className="endpoint-form-section-header">Options</div>
            <div className="form-group">
              <label className="checkbox-label">
                <input type="checkbox" checked={form.Active} onChange={e => setForm({ ...form, Active: e.target.checked })} />
                {' '}
                <Tooltip content="Enable or disable this inference endpoint. Inactive endpoints are ignored for new work.">Active</Tooltip>
              </label>
            </div>
            <div className="form-group">
              <label className="checkbox-label">
                <input type="checkbox" checked={form.EnableRequestHistory} onChange={e => setForm({ ...form, EnableRequestHistory: e.target.checked })} />
                {' '}
                <Tooltip content="Log all completion requests and responses for debugging and auditing.">Enable Request History</Tooltip>
              </label>
            </div>
            <div className="endpoint-form-row">
              <div className="form-group">
                <FormFieldLabel text="Request Timeout (ms)" tooltip="Upper bound for upstream inference calls in milliseconds. Backend values are stored in milliseconds and clamped to positive non-zero integers." />
                <Tooltip content="Upper bound for upstream inference calls in milliseconds. Backend values are stored in milliseconds and clamped to positive non-zero integers." block>
                  <input type="number" min="1" step="1" value={form.MaximumTimeoutMs} onChange={e => setForm({ ...form, MaximumTimeoutMs: e.target.value })} />
                </Tooltip>
              </div>
              <div className="form-group">
                <FormFieldLabel text="Max Concurrent Requests" tooltip="Maximum concurrent upstream inference-provider requests allowed for this endpoint. When the limit is reached, Partio returns HTTP 429." />
                <Tooltip content="Maximum concurrent upstream inference-provider requests allowed for this endpoint. When the limit is reached, Partio returns HTTP 429." block>
                  <input type="number" min="1" step="1" value={form.MaxConcurrentRequests} onChange={e => setForm({ ...form, MaxConcurrentRequests: e.target.value })} />
                </Tooltip>
              </div>
            </div>
            <div className="endpoint-form-row">
              <div className="form-group">
                <FormFieldLabel text="Max Queue Depth" tooltip="How many requests may wait for a concurrency slot before Partio returns HTTP 429. 0 rejects immediately." />
                <Tooltip content="How many requests may wait for a concurrency slot before Partio returns HTTP 429. 0 rejects immediately." block>
                  <input type="number" min="0" step="1" value={form.MaxQueueDepth} onChange={e => setForm({ ...form, MaxQueueDepth: e.target.value })} />
                </Tooltip>
              </div>
              <div className="form-group">
                <FormFieldLabel text="Context Size" tooltip="Context window size (maximum tokens) for the model served by this endpoint. 0 means unspecified." />
                <Tooltip content="Context window size (maximum tokens) for the model served by this endpoint. 0 means unspecified." block>
                  <input type="number" min="0" step="1" value={form.ContextSize} onChange={e => setForm({ ...form, ContextSize: e.target.value })} />
                </Tooltip>
              </div>
            </div>
          </div>

          {/* Health Checks Section */}
          <div className="endpoint-form-section">
            <div className="endpoint-form-section-header">Health Checks</div>
            <div className="form-group">
              <label className="checkbox-label">
                <input type="checkbox" checked={form.HealthCheckEnabled} onChange={e => setForm({ ...form, HealthCheckEnabled: e.target.checked })} />
                {' '}
                <Tooltip content="Periodically probe the endpoint to track availability and uptime.">Enable Health Checks</Tooltip>
              </label>
            </div>
            {form.HealthCheckEnabled && usesConnectivityProbe(form.ApiFormat) && (
              <div className="provider-note health-probe-note">
                {getProvider(form.ApiFormat).label} is checked with a PolyPrompt connectivity probe that {probeDescription(form.ApiFormat, 'completion')}, so the URL, method, and status fields do not apply.
              </div>
            )}
            {form.HealthCheckEnabled && (
              <>
                {!usesConnectivityProbe(form.ApiFormat) && (
                <>
                <div className="form-group">
                  <FormFieldLabel text="Health Check URL" tooltip="Full URL to probe for health. Auto-set from endpoint and format." />
                  <Tooltip content="Full URL to probe for health. Auto-set from endpoint and format." block>
                    <input value={form.HealthCheckUrl} onChange={e => { setHealthFieldsEdited(true); setForm({ ...form, HealthCheckUrl: e.target.value }); }} placeholder="Auto-detected from endpoint" />
                  </Tooltip>
                </div>
                <div className="endpoint-form-row">
                  <div className="form-group">
                    <FormFieldLabel text="Method" tooltip="HTTP method for health checks. GET returns a response body, HEAD is lighter." />
                    <Tooltip content="HTTP method for health checks. GET returns a response body, HEAD is lighter." block>
                      <select value={form.HealthCheckMethod} onChange={e => { setHealthFieldsEdited(true); setForm({ ...form, HealthCheckMethod: e.target.value }); }}>
                        <option value="GET">GET</option>
                        <option value="HEAD">HEAD</option>
                      </select>
                    </Tooltip>
                  </div>
                  <div className="form-group">
                    <FormFieldLabel text="Expected Status Code" tooltip="HTTP status code that indicates success. Typically 200." />
                    <Tooltip content="HTTP status code that indicates success. Typically 200." block>
                      <input type="number" value={form.HealthCheckExpectedStatusCode} onChange={e => { setHealthFieldsEdited(true); setForm({ ...form, HealthCheckExpectedStatusCode: e.target.value }); }} />
                    </Tooltip>
                  </div>
                </div>
                </>
                )}
                <div className="endpoint-form-row">
                  <div className="form-group">
                    <FormFieldLabel text="Interval (ms)" tooltip="Milliseconds between health checks. Example: 15000 = every 15 seconds. Use positive values only." />
                    <Tooltip content="Milliseconds between health checks. Example: 15000 = every 15 seconds. Use positive values only." block>
                      <input type="number" value={form.HealthCheckIntervalMs} onChange={e => { setHealthFieldsEdited(true); setForm({ ...form, HealthCheckIntervalMs: e.target.value }); }} />
                    </Tooltip>
                  </div>
                  <div className="form-group">
                    <FormFieldLabel text="Timeout (ms)" tooltip="Maximum wait time per health check in milliseconds. Example: 5000 = 5 seconds. Use positive values only." />
                    <Tooltip content="Maximum wait time per health check in milliseconds. Example: 5000 = 5 seconds. Use positive values only." block>
                      <input type="number" value={form.HealthCheckTimeoutMs} onChange={e => { setHealthFieldsEdited(true); setForm({ ...form, HealthCheckTimeoutMs: e.target.value }); }} />
                    </Tooltip>
                  </div>
                </div>
                <div className="endpoint-form-row">
                  <div className="form-group">
                    <FormFieldLabel text="Healthy Threshold" tooltip="Consecutive successful checks needed to mark the endpoint healthy. Recommended range: 1-5." />
                    <Tooltip content="Consecutive successful checks needed to mark the endpoint healthy. Recommended range: 1-5." block>
                      <input type="number" value={form.HealthyThreshold} onChange={e => { setHealthFieldsEdited(true); setForm({ ...form, HealthyThreshold: e.target.value }); }} />
                    </Tooltip>
                  </div>
                  <div className="form-group">
                    <FormFieldLabel text="Unhealthy Threshold" tooltip="Consecutive failed checks needed to mark the endpoint unhealthy. Recommended range: 1-5." />
                    <Tooltip content="Consecutive failed checks needed to mark the endpoint unhealthy. Recommended range: 1-5." block>
                      <input type="number" value={form.UnhealthyThreshold} onChange={e => { setHealthFieldsEdited(true); setForm({ ...form, UnhealthyThreshold: e.target.value }); }} />
                    </Tooltip>
                  </div>
                </div>
                {!usesConnectivityProbe(form.ApiFormat) && (
                <div className="form-group">
                  <label className="checkbox-label">
                    <input type="checkbox" checked={form.HealthCheckUseAuth} onChange={e => { setHealthFieldsEdited(true); setForm({ ...form, HealthCheckUseAuth: e.target.checked }); }} />
                    {' '}
                    <Tooltip content="Send the endpoint credential with health check requests, using the provider's header (Authorization, api-key, x-api-key, or x-goog-api-key).">
                      Authenticate Health Checks
                    </Tooltip>
                  </label>
                </div>
                )}
              </>
            )}
          </div>

          </>
          )}

          <div className="btn-group" style={{ marginTop: 16 }}>
            <button className="primary" onClick={handleSave} disabled={!form.ApiFormat}>Save</button>
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
        title="Inference Endpoint JSON"
        data={jsonModal.data}
      />
      <LoadModelModal
        isOpen={loadModal.isOpen}
        endpoint={loadModal.endpoint}
        endpointType="Completion"
        onClose={() => setLoadModal({ isOpen: false, endpoint: null })}
        onLoad={(id, request) => api.loadCompletionEndpoint(id, request)}
        onComplete={() => { load(); loadHealth(); }}
        onLoadingChange={(id, isLoading) => setLoadingModelId(isLoading ? id : null)}
      />
      <HealthDetailModal
        isOpen={healthDetailModal.isOpen}
        onClose={() => setHealthDetailModal({ isOpen: false, data: null })}
        healthData={healthDetailModal.data}
      />
      <ValidateEndpointModal
        isOpen={validateModal.isOpen}
        endpoint={validateModal.endpoint}
        endpointType="Completion"
        onClose={() => setValidateModal({ isOpen: false, endpoint: null })}
        onValidate={(request) => api.exploreCompletionEndpoint(request)}
      />
    </div>
  );
}
