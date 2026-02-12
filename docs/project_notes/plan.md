# Sprint 1 Closeout + Sprint 2 Combat Engine — Plan

**Plan-Execute Contract v1.0 | Comprehensive Tier**
**Date:** 2026-02-11
**Status:** Draft — Pending Approval

---

## 1. Objective

Close remaining gaps in Sprint 1 (unit test coverage) and implement the complete combat engine for Sprint 2, including formula porting from the GM Combat Tracker Excel, pipeline-based combat resolution, and behavior tree AI. Success criteria: all Sprint 1 pure functions have passing unit tests; combat engine resolves a full 3v3 encounter through all 5 phases with correct formula output matching Excel source of truth.

---

## 2. Context Summary

- **Problem**: Sprint 1 backend is ~94% complete but has zero test coverage. Sprint 2 combat engine is the largest and most complex system in the project, requiring 12 interconnected subsystems ported from a tested Excel model.
- **Goals**: Investor/publisher pitch demo with working combat. Solid test foundation for ongoing development.
- **Target users**: Investors viewing demo, future frontend developers consuming API.
- **Constraints**: Immutable state (ADR-001), formulas must exactly replicate Excel (ADR-007), personality traits 5-35% range / 100% sum, no dead ends in dialogue.
- **Key findings**: Existing code follows clean layered architecture with pure functions. Combat system has well-documented formulas but AI decision system is unspecified.

---

## 3. Technology Decisions

| Decision | Choice | Status | Rationale |
|----------|--------|--------|-----------|
| Testing scope | Unit tests only (personality, dialogue, state) | Locked | Pure functions, ~80% logic coverage, minimal setup |
| Combat architecture | Pipeline — staged pure functions transforming CombatState | Locked | Mirrors 5-phase combat flow, independently testable stages |
| Combat state model | Independent CombatState, sync at round boundaries | Locked | Avoids expensive full-GameState copies on micro-steps |
| AI decision system | Behavior tree with priority nodes, conditions, fallbacks | Locked | Extensible for later sprints, flexible NPC differentiation |
| Formula validation | Test-driven porting — tests first from Excel I/O pairs | Locked | Guarantees correctness per ADR-007, regression protection |

---

## 4. Component Architecture

```
Existing (Sprint 1):
  src/types/index.ts          ← Extended with combat types
  src/state/gameState.ts      ← Extended with combat state sync
  src/state/stateUpdaters.ts  ← Extended with combat state transitions
  src/api/                    ← New combat router added

New (Sprint 2):
  src/combat/
  ├── types.ts                 # CombatState, Combatant, Action, Resolution types
  ├── roundManager.ts          # 5-phase pipeline orchestrator
  ├── phases/
  │   ├── aiDecision.ts        # Phase 1: Behavior tree evaluation
  │   ├── declaration.ts       # Phase 3: Player action validation
  │   └── resolution.ts        # Phase 4-5: Priority sort + per-attack resolution
  ├── checks/
  │   ├── rankKO.ts            # Rank dominance instant KO
  │   ├── blindside.ts         # Speed dominance forced Defenseless
  │   └── crushingBlow.ts      # Power dominance Block debuff
  ├── defense/
  │   ├── defenseResolver.ts   # Block/Dodge/Parry with SR/SMR/FMR
  │   └── counterChain.ts      # Parry success cascading
  ├── damage/
  │   ├── damageCalc.ts        # Multi-stage damage calculation
  │   └── stamina.ts           # Health pool, KO detection, Evade recovery
  ├── progression/
  │   ├── paths.ts             # 6 elemental paths, buff/debuff application
  │   └── energy.ts            # Energy segments, ascension levels 0-3
  └── ai/
      ├── behaviorTree.ts      # Tree framework: node types, evaluator
      └── archetypes.ts        # NPC behavior profiles

  src/combat/__tests__/        # Test-driven porting tests
```

**Dependency direction:** types ← checks/defense/damage/progression ← phases ← roundManager ← api
All combat modules are pure functions. No circular dependencies.

---

## 5. Interface Contracts

