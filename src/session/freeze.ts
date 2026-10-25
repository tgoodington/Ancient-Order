// Deep clone and deep freeze for plain data (objects and arrays only).
export function deepCloneFrozen<T>(value: T): T {
  if (typeof value !== 'object' || value === null) return value;
  if (Array.isArray(value)) {
    return Object.freeze(value.map((item) => deepCloneFrozen(item as unknown))) as unknown as T;
  }
  const copy: Record<string, unknown> = {};
  for (const key of Object.keys(value)) {
    Object.defineProperty(copy, key, {
      value: deepCloneFrozen((value as Record<string, unknown>)[key]),
      enumerable: true,
      writable: true,
      configurable: true,
    });
  }
  return Object.freeze(copy) as T;
}
