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

---

### ADR-011: Three-Tier Workflow with Design Exploration (2026-02-11)

**Context:**
- Sprint planning created high-level architecture but left design decisions to execution phase
- Two complex subsystems (behavior tree AI, Group action type) have no existing specifications
- Execution agents shouldn't make architectural design decisions autonomously
- Need collaborative design dialogue for complex subsystems, but discovery-style exploration is too broad

**Decision:**
- Adopt three-tier workflow: Planning → Design Exploration (optional, per-task) → Execution
- Planning flags tasks as execute-ready or `[DESIGN REQUIRED]`
- Design exploration subsystem (`/intuition-design`) engages user in collaborative design for flagged tasks
- Design exploration produces `design_spec_[component].md` ready for execution implementation
- Execution agents only receive fully-specified tasks

**Alternatives Considered:**
- Single-tier (Planning → Execution): Execution makes design decisions. Rejected: loses user control over architecture.
- Bring back discovery for complex tasks: Too heavyweight. Discovery is broad problem-understanding; design needs narrow technical focus.
- Two-tier with deeper planning: Planning goes deeper on complex tasks. Rejected: planning session becomes too long; design dialogue has different feel/flow than planning.

**Consequences:**
- Two subsystems (Tasks 16-17) require `/intuition-design` before implementation begins
- Total project duration increases slightly (design exploration overhead)
- User maintains control over architectural decisions even on complex subsystems
- Design specs provide clear contracts for execution agents
- Better separation of concerns: plan scope/sequence, design architecture, execute code

---

### ADR-012: Pipeline Combat Architecture (2026-02-11)

**Context:**
- Combat resolution involves 12 interconnected subsystems (rank KO, blindside, defense, damage, etc.)
- Subsystems have complex interdependencies but are each logically separate
- 5-phase turn structure is sequential: AI Decision → Visual → Declaration → Priority Sort → Per-Attack Resolution
- Need clean boundaries, independent testability, and clear orchestration

**Decision:**
- Combat engine structured as a staged pipeline: each phase is a pure function transforming CombatState
- Round Manager orchestrates phases in sequence
- Each phase/subsystem can be tested independently
- CombatState is the immutable contract between stages
- All functions follow existing spread-operator immutability pattern

**Alternatives Considered:**
- Monolithic combat resolver: Single function handles all logic. Rejected: untestable, hard to follow.
- Subsystem-per-module (12 separate files): Each subsystem is a module. Rejected: risk of cross-imports and circular dependencies.
- Domain-grouped modules (4-5 groups): Subsystems grouped by category. Rejected: less clean separation.

**Consequences:**
- Combat logic is highly modular and testable
- Mirrors the actual combat flow (5 phases), making it easy to understand
- Matches existing codebase patterns (pure function composition like `processDialogueChoice`)
- Phase functions can run independently in tests
- New combat features can be added as new stages or substages

---

### ADR-013: Independent CombatState with Round-Boundary Sync (2026-02-11)

**Context:**
- Combat resolution involves dozens of micro-steps: checks, defense rolls, damage application, counter triggers, energy gains
- Existing GameState immutability pattern creates new full state on every change
- Creating full GameState copies on every combat micro-step would be expensive and noisy
- Need to keep combat pipeline fast while maintaining immutability guarantee with main GameState

**Decision:**
- Combat pipeline owns a focused CombatState (combatants, actions, queue, logs) during resolution
- CombatState is independent and mutable within the combat module (pure functions transform it)
- Only at round boundaries does CombatState merge back into GameState via immutable sync function
- GameState.activeCombat holds the latest round snapshot
- CombatState never nested inside GameState during resolution

**Alternatives Considered:**
- Nested CombatState inside GameState throughout: Every micro-step creates new full GameState. Rejected: expensive, verbose.
- Hybrid with event log: Independent CombatState + event log for replay/animation. Rejected: adds work without blocking Sprint 2.

**Consequences:**
- Combat pipeline stays lean and fast
- No expensive full-GameState copies during resolution
- Clear boundary between combat system and main game state
- Immutability guarantee maintained at the GameState level (end of round)
- Event replay/animation can be added in later sprints if needed

