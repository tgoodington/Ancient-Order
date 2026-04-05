# Decision Research: Toolchain & Project Structure

## Vite + React (Recommended)
- Native ESM aligns with ADR-016 and backend's "type": "module"
- server.proxy routes /api/* to localhost:3000 — zero CORS config
- Vitest already the test framework — Vite shares config
- Near-instant HMR for component iteration
- No SSR/SSG overhead (not needed: single-player game, no SEO)

## Next.js
- SSR/SSG capabilities unused (no SEO, no public pages)
- API routes redundant (backend already exists)
- File-based routing unnecessary (game navigation is state-machine driven, not URL)
- "use client" annotations everywhere since all code is client-side
- Larger dependency surface for zero benefit

## Project Structure: client/ subfolder (Recommended)
- Shared git history, single repo for solo dev
- client/ gets its own package.json (separate deps: react, vite, etc.)
- client/tsconfig.json extends root, overrides for DOM + bundler moduleResolution
- Trade-off: root package.json uses NodeNext resolution; client needs "bundler" — separate configs required

## Separate Repo
- More isolation but unnecessary complexity for solo-dev prototype
- Cross-repo coordination overhead for no benefit at this scale
