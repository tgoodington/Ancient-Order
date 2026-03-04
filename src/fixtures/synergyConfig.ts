/**
 * Ancient Order - Synergy Paradigm Configuration
 *
 * Default synergy paradigm configuration for the POC.
 *
 * Well Rounded: power x1.10 when party collectively covers all 6 traits >= 25%
 * Bond:         speed x1.10 when player aligns >= 80% with an NPC's dominant traits
 */

import type { ParadigmConfig } from '../types/narrative.js';

/**
 * Default synergy paradigm definitions for Sprint 3.
 *
 * Threshold values are stored as integers:
 * - Well Rounded: 25 means a trait max must be >= 25 (i.e., 25%)
 * - Bond: 80 means alignment ratio must be >= 0.80 (converted via / 100)
 */
export const DEFAULT_PARADIGMS: readonly ParadigmConfig[] = [
  {
    name: 'Well Rounded',
    type: 'well_rounded',
    threshold: 25,
    stat: 'power',
    multiplier: 1.10,
  },
  {
    name: 'Bond',
    type: 'bond',
    threshold: 80,
    stat: 'speed',
    multiplier: 1.10,
  },
] as const;
