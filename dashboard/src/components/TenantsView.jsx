import React, { useState, useEffect, useCallback } from 'react';
import { useApp } from '../context/AppContext';
import { PartioApi } from '../utils/api';
import Modal from './Modal';
import CopyableId from './CopyableId';
import ActionMenu from './ActionMenu';
import DataTable from './DataTable';
import TagInput from './TagInput';
import KeyValueEditor from './KeyValueEditor';
import FormFieldLabel from './FormFieldLabel';
import Tooltip from './Tooltip';
import TooltipIcon from './TooltipIcon';
import AlertModal from './modals/AlertModal';
import DeleteConfirmModal from './modals/DeleteConfirmModal';
import JsonViewModal from './modals/JsonViewModal';
import './TenantsView.css';

export default function TenantsView() {
  const { serverUrl, bearerToken } = useApp();
  const api = new PartioApi(serverUrl, bearerToken);
  const [data, setData] = useState([]);
  const [loading, setLoading] = useState(false);
  const [showModal, setShowModal] = useState(false);
  const [editing, setEditing] = useState(null);
  const [form, setForm] = useState({ Name: '', Labels: [], Tags: {}, Active: true });
  const [alertModal, setAlertModal] = useState({ isOpen: false, message: '', type: 'error' });
  const [deleteModal, setDeleteModal] = useState({ isOpen: false, id: null });
  const [jsonModal, setJsonModal] = useState({ isOpen: false, data: null });

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const result = await api.enumerateTenants({ MaxResults: 1000 });
      setData(result.Data || []);
    } catch (err) {
      console.error(err);
    }
    setLoading(false);
  }, [serverUrl, bearerToken]);

  useEffect(() => { load(); }, [load]);

  const openCreate = () => {
    setEditing(null);
    setForm({ Name: '', Labels: [], Tags: {}, Active: true });
    setShowModal(true);
  };

  const openEdit = (item) => {
    setEditing(item);
    setForm({ Name: item.Name, Labels: item.Labels || [], Tags: item.Tags || {}, Active: item.Active !== false });
    setShowModal(true);
  };

  const handleSave = async () => {
    try {
      if (editing) {
        await api.updateTenant(editing.Id, { ...editing, Name: form.Name, Labels: form.Labels, Tags: form.Tags, Active: form.Active });
      } else {
        await api.createTenant({ Name: form.Name, Labels: form.Labels, Tags: form.Tags, Active: form.Active });
      }
      setShowModal(false);
      load();
    } catch (err) {
      setAlertModal({ isOpen: true, message: err.message, type: 'error' });
    }
  };

  const handleDelete = async () => {
    try {
      await api.deleteTenant(deleteModal.id);
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
      tooltip: 'Unique tenant identifier used to scope users, credentials, endpoints, and request history.',
      width: '280px',
      render: (item) => <CopyableId value={item.Id} />
    },
    {
      key: 'Name',
      label: 'Name',
      tooltip: 'Human-friendly tenant name shown throughout the dashboard.'
    },
    {
      key: 'Active',
      label: 'Status',
      tooltip: 'Whether the tenant is active and available for use.',
      filterValue: (item) => item.Active ? 'Active' : 'Inactive',
      render: (item) => (
        <span className={`status-badge ${item.Active ? 'active' : 'inactive'}`}>
          {item.Active ? 'Active' : 'Inactive'}
        </span>
      )
    },
    {
      key: 'CreatedUtc',
      label: 'Created',
      tooltip: 'When the tenant record was created in Partio.',
      render: (item) => new Date(item.CreatedUtc).toLocaleDateString()
    },
    {
      key: 'actions',
      label: 'Actions',
      tooltip: 'Available actions for this tenant, including edit, JSON view, and delete.',
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
        <div className="page-title-block">
          <h2>Tenants</h2>
          <p className="view-subtitle">Create and manage isolated tenant workspaces, including their labels and metadata.</p>
        </div>
        <div className="header-row-actions">
          <button className="refresh-btn" onClick={load} title="Refresh">
            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
              <polyline points="23 4 23 10 17 10" /><polyline points="1 20 1 14 7 14" />
              <path d="M3.51 9a9 9 0 0114.85-3.36L23 10M1 14l4.64 4.36A9 9 0 0020.49 15" />
            </svg>
          </button>
          <button className="primary" onClick={openCreate}>Create Tenant</button>
        </div>
      </div>
      <DataTable data={data} columns={columns} loading={loading} onRowClick={openEdit} />
      {showModal && (
        <Modal title={editing ? 'Edit Tenant' : 'Create Tenant'} onClose={() => setShowModal(false)}>
          <div className="form-group">
            <FormFieldLabel text="Name" tooltip="Display name for this tenant. Use a short, recognizable workspace name." />
            <Tooltip content="Display name for this tenant. Use a short, recognizable workspace name." block>
              <input value={form.Name} onChange={e => setForm({ ...form, Name: e.target.value })} />
            </Tooltip>
          </div>
          <div className="form-group">
            <FormFieldLabel text="Labels" tooltip="Flat labels applied to the tenant for grouping or quick filtering." />
            <TagInput
              value={form.Labels}
              onChange={labels => setForm({ ...form, Labels: labels })}
              inputTooltip="Add a tenant label, then press Enter or click Add. Labels are short free-form tags."
            />
          </div>
          <div className="form-group">
            <FormFieldLabel text="Tags" tooltip="Structured tenant metadata stored as key/value string pairs." />
            <KeyValueEditor
              value={form.Tags}
              onChange={tags => setForm({ ...form, Tags: tags })}
              keyTooltip="Tenant metadata field name, such as environment, owner, or region."
              valueTooltip="Tenant metadata field value. Values are stored as strings."
            />
          </div>
          <div className="form-group">
            <label className="checkbox-label">
              <input type="checkbox" checked={form.Active} onChange={e => setForm({ ...form, Active: e.target.checked })} />
              {' '}Active
              <TooltipIcon content="Enable or disable this tenant. Inactive tenants remain stored but should not be used for new activity." />
            </label>
          </div>
          <div className="btn-group" style={{ marginTop: 16 }}>
            <button className="primary" onClick={handleSave}>Save</button>
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
        entityType="tenant"
      />
      <JsonViewModal
        isOpen={jsonModal.isOpen}
        onClose={() => setJsonModal({ isOpen: false, data: null })}
        title="Tenant JSON"
        data={jsonModal.data}
      />
    </div>
  );
}