### Combat State (new)
```typescript
interface CombatState {
  id: string;
  round: number;
  phase: CombatPhase; // 'ai_decision' | 'visual_info' | 'pc_declaration' | 'action_resolution' | 'per_attack_resolution'
  combatants: Combatant[];
  actionQueue: DeclaredAction[];
  resolutionLog: AttackResolution[];
  activeBuffs: Buff[];
  status: 'active' | 'completed';
}

interface Combatant {
  id: string;
  name: string;
  team: 'player' | 'enemy';
  stats: CombatStats;        // power, speed, rank (echelon/degree/decimal)
  stamina: StaminaState;     // base, current, max, color
  defenseSkills: DefenseSkills; // block/dodge/parry with rank, xp, sr, smr, fmr
  path: ElementalPath;
  ascension: AscensionState; // level, segments, accumulationBonus
  buffs: Buff[];
  isKO: boolean;
}

interface DeclaredAction {
  actorId: string;
  actionType: 'attack' | 'defend' | 'evade' | 'special' | 'group';
  targetId?: string;
  segmentsUsed?: number;     // Special only, 1-5
  priority: number;          // Defend=1, Group=2, Attack/Special=3, Evade=4
  orderSpeed: number;        // For tie-breaking within same priority
}

interface AttackResolution {
  attackerId: string;
  defenderId: string;
  rankKO: { triggered: boolean; roll: number; threshold: number };
  blindside: { triggered: boolean; roll: number; threshold: number };
  defenseType: 'block' | 'dodge' | 'parry' | 'defenseless';
  defenseForced: boolean;    // True if path special forced defense type
  defenseSuccess: boolean;
  crushingBlow?: { triggered: boolean; roll: number; threshold: number };
  damage: { base: number; mitigated: number; final: number };
  counterTriggered: boolean;
  energyGains: { attacker: number; defender: number };
}
```

### Pipeline Interface (Round Manager ↔ Phases)
```typescript
// Each phase is a pure function: CombatState in → CombatState out
type PhaseFunction = (state: CombatState) => CombatState;

function executeRound(state: CombatState): CombatState;
function resolveAttack(attacker: Combatant, defender: Combatant, action: DeclaredAction, state: CombatState): AttackResolution;
```

### Combat API Endpoints
```
POST /api/combat/start        # Initialize combat from GameState party + enemies
GET  /api/combat/state         # Current CombatState (filtered by phase for info asymmetry)
POST /api/combat/declare       # Player declares actions (Phase 3)
POST /api/combat/resolve       # Advance to next phase/round
GET  /api/combat/history       # Resolution log for current combat
```

### GameState Sync
```typescript
// After each round, merge combat results back
function syncCombatToGameState(gameState: GameState, combatState: CombatState): GameState;
```

---

## 6. Task Sequence

### Sprint 1 Closeout

#### Task 1: Set Up Test Infrastructure
- **Component**: Build system / test config
- **Description**: Install Jest, ts-jest, and @types/jest. Create jest.config.ts with TypeScript support. Add test script to package.json. Verify a trivial test passes.
- **Acceptance Criteria**:
  1. `npm test` runs Jest with TypeScript compilation
  2. A placeholder test in `src/__tests__/setup.test.ts` passes
- **Dependencies**: None
- **Files**: `package.json`, `jest.config.ts` (new)

#### Task 2: Unit Tests — Personality System
- **Component**: `src/personality/personalitySystem.ts`
- **Description**: Write unit tests covering trait validation, adjustment with redistribution, bounds enforcement, normalization, and category grouping.
- **Acceptance Criteria**:
  1. Tests verify 5-35% bounds are enforced on every trait
  2. Tests verify sum always equals 100% after adjustment (within tolerance)
  3. Tests cover edge cases: all traits at bounds, single trait adjustment, multi-trait adjustment
  4. All tests pass
- **Dependencies**: Task 1
- **Files**: `src/personality/__tests__/personalitySystem.test.ts` (new)

