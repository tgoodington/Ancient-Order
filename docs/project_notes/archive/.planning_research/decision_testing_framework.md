# Decision Research: Testing Framework

## Current State
- Jest referenced in package.json scripts but NOT installed as dependency
- No Jest config, no test files, no fixtures, no mocks
- Zero test infrastructure exists

## Testability Analysis
- personalitySystem.ts and dialogueEngine.ts are pure functions — directly unit testable, no mocking needed
- API routes use Express Router factories — need supertest for HTTP-level testing
- persistence/saveLoad.ts has file I/O — needs fs mocking or temp directory
- State updaters are pure functions — directly unit testable

## Sprint 1 Spec
- No testing scenarios documented in Sprint1_API_Reference.md
- Spec focuses on interfaces and endpoints only
