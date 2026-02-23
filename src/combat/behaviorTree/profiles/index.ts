/**
 * Profile registry — maps archetype IDs to ArchetypeProfile data objects.
 *
 * Lookup is data-driven (Map lookup, no switch statements) per design spec.
 * Archetype ID matches Combatant.archetype field.
 *
 * To add a new archetype: implement the profile, import it here, and add an
 * entry to PROFILE_REGISTRY.
 */

import type { ArchetypeProfile } from '../../../types/combat.js';
import { elenaProfile } from './elena.js';
import { larsProfile } from './lars.js';
import { kadeProfile } from './kade.js';

/**
 * Registry mapping archetype ID strings to their ArchetypeProfile data.
 * Keys match the Combatant.archetype field set in encounter fixtures.
 */
const PROFILE_REGISTRY: ReadonlyMap<string, ArchetypeProfile> = new Map([
  ['elena_loyal_scout',   elenaProfile],
  ['lars_scheming_merchant', larsProfile],
  ['kade_rogue_outlaw',   kadeProfile],
]);

/**
 * Look up an archetype profile by combatant archetype ID.
 *
 * @param archetypeId - The combatant's archetype string (e.g. "elena_loyal_scout")
 * @returns The matching ArchetypeProfile, or undefined if not found
 */
export function getProfile(archetypeId: string): ArchetypeProfile | undefined {
  return PROFILE_REGISTRY.get(archetypeId);
}

/** Export all profiles (for testing and introspection). */
export { elenaProfile, larsProfile, kadeProfile };
