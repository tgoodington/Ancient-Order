# Claude Code Project Handoff

## Turn-Based Combat RPG Prototype - Act 1

Complete Project Overview & Development Roadmap

---

## PROJECT OVERVIEW

You are building a turn-based combat RPG prototype that demonstrates two equally important systems: (1) sophisticated personality-driven narrative mechanics, and (2) tactical combat with deep mechanical depth. The game targets 2-3 hours of Act 1 gameplay and is designed as a web application for investor pitch.

### Design Philosophy

- **Personality is as important as combat.** Both systems are fully developed.
- **Only the player character's personality changes.** NPCs have fixed archetypes and remain consistent.
- **Choices have lasting narrative consequences without mechanical punishment.** "Doors close, others open."
- **No inventory/RPG menu hellscape.** Minimal UI with visual cues mattering more than text.
- **Narrative choices are hard-coded options,** not free-form text.
- **Blurred lines between factions** (DEUS vs. Rogues). Neither side is clearly right or wrong.

---

## TECHNICAL ARCHITECTURE

### Stack

- **Backend:** Node.js + Express (immutable game state, all game logic)
- **Frontend:** React (UI rendering, visual cues, minimal text)
- **Persistence:** JSON file storage (works for prototype, easy to migrate)
- **Deployment:** Vercel (zero setup for users, single URL for investors)

### Core Architecture Principle

Backend generates new immutable game state on every action. Frontend syncs with backend state but never modifies it. This prevents desync bugs and makes save/load trivial.

---

## GAME SYSTEMS

### 1. Personality System

Six traits (Patience, Empathy, Cunning, Logic, Kindness, Charisma) tracked as percentages, always 5-35% range, sum to 100%. Each trait maps to a path (elemental affinity).

When player makes a choice, one trait increases by 6%, others decrease proportionally. When one trait hits 35%, increases trigger decreases in other traits to maintain balance.

### 2. NPC Interaction & Personality Gates

NPCs have fixed personalities (never change). Each NPC has preferred traits. Gate logic:

```
IF player_trait < npc.minimumThreshold (typically 10%)
  → NPC ignores player entirely

ELSE IF player_trait >= npc.preferredTrait
  → Full warmth dialogue, quest offered immediately

ELSE IF player_trait >= npc.preferredTrait * 0.6
  → Cordial dialogue, quest offered with hesitation

ELSE
  → Skeptical dialogue, quest still offered but player must prove themselves

Result: Everyone gets content access. Emotional tone varies.
```

### 3. Combat System

3v3 turn-based system with 5 phases per round. **CRITICAL:** Port all formulas from the Excel GM_Combat_Tracker.xlsx documentation (available in project).

#### Five Phases:

1. **Phase 1 - AI Decision:** Enemy team selects actions, locked
2. **Phase 2 - Visual Info:** Frontend displays enemy stances + stamina colors
3. **Phase 3 - PC Declaration:** Player selects actions for all PCs
4. **Phase 4 - Sort Actions:** By priority (Defend→Group→Attack/Special→Evade, then by Speed + random)
5. **Phase 5 - Resolution:** Execute each attack (Rank KO → Blindside → Defense → Damage → Counter chains → Status)

#### Critical Mechanics:

- **Rank KO:** If attacker rank > target rank by 0.5+, roll for instant knockout
- **Blindside:** If attacker speed > target speed, roll to force Defenseless
- **Crushing Blow:** If Block used and action power > target power, roll to debuff Block rates
- **Parry Chains:** Successful Parry triggers counter, chain continues until failure/KO/stamina depleted
- **Defense Resolution:** Defenseless (full damage) / Block (mitigated) / Dodge (evade) / Parry (counter)

#### Paths & Energy:

- **Six paths:** Fire (Charisma), Water (Patience), Air (Empathy), Earth (Logic), Shadow (Cunning), Light (Kindness)
- **Energy segments:** Players build energy through actions. Energy caps at 6 segments.
- **Ascension levels 0-3:** Each requires more total segments, grants bonuses
- **Special attacks:** Cost 1-5 segments, boost damage by 10% per segment, force target into specific defense
- **Path styles:** Action paths boost own defenses (Fire/Air/Light), Reaction paths debuff enemy (Water/Earth/Shadow)

### 4. Team Synergy

No penalties for imbalance. Every composition gets bonuses:

- **Balanced** (all traits 15-25%): +5% all stats, +10% team XP
- **Specialist** (one trait 30%+): +15% trait-specific effects
- **Harmony** (two traits 25%+): +8% dual effects, special techniques unlock

---

## ACT 1 NARRATIVE STRUCTURE

Act 1 demonstrates all mechanics. Six scenes totaling 2-3 hours gameplay.

### Scene Breakdown

**Scene 1 - The Championship Fight:**
Cinematic intro. Player controls a powerful team through a championship match (tutorial). Reveals player character as a kid. Establishes world, mentor (Dontan), and warrior culture.

