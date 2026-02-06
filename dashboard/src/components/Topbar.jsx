import React from 'react';
import { useApp } from '../context/AppContext';
import './Topbar.css';

export default function Topbar() {
  const { serverUrl, isConnected, logout } = useApp();

  return (
    <header className="topbar">
      <div className="topbar-left">
        <span className="topbar-label">Connected to:</span>
        <span className="topbar-url">{serverUrl}</span>
        <span className={`connection-dot ${isConnected ? 'connected' : 'disconnected'}`} />
      </div>
      <button className="secondary" onClick={logout}>Logout</button>
    </header>
  );
}
