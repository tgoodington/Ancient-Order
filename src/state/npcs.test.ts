/**
 * Tests for NPC template registry (src/state/npcs.ts)
 */

import { describe, it, expect } from 'vitest';
import { NPC_TEMPLATES, getNPC, getAllNPCs } from './npcs.js';

// ============================================================================
// NPC_TEMPLATES structure
// ============================================================================

describe('NPC_TEMPLATES', () => {
  it('contains exactly 3 NPC templates', () => {
    expect(Object.keys(NPC_TEMPLATES)).toHaveLength(3);
  });

  it('contains Elena with correct id and archetype', () => {
    const elena = NPC_TEMPLATES['npc_scout_elena'];
    expect(elena).toBeDefined();
    expect(elena.id).toBe('npc_scout_elena');
    expect(elena.archetype).toBe('Loyal Scout');
  });

  it('contains Lars with correct id and archetype', () => {
    const lars = NPC_TEMPLATES['npc_merchant_lars'];
    expect(lars).toBeDefined();
    expect(lars.id).toBe('npc_merchant_lars');
    expect(lars.archetype).toBe('Scheming Merchant');
  });

  it('contains Kade with correct id and archetype', () => {
    const kade = NPC_TEMPLATES['npc_outlaw_kade'];
    expect(kade).toBeDefined();
    expect(kade.id).toBe('npc_outlaw_kade');
    expect(kade.archetype).toBe('Rogue Outlaw');
  });
});

// ============================================================================
// NPC personality data
// ============================================================================

describe('NPC personality data', () => {
  it('Elena has the correct personality traits', () => {
    const elena = NPC_TEMPLATES['npc_scout_elena'];
    expect(elena.personality).toEqual({
      patience: 20,
      empathy: 20,
      cunning: 10,
      logic: 15,
      kindness: 20,
      charisma: 15,
    });
  });

  it('Lars has the correct personality traits', () => {
    const lars = NPC_TEMPLATES['npc_merchant_lars'];
    expect(lars.personality).toEqual({
      patience: 10,
      empathy: 8,
      cunning: 28,
      logic: 25,
      kindness: 12,
      charisma: 17,
    });
  });

  it('Kade has the correct personality traits', () => {
    const kade = NPC_TEMPLATES['npc_outlaw_kade'];
    expect(kade.personality).toEqual({
      patience: 12,
      empathy: 8,
      cunning: 25,
      logic: 18,
      kindness: 10,
      charisma: 27,
    });
  });

  it('Lars starts with trust of -20', () => {
    expect(NPC_TEMPLATES['npc_merchant_lars'].trust).toBe(-20);
  });

  it('Elena and Kade start with trust of 0', () => {
    expect(NPC_TEMPLATES['npc_scout_elena'].trust).toBe(0);
    expect(NPC_TEMPLATES['npc_outlaw_kade'].trust).toBe(0);
  });

  it('all NPCs start with affection of 0', () => {
    for (const npc of Object.values(NPC_TEMPLATES)) {
      expect(npc.affection).toBe(0);
    }
  });
});

// ============================================================================
// getNPC()
// ============================================================================

describe('getNPC()', () => {
  it('returns Elena by id', () => {
    const npc = getNPC('npc_scout_elena');
    expect(npc).toBeDefined();
    expect(npc!.id).toBe('npc_scout_elena');
  });

  it('returns Lars by id', () => {
    const npc = getNPC('npc_merchant_lars');
    expect(npc).toBeDefined();
    expect(npc!.id).toBe('npc_merchant_lars');
  });

  it('returns Kade by id', () => {
    const npc = getNPC('npc_outlaw_kade');
    expect(npc).toBeDefined();
    expect(npc!.id).toBe('npc_outlaw_kade');
  });

  it('returns undefined for an unknown NPC id', () => {
    expect(getNPC('npc_does_not_exist')).toBeUndefined();
  });

  it('returns undefined for an empty string', () => {
    expect(getNPC('')).toBeUndefined();
  });
});

// ============================================================================
// getAllNPCs()
// ============================================================================

describe('getAllNPCs()', () => {
  it('returns an array of 3 NPCs', () => {
    expect(getAllNPCs()).toHaveLength(3);
  });

  it('includes all three archetypes', () => {
    const archetypes = getAllNPCs().map((npc) => npc.archetype);
    expect(archetypes).toContain('Loyal Scout');
    expect(archetypes).toContain('Scheming Merchant');
    expect(archetypes).toContain('Rogue Outlaw');
  });

  it('returns a different array reference on each call (templates are not mutated)', () => {
    const first = getAllNPCs();
    const second = getAllNPCs();
    expect(first).not.toBe(second);
  });
});

// ============================================================================
// Immutability
// ============================================================================

describe('NPC template immutability', () => {
  it('NPC_TEMPLATES record is frozen', () => {
    expect(Object.isFrozen(NPC_TEMPLATES)).toBe(true);
  });

  it('Elena template object is frozen', () => {
    expect(Object.isFrozen(NPC_TEMPLATES['npc_scout_elena'])).toBe(true);
  });

  it('Lars template object is frozen', () => {
    expect(Object.isFrozen(NPC_TEMPLATES['npc_merchant_lars'])).toBe(true);
  });

  it('Kade template object is frozen', () => {
    expect(Object.isFrozen(NPC_TEMPLATES['npc_outlaw_kade'])).toBe(true);
  });

  it('NPC personality sub-objects are frozen', () => {
    expect(Object.isFrozen(NPC_TEMPLATES['npc_scout_elena'].personality)).toBe(true);
    expect(Object.isFrozen(NPC_TEMPLATES['npc_merchant_lars'].personality)).toBe(true);
    expect(Object.isFrozen(NPC_TEMPLATES['npc_outlaw_kade'].personality)).toBe(true);
  });

  it('attempting to mutate a template in strict mode throws or silently fails', () => {
    // In strict mode (which TypeScript compiles to) mutations throw TypeError.
    // In non-strict mode they silently fail. Either way the value must not change.
    const elena = NPC_TEMPLATES['npc_scout_elena'];
    const originalAffection = elena.affection;
    try {
      // @ts-expect-error — deliberate attempt to mutate a frozen object
      elena.affection = 999;
    } catch {
      // TypeError expected in strict mode — that is the correct behaviour
    }
    expect(elena.affection).toBe(originalAffection);
  });
});
