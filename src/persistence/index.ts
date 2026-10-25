export { createMemoryBackend, createWebStorageBackend } from './backends.js';
export type { SaveBackend, StorageLike } from './backends.js';
export { createSaveStore, MIN_SLOT, MAX_SLOT } from './saveStore.js';
export type { SaveStore, SaveMetadata, SaveSlotInfo } from './saveStore.js';
export { validateGameState } from './validation.js';
export type { ValidationResult } from './validation.js';
export { isValidSlot } from './saveFormat.js';