#### Task 3: Unit Tests — Dialogue Engine
- **Component**: `src/dialogue/dialogueEngine.ts`
- **Description**: Write unit tests covering personality gate evaluation (gte, lte, eq operators), option availability filtering, dead-end validation, and dialogue choice processing.
- **Acceptance Criteria**:
  1. Tests verify gate operators evaluate correctly against personality values
  2. Tests verify at least one ungated option exists per node (no dead ends)
  3. Tests verify unavailable options are filtered when gates aren't met
  4. All tests pass
- **Dependencies**: Task 1
- **Files**: `src/dialogue/__tests__/dialogueEngine.test.ts` (new)

#### Task 4: Unit Tests — State Updaters
- **Component**: `src/state/stateUpdaters.ts`
- **Description**: Write unit tests verifying immutability of all state update functions and correctness of personality adjustments, NPC relationship updates, team composition, and conversation logging.
- **Acceptance Criteria**:
  1. Tests verify original state object is never mutated (reference inequality)
  2. Tests verify processDialogueChoice chains all updates correctly
  3. Tests verify team composition validates joinableInTeam constraint
  4. All tests pass
- **Dependencies**: Task 1
- **Files**: `src/state/__tests__/stateUpdaters.test.ts` (new)

#### Task 5: Verify and Fix DELETE Endpoint
- **Component**: `src/api/game.ts`
- **Description**: Audit the DELETE /api/game/saves/:slot endpoint. If incomplete, implement it following existing patterns (slot validation, file deletion, structured response).
- **Acceptance Criteria**:
  1. DELETE /api/game/saves/:slot returns success response when save exists
  2. DELETE /api/game/saves/:slot returns 404 when save doesn't exist
  3. Validates slot range (1-10)
- **Dependencies**: None
- **Files**: `src/api/game.ts`

---

### Sprint 2 Combat Engine

#### Task 6: Combat Type Definitions
- **Component**: `src/combat/types.ts`
- **Description**: Define all TypeScript interfaces for the combat system: CombatState, Combatant, CombatStats, StaminaState, DefenseSkills, DeclaredAction, AttackResolution, ElementalPath, AscensionState, Buff. Follow existing type patterns in `src/types/index.ts`.
- **Acceptance Criteria**:
  1. All interfaces from Section 5 (Interface Contracts) are defined
  2. Types compile without errors
  3. Types follow existing conventions (naming, JSDoc, selective exports)
- **Dependencies**: None
- **Files**: `src/combat/types.ts` (new)

#### Task 7: Dominance Check Functions (Rank KO, Blindside, Crushing Blow)
- **Component**: `src/combat/checks/`
- **Description**: Implement the three dominance check functions as pure functions. Test-driven: write tests FIRST with known input/output pairs from GM Combat Tracker Excel, then implement until tests pass. Formulas per ADR-007 — exact replication, no modifications.
- **Acceptance Criteria**:
  1. `checkRankKO(attacker, defender, roll)` returns correct threshold and triggered status matching Excel formula: `((attackerRank - defenderRank) * 3) / 10`
  2. `checkBlindside(attacker, defender, roll)` returns correct threshold matching: `(attackerSpeed - defenderSpeed) / defenderSpeed`
  3. `checkCrushingBlow(actionPower, targetPower, roll)` returns correct threshold matching: `(actionPower - targetPower) / targetPower`
  4. All functions are pure (no side effects, no state mutation)
  5. Tests include boundary cases (equal stats, barely above/below threshold)
- **Dependencies**: Task 6
- **Files**: `src/combat/checks/rankKO.ts`, `blindside.ts`, `crushingBlow.ts`, `src/combat/__tests__/checks.test.ts` (all new)

#### Task 8: Defense Resolution System
- **Component**: `src/combat/defense/`
- **Description**: Implement Block, Dodge, and Parry resolution with SR/SMR/FMR rates. Test-driven from Excel. Each defense type has success/failure paths with different mitigation rates. Handle Defenseless state (from Blindside). Handle forced defense (from path Specials).
- **Acceptance Criteria**:
  1. `resolveDefense(defender, defenseType, roll)` correctly applies SR for success check
  2. Successful Block applies SMR mitigation to damage
  3. Failed Block/Dodge/Parry applies FMR mitigation
  4. Successful Parry returns counterTriggered = true
  5. Defenseless state bypasses all defense (full damage)
  6. Forced defense type overrides chosen defense
  7. Tests verify against Excel source values
