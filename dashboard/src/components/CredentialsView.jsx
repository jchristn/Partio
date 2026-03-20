import React, { useState, useEffect, useCallback } from 'react';
import { useApp } from '../context/AppContext';
import { PartioApi } from '../utils/api';
import Modal from './Modal';
import CopyableId from './CopyableId';
import ActionMenu from './ActionMenu';
import DataTable from './DataTable';
import FormFieldLabel from './FormFieldLabel';
import Tooltip from './Tooltip';
import TooltipIcon from './TooltipIcon';
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
  const [editing, setEditing] = useState(null);
  const [form, setForm] = useState({ TenantId: 'default', UserId: 'default', Name: '', Active: true });
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
    setEditing(null);
    const tenantId = tenants.length > 0 ? tenants[0].Id : '';
    const tenantUsers = users.filter(u => u.TenantId === tenantId);
    const userId = tenantUsers.length > 0 ? tenantUsers[0].Id : '';
    setForm({ TenantId: tenantId, UserId: userId, Name: '', Active: true });
    setShowModal(true);
  };

  const openEdit = (item) => {
    setEditing(item);
    setForm({
      TenantId: item.TenantId || '',
      UserId: item.UserId || '',
      Name: item.Name || '',
      Active: item.Active !== false
    });
    setShowModal(true);
  };

  const handleSave = async () => {
    try {
      if (editing) {
        await api.updateCredential(editing.Id, {
          ...editing,
          TenantId: form.TenantId,
          UserId: form.UserId,
          Name: form.Name,
          Active: form.Active
        });
      } else {
        await api.createCredential({ TenantId: form.TenantId, UserId: form.UserId, Name: form.Name, Active: form.Active });
      }
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
      tooltip: 'Unique credential identifier used internally by Partio.',
      width: '280px',
      render: (item) => <CopyableId value={item.Id} />
    },
    {
      key: 'Name',
      label: 'Name',
      tooltip: 'Optional display name used to recognize this credential.',
      render: (item) => item.Name || '-'
    },
    {
      key: 'BearerToken',
      label: 'Bearer Token',
      tooltip: 'The API token clients send in the Authorization header to authenticate with Partio.',
      render: (item) => <CopyableId value={item.BearerToken} />
    },
    {
      key: 'Active',
      label: 'Status',
      tooltip: 'Whether this credential can currently be used to access Partio.',
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
      tooltip: 'Available actions for this credential, including edit, JSON view, and delete.',
      isAction: true,
      preventRowClick: true,
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
        <div className="page-title-block">
          <h2>Credentials</h2>
          <p className="view-subtitle">Issue and review bearer tokens used by clients and services to authenticate with Partio.</p>
        </div>
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
      <DataTable data={data} columns={columns} loading={loading} onRowClick={openEdit} />
      {showModal && (
        <Modal title={editing ? 'Edit Credential' : 'Create Credential'} onClose={() => setShowModal(false)}>
          <div className="form-group">
            <FormFieldLabel text="Tenant" tooltip="Tenant that owns this credential. Credentials are scoped to a tenant." />
            <Tooltip content="Tenant that owns this credential. Credentials are scoped to a tenant." block>
              <select value={form.TenantId} onChange={e => { const tid = e.target.value; const tu = users.filter(u => u.TenantId === tid); setForm({ ...form, TenantId: tid, UserId: tu.some(u => u.Id === form.UserId) ? form.UserId : (tu.length > 0 ? tu[0].Id : '') }); }}>
                {tenants.map(t => <option key={t.Id} value={t.Id}>{t.Name || t.Id}</option>)}
              </select>
            </Tooltip>
          </div>
          <div className="form-group">
            <FormFieldLabel text="User" tooltip="User account this credential belongs to. Tokens inherit that user and tenant context." />
            <Tooltip content="User account this credential belongs to. Tokens inherit that user and tenant context." block>
              <select value={form.UserId} onChange={e => setForm({ ...form, UserId: e.target.value })}>
                {users.filter(u => u.TenantId === form.TenantId).map(u => <option key={u.Id} value={u.Id}>{u.Email || u.Id}</option>)}
              </select>
            </Tooltip>
          </div>
          <div className="form-group">
            <FormFieldLabel text="Name" tooltip="Optional friendly name for the credential, such as SDK key, CI token, or local test app." />
            <Tooltip content="Optional friendly name for the credential, such as SDK key, CI token, or local test app." block>
              <input value={form.Name} onChange={e => setForm({ ...form, Name: e.target.value })} />
            </Tooltip>
          </div>
          <div className="form-group">
            <label className="checkbox-label">
              <input type="checkbox" checked={form.Active} onChange={e => setForm({ ...form, Active: e.target.checked })} />
              {' '}Active
              <TooltipIcon content="Enable or disable this credential. Inactive tokens remain stored but should no longer authenticate requests." />
            </label>
          </div>
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
