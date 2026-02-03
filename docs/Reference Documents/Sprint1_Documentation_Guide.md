# Sprint 1: Documentation Summary & Navigation Guide

## Overview

You have 4 comprehensive documents designed to guide Claude Code through building the Act 1 prototype backend. Each document serves a specific purpose in the development workflow.

---

## Document 1: Sprint1_ClaudeCode_Prompt.md
**This is the main prompt to give Claude Code.**

### What It Contains
- Complete project context and design philosophy
- Technical architecture requirements (Node.js, Express, immutability, etc.)
- Six core implementation goals with detailed specifications
- Example data structures (test player character, 3-4 test NPCs)
- Starting point code organization
- Implementation checklist (what gets built first)
- Key constraints and design notes

### When to Use It
- **Give this to Claude Code first** when starting Sprint 1
- Reference it during development if you need to clarify requirements
- Use the checklist to track progress

### Key Sections
1. **Technical Architecture Requirements** - Stack, core principles, persistence strategy
2. **Sprint 1 Implementation Goals** - Immutable state, personality system, NPCs, dialogue, save/load, API
3. **Starting Point** - Example test data and organizational structure
4. **Implementation Checklist** - Phased approach (Phases 1-4)

---

## Document 2: Sprint1_Technical_Reference.md
**Deep dive into personality system and dialogue implementation.**

### What It Contains
- Personality system mechanics with formulas and examples
- Personality gate implementation and logic
- Complete example dialogue trees for 3 NPCs
- Dialogue system architecture
- Testing checklist

### When to Use It
- **Reference during personality system implementation** to ensure correctness
- Use dialogue examples as templates when creating new NPC dialogue trees
- Check testing checklist before marking personality/dialogue as complete

### Key Sections
1. **Personality System Deep Dive** - Mechanics, adjustment algorithm, examples
2. **Personality Gate Implementation** - Gate types, evaluation logic, design principles
3. **Example NPC Dialogue Trees** - 3 fully-featured NPCs (Elena, Lars, Kade) with personality-gated options
4. **Dialogue System Architecture** - State tracking, conversation flow, data structure
5. **Testing Checklist** - What to verify for each system

### Critical Algorithm Reference
The personality adjustment algorithm in section 1 is **essential**—use this exact logic for trait redistribution.

---

## Document 3: Sprint1_API_Reference.md
**Complete API specification and TypeScript interfaces.**

### What It Contains
- Full TypeScript interface definitions for all game objects
- 14 REST endpoints with request/response examples
- Error handling and validation rules
- Data validation rules with examples
- Session state management patterns
- 4 testing scenarios with step-by-step verification

### When to Use It
- **Reference when building Express.js endpoints**
- Use interface definitions as TypeScript templates
- Check testing scenarios for validation strategy
- Reference error codes when implementing error handling

### Key Sections
1. **TypeScript Interfaces** - Complete definitions for GameState, PlayerCharacter, NPC, Dialogue, etc.
2. **REST API Endpoints** - Game management, NPC/dialogue, player management
3. **Error Response Format** - Standard error structure with common error codes
4. **Data Validation Rules** - Constraints for personality, team composition, save slots
5. **Implementation Notes** - Session state patterns, immutability examples, validation functions
6. **Testing Scenarios** - 4 complete test cases with expected outcomes

---

## Document 4: GM_Combat_Tracker_Documentation.docx (from project)
**Reference only—combat is not implemented in Sprint 1.**

### What It Contains
- Complete 3v3 turn-based combat system documentation
- Combat mechanics (Rank KO, Blindside, Crushing Blow)
- 17-sheet Excel architecture
- Path/elemental system details

### When to Use It
- **Don't build this yet—it's for Sprint 3-4**
- Read for context understanding (helps with game narrative)
- Reference `activeCombat` fields in GameState interfaces
- Use for understanding personality path system (affects narrative/dialogue later)

---

## Quick Start: How to Use These Documents with Claude Code

### Step 1: Initial Prompt (Day 1)
```
"Build the backend for Sprint 1 of my game prototype. 
Here's the complete specification: [provide Sprint1_ClaudeCode_Prompt.md]
You'll want to reference these documents:
- Sprint1_Technical_Reference.md (for personality system and dialogue examples)
- Sprint1_API_Reference.md (for API endpoints and TypeScript interfaces)"
```

### Step 2: During Personality Implementation
```
"Building personality system now. Check the algorithm in Sprint1_Technical_Reference.md, 
section 'Personality Adjustment Algorithm'. Make sure redistribution maintains sum=100%."
```

