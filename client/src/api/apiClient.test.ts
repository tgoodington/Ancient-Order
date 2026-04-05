import { describe, it, expect, vi, beforeEach } from 'vitest';
import { apiGet, apiPost, ApiClientError } from './apiClient';

function makeResponse(body: unknown, ok: boolean, status = 200): Response {
  const json = typeof body === 'string' ? body : JSON.stringify(body);
  return {
    ok,
    status,
    json: () => Promise.resolve(typeof body === 'string' ? (() => { throw new SyntaxError('Unexpected token'); })() : body),
    text: () => Promise.resolve(json),
  } as unknown as Response;
}

function makeJsonResponse(body: unknown, ok: boolean, status = 200): Response {
  return {
    ok,
    status,
    json: () => Promise.resolve(body),
    text: () => Promise.resolve(JSON.stringify(body)),
  } as unknown as Response;
}

function makeRawTextResponse(text: string, ok: boolean, status = 200): Response {
  return {
    ok,
    status,
    json: () => Promise.reject(new SyntaxError('Unexpected token')),
    text: () => Promise.resolve(text),
  } as unknown as Response;
}

beforeEach(() => {
  vi.resetAllMocks();
});

describe('apiGet', () => {
  it('returns unwrapped data on success', async () => {
    const fetchMock = vi.fn().mockResolvedValue(
      makeJsonResponse({ success: true, data: { id: 1 } }, true)
    );
    vi.stubGlobal('fetch', fetchMock);

    const result = await apiGet<{ id: number }>('/api/game/state');

    // blueprint:frontend-component:S5.3 — "returns unwrapped data from ApiResponse"
    expect(result).toEqual({ id: 1 });

    // blueprint:frontend-component:S5.3 — "GET request" (apiGet calls fetch(path) with no options)
    expect(fetchMock).toHaveBeenCalledWith('/api/game/state');
  });

  it('throws ApiClientError on non-ok HTTP with code from body', async () => {
    const fetchMock = vi.fn().mockResolvedValue(
      makeJsonResponse(
        { success: false, error: { code: 'GAME_NOT_FOUND', message: 'No game' } },
        false,
        404
      )
    );
    vi.stubGlobal('fetch', fetchMock);

    // blueprint:frontend-component:S5.3 — "On non-ok HTTP response: throw ApiClientError with code from response body if parseable"
    await expect(apiGet('/api/game/state')).rejects.toThrow(ApiClientError);

    let caught: ApiClientError | undefined;
    try {
      await apiGet('/api/game/state');
    } catch (e) {
      caught = e as ApiClientError;
    }

    // blueprint:frontend-component:S5.3 — "throw ApiClientError with code from response body if parseable"
    expect(caught?.code).toBe('GAME_NOT_FOUND');
    // blueprint:frontend-component:S5.3 — error message is "HTTP {status}" for non-ok responses
    expect(caught?.message).toBe('HTTP 404');
  });

  it('throws ApiClientError with NETWORK_ERROR on non-ok with unparseable body', async () => {
    const fetchMock = vi.fn().mockResolvedValue(
      makeRawTextResponse('Internal Server Error', false, 500)
    );
    vi.stubGlobal('fetch', fetchMock);

    let caught: ApiClientError | undefined;
    try {
      await apiGet('/api/game/state');
    } catch (e) {
      caught = e as ApiClientError;
    }

    // blueprint:frontend-component:S5.3 — "else 'NETWORK_ERROR'"
    expect(caught).toBeInstanceOf(ApiClientError);
    expect(caught?.code).toBe('NETWORK_ERROR');
  });

  it('throws ApiClientError when success is false on ok response', async () => {
    const fetchMock = vi.fn().mockResolvedValue(
      makeJsonResponse(
        { success: false, error: { code: 'VALIDATION_ERROR', message: 'Invalid input' } },
        true,
        200
      )
    );
    vi.stubGlobal('fetch', fetchMock);

    let caught: ApiClientError | undefined;
    try {
      await apiGet('/api/game/state');
    } catch (e) {
      caught = e as ApiClientError;
    }

    // blueprint:frontend-component:S5.3 — "On ok response: if success is false throw ApiClientError with error.code and error.message"
    expect(caught).toBeInstanceOf(ApiClientError);
    expect(caught?.code).toBe('VALIDATION_ERROR');
    expect(caught?.message).toBe('Invalid input');
  });

  it('throws ApiClientError with NETWORK_ERROR on fetch exception', async () => {
    const fetchMock = vi.fn().mockRejectedValue(new TypeError('Failed to fetch'));
    vi.stubGlobal('fetch', fetchMock);

    let caught: ApiClientError | undefined;
    try {
      await apiGet('/api/game/state');
    } catch (e) {
      caught = e as ApiClientError;
    }

    // blueprint:frontend-component:S5.3 — "On fetch exception (network down): throw ApiClientError with code 'NETWORK_ERROR'"
    expect(caught).toBeInstanceOf(ApiClientError);
    expect(caught?.code).toBe('NETWORK_ERROR');
  });
});

describe('apiPost', () => {
  it('returns unwrapped data on success', async () => {
    const fetchMock = vi.fn().mockResolvedValue(
      makeJsonResponse({ success: true, data: { team: ['a', 'b'] } }, true)
    );
    vi.stubGlobal('fetch', fetchMock);

    const result = await apiPost<{ team: string[] }>('/api/player/team', { npcIds: ['a', 'b'] });

    // blueprint:frontend-component:S5.3 — "POST request, returns unwrapped data"
    expect(result).toEqual({ team: ['a', 'b'] });

    // blueprint:frontend-component:S5.3 — "POST request"
    expect(fetchMock).toHaveBeenCalledWith(
      '/api/player/team',
      expect.objectContaining({ method: 'POST' })
    );

    // blueprint:frontend-component:S5.3 — "body stringified"
    const callArgs = fetchMock.mock.calls[0][1];
    expect(callArgs.body).toBe(JSON.stringify({ npcIds: ['a', 'b'] }));
  });

  it('sends Content-Type header on POST requests', async () => {
    const fetchMock = vi.fn().mockResolvedValue(
      makeJsonResponse({ success: true, data: {} }, true)
    );
    vi.stubGlobal('fetch', fetchMock);

    await apiPost('/api/player/team', { npcIds: [] });

    const callArgs = fetchMock.mock.calls[0][1];

    // blueprint:frontend-component:S5.3 — "Content-Type: application/json on all POST requests"
    expect(callArgs.headers).toMatchObject({ 'Content-Type': 'application/json' });
  });
});
