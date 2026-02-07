import React, { createContext, useContext, useState, useCallback, useEffect } from 'react';
import { PartioApi } from '../utils/api';

const AppContext = createContext(null);

export function AppProvider({ children }) {
  const [serverUrl, setServerUrl] = useState(localStorage.getItem('partio_serverUrl') || '');
  const [bearerToken, setBearerToken] = useState(localStorage.getItem('partio_bearerToken') || '');
  const [isConnected, setIsConnected] = useState(false);
  const [error, setError] = useState(null);
  const [theme, setTheme] = useState(localStorage.getItem('partio_theme') || 'light');
  const [userRole, setUserRole] = useState(null);
  const [tenantName, setTenantName] = useState(null);

  useEffect(() => {
    document.documentElement.setAttribute('data-theme', theme);
    localStorage.setItem('partio_theme', theme);
  }, [theme]);

  const toggleTheme = useCallback(() => {
    setTheme(prev => prev === 'light' ? 'dark' : 'light');
  }, []);

  const fetchIdentity = useCallback(async (url, token) => {
    try {
      const api = new PartioApi(url, token);
      const identity = await api.whoami();
      if (identity) {
        setUserRole(identity.Role);
        setTenantName(identity.TenantName);
      }
    } catch {}
  }, []);

  const login = useCallback(async (url, token) => {
    try {
      const response = await fetch(`${url}/v1.0/health`, {
        headers: { 'Authorization': `Bearer ${token}` }
      });
      if (response.ok) {
        setServerUrl(url);
        setBearerToken(token);
        setIsConnected(true);
        setError(null);
        localStorage.setItem('partio_serverUrl', url);
        localStorage.setItem('partio_bearerToken', token);
        fetchIdentity(url, token);
        return true;
      } else {
        setError('Failed to connect: ' + response.status);
        return false;
      }
    } catch (err) {
      setError('Failed to connect: ' + err.message);
      return false;
    }
  }, [fetchIdentity]);

  const logout = useCallback(() => {
    setServerUrl('');
    setBearerToken('');
    setIsConnected(false);
    setError(null);
    setUserRole(null);
    setTenantName(null);
    localStorage.removeItem('partio_serverUrl');
    localStorage.removeItem('partio_bearerToken');
  }, []);

  const value = {
    serverUrl, bearerToken, isConnected, error,
    setError, login, logout, theme, toggleTheme,
    userRole, tenantName
  };

  return <AppContext.Provider value={value}>{children}</AppContext.Provider>;
}

export function useApp() {
  const ctx = useContext(AppContext);
  if (!ctx) throw new Error('useApp must be used within AppProvider');
  return ctx;
}
