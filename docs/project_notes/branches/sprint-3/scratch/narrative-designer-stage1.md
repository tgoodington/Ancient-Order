# Narrative Designer - Stage 1 Exploration

## Research Findings

### Engine Constraints (from Blueprint)
- Scenes are arrays of `Scene` objects with `id`, `title`, `text`, `choices[]`, `prerequisites[]`
- Choices carry optional `gate` (personality check), `consequence` (effects), and `nextSceneId` (null = end)
- Gates compare a single personality trait against a threshold using `gte`, `lte`, or `eq`
- Consequences can adjust personality, NPC affection/trust, and set/clear flags
- Prerequisites are flat arrays with implicit AND (no OR logic in POC)
- Every scene must have at least one ungated choice (dead-end validation)
- Scene JSON lives in `src/fixtures/scenes/`

### Player Starting State
- Player starts at ~16.67% per trait (100/6 distributed evenly across patience, empathy, cunning, logic, kindness, charisma)
- Traits are integers 5-35, sum always equals 100
- Existing test fixtures show gates like `kindness >= 25` (+8 from baseline), `cunning >= 28` (+11), `patience >= 20` (+3)
- The personality adjustment algorithm redistributes among unadjusted traits after applying deltas

### NPC Landscape
- **Elena** (npc_scout_elena): Loyal Scout archetype. High patience/empathy/kindness (20/20/20). Affection=0, Trust=0. Healer-support in combat.
- **Lars** (npc_merchant_lars): Scheming Merchant. High cunning/logic (28/25). Affection=0, Trust=-20 (wary baseline). Tank-defender in combat. NOT a party member for the demo.
- **Kade** (npc_outlaw_kade): Rogue Outlaw. High cunning/charisma (25/27). Affection=0, Trust=0. Striker-aggressive in combat.

### Narrative Context (from ADRs)
- **ADR-027**: Party members are neutral warriors. Elena's "Loyal Scout" and Kade's "Rogue Outlaw" are internal shorthand, not in-world titles.
- **ADR-028**: Demo is a mid-journey slice in Gym Town. Kael is already trained, party already assembled (Elena + Kade). NOT the opening of Act 1.
- **ADR-029**: Rogues appear as low-level background presence. Hint/run-in/atmosphere. Not the main event. "Team Rocket pattern" — seeds planted for later acts.
- **ADR-026**: Team Synergy system rewards personality alignment. Player choices shift traits, which affects synergy eligibility.

### Content Starting Point
- No existing scene JSON files. Starting from scratch.
- Dialogue engine fixtures provide architectural patterns but no reusable narrative content.
- World-building is minimal: Gym Town on tournament circuit, DEUS (lawful), Rogues (chaotic), Neutral (profit-driven).

### Demo Party Composition
- Kael (player), Elena, Kade are the active party
- Lars is an NPC they encounter in Gym Town (merchant), not a party member
- This means NPC relationship effects target Elena, Kade, and Lars as encounter NPC

---

## ECD Analysis

### Narrative Structure Exploration

The core design challenge is: how to construct 2-3 scenes that (a) demonstrate the personality-driven branching system to investors, (b) feel like a coherent mid-journey slice, and (c) exercise enough engine features to validate the implementation.

**Linear Spine + Variants vs. True Branching:**

A linear spine means all players visit the same scenes in sequence, but choices within each scene create different flavors of the experience and carry forward via flags and personality shifts. True branching means choices route players to entirely different scenes.

For a 2-3 scene investor demo, true branching is wasteful — it doubles content for scenes investors may never see. Linear spine + variants lets every scene be experienced while still showing meaningful choice. The "variants" manifest as: gated dialogue options revealing different information, flag-based prerequisite text changes, and NPC relationship shifts that alter tone.

**3-Scene Arc Structure (per ADR-028):**

ADR-028 specifies: Town Arrival, Escalation, Gym Fight. This maps naturally to a 3-beat dramatic structure:

1. **Scene 1 — Gym Town Arrival**: Establishment. Party arrives, meets Lars (merchant encounter), player makes personality-defining choice about how to engage with the town. Sets flags and personality shifts.
2. **Scene 2 — Escalation / Rogue Run-In**: Rising action. A low-level Rogue encounter creates tension. Prior flags from Scene 1 influence available options. Personality gates appear here (player has had one scene to shift traits). NPC relationship effects deepen.
3. **Scene 3 — Gym Fight Setup**: Climax setup. Leads into combat encounter. Prior flags and personality state determine the tone of entry. This scene's final choice triggers combat (nextSceneId=null or transitions to combat system).