### Step 3: During Dialogue Implementation
```
"Building dialogue trees. Use the examples in Sprint1_Technical_Reference.md 
(Elena, Lars, Kade) as templates. Each NPC should have 2-3 dialogue nodes 
with personality-gated options."
```

### Step 4: During API Implementation
```
"Building REST endpoints now. Sprint1_API_Reference.md has the complete specification. 
Use the TypeScript interfaces directly, follow the request/response examples, 
and implement error handling with the error codes listed."
```

### Step 5: Testing Phase
```
"Testing phase. Run through the 4 scenarios in Sprint1_API_Reference.md section 'Testing Scenarios'. 
Also check the 'Testing Checklist' in Sprint1_Technical_Reference.md for personality/dialogue testing."
```

---

## Document Cross-References

When you encounter specific topics during development, here's where to find them:

### Personality System
- **Overview:** Sprint1_ClaudeCode_Prompt.md, Goal 2
- **Algorithm:** Sprint1_Technical_Reference.md, section 1
- **Examples:** Sprint1_Technical_Reference.md, section 1 (Scenario examples)
- **API:** Sprint1_API_Reference.md, endpoint `/api/player/personality`
- **Testing:** Sprint1_Technical_Reference.md, Testing Checklist

### Dialogue System
- **Overview:** Sprint1_ClaudeCode_Prompt.md, Goal 4
- **Personality Gates:** Sprint1_Technical_Reference.md, section 2
- **Example Trees:** Sprint1_Technical_Reference.md, section 3
- **Architecture:** Sprint1_Technical_Reference.md, section 4
- **API:** Sprint1_API_Reference.md, endpoints `/api/dialogue/*`
- **Testing:** Sprint1_Technical_Reference.md, Testing Checklist

### Game State & Save/Load
- **Structure:** Sprint1_ClaudeCode_Prompt.md, Goal 1
- **Interfaces:** Sprint1_API_Reference.md, TypeScript Interfaces
- **Persistence:** Sprint1_ClaudeCode_Prompt.md, Goal 5
- **API:** Sprint1_API_Reference.md, endpoints `/api/game/*`
- **Testing:** Sprint1_API_Reference.md, Scenario 2 (Save and Load)

### REST API Implementation
- **All endpoints:** Sprint1_API_Reference.md, section REST API Endpoints
- **Error handling:** Sprint1_API_Reference.md, Error Response Format
- **Validation:** Sprint1_API_Reference.md, Data Validation Rules
- **Patterns:** Sprint1_API_Reference.md, Implementation Notes

---

## Key Design Principles (Summary)

These principles appear throughout all documents. Keep them in mind:

### 1. Immutability
- Never mutate objects
- Always create new objects when state changes
- Example: Use spread operators (`{...obj}`) not mutations

### 2. No Dead Ends
- Every dialogue option must lead somewhere
- Players can always progress, choices affect narrative not availability
- At least one dialogue option per node must always be available

### 3. Personality Redistribution
- When a trait increases, others decrease proportionally
- Final sum must always equal 100%
- Traits are always 5-35% range

### 4. Fixed NPC Archetypes
- NPCs never change their personality
- Only player personality changes based on choices
- Dialogue options reflect NPC's fixed archetype

### 5. Deterministic Systems
- Same input = same output (for save/load reliability)
- No randomness in personality or dialogue systems
- All calculations must be reproducible

---

## Common Questions & Answers

### Q: Do I need to implement combat in Sprint 1?
**A:** No. Combat is Sprint 3-4. Sprint 1 is narrative/state only. The `activeCombat` and `combatHistory` fields in GameState are placeholders for now.

### Q: How many NPCs should I create?
**A:** At least 3-4 test NPCs. Use the examples in Sprint1_Technical_Reference.md (Elena, Lars, Kade) as templates. You can add more, but these three demonstrate the key archetype/faction combinations.

### Q: Should I implement a React frontend in Sprint 1?
**A:** No. Sprint 1 is backend + REST API only. The React frontend comes later. Use Postman or curl to test the API endpoints.

### Q: How should I handle save file storage?
**A:** Use a `saves/` directory with JSON files (`saves/slot_1.json`, etc.). Sprint 1 is file-based. Database integration comes later.

### Q: What if the personality adjustment doesn't sum to 100%?
**A:** That's a bug. Use the algorithm in Sprint1_Technical_Reference.md exactly. The final normalization step ensures the sum is always correct (with floating-point tolerance).

