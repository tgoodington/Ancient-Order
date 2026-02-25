# Design Decisions: Team Synergy System

## Session: 2026-02-24

### D1: Paradigm-based model (not pair-based lookup, not pure trait-distance)
Named archetypes (paradigms) with trigger conditions. Party's aggregate personality distribution evaluated against paradigm patterns. Extensible by adding config entries. Chosen over pair-based lookup (which ignored player personality) and pure trait-distance (which was too abstract).

### D2: Highest-only application
Only the best-matched satisfied paradigm applies its bonus. Tiebreak: Well Rounded > Bond. Rewards commitment to a single party identity. Chosen over additive stacking.

### D3: Player as fourth node
Player personality participates in the calculation alongside NPC personalities. Party = player + NPC members. Calculator signature takes player personality separately (needed for Bond's directional comparison).

### D4: Two POC paradigms
- **Well Rounded** (ATK +10%): Every trait has a party-member at ≥25%. Player covers the gaps the NPCs don't.
- **Bond** (SPD +10%): Player aligns ≥80% with one NPC's dominant traits. Dominant traits derived dynamically (NPC's top 2 by value).

### D5: Binary thresholds
Meet the threshold → full bonus. No partial bonuses. Clean feedback story for investor demo.

### D6: Direct stat modification at initCombatState
Scale `power` or `speed` directly when constructing combatants. No new buff types, no pipeline changes. Simpler POC approach. ActiveBuffs currently handles defense roll modifiers, not ATK/SPD.

### D7: Calculator lives in narrative/
`src/narrative/synergyCalculator.ts`. Reads personality data (narrative concept), consumed by combat/sync.ts. Dependency direction: combat imports from narrative.

### D8: Bond dominant traits derived dynamically
NPC's top 2 traits by value define its "dominant traits" — no manual tagging. Extensible to any future NPC automatically.

### D9: Both paradigms apply to all party members
Whether Well Rounded (party-wide coverage) or Bond (player-NPC alignment), all three combatants receive the bonus. Bond thematically energizes the whole party.

### D10: Flat 10% bonus for both paradigms
ATK: power × 1.10. SPD: speed × 1.10. Equal magnitude. Speed is significant — controls activation order and Blindside calculations.
