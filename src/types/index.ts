/**
 * Ancient Order - Type Definitions
 * Sprint 1: Core game state, personality, dialogue, and NPC types
 */

// ============================================================================
// Personality System
// ============================================================================

/**
 * Six personality traits that define player character behavior.
 * Each trait ranges from 5% to 35%, and all traits must sum to 100%.
 */
export interface Personality {
  patience: number;   // Wisdom category
  empathy: number;    // Wisdom category
  cunning: number;    // Intelligence category
  logic: number;      // Intelligence category
  kindness: number;   // Charisma category
  charisma: number;   // Charisma category
}

export type PersonalityTrait = keyof Personality;

/**
 * Partial personality adjustments applied during dialogue choices.
 * Positive values increase the trait, negative decrease.
 */
export type PersonalityAdjustment = Partial<Personality>;

// ============================================================================
// Player Character
// ============================================================================

export interface Stamina {
  current: number;
  max: number;
}

export interface ReactionSkills {
  block: number;  // 0-11 ranks
  dodge: number;  // 0-11 ranks
  parry: number;  // 0-11 ranks
}

export interface PathSegments {
  current: number;
  max: number;
}

export type ElementalPath = 'Fire' | 'Water' | 'Air' | 'Earth' | 'Shadow' | 'Light';

export interface PlayerCharacter {
  id: string;
  name: string;
  title: string;
  currentRank: number; // e.g., 2.5 = 5th Degree

  // Base Stats
  stamina: Stamina;
  power: number;
  speed: number;

  // Personality (always sums to 100%, each trait 5-35%)
  personality: Personality;

  // Progression
  coreTrainingPoints: number;
  reactionSkills: ReactionSkills;

  // Elemental Path
  elementalPath: ElementalPath;
  pathSegments: PathSegments;
  pathAscensionLevel: number; // 0-3

  // Team (NPC IDs of companions, max 2)
  team: string[];
}

// ============================================================================
// NPCs
// ============================================================================

export type Faction = 'DEUS' | 'Rogues' | 'Neutral';

export interface NPC {
  id: string;
  name: string;
  archetype: string; // "Loyal Scout", "Scheming Merchant", etc.
  faction: Faction;

  // Fixed personality (NEVER changes)
  basePersonality: Personality;

  // Relationship tracking (changes with player choices)
  affection: number; // -100 to +100
  trust: number;     // -100 to +100

  // Availability
  joinableInTeam: boolean;
  availableLocations: string[];
  questsAvailable: string[];

  // Dialogue tree reference
  dialogueTree: DialogueNode[];
}

// ============================================================================
// Dialogue System
// ============================================================================

/**
 * Personality gate that controls dialogue option availability.
 * null means the option is always available (no gate).
 */
export interface PersonalityGate {
  trait: PersonalityTrait;
  operator: 'gte' | 'lte' | 'eq'; // >=, <=, ==
  value: number; // 5-35 range
}

export interface DialogueOption {
  id: string;
  text: string;

  // Personality gate (null = always available)
  personalityGate: PersonalityGate | null;

  // Effect on player personality when chosen
  personalityAdjustment: PersonalityAdjustment;

  // Narrative consequence
  consequenceText: string;

  // Effect on NPC relationship
  affectionChange: number;
  trustChange: number;

  // Navigation (null = end conversation)
  nextNodeId: string | null;
}

export interface DialogueNode {
  id: string;
  speakerId: string; // NPC id or "player"
  text: string;
  options: DialogueOption[];
}

/**
 * Response format for dialogue endpoint - includes availability status
 */
export interface DialogueOptionResponse {
  id: string;
  text: string;
  available: boolean;
  personalityGate: PersonalityGate | null;
}

export interface DialogueNodeResponse {
  nodeId: string;
  speakerId: string;
  text: string;
  options: DialogueOptionResponse[];
}

// ============================================================================
// Conversation History
// ============================================================================

export interface ConversationEntry {
  timestamp: number;
  npcId: string;
  nodeId: string;
  optionChosen: {
    id: string;
    text: string;
  };
  personalityBefore: Personality;
  personalityAfter: Personality;
  affectionChange: number;
  trustChange: number;
}

// ============================================================================
// Combat (Placeholder for Sprint 3-4)
// ============================================================================

export interface CombatCombatant {
  id: string;
  name: string;
  stamina: Stamina;
  // Additional combat fields TBD
}

export interface CombatAction {
  round: number;
  actorId: string;
  action: string;
  targetId: string;
  result: string;
}

export interface CombatState {
  id: string;
  round: number;
  playerTeamIds: string[];
  enemyTeamIds: string[];
  playerTeam: CombatCombatant[];
  enemyTeam: CombatCombatant[];
  combatHistory: CombatAction[];
}

export interface CompletedCombat {
  id: string;
  timestamp: number;
  location: string;
  playerTeamIds: string[];
  enemyTeamIds: string[];
  result: 'victory' | 'defeat';
  xpGained: number;
  reward: Record<string, unknown>;
}

// ============================================================================
// Game State (Root Object)
// ============================================================================

export interface GameState {
  // Metadata
  id: string;
  timestamp: number;
  version: string;

  // World State
  currentLocation: string;
  conversationLog: ConversationEntry[];

  // Player
  player: PlayerCharacter;

  // NPCs (keyed by NPC ID)
  npcs: Record<string, NPC>;

  // Combat (null when not in combat)
  activeCombat: CombatState | null;
  combatHistory: CompletedCombat[];

  // World Flags
  questFlags: Record<string, boolean>;
  relationshipFlags: Record<string, boolean>;
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
  details?: Record<string, unknown>;
}

// Error codes
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

export type ErrorCode = typeof ErrorCodes[keyof typeof ErrorCodes];

// ============================================================================
// Save/Load Types
// ============================================================================

export interface SaveMetadata {
  slot: number;
  slotName: string | null;
  playerName: string;
  location: string;
  playtime: string;
  savedAt: string; // ISO date string
  timestamp: number;
}

export interface SavedGame extends GameState {
  slotName?: string;
}

// ============================================================================
// Request Types
// ============================================================================

export interface NewGameRequest {
  playerName: string;
  difficulty?: 'easy' | 'normal' | 'hard';
}

export interface SaveGameRequest {
  slotName?: string;
}

export interface DialogueChoiceRequest {
  npcId: string;
  optionId: string;
  currentNodeId: string;
}

export interface SetTeamRequest {
  npcIds: string[];
}

// ============================================================================
// Response Types
// ============================================================================

export interface DialogueChoiceResponse {
  success: boolean;
  personalityBefore: Personality;
  personalityAfter: Personality;
  consequenceText: string;
  npcAffectionChange: number;
  npcTrustChange: number;
  nextNode: DialogueNodeResponse | null;
  gameState: GameState;
}

export interface PersonalityResponse {
  personality: Personality;
  categories: {
    wisdom: { patience: number; empathy: number; total: number };
    intelligence: { cunning: number; logic: number; total: number };
    charisma: { kindness: number; charisma: number; total: number };
  };
}

export interface TeamMember {
  id: string;
  name: string;
  archetype: string;
}

export interface SetTeamResponse {
  success: boolean;
  team: TeamMember[];
  gameState: GameState;
}
