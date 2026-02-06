import React, { useState, useEffect, useCallback } from 'react';
import { useApp } from '../context/AppContext';
import { PartioApi } from '../utils/api';
import Modal from './Modal';
import CopyableId from './CopyableId';
import ActionMenu from './ActionMenu';
import Pagination from './Pagination';
import './RequestHistoryView.css';

export default function RequestHistoryView() {
  const { serverUrl, bearerToken } = useApp();
  const api = new PartioApi(serverUrl, bearerToken);
  const [data, setData] = useState([]);
  const [hasMore, setHasMore] = useState(false);
  const [continuationToken, setContinuationToken] = useState(null);
  const [loading, setLoading] = useState(false);
  const [detail, setDetail] = useState(null);

  const load = useCallback(async (token = null) => {
    setLoading(true);
    try {
      const result = await api.enumerateRequestHistory({ MaxResults: 25, ContinuationToken: token });
      setData(result.Data || []);
      setHasMore(result.HasMore);
      setContinuationToken(result.ContinuationToken);
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

  const handleDelete = async (id) => {
    if (!confirm('Delete this entry?')) return;
    try { await api.deleteRequestHistory(id); load(); } catch (err) { alert(err.message); }
  };

  return (
    <div>
      <div className="header-row"><h2>Request History</h2></div>
      {data.length === 0 && !loading ? (
        <div className="empty-state">No request history found.</div>
      ) : (
        <table>
          <thead><tr><th>ID</th><th>Method</th><th>URL</th><th>Status</th><th>Time (ms)</th><th>Created</th><th></th></tr></thead>
          <tbody>
            {data.map(item => (
              <tr key={item.Id}>
                <td><CopyableId value={item.Id} /></td>
                <td>{item.HttpMethod}</td>
                <td style={{ maxWidth: 200, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>{item.HttpUrl}</td>
                <td>{item.HttpStatus || '-'}</td>
                <td>{item.ResponseTimeMs != null ? item.ResponseTimeMs : '-'}</td>
                <td>{new Date(item.CreatedUtc).toLocaleString()}</td>
                <td><ActionMenu actions={[{ label: 'View Detail', onClick: () => viewDetail(item.Id) }, { label: 'Delete', onClick: () => handleDelete(item.Id) }]} /></td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
      <Pagination hasMore={hasMore} onNext={() => load(continuationToken)} onReset={() => load()} loading={loading} />
      {detail !== null && (
        <Modal title="Request Detail" onClose={() => setDetail(null)}>
          <pre style={{ whiteSpace: 'pre-wrap', wordBreak: 'break-all', fontSize: 13, fontFamily: 'Courier New, monospace', maxHeight: 400, overflow: 'auto' }}>{detail}</pre>
        </Modal>
      )}
    </div>
  );
}