### Choice Design Patterns

**Personality Expression Choices:** Each scene should offer 2-4 choices that map to different personality trait clusters. Not every choice needs a gate — most are ungated but carry personality consequences. The gates are for "earned" options that reward prior investment in a trait.

**Gate Threshold Design:** Player starts at 16.67%. After Scene 1's personality adjustment, they might be at ~18-20 in a favored trait. Scene 2 gates should target ~20 (easily achievable with one prior choice). Scene 3 gates could target ~22-23 (achievable with consistent choices across two scenes). Targeting higher than 23 in a 3-scene demo risks the gate being unreachable.

**Flag Cascading:** Scene 1 sets flags. Scene 2 uses those flags as prerequisites for variant text or gated choices. Scene 3 uses accumulated flags. This creates a visible consequence chain for investors: "I chose X in scene 1, and now in scene 3 I see the result."

### NPC Relationship Opportunities

- **Elena** (in party): Responds to kindness/empathy choices. Trust and affection shift based on whether player is protective, collaborative, or dismissive.
- **Kade** (in party): Responds to cunning/charisma choices. Respects boldness, cleverness. Distrusts overly cautious or preachy approaches.
- **Lars** (encounter NPC): The merchant with trust=-20. A key investor demo moment: can the player earn Lars's trust through smart dealing, or does Lars remain wary? Responds to logic/cunning. His low starting trust makes any positive shift feel dramatic.

### Scene Content Concepts

**Scene 1 — Arrival at Ironhold Gym Town:**
- Party arrives at a bustling tournament town
- Lars has a merchant stall; party needs supplies/information
- Core choice: How does Kael approach Lars?
  - Friendly/kind approach (kindness +2, empathy +1; Lars affection +5)
  - Shrewd bargaining (cunning +2, logic +1; Lars trust +5) — speaks his language
  - Direct/impatient (patience -2, charisma +2; Lars affection -5)
  - [Ungated fallback] Neutral transaction (minimal effects)
- Sets flags: `approached_lars_friendly`, `approached_lars_shrewd`, `approached_lars_direct`
- DEUS presence noted in scene text (uniformed officials, order banners)

**Scene 2 — Market District Disturbance:**
- A commotion in the market — a young thief (Rogue-affiliated) is caught stealing
- DEUS guards are heavy-handed
- Core choice: How does Kael respond?
  - [Gate: kindness >= 20] Intervene compassionately — de-escalate (kindness +2, Elena trust +10, sets `defended_thief`)
  - [Gate: cunning >= 20] Use the distraction to gather intel (cunning +2, Kade affection +5, sets `gathered_intel`)
  - Stay out of it — not our problem (logic +2, patience +1, sets `stayed_neutral`)
  - [Ungated fallback] Watch carefully, say nothing (minimal effects, sets `observed_disturbance`)
- Prerequisite variant: If `approached_lars_shrewd`, Lars tips you off about the thief beforehand (bonus scene text, no mechanical difference — demonstrates prerequisite system)
- Rogue presence seeded per ADR-029: thief wears a subtle symbol, party may or may not notice

**Scene 3 — Gym Registration / Fight Setup:**
- Registering for the gym fight tournament
- Opponent selection or fight conditions are influenced by prior choices
- Core choice: Fighting style declaration
  - [Gate: patience >= 22] Methodical approach — study opponent first (patience +2, sets `methodical_fighter`)
  - Bold challenge — call out the strongest (charisma +3, courage flag, Kade affection +10, sets `bold_challenger`)
  - [Prerequisite: `gathered_intel`] Use intelligence to pick favorable matchup (logic +2, cunning +1, sets `tactical_fighter`)
  - [Ungated fallback] Standard registration (sets `standard_fighter`)
- Scene ends (nextSceneId=null) — transitions to combat encounter
- Flag accumulated from all 3 scenes could influence combat encounter parameters in future sprints

### Investor Demo Flow Analysis

The 3-scene arc demonstrates to investors:
1. **Scene 1**: Personality system works — choices shift traits, NPC relationships respond
2. **Scene 2**: Consequences carry forward — flags from Scene 1 affect Scene 2 options, personality gates reward prior choices
3. **Scene 3**: Accumulated state matters — prerequisites, gates, and flags from prior scenes combine. Leads into combat (showing narrative-to-combat bridge)

