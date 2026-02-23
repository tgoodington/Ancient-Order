/**
 * Combat perception layer for the behavior tree evaluator.
 *
 * Translates raw CombatState + Combatant into a pre-computed readonly snapshot
 * (CombatPerception) that factors can query without redundant computation.
 *
 * Ownership: perception.ts builds the snapshot. It does NOT contain scoring logic.
 * Factors receive CombatPerception and read from it — they never write to CombatState.
 */

import type {
  Combatant,
  CombatState,
  CombatPerception,
  AllyPerception,
  EnemyPerception,
} from '../../types/combat.js';

/**
 * Build a CombatPerception snapshot for the given combatant in the given state.
 *
 * The combatant's own party is determined by checking which party array
 * contains the combatant's ID. The opposing party becomes "enemies".
 *
 * Ally list: all non-KO'd combatants in same party, excluding self.
 * Enemy list: all non-KO'd combatants in opposing party.
 * Both lists sorted by staminaPct ascending (weakest first).
 *
 * @param combatant - The NPC for whom we are building perception
 * @param state - Current combat state (read-only)
 * @returns A fully pre-computed CombatPerception snapshot
 */
export function buildPerception(combatant: Combatant, state: CombatState): CombatPerception {
  // Determine which party the combatant belongs to
  const isInPlayerParty = state.playerParty.some((c) => c.id === combatant.id);
  const ownParty: readonly Combatant[] = isInPlayerParty ? state.playerParty : state.enemyParty;
  const opposingParty: readonly Combatant[] = isInPlayerParty
    ? state.enemyParty
    : state.playerParty;

  // Self stats
  const selfStaminaPct =
    combatant.maxStamina > 0 ? combatant.stamina / combatant.maxStamina : 0;

  // Allies: same party, excluding self, including KO'd (isKO flag set)
  const allAllies: AllyPerception[] = ownParty
    .filter((c) => c.id !== combatant.id)
    .map((c) => ({
      id: c.id,
      staminaPct: c.maxStamina > 0 ? c.stamina / c.maxStamina : 0,
      isKO: c.isKO,
    }))
    .sort((a, b) => a.staminaPct - b.staminaPct);

  const nonKOAllies = allAllies.filter((a) => !a.isKO);
  const allyCount = nonKOAllies.length;

  // lowestAllyStaminaPct: min across non-KO'd allies (1.0 if no allies alive)
  const lowestAllyStaminaPct =
    nonKOAllies.length > 0
      ? Math.min(...nonKOAllies.map((a) => a.staminaPct))
      : 1.0;

  // teamAvgStaminaPct: average across non-KO'd allies + self
  const teamMembers = [...nonKOAllies.map((a) => a.staminaPct), selfStaminaPct];
  const teamAvgStaminaPct =
    teamMembers.length > 0
      ? teamMembers.reduce((sum, v) => sum + v, 0) / teamMembers.length
      : 0;

  // Enemies
  const allEnemies: EnemyPerception[] = opposingParty.map((e) => {
    const speedDelta =
      e.speed > 0 ? (combatant.speed - e.speed) / e.speed : 0;
    const rankDelta = combatant.rank - e.rank;
    const staminaPct = e.maxStamina > 0 ? e.stamina / e.maxStamina : 0;
    return {
      id: e.id,
      staminaPct,
      isKO: e.isKO,
      speedDelta,
      rankDelta,
      power: e.power,
    };
  }).sort((a, b) => a.staminaPct - b.staminaPct);

  const nonKOEnemies = allEnemies.filter((e) => !e.isKO);
  const enemyCount = nonKOEnemies.length;

  const weakestEnemyStaminaPct =
    nonKOEnemies.length > 0
      ? Math.min(...nonKOEnemies.map((e) => e.staminaPct))
      : 1.0;

  const enemyAvgStaminaPct =
    nonKOEnemies.length > 0
      ? nonKOEnemies.reduce((sum, e) => sum + e.staminaPct, 0) / nonKOEnemies.length
      : 0;

  return {
    selfId: combatant.id,
    selfStaminaPct,
    selfEnergy: combatant.energy,
    selfAscension: combatant.ascensionLevel,
    selfRank: combatant.rank,
    selfPath: combatant.elementalPath,

    allies: allAllies,
    lowestAllyStaminaPct,
    teamAvgStaminaPct,
    allyCount,

    enemies: allEnemies,
    weakestEnemyStaminaPct,
    enemyAvgStaminaPct,
    enemyCount,

    round: state.round,
  };
}
