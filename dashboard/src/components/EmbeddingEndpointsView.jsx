import React, { useState, useEffect, useCallback } from 'react';
import { useApp } from '../context/AppContext';
import { PartioApi } from '../utils/api';
import Modal from './Modal';
import CopyableId from './CopyableId';
import ActionMenu from './ActionMenu';
import DataTable from './DataTable';
import AlertModal from './modals/AlertModal';
import DeleteConfirmModal from './modals/DeleteConfirmModal';
import './EmbeddingEndpointsView.css';

export default function EmbeddingEndpointsView() {
  const { serverUrl, bearerToken } = useApp();
  const api = new PartioApi(serverUrl, bearerToken);
  const [data, setData] = useState([]);
  const [loading, setLoading] = useState(false);
  const [showModal, setShowModal] = useState(false);
  const [editing, setEditing] = useState(null);
  const [form, setForm] = useState({ TenantId: 'default', Model: '', Endpoint: '', ApiFormat: 'Ollama', ApiKey: '', EnableRequestHistory: false });
  const [alertModal, setAlertModal] = useState({ isOpen: false, message: '', type: 'error' });
  const [deleteModal, setDeleteModal] = useState({ isOpen: false, id: null });
  const [tenants, setTenants] = useState([]);

  const loadTenants = useCallback(async () => {
    try {
      const result = await api.enumerateTenants({ MaxResults: 1000 });
      setTenants(result.Data || []);
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

  useEffect(() => { load(); loadTenants(); }, [load, loadTenants]);

  const openCreate = () => {
    setEditing(null);
    const tenantId = tenants.length > 0 ? tenants[0].Id : '';
    setForm({ TenantId: tenantId, Model: '', Endpoint: '', ApiFormat: 'Ollama', ApiKey: '', EnableRequestHistory: false });
    setShowModal(true);
  };

  const openEdit = (item) => {
    setEditing(item);
    setForm({ TenantId: item.TenantId, Model: item.Model, Endpoint: item.Endpoint, ApiFormat: item.ApiFormat, ApiKey: item.ApiKey || '', EnableRequestHistory: item.EnableRequestHistory || false });
    setShowModal(true);
  };

  const handleSave = async () => {
    try {
      if (editing) {
        await api.updateEndpoint(editing.Id, { ...editing, Model: form.Model, Endpoint: form.Endpoint, ApiFormat: form.ApiFormat, ApiKey: form.ApiKey || null, EnableRequestHistory: form.EnableRequestHistory });
      } else {
        await api.createEndpoint({ TenantId: form.TenantId, Model: form.Model, Endpoint: form.Endpoint, ApiFormat: form.ApiFormat, ApiKey: form.ApiKey || null, EnableRequestHistory: form.EnableRequestHistory });
      }
      setShowModal(false);
      load();
    } catch (err) { setAlertModal({ isOpen: true, message: err.message, type: 'error' }); }
  };

  const handleDelete = async () => {
    try {
      await api.deleteEndpoint(deleteModal.id);
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
      key: 'Model',
      label: 'Model'
    },
    {
      key: 'Endpoint',
      label: 'Endpoint'
    },
    {
      key: 'ApiFormat',
      label: 'Format'
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
      key: 'EnableRequestHistory',
      label: 'History',
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
      isAction: true,
      sortable: false,
      render: (item) => (
        <ActionMenu actions={[
          { label: 'Edit', onClick: () => openEdit(item) },
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
        <button className="primary" onClick={openCreate}>Create Endpoint</button>
      </div>
      <DataTable data={data} columns={columns} loading={loading} />
      {showModal && (
        <Modal title={editing ? 'Edit Endpoint' : 'Create Endpoint'} onClose={() => setShowModal(false)}>
          {!editing && <div className="form-group"><label>Tenant</label><select value={form.TenantId} onChange={e => setForm({ ...form, TenantId: e.target.value })}>{tenants.map(t => <option key={t.Id} value={t.Id}>{t.Name || t.Id}</option>)}</select></div>}
          <div className="form-group"><label>Model</label><input value={form.Model} onChange={e => setForm({ ...form, Model: e.target.value })} placeholder="e.g. all-minilm" /></div>
          <div className="form-group"><label>Endpoint</label><input value={form.Endpoint} onChange={e => setForm({ ...form, Endpoint: e.target.value })} placeholder="http://localhost:11434" /></div>
          <div className="form-group"><label>API Format</label><select value={form.ApiFormat} onChange={e => setForm({ ...form, ApiFormat: e.target.value })}><option value="Ollama">Ollama</option><option value="OpenAI">OpenAI</option></select></div>
          <div className="form-group"><label>API Key (optional)</label><input type="password" value={form.ApiKey} onChange={e => setForm({ ...form, ApiKey: e.target.value })} /></div>
          <div className="form-group"><label className="checkbox-label"><input type="checkbox" checked={form.EnableRequestHistory} onChange={e => setForm({ ...form, EnableRequestHistory: e.target.checked })} /> Enable Request History</label></div>
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
        entityType="endpoint"
      />
    </div>
  );
}
