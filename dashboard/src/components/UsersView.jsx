import React, { useState, useEffect, useCallback } from 'react';
import { useApp } from '../context/AppContext';
import { PartioApi } from '../utils/api';
import Modal from './Modal';
import CopyableId from './CopyableId';
import ActionMenu from './ActionMenu';
import Pagination from './Pagination';
import './UsersView.css';

export default function UsersView() {
  const { serverUrl, bearerToken } = useApp();
  const api = new PartioApi(serverUrl, bearerToken);
  const [data, setData] = useState([]);
  const [hasMore, setHasMore] = useState(false);
  const [continuationToken, setContinuationToken] = useState(null);
  const [loading, setLoading] = useState(false);
  const [showModal, setShowModal] = useState(false);
  const [editing, setEditing] = useState(null);
  const [form, setForm] = useState({ TenantId: 'default', Email: '', Password: '', FirstName: '', LastName: '', IsAdmin: false });

  const load = useCallback(async (token = null) => {
    setLoading(true);
    try {
      const result = await api.enumerateUsers({ MaxResults: 25, ContinuationToken: token });
      setData(result.Data || []);
      setHasMore(result.HasMore);
      setContinuationToken(result.ContinuationToken);
    } catch (err) { console.error(err); }
    setLoading(false);
  }, [serverUrl, bearerToken]);

  useEffect(() => { load(); }, [load]);

  const openCreate = () => {
    setEditing(null);
    setForm({ TenantId: 'default', Email: '', Password: '', FirstName: '', LastName: '', IsAdmin: false });
    setShowModal(true);
  };

  const openEdit = (item) => {
    setEditing(item);
    setForm({ TenantId: item.TenantId, Email: item.Email, Password: '', FirstName: item.FirstName || '', LastName: item.LastName || '', IsAdmin: item.IsAdmin });
    setShowModal(true);
  };

  const handleSave = async () => {
    try {
      if (editing) {
        const update = { ...editing, Email: form.Email, FirstName: form.FirstName, LastName: form.LastName, IsAdmin: form.IsAdmin };
        if (form.Password) update.Password = form.Password;
        await api.updateUser(editing.Id, update);
      } else {
        await api.createUser({ TenantId: form.TenantId, Email: form.Email, Password: form.Password, FirstName: form.FirstName, LastName: form.LastName, IsAdmin: form.IsAdmin });
      }
      setShowModal(false);
      load();
    } catch (err) { alert(err.message); }
  };

  const handleDelete = async (id) => {
    if (!confirm('Delete this user?')) return;
    try { await api.deleteUser(id); load(); } catch (err) { alert(err.message); }
  };

  return (
    <div>
      <div className="header-row">
        <h2>Users</h2>
        <button className="primary" onClick={openCreate}>Create User</button>
      </div>
      {data.length === 0 && !loading ? (
        <div className="empty-state">No users found.</div>
      ) : (
        <table>
          <thead><tr><th>ID</th><th>Email</th><th>Name</th><th>Admin</th><th>Status</th><th></th></tr></thead>
          <tbody>
            {data.map(item => (
              <tr key={item.Id}>
                <td><CopyableId value={item.Id} /></td>
                <td>{item.Email}</td>
                <td>{[item.FirstName, item.LastName].filter(Boolean).join(' ') || '-'}</td>
                <td>{item.IsAdmin ? 'Yes' : 'No'}</td>
                <td><span className={`status-badge ${item.Active ? 'active' : 'inactive'}`}>{item.Active ? 'Active' : 'Inactive'}</span></td>
                <td><ActionMenu actions={[{ label: 'Edit', onClick: () => openEdit(item) }, { label: 'Delete', onClick: () => handleDelete(item.Id) }]} /></td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
      <Pagination hasMore={hasMore} onNext={() => load(continuationToken)} onReset={() => load()} loading={loading} />
      {showModal && (
        <Modal title={editing ? 'Edit User' : 'Create User'} onClose={() => setShowModal(false)}>
          {!editing && <div className="form-group"><label>Tenant ID</label><input value={form.TenantId} onChange={e => setForm({ ...form, TenantId: e.target.value })} /></div>}
          <div className="form-group"><label>Email</label><input value={form.Email} onChange={e => setForm({ ...form, Email: e.target.value })} /></div>
          <div className="form-group"><label>Password {editing ? '(leave blank to keep)' : ''}</label><input type="password" value={form.Password} onChange={e => setForm({ ...form, Password: e.target.value })} /></div>
          <div className="form-group"><label>First Name</label><input value={form.FirstName} onChange={e => setForm({ ...form, FirstName: e.target.value })} /></div>
          <div className="form-group"><label>Last Name</label><input value={form.LastName} onChange={e => setForm({ ...form, LastName: e.target.value })} /></div>
          <div className="form-group"><label><input type="checkbox" checked={form.IsAdmin} onChange={e => setForm({ ...form, IsAdmin: e.target.checked })} /> Admin</label></div>
          <div className="btn-group" style={{ marginTop: 16 }}><button className="primary" onClick={handleSave}>Save</button><button className="secondary" onClick={() => setShowModal(false)}>Cancel</button></div>
        </Modal>
      )}
    </div>
  );
}