Total playtime: ~3-5 minutes of reading/choosing. Enough to demonstrate the system without overstaying.

---

## Assumptions

### A1: 3-Scene Linear Spine Structure
- **Default**: Three scenes in fixed sequence (Arrival → Disturbance → Gym Registration), all players visit all three, with variant content within each.
- **Rationale**: ADR-028 specifies "3-scene structure: Town Arrival → Escalation → Gym Fight." Linear spine caps content volume (critical for POC) while still demonstrating meaningful branching through gated choices and flag-based variants. True branching would double or triple the content for scenes investors might miss.

### A2: Personality Adjustments Are Small Per Choice (+1 to +3)
- **Default**: Individual choice consequences shift traits by 1-3 points, with the primary trait getting +2 or +3 and a secondary trait getting +1.
- **Rationale**: With 6 traits summing to 100 and the redistribution algorithm, large shifts (+5 or more) in a single choice would create jarring personality swings. Small increments across 3 scenes allow a trait to reach ~22 from baseline 16.67, which is enough to hit reasonable gates without feeling arbitrary. Existing test fixtures show adjustments in the +1 to +3 range.

### A3: Lars Is an Encounter NPC, Not a Party Member
- **Default**: Lars appears in Scene 1 as a merchant the party interacts with, but is not in the combat party (which is Kael + Elena + Kade).
- **Rationale**: Key_facts and ADR-028 specify the demo party as "Kael, Elena, Kade." Lars's Scheming Merchant archetype and trust=-20 baseline make him a compelling encounter NPC whose relationship the player can shift through choices, but he doesn't participate in combat or travel with the party.

### A4: Scene Text Is Narrative Prose (Not Dialogue Trees)
- **Default**: The `text` field in each Scene object contains narrative prose describing the situation, not a dialogue tree. Dialogue-style interaction happens through choice selection, not within scene text.
- **Rationale**: The scene graph engine is separate from the dialogue engine (ADR-031 confirmed independent modules). Scene text sets the stage; choices are the interactive element. This avoids mixing two interaction paradigms in the POC.

### A5: NPC Affection/Trust Shifts Are Moderate (5-10 Per Choice)
- **Default**: NPC relationship effects range from -10 to +10 per choice, with most being +5 or -5.
- **Rationale**: NPC affection/trust clamp to [-100, +100]. With only 3 scenes (and not every choice affecting every NPC), shifts of 5-10 create meaningful but not overwhelming changes. A +10 trust on Lars (from -20 baseline) brings him to -10 — still wary but warming. This gradual progression is more narratively satisfying than binary swings.

### A6: Combat Transition Is Out of Scope for Scene JSON
- **Default**: Scene 3's final choice sets nextSceneId=null, signaling end of narrative. The actual transition to combat is handled by game logic outside the scene graph.
- **Rationale**: The scene graph engine handles narrative state transitions. Combat initialization is a separate system. The bridge between narrative end and combat start is an integration concern for T12 (final integration validation), not scene content authoring.

---

## Key Decisions

### D1: Gate Threshold Strategy
- **Options**:
  - **(A) Conservative gates (18-20)**: Achievable after a single favorable choice from baseline 16.67. Almost any player hits them.
  - **(B) Moderate gates (20-23)**: Achievable after 1-2 consistent choices. Rewards intentional play.
  - **(C) Ambitious gates (24-28)**: Requires 2-3 strongly consistent choices. May be unreachable for some players in a 3-scene demo.
