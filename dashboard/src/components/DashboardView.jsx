import React, { useState, useEffect, useCallback } from 'react';
import { useNavigate } from 'react-router-dom';
import { useApp } from '../context/AppContext';
import { PartioApi } from '../utils/api';
import { BarChart, Bar, XAxis, YAxis, CartesianGrid, Tooltip, Legend, ResponsiveContainer } from 'recharts';
import './DashboardView.css';

const TIMEFRAMES = [
  { value: 'Hour', label: 'Last Hour' },
  { value: 'Day', label: 'Last 24 Hours' },
  { value: 'Week', label: 'Last 7 Days' },
  { value: 'Month', label: 'Last 30 Days' },
];

const REQUEST_TYPES = [
  { value: '', label: 'All Requests' },
  { value: 'Embedding', label: 'Embeddings' },
  { value: 'Inference', label: 'Inference' },
];

function formatBucketLabel(bucket, timeframe) {
  if (!bucket || bucket.length < 16) return bucket || '';
  // All buckets are now "yyyy-MM-ddTHH:mm" format
  switch (timeframe) {
    case 'Hour':
    case 'Day':
      // Show time only: "14:30"
      return bucket.substring(11, 16);
    case 'Week':
    case 'Month':
      // Show date + time: "03-20 14:00"
      return bucket.substring(5, 10) + ' ' + bucket.substring(11, 16);
    default:
      return bucket.substring(11, 16);
  }
}

function RefreshIcon({ spinning }) {
  return (
    <svg
      className={spinning ? 'refresh-icon spinning' : 'refresh-icon'}
      width="16" height="16" viewBox="0 0 16 16" fill="none" stroke="currentColor"
      strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"
    >
      <path d="M1 1v4h4" />
      <path d="M15 15v-4h-4" />
      <path d="M2.5 6A6.5 6.5 0 0 1 14 5.5" />
      <path d="M13.5 10A6.5 6.5 0 0 1 2 10.5" />
    </svg>
  );
}

export default function DashboardView() {
  const { serverUrl, bearerToken } = useApp();
  const navigate = useNavigate();
  const [timeframe, setTimeframe] = useState('Day');
  const [requestType, setRequestType] = useState('');
  const [endpointFilter, setEndpointFilter] = useState('');
  const [stats, setStats] = useState(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState(null);

  const loadStats = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const api = new PartioApi(serverUrl, bearerToken);
      const req = { Timeframe: timeframe };
      if (requestType) req.RequestType = requestType;
      if (endpointFilter.trim()) req.EndpointFilter = endpointFilter.trim();
      const result = await api.getRequestStatistics(req);
      setStats(result);
    } catch (err) {
      setError(err.message || 'Failed to load statistics');
    } finally {
      setLoading(false);
    }
  }, [serverUrl, bearerToken, timeframe, requestType, endpointFilter]);

  useEffect(() => {
    loadStats();
  }, [loadStats]);

  const chartData = stats?.Buckets?.map(b => ({
    name: formatBucketLabel(b.TimeBucket, timeframe),
    Success: b.SuccessCount,
    Failure: b.FailureCount,
  })) || [];

  // Auto-thin X-axis labels based on data point count
  const tickInterval = chartData.length > 60 ? Math.floor(chartData.length / 30) - 1 : 0;

  return (
    <div className="dashboard-view" data-tour-id="dashboard-view">
      <div className="view-header">
        <div>
          <h2>Dashboard</h2>
          <p className="view-subtitle">Monitor request traffic across your embedding and inference endpoints.</p>
        </div>
      </div>

      <div className="dashboard-filters">
        <div className="filter-group">
          <label>Timeframe</label>
          <div className="filter-buttons">
            {TIMEFRAMES.map(tf => (
              <button
                key={tf.value}
                className={timeframe === tf.value ? 'filter-btn active' : 'filter-btn'}
                onClick={() => setTimeframe(tf.value)}
              >
                {tf.label}
              </button>
            ))}
          </div>
        </div>
        <div className="filter-group">
          <label>Request Type</label>
          <div className="filter-buttons">
            {REQUEST_TYPES.map(rt => (
              <button
                key={rt.value}
                className={requestType === rt.value ? 'filter-btn active' : 'filter-btn'}
                onClick={() => setRequestType(rt.value)}
              >
                {rt.label}
              </button>
            ))}
          </div>
        </div>
        <div className="filter-group">
          <label>Endpoint Filter</label>
          <div className="filter-input-row">
            <input
              type="text"
              className="filter-input"
              placeholder="Filter by URL substring..."
              value={endpointFilter}
              onChange={e => setEndpointFilter(e.target.value)}
              onKeyDown={e => { if (e.key === 'Enter') loadStats(); }}
            />
            <button className="refresh-btn" onClick={loadStats} disabled={loading} title="Refresh">
              <RefreshIcon spinning={loading} />
            </button>
          </div>
        </div>
      </div>

      {error && <div className="dashboard-error">{error}</div>}

      <div className="dashboard-summary">
        <div className="summary-card success">
          <div className="summary-value">{stats?.TotalSuccess ?? 0}</div>
          <div className="summary-label">Successful</div>
        </div>
        <div className="summary-card failure">
          <div className="summary-value">{stats?.TotalFailure ?? 0}</div>
          <div className="summary-label">Failed</div>
        </div>
        <div className="summary-card total">
          <div className="summary-value">{(stats?.TotalSuccess ?? 0) + (stats?.TotalFailure ?? 0)}</div>
          <div className="summary-label">Total</div>
        </div>
      </div>

      <div className="dashboard-chart-card">
        <h3>Request History</h3>
        {chartData.length === 0 && !loading ? (
          <div className="chart-empty">No request data for the selected timeframe.</div>
        ) : (
          <ResponsiveContainer width="100%" height={360}>
            <BarChart data={chartData} margin={{ top: 8, right: 24, left: 0, bottom: 4 }}>
              <CartesianGrid strokeDasharray="3 3" stroke="var(--border-color)" />
              <XAxis
                dataKey="name"
                tick={{ fill: 'var(--text-secondary)', fontSize: 11 }}
                interval={tickInterval}
                angle={chartData.length > 30 ? -45 : 0}
                textAnchor={chartData.length > 30 ? 'end' : 'middle'}
                height={chartData.length > 30 ? 60 : 30}
              />
              <YAxis allowDecimals={false} tick={{ fill: 'var(--text-secondary)', fontSize: 12 }} />
              <Tooltip
                contentStyle={{
                  background: 'var(--card-bg)',
                  border: '1px solid var(--border-color)',
                  borderRadius: 'var(--radius)',
                  color: 'var(--text-primary)',
                }}
              />
              <Legend />
              <Bar dataKey="Success" stackId="a" fill="var(--success-color)" radius={[0, 0, 0, 0]} />
              <Bar dataKey="Failure" stackId="a" fill="var(--danger-color)" radius={[4, 4, 0, 0]} />
            </BarChart>
          </ResponsiveContainer>
        )}
      </div>

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
            <div className="action-title">Endpoint Explorer</div>
            <div className="action-desc">Test embedding and inference endpoints</div>
          </button>
        </div>
      </div>
    </div>
  );
}
