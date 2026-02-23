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

(None currently tracked)

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

## Prevention Notes

When adding new bugs, think about:
- What conditions trigger this bug?
- How can we detect it earlier?
- What architectural patterns could prevent it?
