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

### 2026-05-31 — Natural-20 defense roll → 0 damage rule not implemented
- **Description:** All three Excel defense formulas wrap with `IF(defenseRoll = 20, 0, …)` — a defense roll of exactly 20 fully negates damage regardless of SR. Our `resolveDefense` has no such rule.
- **Status:** Deferred by decision (2026-05-31). Documented as a known, intentional omission; revisit if combat fidelity becomes a priority. Note our defense roll convention is also inverted vs Excel (we use low-roll-success `roll ≤ SR·20`; Excel uses high-roll-success `roll/20 ≥ 1−SR`) — statistically identical (`P=SR`) and internally consistent, so no action, but a literal natural-20 port would need the convention reconciled first.

## Resolved Issues

### 2026-05-31 — Dodge/Parry success was full-negation instead of SMR-mitigated (ADR-054 Layer A)
- **Description:** `Math` sheet `T40` (Dodge) and `V40` (Parry) resolve a *successful* defense as `(1 − SMR) × ActionPower` — parallel to Block's `R40`. The implementation hardcoded dodge/parry success → `0` (full evade / counter) and `ReactionSkills` omitted `dodge.SMR`/`parry.SMR`, following the documentation's simplification rather than the Excel. Separately, `counterChain.ts` forced a Parry on every counter exchange (a parry-only loop) instead of letting the target choose a reaction.
- **Solution (Layer A of ADR-054):** Added `SMR` to `dodge`/`parry` in `ReactionSkills`; `calculateDodgeDamage`/`calculateParryDamage` now apply `(1 − SMR) × ActionPower` on success (counter still triggers independently of mitigation). Extended `ModifiedStats`/`BUFF_STAT_MAP` with `dodgeSMR`/`parrySMR` so path buffs fold them. Reworked `counterChain.ts` so each counter is a new attack resolved through `getPreferredDefense` + `resolveDefense`, continuing only on a successful Parry (Block/Dodge ends the chain). Lifted the buff-folding helper to `defense.ts#effectiveReactionSkills`, shared by pipeline and counter chain. Migrated all 12 combatant fixtures (`encounter.json`, `api/combat.ts`) with placeholder SMR (dodge 0.80 / parry 0.90). Re-baselined 7 combat test files. 1030 tests pass.
- **Dodge "Order Speed" base bug:** the Excel Dodge-success base (`VLOOKUP(…$A$31:$D$36,4)` = "Order Speed") is a confirmed spreadsheet bug; the code already operated on Action Power, so applying the SMR formula (mirroring Parry) was the fix — no behavioral regression.
- **Still open (ADR-054 Layer B, feature work not a bug):** per-reaction rank/XP progression and a rank→rates table from the Excel `Defense Simulations` sheet. The fixture SMR values (uniform 0.80/0.90) are placeholders pending a balance pass.
- **Prevention:** When porting a spreadsheet, verify each defense branch against the raw cell formula, not a prose summary — the doc's "full negation" simplification masked the SMR axis the sheet actually defines.

### 2026-05-31 — Special damage rounded twice (diverged from Math!O single MROUND)
- **Description:** The pipeline computed SPECIAL damage as `MROUND(Power, 0.25)` (via `calculateBaseDamage`) and *then* `× (1 + 0.1·segments)` with no final rounding. Excel `Math!O` is a single `MROUND(L + M + N, 0.25)` where the Special term `M = Power × (1 + segments/10)` is already boosted before the one rounding step. The two agree for 0.25-aligned products but diverge by ≤ ~0.13 otherwise (e.g. Power 53, 1 seg: Excel 58.25 vs old 58.3).
- **Root cause:** Base-damage rounding was applied before the Special multiplier instead of after.
- **Solution:** Reordered Step 5 in `pipeline.ts` to compute the raw (un-rounded) action power first (`calculateSpecialDamageBonus` now documented as returning raw Special power), then round once via `calculateBaseDamage`. Added a pipeline test pinning `MROUND(53×1.1, 0.25) = 58.25`. 1028 backend tests pass.
- **Prevention:** When a spreadsheet formula rounds a *sum/product* once at the end, port the rounding as the final step — never round an intermediate term that is then scaled.

