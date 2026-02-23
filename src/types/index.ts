/**
 * Ancient Order - Sprint 1 Type Definitions
 *
 * Barrel file exporting all Sprint 1 interfaces and constants.
 * Zero dependencies — this is the leaf module all other Sprint 1 modules import from.
 *
 * GameState.combatState is typed using an import type from combat.ts.
 * combat.ts imports from this file (not the reverse), so there is no circular dependency.
 */

import type { CombatState } from './combat.js';

// ============================================================================
// Personality System
// ============================================================================

/**
 * Six personality traits that define player character behavior.
 * Each trait ranges from 5% to 35%, and all traits must sum to 100%.
 */
export interface Personality {
  patience: number; // Wisdom category
  empathy: number; // Wisdom category
  cunning: number; // Intelligence category
  logic: number; // Intelligence category
  kindness: number; // Charisma category
  charisma: number; // Charisma category
}

/**
 * Union of all valid personality trait names.
 */
export type PersonalityTrait =
  | 'patience'
  | 'empathy'
  | 'cunning'
  | 'logic'
  | 'kindness'
  | 'charisma';

/**
 * Partial personality adjustments applied during dialogue choices.
 * Positive values increase the trait, negative values decrease it.
 * Redistribution algorithm ensures sum stays at 100% after adjustment.
 */
export type PersonalityAdjustment = Partial<Personality>;

/**
 * Gate condition that controls dialogue option availability.
 * Compares a single personality trait against a threshold value.
 */
export interface PersonalityGate {
  trait: PersonalityTrait;
  operator: 'gte' | 'lte' | 'eq'; // >=, <=, ==
  value: number; // 5-35 range
}

// ============================================================================
// Player Character
// ============================================================================

export interface PlayerCharacter {
  readonly id: string;
  readonly name: string;
  readonly personality: Personality;
}

// ============================================================================
// NPCs
// ============================================================================

export interface NPC {
  readonly id: string;
  readonly archetype: string; // "Loyal Scout", "Scheming Merchant", etc.
  readonly personality: Personality; // Fixed — never changes for NPCs
  readonly affection: number; // -100 to +100
  readonly trust: number; // -100 to +100
}

// ============================================================================
// Game State (Root Object)
// ============================================================================

/**
 * Root game state object. All state transitions produce a new GameState
 * (immutable pattern via spread operator).
 *
 * combatState is null when not in combat, and a full CombatState when an
 * encounter is active. Typed via import from combat.ts (no circular import —
 * combat.ts imports from this file, not the reverse).
 */
export interface GameState {
  readonly player: PlayerCharacter;
  readonly npcs: Record<string, NPC>;
  readonly currentDialogueNode: string | null;
  readonly saveSlot: number | null;
  readonly combatState: CombatState | null;
  readonly conversationLog: ConversationEntry[];
  readonly timestamp: number;
}

// ============================================================================
// Dialogue System
// ============================================================================

/**
 * A single node in a dialogue tree.
 */
export interface DialogueNode {
  readonly id: string;
  readonly npcId: string;
  readonly text: string;
  readonly options: DialogueOption[];
}

/**
 * Adjustment to an NPC's relationship values when a dialogue option is chosen.
 */
export interface NpcAdjustment {
  affectionChange?: number;
  trustChange?: number;
}

/**
 * A selectable dialogue option presented to the player.
 * Options without a gate are always available.
 */
export interface DialogueOption {
  readonly id: string;
  readonly text: string;
  readonly gate?: PersonalityGate; // undefined = always available
  readonly personalityAdjustment?: PersonalityAdjustment;
  readonly npcAdjustment?: NpcAdjustment;
  readonly nextNodeId: string | null; // null = end conversation
}

/**
 * Result of processing a dialogue selection. Contains the updated game state,
 * the next node to display (null if conversation ended), and the chosen option.
 */
export interface DialogueResult {
  readonly state: GameState;
  readonly nextNode: DialogueNode | null;
  readonly selectedOption: DialogueOption;
}

// ============================================================================
// Conversation History
// ============================================================================

/**
 * A single entry in the conversation log recording a player dialogue choice.
 */
export interface ConversationEntry {
  readonly npcId: string;
  readonly nodeId: string;
  readonly optionId: string;
  readonly timestamp: number;
}

// ============================================================================
// API Response Types
// ============================================================================

export interface ApiResponse<T = unknown> {
  success: boolean;
  data?: T;
  error?: ApiError;
}

export interface ApiError {
  code: string;
  message: string;
}

// ============================================================================
// Error Codes
// ============================================================================

/**
 * Application-level error codes used in ApiError.code.
 * Defined as a const object (not enum) for ESM compatibility.
 */
export const ErrorCodes = {
  GAME_NOT_FOUND: 'GAME_NOT_FOUND',
  INVALID_SLOT: 'INVALID_SLOT',
  SAVE_NOT_FOUND: 'SAVE_NOT_FOUND',
  NPC_NOT_FOUND: 'NPC_NOT_FOUND',
  DIALOGUE_NODE_NOT_FOUND: 'DIALOGUE_NODE_NOT_FOUND',
  DIALOGUE_OPTION_NOT_AVAILABLE: 'DIALOGUE_OPTION_NOT_AVAILABLE',
  DIALOGUE_OPTION_NOT_FOUND: 'DIALOGUE_OPTION_NOT_FOUND',
  INVALID_PERSONALITY_ADJUSTMENT: 'INVALID_PERSONALITY_ADJUSTMENT',
  TEAM_COMPOSITION_INVALID: 'TEAM_COMPOSITION_INVALID',
  VALIDATION_ERROR: 'VALIDATION_ERROR',
} as const;

export type ErrorCode = (typeof ErrorCodes)[keyof typeof ErrorCodes];
