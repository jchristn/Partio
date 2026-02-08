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
import './CredentialsView.css';

export default function CredentialsView() {
  const { serverUrl, bearerToken } = useApp();
  const api = new PartioApi(serverUrl, bearerToken);
  const [data, setData] = useState([]);
  const [loading, setLoading] = useState(false);
  const [showModal, setShowModal] = useState(false);
  const [form, setForm] = useState({ TenantId: 'default', UserId: 'default', Name: '' });
  const [alertModal, setAlertModal] = useState({ isOpen: false, message: '', type: 'error' });
  const [deleteModal, setDeleteModal] = useState({ isOpen: false, id: null });
  const [tenants, setTenants] = useState([]);
  const [users, setUsers] = useState([]);
  const [jsonModal, setJsonModal] = useState({ isOpen: false, data: null });

  const loadTenants = useCallback(async () => {
    try {
      const result = await api.enumerateTenants({ MaxResults: 1000 });
      setTenants(result.Data || []);
    } catch (err) { console.error(err); }
  }, [serverUrl, bearerToken]);

  const loadUsers = useCallback(async () => {
    try {
      const result = await api.enumerateUsers({ MaxResults: 1000 });
      setUsers(result.Data || []);
    } catch (err) { console.error(err); }
  }, [serverUrl, bearerToken]);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const result = await api.enumerateCredentials({ MaxResults: 1000 });
      setData(result.Data || []);
    } catch (err) { console.error(err); }
    setLoading(false);
  }, [serverUrl, bearerToken]);

  useEffect(() => { load(); loadTenants(); loadUsers(); }, [load, loadTenants, loadUsers]);

  const openCreate = () => {
    const tenantId = tenants.length > 0 ? tenants[0].Id : '';
    const tenantUsers = users.filter(u => u.TenantId === tenantId);
    const userId = tenantUsers.length > 0 ? tenantUsers[0].Id : '';
    setForm({ TenantId: tenantId, UserId: userId, Name: '' });
    setShowModal(true);
  };

  const handleSave = async () => {
    try {
      await api.createCredential({ TenantId: form.TenantId, UserId: form.UserId, Name: form.Name });
      setShowModal(false);
      load();
    } catch (err) { setAlertModal({ isOpen: true, message: err.message, type: 'error' }); }
  };

  const handleDelete = async () => {
    try {
      await api.deleteCredential(deleteModal.id);
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
      key: 'Name',
      label: 'Name',
      render: (item) => item.Name || '-'
    },
    {
      key: 'BearerToken',
      label: 'Bearer Token',
      render: (item) => <CopyableId value={item.BearerToken} />
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
        <h2>Credentials</h2>
        <div className="header-row-actions">
          <button className="refresh-btn" onClick={load} title="Refresh">
            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
              <polyline points="23 4 23 10 17 10" /><polyline points="1 20 1 14 7 14" />
              <path d="M3.51 9a9 9 0 0114.85-3.36L23 10M1 14l4.64 4.36A9 9 0 0020.49 15" />
            </svg>
          </button>
          <button className="primary" onClick={openCreate}>Create Credential</button>
        </div>
      </div>
      <DataTable data={data} columns={columns} loading={loading} />
      {showModal && (
        <Modal title="Create Credential" onClose={() => setShowModal(false)}>
          <div className="form-group"><label>Tenant</label><select value={form.TenantId} onChange={e => { const tid = e.target.value; const tu = users.filter(u => u.TenantId === tid); setForm({ ...form, TenantId: tid, UserId: tu.length > 0 ? tu[0].Id : '' }); }}>{tenants.map(t => <option key={t.Id} value={t.Id}>{t.Name || t.Id}</option>)}</select></div>
          <div className="form-group"><label>User</label><select value={form.UserId} onChange={e => setForm({ ...form, UserId: e.target.value })}>{users.filter(u => u.TenantId === form.TenantId).map(u => <option key={u.Id} value={u.Id}>{u.Email || u.Id}</option>)}</select></div>
          <div className="form-group"><label>Name</label><input value={form.Name} onChange={e => setForm({ ...form, Name: e.target.value })} /></div>
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
        entityType="credential"
      />
      <JsonViewModal
        isOpen={jsonModal.isOpen}
        onClose={() => setJsonModal({ isOpen: false, data: null })}
        title="Credential JSON"
        data={jsonModal.data}
      />
    </div>
  );
}
