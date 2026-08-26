import { afterEach, describe, expect, it } from 'vitest';
import { act, renderHook } from '@testing-library/react';
import { usePersistedState } from './usePersistedState';

describe('usePersistedState', () => {
  afterEach(() => {
    localStorage.clear();
  });

  it('returns the default when nothing is stored', () => {
    const { result } = renderHook(() => usePersistedState('k.default', 'Day'));
    expect(result.current[0]).toBe('Day');
  });

  it('persists updates to localStorage', () => {
    const { result } = renderHook(() => usePersistedState('k.persist', 'Day'));
    act(() => result.current[1]('Week'));
    expect(result.current[0]).toBe('Week');
    expect(JSON.parse(localStorage.getItem('k.persist'))).toBe('Week');
  });

  it('hydrates the stored value on a fresh mount (survives revisits)', () => {
    localStorage.setItem('k.hydrate', JSON.stringify(50));
    const { result } = renderHook(() => usePersistedState('k.hydrate', 25));
    expect(result.current[0]).toBe(50);
  });

  it('falls back to the default when the stored value is corrupt', () => {
    localStorage.setItem('k.corrupt', '{not-json');
    const { result } = renderHook(() => usePersistedState('k.corrupt', ''));
    expect(result.current[0]).toBe('');
  });
});
