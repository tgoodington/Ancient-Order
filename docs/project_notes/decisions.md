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

---

### ADR-020: Group Action Type (2026-02-21)

**Context:**
- GROUP is a 5th action type for coordinated team attacks (alongside ATTACK, DEFEND, EVADE, SPECIAL)
- Completely undefined in prior documentation; inspired by Skies of Arcadia crew specials
- Requires design exploration to define mechanics, targeting, synergy, defense interaction
- Design brief flagged GROUP for collaborative design before implementation
- Existing behavior tree AI design (ADR-019) includes GROUP factor scoring but excluded it via config flag pending this design

**Decision:**
- GROUP is a leader-initiated action: one combatant declares GROUP, all non-KO'd allies are conscripted (overriding their individual declarations)
- Full trio attack: all 3 allies coordinate a single strike against one enemy target
- **Priority 0 (highest):** GROUP resolves before all other actions, including DEFEND intercepts — cannot be redirected by DEFEND
- **Energy gate:** All participants must have full energy segments at Phase 3 declaration time; validation is one-time (at declaration)
- **Defense suppression:** Target forced to Block only — no Dodge, no Parry, no counter chains
- **Damage multiplier:** 1.5x flat multiplier on sum of all participants' individual damage: `(damageA + damageB + damageC) × 1.5`
- **Flexible participant count:** GROUP fires with whoever is non-KO'd at resolution. If opposing GROUP KO's allies first, GROUP still executes with 2 or 1 participant; multiplier unchanged
- **Opposing GROUP tie-break:** Higher team average speed (of non-KO'd members) resolves first
- **Designed for extensibility:** POC implements one GROUP variety. Future varieties can define different synergy bonuses, join rules, and targeting models via `GroupActionConfig`

**Key Design Elements:**
- Priority table change: GROUP=0, DEFEND=1, ATTACK/SPECIAL=2, EVADE=3 (vs prior GROUP=2)
- Energy consumption: all participants' energy reset to 0 on GROUP execution (atomic)
- Immutable: all state updates produce new objects (existing ADR-001 pattern)
- Pure functions: GROUP resolver has no side effects, deterministic

**Alternatives Considered:**
- Pair-based or flexible participation: Rejected for POC; full trio chosen for drama and simplicity
- Multiple GROUP varieties in POC: Rejected; one variety with extensibility for future enhancement
- GROUP as bonus to individual attacks (not priority 0): Rejected; priority 0 provides clear tactical identity (counter to DEFEND-heavy strategies)
- Defense alternatives (single roll with reduced rates, three rolls): Rejected; Block-only best balances overwhelm concept with rule simplicity

**Consequences:**
- GROUP is the highest-priority action; nothing else resolves before it (except slower opposing GROUP)
- Full team commitment to one enemy per round when GROUP used
- Energy buildup over several rounds creates rhythm (build → unleash → build → unleash)
- Behavior tree AI can score GROUP once `groupActionsEnabled = true` in config
- Clear contract for implementation: validation at declaration, resolution at priority 0, produces `GroupResolutionResult`

**Related Design Spec:** `design_spec_group_action_type.md`

---

### ADR-021: ECMAScript Modules (2026-02-21)

**Context:**
- Project rebuild starting from empty `src/` directory with clean dependency slate
- Vitest and Fastify both have native ESM support
- TypeScript configuration initially targeted CommonJS
- Build tooling needs clear module strategy from start to avoid mid-project migration

**Decision:**
- Use ECMAScript modules (ESM) throughout: `"type": "module"` in package.json, `"module": "NodeNext"` in tsconfig.json
- All import statements use explicit `.js` extensions in source code (TypeScript preserves these in compiled output)
- Dev runner (`tsx`) supports ESM natively for `npm run dev`
- Vitest configuration requires no special ESM setup (native support)

**Alternatives Considered:**
- CommonJS (existing tsconfig.json): Works but requires ts-node or tsx with CommonJS config. Rejected: ESM is modern standard, better tooling support.
- Dual ESM/CommonJS build: Over-complex for single backend. Rejected.

**Consequences:**
- All imports must include `.js` extension (TypeScript/Node requirement for ESM)
- Faster startup time for dev runner (tsx optimized for ESM)
- Better tree-shaking in future frontend integration
- Build agents must understand `.js` import semantics
- No migration path needed to CommonJS later (ESM is standard)

---

### ADR-022: Session State via Fastify Decorate (2026-02-21)

**Context:**
- Archived Sprint 1 prototype used module-level singleton: `getActiveGameState()` / `setActiveGameState()`
- Fastify plugin architecture differs from Express middleware (archived pattern)
- Need type-safe session state access across all route plugins
- Fastify's `fastify.decorate()` is idiomatic pattern for plugin-shared state

**Decision:**
- Session state (`gameState: GameState | null`) attached to Fastify instance via `fastify.decorate('gameState', null)`
- Type-safe access via Fastify declaration merging in module extending `FastifyInstance`
- All route plugins access via `fastify.gameState`; mutations via `fastify.gameState = newState`
- Mutable reference (state location), immutable object content (state values)

**Alternatives Considered:**
- Module-level singleton (archived pattern): Simpler, but not idiomatic Fastify. Breaks plugin isolation.
- Context-local storage (AsyncLocalStorage): Overkill for single session prototype.
- Global variable: Anti-pattern, would prevent future multi-session support.

**Consequences:**
- Session state is plugin-accessible (all plugins see same instance)
- Type declaration required in each plugin that accesses state
- Better scoping than global variables (state tied to Fastify instance lifecycle)
- Prepares for future multi-session support (each request could have separate instance)
- Differs from archived prototype; build agents must learn pattern

---

### ADR-023: Per-Call-Site Roll Injection (2026-02-21)

**Context:**
- Combat resolution involves multiple probabilistic checks: Rank KO, Blindside, Crushing Blow, defense success/failure
- Need deterministic testing (same state → same result) without seeding global Math.random()
- TDD formula porting requires fixed roll values to validate against Excel

**Decision:**
- Functions accepting random rolls take a `rollFn` parameter: `(state, action, rollFn?: () => number) => newState`
- Default: `rollFn = () => Math.random() * 20` (produces 0-20 roll value)
- Tests pass fixed functions: `() => 15.0` for specific roll values
- Roll values never change during a function call (no repeated randomness)

**Alternatives Considered:**
- Global seed (seedrandom library): Heavy dependency for prototype. Rejected.
- Module-level `currentRoll` variable: Global state, hard to manage. Rejected.
- Random seed in CombatState: Couples state to randomness; harder to reason about.

**Consequences:**
- All functions with randomness have explicit `rollFn` parameter (clear API contract)
- Tests can deterministically control outcomes
- No shared random state across functions
- Slightly more verbose function signatures
- Enables determinism test: run twice with identical rollFn sequences, expect identical results

---

### ADR-024: Shared Damage Calculation Utility (2026-02-21)

**Context:**
- Combat pipeline (Task 15) resolves per-attack damage using base damage formula
- GROUP action (Task 18) needs identical base damage calculation for each participant
- Both need to use same formula (no duplication of game logic)
- Excel formula ADR-007 mandates exact replication; shared utility ensures single source of truth

**Decision:**
- Extract `calculateBaseDamage(attacker, target, ...modifiers): number` into `combat/formulas.ts`
- Both `pipeline.ts` and `groupAction.ts` import and call this utility
- Utility handles attacker power, target power, rank modifiers, buff/debuff modifiers
- No per-participant synergy applied at utility level (synergy is GROUP-specific)

**Alternatives Considered:**
- GROUP imports from pipeline.ts: Tighter coupling, harder to trace. Rejected.
- Inline duplicate: Code duplication increases maintenance risk. Rejected.
- Abstract base class: Over-engineered for single utility. Rejected.

**Consequences:**
- Single source of truth for base damage calculation
- Both subsystems stay decoupled (utility is clear boundary)
- Utility tested once in formulas.test.ts (both users benefit)
- GROUP multiplier applied after utility call (1.5x to aggregated base damages)
- Clear contract: utility returns plain damage number, caller applies action-specific modifiers

---

### ADR-025: Co-Located Tests + ESLint/Prettier (2026-02-21)

**Context:**
- Sprint 2 requires TDD workflow for formula porting (ADR-015) with fast iteration
- Archived prototype has zero tests; clean slate for testing structure
- Code consistency tooling absent; optional to add during build

**Decision:**
- Unit tests co-located with source: `personalitySystem.test.ts` next to `personalitySystem.ts`
- Integration/E2E tests in `tests/` directory at project root (avoid source bloat)
- ESLint flat config (`eslint.config.js`) with `@typescript-eslint` plugin
- Prettier formatter (`.prettierrc`) for consistent style
- Scripts: `"test": "vitest run"`, `"test:watch": "vitest"`, `"lint": "eslint src/"`, `"format": "prettier --write src/"`

**Alternatives Considered:**
- Centralized test directory (all tests in `tests/`): Harder to navigate source files. Rejected.
- No linting: Code quality suffers. Rejected.
- Different linter (Biome): Ecosystem not as mature. Rejected.

**Consequences:**
- Fast feedback loop: edit source, save, test runs (co-location + watch mode)
- Clear separation: unit tests stay with code, integration tests separate
- Consistent code style reduces reviewer friction
- Slightly slower initial build (linting overhead), mitigated by pre-commit hooks in future sprints
- Build agents must understand test file naming convention (`*.test.ts`)

---

### ADR-026: Team Synergy System — Paradigm-Based Party Bonuses (2026-02-24)

**Context:**
- Sprint 3 requires pre-combat stat bonuses based on party member personality interactions
- Three viable calculation models existed: pair-based lookup (static), trait-distance formula (dynamic), full team composition
- User chose trait-distance formula, then clarified the design to paradigm-based model
- Party is always 3 members (Elena, Lars, Kade as fixed NPCs + player) in demo; system must extensible to swappable parties

**Decision:**
- **Paradigm-based model:** Named archetypes describing personality distribution patterns across the whole party
- Only the best-matched satisfied paradigm applies (highest-only rule, no stacking)
- **Two POC paradigms:**
  - **Well Rounded** (power ×1.10): Every personality trait has ≥25% representation across the party. Rewards versatility/coverage.
  - **Bond** (speed ×1.10): Player personality ≥80% aligned with one NPC's dominant traits (top 2 by value). Rewards deep character resonance.
- **Binary thresholds:** Meet it or don't; no partial/graduated bonuses
- **Highest-only comparison:** Each paradigm has a match quality score (0.0–1.0+); highest score wins. Tiebreak: Well Rounded > Bond
- **Direct stat modification:** Bonuses applied by scaling `power` or `speed` at `initCombatState` (no new buff pipeline)
- **Pure function, data-driven:** `calculateSynergy()` is deterministic; paradigm definitions in `ParadigmConfig` (not hardcoded)
- **Bond dominant traits derived dynamically:** NPC's top 2 traits by value; auto-extends to any new NPC (no manual tagging)

**Key Design Elements:**
- **Well Rounded match quality:** Minimum of all trait party-maxes / threshold (0.25). If all traits pass, score ≥1.0 clamped to 1.0.
- **Bond match quality:** Best player-to-NPC alignment ratio: sum(player's NPC top-2 traits) / sum(NPC's own top-2 traits).
- **Thresholds:** Well Rounded 25%, Bond 80% (as ratio: 0.25 and 0.80)
- **Application scope:** All party members (player + 3 NPCs) receive the bonus if their party is player party
- **Calculator lives in `src/narrative/synergyCalculator.ts`** (reads GameState personalities, consumed by `combat/sync.ts`)
- **REST API exposed:** GET /narrative/synergy (Task 9 endpoint calls same calculator)

**Alternatives Considered:**
- Pair-based lookup: Static, transparent, but ignored player personality. Rejected for thematic weakness.
- Pure trait-distance: Too abstract, hard to balance. Rejected.
- Additive stacking: Multiple paradigms both apply. Rejected for cleaner single-identity design.
- Graduated bonuses: Partial progress toward paradigm. Rejected for simplicity/clarity.

**Consequences:**
- Party composition directly affects combat stats; personality choices matter
- Player personality choices influence synergy (Well Rounded via trait gaps, Bond via alignment)
- Well Rounded demo-achievable by supportive player build (patience/empathy/kindness ≥25%)
- Bond demo-achievable by mirroring an NPC's dominant traits
- System scales to any future party composition/NPC count automatically
- Extensible: new paradigms added to config without code changes
- Match quality scores enable future UI/narrative feedback ("92% aligned with Lars")

**Related Design Spec:** `design_spec_team_synergy_system.md`

---

### ADR-027: Party Members Are Neutral Warriors (2026-02-24)

**Context:**
- Original Sprint 1 design labelled Elena as "Loyal Scout (DEUS)" and Kade as "Rogue Outlaw (Rogues)" — implying faction alignment within the party
- Post-demo design intent: party composition changes as the player meets new warriors on their journey
- Locking party members to factions creates narrative constraints that break down when party members swap out
- Moral ambiguity (DEUS vs Rogues) is a world-level story arc, not a party-level tension

**Decision:**
- Party members are warriors first. Their identity is defined by: elemental path, personality distribution, rank, and individual backstory — not faction allegiance
- DEUS/Rogues tension is delivered through world NPCs encountered during the journey, not through party member friction
- Elena and Kade remain consistent characters with fixed personalities, but their faction labels ("Loyal Scout," "Rogue Outlaw") are internal Sprint 1 shorthand — not in-world titles
- This generalizes cleanly: any new warrior the player recruits is just a warrior; faction politics stay in the world layer

**Alternatives Considered:**
- Faction-aligned party members: Creates interesting intra-party tension. Rejected because it breaks down when party composition becomes dynamic in later acts.
- Neutral world, faction party: Faction tension expressed only through party. Rejected: inverts the intended design where world events carry the faction story.

**Consequences:**
- Scene writing can focus on world-level DEUS/Rogues encounters rather than intra-party conflict
- Party synergy design (ADR-026) already supports this — paradigm bonuses are about personality distribution, not faction
- Future party members designed as warriors with personality, not as faction representatives

---

### ADR-028: Act 1 Demo — Mid-Journey Gym Town Slice (2026-02-24)

**Context:**
- Original plan assumed the demo would be the Act 1 opening sequence (Championship Fight → Town → Dontan → Time Skip, etc.)
- Act 1 opening is slow to onboard: young player, training arc, 5-year time skip before the journey begins
- For an investor/publisher demo, the opening pace undersells the full system capability

**Decision:**
- The investor demo uses a purpose-built mid-journey slice, not the Act 1 opening
- Setting: a Gym Town on the tournament circuit — player is already a trained warrior, party is assembled, competition is underway
- Content ingredients: DEUS presence and world-building, personality choice moments, training fight opportunities, a Rogue run-in (present but not central), and a gym fight climax
- 3 scenes: Town Arrival (DEUS NPC interaction, flag-setting) → Escalation (Rogue run-in as consequence) → Gym Fight
- Full Act 1 (Championship Fight, Dontan's Trials, Time Skip, Setting Out, etc.) remains the intended post-demo story arc

**Alternatives Considered:**
- Act 1 opening as demo: Authentic story start but slow. Investor sees tutorial combat with a kid character before any system depth. Rejected.
- Single combat demo: No narrative context. Rejected.

**Consequences:**
- Demo scenes are not canonical Act 1 content — they're a showcase slice at a mid-story moment
- Player is presented as a young adult warrior already on the journey (no origin onboarding needed)
- Scene JSON produced by Task 10 is demo-specific; Act 1 opening scenes are future content
- Scene design can assume player familiarity with warrior culture (no need to explain world from scratch)

---

### ADR-029: Rogues Faction — Sporadic Arc Pattern (2026-02-24)

**Context:**
- Original design emphasized DEUS vs Rogues moral tension as a core theme
- Risk of over-foregrounding the faction conflict in early scenes when it should build gradually
- User's intent: Rogues appear as a low-level background presence in early acts, escalating to a central narrative conflict in later acts

**Decision:**
- Rogues narrative follows a sporadic escalation pattern (analogous to Team Rocket in Pokémon): early encounters are brief, mysterious, or minor; tension builds across acts until the Rogues' plot becomes the central conflict
- In Act 1 / early demo scenes: Rogues are *present* (a run-in, a hint of their activity) but NOT the main event
- The faction moral ambiguity (DEUS vs Rogues, neither clearly right) is seeded through world-building encounters, not resolved or even heavily pressed in early acts
- Player's relationship to the factions develops through accumulated encounters over the whole story

**Consequences:**
- Early scene writing should treat Rogue appearances as atmosphere and hint — not confrontation or faction-choice moments
- Players are not pressured to "pick a side" in the demo
- Faction tension has narrative room to grow across acts 2-9
- DEUS presence in early towns feels like normal world infrastructure, not an antagonistic force (yet)
