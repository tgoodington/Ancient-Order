/**
 * Ancient Order - NPC Definitions
 *
 * Contains the 3 test NPCs for Sprint 1:
 * - Elena (Loyal Scout, DEUS faction)
 * - Lars (Scheming Merchant, Neutral)
 * - Kade (Rogue Outlaw, Rogues faction)
 */

import { NPC, DialogueNode, DialogueOption } from '../types';

// ============================================================================
// Elena - Loyal Scout (DEUS)
// ============================================================================

const elenaDialogue: DialogueNode[] = [
  {
    id: 'elena_greet',
    speakerId: 'npc_scout_elena',
    text: "Hail, friend. I'm Elena, a scout for DEUS. We could use someone with your... determination.",
    options: [
      {
        id: 'elena_opt_1',
        text: 'Tell me about DEUS and what you do.',
        personalityGate: null, // Always available
        personalityAdjustment: {},
        consequenceText: "Elena explains DEUS's mission to protect the innocent from rogue elements.",
        affectionChange: 0,
        trustChange: 5,
        nextNodeId: 'elena_mission',
      },
      {
        id: 'elena_opt_2',
        text: "I don't trust organizations. What's the catch?",
        personalityGate: { trait: 'cunning', operator: 'gte', value: 18 },
        personalityAdjustment: { cunning: -3, logic: 2 },
        consequenceText: "Elena nods thoughtfully. 'No catch—just honest work protecting people. Some distrust authority, and that's fair. But DEUS has no hidden agenda.'",
        affectionChange: 2,
        trustChange: 8,
        nextNodeId: 'elena_mission',
      },
      {
        id: 'elena_opt_3',
        text: 'DEUS sounds noble. I want to help.',
        personalityGate: { trait: 'kindness', operator: 'gte', value: 20 },
        personalityAdjustment: { kindness: 3, empathy: 2 },
        consequenceText: "Elena's face brightens. 'I knew there was good in you. We need more people like you.'",
        affectionChange: 5,
        trustChange: 10,
        nextNodeId: 'elena_mission',
      },
    ],
  },
  {
    id: 'elena_mission',
    speakerId: 'npc_scout_elena',
    text: 'Excellent. We have a problem in the Harbor district. Supplies meant for orphans are going missing. DEUS suspects rogue operatives are intercepting them.',
    options: [
      {
        id: 'elena_mission_opt_1',
        text: "I'll help you investigate. The orphans need those supplies.",
        personalityGate: null, // Always available
        personalityAdjustment: { empathy: 4, kindness: 2 },
        consequenceText: "Elena hands you a map marked with supply route checkpoints. 'Start at the warehouse at dusk. Be careful.'",
        affectionChange: 3,
        trustChange: 8,
        nextNodeId: null, // End conversation
      },
      {
        id: 'elena_mission_opt_2',
        text: "Interesting business opportunity. What's my cut?",
        personalityGate: { trait: 'cunning', operator: 'gte', value: 22 },
        personalityAdjustment: { cunning: 4, empathy: -2 },
        consequenceText: "Elena frowns slightly, but doesn't judge. 'DEUS pays fairly. 50 gold per confirmed rogue apprehended. Help us, and we'll pay you.'",
        affectionChange: -2,
        trustChange: 4,
        nextNodeId: null,
      },
      {
        id: 'elena_mission_opt_3',
        text: 'That sounds dangerous. Can I think about it?',
        personalityGate: { trait: 'patience', operator: 'gte', value: 18 },
        personalityAdjustment: { patience: 2 },
        consequenceText: "Elena understands. 'Take your time. But the supplies are disappearing daily. We can't wait forever.'",
        affectionChange: 1,
        trustChange: 3,
        nextNodeId: null,
      },
    ],
  },
];

const elena: NPC = {
  id: 'npc_scout_elena',
  name: 'Elena',
  archetype: 'Loyal Scout',
  faction: 'DEUS',
  basePersonality: {
    patience: 20,
    empathy: 20,
    cunning: 10,
    logic: 15,
    kindness: 20,
    charisma: 15,
  },
  affection: 0,
  trust: 0,
  joinableInTeam: true,
  availableLocations: ['Harbor', 'Market', 'Tavern'],
  questsAvailable: ['escort_to_tower', 'gather_intel'],
  dialogueTree: elenaDialogue,
};

// ============================================================================
// Lars - Scheming Merchant (Neutral)
// ============================================================================

