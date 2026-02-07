import React, { useState, useEffect, useCallback } from 'react';
import { useApp } from '../context/AppContext';
import { PartioApi } from '../utils/api';
import Modal from './Modal';
import CopyableId from './CopyableId';
import ActionMenu from './ActionMenu';
import DataTable from './DataTable';
import AlertModal from './modals/AlertModal';
import DeleteConfirmModal from './modals/DeleteConfirmModal';
import './RequestHistoryView.css';

export default function RequestHistoryView() {
  const { serverUrl, bearerToken } = useApp();
  const api = new PartioApi(serverUrl, bearerToken);
  const [data, setData] = useState([]);
  const [loading, setLoading] = useState(false);
  const [detail, setDetail] = useState(null);
  const [alertModal, setAlertModal] = useState({ isOpen: false, message: '', type: 'error' });
  const [deleteModal, setDeleteModal] = useState({ isOpen: false, id: null });

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const result = await api.enumerateRequestHistory({ MaxResults: 1000 });
      setData(result.Data || []);
    } catch (err) { console.error(err); }
    setLoading(false);
  }, [serverUrl, bearerToken]);

  useEffect(() => { load(); }, [load]);

  const viewDetail = async (id) => {
    try {
      const d = await api.getRequestHistoryDetail(id);
      setDetail(JSON.stringify(d, null, 2));
    } catch (err) {
      setDetail('Error loading detail: ' + err.message);
    }
  };

  const handleDelete = async () => {
    try {
      await api.deleteRequestHistory(deleteModal.id);
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
      key: 'HttpMethod',
      label: 'Method'
    },
    {
      key: 'HttpUrl',
      label: 'URL',
      render: (item) => (
        <span style={{ maxWidth: 200, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap', display: 'inline-block' }}>
          {item.HttpUrl}
        </span>
      )
    },
    {
      key: 'HttpStatus',
      label: 'Status',
      render: (item) => item.HttpStatus || '-'
    },
    {
      key: 'ResponseTimeMs',
      label: 'Time (ms)',
      render: (item) => item.ResponseTimeMs != null ? item.ResponseTimeMs : '-',
      sortValue: (item) => item.ResponseTimeMs
    },
    {
      key: 'CreatedUtc',
      label: 'Created',
      render: (item) => new Date(item.CreatedUtc).toLocaleString()
    },
    {
      key: 'actions',
      label: 'Actions',
      isAction: true,
      sortable: false,
      render: (item) => (
        <ActionMenu actions={[
          { label: 'View Detail', onClick: () => viewDetail(item.Id) },
          { divider: true },
          { label: 'Delete', danger: true, onClick: () => setDeleteModal({ isOpen: true, id: item.Id }) }
        ]} />
      )
    }
  ];

  return (
    <div>
      <div className="header-row"><h2>Request History</h2></div>
      <DataTable data={data} columns={columns} loading={loading} />
      {detail !== null && (
        <Modal title="Request Detail" onClose={() => setDetail(null)}>
          <pre style={{ whiteSpace: 'pre-wrap', wordBreak: 'break-all', fontSize: 13, fontFamily: 'Courier New, monospace', maxHeight: 400, overflow: 'auto', color: 'var(--text-primary)' }}>{detail}</pre>
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
        entityType="entry"
      />
    </div>
  );
}
