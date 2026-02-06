import React, { useState, useEffect, useCallback } from 'react';
import { useApp } from '../context/AppContext';
import { PartioApi } from '../utils/api';
import Modal from './Modal';
import CopyableId from './CopyableId';
import ActionMenu from './ActionMenu';
import Pagination from './Pagination';
import './EmbeddingEndpointsView.css';

export default function EmbeddingEndpointsView() {
  const { serverUrl, bearerToken } = useApp();
  const api = new PartioApi(serverUrl, bearerToken);
  const [data, setData] = useState([]);
  const [hasMore, setHasMore] = useState(false);
  const [continuationToken, setContinuationToken] = useState(null);
  const [loading, setLoading] = useState(false);
  const [showModal, setShowModal] = useState(false);
  const [editing, setEditing] = useState(null);
  const [form, setForm] = useState({ TenantId: 'default', Model: '', Endpoint: '', ApiFormat: 'Ollama', ApiKey: '' });

  const load = useCallback(async (token = null) => {
    setLoading(true);
    try {
      const result = await api.enumerateEndpoints({ MaxResults: 25, ContinuationToken: token });
      setData(result.Data || []);
      setHasMore(result.HasMore);
      setContinuationToken(result.ContinuationToken);
    } catch (err) { console.error(err); }
    setLoading(false);
  }, [serverUrl, bearerToken]);

  useEffect(() => { load(); }, [load]);

  const openCreate = () => {
    setEditing(null);
    setForm({ TenantId: 'default', Model: '', Endpoint: '', ApiFormat: 'Ollama', ApiKey: '' });
    setShowModal(true);
  };

  const openEdit = (item) => {
    setEditing(item);
    setForm({ TenantId: item.TenantId, Model: item.Model, Endpoint: item.Endpoint, ApiFormat: item.ApiFormat, ApiKey: item.ApiKey || '' });
    setShowModal(true);
  };

  const handleSave = async () => {
    try {
      if (editing) {
        await api.updateEndpoint(editing.Id, { ...editing, Model: form.Model, Endpoint: form.Endpoint, ApiFormat: form.ApiFormat, ApiKey: form.ApiKey || null });
      } else {
        await api.createEndpoint({ TenantId: form.TenantId, Model: form.Model, Endpoint: form.Endpoint, ApiFormat: form.ApiFormat, ApiKey: form.ApiKey || null });
      }
      setShowModal(false);
      load();
    } catch (err) { alert(err.message); }
  };

  const handleDelete = async (id) => {
    if (!confirm('Delete this endpoint?')) return;
    try { await api.deleteEndpoint(id); load(); } catch (err) { alert(err.message); }
  };

  return (
    <div>
      <div className="header-row">
        <h2>Embedding Endpoints</h2>
        <button className="primary" onClick={openCreate}>Create Endpoint</button>
      </div>
      {data.length === 0 && !loading ? (
        <div className="empty-state">No endpoints found.</div>
      ) : (
        <table>
          <thead><tr><th>ID</th><th>Model</th><th>Endpoint</th><th>Format</th><th>Status</th><th></th></tr></thead>
          <tbody>
            {data.map(item => (
              <tr key={item.Id}>
                <td><CopyableId value={item.Id} /></td>
                <td>{item.Model}</td>
                <td>{item.Endpoint}</td>
                <td>{item.ApiFormat}</td>
                <td><span className={`status-badge ${item.Active ? 'active' : 'inactive'}`}>{item.Active ? 'Active' : 'Inactive'}</span></td>
                <td><ActionMenu actions={[{ label: 'Edit', onClick: () => openEdit(item) }, { label: 'Delete', onClick: () => handleDelete(item.Id) }]} /></td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
      <Pagination hasMore={hasMore} onNext={() => load(continuationToken)} onReset={() => load()} loading={loading} />
      {showModal && (
        <Modal title={editing ? 'Edit Endpoint' : 'Create Endpoint'} onClose={() => setShowModal(false)}>
          {!editing && <div className="form-group"><label>Tenant ID</label><input value={form.TenantId} onChange={e => setForm({ ...form, TenantId: e.target.value })} /></div>}
          <div className="form-group"><label>Model</label><input value={form.Model} onChange={e => setForm({ ...form, Model: e.target.value })} placeholder="e.g. all-minilm" /></div>
          <div className="form-group"><label>Endpoint</label><input value={form.Endpoint} onChange={e => setForm({ ...form, Endpoint: e.target.value })} placeholder="http://localhost:11434" /></div>
          <div className="form-group"><label>API Format</label><select value={form.ApiFormat} onChange={e => setForm({ ...form, ApiFormat: e.target.value })}><option value="Ollama">Ollama</option><option value="OpenAI">OpenAI</option></select></div>
          <div className="form-group"><label>API Key (optional)</label><input type="password" value={form.ApiKey} onChange={e => setForm({ ...form, ApiKey: e.target.value })} /></div>
          <div className="btn-group" style={{ marginTop: 16 }}><button className="primary" onClick={handleSave}>Save</button><button className="secondary" onClick={() => setShowModal(false)}>Cancel</button></div>
        </Modal>
      )}
    </div>
  );
}
