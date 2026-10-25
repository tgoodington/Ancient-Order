import type { DialogueNode } from '../types/index.js';
import type { EncounterConfig } from '../types/combat.js';
import type { SceneGraph } from '../types/narrative.js';
import { TEST_SCENE_GRAPH } from '../narrative/fixtures.js';
import placeholderDialogues from '../fixtures/dialogues/placeholders.json' with { type: 'json' };
import { ENCOUNTER_REGISTRY } from './encounters.js';

export interface SessionContent {
  sceneGraph: SceneGraph;
  encounters: Record<string, EncounterConfig>;
  dialogues: Record<string, DialogueNode[]>;
}

// Dialogues are PLACEHOLDER greetings until Sprint 7.
export const defaultContent: SessionContent = {
  sceneGraph: TEST_SCENE_GRAPH,
  encounters: ENCOUNTER_REGISTRY,
  dialogues: placeholderDialogues.dialogues as Record<string, DialogueNode[]>,
};
