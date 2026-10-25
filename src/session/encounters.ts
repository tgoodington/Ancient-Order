import type { EncounterConfig } from '../types/combat.js';
import encounterDemoConfig from '../fixtures/encounter.json' with { type: 'json' };

// Available encounter configurations, moved from the retired server combat routes.
// Stats are representative; verify against GM Combat Tracker.xlsx "Battle Scenarios"
// before the investor/publisher pitch demo. encounter_test is used by tests.
export const ENCOUNTER_REGISTRY: Record<string, EncounterConfig> = {
  encounter_demo: encounterDemoConfig as EncounterConfig,
  encounter_test: {
    id: 'encounter_test',
    name: 'Test Encounter (3v3)',
    playerParty: [
      {
        id: 'player_warrior',
        name: 'Warrior',
        archetype: 'warrior',
        rank: 2.0,
        stamina: 120,
        power: 18,
        speed: 14,
        elementalPath: 'Fire',
        ascensionLevel: 0,
        reactionSkills: {
          block: { SR: 0.6, SMR: 0.5, FMR: 0.2 },
          dodge: { SR: 0.4, SMR: 0.84, FMR: 0.3 },
          parry: { SR: 0.3, SMR: 1.0, FMR: 0.25 },
        },
      },
      {
        id: 'player_scout',
        name: 'Scout',
        archetype: 'scout',
        rank: 1.5,
        stamina: 90,
        power: 14,
        speed: 20,
        elementalPath: 'Air',
        ascensionLevel: 0,
        reactionSkills: {
          block: { SR: 0.4, SMR: 0.4, FMR: 0.15 },
          dodge: { SR: 0.65, SMR: 0.9, FMR: 0.2 },
          parry: { SR: 0.2, SMR: 0.93, FMR: 0.2 },
        },
      },
      {
        id: 'player_mage',
        name: 'Mage',
        archetype: 'mage',
        rank: 1.5,
        stamina: 80,
        power: 22,
        speed: 12,
        elementalPath: 'Shadow',
        ascensionLevel: 0,
        reactionSkills: {
          block: { SR: 0.3, SMR: 0.35, FMR: 0.1 },
          dodge: { SR: 0.35, SMR: 0.83, FMR: 0.15 },
          parry: { SR: 0.15, SMR: 0.9, FMR: 0.15 },
        },
      },
    ],
    enemyParty: [
      {
        id: 'enemy_brute',
        name: 'Iron Brute',
        archetype: 'elena_loyal_scout',
        rank: 2.0,
        stamina: 110,
        power: 20,
        speed: 10,
        elementalPath: 'Earth',
        ascensionLevel: 0,
        reactionSkills: {
          block: { SR: 0.65, SMR: 0.55, FMR: 0.25 },
          dodge: { SR: 0.2, SMR: 0.8, FMR: 0.3 },
          parry: { SR: 0.2, SMR: 0.93, FMR: 0.2 },
        },
      },
      {
        id: 'enemy_archer',
        name: 'Shadow Archer',
        archetype: 'kade_bold_outlaw',
        rank: 1.5,
        stamina: 85,
        power: 16,
        speed: 18,
        elementalPath: 'Shadow',
        ascensionLevel: 0,
        reactionSkills: {
          block: { SR: 0.35, SMR: 0.35, FMR: 0.15 },
          dodge: { SR: 0.6, SMR: 0.89, FMR: 0.2 },
          parry: { SR: 0.25, SMR: 0.97, FMR: 0.2 },
        },
      },
      {
        id: 'enemy_shaman',
        name: 'Bog Shaman',
        archetype: 'lars_cautious_merchant',
        rank: 1.5,
        stamina: 75,
        power: 19,
        speed: 11,
        elementalPath: 'Water',
        ascensionLevel: 0,
        reactionSkills: {
          block: { SR: 0.45, SMR: 0.4, FMR: 0.15 },
          dodge: { SR: 0.35, SMR: 0.83, FMR: 0.2 },
          parry: { SR: 0.3, SMR: 1.0, FMR: 0.2 },
        },
      },
    ],
  },
};