const larsDialogue: DialogueNode[] = [
  {
    id: 'lars_greet',
    speakerId: 'npc_merchant_lars',
    text: "Well, well. A newcomer with potential. I'm Lars. I deal in... information and goods that others overlook.",
    options: [
      {
        id: 'lars_opt_1',
        text: 'What kind of goods?',
        personalityGate: null, // Always available
        personalityAdjustment: {},
        consequenceText: "Lars leans in conspiratorially. 'Anything that pays well. Weapons, artifacts, forbidden texts. No morality attached to transactions, friend.'",
        affectionChange: 0,
        trustChange: -5, // He's suspicious of naive people
        nextNodeId: 'lars_deal',
      },
      {
        id: 'lars_opt_2',
        text: 'You look like someone who knows how to get what they want.',
        personalityGate: { trait: 'cunning', operator: 'gte', value: 25 },
        personalityAdjustment: { cunning: 3 },
        consequenceText: "Lars grins. 'Now we're speaking the same language. I respect someone who doesn't waste time with pleasantries.'",
        affectionChange: 4,
        trustChange: 8,
        nextNodeId: 'lars_deal',
      },
      {
        id: 'lars_opt_3',
        text: "I'm not interested in black-market dealings.",
        personalityGate: { trait: 'kindness', operator: 'gte', value: 22 },
        personalityAdjustment: { kindness: 2, logic: -2 },
        consequenceText: "Lars shrugs indifferently. 'Your loss. But when you change your mind—and you will—know where to find me.'",
        affectionChange: -8,
        trustChange: -3,
        nextNodeId: null, // End conversation
      },
    ],
  },
  {
    id: 'lars_deal',
    speakerId: 'npc_merchant_lars',
    text: "I have a proposition for you. A shipment of rare components is passing through the warehouse district. The buyer has deep pockets. You deliver it, we split 30-70. I get the larger cut because I arrange everything.",
    options: [
      {
        id: 'lars_deal_opt_1',
        text: "That's robbery. 30% is insultingly low.",
        personalityGate: { trait: 'logic', operator: 'gte', value: 20 },
        personalityAdjustment: { logic: 3, cunning: 2 },
        consequenceText: "Lars laughs. 'Fair point. How about 40-60? You do the dangerous work, I take the organization risk.'",
        affectionChange: 2,
        trustChange: 5,
        nextNodeId: 'lars_negotiate',
      },
      {
        id: 'lars_deal_opt_2',
        text: "I'll do it. 30% sounds reasonable.",
        personalityGate: null, // Always available (fallback)
        personalityAdjustment: { cunning: 4 },
        consequenceText: "Lars nods approvingly. 'Excellent. You understand necessity. Come back when it's done.'",
        affectionChange: 3,
        trustChange: 10,
        nextNodeId: null,
      },
      {
        id: 'lars_deal_opt_3',
        text: "I can't help with smuggling. It's wrong.",
        personalityGate: { trait: 'kindness', operator: 'gte', value: 20 },
        personalityAdjustment: { kindness: 3, empathy: 2 },
        consequenceText: "Lars doesn't look surprised. 'Morals. Understandable, if impractical. When idealism fails you, remember my offer.'",
        affectionChange: -5,
        trustChange: 0,
        nextNodeId: null,
      },
    ],
  },
  {
    id: 'lars_negotiate',
    speakerId: 'npc_merchant_lars',
    text: "45-55 is my final offer. Not a copper less. What's your answer?",
    options: [
      {
        id: 'lars_negotiate_accept',
        text: "45-55 works. Let's do this.",
        personalityGate: null, // Always available
        personalityAdjustment: { cunning: 2 },
        consequenceText: "Lars extends his hand. 'Pleasure doing business with someone practical.'",
        affectionChange: 2,
        trustChange: 8,
        nextNodeId: null,
      },
      {
        id: 'lars_negotiate_refuse',
        text: "I've changed my mind. I'm not doing this.",
        personalityGate: null, // Always available
        personalityAdjustment: { kindness: 4 },
        consequenceText: "Lars doesn't seem bothered. 'Your choice. But you've marked yourself as unreliable now, friend. We're done here.'",
        affectionChange: -10,
        trustChange: -15,
        nextNodeId: null,
      },
    ],
  },
];

const lars: NPC = {
  id: 'npc_merchant_lars',
  name: 'Lars',
  archetype: 'Scheming Merchant',
  faction: 'Neutral',
  basePersonality: {
    patience: 10,
    empathy: 8,
    cunning: 28,
    logic: 25,
    kindness: 12,
    charisma: 17,
  },
  affection: 0,
  trust: -20, // Starts distrustful
  joinableInTeam: false,
  availableLocations: ['Market', 'Warehouse'],
  questsAvailable: ['smuggle_goods', 'information_trade'],
  dialogueTree: larsDialogue,
};

// ============================================================================
// Kade - Rogue Outlaw (Rogues)
// ============================================================================

