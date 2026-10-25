import { ErrorCodes } from '../types/index.js';
import type { ErrorCode } from '../types/index.js';

export interface SessionError {
  code: ErrorCode;
  message: string;
  details?: unknown;
  cause?: unknown;
}

export type SessionResult<T> =
  | { ok: true; data: T }
  | { ok: false; error: SessionError };

const KNOWN_CODES: ReadonlySet<string> = new Set(Object.values(ErrorCodes));

export function ok<T>(data: T): SessionResult<T> {
  return { ok: true, data };
}

export function fail(
  code: ErrorCode,
  message: string,
  details?: unknown,
  cause?: unknown
): SessionResult<never> {
  const error: SessionError = { code, message };
  if (details !== undefined) error.details = details;
  if (cause !== undefined) error.cause = cause;
  return { ok: false, error };
}

export function fromThrown(err: unknown): SessionResult<never> {
  if (err instanceof Error) {
    const code = (err as Error & { code?: unknown }).code;
    if (typeof code === 'string' && KNOWN_CODES.has(code)) {
      return fail(code as ErrorCode, err.message);
    }
  }
  return fail(ErrorCodes.INTERNAL_ERROR, 'An unexpected error occurred', undefined, err);
}
