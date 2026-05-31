/**
 * Ancient Order - Reaction Progression (ADR-054 Layer B)
 *
 * Derives a combatant's reaction rates (SR/SMR/FMR) from accumulated reaction
 * XP, and awards XP when a player-party combatant uses a reaction.
 *
 * Single source of truth: XP points per reaction. Rank and rates are *derived*,
 * never stored. Values are decoded from `GM Combat Tracker.xlsx`:
 *   - XP curve         → `Reaction Progression & Log` sheet (cumulative thresholds)
 *   - rank → SR/SMR/FMR → `Defense Simulations` sheet (base + per-rank step)
 * Both captured in ADR-054 so this module need not re-read the workbook.
 *
 * Pure functions — no mutations. All callers spread the returned objects.
 */

import type {
  Combatant,
  ReactionProgress,
  ReactionSkills,
} from '../types/combat.js';

// ============================================================================
// Decoded tables (ADR-054)
// ============================================================================

/** The three trainable reactions. 'defenseless' is not a reaction and earns no XP. */
export type ReactionKind = 'block' | 'dodge' | 'parry';

/**
 * Cumulative XP required to *reach* each rank, ranks 1–11 (index 0 = rank 1).
 * Source: `Reaction Progression & Log` column B. The per-rank step grows by +15
 * each rank (100, 115, 130, … 235). Rank 11 (1675) is the cap.
 */
export const XP_THRESHOLDS: readonly number[] = [
  0, 100, 215, 345, 490, 650, 825, 1015, 1220, 1440, 1675,
];

/** Maximum rank (and the index ceiling into XP_THRESHOLDS + rate ramps). */
export const MAX_REACTION_RANK = XP_THRESHOLDS.length; // 11

/** XP cap — the rank-11 threshold. No reaction accumulates beyond this. */
export const MAX_REACTION_XP = XP_THRESHOLDS[XP_THRESHOLDS.length - 1]; // 1675

/** XP awarded for a reaction use, by outcome (ADR-054 decision: success 4 / fail 2). */
export const XP_ON_SUCCESS = 4;
export const XP_ON_FAILURE = 2;

/**
 * Rate ramp per reaction: each of SR/SMR/FMR is `base + step·(rank − 1)`.
 * Source: `Defense Simulations` base/increment rows (ADR-054 table).
 */
const RATE_RAMP: Record<
  ReactionKind,
  { SR: readonly [number, number]; SMR: readonly [number, number]; FMR: readonly [number, number] }
> = {
  block: { SR: [0.4, 0.05], SMR: [0.55, 0.025], FMR: [0.4, 0.025] },
  dodge: { SR: [0.3, 0.025], SMR: [0.8, 0.01], FMR: [0.1, 0.025] },
  parry: { SR: [0.1, 0.025], SMR: [0.9, 0.01], FMR: [0.0, 0.025] },
};

/** Reaction XP totals for a brand-new combatant (rank 1 across the board). */
export const ZERO_REACTION_PROGRESS: ReactionProgress = { block: 0, dodge: 0, parry: 0 };

// ============================================================================
// Rank & rate derivation
// ============================================================================

/**
 * Returns the rank (1–11) for an accumulated XP total: the highest rank whose
 * cumulative threshold has been met. Clamps to [1, MAX_REACTION_RANK].
 */
export function rankFromXp(xp: number): number {
  let rank = 1;
  for (let r = 0; r < XP_THRESHOLDS.length; r++) {
    if (xp >= XP_THRESHOLDS[r]) {
      rank = r + 1;
    } else {
      break;
    }
  }
  return rank;
}

/**
 * Derives the SR/SMR/FMR for one reaction at a given rank from the decoded ramp.
 * Rank is clamped to [1, MAX_REACTION_RANK]. Rates are not clamped here; the
 * decoded ramps stay within [0, 1] across ranks 1–11, and defense resolution
 * clamps again when folding buffs.
 */
export function ratesForRank(
  reaction: ReactionKind,
  rank: number,
): { SR: number; SMR: number; FMR: number } {
  const clampedRank = Math.max(1, Math.min(MAX_REACTION_RANK, rank));
  const step = clampedRank - 1;
  const ramp = RATE_RAMP[reaction];
  const ratesAt = (b: readonly [number, number]): number =>
    _round4(b[0] + b[1] * step);
  return { SR: ratesAt(ramp.SR), SMR: ratesAt(ramp.SMR), FMR: ratesAt(ramp.FMR) };
}

/**
 * Builds a full ReactionSkills object from accumulated per-reaction XP.
 * Each reaction's rank (and therefore its rates) is derived independently.
 */
export function reactionSkillsFromProgress(progress: ReactionProgress): ReactionSkills {
  return {
    block: ratesForRank('block', rankFromXp(progress.block)),
    dodge: ratesForRank('dodge', rankFromXp(progress.dodge)),
    parry: ratesForRank('parry', rankFromXp(progress.parry)),
  };
}

// ============================================================================
// XP award (mid-combat, ADR-054 decision: recompute rates on rank-up)
// ============================================================================

/**
 * Awards reaction XP to a live combatant for using `reaction` with the given
 * outcome, and — if the new XP crosses a rank threshold — rederives that one
 * reaction's base rates immediately (mid-combat recompute).
 *
 * Only the *leveled* reaction's rates are rederived, leaving the other two
 * untouched. This deliberately preserves in-combat block degradation from
 * Crushing Blow (which writes base block rates directly): the only case it
 * would be reset is a block rank-up in the same fight after a Crushing Blow —
 * a rare, documented edge (ADR-054).
 *
 * Combatants without a `reactionProgress` (enemies/NPCs) are returned unchanged.
 * XP is capped at MAX_REACTION_XP.
 *
 * @returns A new Combatant; the input is never mutated.
 */
export function awardReactionXp(
  combatant: Combatant,
  reaction: ReactionKind,
  success: boolean,
): Combatant {
  const progress = combatant.reactionProgress;
  if (progress === undefined) return combatant; // non-progressing combatant

  const gain = success ? XP_ON_SUCCESS : XP_ON_FAILURE;
  const oldXp = progress[reaction];
  const newXp = Math.min(MAX_REACTION_XP, oldXp + gain);
  if (newXp === oldXp) return combatant; // already capped

  const newProgress: ReactionProgress = { ...progress, [reaction]: newXp };

  // Rederive rates only if this reaction changed rank (mid-combat recompute).
  let reactionSkills = combatant.reactionSkills;
  if (rankFromXp(newXp) !== rankFromXp(oldXp)) {
    reactionSkills = {
      ...reactionSkills,
      [reaction]: ratesForRank(reaction, rankFromXp(newXp)),
    };
  }

  return { ...combatant, reactionProgress: newProgress, reactionSkills };
}

// ============================================================================
// Internal helpers
// ============================================================================

/** Rounds to 4 decimals to avoid float drift in derived rates (e.g. 0.55 + 0.025·n). */
function _round4(n: number): number {
  return Math.round(n * 10000) / 10000;
}
