# Architectural Decisions

This file logs architectural decisions (ADRs) with context and trade-offs.

---

## Entries

### ADR-001: Immutable State Management (2026-01-22)

**Context:**
- Need reliable save/load functionality
- Combat and personality systems must be deterministic
- Future React integration will benefit from predictable state updates

**Decision:**
- All state updates create new objects (spread operator pattern)
- Never mutate existing objects
- Timestamp updated on every state change

**Alternatives Considered:**
- Mutable state with deep cloning on save → Rejected: error-prone, hard to debug
- Redux/MobX → Rejected: overkill for prototype, adds complexity

**Consequences:**
- More verbose code (spread operators everywhere)
- Easier debugging (state is always a snapshot)
- Save/load guaranteed to preserve exact state
- Memory usage slightly higher (creating new objects)

---

### ADR-002: Personality Redistribution Algorithm (2026-01-22)

**Context:**
- 6 personality traits must always sum to 100%
- Traits must stay within 5-35% range
- Dialogue choices adjust specific traits

**Decision:**
- When traits are adjusted, redistribute difference proportionally among unadjusted traits
- Apply bounds (5-35%) after redistribution
- Final normalization ensures sum = 100% (with floating-point tolerance)

**Alternatives Considered:**
- Fixed ratio adjustments → Rejected: less flexible, harder to balance
- Unconstrained traits → Rejected: players could min-max to extremes

**Consequences:**
- Natural feeling trait evolution
- No "dump stats" possible (minimum 5%)
- No god-mode traits (maximum 35%)
- Requires careful algorithm implementation

---

### ADR-003: Fixed NPC Archetypes (2026-01-22)

**Context:**
- NPCs need consistent, predictable behavior
- Player expects NPC personalities to remain recognizable
- Reduces complexity of dialogue system

**Decision:**
- NPC `basePersonality` never changes
- Only player personality changes through dialogue choices
- NPCs have `affection` and `trust` values that change (relationship, not personality)

**Alternatives Considered:**
- NPCs also evolve personality → Rejected: too complex for prototype, confusing for players
- No relationship tracking → Rejected: removes narrative depth

**Consequences:**
- Elena always feels like Elena
- Dialogue variation comes from player choices, not NPC changes
- Simpler to write dialogue trees
- Relationship values provide narrative consequence without personality drift

---

### ADR-004: No Dead Ends in Dialogue (2026-01-22)

**Context:**
- Personality gates could lock players out of content
- Players should feel agency, not punishment for personality choices

**Decision:**
- Every dialogue node has at least one ungated option
- Personality gates affect flavor/narrative, not quest availability
- All dialogue paths lead to valid outcomes

**Alternatives Considered:**
- Hard gates that lock content → Rejected: frustrating for players, reduces replayability
- No gates at all → Rejected: removes personality system's impact on narrative

**Consequences:**
- Players always have options
- High-cunning and low-cunning players get different dialogue, same quests
- More dialogue writing required (multiple paths to same outcome)
- Personality feels meaningful without being punishing

---

### ADR-005: JSON File-Based Persistence (2026-01-22)

**Context:**
- Sprint 1 needs working save/load
- No database infrastructure yet
- Vercel deployment is stateless

**Decision:**
- Save to JSON files in `saves/` directory
- 10 save slots (slot_1.json through slot_10.json)
- Full game state serialized to JSON

**Alternatives Considered:**
- SQLite → Rejected: adds dependency, harder Vercel deployment
- Browser localStorage → Rejected: backend-focused, need server-side saves
- No persistence → Rejected: prototype needs save/load for testing

**Consequences:**
- Simple implementation
- Easy to inspect save files during development
- Will need migration path to database in future sprints
- File I/O on Vercel may have limitations (future concern)

---

### ADR-006: Express.js REST API (2026-01-22)

**Context:**
- Need API for testing during development
- Future React frontend will consume this API
- Simple, well-documented approach preferred

**Decision:**
- Express.js with REST endpoints
- JSON request/response bodies
- Stateful session (one active game in memory per session)

**Alternatives Considered:**
- GraphQL → Rejected: overkill for prototype, steeper learning curve
- tRPC → Rejected: requires full-stack TypeScript setup
- No API (direct function calls) → Rejected: harder to test, no frontend integration path

**Consequences:**
- Easy to test with Postman/curl
- Clear contract for frontend integration
- Well-understood patterns
- Session state requires careful handling for multi-user (future concern)

