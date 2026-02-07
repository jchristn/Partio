import React, { useState, useEffect, useCallback } from 'react';
import { useApp } from '../context/AppContext';
import { PartioApi } from '../utils/api';
import Modal from './Modal';
import CopyableId from './CopyableId';
import ActionMenu from './ActionMenu';
import DataTable from './DataTable';
import TagInput from './TagInput';
import KeyValueEditor from './KeyValueEditor';
import AlertModal from './modals/AlertModal';
import DeleteConfirmModal from './modals/DeleteConfirmModal';
import './TenantsView.css';

export default function TenantsView() {
  const { serverUrl, bearerToken } = useApp();
  const api = new PartioApi(serverUrl, bearerToken);
  const [data, setData] = useState([]);
  const [loading, setLoading] = useState(false);
  const [showModal, setShowModal] = useState(false);
  const [editing, setEditing] = useState(null);
  const [form, setForm] = useState({ Name: '', Labels: [], Tags: {} });
  const [alertModal, setAlertModal] = useState({ isOpen: false, message: '', type: 'error' });
  const [deleteModal, setDeleteModal] = useState({ isOpen: false, id: null });

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
      width: '280px',
      render: (item) => <CopyableId value={item.Id} />
    },
    {
      key: 'Name',
      label: 'Name'
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
      key: 'CreatedUtc',
      label: 'Created',
      render: (item) => new Date(item.CreatedUtc).toLocaleDateString()
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
        <h2>Tenants</h2>
        <button className="primary" onClick={openCreate}>Create Tenant</button>
      </div>
      <DataTable data={data} columns={columns} loading={loading} />
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
    </div>
  );
}
