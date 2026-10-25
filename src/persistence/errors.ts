import { ErrorCodes } from '../types/index.js';
import type { ErrorCode } from '../types/index.js';

export function codedError(code: ErrorCode, message: string, cause?: unknown): Error {
  const err = new Error(message) as Error & { code: ErrorCode; cause?: unknown };
  err.code = code;
  if (cause !== undefined) err.cause = cause;
  return err;
}

export function storageError(message: string, cause?: unknown): Error {
  return codedError(ErrorCodes.STORAGE_ERROR, message, cause);
}