- **Dependencies**: Task 6
- **Files**: `src/combat/defense/defenseResolver.ts`, `src/combat/__tests__/defense.test.ts` (new)

#### Task 9: Damage Calculation and Stamina Tracking
- **Component**: `src/combat/damage/`
- **Description**: Implement multi-stage damage calculation (base → special boost → defense mitigation → final) and stamina management (max calculation, damage application, Evade recovery, KO detection, color coding).
- **Acceptance Criteria**:
  1. Base damage equals action power (Attack) or special power (Special)
  2. Special boost applies +10% per segment used
  3. Defense mitigation correctly applies SMR or FMR based on defense result
  4. Stamina max = base stat * 5
  5. Evade restores 30% of max stamina
  6. KO triggers when current stamina reaches 0
  7. Stamina color correctly maps to thresholds (Green/Yellow/Orange/Red/Black)
- **Dependencies**: Task 6, Task 8
- **Files**: `src/combat/damage/damageCalc.ts`, `stamina.ts`, `src/combat/__tests__/damage.test.ts` (new)

#### Task 10: Counter Chain System
- **Component**: `src/combat/defense/counterChain.ts`
- **Description**: Implement Parry success counter-attack cascading. When Parry succeeds, insert counter-attack into resolution queue using counter-attacker's Power stat. Original attacker can Parry the counter (chain continues). Chain ends on Parry failure, KO, or stamina depletion.
- **Acceptance Criteria**:
  1. Successful Parry inserts counter-attack into resolution queue
  2. Counter uses counter-attacker's Power stat for damage
  3. Chains can continue indefinitely while both combatants have Parry success
  4. Chain terminates on Parry failure
  5. Chain terminates if a combatant is KO'd
  6. Function is pure — returns updated resolution queue, no side effects
- **Dependencies**: Task 8, Task 9
- **Files**: `src/combat/defense/counterChain.ts`, `src/combat/__tests__/counterChain.test.ts` (new)

#### Task 11: Path/Elemental System
- **Component**: `src/combat/progression/paths.ts`
- **Description**: Implement the 6 elemental paths with their buff/debuff mechanics. Reaction paths (Fire, Air, Light) boost own defensive rates. Action paths (Water, Earth, Shadow) debuff target defensive rates. Special attacks force specific defense types.
- **Acceptance Criteria**:
  1. Each path correctly identifies as Action or Reaction type
  2. Reaction paths apply correct self-buff to the matching defense skill SR
  3. Action paths apply correct debuff to target's matching defense skill SR
  4. Special attacks force the correct defense type per path (e.g., Fire forces Parry)
  5. Buff/debuff values match Excel source
  6. All 6 paths have distinct behavior
- **Dependencies**: Task 6
- **Files**: `src/combat/progression/paths.ts`, `src/combat/__tests__/paths.test.ts` (new)

#### Task 12: Energy and Ascension System
- **Component**: `src/combat/progression/energy.ts`
- **Description**: Implement energy segment accumulation and ascension level tracking. Base gains: Action success 1.0, Action failure 0.5, Reaction success 0.5, Reaction failure 0.25. Ascension levels 0-3 with cumulative segment thresholds (0, 35, 95, 180) and accumulation bonuses (+0%, +25%, +25%, +50%). Special attacks cost 1-5 segments.
- **Acceptance Criteria**:
  1. Energy gains correctly calculated per action/reaction result type
  2. Ascension bonus correctly applied to accumulation (+25% at L1-2, +50% at L3)
  3. Ascension level transitions at correct thresholds
  4. Special attack segment cost deducted correctly
  5. Starting segments per ascension level correct (0, 0, 1, 2)
  6. Values match Excel source