---

### ADR-014: Behavior Tree AI Architecture (2026-02-11)

**Context:**
- NPC combat decisions need to be made in Phase 1 (hidden from player)
- Decisions should consider combat state (own stamina, target stamina, available energy, team composition)
- Different NPC archetypes should have distinct decision patterns
- Simple random/weighted system lacks sophistication for investor demo
- Need extensible system for later sprint enhancements

**Decision:**
- Implement behavior trees for NPC decision-making
- Tree framework includes node types: Selector (priority), Condition, Action, Decorator
- Tree structure loaded per NPC or archetype (Elena = loyal scout tree, Lars = scheming merchant tree, Kade = rogue tree)
- Trees read CombatState via readonly perception adapter (maintains immutability)
- Evaluation produces single DeclaredAction per tree tick (Phase 1)

**Alternatives Considered:**
- Rule-based archetype profiles: Simple, less flexible. Rejected: user chose extensibility.
- Weighted random: Fastest. Rejected: too simple for demo.
- Full AI system with learning: Overkill for prototype.

**Consequences:**
- Behavior trees are familiar pattern to game developers
- Extensible for later sprints (behavior modifications, dynamic tree loading)
- Requires design exploration to specify node types and evaluation algorithm
- NPC behavior is predictable and can be read/debugged via tree inspection
- Supports archetype differentiation (aggressive, defensive, tactical)

---

### ADR-016: Fastify HTTP Framework (2026-02-21)

**Context:**
- Planning phase evaluated framework choices: Express.js (archived patterns), Fastify, Hono
- Archived Express patterns could be ported but framework itself is open for re-evaluation
- Need TypeScript-native, performant, well-documented framework

**Decision:**
- Use Fastify 4.18 as HTTP framework for Sprint 1 and Sprint 2 APIs
- Leverage Fastify's plugin architecture for modular route organization
- Use built-in JSON Schema validation via Ajv
- Session state managed via `fastify.decorate()` (replaces archived `api/game.ts` singleton pattern)

**Alternatives Considered:**
- Express.js: Archived patterns translate directly. Rejected: user chose Fastify for TypeScript-first approach.
- Hono: Lightweight, edge-compatible. Rejected: less mature ecosystem for this use case.

**Consequences:**
- Fastify plugin API differs from Express middleware; route handlers need adaptation
- Built-in schema validation reduces boilerplate vs Express
- Slightly faster performance than Express (not critical for demo, but positive)
- Engineering phase must translate archived Express router patterns to Fastify plugins
- Session state pattern (`fastify.decorate`) differs from archived singleton

---

### ADR-017: Vitest Test Framework (2026-02-21)

**Context:**
- Planning required fast TDD workflow for formula porting (ADR-015)
- Jest listed in package.json but not installed; clean slate for testing framework choice
- Need TypeScript support without additional configuration

**Decision:**
- Use Vitest as test framework for all unit, integration, and E2E tests
- Jest-compatible `describe/it/expect` API for familiarity
- Native TypeScript support without ts-jest or babel-jest
- Watch mode optimized for TDD workflow

**Alternatives Considered:**
- Jest: Requires ts-jest config; slower TypeScript cold start. Rejected: Vitest addresses both issues.
- Node built-in runner: Minimal ecosystem. Rejected: need mature test framework for complex combat system.

**Consequences:**
- Fast watch mode critical for formula porting TDD (ADR-015)
- All existing Jest knowledge transfers (API compatibility)
- Vitest configuration simpler than Jest + ts-jest
- No migration path needed to Jest later (both APIs compatible)

---

### ADR-019: Utility Scoring for Behavior Tree AI (2026-02-21)

**Context:**
- Behavior tree AI (Task 17) requires transparent, testable decision-making for 3 archetypes
- Prior ADR-014 was conceptual; design exploration produced specific technical decisions
- Need an evaluation model that is traceable, deterministic, and data-driven

