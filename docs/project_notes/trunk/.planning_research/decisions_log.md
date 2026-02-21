# Planning Decisions Log

## Build Sequencing
- **Decision**: Whether to build Sprint 1 and Sprint 2 linearly or in parallel tracks
- **Choice**: Linear — Sprint 1 complete first, then Sprint 2
- **Status**: Locked
- **Rationale**: Sprint 1 establishes the state management foundation Sprint 2 extends. Linear sequencing ensures a tested base before combat engine build begins.
- **Alternatives**: Types-first then parallel (riskier due to type churn), interleaved by system (too complex for execution agents)

## Immutability Approach
- **Decision**: How to enforce immutable state management
- **Choice**: Deferred to engineering phase
- **Status**: Open Engineering Question
- **Rationale**: "State must be immutable" is the planning-level constraint. The mechanism (TypeScript Readonly types, Immer, or convention-only) is a code-level HOW the engineer decides. User preference leans toward Spread + Readonly if engineer needs guidance.
- **Alternatives**: Spread + Readonly<> types (user preference), Immer, spread-only (archived approach)

## HTTP Framework
- **Decision**: Which HTTP framework to use for the REST API layer
- **Choice**: Fastify
- **Status**: Locked
- **Rationale**: User selected. TypeScript-native, built-in JSON Schema validation, faster than Express. Archived Express patterns will need adapting to Fastify plugin architecture.
- **Alternatives**: Express.js (archived patterns use it), Hono (lightest)

## Test Framework
- **Decision**: Which test framework to use for TDD formula porting and all unit/integration tests
- **Choice**: Vitest
- **Status**: Locked
- **Rationale**: Native TypeScript (no ts-jest config), fast watch mode critical for TDD formula porting workflow (ADR-015), Jest-compatible API. Jest is listed in package.json but not installed — clean choice with no migration cost.
- **Alternatives**: Jest (not installed, slower TS config), Node built-in runner (minimal ecosystem)