- **Dependencies**: Task 6
- **Files**: `src/combat/progression/energy.ts`, `src/combat/__tests__/energy.test.ts` (new)

#### Task 13: Action Priority and Resolution Pipeline
- **Component**: `src/combat/phases/resolution.ts`
- **Description**: Implement Phase 4-5 of combat: sort declared actions by priority (Defend=1, Group=2, Attack/Special=3, Evade=4), break ties by speed + random, then execute each action through the per-attack resolution chain (Rank KO → Blindside → Defense → Damage → Counter → Energy).
- **Acceptance Criteria**:
  1. Actions sorted correctly by priority tier
  2. Tie-breaking uses speed stat with random component
  3. Per-attack resolution chains all checks in correct order
  4. Rank KO success skips all subsequent checks for that attack
  5. Blindside success forces Defenseless before defense resolution
  6. Resolution produces complete AttackResolution record
  7. Energy gains awarded after each attack resolves
- **Dependencies**: Task 7, Task 8, Task 9, Task 10, Task 11, Task 12
- **Files**: `src/combat/phases/resolution.ts`, `src/combat/__tests__/resolution.test.ts` (new)

#### Task 14: Player Declaration Phase
- **Component**: `src/combat/phases/declaration.ts`
- **Description**: Implement Phase 3: validate player-declared actions. Check that action types are valid, targets exist and aren't KO'd, Special attacks have sufficient energy segments, Defend targets an ally, team composition allows declared actions.
- **Acceptance Criteria**:
  1. Validates action type is one of the 5 allowed types
  2. Validates target exists and is not KO'd
  3. Validates Special segment cost doesn't exceed available segments
  4. Validates Defend action targets an ally
  5. Returns structured validation errors for invalid declarations
  6. Produces DeclaredAction[] ready for resolution pipeline
- **Dependencies**: Task 6
- **Files**: `src/combat/phases/declaration.ts`, `src/combat/__tests__/declaration.test.ts` (new)

#### Task 15: Round Manager (Pipeline Orchestrator)
- **Component**: `src/combat/roundManager.ts`
- **Description**: Implement the top-level combat pipeline that orchestrates a complete round through all 5 phases. Manages phase transitions, calls phase functions in sequence, handles round completion, checks for combat end conditions (all enemies KO'd, all players KO'd).
- **Acceptance Criteria**:
  1. Executes phases in correct order: AI Decision → Visual Info → PC Declaration → Action Resolution → Per-Attack Resolution
  2. Phase transitions update CombatState.phase correctly
  3. Round number increments after each complete round
  4. Combat ends when all combatants on one team are KO'd
  5. Returns updated CombatState after each phase
  6. Function is pure — no side effects
- **Dependencies**: Task 13, Task 14, Task 16
- **Files**: `src/combat/roundManager.ts`, `src/combat/__tests__/roundManager.test.ts` (new)

#### Task 16: Behavior Tree AI System [DESIGN REQUIRED]
- **Component**: `src/combat/ai/`
- **Description**: Design and implement the behavior tree framework for NPC combat decision-making (Phase 1). Requires design exploration to define: node types, evaluation model, state access pattern, NPC archetype profiles, and integration with combat pipeline.
- **Acceptance Criteria**:
  1. Behavior tree evaluates and returns valid DeclaredAction for each AI combatant
  2. Different NPC archetypes produce distinct action patterns
  3. AI considers combat state (own stamina, target stamina, available energy)
  4. Every tree has a fallback action (no dead ends)
  5. Tree evaluation is a pure function
- **Dependencies**: Task 6, Task 14
- **Design**: Run `/intuition-design` before execution
- **Files**: `src/combat/ai/behaviorTree.ts`, `archetypes.ts` (new, structure TBD by design)

#### Task 17: Group Action Type [DESIGN REQUIRED]
- **Component**: `src/combat/phases/`
- **Description**: Design and implement the Group coordinated team action. Documentation notes it as "implementation undefined." Requires design exploration to define: mechanics, targeting rules, synergy effects, resolution within the priority system (priority 2).
- **Acceptance Criteria**:
  1. Group action resolves at priority 2 (after Defend, before Attack/Special)
  2. Mechanics are defined and documented
  3. Group action interacts correctly with defense resolution
  4. Team synergy bonuses apply where relevant
  5. Pure function implementation