---

### ADR-007: Combat Formula Source of Truth (2026-01-23)

**Context:**
- Combat system requires complex interconnected formulas for balance
- Formulas have been developed and tested in Excel spreadsheet
- Need consistent implementation across backend

**Decision:**
- All combat formulas must be ported directly from `GM Combat Tracker.xlsx`
- `GM_Combat_Tracker_Documentation.md` provides formula locations and explanations
- No "improvements" or modifications to formulas during port - exact replication first
- Combat balance depends on tested Excel formulas

**Key Formula Categories:**
- Rank KO threshold: `((Attacker Rank - Target Rank) × 3) / 10`
- Blindside threshold: `(Attacker Speed - Target Speed) / Target Speed`
- Crushing Blow threshold: `(Action Power - Target Power) / Target Power`
- Defense resolution with SR/SMR/FMR rates
- Energy segment gains by action/reaction result

**Consequences:**
- Consistent game balance with tested values
- Clear reference for any formula questions
- May need Excel access for deep debugging
- Future balance changes should update Excel first, then port

---

### ADR-008: Information Asymmetry in Combat (2026-01-23)

**Context:**
- Need tactical depth in 3v3 combat
- AI behavior needs to be predictable enough to read
- Player decisions need to feel meaningful

**Decision:**
- AI commits to actions before player sees battlefield (Phase 1)
- Player then sees AI stances and stamina via visual cues (Phase 2)
- Player responds with full knowledge of enemy intentions (Phase 3)
- This asymmetry makes player decisions meaningful while keeping AI readable

**Consequences:**
- Tactical depth through asymmetric information flow
- Visual cue system (stance indicators, stamina colors) is critical UI
- AI behavior is transparent once you learn to read the cues
- Creates skill ceiling through pattern recognition

---

### ADR-009: Reaction vs Action Path Philosophy (2026-01-23)

**Context:**
- Six elemental paths need distinct identities
- Paths should create build diversity
- Special attacks force specific defenses

**Decision:**
- **Reaction paths** (Fire, Air, Light): Improve your own defensive rates
- **Action paths** (Water, Earth, Shadow): Debuff enemy defensive rates
- Each path's Special attack forces target into specific defense
- Opposing philosophies create meaningful build choices

**Consequences:**
- Clear identity for each path
- Players can predict forced defenses from enemy paths
- Synergy opportunities between party members
- Distinct playstyles (defensive boost vs offensive debuff)

---

### ADR-010: Two-Tier Hierarchical Agent System Design (2026-01-23)

**Context:**
- Complex implementation tasks benefit from specialized agents
- Planning/ideation has different needs than execution
- Want fast iteration during planning, high capability during execution
- Need clear separation between "what to build" and "building it"

**Decision:**
- Implement a two-tier agent system: Planning (Waldo) and Execution (Architect)
- **Waldo** (Haiku): Planning/ideation partner, works with user in Plan mode
- **Architect** (Opus): Execution orchestrator, receives approved plans
- Four specialized sub-agents: Coder, Reviewer, Researcher, Tester (all Sonnet)
- Both tiers can use sub-agents, but for different purposes
- Explicit user approval required between planning and execution phases

**Architecture:**
```
PLAN MODE:                         IMPLEMENTATION MODE:
User <-> Waldo (Haiku)             User <-> Architect (Opus)
              |                                 |
         Sub-agents                        Sub-agents
         (exploration)                     (execution)

         [User Approval]
    Waldo's Plan ───────────> Architect Executes
```

**Why "Waldo":**
- Named after Ralph Waldo Emerson, transcendentalist philosopher
- Emphasizes self-reliance, intuition, and thoughtful exploration
- Reflects the agent's role as a thinking partner, not just a command executor

**Alternatives Considered:**
- Single Architect for both planning and execution -> Rejected: Opus is expensive for iterative ideation
- Automatic handoff from Waldo to Architect -> Rejected: user wants explicit control over transition
- Haiku for sub-agents -> Rejected per user preference: all sub-agents use Sonnet

**Consequences:**
- Fast, cheap iteration during planning (Haiku)
- High-capability orchestration during execution (Opus)
- Clear separation of concerns (plan vs execute)
- User maintains control over when to commit to implementation
- Sub-agents serve both tiers with different intent (exploration vs execution)