### Q: Can I add more dialogue nodes to NPCs than the examples show?
**A:** Yes! The examples (Elena, Lars, Kade) show the pattern. Each node is an object with an ID, text, and options. Create as many as you want—just make sure each option leads somewhere.

### Q: Should I validate personality constraints on the backend?
**A:** Yes, absolutely. Every endpoint that modifies personality should validate that the final state sums to 100% and each trait is 5-35%. See Sprint1_API_Reference.md, Implementation Notes section.

---

## Success Criteria for Sprint 1

By the end of Sprint 1, you should have:

### Backend Structure
- [ ] Node.js + Express.js project with proper structure
- [ ] TypeScript for type safety
- [ ] `src/` directory with subdirectories: types, state, dialogue, persistence, api

### Functionality
- [ ] Complete immutable game state (no mutations anywhere)
- [ ] Personality adjustment with redistribution (sum always = 100%)
- [ ] Dialogue trees with personality-gated options
- [ ] Save/load system (file-based, JSON format)
- [ ] Express API with all 14+ endpoints working

### Data & Examples
- [ ] Test player character (fully populated)
- [ ] 3-4 test NPCs with complete dialogue trees
- [ ] Example personality adjustments demonstrating gate filtering
- [ ] Multiple save/load cycles working correctly

### Documentation
- [ ] API endpoint documentation (what each does)
- [ ] How to extend with new NPCs
- [ ] How personality system works (for future developers)

### Testing
- [ ] All 4 testing scenarios from Sprint1_API_Reference.md passing
- [ ] Personality system testing checklist complete
- [ ] Dialogue system testing checklist complete
- [ ] No mutations anywhere in the codebase

---

## Notes for Future Sprints

### Sprint 2: Narrative Expansion
- Expand dialogue trees with more NPCs
- Add quest flags and relationship flags system
- Implement location system
- More personality-gated narrative branches

### Sprint 3-4: Combat Integration
- Reference GM_Combat_Tracker_Documentation.docx
- Integrate combat system with game state
- Implement combat endpoints
- Connect personality paths to combat mechanics

### Sprint 5+: Frontend & Refinement
- Build React frontend
- Integrate with Express API
- Add audio/visual elements
- Performance optimization

---

## Getting Help

If Claude Code encounters issues during implementation:

1. **For personality algorithm issues:** See Sprint1_Technical_Reference.md, Personality System Deep Dive
2. **For dialogue problems:** Check examples (Elena, Lars, Kade) in Sprint1_Technical_Reference.md
3. **For API issues:** Reference Sprint1_API_Reference.md exactly
4. **For immutability problems:** See Implementation Notes in Sprint1_API_Reference.md
5. **For validation errors:** Check Data Validation Rules in Sprint1_API_Reference.md

---

## File Organization

```
/game-prototype/
├── src/
│   ├── types/
│   │   └── index.ts                 # All TypeScript interfaces
│   ├── state/
│   │   ├── gameState.ts             # Initial state creation
│   │   └── stateUpdaters.ts         # Immutable update functions
│   ├── dialogue/
│   │   ├── dialogueTrees.ts         # NPC dialogue definitions
│   │   └── dialogueEngine.ts        # Dialogue logic
│   ├── personality/
│   │   └── personalitySystem.ts     # Personality adjustment
│   ├── persistence/
│   │   └── saveLoad.ts              # File-based save/load
│   └── api/
│       ├── index.ts                 # Express app setup
│       ├── game.ts                  # Game endpoints
│       ├── dialogue.ts              # Dialogue endpoints
│       ├── npc.ts                   # NPC endpoints
│       └── player.ts                # Player endpoints
├── saves/                           # Save file directory
├── package.json
└── tsconfig.json
```

---

## Document Maintenance

These documents are designed to be self-contained. If you update one document, ensure consistency across all four:

- **Change in architecture?** Update both ClaudeCode_Prompt and API_Reference
- **New personality rules?** Update both Technical_Reference and API_Reference validation
- **New NPC?** Add to Technical_Reference examples if it demonstrates a new pattern
- **New API endpoint?** Add to API_Reference with full specification

---

## Final Notes

These documents represent **months of design and iteration** condensed into clear specifications. The personality system, dialogue gating, immutability patterns, and save/load architecture are all tested designs. Follow them closely, and Sprint 1 will be solid.

The key to success is **precision in implementation**:
- Personality sum must always equal 100% (with floating-point tolerance)
- Dialogue options must always be available (no dead ends)
- Game state must be fully immutable (no mutations)
- All validation must happen on the backend (trust but verify)

Good luck! 🎮