**Scene 2 - Town Exploration:**
Player completes 4 tasks to convince Dontan to be their mentor: (1) Learn culture at school, (2) Learn tech at DEUS facility, (3) Help Auntie M with cat (personality choice), (4) Battle another team with Dontan coaching.

**Scene 3 - Dontan's Trials:**
Dontan tests player's warrior worth. Dialogue-based trials proving they understand the code and commitment to training.

**Scene 4 - Time Skip:**
Abstract 5-year training montage. No gameplay, pure narrative/character development.

**Scene 5 - Setting Out:**
Team registers at town hall as official competitors. Final preparations in hometown. Departure for championship journey.

**Scene 6 - First Gym Town:**
Act 1 ends at arrival in first town. New NPCs demonstrate personality gates. Sets up first gym battle (Act 2 preview). Player experiences how personality affects new NPC interactions.

---

## USER INTERFACE

### Design Philosophy

Minimal UI. Visual cues prioritized over text. Equinox (in-world armor tech) serves as the primary HUD, displaying all essential info without menu clutter.

### Equinox HUD

In-world device showing: stamina bar (color-coded), energy segments, current stance indicator, personality breakdown (expandable), path selector (press center to switch between two paths).

**Stamina colors:**
- Green (100-75%)
- Yellow (74-50%)
- Orange (49-25%)
- Red (24-1%)
- Black (0% KO)

**Aesthetic:** Minimal, armor-like, matches concept art (browns, leather, metal). Focus on clarity over decoration.

### Combat UI

- **Enemy Stance View:** Three enemy combatants with stance indicators (A/D/E/S/G) and stamina colors. Players react to visual cues.
- **Action Declaration:** Three action buttons per PC (Attack/Defend/Evade/Special/Group). Visual selection.
- **Action Queue:** After resolution, show sorted action order with damage numbers, status changes, minimal text.

### Town Navigation

- **Click-based zone navigation** (not free-roaming). Static background image with clickable NPC/location zones.
- **Dialogue UI:** NPC portrait + text + hard-coded player response options (numbered buttons or full buttons).

---

## DEVELOPMENT ROADMAP

### Sprint 1: Backend Core (Game State & Personality)

- Define immutable game state structure
- Implement personality trait adjustment logic
- Build NPC archetype system + dialogue adaptation
- Implement personality gate logic (blended threshold + relative)

### Sprint 2: Combat Engine

- Port all Excel combat formulas (Rank KO, Blindside, Crushing Blow, Defense, Counters)
- Implement 5-phase round structure
- Build action priority sorting
- Implement path/energy/ascension tracking

### Sprint 3: Narrative & State Machine

- Design Act 1 scene progression
- Build choice-tracking & consequence system
- Implement team synergy bonus calculations

### Sprint 4: Persistence & API

- Implement save/load (JSON serialization)
- Build all REST API endpoints (game state, interactions, combat, save/load)

### Sprint 5-7: Frontend (React)

- **Sprint 5:** React setup, GameStateProvider, Equinox HUD component
- **Sprint 6:** Town scene (navigation, dialogue UI, NPC interactions)
- **Sprint 7:** Combat scene (UI, action declaration, results display)

### Sprint 8-9: Testing & Deployment

- **Sprint 8:** Full playthrough testing, balance tweaks, bug fixes
- **Sprint 9:** Deploy to Vercel, share investor link

---

## KEY DOCUMENTS

- **GM_Combat_Tracker.xlsx:** Complete combat system with all formulas, mechanics, progression
- **GM_Combat_Tracker_Documentation.docx:** Excel analysis (17 sheets explained, key formulas mapped)
- **Game_Technical_Architecture.docx:** Complete technical spec (state structures, API, components)
- **Combat_System_Implementation_Guide.docx:** Developer-focused combat formulas and mechanics
- **Narrative Lore Document:** Story, creation legend, character bios, world setting
- **NPC Roster** (to be developed): Names, archetypes, personalities, preferred traits, dialogue variations

---

## WORKING PRINCIPLES

- Start with Sprint 1 bare structure + example data. Iterate based on what feels right.
- Frontend-design skill should be used for UI components (distinctive, production-grade aesthetics).
- React Artifacts work well for interactive UI mockups during development.
- **Combat formulas are non-negotiable** (port from Excel exactly). Game balance depends on them.
- Personality system is core design pillar. Every choice matters. Test extensively.
- Save/load must work flawlessly (JSON serialization and deserialization).
- NPC roster and dialogue variations will be provided as they're developed.

---

## NEXT STEPS

1. Create Sprint 1 Node.js backend with game state + personality system + example NPCs
2. Design UI components with frontend-design skill (Equinox HUD, combat board, dialogue)
3. Build out NPC roster, Act 1 scenes, and dialogue variations
4. Integrate all systems and iterate on balance/feel