### 2026-05-31 — Combat formula audit (spot-check vs Excel Math sheet)
- **Verified exact matches** (extracted from `Math!` Phase 4/5 block, rows 39–41; ADR-007 satisfied): Rank KO threshold `((aR−tR)·3)/10` + eligibility `≥0.5` + check `roll/20≥1−thr` (`D40`/`E40`); Blindside threshold `(aS−tS)/tS` + check (`H40`/`I40`); Crushing Blow threshold `(O−targetPow)/targetPow` + Block-only eligibility + check (`AF40`/`AG40`); Base/Action Power `MROUND(L+M+N,0.25)` with `L`=attacker Power (`O40`/`L40`); Block damage `(1−SMR)`/`(1−FMR)` (`R40`). These confirm the 2026-05-30 base-damage and Rank-KO-eligibility fixes against the source of truth.
- **Not traced in this pass:** energy segment gains (Excel chains `AA/AD` → `A22:E27` + `AA57/AD57`, capped at 6) and ascension thresholds/bonuses — implemented from the documentation table (1.0/0.5/0.5/0.25; 35/95/180), not re-derived from the Excel energy block. Evade regen base (0.30) is a per-combatant data value in Excel, consistent with the doc.
- **Method:** `GM Combat Tracker.xlsx` is a zip; `Math` = `xl/worksheets/sheet7.xml` (rId7). Parsed cells + shared strings to resolve labels/formulas. Repeat this method for any further formula audits.

### 2026-05-30 — Elemental path buffs/debuffs accumulated but never applied; normal attacks hardcoded to Block
- **Description:** `applyPathBuff`/`applyPathDebuff` appended `Buff` entries to `activeBuffs`, and `applyDynamicModifiers` (formulas.ts) existed to fold buffs/debuffs into stats — but the per-attack pipeline resolved defense against `target.reactionSkills` *directly* and never called it, so every elemental-path buff/debuff was mechanically inert. Compounding this, Step 4 hardcoded `'block'` as the reaction for every normal ATTACK, so Dodge/Parry (and therefore Parry-only counter chains) never occurred outside of SPECIAL forced-defense.
- **Root cause:** Two coupled gaps. Defense selection had no policy for the normal-attack reaction (the GM tracker leaves it to "Defender Chooses Reaction"), so it defaulted to block; and the buff-folding hook (`applyDynamicModifiers`) was written but never wired into resolution. The two are interdependent — folding only matters once dodge/parry are reachable.
- **Solution:** Implemented the full "deepen defense resolution" pass (ADR-053). Normal attacks now select the defender's path-signature defense via `getPreferredDefense` (Fire→parry, Air→dodge, Light→block; action paths→block). Defense resolves against *effective* reaction skills — base rates folded with `activeBuffs` via `applyDynamicModifiers` (`_effectiveReactionSkills`), clamped to [0,1]. Extended `BUFF_STAT_MAP` with `_debuff` variants so action-path debuffs fold through. Crushing Blow stays applied directly to base block rates (not folded) to avoid double-counting. Re-baselined affected combat tests; the integration counter-chain test now asserts the chain via accurate round history rather than inferring it from damage. 1025 backend + 54 client tests pass.
- **Prevention:** A compute helper that is exported but never called from the resolution path is a red flag (same lesson as the result-discard bug). When a subsystem (elemental paths) has no observable effect, trace whether its outputs are actually consumed downstream — not just produced.

### 2026-05-31 — Path buffs/debuffs stacked unbounded per hit
- **Description:** Once buff folding was wired in (above), `applyPathBuff`/`applyPathDebuff` appended a fresh ±0.10 `Buff` of the same type on *every* qualifying exchange, and `Buff.duration` is never ticked. Over a long fight a reaction-path defender's signature SR climbed +0.10 → +0.20 → +0.30 … toward the 1.0 clamp, trending toward near-unhittable.
- **Root cause:** Append-only buff application with no de-duplication or expiry; the path bonus was modeled as a per-trigger event instead of a stable path trait.
- **Solution:** Made application idempotent per `(type, source)` (ADR-053): if the combatant already carries that path's buff/debuff, re-application is a no-op. The bonus is now a flat ±0.10 path trait for the whole combat. Added idempotency tests; 1027 backend tests pass. Chosen over capped-stacking and `duration`-based decay (decay would need round-boundary tick infrastructure the engine lacks).
- **Prevention:** When a per-event modifier is meant to represent a persistent trait, apply it idempotently (key by type+source) or give it an explicit, ticked duration — never append-only without a bound.

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
