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
import './UsersView.css';

export default function UsersView() {
  const { serverUrl, bearerToken } = useApp();
  const api = new PartioApi(serverUrl, bearerToken);
  const [data, setData] = useState([]);
  const [loading, setLoading] = useState(false);
  const [showModal, setShowModal] = useState(false);
  const [editing, setEditing] = useState(null);
  const [form, setForm] = useState({ TenantId: 'default', Email: '', Password: '', FirstName: '', LastName: '', IsAdmin: false });
  const [alertModal, setAlertModal] = useState({ isOpen: false, message: '', type: 'error' });
  const [deleteModal, setDeleteModal] = useState({ isOpen: false, id: null });
  const [tenants, setTenants] = useState([]);
  const [jsonModal, setJsonModal] = useState({ isOpen: false, data: null });

  const loadTenants = useCallback(async () => {
    try {
      const result = await api.enumerateTenants({ MaxResults: 1000 });
      setTenants(result.Data || []);
    } catch (err) { console.error(err); }
  }, [serverUrl, bearerToken]);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const result = await api.enumerateUsers({ MaxResults: 1000 });
      setData(result.Data || []);
    } catch (err) { console.error(err); }
    setLoading(false);
  }, [serverUrl, bearerToken]);

  useEffect(() => { load(); loadTenants(); }, [load, loadTenants]);

  const openCreate = () => {
    setEditing(null);
    const tenantId = tenants.length > 0 ? tenants[0].Id : '';
    setForm({ TenantId: tenantId, Email: '', Password: '', FirstName: '', LastName: '', IsAdmin: false });
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
    } catch (err) { setAlertModal({ isOpen: true, message: err.message, type: 'error' }); }
  };

  const handleDelete = async () => {
    try {
      await api.deleteUser(deleteModal.id);
      setDeleteModal({ isOpen: false, id: null });
      load();
    } catch (err) {
      setDeleteModal({ isOpen: false, id: null });
      setAlertModal({ isOpen: true, message: err.message, type: 'error' });
    }
  };

  const columns = [
    {
      key: 'Id',
      label: 'ID',
      width: '280px',
      render: (item) => <CopyableId value={item.Id} />
    },
    {
      key: 'Email',
      label: 'Email'
    },
    {
      key: 'Name',
      label: 'Name',
      render: (item) => [item.FirstName, item.LastName].filter(Boolean).join(' ') || '-',
      filterValue: (item) => [item.FirstName, item.LastName].filter(Boolean).join(' ')
    },
    {
      key: 'IsAdmin',
      label: 'Admin',
      filterValue: (item) => item.IsAdmin ? 'Yes' : 'No',
      render: (item) => item.IsAdmin ? 'Yes' : 'No'
    },
    {
      key: 'Active',
      label: 'Status',
      filterValue: (item) => item.Active ? 'Active' : 'Inactive',
      render: (item) => (
        <span className={`status-badge ${item.Active ? 'active' : 'inactive'}`}>
          {item.Active ? 'Active' : 'Inactive'}
        </span>
      )
    },
    {
      key: 'actions',
      label: 'Actions',
      isAction: true,
      sortable: false,
      render: (item) => (
        <ActionMenu actions={[
          { label: 'Edit', onClick: () => openEdit(item) },
          { label: 'View JSON', onClick: () => setJsonModal({ isOpen: true, data: item }) },
          { divider: true },
          { label: 'Delete', danger: true, onClick: () => setDeleteModal({ isOpen: true, id: item.Id }) }
        ]} />
      )
    }
  ];

  return (
    <div>
      <div className="header-row">
        <h2>Users</h2>
        <div className="header-row-actions">
          <button className="refresh-btn" onClick={load} title="Refresh">
            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
              <polyline points="23 4 23 10 17 10" /><polyline points="1 20 1 14 7 14" />
              <path d="M3.51 9a9 9 0 0114.85-3.36L23 10M1 14l4.64 4.36A9 9 0 0020.49 15" />
            </svg>
          </button>
          <button className="primary" onClick={openCreate}>Create User</button>
        </div>
      </div>
      <DataTable data={data} columns={columns} loading={loading} />
      {showModal && (
        <Modal title={editing ? 'Edit User' : 'Create User'} onClose={() => setShowModal(false)}>
          {!editing && <div className="form-group"><label>Tenant</label><select value={form.TenantId} onChange={e => setForm({ ...form, TenantId: e.target.value })}>{tenants.map(t => <option key={t.Id} value={t.Id}>{t.Name || t.Id}</option>)}</select></div>}
          <div className="form-group"><label>Email</label><input value={form.Email} onChange={e => setForm({ ...form, Email: e.target.value })} /></div>
          <div className="form-group"><label>Password {editing ? '(leave blank to keep)' : ''}</label><input type="password" value={form.Password} onChange={e => setForm({ ...form, Password: e.target.value })} /></div>
          <div className="form-group"><label>First Name</label><input value={form.FirstName} onChange={e => setForm({ ...form, FirstName: e.target.value })} /></div>
          <div className="form-group"><label>Last Name</label><input value={form.LastName} onChange={e => setForm({ ...form, LastName: e.target.value })} /></div>
          <div className="form-group"><label><input type="checkbox" checked={form.IsAdmin} onChange={e => setForm({ ...form, IsAdmin: e.target.checked })} style={{ width: 'auto', marginRight: 8 }} /> Admin</label></div>
          <div className="btn-group" style={{ marginTop: 16 }}><button className="primary" onClick={handleSave}>Save</button><button className="secondary" onClick={() => setShowModal(false)}>Cancel</button></div>
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
        entityType="user"
      />
      <JsonViewModal
        isOpen={jsonModal.isOpen}
        onClose={() => setJsonModal({ isOpen: false, data: null })}
        title="User JSON"
        data={jsonModal.data}
      />
    </div>
  );
}