const kadeDialogue: DialogueNode[] = [
  {
    id: 'kade_greet',
    speakerId: 'npc_outlaw_kade',
    text: "Hey there, friend. Name's Kade. I heard there's a new player in town with... potential. Interested in running jobs with the Rogues?",
    options: [
      {
        id: 'kade_opt_1',
        text: 'Who are the Rogues, and what kind of jobs?',
        personalityGate: null, // Always available
        personalityAdjustment: {},
        consequenceText: "Kade grins. 'We're the freedom fighters everyone pretends don't exist. We take from the rich, help the desperate, and have fun doing it. You in?'",
        affectionChange: 1,
        trustChange: 3,
        nextNodeId: 'kade_jobs',
      },
      {
        id: 'kade_opt_2',
        text: "I like your confidence. What's the first job?",
        personalityGate: { trait: 'charisma', operator: 'gte', value: 24 },
        personalityAdjustment: { charisma: 2, cunning: 3 },
        consequenceText: "Kade laughs and claps you on the back. 'Ha! I knew I'd like you. We steal from a DEUS convoy tomorrow. Gold meant for soldiers, but it's really just extortion from farmers.'",
        affectionChange: 5,
        trustChange: 10,
        nextNodeId: 'kade_jobs',
      },
      {
        id: 'kade_opt_3',
        text: "DEUS is trying to help people. I won't work against them.",
        personalityGate: { trait: 'kindness', operator: 'gte', value: 20 },
        personalityAdjustment: { kindness: 3, empathy: 2 },
        consequenceText: "Kade's smile fades. 'Ah, a true believer. That's... unfortunate. But if you change your mind, the Rogues don't give up on talent.'",
        affectionChange: -4,
        trustChange: 1,
        nextNodeId: null, // End conversation
      },
    ],
  },
  {
    id: 'kade_jobs',
    speakerId: 'npc_outlaw_kade',
    text: "First job: DEUS convoy hits the Trade Route at dawn. Gold shipment. We need someone fast and smart to intercept it. You handle the guards; I'll get the gold out.",
    options: [
      {
        id: 'kade_jobs_opt_1',
        text: "I'm in. When do we move?",
        personalityGate: null, // Always available
        personalityAdjustment: { cunning: 3, charisma: 2 },
        consequenceText: "Kade hands you a worn map. 'Meet me at the old bridge at sundown. Bring something sharp and wear dark clothes.'",
        affectionChange: 3,
        trustChange: 8,
        nextNodeId: null,
      },
      {
        id: 'kade_jobs_opt_2',
        text: "What if we get caught? What's the exit plan?",
        personalityGate: { trait: 'logic', operator: 'gte', value: 18 },
        personalityAdjustment: { logic: 2, cunning: 2 },
        consequenceText: "Kade nods approvingly. 'Smart. We have safe houses in the Underground. DEUS won't find us there. And if things go sideways, we scatter and regroup later.'",
        affectionChange: 4,
        trustChange: 12,
        nextNodeId: null,
      },
      {
        id: 'kade_jobs_opt_3',
        text: 'Robbing a convoy is too risky. Find someone else.',
        personalityGate: { trait: 'patience', operator: 'gte', value: 20 },
        personalityAdjustment: { patience: 2 },
        consequenceText: "Kade shrugs. 'Maybe you're right to be cautious. But we'll need that gold, and we won't forget your caution. Maybe next time.'",
        affectionChange: 0,
        trustChange: 3,
        nextNodeId: null,
      },
    ],
  },
];

const kade: NPC = {
  id: 'npc_outlaw_kade',
  name: 'Kade',
  archetype: 'Rogue Outlaw',
  faction: 'Rogues',
  basePersonality: {
    patience: 12,
    empathy: 8,
    cunning: 25,
    logic: 18,
    kindness: 10,
    charisma: 27,
  },
  affection: 0,
  trust: 0,
  joinableInTeam: true,
  availableLocations: ['Underground', 'Tavern', 'Ruins'],
  questsAvailable: ['raid_convoy', 'steal_artifact'],
  dialogueTree: kadeDialogue,
};

// ============================================================================
// Export Functions
// ============================================================================

/**
 * Creates all NPCs for a new game.
 * Returns a Record keyed by NPC ID.
 */
export function createNPCs(): Record<string, NPC> {
  return {
    [elena.id]: { ...elena },
    [lars.id]: { ...lars },
    [kade.id]: { ...kade },
  };
}

/**
 * Gets an NPC template by ID.
 */
export function getNPCTemplate(npcId: string): NPC | undefined {
  const templates: Record<string, NPC> = {
    [elena.id]: elena,
    [lars.id]: lars,
    [kade.id]: kade,
  };
  return templates[npcId];
}

/**
 * Gets all NPC IDs.
 */
export function getAllNPCIds(): string[] {
  return [elena.id, lars.id, kade.id];
}
