/**
 * Ancient Order - NPC Template Registry
 *
 * Provides frozen NPC template objects for the 3 test NPCs:
 * - Elena (npc_scout_elena): Loyal Scout, DEUS faction
 * - Lars (npc_merchant_lars): Scheming Merchant, Neutral faction
 * - Kade (npc_outlaw_kade): Rogue Outlaw, Rogues faction
 *
 * Templates are Object.freeze()'d at module level — immutable at runtime.
 */

import { NPC } from '../types/index.js';

// ============================================================================
// NPC Templates
// ============================================================================

const _elena: NPC = Object.freeze({
  id: 'npc_scout_elena',
  archetype: 'Loyal Scout',
  personality: Object.freeze({
    patience: 20,
    empathy: 20,
    cunning: 10,
    logic: 15,
    kindness: 20,
    charisma: 15,
  }),
  affection: 0,
  trust: 0,
});

const _lars: NPC = Object.freeze({
  id: 'npc_merchant_lars',
  archetype: 'Scheming Merchant',
  personality: Object.freeze({
    patience: 10,
    empathy: 8,
    cunning: 28,
    logic: 25,
    kindness: 12,
    charisma: 17,
  }),
  affection: 0,
  trust: -20,
});

const _kade: NPC = Object.freeze({
  id: 'npc_outlaw_kade',
  archetype: 'Rogue Outlaw',
  personality: Object.freeze({
    patience: 12,
    empathy: 8,
    cunning: 25,
    logic: 18,
    kindness: 10,
    charisma: 27,
  }),
  affection: 0,
  trust: 0,
});

/**
 * Frozen NPC template objects keyed by NPC ID.
 * These are the canonical archetypes — never modified after module load.
 */
export const NPC_TEMPLATES: Record<string, NPC> = Object.freeze({
  [_elena.id]: Object.freeze(_elena),
  [_lars.id]: Object.freeze(_lars),
  [_kade.id]: Object.freeze(_kade),
});

// ============================================================================
// Query Functions
// ============================================================================

/**
 * Retrieves an NPC template by ID.
 * Returns undefined if the ID does not match any template.
 */
export function getNPC(id: string): NPC | undefined {
  return NPC_TEMPLATES[id];
}

/**
 * Returns all NPC templates as an array.
 */
export function getAllNPCs(): NPC[] {
  return Object.values(NPC_TEMPLATES);
}
