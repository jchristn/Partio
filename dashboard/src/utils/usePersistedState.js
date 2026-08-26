import { useState, useEffect } from 'react';

/**
 * useState variant that persists the value in localStorage under `key`, so a chosen
 * preference (timeframe, request type, rows-per-page, ...) survives navigation and reloads.
 * Falls back to `defaultValue` when nothing is stored or storage is unavailable.
 *
 * @param {string} key - localStorage key.
 * @param {*} defaultValue - value used when no stored value exists.
 * @returns {[*, Function]} the standard [value, setValue] tuple.
 */
export function usePersistedState(key, defaultValue) {
  const [value, setValue] = useState(() => {
    try {
      const stored = localStorage.getItem(key);
      return stored !== null ? JSON.parse(stored) : defaultValue;
    } catch {
      return defaultValue;
    }
  });

  useEffect(() => {
    try {
      localStorage.setItem(key, JSON.stringify(value));
    } catch {
      // Ignore storage errors (private mode, quota, disabled storage).
    }
  }, [key, value]);

  return [value, setValue];
}
