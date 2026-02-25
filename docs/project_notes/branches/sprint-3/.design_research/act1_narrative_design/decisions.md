# Act 1 Narrative Design — In-Progress Decisions Log

**Status:** Paused mid-design. Resume with Scene 1 DEUS encounter specifics.
**Last updated:** 2026-02-24

---

## ECD Coverage State

- **Elements:** Partially explored. Scene structure and content ingredients decided. Individual scene content (NPC identities, choice text, flags) still pending.
- **Connections:** Partially explored. Scene 1 → Scene 2 flag propagation mechanism decided. Scene 2 → Scene 3 connection pending.
- **Dynamics:** Not yet explored. Personality gate placement, choice-to-flag mapping, NPC reaction variants all pending.

---

## Decisions Made

### D1: Branching Strategy — Linear Spine + Variants
One main story path. Personality gates unlock variant dialogue, alternative NPC reactions, and optional side-moments within each scene. Flags track what the player saw/did for later reference. Best demo-to-effort ratio.

### D2: Scene Selection — Original Simplified Starter (Mid-Journey)
Not the Act 1 blueprint (Championship Fight, Dontan, etc.). The demo uses a purpose-built mid-journey slice: player is already a working warrior on the tournament circuit. Skips slow onboarding, puts all systems at full throttle.

### D3: Setting — Gym Town
A tournament Gym Town mid-journey. DEUS presence prominent. Town infrastructure (training facilities, culture visible). The player arrives here for the gym challenge.

### D4: Scene Arc — Three Scenes
1. **Scene 1: Town Arrival** — DEUS-heavy exploration, culture world-building, personality choices with a DEUS NPC. Player's choice sets a flag.
2. **Scene 2: Escalation** — Rogue run-in surfaces as a consequence of the Scene 1 flag. Rogues respond to how player handled DEUS.
3. **Scene 3: Gym Fight** — Climactic combat encounter.

### D5: Rogue Encounter Placement — Scene 2 as Consequence
The Rogue run-in is NOT a standalone Scene 1 discovery. It emerges as an escalation in Scene 2, triggered by the flag set in Scene 1's DEUS interaction. The Rogues respond to whether the player deferred to, challenged, or stayed neutral with DEUS.

### D6: Party Members Are Neutral Warriors
Elena and Kade are warriors who travel with Kael. They are not faction representatives. Their identity is their elemental path, personality distribution, and combat style. The DEUS/Rogues tension is delivered through world NPCs, not party members.

---

## Pending Decisions (Resume Here)

### P1: Scene 1 DEUS Encounter — What Is The Situation?
The moral fork that creates the Scene 2 flag. Options presented to user but not answered:
- DEUS checkpoint/enforcement in the town market (protection tax, ambiguous order vs oppression)
- DEUS recruiting drive (pressuring young fighters to register, ambiguous legitimacy vs gatekeeping)
- DEUS aid operation with a catch (locals resentful of help, ambiguous genuine aid vs manufactured dependency)
→ **User paused here.** This is the first question on resume.

### P2: Scene 1 Personality Gate Design
Which traits gate which choices in Scene 1? What adjustments do they apply?

### P3: Scene 2 Rogue Encounter Details
What do the Rogues want? What is the moral tension? How does the Scene 1 flag change their approach?

### P4: Scene 2 Personality Gate Design
Which traits gate which choices in Scene 2?

### P5: Scene 3 Gym Fight Details
Who is the gym master? What are the narrative stakes? Any personality-reactive dialogue before/after?

### P6: Flag Names and Types
What are the named flags, and what do they track? (e.g., `deus_deferred`, `deus_challenged`, `deus_neutral`)

### P7: Personality Adjustment Values
How much do choices shift traits? What traits are rewarded by each approach?

---

## Open Questions

- Does Kael have a name in the demo, or is naming deferred to frontend?
- Do Elena and Kade have any spoken lines in these scenes, or are they background presence?
- Is the gym fight using an existing encounter fixture or a new encounter designed here?
