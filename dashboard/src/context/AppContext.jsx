import React, { createContext, useContext, useState, useCallback } from 'react';

const AppContext = createContext(null);

export function AppProvider({ children }) {
  const [serverUrl, setServerUrl] = useState(localStorage.getItem('partio_serverUrl') || '');
  const [bearerToken, setBearerToken] = useState(localStorage.getItem('partio_bearerToken') || '');
  const [isConnected, setIsConnected] = useState(false);
  const [error, setError] = useState(null);

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
        return true;
      } else {
        setError('Failed to connect: ' + response.status);
        return false;
      }
    } catch (err) {
      setError('Failed to connect: ' + err.message);
      return false;
    }
  }, []);

  const logout = useCallback(() => {
    setServerUrl('');
    setBearerToken('');
    setIsConnected(false);
    setError(null);
    localStorage.removeItem('partio_serverUrl');
    localStorage.removeItem('partio_bearerToken');
  }, []);

  const value = {
    serverUrl, bearerToken, isConnected, error,
    setError, login, logout
  };

  return <AppContext.Provider value={value}>{children}</AppContext.Provider>;
}

export function useApp() {
  const ctx = useContext(AppContext);
  if (!ctx) throw new Error('useApp must be used within AppProvider');
  return ctx;
}
