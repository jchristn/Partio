import React, { useState, useEffect, useCallback } from 'react';
import { useApp } from '../context/AppContext';
import { PartioApi } from '../utils/api';
import Modal from './Modal';
import CopyableId from './CopyableId';
import ActionMenu from './ActionMenu';
import Pagination from './Pagination';
import './CredentialsView.css';

export default function CredentialsView() {
  const { serverUrl, bearerToken } = useApp();
  const api = new PartioApi(serverUrl, bearerToken);
  const [data, setData] = useState([]);
  const [hasMore, setHasMore] = useState(false);
  const [continuationToken, setContinuationToken] = useState(null);
  const [loading, setLoading] = useState(false);
  const [showModal, setShowModal] = useState(false);
  const [form, setForm] = useState({ TenantId: 'default', UserId: 'default', Name: '' });

  const load = useCallback(async (token = null) => {
    setLoading(true);
    try {
      const result = await api.enumerateCredentials({ MaxResults: 25, ContinuationToken: token });
      setData(result.Data || []);
      setHasMore(result.HasMore);
      setContinuationToken(result.ContinuationToken);
    } catch (err) { console.error(err); }
    setLoading(false);
  }, [serverUrl, bearerToken]);

  useEffect(() => { load(); }, [load]);

  const openCreate = () => {
    setForm({ TenantId: 'default', UserId: 'default', Name: '' });
    setShowModal(true);
  };

  const handleSave = async () => {
    try {
      await api.createCredential({ TenantId: form.TenantId, UserId: form.UserId, Name: form.Name });
      setShowModal(false);
      load();
    } catch (err) { alert(err.message); }
  };

  const handleDelete = async (id) => {
    if (!confirm('Delete this credential?')) return;
    try { await api.deleteCredential(id); load(); } catch (err) { alert(err.message); }
  };

  return (
    <div>
      <div className="header-row">
        <h2>Credentials</h2>
        <button className="primary" onClick={openCreate}>Create Credential</button>
      </div>
      {data.length === 0 && !loading ? (
        <div className="empty-state">No credentials found.</div>
      ) : (
        <table>
          <thead><tr><th>ID</th><th>Name</th><th>Bearer Token</th><th>Status</th><th></th></tr></thead>
          <tbody>
            {data.map(item => (
              <tr key={item.Id}>
                <td><CopyableId value={item.Id} /></td>
                <td>{item.Name || '-'}</td>
                <td><CopyableId value={item.BearerToken} /></td>
                <td><span className={`status-badge ${item.Active ? 'active' : 'inactive'}`}>{item.Active ? 'Active' : 'Inactive'}</span></td>
                <td><ActionMenu actions={[{ label: 'Delete', onClick: () => handleDelete(item.Id) }]} /></td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
      <Pagination hasMore={hasMore} onNext={() => load(continuationToken)} onReset={() => load()} loading={loading} />
      {showModal && (
        <Modal title="Create Credential" onClose={() => setShowModal(false)}>
          <div className="form-group"><label>Tenant ID</label><input value={form.TenantId} onChange={e => setForm({ ...form, TenantId: e.target.value })} /></div>
          <div className="form-group"><label>User ID</label><input value={form.UserId} onChange={e => setForm({ ...form, UserId: e.target.value })} /></div>
          <div className="form-group"><label>Name</label><input value={form.Name} onChange={e => setForm({ ...form, Name: e.target.value })} /></div>
          <div className="btn-group" style={{ marginTop: 16 }}><button className="primary" onClick={handleSave}>Save</button><button className="secondary" onClick={() => setShowModal(false)}>Cancel</button></div>
        </Modal>
      )}
    </div>
  );
}
