# Bug Log

Repository of bugs encountered, root causes identified, and solutions applied. Use this log to identify patterns and prevent recurring issues.

## Format

Each bug entry includes:
- Date discovered (YYYY-MM-DD)
- Brief description of the issue
- Root cause analysis
- Solution applied
- Prevention notes for future

---

## Active Issues

### 2026-05-30 — Elemental path buffs/debuffs are accumulated but never applied during defense resolution
- **Description:** `applyPathBuff`/`applyPathDebuff` append `Buff` entries to a combatant's `activeBuffs`, and `applyDynamicModifiers` (formulas.ts) exists to fold buffs/debuffs into stats — but the per-attack pipeline resolves defense against `target.reactionSkills` *directly* and never calls `applyDynamicModifiers`. As a result, every elemental-path buff/debuff is stored but mechanically inert: a Water attacker's Dodge debuff, a Light defender's Block buff, etc. have no effect on actual rolls or damage.
- **Impact:** Elemental paths currently have no combat-mechanical effect beyond the SPECIAL forced-defense type. This undercuts the path-identity design (ADR-009). Not yet fixed — it is a balance-affecting change that warrants its own pass (decide whether resolution reads base rates + folded buffs, and re-baseline any tests whose outcomes shift).
- **Note:** Crushing Blow (fixed 2026-05-30) is deliberately applied *directly* to `reactionSkills.block` (not via `activeBuffs`) precisely so it is effective today and will not be double-applied when the buff-folding gap is addressed.

## Resolved Issues

### 2026-05-30 — Per-attack results discarded; round history was reverse-engineered and inaccurate
- **Description:** `resolvePerAttack` built a full `AttackResult` (damage, defenseType, rankKO, blindside, crushingBlow, counterChain) then dropped it through a no-op `_appendActionResult`, returning only `CombatState`. `roundManager` reconstructed results by diffing stamina, hardcoding `defenseType: 'block'` and `blindside/crushingBlow/counterChain: false`. So `roundHistory` (what a frontend uses to render combat) was wrong: dodges/parries showed as block, and blindsides/crushing blows/counters never appeared even when they happened. `groupAction` had the same discard pattern.
- **Root cause:** The pipeline's result-collection hook was stubbed and never completed; the round manager compensated with a lossy heuristic instead of the real data.
- **Solution:** Introduced `resolveAction(state, action, rollFn) => { state, result }` as the single source of truth (and `resolveGroupAction` for GROUP); `resolvePerAttack`/`resolveGroup` are now thin state-only wrappers (zero test churn). `roundManager` consumes the real `ActionResult`. Removed both no-op appenders. Crushing Blow is now actually applied (degrades target Block SR/SMR/FMR by 0.1, clamped ≥0). Also fixed Rank KO eligibility from the `> target.rank + 0.49` float proxy to the exact `attacker.rank - target.rank >= 0.5` (Excel Math!D). 1025 tests pass.
- **Prevention:** A function that computes a result and discards it (a no-op "append" hook) is a red flag — either return the result or don't compute it. When one layer reverse-engineers another layer's output, plumb the real value through instead.


## Resolved Issues

### 2026-02-22 — Fastify plugin-scoped state isolation
- **Description:** `fastify.gameState = value` inside one plugin was invisible to sibling plugins. Cross-plugin state reads returned `null` even after assignment.
- **Root cause:** Fastify's plugin encapsulation — decorated values are scoped to the plugin that modifies them. Direct assignment doesn't propagate to sibling plugins registered at the same level.
- **Solution:** Switched to a container object pattern: `fastify.decorate('gameStateContainer', { state: null })`. All plugins mutate `container.state` (a property on the shared object), which is visible everywhere because the object reference itself is shared.
- **Prevention:** Always use the container pattern when decorated values need to be read/written across multiple sibling Fastify plugins. Never use `fastify.decorated = value` for cross-plugin state.

### 2026-02-22 — Error handler registration order in Fastify
- **Description:** Plugin routes returned 500 for domain errors (SAVE_NOT_FOUND, INVALID_SLOT) instead of the expected 404/400 from the global error handler.
- **Root cause:** `fastify.setErrorHandler()` was called AFTER `fastify.register()`. Plugins capture the active error handler at registration time — so they inherited Fastify's default handler, not our custom one.
- **Solution:** Moved `fastify.setErrorHandler(globalErrorHandler)` to before all `fastify.register()` calls in `buildApp()`.
- **Prevention:** Always register error handlers before plugins in Fastify app factories.

### 2026-02-22 — Shallow freeze on NPC personality sub-objects
- **Description:** `Object.freeze(npc)` froze the top-level NPC object but not the nested `personality` object. Mutations to `npc.personality.patience = 99` were not rejected.
- **Root cause:** `Object.freeze()` is shallow — it only freezes own properties of the target object.
- **Solution:** Applied `Object.freeze()` to each NPC's `personality` literal at construction time.
- **Prevention:** For deeply immutable objects, freeze all nested objects explicitly. Consider a deep-freeze utility for complex state trees.

