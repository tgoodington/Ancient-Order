import type { ApiResponse } from '../types/index';

export class ApiClientError extends Error {
  code: string;

  constructor(code: string, message: string) {
    super(message);
    this.name = 'ApiClientError';
    this.code = code;
  }
}

export async function apiGet<T>(path: string): Promise<T> {
  let response: Response;
  try {
    response = await fetch(path);
  } catch {
    throw new ApiClientError('NETWORK_ERROR', 'Network request failed');
  }

  if (!response.ok) {
    let code = 'NETWORK_ERROR';
    try {
      const body = await response.json() as ApiResponse<T>;
      if (body.error?.code) {
        code = body.error.code;
      }
    } catch {
      // body not parseable -- use default code
    }
    throw new ApiClientError(code, `HTTP ${response.status}`);
  }

  const body = await response.json() as ApiResponse<T>;
  if (!body.success) {
    throw new ApiClientError(
      body.error?.code ?? 'UNKNOWN_ERROR',
      body.error?.message ?? 'Request failed'
    );
  }

  return body.data as T;
}

export async function apiPost<T>(path: string, payload?: unknown): Promise<T> {
  let response: Response;
  try {
    response = await fetch(path, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload)
    });
  } catch {
    throw new ApiClientError('NETWORK_ERROR', 'Network request failed');
  }

  if (!response.ok) {
    let code = 'NETWORK_ERROR';
    try {
      const body = await response.json() as ApiResponse<T>;
      if (body.error?.code) {
        code = body.error.code;
      }
    } catch {
      // body not parseable -- use default code
    }
    throw new ApiClientError(code, `HTTP ${response.status}`);
  }

  const body = await response.json() as ApiResponse<T>;
  if (!body.success) {
    throw new ApiClientError(
      body.error?.code ?? 'UNKNOWN_ERROR',
      body.error?.message ?? 'Request failed'
    );
  }

  return body.data as T;
}