- **Dependencies**: Task 6, Task 13
- **Design**: Run `/intuition-design` before execution
- **Files**: TBD — combat phases area

#### Task 18: Combat API Endpoints
- **Component**: `src/api/combat.ts`
- **Description**: Create Express router for combat endpoints following existing patterns (structured ApiResponse, error codes, validation). Implement start, state, declare, resolve, and history endpoints. Wire into main app factory.
- **Acceptance Criteria**:
  1. POST /api/combat/start initializes CombatState from GameState party
  2. GET /api/combat/state returns phase-appropriate view (info asymmetry in Phase 2)
  3. POST /api/combat/declare validates and accepts player action declarations
  4. POST /api/combat/resolve advances combat to next phase/round
  5. GET /api/combat/history returns resolution log
  6. All endpoints follow existing ApiResponse<T> pattern
- **Dependencies**: Task 15
- **Files**: `src/api/combat.ts` (new), `src/api/index.ts` (modified to mount router)

#### Task 19: GameState Combat Integration
- **Component**: `src/state/`
- **Description**: Implement sync between independent CombatState and GameState. Add functions to initialize CombatState from GameState (extracting party combatants), sync results back after each round (stamina, energy, buffs), and finalize combat (update combatHistory, handle KO consequences).
- **Acceptance Criteria**:
  1. `initCombatFromGameState(gameState, enemies)` creates valid CombatState
  2. `syncCombatToGameState(gameState, combatState)` merges round results immutably
  3. Combat completion updates GameState.combatHistory
  4. All sync functions maintain immutability (new objects, no mutation)
  5. Personality-based combat modifiers are extracted from GameState correctly
- **Dependencies**: Task 6, Task 15
- **Files**: `src/state/stateUpdaters.ts` (modified), `src/state/combatSync.ts` (new)

---

## 7. Testing Strategy

### Test Framework
- Jest with ts-jest for TypeScript compilation
- No supertest (API tests deferred per decision)
- No mocking needed for pure function tests

### Test Coverage by Task

| Task | Test Type | Critical Scenarios |
|------|-----------|-------------------|
| 2 | Unit | Bounds enforcement, redistribution, normalization, edge cases |
| 3 | Unit | Gate operators (gte/lte/eq), dead-end validation, option filtering |
| 4 | Unit | Immutability verification, update chaining, team validation |
| 7 | Unit (TDD) | Threshold calculations at exact boundaries, equal stats, roll comparisons |
| 8 | Unit (TDD) | All 4 defense states, SR/SMR/FMR application, forced defense |
| 9 | Unit (TDD) | Damage pipeline stages, stamina boundaries, KO trigger, Evade recovery |
| 10 | Unit | Chain continuation, chain termination conditions, queue ordering |
| 11 | Unit (TDD) | All 6 paths buff/debuff, forced defense types |
| 12 | Unit (TDD) | Accumulation with bonuses, level transitions, segment costs |
| 13 | Unit | Priority sorting, tie-breaking, resolution chain order |
| 14 | Unit | Validation rules, error messages, edge cases (KO targets, insufficient segments) |
| 15 | Integration | Full round execution, phase ordering, combat end conditions |

### Test-Driven Porting Protocol (Tasks 7-9, 11-12)
1. Extract known input/output pairs from GM Combat Tracker Excel
2. Write failing tests with those values
3. Implement until tests pass
4. Verify boundary cases

---

## 8. Risks & Mitigations