### 2026-02-22 — TypeScript double-cast needed for destructuring unknown type
- **Description:** `const { timestamp: _ts, ...bad } = makeGameState() as Record<string, unknown>` produced a TS2352 error — GameState doesn't sufficiently overlap with `Record<string, unknown>`.
- **Root cause:** TypeScript strict mode prevents casting between non-overlapping types in a single step.
- **Solution:** Double-cast via unknown: `makeGameState() as unknown as Record<string, unknown>`.
- **Prevention:** When casting to a structurally incompatible type for test purposes, use the `as unknown as T` pattern.

### 2026-03-07 — Missing slot validation on DELETE endpoint (Security Pattern)
- **Description:** DELETE /saves/:slot route was missing input validation before delegating to persistence layer. `parseInt` could return `NaN`, and even though `deleteSave()` would throw, the unvalidated value was echoed in the response.
- **Root cause:** Blueprint omission — other slot routes (save/load) had the guard, but the new DELETE route was implemented without it. Security review caught the inconsistency.
- **Solution:** Added `isNaN()` + range guard (`slot < 1 || slot > 10`) matching the established pattern on save/load routes.
- **Prevention:** Slot validation must be enforced at the API layer BEFORE delegation to persistence functions. Never rely on lower layers as the sole input guard. When adding routes that follow an existing pattern, copy all guards, not just the happy-path logic.

### 2026-03-22 — Vitest + @testing-library/react requires explicit cleanup
- **Description:** DOM from previous tests leaked into subsequent tests, causing "Found multiple elements" errors with `getByText` and `getByRole` queries.
- **Root cause:** `@testing-library/react` does not auto-cleanup in Vitest (unlike Jest). The `cleanup()` function must be called explicitly via `afterEach(cleanup)` in the test setup file.
- **Solution:** Added `import { cleanup } from '@testing-library/react'` and `afterEach(() => { cleanup(); })` to `client/src/test/setup.ts`.
- **Prevention:** Always include explicit cleanup in Vitest setup when using @testing-library/react. This is a known Vitest compatibility issue.

### 2026-03-22 — vi.mock hoisting breaks ApiClientError instanceof checks
- **Description:** Auto-mocking a module containing both functions and a class (`apiClient.ts` with `apiGet`, `apiPost`, and `ApiClientError`) replaced the class with a mock, breaking `err instanceof ApiClientError` checks in production code under test.
- **Root cause:** `vi.mock()` is hoisted to the top of the file. Variables referenced inside the factory must use `vi.hoisted()`. The class must be re-defined inline in the factory function.
- **Solution:** Used `vi.hoisted()` for mock function refs, defined `ApiClientError` class inline in `vi.mock()` factory, then imported the mocked class after the mock declaration.
- **Prevention:** When mocking modules that export both functions and classes used with `instanceof`, always define the class inline in the `vi.mock()` factory. Never rely on auto-mock for modules with class exports that are used in type checks.

### 2026-05-30 — Base damage formula was invented, not ported from Excel (ADR-007 violation)
- **Description:** `calculateBaseDamage()` used `attackerPower * (attackerPower / targetPower)` — a "power dominance" model with no basis in the GM Combat Tracker. The base/raw damage of every attack (the core balance number) was wrong. A code comment claimed it "matches" Math!A40:AM54, but that range was never transcribed into the markdown docs, so the claim was unverifiable and the TDD-against-Excel guarantee (ADR-015) never actually covered it. `formulas.test.ts` even carried a NOTE: "Formula pending Excel verification."
- **Root cause:** The damage formula lives only in the Excel cells (`Math!O`, "Action Power"), not in `GM_Combat_Tracker_Documentation.md`. The implementer modeled a plausible formula instead of extracting the real one, violating ADR-007 ("exact replication, no improvements").
- **Solution:** Extracted the Math sheet directly from the `.xlsx` (it is a zip; sheet "Math" = `xl/worksheets/sheet7.xml`). Math!O = `MROUND(L + M + N, 0.25)` where L = attacker Power (Attack/Counter), M = Special Power = `Power × (1 + segments/10)`, N = Group term. **Base damage = the attacker's Power stat, rounded to 0.25 — no target-power term.** Target Power only feeds the Crushing Blow threshold (`Math!AF = (O − targetPower)/targetPower`) and the SMR/FMR mitigation lookups, both applied separately. Rewrote `calculateBaseDamage` accordingly (targetPower param kept but vestigial), updated `formulas.test.ts` + `counterChain.test.ts` to Excel-correct values. All 1020 backend tests pass.
- **Prevention:** When a formula's source is an Excel cell not present in the markdown, extract the cell formula from the `.xlsx` before implementing — never model a "plausible" replacement. ADR-007/ADR-015 require Excel-derived values; a test asserting an unverified formula is not a real contract. Remaining un-audited combat formulas should be spot-checked the same way against the Math sheet.

## Prevention Notes

When adding new bugs, think about:
- What conditions trigger this bug?
- How can we detect it earlier?
- What architectural patterns could prevent it?
