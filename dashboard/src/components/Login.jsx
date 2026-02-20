import React, { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useApp } from '../context/AppContext';
import './Login.css';

export default function Login() {
  const { login, error, theme } = useApp();
  const navigate = useNavigate();
  const defaultServerUrl = window.__PARTIO_ENV__?.PARTIO_SERVER_URL || 'http://localhost:8400';
  const [serverUrl, setServerUrl] = useState(defaultServerUrl);
  const [token, setToken] = useState('partioadmin');
  const [loading, setLoading] = useState(false);

  const handleSubmit = async (e) => {
    e.preventDefault();
    setLoading(true);
    const success = await login(serverUrl.replace(/\/+$/, ''), token);
    setLoading(false);
    if (success) navigate('/');
  };

  return (
    <div className="login-container">
      <div className="login-card">
        <div className="login-header">
          <img
            src={theme === 'dark' ? '/logo-light-text.png' : '/logo-dark-text.png'}
            alt="Partio"
            className="login-logo"
          />
          <p className="login-subtitle">Admin Dashboard</p>
        </div>
        <form onSubmit={handleSubmit}>
          <div className="form-group">
            <label>Server URL</label>
            <input type="text" value={serverUrl} onChange={e => setServerUrl(e.target.value)} placeholder="http://localhost:8400" required />
          </div>
          <div className="form-group">
            <label>Bearer Token</label>
            <input type="password" value={token} onChange={e => setToken(e.target.value)} placeholder="Admin API Key" required />
          </div>
          {error && <div className="login-error">{error}</div>}
          <button type="submit" className="primary login-btn" disabled={loading}>
            {loading ? 'Connecting...' : 'Connect'}
          </button>
        </form>
      </div>
    </div>
  );
}
