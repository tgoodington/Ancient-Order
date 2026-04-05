// Duplicated from backend -- kept minimal for HUD needs

export interface Personality {
  patience: number;
  empathy: number;
  cunning: number;
  logic: number;
  kindness: number;
  charisma: number;
}

export type PersonalityTrait = 'patience' | 'empathy' | 'cunning' | 'logic' | 'kindness' | 'charisma';

export const PERSONALITY_TRAITS: readonly PersonalityTrait[] = [
  'patience', 'empathy', 'cunning', 'logic', 'kindness', 'charisma'
] as const;

export interface PlayerCharacter {
  readonly id: string;
  readonly name: string;
  readonly personality: Personality;
}

export interface NPC {
  readonly id: string;
  readonly archetype: string;
  readonly personality: Personality;
  readonly affection: number;
  readonly trust: number;
}

export type ElementalPath = 'Fire' | 'Water' | 'Air' | 'Earth' | 'Shadow' | 'Light';

export interface Combatant {
  readonly id: string;
  readonly name: string;
  readonly archetype: string;
  readonly rank: number;
  readonly stamina: number;
  readonly maxStamina: number;
  readonly power: number;
  readonly speed: number;
  readonly energy: number;
  readonly maxEnergy: number;
  readonly ascensionLevel: 0 | 1 | 2 | 3;
  readonly elementalPath: ElementalPath;
  readonly isKO: boolean;
}

export type CombatPhase = 'AI_DECISION' | 'VISUAL_INFO' | 'PC_DECLARATION' | 'ACTION_RESOLUTION' | 'PER_ATTACK';

export interface CombatState {
  readonly round: number;
  readonly phase: CombatPhase;
  readonly playerParty: readonly Combatant[];
  readonly enemyParty: readonly Combatant[];
  readonly status: 'active' | 'victory' | 'defeat';
}

export interface GameState {
  readonly player: PlayerCharacter;
  readonly npcs: Record<string, NPC>;
  readonly team: readonly string[];
  readonly combatState: CombatState | null;
  readonly narrativeState: unknown | null;
  readonly timestamp: number;
}

export interface ApiError {
  code: string;
  message: string;
}

export interface ApiResponse<T = unknown> {
  success: boolean;
  data?: T;
  error?: ApiError;
}
