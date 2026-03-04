/**
 * Ancient Order - Act 1 Scene Data Validation Tests
 *
 * Validates the act1_demo.json scene graph fixture against the scene engine.
 *
 * Coverage:
 *   - validateSceneGraph: act1 data has no dead ends
 *   - Scene count and IDs match expected Act 1 structure
 *   - Each scene has at least 2 ungated choices
 *   - nextSceneId referential integrity
 *   - Gate trait name validity
 */

import { describe, it, expect } from 'vitest';

import act1SceneGraph from './act1_demo.json' with { type: 'json' };
import { validateSceneGraph } from '../../narrative/sceneEngine.js';

// ============================================================================
// Act 1 Scene Graph Validation
// ============================================================================

describe('act1_demo scene graph', () => {
  describe('validateSceneGraph', () => {
    it('returns valid: true (no dead ends)', () => {
      const result = validateSceneGraph(act1SceneGraph);
      expect(result.valid).toBe(true);
    });

    it('returns no problematic scenes', () => {
      const result = validateSceneGraph(act1SceneGraph);
      expect(result.problematicScenes).toHaveLength(0);
    });
  });

  describe('scene count and IDs', () => {
    it('has exactly 4 scenes', () => {
      expect(act1SceneGraph).toHaveLength(4);
    });

    it('contains scene_ironhold_arrival', () => {
      const ids = act1SceneGraph.map(s => s.id);
      expect(ids).toContain('scene_ironhold_arrival');
    });

    it('contains scene_market_disturbance', () => {
      const ids = act1SceneGraph.map(s => s.id);
      expect(ids).toContain('scene_market_disturbance');
    });

    it('contains scene_gym_registration', () => {
      const ids = act1SceneGraph.map(s => s.id);
      expect(ids).toContain('scene_gym_registration');
    });

    it('includes scene_combat_briefing', () => {
      expect(act1SceneGraph.some(s => s.id === 'scene_combat_briefing')).toBe(true);
    });
  });

  describe('ungated choice count (>= 2 per scene)', () => {
    it.each(act1SceneGraph)('$id has at least 2 ungated choices', (scene) => {
      const ungatedCount = scene.choices.filter(c => !('gate' in c) || c.gate === null || c.gate === undefined).length;
      expect(ungatedCount).toBeGreaterThanOrEqual(2);
    });
  });

  describe('nextSceneId referential integrity', () => {
    it('all nextSceneId values are null or a valid scene ID', () => {
      const sceneIds = new Set(act1SceneGraph.map(s => s.id));
      for (const scene of act1SceneGraph) {
        for (const choice of scene.choices) {
          if (choice.nextSceneId !== null) {
            expect(sceneIds.has(choice.nextSceneId as string)).toBe(true);
          }
        }
      }
    });
  });

  describe('gate trait name validity', () => {
    const VALID_TRAITS = new Set(['patience', 'empathy', 'cunning', 'logic', 'kindness', 'charisma']);
    it('all gate traits are valid PersonalityTrait names', () => {
      for (const scene of act1SceneGraph) {
        for (const choice of scene.choices) {
          if ('gate' in choice && choice.gate !== null && choice.gate !== undefined) {
            expect(VALID_TRAITS.has(choice.gate.trait)).toBe(true);
          }
        }
      }
    });
  });
});
