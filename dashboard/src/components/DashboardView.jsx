import React from 'react';
import { useNavigate } from 'react-router-dom';
import RequestHistoryChart from './RequestHistoryChart';
import './DashboardView.css';

export default function DashboardView() {
  const navigate = useNavigate();

  return (
    <div className="dashboard-view" data-tour-id="dashboard-view">
      <div className="view-header">
        <div>
          <h2>Dashboard</h2>
          <p className="view-subtitle">Monitor request traffic across your embedding and inference endpoints.</p>
        </div>
      </div>

      <RequestHistoryChart />

      <div className="dashboard-actions">
        <h3>Quick Actions</h3>
        <div className="actions-grid">
          <button className="action-card" onClick={() => navigate('/endpoints/embeddings')}>
            <div className="action-title">Manage Embedding Endpoints</div>
            <div className="action-desc">View, create, and configure embedding endpoints</div>
          </button>
          <button className="action-card" onClick={() => navigate('/endpoints/inference')}>
            <div className="action-title">Manage Inference Endpoints</div>
            <div className="action-desc">View, create, and configure completion endpoints</div>
          </button>
          <button className="action-card" onClick={() => navigate('/history')}>
            <div className="action-title">View Request History</div>
            <div className="action-desc">Browse detailed request and response logs</div>
          </button>
          <button className="action-card" onClick={() => navigate('/explorer')}>
            <div className="action-title">API Explorer</div>
            <div className="action-desc">Test embedding and inference endpoints</div>
          </button>
        </div>
      </div>
    </div>
  );
}