- **Recommendation**: **(B) Moderate gates (20-23)** with Scene 2 gates at ~20 and Scene 3 gates at ~22-23. This ensures gates are achievable but feel earned. Scene 1 has no personality gates (player hasn't made choices yet), Scene 2 gates are easy to hit after one choice, and Scene 3 gates reward consistent investment. Specifically: Scene 2 gates at 20 (achievable with +3 from one Scene 1 choice), Scene 3 gates at 22 (achievable with +3 and +2 from two prior choices).
- **Risk if wrong**: If gates are too low, gating feels meaningless (everyone passes). If too high, gates are unreachable and the investor demo never shows the gated content. Moderate range gives the best chance of demonstrating the system.

### D2: Flag Naming Convention
- **Options**:
  - **(A) Action-based**: `helped_thief`, `bargained_with_lars`, `challenged_opponent`
  - **(B) Scene-prefixed**: `scene1_friendly_approach`, `scene2_intervened`, `scene3_bold`
  - **(C) Trait-cluster**: `kind_response_market`, `cunning_response_market`
  - **(D) Hybrid verb_object**: `defended_thief`, `gathered_intel`, `approached_lars_shrewd`
- **Recommendation**: **(D) Hybrid verb_object** — semantically meaningful flags that describe the action taken. Format: `[verb]_[object/context]`. Examples: `approached_lars_friendly`, `defended_thief`, `gathered_intel`, `bold_challenger`. These are self-documenting, grep-friendly, and don't tie flags to scene numbers (which could change).
- **Risk if wrong**: Poor flag names cause confusion during implementation and debugging. The hybrid approach is the most readable and maintainable. Scene-prefixed names break if scenes are reordered; trait-cluster names don't describe what happened.

### D3: How Many Choices Per Scene
- **Options**:
  - **(A) 2-3 choices**: Minimal, fast pacing. Quick decisions.
  - **(B) 3-4 choices**: Standard RPG feel. 1-2 gated + 1-2 ungated.
  - **(C) 4-5 choices**: Rich but potentially overwhelming for a short demo.
- **Recommendation**: **(B) 3-4 choices per scene** — typically 1-2 personality-gated options and 2 ungated options (one meaningful, one "safe" fallback). This gives investors enough variety to see the system work without analysis paralysis. Scene 1 can have 4 (establishing personality direction), Scene 2 can have 4 (demonstrating gates), Scene 3 can have 4 (demonstrating prerequisites + gates).
- **Risk if wrong**: Too few choices makes the demo feel linear. Too many choices slow pacing and increase content authoring burden. 3-4 is the established RPG sweet spot.

### D4: Rogue Encounter Intensity in Scene 2
- **Options**:
  - **(A) Direct Rogue NPC encounter**: Named Rogue character with dialogue
  - **(B) Indirect Rogue presence**: Thief with Rogue symbol, DEUS guards' reaction is the focus
  - **(C) Pure atmosphere**: Graffiti, whispers, no direct encounter
- **Recommendation**: **(B) Indirect Rogue presence** — a caught thief wearing a subtle Rogue symbol. The scene is about the player's moral response to DEUS heavy-handedness, not about the Rogues directly. This seeds the faction tension per ADR-029 ("hint, run-in, atmosphere") without making Rogues the main event. The thief is unnamed and doesn't recur.
- **Risk if wrong**: Option A makes Rogues too prominent too early (violates ADR-029). Option C might be too subtle for investors to notice the faction setup. Option B threads the needle — visible enough to register, understated enough to feel like a seed.

### D5: Gym Town Name
- **Options**:
  - **(A) "Gym Town"**: Use the placeholder name from design docs
  - **(B) Named town**: Give it a proper name (e.g., "Ironhold", "Ashgate", "Millhaven")
  - **(C) Unnamed**: Reference it as "the town" or "the tournament town"
- **Recommendation**: **(B) Named town — "Ironhold"** — a name suggesting strength and permanence, fitting a tournament circuit stop. Proper names make the world feel real for investor demos. "Gym Town" reads as a placeholder. The name should evoke a fortified settlement where combat training is prominent.
- **Risk if wrong**: Minimal. The name is cosmetic and easily changed. But having a real name for the demo adds polish.

### D6: Scene Text Length and Tone
- **Options**:
  - **(A) Brief/punchy**: 2-3 sentences per scene. Action-oriented.
  - **(B) Moderate prose**: 4-6 sentences. Sets atmosphere, names characters, describes the environment.
  - **(C) Rich narrative**: 7+ sentences. Full descriptive prose with NPC dialogue inline.
- **Recommendation**: **(B) Moderate prose (4-6 sentences)** — enough to establish setting and mood, short enough to not bore demo viewers. Each scene should orient the player (where am I, who's here, what's happening) and end with a clear prompt for choice. Tone should be confident fantasy prose — not purple, not terse. NPC dialogue can appear in choice text rather than scene body.
- **Risk if wrong**: Too brief feels like a tech demo. Too long and investors lose interest before reaching the choices (which are the real showcase).

---

## Risks Identified

### R1: Gate Unreachability in Short Demo
**Risk**: With only 3 scenes and small personality adjustments per choice, Scene 3 gates might be unreachable for players who didn't make "optimal" choices.
**Mitigation**: Keep Scene 3 gates at 22 maximum. Test that at least one reasonable choice path through Scenes 1-2 yields enough trait accumulation to pass each gate. Ensure ungated fallbacks exist so unreachable gates never block progress.

### R2: Flag Explosion / Tracking Complexity
**Risk**: With 3-4 choices per scene and 3 scenes, we could have 9-12 flags. Tracking which flags gate which prerequisites becomes complex.
**Mitigation**: Limit flag-based prerequisites to 1-2 instances across the demo. Scene 2 can check one Scene 1 flag for variant text. Scene 3 can check one Scene 2 flag for a gated choice. Keep the prerequisite graph shallow and well-documented.

### R3: Narrative Tone Inconsistency
**Risk**: Scene text authored in isolation (as JSON strings) may have inconsistent voice, especially across multiple authoring sessions.
**Mitigation**: Establish tone guidelines in the design document: confident fantasy, present tense for scene descriptions, second person ("You approach...") or third person ("Kael approaches..."). Pick one and stick with it. Recommend third person to match RPG convention and keep consistency with NPC names.

### R4: Lars Interaction Feels Thin
**Risk**: Lars is the only non-party NPC in the demo. If his Scene 1 interaction is just a transaction, the investor demo doesn't showcase NPC relationship depth.
**Mitigation**: Make the Lars interaction the emotional anchor of Scene 1. His wary demeanor (trust=-20) should be palpable. Different approaches should yield visibly different reactions. If the player earns even a small trust gain, Lars could provide the Scene 2 tip-off (flag-based prerequisite), creating a satisfying callback.

### R5: Investor Demo Doesn't Reach Combat
**Risk**: If the narrative takes too long, demo viewers might not see the combat system (which is the other major Sprint 2 deliverable).
**Mitigation**: Keep scenes lean (moderate prose). Ensure the 3-scene arc can be played through in under 5 minutes. Scene 3 explicitly terminates with a combat transition hook. The demo operator can fast-track if needed.

### R6: Personality System Not Visibly Demonstrated
**Risk**: Personality shifts happen numerically in the backend. If the demo UI doesn't show trait values, investors won't see the system working.
**Mitigation**: Design at least one moment where a personality gate blocks/unlocks visibly. The player should experience "I can't choose this because my kindness isn't high enough" or "I earned this option through my prior choices." This is the clearest proof of the system for investors. Scene 2 gates are the ideal showcase moment.

---

## Recommended Approach

### Architecture: Linear Spine + Variants, 3 Scenes

Adopt the 3-scene linear spine structure mandated by ADR-028:

1. **Scene 1: Arrival at Ironhold** (`scene_ironhold_arrival`)
2. **Scene 2: Market Disturbance** (`scene_market_disturbance`)
3. **Scene 3: Gym Registration** (`scene_gym_registration`)

All players traverse all three scenes in order. Variation comes from choice selection within each scene, which shifts personality traits, NPC relationships, and sets flags that influence subsequent scenes.

### Choice Architecture Per Scene

**Scene 1 — Arrival at Ironhold** (4 choices, 0 gates, 0 prerequisites):
- Establishes personality direction. Player meets Lars and chooses an approach.
- Each choice shifts 2 traits and affects Lars's affection or trust.
- Sets one flag per choice (e.g., `approached_lars_friendly`, `approached_lars_shrewd`, `approached_lars_direct`, `approached_lars_neutral`).
- No gates because the player has had no prior choices to build traits.

**Scene 2 — Market Disturbance** (4 choices, 2 gates, 1 prerequisite):
- Demonstrates personality gates and flag prerequisites.
- Gate 1: `kindness >= 20` — compassionate intervention.
- Gate 2: `cunning >= 20` — exploit the distraction.
- Prerequisite: If `approached_lars_shrewd` is set, bonus context text appears (Lars warned you about thieves).
- Ungated options: stay neutral, observe quietly.
- NPC effects: Elena responds to kindness, Kade responds to cunning.

**Scene 3 — Gym Registration** (4 choices, 1 gate, 1 prerequisite):
- Demonstrates accumulated state and combat bridge.
- Gate: `patience >= 22` — methodical approach (achievable if player chose patient options in Scenes 1-2).
- Prerequisite: `gathered_intel` flag from Scene 2 unlocks a tactical option.
- Bold challenge option targets charisma and pleases Kade.
- Ungated fallback: standard registration.
- All choices set nextSceneId = null (narrative ends, combat begins).

### Flag Registry

| Flag | Set In | Used In | Meaning |
|------|--------|---------|---------|
| `approached_lars_friendly` | Scene 1 | — | Player was kind to Lars |
| `approached_lars_shrewd` | Scene 1 | Scene 2 (prereq) | Player bargained cleverly with Lars |
| `approached_lars_direct` | Scene 1 | — | Player was impatient with Lars |
| `approached_lars_neutral` | Scene 1 | — | Player had minimal interaction |
| `defended_thief` | Scene 2 | — | Player intervened for the thief |
| `gathered_intel` | Scene 2 | Scene 3 (prereq) | Player gathered information during disturbance |
| `stayed_neutral_market` | Scene 2 | — | Player stayed out of market incident |
| `observed_disturbance` | Scene 2 | — | Player watched silently |
| `methodical_fighter` | Scene 3 | (combat) | Player chose studied approach |
| `bold_challenger` | Scene 3 | (combat) | Player called out strongest opponent |
| `tactical_fighter` | Scene 3 | (combat) | Player used intel for advantage |
| `standard_fighter` | Scene 3 | (combat) | Player registered normally |

### Personality Gate Summary

| Scene | Gate | Threshold | Achievable Via |
|-------|------|-----------|----------------|
| Scene 2 | `kindness >= 20` | 20 | Scene 1 friendly approach (+2 kindness, +1 empathy) brings kindness to ~18.67; needs small buffer — set at 19 instead for safety |
| Scene 2 | `cunning >= 20` | 20 | Scene 1 shrewd approach (+2 cunning, +1 logic) brings cunning to ~18.67; same buffer consideration |
| Scene 3 | `patience >= 22` | 22 | Requires patience investment across 2 prior scenes (~+3 each time) |

**Revised Gate Recommendation**: After analysis, Scene 2 gates at 19 (not 20) ensure reachability after a single +2-3 adjustment from baseline 16.67. Scene 3 gate at 22 requires two scenes of investment (+3 then +2, reaching ~21.67, rounded). This may need the gate set at 21 for safety. Final thresholds should be validated against the personality adjustment algorithm during JSON authoring (T11).

### NPC Relationship Arc

- **Lars**: Starts at trust=-20. Scene 1 choices can give +5 to +10 trust or +5 affection depending on approach. Best case after Scene 1: trust=-10 (warming but still wary). Lars does not appear in Scenes 2-3 directly but his tip-off (flag-based) shows lasting impact.
- **Elena**: Scene 2 is her moment. Defending the thief aligns with her high empathy/kindness — trust +10. Being callous or exploitative costs Elena trust (-5). Her reaction demonstrates the party relationship system.
- **Kade**: Scene 2 rewards cunning (his trait), Scene 3 rewards boldness. Affection +5 to +10 for choices that match his adventurous personality. He's less reactive than Elena — more nods of approval than emotional responses.

### Narrative Voice

- Third person, past tense: "Kael surveyed the crowded market square..."
- Confident fantasy tone, not flowery. Lean and evocative.
- Scene text: 4-6 sentences establishing setting, characters present, and situation.
- Choice text: 1 sentence, clear action statement. "Offer Lars a fair price and a friendly word." / "Haggle hard — find out what Lars really knows."
- NPC names used naturally in scene text to establish who's present.

### File Structure

Per ADR-032, scene JSON files go in `src/fixtures/scenes/`. Recommended structure:
- `src/fixtures/scenes/act1_demo.json` — single file containing all 3 scenes as an array

Single file preferred over per-scene files because: (a) the scenes form one coherent graph, (b) cross-scene references (nextSceneId) are easier to validate in one file, (c) the scene graph engine loads a graph as a parameter, not individual scenes.

### Implementation Priority for T11

1. Author Scene 1 JSON with all 4 choices, effects, and flags
2. Author Scene 2 JSON with gates, prerequisites, and NPC effects
3. Author Scene 3 JSON with accumulated gates, prerequisites, and terminal choices
4. Validate: every scene has ungated fallback, all nextSceneIds reference valid scenes, all flags are spelled consistently
5. Write structural unit tests (dead-end validation, gate format, flag consistency)
