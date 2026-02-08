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
import { copyToClipboard } from '../utils/clipboard';
import './RequestHistoryView.css';

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

  const handleCopy = async (e) => {
    e.stopPropagation();
    if (isEmpty) return;
    const success = await copyToClipboard(typeof content === 'string' ? content : JSON.stringify(content, null, 2));
    if (success) {
      setCopied(true);
      setTimeout(() => setCopied(false), 1500);
    }
  };

  let isJson = false;
  if (!isEmpty) {
    try { JSON.parse(content); isJson = true; } catch {}
  }

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

function parseEndpointId(url) {
  if (!url) return null;
  const match = url.match(/\/v1\.0\/endpoints\/([^/]+)\//);
  return match ? match[1] : null;
}

function buildFullEndpointUrl(endpoint) {
  if (!endpoint || !endpoint.Endpoint) return null;
  const base = endpoint.Endpoint.replace(/\/+$/, '');
  switch (endpoint.ApiFormat) {
    case 'Ollama': return base + '/api/embed';
    case 'OpenAI': return base + '/v1/embeddings';
    default: return base;
  }
}

function formatHeaders(headers) {
  if (!headers || typeof headers !== 'object') return null;
  const entries = Object.entries(headers);
  if (entries.length === 0) return null;
  return entries.map(([k, v]) => `${k}: ${v}`).join('\n');
}

export default function RequestHistoryView() {
  const { serverUrl, bearerToken } = useApp();
  const api = new PartioApi(serverUrl, bearerToken);
  const [data, setData] = useState([]);
  const [loading, setLoading] = useState(false);
  const [detailModal, setDetailModal] = useState({ isOpen: false, item: null, detail: null, endpoint: null, loading: false, error: null });
  const [alertModal, setAlertModal] = useState({ isOpen: false, message: '', type: 'error' });
  const [deleteModal, setDeleteModal] = useState({ isOpen: false, id: null });
  const [jsonModal, setJsonModal] = useState({ isOpen: false, data: null });

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const result = await api.enumerateRequestHistory({ MaxResults: 1000 });
      setData(result.Data || []);
    } catch (err) { console.error(err); }
    setLoading(false);
  }, [serverUrl, bearerToken]);

  useEffect(() => { load(); }, [load]);

  const viewDetail = async (item) => {
    setDetailModal({ isOpen: true, item, detail: null, endpoint: null, loading: true, error: null });

    const endpointId = parseEndpointId(item.HttpUrl);

    const [detailResult, endpointResult] = await Promise.allSettled([
      api.getRequestHistoryDetail(item.Id),
      endpointId ? api.getEndpoint(endpointId) : Promise.resolve(null)
    ]);

    setDetailModal(prev => ({
      ...prev,
      detail: detailResult.status === 'fulfilled' ? detailResult.value : null,
      endpoint: endpointResult.status === 'fulfilled' ? endpointResult.value : null,
      loading: false,
      error: detailResult.status === 'rejected' ? detailResult.reason.message : null
    }));
  };

  const handleDelete = async () => {
    try {
      await api.deleteRequestHistory(deleteModal.id);
      setDeleteModal({ isOpen: false, id: null });
      load();
    } catch (err) {
      setDeleteModal({ isOpen: false, id: null });
      setAlertModal({ isOpen: true, message: err.message, type: 'error' });
    }
  };

  const formatBytes = (bytes) => {
    if (bytes == null) return '-';
    if (bytes < 1024) return bytes + ' B';
    if (bytes < 1048576) return (bytes / 1024).toFixed(1) + ' KB';
    return (bytes / 1048576).toFixed(1) + ' MB';
  };

  const columns = [
    {
      key: 'Id',
      label: 'ID',
      width: '280px',
      render: (item) => <CopyableId value={item.Id} />
    },
    {
      key: 'HttpMethod',
      label: 'Method'
    },
    {
      key: 'HttpUrl',
      label: 'URL',
      render: (item) => (
        <span style={{ maxWidth: 200, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap', display: 'inline-block' }}>
          {item.HttpUrl}
        </span>
      )
    },
    {
      key: 'HttpStatus',
      label: 'Status',
      render: (item) => item.HttpStatus
        ? <span className={`http-status ${statusClass(item.HttpStatus)}`}>{item.HttpStatus}</span>
        : '-'
    },
    {
      key: 'ResponseTimeMs',
      label: 'Time (ms)',
      render: (item) => item.ResponseTimeMs != null ? item.ResponseTimeMs : '-',
      sortValue: (item) => item.ResponseTimeMs
    },
    {
      key: 'CreatedUtc',
      label: 'Created',
      render: (item) => new Date(item.CreatedUtc).toLocaleString()
    },
    {
      key: 'actions',
      label: 'Actions',
      isAction: true,
      sortable: false,
      render: (item) => (
        <ActionMenu actions={[
          { label: 'View Detail', onClick: () => viewDetail(item) },
          { label: 'View JSON', onClick: () => setJsonModal({ isOpen: true, data: item }) },
          { divider: true },
          { label: 'Delete', danger: true, onClick: () => setDeleteModal({ isOpen: true, id: item.Id }) }
        ]} />
      )
    }
  ];

  const { isOpen: detailOpen, item: detailItem, detail: detailData, endpoint: detailEndpoint, loading: detailLoading, error: detailError } = detailModal;

  return (
    <div>
      <div className="header-row">
        <h2>Request History</h2>
        <div className="header-row-actions">
          <button className="refresh-btn" onClick={load} title="Refresh">
            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
              <polyline points="23 4 23 10 17 10" /><polyline points="1 20 1 14 7 14" />
              <path d="M3.51 9a9 9 0 0114.85-3.36L23 10M1 14l4.64 4.36A9 9 0 0020.49 15" />
            </svg>
          </button>
        </div>
      </div>
      <DataTable data={data} columns={columns} loading={loading} />

      {detailOpen && detailItem && (
        <Modal title="Request Detail" onClose={() => setDetailModal({ isOpen: false, item: null, detail: null, endpoint: null, loading: false, error: null })} className="modal-extra-wide">
          <div className="detail-content">
            <div className="detail-header-row">
              <div className="detail-id">
                <CopyableId value={detailItem.Id} />
              </div>
            </div>

            <div className="detail-section">
              <h3>Overview</h3>
              <div className="detail-grid">
                <div className="detail-item">
                  <label>Method</label>
                  <span>{detailItem.HttpMethod || '-'}</span>
                </div>
                <div className="detail-item">
                  <label>Status</label>
                  {detailItem.HttpStatus
                    ? <span className={`http-status ${statusClass(detailItem.HttpStatus)}`}>{detailItem.HttpStatus}</span>
                    : <span>-</span>
                  }
                </div>
                <div className="detail-item">
                  <label>Response Time</label>
                  <span>{detailItem.ResponseTimeMs != null ? detailItem.ResponseTimeMs + ' ms' : '-'}</span>
                </div>
                <div className="detail-item detail-item-wide">
                  <label>Request URL</label>
                  <code>{detailItem.HttpUrl || '-'}</code>
                </div>
                {detailEndpoint && (
                  <div className="detail-item detail-item-wide">
                    <label>Endpoint URL</label>
                    <code>{buildFullEndpointUrl(detailEndpoint) || detailEndpoint.Endpoint || '-'}</code>
                  </div>
                )}
              </div>
            </div>

            <div className="detail-section">
              <h3>Context</h3>
              <div className="detail-grid">
                <div className="detail-item">
                  <label>Requestor IP</label>
                  <span>{detailItem.RequestorIp || '-'}</span>
                </div>
                <div className="detail-item">
                  <label>Tenant ID</label>
                  <span>{detailItem.TenantId ? <CopyableId value={detailItem.TenantId} /> : '-'}</span>
                </div>
                <div className="detail-item">
                  <label>User ID</label>
                  <span>{detailItem.UserId ? <CopyableId value={detailItem.UserId} /> : '-'}</span>
                </div>
                <div className="detail-item">
                  <label>Credential ID</label>
                  <span>{detailItem.CredentialId ? <CopyableId value={detailItem.CredentialId} /> : '-'}</span>
                </div>
              </div>
            </div>

            <div className="detail-section">
              <h3>Timing</h3>
              <div className="detail-grid">
                <div className="detail-item">
                  <label>Created</label>
                  <span>{new Date(detailItem.CreatedUtc).toLocaleString()}</span>
                </div>
                <div className="detail-item">
                  <label>Completed</label>
                  <span>{detailItem.CompletedUtc ? new Date(detailItem.CompletedUtc).toLocaleString() : '-'}</span>
                </div>
                <div className="detail-item">
                  <label>Request Body Size</label>
                  <span>{formatBytes(detailItem.RequestBodyLength)}</span>
                </div>
                <div className="detail-item">
                  <label>Response Body Size</label>
                  <span>{formatBytes(detailItem.ResponseBodyLength)}</span>
                </div>
              </div>
            </div>

            <div className="detail-section">
              <h3>Request</h3>
              {detailLoading && <div className="detail-loading">Loading detail...</div>}
              {detailError && <div className="detail-error">{detailError}</div>}
              {!detailLoading && !detailError && detailData && (
                <>
                  <CollapsibleSection
                    title="Request Headers"
                    content={formatHeaders(detailData.RequestHeaders)}
                    defaultExpanded={false}
                  />
                  <CollapsibleSection
                    title="Request Body"
                    content={typeof detailData.RequestBody === 'string' ? detailData.RequestBody : detailData.RequestBody != null ? JSON.stringify(detailData.RequestBody) : null}
                    defaultExpanded={false}
                  />
                </>
              )}
            </div>

            <div className="detail-section">
              <h3>Response</h3>
              {!detailLoading && !detailError && detailData && (
                <>
                  <CollapsibleSection
                    title="Response Headers"
                    content={formatHeaders(detailData.ResponseHeaders)}
                    defaultExpanded={false}
                  />
                  <CollapsibleSection
                    title="Response Body"
                    content={typeof detailData.ResponseBody === 'string' ? detailData.ResponseBody : detailData.ResponseBody != null ? JSON.stringify(detailData.ResponseBody) : null}
                    defaultExpanded={false}
                  />
                </>
              )}
              {!detailLoading && !detailError && !detailData && (
                <div className="detail-empty">No detail available</div>
              )}
            </div>

            {!detailLoading && !detailError && detailData && detailData.EmbeddingCalls && detailData.EmbeddingCalls.length > 0 && (
              <div className="detail-section">
                <h3>Upstream Embedding Calls</h3>
                {detailData.EmbeddingCalls.map((call, idx) => (
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
                      {!call.Success && (
                        <span className="embedding-call-failed">FAILED</span>
                      )}
                    </div>
                    {call.Error && (
                      <div className="embedding-call-error">{call.Error}</div>
                    )}
                    <CollapsibleSection
                      title="Request Headers"
                      content={formatHeaders(call.RequestHeaders)}
                      defaultExpanded={false}
                    />
                    <CollapsibleSection
                      title="Request Body"
                      content={typeof call.RequestBody === 'string' ? call.RequestBody : call.RequestBody != null ? JSON.stringify(call.RequestBody) : null}
                      defaultExpanded={false}
                    />
                    <CollapsibleSection
                      title="Response Headers"
                      content={formatHeaders(call.ResponseHeaders)}
                      defaultExpanded={false}
                    />
                    <CollapsibleSection
                      title="Response Body"
                      content={typeof call.ResponseBody === 'string' ? call.ResponseBody : call.ResponseBody != null ? JSON.stringify(call.ResponseBody) : null}
                      defaultExpanded={false}
                    />
                  </div>
                ))}
              </div>
            )}
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
        entityType="entry"
      />
      <JsonViewModal
        isOpen={jsonModal.isOpen}
        onClose={() => setJsonModal({ isOpen: false, data: null })}
        title="Request History JSON"
        data={jsonModal.data}
      />
    </div>
  );
}