| Risk | Likelihood | Impact | Mitigation |
|------|-----------|--------|------------|
| Excel formula misinterpretation | Med | High | Test-driven porting with exact I/O pairs from spreadsheet. Cross-reference GM_Combat_Tracker_Documentation.md for formula explanations. |
| Behavior tree scope creep | Med | Med | Design exploration before execution. Lock scope to Sprint 2 needs only — defer sophistication to later sprints. |
| Counter chain infinite loop | Low | High | Implement chain depth limit (configurable, default 10). Stamina depletion is natural terminator. |
| Group action design delays | Med | Low | Group is lowest priority. Can be stubbed (placeholder returning "not implemented") without blocking other tasks. |
| Combat state complexity | Med | Med | Independent CombatState keeps pipeline focused. Sync only at round boundaries. Type system enforces structure. |
| Float precision in formulas | Low | Med | Use same tolerance pattern as personality system (0.01). Round consistently. Test at boundaries. |

---

## 9. Open Questions

| Question | Why It Matters | Recommended Default |
|----------|---------------|-------------------|
| Group action mechanics | Not defined in any documentation. Affects combat balance and team strategy. | Stub as "not implemented" for Sprint 2. Design in Sprint 3 when team synergy is the focus. |
| XP gain formula details | Excel has XP calculations but unclear if Sprint 2 or later. | Include in energy/progression task if clearly documented in Excel. Defer if ambiguous. |
| Visual Information Phase output format | Phase 2 shows stances/stamina to player. Format depends on future frontend. | Return structured JSON with stance indicators and stamina color codes. Frontend decides rendering. |
| Combat encounter configuration | How are enemy parties defined? Static or dynamic? | Static JSON configuration for Sprint 2 (like NPC templates). Dynamic encounter system in later sprint. |

---

## 10. Execution Notes

### Recommended Execution Order

**Wave 1 (parallel, no dependencies):**
- Task 1: Test infrastructure
- Task 5: DELETE endpoint fix
- Task 6: Combat type definitions

**Wave 2 (parallel, depends on Wave 1):**
- Tasks 2, 3, 4: Sprint 1 unit tests (parallel, all depend on Task 1)
- Task 7: Dominance checks (depends on Task 6)
- Task 8: Defense resolution (depends on Task 6)
- Task 11: Path/elemental system (depends on Task 6)
- Task 12: Energy/ascension (depends on Task 6)
- Task 14: Player declaration (depends on Task 6)

**Wave 3 (depends on Wave 2):**
- Task 9: Damage calc + stamina (depends on Tasks 6, 8)
- Task 10: Counter chains (depends on Tasks 8, 9)

**Wave 4 (depends on Wave 3):**
- Task 13: Resolution pipeline (depends on Tasks 7-12)

**Design exploration (can run in parallel with Waves 1-3):**
- Task 16: Behavior tree AI — run `/intuition-design` early
- Task 17: Group action type — run `/intuition-design` (lower priority, can stub)

**Wave 5 (depends on Wave 4 + design):**
- Task 15: Round manager (depends on Tasks 13, 14, 16)
- Task 18: Combat API (depends on Task 15)
- Task 19: GameState integration (depends on Tasks 6, 15)

### Parallelization Opportunities
- Sprint 1 tests (Tasks 2-4) are fully independent of each other and of Sprint 2 work
- Combat subsystem tasks (7, 8, 11, 12, 14) can all run in parallel after types are defined
- Design exploration for Tasks 16-17 can start immediately and run alongside implementation

### Watch Points
- **Formula accuracy**: Every ported formula must be verified against Excel. Don't optimize or "improve" formulas during porting (ADR-007).
- **Immutability discipline**: Combat functions must follow the same spread-operator pattern as existing code. No mutations.
- **Counter chain termination**: Must have a safety valve. Infinite recursion is a real risk if Parry rates are high.
- **Phase 2 info asymmetry**: GET /api/combat/state must filter response based on current phase — player shouldn't see AI decisions before Phase 2.

### Fallback Strategies
- **Behavior tree too complex**: Fall back to rule-based archetype profiles (simpler, still functional for demo). Design exploration will surface this early.
- **Group action undefined**: Stub with "not implemented" response. Does not block any other combat functionality.
- **Excel formula ambiguity**: Cross-reference documentation first, then check multiple Excel sheets for consistency. If truly ambiguous, document the interpretation and flag for review.