**Decision:**
- Use utility scoring over classic behavior tree traversal
- 7 multi-output scoring factors (each returns scores for all 5 action types)
- Combined (action, target) scoring: all candidates evaluated together
- Combat perception layer: pre-computed readonly snapshot mediates between CombatState and factors
- Rank-based decision quality coefficient: linear scaling from 0.2 (low rank) to 1.0 (high rank)
- Path-based tie-breaking: elemental path determines action priority for score ties

**Key Design Elements:**
- **7 Scoring Factors:** OwnStamina, AllyInDanger, TargetVulnerability, EnergyAvailability, SpeedAdvantage, RoundPhase, TeamBalance
- **Perception Layer:** Pre-computed CombatPerception includes sorted ally/enemy lists, stamina percentages, rank/speed deltas, team averages
- **Rank Coefficient:** max(0.2, rank/10.0) — low-rank NPCs rely on instinct, high-rank on full tactical awareness
- **Path Tie-Breaking:** Fire→[ATTACK, SPECIAL, DEFEND, EVADE, GROUP]; Water→[DEFEND, EVADE, SPECIAL, ATTACK, GROUP]; etc.
- **Archetype Profiles:** Data-driven (Elena: support-weighted, Lars: efficient/defensive, Kade: aggressive/opportunistic)

**Alternatives Considered:**
- Classic behavior tree (Selector/Sequence nodes): Order-dependent, harder to balance. Rejected for utility scoring's transparency.
- Two-phase (action then target): Less accurate cross-comparison. Rejected for combined scoring's expressiveness.
- Direct CombatState access vs perception layer: Perception layer avoids redundant computation and enforces immutability boundary.

**Consequences:**
- Every NPC decision is traceable to a score breakdown (debuggable, testable)
- 7 factors cover full combat decision space with natural modeling
- Multi-output factors reduce factor count vs flat per-action approach
- Deterministic: same state → same decision
- Extensible: new factors can be added by engineering
- Archetype profiles are 100% data-driven (JSON-like structures)

**Related Design Specs:** `design_spec_behavior_tree_ai_system.md`

---

### ADR-018: Linear Build Sequencing (2026-02-21)

**Context:**
- Sprint 1 and Sprint 2 share state management and type foundation
- Sprint 2 depends on working Sprint 1 as a tested base
- Risk: attempting parallel build when Sprint 1 unstable causes Sprint 2 churn

**Decision:**
- Build Sprint 1 completely (Tasks 1-9) before beginning Sprint 2 (Tasks 10-22)
- Sprint 1 integration validation (Task 9) is a hard gate before Sprint 2 starts
- Allows Sprint 1 test coverage and API validation before combat engine complexity

**Alternatives Considered:**
- Types-first parallel tracks: Define all types upfront, build both concurrently. Rejected: risk of type churn mid-build.
- Interleaved by system: Alternate tasks across sprints based on dependencies. Rejected: too complex to track for execution agents.

**Consequences:**
- Total build duration slightly longer than parallel (sequential dependency chain)
- Sprint 1 fully tested before Sprint 2 begins (higher confidence)
- Clear milestone at Task 9 for user to validate progress
- Sprint 2 can begin with confidence in foundation

---

### ADR-015: Test-Driven Formula Porting from Excel (2026-02-11)

**Context:**
- Combat formulas are the source of balance and must be accurate
- GM Combat Tracker Excel has 17 sheets with tested formulas
- ADR-007 requires exact replication (no modifications, no "improvements")
- Risk: formula misinterpretation during porting introduces subtle bugs
- Need guarantee of correctness per ADR-007

**Decision:**
- All formulas ported using test-driven development (TDD)
- For each formula category: extract known input/output pairs from Excel first
- Write failing tests with those pairs
- Implement formula until tests pass
- Test boundary cases (equal stats, thresholds, edge conditions)

**Alternatives Considered:**
- Manual spot-check: Fast but error-prone. Rejected: no regression protection.
- Golden file testing: Comprehensive but requires Excel export. Rejected: TDD sufficient.

**Consequences:**
- 100% confidence in formula accuracy
- Tests serve as documentation of formula intent
- Regression protection: future changes must pass existing tests
- Slightly slower porting (test writing overhead)
- All formula tests committed to codebase
