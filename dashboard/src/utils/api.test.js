import { afterEach, describe, expect, it, vi } from 'vitest';
import { PartioApi } from './api';

function jsonResponse(data, status = 200) {
  return new Response(JSON.stringify(data), {
    status,
    headers: { 'Content-Type': 'application/json' }
  });
}

describe('PartioApi model load methods', () => {
  afterEach(() => {
    vi.restoreAllMocks();
  });

  it('posts embedding load requests to the endpoint model-load route', async () => {
    const fetchMock = vi.spyOn(globalThis, 'fetch').mockResolvedValue(jsonResponse({ Success: true }));
    const api = new PartioApi('http://partio.local', 'test-token');

    await api.loadEndpoint('embedding-1', { Strategy: 'WarmRequest' });

    expect(fetchMock).toHaveBeenCalledTimes(1);
    const [url, options] = fetchMock.mock.calls[0];
    expect(url).toBe('http://partio.local/v1.0/endpoints/embedding/embedding-1/load');
    expect(options.method).toBe('POST');
    expect(options.headers.Authorization).toBe('Bearer test-token');
    expect(JSON.parse(options.body)).toEqual({ Strategy: 'WarmRequest' });
  });

  it('posts completion load requests to the endpoint model-load route', async () => {
    const fetchMock = vi.spyOn(globalThis, 'fetch').mockResolvedValue(jsonResponse({ Success: true }));
    const api = new PartioApi('http://partio.local', 'test-token');

    await api.loadCompletionEndpoint('completion-1', { RequireNativeLoad: true });

    expect(fetchMock).toHaveBeenCalledTimes(1);
    const [url, options] = fetchMock.mock.calls[0];
    expect(url).toBe('http://partio.local/v1.0/endpoints/completion/completion-1/load');
    expect(options.method).toBe('POST');
    expect(options.headers.Authorization).toBe('Bearer test-token');
    expect(JSON.parse(options.body)).toEqual({ RequireNativeLoad: true });
  });
});
