import React, { useState, useEffect, useCallback } from 'react';
import { useApp } from '../context/AppContext';
import { PartioApi } from '../utils/api';
import Modal from './Modal';
import CopyableId from './CopyableId';
import ActionMenu from './ActionMenu';
import Pagination from './Pagination';
import TagInput from './TagInput';
import KeyValueEditor from './KeyValueEditor';
import './TenantsView.css';

export default function TenantsView() {
  const { serverUrl, bearerToken } = useApp();
  const api = new PartioApi(serverUrl, bearerToken);
  const [data, setData] = useState([]);
  const [hasMore, setHasMore] = useState(false);
  const [continuationToken, setContinuationToken] = useState(null);
  const [loading, setLoading] = useState(false);
  const [showModal, setShowModal] = useState(false);
  const [editing, setEditing] = useState(null);
  const [form, setForm] = useState({ Name: '', Labels: [], Tags: {} });

  const load = useCallback(async (token = null) => {
    setLoading(true);
    try {
      const result = await api.enumerateTenants({ MaxResults: 25, ContinuationToken: token });
      setData(result.Data || []);
      setHasMore(result.HasMore);
      setContinuationToken(result.ContinuationToken);
    } catch (err) {
      console.error(err);
    }
    setLoading(false);
  }, [serverUrl, bearerToken]);

  useEffect(() => { load(); }, [load]);

  const openCreate = () => {
    setEditing(null);
    setForm({ Name: '', Labels: [], Tags: {} });
    setShowModal(true);
  };

  const openEdit = (item) => {
    setEditing(item);
    setForm({ Name: item.Name, Labels: item.Labels || [], Tags: item.Tags || {} });
    setShowModal(true);
  };

  const handleSave = async () => {
    try {
      if (editing) {
        await api.updateTenant(editing.Id, { ...editing, Name: form.Name, Labels: form.Labels, Tags: form.Tags });
      } else {
        await api.createTenant({ Name: form.Name, Labels: form.Labels, Tags: form.Tags });
      }
      setShowModal(false);
      load();
    } catch (err) {
      alert(err.message);
    }
  };

  const handleDelete = async (id) => {
    if (!confirm('Delete this tenant?')) return;
    try {
      await api.deleteTenant(id);
      load();
    } catch (err) {
      alert(err.message);
    }
  };

  return (
    <div>
      <div className="header-row">
        <h2>Tenants</h2>
        <button className="primary" onClick={openCreate}>Create Tenant</button>
      </div>
      {data.length === 0 && !loading ? (
        <div className="empty-state">No tenants found.</div>
      ) : (
        <table>
          <thead>
            <tr>
              <th>ID</th>
              <th>Name</th>
              <th>Status</th>
              <th>Created</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            {data.map(item => (
              <tr key={item.Id}>
                <td><CopyableId value={item.Id} /></td>
                <td>{item.Name}</td>
                <td><span className={`status-badge ${item.Active ? 'active' : 'inactive'}`}>{item.Active ? 'Active' : 'Inactive'}</span></td>
                <td>{new Date(item.CreatedUtc).toLocaleDateString()}</td>
                <td>
                  <ActionMenu actions={[
                    { label: 'Edit', onClick: () => openEdit(item) },
                    { label: 'Delete', onClick: () => handleDelete(item.Id) }
                  ]} />
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
      <Pagination hasMore={hasMore} onNext={() => load(continuationToken)} onReset={() => load()} loading={loading} />
      {showModal && (
        <Modal title={editing ? 'Edit Tenant' : 'Create Tenant'} onClose={() => setShowModal(false)}>
          <div className="form-group">
            <label>Name</label>
            <input value={form.Name} onChange={e => setForm({ ...form, Name: e.target.value })} />
          </div>
          <div className="form-group">
            <label>Labels</label>
            <TagInput value={form.Labels} onChange={labels => setForm({ ...form, Labels: labels })} />
          </div>
          <div className="form-group">
            <label>Tags</label>
            <KeyValueEditor value={form.Tags} onChange={tags => setForm({ ...form, Tags: tags })} />
          </div>
          <div className="btn-group" style={{ marginTop: 16 }}>
            <button className="primary" onClick={handleSave}>Save</button>
            <button className="secondary" onClick={() => setShowModal(false)}>Cancel</button>
          </div>
        </Modal>
      )}
    </div>
  );
}
