/**
 * Ancient Order - Save validation
 *
 * validateGameState and ValidationResult, carried over unchanged from the original save module.
 */

export type ValidationResult =
  | { valid: true }
  | { valid: false; errors: string[] };

/**
 * Validates data loaded from disk against the GameState shape.
 * Accumulates all errors and returns them in a ValidationResult.
 * Returns { valid: true } if data passes all checks.
 */
export function validateGameState(data: unknown): ValidationResult {
  const errors: string[] = [];

  // Phase 1: Structural checks
  if (data === null || typeof data !== 'object') {
    errors.push('GameState must be a non-null object');
    return { valid: false, errors };
  }

  const s = data as Record<string, unknown>;

  if (typeof s.timestamp !== 'number') {
    errors.push(`timestamp: expected number, got ${typeof s.timestamp}`);
  }
  if (s.currentDialogueNode !== null && typeof s.currentDialogueNode !== 'string') {
    errors.push('currentDialogueNode: expected string or null');
  }
  if (s.saveSlot !== null && typeof s.saveSlot !== 'number') {
    errors.push('saveSlot: expected number or null');
  }
  if (!Array.isArray(s.conversationLog)) {
    errors.push('conversationLog: expected array');
  }
  if (!s.player || typeof s.player !== 'object') {
    errors.push('player: expected object');
  }

  const traits = ['patience', 'empathy', 'cunning', 'logic', 'kindness', 'charisma'];

  if (s.player && typeof s.player === 'object') {
    const player = s.player as Record<string, unknown>;
    if (typeof player.id !== 'string') {
      errors.push('player.id: expected string');
    }
    if (typeof player.name !== 'string') {
      errors.push('player.name: expected string');
    }
    if (!player.personality || typeof player.personality !== 'object') {
      errors.push('player.personality: expected object');
    }

    // Phase 2: Personality deep validation
    if (player.personality && typeof player.personality === 'object') {
      const p = player.personality as Record<string, unknown>;
      let allTraitsValid = true;
      let sum = 0;
      for (const trait of traits) {
        if (typeof p[trait] !== 'number') {
          errors.push(`player.personality.${trait}: expected number, got ${typeof p[trait]}`);
          allTraitsValid = false;
        } else {
          const val = p[trait] as number;
          if (val < 5) {
            errors.push(`player.personality.${trait}: value ${val} is below minimum 5`);
            allTraitsValid = false;
          } else if (val > 35) {
            errors.push(`player.personality.${trait}: value ${val} is above maximum 35`);
            allTraitsValid = false;
          } else {
            sum += val;
          }
        }
      }
      if (allTraitsValid && Math.abs(sum - 100) > 0.01) {
        errors.push(`player.personality: sum ${sum} does not equal 100 (tolerance 0.01)`);
      }
    }
  }

  // Phase 3: NPC validation
  if (!s.npcs || typeof s.npcs !== 'object' || Array.isArray(s.npcs)) {
    errors.push('npcs: expected object');
  } else {
    const npcs = s.npcs as Record<string, unknown>;
    for (const [key, npcEntry] of Object.entries(npcs)) {
      if (!npcEntry || typeof npcEntry !== 'object') {
        errors.push(`npcs.${key}: expected object`);
        continue;
      }
      const npc = npcEntry as Record<string, unknown>;
      if (typeof npc.id !== 'string') {
        errors.push(`npcs.${key}.id: expected string`);
      }
      if (typeof npc.archetype !== 'string') {
        errors.push(`npcs.${key}.archetype: expected string`);
      }
      if (typeof npc.affection !== 'number') {
        errors.push(`npcs.${key}.affection: expected number`);
      }
      if (typeof npc.trust !== 'number') {
        errors.push(`npcs.${key}.trust: expected number`);
      }
      if (!npc.personality || typeof npc.personality !== 'object') {
        errors.push(`npcs.${key}.personality: expected object`);
      } else {
        const np = npc.personality as Record<string, unknown>;
        for (const trait of traits) {
          if (typeof np[trait] !== 'number') {
            errors.push(`npcs.${key}.personality.${trait}: expected number, got ${typeof np[trait]}`);
          }
        }
      }
    }
  }

  // Phase 4: Team validation
  if (s.team !== undefined) {
    if (!Array.isArray(s.team)) {
      errors.push('team: expected array');
    } else {
      const team = s.team as unknown[];
      if (team.length !== 0 && team.length !== 2) {
        errors.push(`team: must contain exactly 0 or 2 NPC IDs, got ${team.length}`);
      } else if (team.length === 2) {
        for (let i = 0; i < team.length; i++) {
          if (typeof team[i] !== 'string') {
            errors.push(`team[${i}]: expected string`);
          }
        }
      }
    }
  }

  // Phase 5: Combat state validation
  if (s.combatState !== null && s.combatState !== undefined) {
    if (typeof s.combatState !== 'object') {
      errors.push('combatState: expected object or null');
    } else {
      const cs = s.combatState as Record<string, unknown>;
      if (typeof cs.round !== 'number') {
        errors.push('combatState.round: expected number');
      }
      const validPhases = ['AI_DECISION', 'VISUAL_INFO', 'PC_DECLARATION', 'ACTION_RESOLUTION', 'PER_ATTACK'];
      if (!validPhases.includes(cs.phase as string)) {
        errors.push(`combatState.phase: invalid value '${cs.phase}', expected one of AI_DECISION, VISUAL_INFO, PC_DECLARATION, ACTION_RESOLUTION, PER_ATTACK`);
      }
      const validStatuses = ['active', 'victory', 'defeat'];
      if (!validStatuses.includes(cs.status as string)) {
        errors.push(`combatState.status: invalid value '${cs.status}', expected one of active, victory, defeat`);
      }
      if (!Array.isArray(cs.playerParty)) {
        errors.push('combatState.playerParty: expected array');
      }
      if (!Array.isArray(cs.enemyParty)) {
        errors.push('combatState.enemyParty: expected array');
      }
      if (!Array.isArray(cs.actionQueue)) {
        errors.push('combatState.actionQueue: expected array');
      }
      if (!Array.isArray(cs.roundHistory)) {
        errors.push('combatState.roundHistory: expected array');
      }
    }
  }

  // Phase 5b: Reaction progression map validation (ADR-054 Layer B).
  // Optional for backward compatibility; loadGame normalizes a missing map to {}.
  if (s.reactionProgress !== undefined) {
    if (
      s.reactionProgress === null ||
      typeof s.reactionProgress !== 'object' ||
      Array.isArray(s.reactionProgress)
    ) {
      errors.push('reactionProgress: expected object');
    } else {
      const rp = s.reactionProgress as Record<string, unknown>;
      for (const [combatantId, entry] of Object.entries(rp)) {
        if (!entry || typeof entry !== 'object' || Array.isArray(entry)) {
          errors.push(`reactionProgress.${combatantId}: expected object`);
          continue;
        }
        const e = entry as Record<string, unknown>;
        for (const reaction of ['block', 'dodge', 'parry']) {
          if (typeof e[reaction] !== 'number') {
            errors.push(
              `reactionProgress.${combatantId}.${reaction}: expected number, got ${typeof e[reaction]}`,
            );
          }
        }
      }
    }
  }

  // Phase 6: Narrative state validation
  if (s.narrativeState !== null && s.narrativeState !== undefined) {
    if (typeof s.narrativeState !== 'object') {
      errors.push('narrativeState: expected object or null');
    } else {
      const ns = s.narrativeState as Record<string, unknown>;
      if (typeof ns.currentSceneId !== 'string') {
        errors.push('narrativeState.currentSceneId: expected string');
      }
      if (!Array.isArray(ns.visitedSceneIds)) {
        errors.push('narrativeState.visitedSceneIds: expected array');
      } else {
        const visited = ns.visitedSceneIds as unknown[];
        for (let i = 0; i < visited.length; i++) {
          if (typeof visited[i] !== 'string') {
            errors.push(`narrativeState.visitedSceneIds[${i}]: expected string`);
          }
        }
      }
      if (!ns.choiceFlags || typeof ns.choiceFlags !== 'object' || Array.isArray(ns.choiceFlags)) {
        errors.push('narrativeState.choiceFlags: expected object');
      } else {
        const flags = ns.choiceFlags as Record<string, unknown>;
        for (const [flagKey, flagVal] of Object.entries(flags)) {
          if (typeof flagVal !== 'boolean') {
            errors.push(`narrativeState.choiceFlags.${flagKey}: expected boolean, got ${typeof flagVal}`);
          }
        }
      }
      if (!Array.isArray(ns.sceneHistory)) {
        errors.push('narrativeState.sceneHistory: expected array');
      } else {
        const history = ns.sceneHistory as unknown[];
        for (let i = 0; i < history.length; i++) {
          const entry = history[i] as Record<string, unknown>;
          if (typeof entry.sceneId !== 'string') {
            errors.push(`narrativeState.sceneHistory[${i}].sceneId: expected string`);
          }
          if (typeof entry.choiceId !== 'string') {
            errors.push(`narrativeState.sceneHistory[${i}].choiceId: expected string`);
          }
          if (typeof entry.timestamp !== 'number') {
            errors.push(`narrativeState.sceneHistory[${i}].timestamp: expected number`);
          }
        }
      }
    }
  }

  if (errors.length === 0) {
    return { valid: true };
  }
  return { valid: false, errors };
}
