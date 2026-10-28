# Port record: TypeScript build to C# (godot-foundation)

This record documents the move of the game rules, session, and saves from the TypeScript build to C# on Godot (ADR-063). It outlives the branch: the combat sprint reads the combat balance review, Sprint 7 reads its notes, and the designer reads the open creative questions.

The TypeScript build is frozen at the commit this branch started from (367be1b, "Sprint 6: run the game entirely in the browser"); the branch does not change or delete it. Every entry names the decision date. Decisions marked "build" were made during the build (mostly security-review hardening) rather than in design.

How the record is checked: `PortRecordTests.EveryTypeScriptTestIsAccounted` (rules/AncientOrder.Rules.Tests/PortRecord) reads the in-scope TypeScript test files (the files named by the test tasks, read only), extracts every `it(` and `test(` title (an `it.each` counts once, under its template title), and passes only if each title is a C# test display name or is listed here. A listed test has the form `- <TypeScript file> › <title> → <reason>`; a second test checks that every such line names a real TypeScript test. Tests carried over unchanged keep their TypeScript title as their C# display name and need no entry. A "changed" entry means the test is carried over (usually under the same name) but its setup, call, or assertion changed.

Sections: Build and conventions; Personality; Characters and game state; Lineup and synergy; Combat math; Enemy decisions; Round resolution; Fight setup; Game session; Saves; Godot project; Combat balance review (combat sprint); Open creative questions; Notes for Sprint 7.

## Build and conventions

No TypeScript tests belong to this system.

### Deliberate differences

- Every C# project targets .NET 10. Why: Godot 4.7.2 built and ran a net10.0 game project headless; .NET 8 support ends Nov 2026 and the machine has no .NET 8 runtime. Desktop export with net10 is unverified until export templates are installed; retargeting is a one-property change. (2026-10-28)
- A classic AncientOrder.sln, not .slnx. Why: Godot builds the csproj directly and there is no evidence it reads .slnx; a classic solution also stops the Godot editor from generating a second one. (2026-10-28)
- The rules library has zero dependencies, enforced by a build guard and an architecture test. Why: ADR-063: the rules are independent of Godot and rendering; the guard fails the build before any test runs. (2026-10-28)
- Immutable sealed records; order is always explicit (ImmutableArray where order matters, ImmutableDictionary for lookup only and never iterated, no sorted dictionaries). Why: A spike showed System.Text.Json reloads an ImmutableSortedDictionary with the culture-sensitive default comparer, so key order changed with the OS language and re-saves differed. The TypeScript iterates objects in insertion order (Elena, Lars, Kade), which id-sorted order would silently change (group damage is summed in insertion order and double addition is order-sensitive). (2026-10-28, after research)
- Culture-sensitive string APIs are build errors in the rules. Why: Same root cause as the sorted-map finding; catches the bug class at compile time. (2026-10-28)
- Refusals are Result<T> values; only real bugs throw, and the session turns them into INTERNAL_ERROR. Why: Carries ADR-057 results-as-values into the rules themselves. (2026-10-28)
- Rolls, clock, and ids are handed in (IRollSource, TimeProvider, IIdSource). Why: Any outcome can be reproduced exactly from its rolls, live fights included. (2026-10-28)
- The library has its own seeded generator (xoshiro256** via SplitMix64), not System.Random. Why: Microsoft documents that seeded System.Random sequences may differ between .NET versions, which would break replays after an upgrade. Integer-only state is exactly reproducible; a golden test pins it. (2026-10-28, after research)
- Double arithmetic is kept with JS-identical rounding (halves up), stable sorts, and the same order of operations; transcendental and fused math are banned in the rules. Why: Integer or fixed-point math would change results throughout combat and break parity. IEEE 754 requires + - * / to be correctly rounded and the engine uses only those plus floor, round, min, max, so results match the TypeScript to the last digit. Whether RyuJIT ever fuses a*b+c implicitly could not be sourced; the carried-over tests pin exact values and would catch it. (2026-10-28, after research)
- xUnit v3 on Microsoft.Testing.Platform, one C# test file per TypeScript test file with the same test names as display names. Why: Lets this record account for every TypeScript test one to one (checked by PortRecordTests.EveryTypeScriptTestIsAccounted). (2026-10-28)
- verify.ps1 is the single check (formatting, build with nullable and analyzer warnings as errors, tests, Godot import when needed, Godot smoke run). Why: Mirrors `npm run verify`: the C# equivalent of typecheck plus lint plus tests. (2026-10-28)
- The TypeScript build is a frozen reference at the commit this branch started from. Why: ADR-063: the engine move to Godot 4.7 (.NET) with C#. The TypeScript is not changed or deleted by this branch. (2026-10-28)

### Tests changed

None.

### Tests dropped

None.

### Set aside

- Banned-API list misses Environment.TickCount, Stopwatch, HashCode and Guid.CreateVersion7; the build guard can be bypassed through IsImplicitlyDefined metadata or linked source (security review, not acted on). (for: a later hardening pass)
- A few public entry points throw on null or default arguments (FromState(null), public Evaluate/Apply with null, default or zero thresholds). Programmer-error throws are allowed by the conventions (security review, not acted on). (for: a later hardening pass)
- Desktop export with net10.0 is not tested; export templates are not installed. (for: the export or release sprint)

## Personality

### Deliberate differences

- The micro-correction leaves the leftover with the largest trait that can take it within 5-35. Why: Today a trait capped at 35 swallows the correction and the total stays at 99.99, so saving is refused. Proven by running both versions over 800,000 adjustments (parity check: 20,002 cases with 0 differences where the TypeScript summed to 100; 808 buggy cases fixed). (2026-10-28 (T2))
- Validation reports non-finite traits invalid ("{name} (NaN) is not a finite number") and non-finite sums; the TypeScript accepted NaN. Why: Security review finding during the build. (2026-10-28 (T2, build))
- Everything else is kept as today: default personality with charisma 16.65, a zero delta counts as adjusted, tolerance 0.01 for validity, 10 passes. Why: No reason to change; parity is the default. The default personality comment ("all equal") is wrong, not the value: six traits at 16.67 would total 100.02. (2026-10-28 (T2))

### Tests changed

None.

All 39 personalitySystem tests carried over by name; the three clampTrait tests go through Adjust because Clamp is private. New: a randomized invariant test (at least 100,000 seeded adjustments, seed 20261028) guards the personality limits; the invariant test uses a seeded System.Random in the test project only.

### Tests dropped

None.

### Set aside

None.

## Characters and game state

### Deliberate differences

- Characters split into a read-only catalog (from content) and per-game character state, with one characters module owning all character reads and changes behind the session; no stateful controller object. Why: Designer decision (asked for a character controller that expands to the full game without rework). Godot docs note a loaded Resource is one shared copy, so fixed definitions and per-game state must be separate. One immutable snapshot stays the single source of truth (ADR-057). (2026-10-28 (T3))
- A new game takes its cast, player name, starting personality, and lineup size from content handed in; the library has no characters built in. Elena, Lars, and Kade become a test-only cast. Why: Matches Sprint 7 settled content-pack shape, so Sprint 7 adds loading from files without changing the rules (ADR-062). (2026-10-28 (T3))
- Characters gain a name field. Why: The TypeScript NPC type has none; Sprint 7 character design includes it. Part of the designer-approved new-game setup. (2026-10-28 (T3))
- currentDialogueNode, conversationLog, narrativeState, saveSlot, and the dialogue and scene types and error codes are dropped. Why: They served only the retired engines (saveSlot was never written). Sprint 7 brings its own conversation state. (2026-10-28 (T3))
- Changing an unknown character relationship is refused with NPC_NOT_FOUND instead of silently ignored. Why: Results-as-values: a silent no-op hides content mistakes. Nothing in today build calls these updaters. Shown to the designer under "smaller things I will handle unless you object"; no objection. (2026-10-28 (T3))
- A non-finite affection or trust change is refused with VALIDATION_ERROR (the TypeScript stored NaN). Null definitions, personality or settings and default arrays are refused as VALIDATION_ERROR rather than crashing. Why: Security review during the build. (2026-10-28 (T3, build))
- Content creation refuses an empty player name, negative lineup size, invalid starting personality or player combat stats, non-finite stats, and undefined path or ascension values; errors carry paths such as characters/0/id. Why: Internal content checks added during the build so bad content is refused at load; whitespace-only names are accepted. (2026-10-28 (T3, build))
- Requirements and conditions on characters come from Sprint 7 shared condition system, not a characters-only one. Why: Avoids two condition systems. (2026-10-28 (T3))
- Saves keep only character state and pair it with the current catalog on load. Why: Sprint 7 settled rule: content is authoritative for fixed traits, so content fixes reach existing saves (see Saves). (2026-10-28 (T3))
- Character archetype ids in the test cast use the archetype record ids (elena_loyal_scout, lars_scheming_merchant, kade_rogue_outlaw) instead of the display labels (Loyal Scout, Scheming Merchant, Rogue Outlaw). Why: Unifies the two archetype notions: the NPC display labels and the combat profile ids; characters and encounter enemies point to one archetype record. (2026-10-28 (T6))

### Tests changed

- src/state/gameState.test.ts › each call generates a unique player id → the player id comes from the caller (the session id source), so uniqueness is the caller job; NewGame keeps the id it is given
- src/state/gameState.test.ts › npcs record contains exactly 3 NPCs → rewritten against the test cast instead of the hard-coded roster
- src/state/gameState.test.ts › Elena is retrievable by id from npcs → rewritten against the test cast instead of the hard-coded roster
- src/state/gameState.test.ts › Lars is retrievable by id from npcs → rewritten against the test cast instead of the hard-coded roster
- src/state/gameState.test.ts › Kade is retrievable by id from npcs → rewritten against the test cast instead of the hard-coded roster
- src/state/gameState.test.ts › timestamp is a positive number close to now → the timestamp is the explicit now handed to NewGame, not the clock
- src/state/stateUpdaters.test.ts › updates the timestamp field → there is no separate updateTimestamp; every update stamps the explicit now, so the cases run through GameStateUpdates.WithCombat
- src/state/stateUpdaters.test.ts › does not change other fields → there is no separate updateTimestamp; every update stamps the explicit now, so the cases run through GameStateUpdates.WithCombat
- src/state/stateUpdaters.test.ts › returns state unchanged if NPC not found → an unknown character is now an NPC_NOT_FOUND refusal (affection, trust, and relationship blocks); the TypeScript returned the same state silently

### Tests dropped

- src/state/gameState.test.ts › currentDialogueNode is null → retired dialogue engine (Sprint 7 brings its own conversation state)
- src/state/gameState.test.ts › conversationLog starts as an empty array → retired dialogue engine (Sprint 7 brings its own conversation state)
- src/state/gameState.test.ts › saveSlot is null → field removed: saveSlot was never written and is not part of game state
- src/state/npcs.test.ts › contains exactly 3 NPC templates → removed hard-coded data: the library has no characters built in; the test cast and CharacterCatalogTests/CharacterRulesTests replace it
- src/state/npcs.test.ts › contains Elena with correct id and archetype → removed hard-coded data: the library has no characters built in; the test cast and CharacterCatalogTests/CharacterRulesTests replace it
- src/state/npcs.test.ts › contains Lars with correct id and archetype → removed hard-coded data: the library has no characters built in; the test cast and CharacterCatalogTests/CharacterRulesTests replace it
- src/state/npcs.test.ts › contains Kade with correct id and archetype → removed hard-coded data: the library has no characters built in; the test cast and CharacterCatalogTests/CharacterRulesTests replace it
- src/state/npcs.test.ts › Elena has the correct personality traits → removed hard-coded data: the library has no characters built in; the test cast and CharacterCatalogTests/CharacterRulesTests replace it
- src/state/npcs.test.ts › Lars has the correct personality traits → removed hard-coded data: the library has no characters built in; the test cast and CharacterCatalogTests/CharacterRulesTests replace it
- src/state/npcs.test.ts › Kade has the correct personality traits → removed hard-coded data: the library has no characters built in; the test cast and CharacterCatalogTests/CharacterRulesTests replace it
- src/state/npcs.test.ts › Lars starts with trust of -20 → removed hard-coded data: the library has no characters built in; the test cast and CharacterCatalogTests/CharacterRulesTests replace it
- src/state/npcs.test.ts › Elena and Kade start with trust of 0 → removed hard-coded data: the library has no characters built in; the test cast and CharacterCatalogTests/CharacterRulesTests replace it
- src/state/npcs.test.ts › all NPCs start with affection of 0 → removed hard-coded data: the library has no characters built in; the test cast and CharacterCatalogTests/CharacterRulesTests replace it
- src/state/npcs.test.ts › returns Elena by id → removed hard-coded data: the library has no characters built in; the test cast and CharacterCatalogTests/CharacterRulesTests replace it
- src/state/npcs.test.ts › returns Lars by id → removed hard-coded data: the library has no characters built in; the test cast and CharacterCatalogTests/CharacterRulesTests replace it
- src/state/npcs.test.ts › returns Kade by id → removed hard-coded data: the library has no characters built in; the test cast and CharacterCatalogTests/CharacterRulesTests replace it
- src/state/npcs.test.ts › returns undefined for an unknown NPC id → removed hard-coded data: the library has no characters built in; the test cast and CharacterCatalogTests/CharacterRulesTests replace it
- src/state/npcs.test.ts › returns undefined for an empty string → removed hard-coded data: the library has no characters built in; the test cast and CharacterCatalogTests/CharacterRulesTests replace it
- src/state/npcs.test.ts › returns an array of 3 NPCs → removed hard-coded data: the library has no characters built in; the test cast and CharacterCatalogTests/CharacterRulesTests replace it
- src/state/npcs.test.ts › includes all three archetypes → removed hard-coded data: the library has no characters built in; the test cast and CharacterCatalogTests/CharacterRulesTests replace it
- src/state/npcs.test.ts › returns a different array reference on each call (templates are not mutated) → removed hard-coded data: the library has no characters built in; the test cast and CharacterCatalogTests/CharacterRulesTests replace it
- src/state/npcs.test.ts › NPC_TEMPLATES record is frozen → a check a typed API makes impossible: records are immutable by type
- src/state/npcs.test.ts › Elena template object is frozen → a check a typed API makes impossible: records are immutable by type
- src/state/npcs.test.ts › Lars template object is frozen → a check a typed API makes impossible: records are immutable by type
- src/state/npcs.test.ts › Kade template object is frozen → a check a typed API makes impossible: records are immutable by type
- src/state/npcs.test.ts › NPC personality sub-objects are frozen → a check a typed API makes impossible: records are immutable by type
- src/state/npcs.test.ts › attempting to mutate a template in strict mode throws or silently fails → a check a typed API makes impossible: records are immutable by type
- src/state/stateUpdaters.test.ts › appends the entry to the conversation log → retired dialogue engine (Sprint 7 brings its own conversation state) (addConversationEntry)
- src/state/stateUpdaters.test.ts › creates a new array (not mutating the original) → retired dialogue engine (Sprint 7 brings its own conversation state) (addConversationEntry)
- src/state/stateUpdaters.test.ts › preserves existing entries when appending → retired dialogue engine (Sprint 7 brings its own conversation state) (addConversationEntry)
- src/state/stateUpdaters.test.ts › applies personality adjustment when option has one → retired dialogue engine (Sprint 7 brings its own conversation state) (processDialogueChoice)
- src/state/stateUpdaters.test.ts › applies NPC relationship changes when option has them → retired dialogue engine (Sprint 7 brings its own conversation state) (processDialogueChoice)
- src/state/stateUpdaters.test.ts › appends a ConversationEntry with correct fields → retired dialogue engine (Sprint 7 brings its own conversation state) (processDialogueChoice)
- src/state/stateUpdaters.test.ts › maintains 100% personality sum after processing → retired dialogue engine (Sprint 7 brings its own conversation state) (processDialogueChoice)
- src/state/stateUpdaters.test.ts › handles option with no personality adjustment or npc adjustment → retired dialogue engine (Sprint 7 brings its own conversation state) (processDialogueChoice)
- src/state/stateUpdaters.test.ts › handles option with personality adjustment only → retired dialogue engine (Sprint 7 brings its own conversation state) (processDialogueChoice)
- src/state/stateUpdaters.test.ts › handles option with NPC relationship only → retired dialogue engine (Sprint 7 brings its own conversation state) (processDialogueChoice)
- src/state/stateUpdaters.test.ts › composes correctly: personality and relationship both applied → retired dialogue engine (Sprint 7 brings its own conversation state) (processDialogueChoice)
- src/state/stateUpdaters.test.ts › sets narrativeState.currentSceneId to the provided scene ID → retired narrative engine (Sprint 7 brings its own conversation state) (initializeNarrative, updateNarrativeState, clearNarrative)
- src/state/stateUpdaters.test.ts › includes starting scene in visitedSceneIds → retired narrative engine (Sprint 7 brings its own conversation state) (initializeNarrative, updateNarrativeState, clearNarrative)
- src/state/stateUpdaters.test.ts › initializes choiceFlags as empty → retired narrative engine (Sprint 7 brings its own conversation state) (initializeNarrative, updateNarrativeState, clearNarrative)
- src/state/stateUpdaters.test.ts › initializes sceneHistory as empty → retired narrative engine (Sprint 7 brings its own conversation state) (initializeNarrative, updateNarrativeState, clearNarrative)
- src/state/stateUpdaters.test.ts › updates the timestamp → retired narrative engine (Sprint 7 brings its own conversation state) (initializeNarrative, updateNarrativeState, clearNarrative)
- src/state/stateUpdaters.test.ts › replaces narrativeState with the provided NarrativeState → retired narrative engine (Sprint 7 brings its own conversation state) (initializeNarrative, updateNarrativeState, clearNarrative)
- src/state/stateUpdaters.test.ts › preserves other fields (player, npcs) → retired narrative engine (Sprint 7 brings its own conversation state) (initializeNarrative, updateNarrativeState, clearNarrative)
- src/state/stateUpdaters.test.ts › sets narrativeState to null → retired narrative engine (Sprint 7 brings its own conversation state) (initializeNarrative, updateNarrativeState, clearNarrative)

The dropped stateUpdaters blocks (addConversationEntry 5, processDialogueChoice 10, initializeNarrative 8, updateNarrativeState 4, clearNarrative 4 = 31 tests) reuse some titles that carried-over blocks also use (for example "returns a new object (reference inequality)", "does not mutate the input state"); those instances are dropped too, though the title is matched by name. gameState.test.ts carried over 8 tests unchanged, stateUpdaters.test.ts 28.

### Set aside

- Character requirements and conditions come from the shared condition system, built once in Sprint 7 (see Notes for Sprint 7). (for: Sprint 7)

## Lineup and synergy

### Deliberate differences

- Synergy comes from the player plus the chosen lineup, worked out on demand and reported with each snapshot. Why: Designer: "the bonus should be instantly evident based on the current configuration". Today synergy uses every character and the lineup has no effect on play. Matches ADR-026 intent for swappable parties. Synergy is empty when the lineup is empty, even though Coverage could match on the player alone. (2026-10-28 (T4))
- Bonuses become content; "best only" or "all that qualify" is a content setting; the test content keeps today best-only. Why: Exact conditions are later content work, and overlapping bonuses may have value. ADR-062: systems support the options without committing; the test setting proves parity. (2026-10-28 (T4))
- Stat bonuses go through one pipeline with explicit steps (flat, additive percent, multiply) and one rounding at the end. An additive-percent value is a fraction (0.10 means +10%). Why: The common RPG convention (increased vs more); gear and buffs later reuse it. A single x1.10 bonus reproduces today result exactly. Which step each bonus uses is a balance decision for combat tuning. (2026-10-28 (T4))
- Today scoring math is kept (Well Rounded uses the average coverage, uncapped), and ADR-026 description is corrected. Why: Keep behavior; ADR-026 described the minimum coverage capped at 1.0, which the code never did. (2026-10-28 (T4))
- Lineup size comes from content instead of a hard-coded 2; the duplicate check is generalized to the first repeated id. For size 2 the messages and outcomes match today. Why: The hard-coded party size goes with the hard-coded roster. Refusal order is combat, size, unknown, no-combat-stats, duplicate; the session checks the size input first so session-level order matches the TypeScript. (2026-10-28 (T4))
- The lineup refuses a character with no combat stats ("{id} has no combat stats"). Why: The lineup is who fights, with combat stats from content; characters such as townsfolk may have none. With the all-fighter test cast no carried-over result changes. (2026-10-28 (T4))
- A synergy name tie between "well rounded" and a bond bonus is resolved by content order; "null" results become empty results. Why: Content order is the explicit order the C# keeps. (2026-10-28 (T4, T14))

### Tests changed

- src/state/stateUpdaters.test.ts › sets team to provided NPC IDs → updateTeamComposition moved to LineupRulesTests (lineup of the content size, stamped with the explicit now)
- src/state/stateUpdaters.test.ts › updates timestamp → updateTeamComposition moved to LineupRulesTests (lineup of the content size, stamped with the explicit now)
- src/state/stateUpdaters.test.ts › preserves other fields (player, npcs, conversationLog) → updateTeamComposition moved to LineupRulesTests (lineup of the content size, stamped with the explicit now)
- src/state/stateUpdaters.test.ts › rejects empty array → moved to LineupRulesTests; a rejection is a TEAM_COMPOSITION_INVALID refusal (Result), not a thrown error
- src/state/stateUpdaters.test.ts › rejects array with 1 element → moved to LineupRulesTests; a rejection is a TEAM_COMPOSITION_INVALID refusal (Result), not a thrown error
- src/state/stateUpdaters.test.ts › rejects array with 3 elements → moved to LineupRulesTests; a rejection is a TEAM_COMPOSITION_INVALID refusal (Result), not a thrown error
- src/state/stateUpdaters.test.ts › rejects unknown NPC ID → moved to LineupRulesTests; a rejection is a TEAM_COMPOSITION_INVALID refusal (Result), not a thrown error
- src/state/stateUpdaters.test.ts › rejects duplicate NPC IDs → moved to LineupRulesTests; a rejection is a TEAM_COMPOSITION_INVALID refusal (Result), not a thrown error
- src/state/stateUpdaters.test.ts › thrown error has code TEAM_COMPOSITION_INVALID → moved to LineupRulesTests; a rejection is a TEAM_COMPOSITION_INVALID refusal (Result), not a thrown error

All 19 synergyCalculator tests carried over by name against the test content. New tests cover content-set "best only" vs "all that qualify", lineup-dependent synergy and bit-identical stat steps (StatModifiersTests, 1-500 and fractional values).

### Tests dropped

None.

### Set aside

- Which step (flat, additive percent, multiply) each synergy bonus uses, and the exact bonus conditions. (for: combat sprint (balance) and Sprint 7 (content))
- StatModifiers.Apply can overflow to infinity or NaN with huge unvalidated modifiers; gear or buffs that supply modifiers later must validate them (security review, not acted on). (for: the sprint that adds gear or buffs)

## Combat math

### Deliberate differences

- The combat math is ported exactly as it plays today; every balance finding is set aside for a combat balance review. Why: Designer: so fights can be felt in Godot before rules change. 78 of 78 spot values were identical to the TypeScript. (2026-10-28 (T5))
- Crushing Blow eligibility and threshold use the attack action power (the spreadsheet O: Special-boosted, 0.25-rounded), not base power; implemented at the call site in round resolution. Why: Match the combat spreadsheet (Math!AF uses O). Differs from today only for Special attacks forced into a block; every current fixture power is a whole number, so normal attacks are unchanged. (2026-10-28 (T5))
- Code with no effect today is not ported: resetRoundEnergy and getStartingSegments (no callers), Buff.duration (never counted down), DebuffEffect and the debuff argument (always empty), and the unused target-power parameter of calculateBaseDamage. Why: Dead code. The energy items return with the combat balance review if wanted. (2026-10-28 (T5))
- KO is an explicit flag, not stamina set to -Infinity; buff kinds are an enum (the path buff source string is "Fire", not "fire"); a debuff is a buff with a negative modifier. Why: Typed C# idiom; same outcomes. (2026-10-28 (T5))

### Tests changed

- src/combat/formulas.test.ts › returns the attacker Power when powers are equal (100) → calculateBaseDamage drops the unused target-power argument; expected values unchanged
- src/combat/formulas.test.ts › returns the attacker Power regardless of a weaker target (150) → calculateBaseDamage drops the unused target-power argument; expected values unchanged
- src/combat/formulas.test.ts › returns the attacker Power regardless of a stronger target (80) → calculateBaseDamage drops the unused target-power argument; expected values unchanged
- src/combat/formulas.test.ts › adds a flat modifier (the Group N term): 100 + 10 = 110 → calculateBaseDamage drops the unused target-power argument; expected values unchanged
- src/combat/formulas.test.ts › rounds to the nearest 0.25 per Math!O MROUND → calculateBaseDamage drops the unused target-power argument; expected values unchanged
- src/combat/formulas.test.ts › ignores target power entirely (same result with any target) → calculateBaseDamage drops the unused target-power argument; expected values unchanged
- src/combat/formulas.test.ts › actionSuccess → 1.0 segments → calculateEnergyGain takes an EnergyEvent enum (Action or Reaction) and a bool instead of the string pair; expected values unchanged
- src/combat/formulas.test.ts › actionFailure → 0.5 segments → calculateEnergyGain takes an EnergyEvent enum (Action or Reaction) and a bool instead of the string pair; expected values unchanged
- src/combat/formulas.test.ts › reactionSuccess → 0.5 segments → calculateEnergyGain takes an EnergyEvent enum (Action or Reaction) and a bool instead of the string pair; expected values unchanged
- src/combat/formulas.test.ts › reactionFailure → 0.25 segments → calculateEnergyGain takes an EnergyEvent enum (Action or Reaction) and a bool instead of the string pair; expected values unchanged
- src/combat/formulas.test.ts › actionSuccess → 1.0 * 1.25 = 1.25 segments → calculateEnergyGain takes an EnergyEvent enum (Action or Reaction) and a bool instead of the string pair; expected values unchanged
- src/combat/formulas.test.ts › actionFailure → 0.5 * 1.25 = 0.625 segments → calculateEnergyGain takes an EnergyEvent enum (Action or Reaction) and a bool instead of the string pair; expected values unchanged
- src/combat/formulas.test.ts › reactionSuccess → 0.5 * 1.25 = 0.625 segments → calculateEnergyGain takes an EnergyEvent enum (Action or Reaction) and a bool instead of the string pair; expected values unchanged
- src/combat/formulas.test.ts › reactionFailure → 0.25 * 1.25 = 0.3125 segments → calculateEnergyGain takes an EnergyEvent enum (Action or Reaction) and a bool instead of the string pair; expected values unchanged
- src/combat/formulas.test.ts › actionSuccess → 1.0 * 1.50 = 1.5 segments → calculateEnergyGain takes an EnergyEvent enum (Action or Reaction) and a bool instead of the string pair; expected values unchanged
- src/combat/formulas.test.ts › actionFailure → 0.5 * 1.50 = 0.75 segments → calculateEnergyGain takes an EnergyEvent enum (Action or Reaction) and a bool instead of the string pair; expected values unchanged
- src/combat/formulas.test.ts › reactionSuccess → 0.5 * 1.50 = 0.75 segments → calculateEnergyGain takes an EnergyEvent enum (Action or Reaction) and a bool instead of the string pair; expected values unchanged
- src/combat/formulas.test.ts › reactionFailure → 0.25 * 1.50 = 0.375 segments → calculateEnergyGain takes an EnergyEvent enum (Action or Reaction) and a bool instead of the string pair; expected values unchanged
- src/combat/formulas.test.ts › returns base stats unchanged when no buffs or debuffs → applyDynamicModifiers: a Buff has no duration and a debuff is a buff with a negative modifier (no separate debuff list); expected values unchanged
- src/combat/formulas.test.ts › applies a single buff to the correct stat → applyDynamicModifiers: a Buff has no duration and a debuff is a buff with a negative modifier (no separate debuff list); expected values unchanged
- src/combat/formulas.test.ts › applies a single debuff reducing the correct stat → applyDynamicModifiers: a Buff has no duration and a debuff is a buff with a negative modifier (no separate debuff list); expected values unchanged
- src/combat/formulas.test.ts › stacks multiple buffs on the same stat additively → applyDynamicModifiers: a Buff has no duration and a debuff is a buff with a negative modifier (no separate debuff list); expected values unchanged
- src/combat/formulas.test.ts › applies buffs and debuffs independently without interference → applyDynamicModifiers: a Buff has no duration and a debuff is a buff with a negative modifier (no separate debuff list); expected values unchanged
- src/combat/formulas.test.ts › returns a new object — does not mutate baseStats → applyDynamicModifiers: a Buff has no duration and a debuff is a buff with a negative modifier (no separate debuff list); expected values unchanged
- src/combat/elementalPaths.test.ts › contains all 6 elemental paths → same assertions, written with BuffStat enum names, ElementalPaths.Config, and buffs without a duration
- src/combat/elementalPaths.test.ts › classifies Fire, Air, Light as reaction paths → same assertions, written with BuffStat enum names, ElementalPaths.Config, and buffs without a duration
- src/combat/elementalPaths.test.ts › classifies Water, Earth, Shadow as action paths → same assertions, written with BuffStat enum names, ElementalPaths.Config, and buffs without a duration
- src/combat/elementalPaths.test.ts › assigns correct defenseBoost per path → same assertions, written with BuffStat enum names, ElementalPaths.Config, and buffs without a duration
- src/combat/elementalPaths.test.ts › assigns correct specialForces per path (matches defenseBoost) → same assertions, written with BuffStat enum names, ElementalPaths.Config, and buffs without a duration
- src/combat/elementalPaths.test.ts › Fire path adds a parrySR_boost buff to the combatant → same assertions, written with BuffStat enum names, ElementalPaths.Config, and buffs without a duration
- src/combat/elementalPaths.test.ts › Air path adds a dodgeSR_boost buff to the combatant → same assertions, written with BuffStat enum names, ElementalPaths.Config, and buffs without a duration
- src/combat/elementalPaths.test.ts › Light path adds a blockSR_boost buff to the combatant → same assertions, written with BuffStat enum names, ElementalPaths.Config, and buffs without a duration
- src/combat/elementalPaths.test.ts › buff modifier is positive (SR increase) → same assertions, written with BuffStat enum names, ElementalPaths.Config, and buffs without a duration
- src/combat/elementalPaths.test.ts › does not mutate the original combatant → same assertions, written with BuffStat enum names, ElementalPaths.Config, and buffs without a duration
- src/combat/elementalPaths.test.ts › returns a new combatant reference → same assertions, written with BuffStat enum names, ElementalPaths.Config, and buffs without a duration
- src/combat/elementalPaths.test.ts › appends buff without removing existing buffs → same assertions, written with BuffStat enum names, ElementalPaths.Config, and buffs without a duration
- src/combat/elementalPaths.test.ts › is idempotent — re-applying the same path buff does not stack (ADR-053) → same assertions, written with BuffStat enum names, ElementalPaths.Config, and buffs without a duration
- src/combat/elementalPaths.test.ts › Water path adds a dodgeSR debuff to the target → same assertions, written with BuffStat enum names, ElementalPaths.Config, and buffs without a duration
- src/combat/elementalPaths.test.ts › Earth path adds a blockSR debuff to the target → same assertions, written with BuffStat enum names, ElementalPaths.Config, and buffs without a duration
- src/combat/elementalPaths.test.ts › Shadow path adds a parrySR debuff to the target → same assertions, written with BuffStat enum names, ElementalPaths.Config, and buffs without a duration
- src/combat/elementalPaths.test.ts › debuff modifier is negative (SR reduction) → same assertions, written with BuffStat enum names, ElementalPaths.Config, and buffs without a duration
- src/combat/elementalPaths.test.ts › does not mutate the original target → same assertions, written with BuffStat enum names, ElementalPaths.Config, and buffs without a duration
- src/combat/elementalPaths.test.ts › appends debuff without removing existing buffs → same assertions, written with BuffStat enum names, ElementalPaths.Config, and buffs without a duration
- src/combat/elementalPaths.test.ts › is idempotent — re-applying the same path debuff does not stack (ADR-053) → same assertions, written with BuffStat enum names, ElementalPaths.Config, and buffs without a duration
- src/combat/elementalPaths.test.ts › Fire forces parry defense → same assertions, written with BuffStat enum names, ElementalPaths.Config, and buffs without a duration
- src/combat/elementalPaths.test.ts › Water forces dodge defense → same assertions, written with BuffStat enum names, ElementalPaths.Config, and buffs without a duration
- src/combat/elementalPaths.test.ts › Air forces dodge defense → same assertions, written with BuffStat enum names, ElementalPaths.Config, and buffs without a duration
- src/combat/elementalPaths.test.ts › Earth forces block defense → same assertions, written with BuffStat enum names, ElementalPaths.Config, and buffs without a duration
- src/combat/elementalPaths.test.ts › Shadow forces parry defense → same assertions, written with BuffStat enum names, ElementalPaths.Config, and buffs without a duration
- src/combat/elementalPaths.test.ts › Light forces block defense → same assertions, written with BuffStat enum names, ElementalPaths.Config, and buffs without a duration
- src/combat/elementalPaths.test.ts › returns a valid DefenseType for every path → same assertions, written with BuffStat enum names, ElementalPaths.Config, and buffs without a duration
- src/combat/elementalPaths.test.ts › applying Fire buff produces a positive modifier (self-enhancement) → same assertions, written with BuffStat enum names, ElementalPaths.Config, and buffs without a duration
- src/combat/elementalPaths.test.ts › applying Water debuff produces a negative modifier (target reduction) → same assertions, written with BuffStat enum names, ElementalPaths.Config, and buffs without a duration
- src/combat/elementalPaths.test.ts › reaction path buff targets the correct defense type (Fire → parry) → same assertions, written with BuffStat enum names, ElementalPaths.Config, and buffs without a duration
- src/combat/elementalPaths.test.ts › action path debuff targets the correct defense type (Earth → block) → same assertions, written with BuffStat enum names, ElementalPaths.Config, and buffs without a duration
- src/combat/defense.test.ts › always returns success = false → resolveDefenseless is reached through ResolveDefense with a defenseless target; same assertion

formulas.test.ts carried over 87 tests (the lists above account for the ones whose calls changed), energy.test.ts 28, elementalPaths.test.ts 32 (all with enum names), defense.test.ts 29 plus the one above, reactionProgression.test.ts 18 unchanged. Where a test used toBeCloseTo(x, n) it is Assert.Equal(x, actual, n); with no precision it is 2 decimals.

### Tests dropped

- src/combat/energy.test.ts › level 0 → 0 starting segments → unported dead code (no callers in the game; T5) (getStartingSegments, resetRoundEnergy, and the cross-system block that calls both)
- src/combat/energy.test.ts › level 1 → 0 starting segments → unported dead code (no callers in the game; T5) (getStartingSegments, resetRoundEnergy, and the cross-system block that calls both)
- src/combat/energy.test.ts › level 2 → 1 starting segment → unported dead code (no callers in the game; T5) (getStartingSegments, resetRoundEnergy, and the cross-system block that calls both)
- src/combat/energy.test.ts › level 3 → 2 starting segments → unported dead code (no callers in the game; T5) (getStartingSegments, resetRoundEnergy, and the cross-system block that calls both)
- src/combat/energy.test.ts › level 0 combatant resets to 0 energy → unported dead code (no callers in the game; T5) (getStartingSegments, resetRoundEnergy, and the cross-system block that calls both)
- src/combat/energy.test.ts › level 1 combatant resets to 0 energy → unported dead code (no callers in the game; T5) (getStartingSegments, resetRoundEnergy, and the cross-system block that calls both)
- src/combat/energy.test.ts › level 2 combatant resets to 1 energy segment → unported dead code (no callers in the game; T5) (getStartingSegments, resetRoundEnergy, and the cross-system block that calls both)
- src/combat/energy.test.ts › level 3 combatant resets to 2 energy segments → unported dead code (no callers in the game; T5) (getStartingSegments, resetRoundEnergy, and the cross-system block that calls both)
- src/combat/energy.test.ts › preserves all other combatant fields on reset → unported dead code (no callers in the game; T5) (getStartingSegments, resetRoundEnergy, and the cross-system block that calls both)
- src/combat/energy.test.ts › level ${level} → ${expectedStart} starting segments on reset → unported dead code (no callers in the game; T5) (getStartingSegments, resetRoundEnergy, and the cross-system block that calls both)

### Set aside

- Every combat balance finding for this system: see "Combat balance review (combat sprint)". (for: combat sprint)
- A tampered save can supply an undefined AscensionLevel, near-int.MaxValue XP, or NaN stats that propagate as in the TypeScript; the save checks reject them (see Saves), and combat itself does not re-check (security advisories). (for: combat sprint)

## Enemy decisions

### Deliberate differences

- Decision profiles become content, as archetype records keyed by archetype id; characters and encounter combatants point to an archetype; the Elena, Lars, and Kade profiles move to the test content. One archetype record will also carry Sprint 7 reaction leanings. Why: ADR-062. (2026-10-28 (T6))
- An explicit Basic behavior replaces the silent fallback; unknown archetypes and missing weights are refused at content load; the catch-all is removed. Why: Today every enemy in the demo encounter (archetype "enemy") and two misspelled test archetypes (kade_bold_outlaw, lars_cautious_merchant) silently use the stub, and any evaluator bug is hidden the same way. Declaring those archetypes Basic in the test content keeps every fight identical. Evaluating an unknown archetype now throws InvalidOperationException. (2026-10-28 (T6))
- Basic behavior attacks the first living opponent. The TypeScript stub attacked the first living player-party member regardless of side. Why: Differs only for player-side actors, which are never AI-decided in play. (2026-10-28 (T6, build))
- Tie-breaks use the combatant own elemental path; profiles no longer carry a path. Why: One source of truth. Changes behavior only when the two disagree, for example encounter_test enemy-side Elena (combatant path Earth, profile path Light). (2026-10-28 (T6))
- Extra content checks: non-finite scores and weights, empty or duplicate archetype ids, a Scored archetype with a null profile are refused. Unknown extra weight names are not refused. Why: Added during the build; candidates are still generated in the TypeScript order (Attack, Defend, Evade, Special, Group). (2026-10-28 (T6, build))

### Tests changed

- src/combat/behaviorTree/profiles/profiles.test.ts › %s has all 5 action types in baseScores → the it.each over Elena, Lars, and Kade is three C# tests with the name substituted (for example "Elena has all 5 action types in baseScores"), run against the test archetypes
- src/combat/behaviorTree/profiles/profiles.test.ts › %s has weights for all 7 factors → the it.each over Elena, Lars, and Kade is three C# tests with the name substituted (for example "Elena has all 5 action types in baseScores"), run against the test archetypes
- src/combat/behaviorTree/factors/factors.test.ts › all factors implement evaluate() → the typeof evaluate check is meaningless in C#; each factor Evaluate is called instead
- src/combat/behaviorTree/evaluator.test.ts › throws for unknown archetype ID → the evaluator throws InvalidOperationException for an archetype missing from the catalog

perception 18, rankCoefficient 9, tieBreaking 14, evaluator 22 (one changed above), factors 45 (one changed above), profiles 24 (30 as run) carried over. New: 25 tests (ArchetypeCatalog 16, Basic behavior 6, unknown archetype 3). tieBreaking tests pass the path directly, so no expected value changed.

### Tests dropped

- src/combat/behaviorTree/profiles/profiles.test.ts › %s has a valid elementalPath → removed hard-coded data: the profile no longer carries an elemental path (tie-breaks use the combatant own)
- src/combat/behaviorTree/profiles/profiles.test.ts › has Light path → removed hard-coded data: the profile no longer carries an elemental path (tie-breaks use the combatant own)
- src/combat/behaviorTree/profiles/profiles.test.ts › has Earth path → removed hard-coded data: the profile no longer carries an elemental path (tie-breaks use the combatant own)
- src/combat/behaviorTree/profiles/profiles.test.ts › has Fire path → removed hard-coded data: the profile no longer carries an elemental path (tie-breaks use the combatant own)

### Set aside

- AI tuning findings: see "Combat balance review (combat sprint)". (for: combat sprint)
- Finite-but-huge scores or weights can overflow to a NaN top score, so tie-breaking throws and the session reports INTERNAL_ERROR. Bound content values in the balance review (security review, not acted on). (for: combat sprint)

## Round resolution

### Deliberate differences

- Speed ties are broken by one roll per tied action, highest first, queue order on equal rolls. Why: Today `rollFn() - rollFn()` inside the sort comparator depends on V8 sort call sequence and cannot be reproduced. Constant-roll tests give today results unchanged. (2026-10-28 (T7))
- One order per fighter per round (duplicate message: "Combatant 'x' already has an order this round"; duplicates are counted on accepted orders only; the last GROUP leader override wins, as in the TypeScript). Why: Today two Specials from one fighter both spend energy and both get the boost, and a GROUP leader can also attack. (2026-10-28 (T7))
- Enemies do not choose GROUP until the balance review designs enemy group attacks. Why: Today an enemy GROUP has no target, does nothing, and drains its team energy, skipping the energy gate. (2026-10-28 (T7))
- A knocked-out guard no longer intercepts; GROUP blocks use buffed rates. Why: Today a KO'd defender absorbs the attack, and GROUP alone ignores path buffs. (2026-10-28 (T7))
- Round records keep the end-of-round state without earlier history, record counter exchanges (CounterExchange) and Evade recovery (stamina actually restored after the cap, null if KO). Why: Today each record embeds every earlier record, so a fight record roughly doubles in size each round and a long fight could not be saved. Combat screens need the missing events. No outcome changes. Verified: a 30-round fight is under a quarter of the 2 MB limit with linear growth. (2026-10-28 (T7))
- Crushing Blow uses action power. Why: Match the combat spreadsheet (see Combat math). (2026-10-28 (T7))
- The snapshot dice are the advanced end-of-round dice; visual info (buildVisualInfo) and the stub behavior tree are not ported. Why: Required for exact replay and same-dice reload; visual info had no use outside the retired renderer. (2026-10-28 (T7, build))
- An existing SPECIAL damage test now also triggers Crushing Blow because of the action-power rule; the stamina result is unchanged. Why: Consequence of the action-power decision. (2026-10-28 (T16))

### Tests changed

- src/combat/pipeline.test.ts › speed ties are broken by random factor → tie rule: each tied action draws one roll in queue order, highest first; rolls 15 then 5 now put a ahead of b (the TypeScript asserted only the length)
- src/combat/pipeline.test.ts › returns a new array without mutating the input → the result is an ImmutableArray
- src/combat/counterChain.test.ts › all actions in the chain have counterChain = true → a CounterExchange has no counterChain flag; every entry in Exchanges is a counter-attack by definition, so the test checks Parry and ChainLength
- src/combat/counterChain.test.ts › all actions have rankKO, blindside, crushingBlow = false (not applicable in counter chain) → an exchange has no such flags; the chain draws exactly one roll per exchange (the defense roll), so no rank KO, blindside, or Crushing Blow roll can happen
- src/combat/roundManager.test.ts › AI stub targets the first non-KO'd player combatant → stubBehaviorTree is retired; the test calls EnemyDecisions.Decide with the Basic archetype
- src/combat/roundManager.test.ts › AI stub produces ATTACK type action → stubBehaviorTree is retired; the test calls EnemyDecisions.Decide with the Basic archetype
- src/combat/integration.test.ts › counter chain terminates and does not cause infinite loops → the timing assertion uses a Stopwatch

declaration 32 of 34, counterChain 11 of 13 unchanged plus the two above, groupAction 24 of 24, pipeline 38 of 40 unchanged plus the two above, roundManager 25 of 33 unchanged plus two changed and six dropped, integration 15 of 16 unchanged plus one changed. New tests: declaration +6, counterChain +3, groupAction +4 (buffed block rates), pipeline +18, roundManager +12, round-history size +4 (linear growth).

### Tests dropped

- src/combat/declaration.test.ts › returns the combatant maxEnergy field directly → unported dead code (no callers in the game; T5) (maxEnergyForAscensionLevel only returned the maxEnergy field)
- src/combat/declaration.test.ts › returns 3 for a combatant with maxEnergy: 3 → unported dead code (no callers in the game; T5) (maxEnergyForAscensionLevel only returned the maxEnergy field)
- src/combat/roundManager.test.ts › VisualInfo includes all combatants → unported dead code (no callers in the game; T5) (buildVisualInfo: visual info is not ported)
- src/combat/roundManager.test.ts › VisualInfo includes stamina and staminaPct → unported dead code (no callers in the game; T5) (buildVisualInfo: visual info is not ported)
- src/combat/roundManager.test.ts › VisualInfo shows stance as "active" for living combatants → unported dead code (no callers in the game; T5) (buildVisualInfo: visual info is not ported)
- src/combat/roundManager.test.ts › VisualInfo shows stance as "KO" for KO'd combatants → unported dead code (no callers in the game; T5) (buildVisualInfo: visual info is not ported)
- src/combat/roundManager.test.ts › VisualInfo shows player targeting from declarations → unported dead code (no callers in the game; T5) (buildVisualInfo: visual info is not ported)
- src/combat/roundManager.test.ts › VisualInfo does NOT include AI targeting (null for enemies) → unported dead code (no callers in the game; T5) (buildVisualInfo: visual info is not ported)

### Set aside

- Enemy group attacks, and every other combat finding: see "Combat balance review (combat sprint)". (for: combat sprint)
- Declarations do not check energy range or party membership inside RunRound (only in the session operations); safe while nothing changes energy between declaring and advancing (security review, not acted on). (for: combat sprint)

## Fight setup

### Deliberate differences

- The player side is the player plus the chosen lineup, with combat stats from content; encounters list only enemies; a fight needs a full lineup (TEAM_COMPOSITION_INVALID "Choose a full lineup before starting a fight"). Why: Today the player side comes entirely from the encounter file and the lineup has no effect. Content can omit the player combat stats and use a lineup of 3 to reproduce today companion-only fights, so whether the player character fights stays a content decision (ADR-062). (2026-10-28 (T8))
- Reaction progress is keyed by character id (or the player id). Why: Fighters are characters; XP now follows a character across fights instead of an encounter-specific id such as player_elena. (2026-10-28 (T8))
- Encounters are content; encounter_demo and encounter_test move to the test content. Why: ADR-062. (2026-10-28 (T8))
- Enemy stats, skills, ids, and names are validated at encounter load (the TypeScript had no checks); an encounter needs at least one enemy and an id of at most 64 characters; duplicate fighter ids across player, lineup, and enemies are refused ("Duplicate fighter id: {id}"). Why: Security review during the build (hand-edited saves and content could collide ids or carry non-finite stats). (2026-10-28 (T8, build))
- Synergy is applied last, to the player side only; stats it does not modify are not rounded. Synergy on MaxStamina sets both MaxStamina and Stamina (the fight starts full). A synergy that makes a stat negative or non-finite is refused ("Fight setup gave {id} an invalid {stat}"), with no clamp. Why: No TypeScript precedent for the stamina case; clamping is a balance decision. (2026-10-28 (T8, build))
- Stamina still resets between fights. Why: Set aside for the combat balance review. (2026-10-28 (T8))
- Refusals keep the TypeScript messages: ENCOUNTER_NOT_FOUND "Encounter '{id}' not found in registry", COMBAT_ALREADY_ACTIVE "A combat encounter is already active". Why: Parity. (2026-10-28 (T8))

### Tests changed

- src/combat/sync.test.ts › maps playerParty from encounter config (1 player) → rewritten for the player plus lineup side: the player id and name come from the game, a lineup of two makes the 3-strong side, the player has no archetype; fighter ids are character ids
- src/combat/sync.test.ts › correctly maps 3v3 encounter with all combatants → rewritten for the player plus lineup side: the player id and name come from the game, a lineup of two makes the 3-strong side, the player has no archetype; fighter ids are character ids
- src/combat/sync.test.ts › preserves combatant stats from config (rank, power, speed, archetype) → rewritten for the player plus lineup side: the player id and name come from the game, a lineup of two makes the 3-strong side, the player has no archetype; fighter ids are character ids
- src/combat/sync.test.ts › preserves player id and name unchanged → rewritten for the player plus lineup side: the player id and name come from the game, a lineup of two makes the 3-strong side, the player has no archetype; fighter ids are character ids
- src/combat/sync.test.ts › applies no synergy boost when NPCs are empty → rebuilt on content synergy settings and a lineup ("no NPCs" is a lineup of zero); expected values copied from the TypeScript
- src/combat/sync.test.ts › produces same CombatState as before when personality does not meet synergy threshold → rebuilt on content synergy settings and a lineup ("no NPCs" is a lineup of zero); expected values copied from the TypeScript
- src/combat/sync.test.ts › applies synergy power boost (Well Rounded) when personality threshold is met → rebuilt on content synergy settings and a lineup ("no NPCs" is a lineup of zero); expected values copied from the TypeScript
- src/combat/sync.test.ts › does not apply synergy boost to enemy party → rebuilt on content synergy settings and a lineup ("no NPCs" is a lineup of zero); expected values copied from the TypeScript
- src/combat/sync.test.ts › uses Math.round when applying synergy multiplier → rebuilt on content synergy settings and a lineup ("no NPCs" is a lineup of zero); expected values copied from the TypeScript

sync.test.ts: 30 of 42 carried over, with the fixtures built from the test content (encounter_demo, encounter_test, and the lineup variants that reproduce each TypeScript encounter player party). Fight-setup parity was checked by running the TypeScript initCombatState on a new game: values match except fighter ids and names. New: 42 FightSetup tests (refusals, reaction progress by id, enemy ascension, demo-fight parity, EncounterCatalog validation).

### Tests dropped

- src/combat/sync.test.ts › preserves conversationLog unchanged → retired dialogue engine (Sprint 7 brings its own conversation state) (GameState no longer has those fields; the endCombat log entry is retired)
- src/combat/sync.test.ts › preserves currentDialogueNode unchanged → retired dialogue engine (Sprint 7 brings its own conversation state) (GameState no longer has those fields; the endCombat log entry is retired)
- src/combat/sync.test.ts › preserves saveSlot unchanged → retired dialogue engine (Sprint 7 brings its own conversation state) (GameState no longer has those fields; the endCombat log entry is retired)
- src/combat/sync.test.ts › records the result in conversationLog → retired dialogue engine (Sprint 7 brings its own conversation state) (GameState no longer has those fields; the endCombat log entry is retired)
- src/combat/sync.test.ts › records defeat result in conversationLog → retired dialogue engine (Sprint 7 brings its own conversation state) (GameState no longer has those fields; the endCombat log entry is retired)
- src/combat/sync.test.ts › applies no synergy boost when player personality is missing (guard) → a check a typed API makes impossible: a player always has a personality

### Set aside

- Stamina resets between fights; see "Combat balance review (combat sprint)". (for: combat sprint)
- Whether the player character fights alongside the companions and the lineup size: see "Open creative questions". (for: creative work, then Sprint 7 content)

## Game session

### Deliberate differences

- ADR-057 session is kept: single entry point, results as values, one immutable snapshot, one notification per change. Why: Reconfirmed after the 2026-10-28 research (suits a turn-based rules engine: exact replay, cheap tests). (2026-10-28 (T9))
- A throwing listener is isolated and reported through a hook (OnListenerError). Notifications are queued and delivered in commit order, a runaway re-entrant loop is bounded and reported through the same hook, and a listener removed mid-pass is not called. Why: Today a throwing listener makes the operation throw after the snapshot has already changed; re-entrant changes were delivered out of order or recursed without bound. Security review during the build. (2026-10-28 (T9, build))
- Character list and lookup both read the live game, or content starting values without a game. Why: Today listNpcs ignores the live game while getNpc prefers it, contrary to ADR-042 intent. (2026-10-28 (T9))
- Snapshots carry a version counter and the current synergy. Why: Screens can detect change cheaply, and the bonus is always visible for the current lineup. (2026-10-28 (T9))
- One 64-character limit on every text id. Why: Today the limits are 64, 128, or none depending on the operation. (2026-10-28 (T9))
- Scene and dialogue operations are not ported; the synergy operation stays (GetSynergy with no game is GAME_NOT_FOUND, as before). The narrative-scene lineup refusal is dropped. Why: Retired engines; synergy now reports the player plus lineup. (2026-10-28 (T9))
- Error codes keep today names (TEAM_COMPOSITION_INVALID, NPC_NOT_FOUND) while C# operation names say Lineup and Character. SetLineup returns the lineup ids (the TypeScript returned {team}). INTERNAL_ERROR carries "An unexpected error occurred". Why: Same error codes as today is an acceptance criterion; names in code follow the new concepts. (2026-10-28 (T9))
- Personality adjustment input checks run in a fixed order (null, fewer than one delta, unknown key, non-finite in trait order), and a new game player id is "player_" plus the id source id. Why: A dictionary has no insertion order; the TypeScript used player_${uuid}. (2026-10-28 (T9, build))
- Reloading a mid-fight save replays the same dice: each fight dice live in its CombatState and are saved with it. Why: Designer: "same dice" stops reload-to-reroll and lets any fight be replayed exactly. Today live fights use Math.random and cannot be replayed. (2026-10-28 (T10))
- One order per fighter per round is enforced at declaration, GROUP ally overrides are applied at declaration (the pending-orders view shows allies orders removed; the TypeScript did it inside the round), an all-invalid non-empty declaration is COMBAT_INVALID_DECLARATION "All declarations were invalid" and keeps the old orders, an empty list clears orders, and at most 10 orders are accepted. Why: Designer decision for the one-order rule (Group 4); the rest came from the safety review of round resolution. (2026-10-28 (T10))
- Declaration safety refusals: non-finite energy segments (VALIDATION_ERROR "actions/i/energySegments must be number"), segments that are zero, negative, fractional, or above the fighter energy ("energySegments must be an integer between 1 and N"), segments on a non-SPECIAL order, null lists or elements, an enemy actor ("Combatant 'x' is not in the player's party"), ids over 64 characters, and a non-active fight (NO_ACTIVE_COMBAT). Why: Security review during the build: NaN segments make a target unkillable and negative segments heal. (2026-10-28 (T10, build))
- The shape message prefix is "actions/..." where the TypeScript said "body/actions/...", empty ids are not refused at shape level (they are rejected as not in the party), and targetId is checked as an id (stricter than the TypeScript). Why: The session is no longer an HTTP-style body. (2026-10-28 (T10, build))
- Personality adjustment messages drop the TypeScript "body" wording ("{trait} must be number", "adjustment must be object", "adjustment must NOT have fewer than 1 properties", "adjustment must NOT have additional properties"); error codes are unchanged. Why: This matches the combat shape messages, and the session is no longer an HTTP-style body. (2026-10-28 (verify))
- A loaded save commits a fresh snapshot (pending orders and last outcome cleared, version +1) and notifies once; save, list, and delete never commit. Why: Same behavior as the TypeScript for load; the version counter is new. (2026-10-28 (T12))

### Tests changed

- src/session/gameSession.test.ts › replaces the whole game, including dialogue position and narrative → dialogue position and narrative are retired; the whole game is still replaced (lineup, personality, relationships)
- src/session/gameSession.test.ts › turns an unexpected throw into INTERNAL_ERROR with state unchanged and no notification → the C# RuleError has no cause; a throwing seed source stands in for the null encounter parties
- src/session/validation.test.ts › rejects non-object %j → a typed API cannot take a string, number, array, or prototype key: only null and non-finite numbers remain, and an unknown trait is a Trait value outside the enum
- src/session/validation.test.ts › rejects non-finite-number value %j → a typed API cannot take a string, number, array, or prototype key: only null and non-finite numbers remain, and an unknown trait is a Trait value outside the enum; the message is now "patience must be number" (was "body/patience must be number")
- src/session/validation.test.ts › rejects unknown traits, including prototype names → a typed API cannot take a string, number, array, or prototype key: only null and non-finite numbers remain, and an unknown trait is a Trait value outside the enum
- src/session/validation.test.ts › accepts a string with no limit, or within the limit → the limit is fixed at 64 characters (no limit argument); null stands in for "not a string"
- src/session/validation.test.ts › rejects non-strings and strings over the limit → the limit is fixed at 64 characters (no limit argument); null stands in for "not a string"
- src/session/validation.test.ts › rejects a non-array → a typed API makes most bad shapes impossible (a default array is the only non-array, a null element the only non-object, the action type is an enum, a null entry stands in for a non-string); the ids, the non-finite energy, and the 64-character limit remain
- src/session/validation.test.ts › rejects non-object items → a typed API makes most bad shapes impossible (a default array is the only non-array, a null element the only non-object, the action type is an enum, a null entry stands in for a non-string); the ids, the non-finite energy, and the 64-character limit remain
- src/session/validation.test.ts › requires combatantId and type → a typed API makes most bad shapes impossible (a default array is the only non-array, a null element the only non-object, the action type is an enum, a null entry stands in for a non-string); the ids, the non-finite energy, and the 64-character limit remain
- src/session/validation.test.ts › rejects extra keys and wrongly typed fields → a typed API makes most bad shapes impossible (a default array is the only non-array, a null element the only non-object, the action type is an enum, a null entry stands in for a non-string); the ids, the non-finite energy, and the 64-character limit remain
- src/session/validation.test.ts › rejects non-string and over-long entries → a typed API makes most bad shapes impossible (a default array is the only non-array, a null element the only non-object, the action type is an enum, a null entry stands in for a non-string); the ids, the non-finite energy, and the 64-character limit remain
- src/session/combat.test.ts › refuses malformed declarations → a null list stands in for the non-array
- src/session/operations/combat.test.ts › refuses a non-string id with VALIDATION_ERROR → a null id stands in for the non-string 42 and a null combatant id for the missing type
- src/session/operations/combat.test.ts › refuses malformed input with VALIDATION_ERROR and keeps orders → a null id stands in for the non-string 42 and a null combatant id for the missing type
- src/session/operations/combat.test.ts › refuses energySegments %s and keeps previous orders → the it.each over 0, 3, -1, 1.5 is one C# test per value
- src/session/operations/player.test.ts › refuses invalid input without changing or notifying → the bad inputs are the ones the typed API can express (no string, array, or non-number value)
- src/session/operations/protoKeys.test.ts › getNpc refuses a non-string id → a null id stands in for a non-string id
- src/session/operations/protoKeys.test.ts › setTeam refuses prototype keys and leaves the snapshot unchanged → prototype names are simply unknown ids in C#; the refusal is the same
- src/session/operations/protoKeys.test.ts › getNpc('${key}') is NPC_NOT_FOUND ${withGame ? 'with' : 'without'} a game → the 3 keys with and without a game are 8 named C# cases ("constructor" and similar ids are unknown ids, refused with the same message)
- tests/session.sprint1.test.ts › getNpc(npc_scout_elena) returns Elena with correct archetype → the archetype is an archetype id (for example elena_loyal_scout), not the display name
- tests/session.sprint1.test.ts › getNpc(npc_merchant_lars) returns Lars with correct archetype → the archetype is an archetype id (for example elena_loyal_scout), not the display name
- tests/session.sprint1.test.ts › getNpc(npc_outlaw_kade) returns Kade with correct archetype → the archetype is an archetype id (for example elena_loyal_scout), not the display name
- tests/session.sprint1.test.ts › combatState remains null in game state after dialogue choice → becomes "after a lineup is set" (dialogue is retired)
- tests/session.sprint4.test.ts › personality has 6 traits summing to ~100 → the typeof number checks are meaningless for typed doubles; each trait is checked to be in the 5 to 35 range instead
- tests/session.sprint4.test.ts › refuses a wrong number of NPCs — validation rejects a team that is not exactly 2 → the lineup size comes from content; the refusal is the same VALIDATION_ERROR
- tests/session.sprint4.test.ts › returns live NPC data when game is active → with no way to change a relationship through the session, the live values equal the content starting values

Carried over by name: session/combat 8 (1 changed), gameSession 11 (2 changed), validation 18 (11 changed above), operations/combat 31 (3 changed and one it.each), operations/player 10 of 11 (the narrative lock dropped), protoKeys 3 (as 8 cases), session.combat-e2e 26 (rank KO, blindside, ascension, and defense assertions adapted to typed results; fighter ids map player_warrior to Hero, player_scout to Elena, player_mage to Lars, and for the demo player_elena to Hero, player_lars to Elena, player_kade to Lars; the lineup must be set before StartEncounter), session.sprint4 10 of 11, session.sprint1 (see below). Several titles in the dropped dialogue and narrative files (for example "refuses without a game", "refuses a non-string id with VALIDATION_ERROR") are shared with carried-over tests in other files, so they are matched by name; the dropped instances are listed anyway. T17 counted 3 save tests left in session.sprint1 for T18; reconciled from the file: none remain there (its header says the save tests moved to session.saves.test.ts, which T18 carries over), and session.sprint1 has 44 distinct titles of which 14 are dropped (dialogue) and 30 carried over by name. New: one order per fighter, shape rules, list copied, no lineup, seed determinism, version bumps, synergy snapshot, id and clock, INTERNAL_ERROR through the public API, listener isolation and queue order and runaway and dispose, whole-fight end-to-end with exact replay from seed and orders, a different seed gives a different fight (fragile: relies on round counts differing).

### Tests dropped

- src/session/operations/player.test.ts › is locked during a narrative scene, and unlocks when the narrative is reset → retired narrative engine (Sprint 7 brings its own conversation state)
- tests/session.sprint4.test.ts › refuses when narrative is active → retired narrative engine (Sprint 7 brings its own conversation state)
- src/session/narrativeDialogue.test.ts › has no dead-end nodes → retired narrative engine (Sprint 7 brings its own conversation state); the prototype-key id check lives on in protoKeys
- src/session/narrativeDialogue.test.ts › refuses an unknown scene and leaves the game unchanged → retired narrative engine (Sprint 7 brings its own conversation state); the prototype-key id check lives on in protoKeys
- src/session/narrativeDialogue.test.ts › starts a scene and makes a choice → retired narrative engine (Sprint 7 brings its own conversation state); the prototype-key id check lives on in protoKeys
- src/session/narrativeDialogue.test.ts › runs a placeholder conversation and ends it → retired narrative engine (Sprint 7 brings its own conversation state); the prototype-key id check lives on in protoKeys
- src/session/narrativeDialogue.test.ts › refuses %s as NPC_NOT_FOUND → retired narrative engine (Sprint 7 brings its own conversation state); the prototype-key id check lives on in protoKeys
- src/session/operations/dialogue.test.ts › passes validateDialogueTree for every default tree → retired dialogue engine (Sprint 7 brings its own conversation state)
- src/session/operations/dialogue.test.ts › gives every NPC a <npcId>_greet node with an opt_farewell option → retired dialogue engine (Sprint 7 brings its own conversation state)
- src/session/operations/dialogue.test.ts › refuses a non-string id with VALIDATION_ERROR → retired dialogue engine (Sprint 7 brings its own conversation state)
- src/session/operations/dialogue.test.ts › refuses without a game, for an unknown NPC, and for an NPC with no greet node → retired dialogue engine (Sprint 7 brings its own conversation state)
- src/session/operations/dialogue.test.ts › records the position and lists only the options the player may take → retired dialogue engine (Sprint 7 brings its own conversation state)
- src/session/operations/dialogue.test.ts › restarts the conversation from the greeting → retired dialogue engine (Sprint 7 brings its own conversation state)
- src/session/operations/dialogue.test.ts › refuses a non-string option id with VALIDATION_ERROR → retired dialogue engine (Sprint 7 brings its own conversation state)
- src/session/operations/dialogue.test.ts › refuses without a game → retired dialogue engine (Sprint 7 brings its own conversation state)
- src/session/operations/dialogue.test.ts › refuses with DIALOGUE_NOT_ACTIVE before a conversation starts, without changing state → retired dialogue engine (Sprint 7 brings its own conversation state)
- src/session/operations/dialogue.test.ts › moves through nodes taking only the option id, tracking the position → retired dialogue engine (Sprint 7 brings its own conversation state)
- src/session/operations/dialogue.test.ts › applies personality and NPC adjustments and logs the choice → retired dialogue engine (Sprint 7 brings its own conversation state)
- src/session/operations/dialogue.test.ts › ends the conversation, then refuses another choice with DIALOGUE_NOT_ACTIVE → retired dialogue engine (Sprint 7 brings its own conversation state)
- src/session/operations/dialogue.test.ts › refuses an unknown option and a gated option, keeping the position → retired dialogue engine (Sprint 7 brings its own conversation state)
- src/session/operations/dialogue.test.ts › opens a gated option once the personality meets the gate → retired dialogue engine (Sprint 7 brings its own conversation state)
- src/session/operations/dialogue.test.ts › ends the placeholder conversation with opt_farewell → retired dialogue engine (Sprint 7 brings its own conversation state)
- src/session/operations/narrative.test.ts › refuses without a game → retired narrative engine (Sprint 7 brings its own conversation state)
- src/session/operations/narrative.test.ts › refuses a non-string or over-long id with VALIDATION_ERROR → retired narrative engine (Sprint 7 brings its own conversation state)
- src/session/operations/narrative.test.ts › refuses an unknown scene and leaves state alone (approved fix) → retired narrative engine (Sprint 7 brings its own conversation state)
- src/session/operations/narrative.test.ts › starts a known scene and notifies once → retired narrative engine (Sprint 7 brings its own conversation state)
- src/session/operations/narrative.test.ts › accepts a scene from content injected into the session → retired narrative engine (Sprint 7 brings its own conversation state)
- src/session/operations/narrative.test.ts › refuse without a game → retired narrative engine (Sprint 7 brings its own conversation state)
- src/session/operations/narrative.test.ts › getCurrentScene refuses before a scene is started → retired narrative engine (Sprint 7 brings its own conversation state)
- src/session/operations/narrative.test.ts › getNarrativeState is null before a scene and set after → retired narrative engine (Sprint 7 brings its own conversation state)
- src/session/operations/narrative.test.ts › getCurrentScene lists the choices available to the player → retired narrative engine (Sprint 7 brings its own conversation state)
- src/session/operations/narrative.test.ts › refuses a non-string id with VALIDATION_ERROR → retired narrative engine (Sprint 7 brings its own conversation state)
- src/session/operations/narrative.test.ts › refuses before a scene is started → retired narrative engine (Sprint 7 brings its own conversation state)
- src/session/operations/narrative.test.ts › refuses an unknown choice and a gated choice, leaving state alone → retired narrative engine (Sprint 7 brings its own conversation state)
- src/session/operations/narrative.test.ts › applies the consequence and advances to the next scene → retired narrative engine (Sprint 7 brings its own conversation state)
- src/session/operations/narrative.test.ts › keeps the consequence and the advanced narrative state together, so a flag it sets opens a later scene → retired narrative engine (Sprint 7 brings its own conversation state)
- src/session/operations/narrative.test.ts › refuses a move into a scene whose prerequisites are unmet, leaving state alone → retired narrative engine (Sprint 7 brings its own conversation state)
- src/session/operations/narrative.test.ts › ends the narrative when the last choice has no next scene → retired narrative engine (Sprint 7 brings its own conversation state)
- src/session/operations/narrative.test.ts › resetNarrative clears the narrative state → retired narrative engine (Sprint 7 brings its own conversation state)
- src/session/operations/narrative.test.ts › getSynergy reads without changing the session → retired narrative engine (Sprint 7 brings its own conversation state)
- src/session/contentFreeze.test.ts › returned scene and dialogue node are frozen → a check a typed API makes impossible: records are immutable by type (content records are immutable, and a session shares no mutable content)
- src/session/contentFreeze.test.ts › encounter content is frozen, engine runs on it, originals stay unfrozen → a check a typed API makes impossible: records are immutable by type (content records are immutable, and a session shares no mutable content)
- src/session/contentFreeze.test.ts › mutating source objects after creation does not affect an existing session → a check a typed API makes impossible: records are immutable by type (content records are immutable, and a session shares no mutable content)
- src/session/contentFreeze.test.ts › two sessions do not share content → a check a typed API makes impossible: records are immutable by type (content records are immutable, and a session shares no mutable content)
- tests/session.sprint1.test.ts › chooseDialogueOption updates state via ungated option → retired dialogue engine (Sprint 7 brings its own conversation state) (startDialogue, chooseDialogueOption, dialogue gates, the placeholder greet node, conversation log)
- tests/session.sprint1.test.ts › dialogue choice appends to conversation log → retired dialogue engine (Sprint 7 brings its own conversation state) (startDialogue, chooseDialogueOption, dialogue gates, the placeholder greet node, conversation log)
- tests/session.sprint1.test.ts › startDialogue returns GAME_NOT_FOUND when no game exists → retired dialogue engine (Sprint 7 brings its own conversation state) (startDialogue, chooseDialogueOption, dialogue gates, the placeholder greet node, conversation log)
- tests/session.sprint1.test.ts › chooseDialogueOption returns GAME_NOT_FOUND when no game exists → retired dialogue engine (Sprint 7 brings its own conversation state) (startDialogue, chooseDialogueOption, dialogue gates, the placeholder greet node, conversation log)
- tests/session.sprint1.test.ts › startDialogue with unknown NPC returns NPC_NOT_FOUND → retired dialogue engine (Sprint 7 brings its own conversation state) (startDialogue, chooseDialogueOption, dialogue gates, the placeholder greet node, conversation log)
- tests/session.sprint1.test.ts › startDialogue with missing greet node in tree returns DIALOGUE_NODE_NOT_FOUND → retired dialogue engine (Sprint 7 brings its own conversation state) (startDialogue, chooseDialogueOption, dialogue gates, the placeholder greet node, conversation log)
- tests/session.sprint1.test.ts › chooseDialogueOption with gated option when gate fails returns DIALOGUE_OPTION_NOT_AVAILABLE → retired dialogue engine (Sprint 7 brings its own conversation state) (startDialogue, chooseDialogueOption, dialogue gates, the placeholder greet node, conversation log)
- tests/session.sprint1.test.ts › personality sums to 100% after dialogue choice with personality adjustment → retired dialogue engine (Sprint 7 brings its own conversation state) (startDialogue, chooseDialogueOption, dialogue gates, the placeholder greet node, conversation log)
- tests/session.sprint1.test.ts › startDialogue returns the placeholder node with id = npcId_greet → retired dialogue engine (Sprint 7 brings its own conversation state) (startDialogue, chooseDialogueOption, dialogue gates, the placeholder greet node, conversation log)
- tests/session.sprint1.test.ts › startDialogue returns correct node id for each NPC → retired dialogue engine (Sprint 7 brings its own conversation state) (startDialogue, chooseDialogueOption, dialogue gates, the placeholder greet node, conversation log)
- tests/session.sprint1.test.ts › startDialogue returns NPC_NOT_FOUND for unknown NPC → retired dialogue engine (Sprint 7 brings its own conversation state) (startDialogue, chooseDialogueOption, dialogue gates, the placeholder greet node, conversation log)
- tests/session.sprint1.test.ts › gated option is blocked when personality gate is not met → retired dialogue engine (Sprint 7 brings its own conversation state) (startDialogue, chooseDialogueOption, dialogue gates, the placeholder greet node, conversation log)
- tests/session.sprint1.test.ts › ungated option always succeeds regardless of personality traits → retired dialogue engine (Sprint 7 brings its own conversation state) (startDialogue, chooseDialogueOption, dialogue gates, the placeholder greet node, conversation log)
- tests/session.sprint1.test.ts › gated option succeeds after personality is raised above gate threshold → retired dialogue engine (Sprint 7 brings its own conversation state) (startDialogue, chooseDialogueOption, dialogue gates, the placeholder greet node, conversation log)

### Set aside

- A saved dialogue or conversation state: Sprint 7 brings its own conversation state. (for: Sprint 7)
- Echoed ids (at most 64 characters) in refusal messages may contain control characters; the UI should sanitise them when logging (security review, not acted on). (for: the UI sprint)
- Energy range and party membership are validated in the session combat operations, not again inside round resolution. (for: combat sprint)

## Saves

### Deliberate differences

- A fresh save format (version 1) with its own records, separate from the rules state. Browser saves are not carried over. Why: ADR-063 and the brief. Separate records fix the culture-dependent ordering found in the spike and make versioning safe: renaming a rules field cannot silently change the file. A strict custom enum converter (exact camelCase, no integers or numeric strings) and strict reading are used. (2026-10-28 (T11))
- Saves hold only what changes in play; fixed character details always come from current content. Why: Sprint 7 settled rule (Group 2). (2026-10-28 (T11))
- Loading against changed content reconciles instead of refusing, and the report lists what was adjusted. If any lineup member is gone the whole lineup is cleared (both adjustments reported); a non-empty lineup of the wrong size is cleared. Why: Content changes constantly during development; a sound save always loads. (2026-10-28 (T11))
- Size is checked on write as well as on read, in UTF-8 bytes (2 MB). Why: Today a too-large save is written and then shows as damaged, and the "bytes" limit counts UTF-16 characters. (2026-10-28 (T11))
- A mid-fight save carries the fight dice. Why: Same dice decision. (2026-10-28 (T11))
- Loading checks more than today: relationship ranges, unique ids, XP (finite, whole, 0..int.MaxValue; values above 1675 accepted as saved), fighter stamina, dice, ids of 1 to 64 characters on characters, lineup, reaction progress, combatants, archetypes, action queue, attack results and counter exchanges, and the top-level saved fight must be Active. Why: A broken file is refused instead of half-working. The id and Active checks came from the security review (a hand-edited save could otherwise soft-lock). (2026-10-28 (T11, build))
- The envelope slot is not compared with the slot it was loaded from. Why: Kept as today: desktop players copy save files between slots for backups. (2026-10-28 (T11))
- Save operations stay asynchronous. Why: A slow disk never freezes the game, Steam Cloud and console save systems are asynchronous, and it matches today async save operations. (2026-10-28 (T12))
- Saves are written to a temporary file (.json.tmp), flushed, and swapped into place. Disk-full errors map to "Not enough storage space to save", other IO errors to "Save storage is unavailable". Reads are bounded to the size limit plus one byte, and disk work runs off the caller thread. Why: Browser storage was atomic, plain files are not; a crash mid-save must not damage the existing save. The bounded read and thread hop came from the security review. (2026-10-28 (T12, build))
- Slot files are named manual-01 to manual-10, leaving room for autosaves. Slot arguments are ints (range check only); the metadata field is SavedAt (the TypeScript called it timestamp). GameSessionOptions.Saves is a nullable SaveStore (null makes a store over a memory backend). Why: ADR-058 "auto" kind stays reserved; SaveStore needs content. (2026-10-28 (T12))
- A missing field in a save file (for example the fight) is refused: the reader requires every member, and there is no backward compatibility with older formats (the upgrade step sets the fight to null). Why: Fresh format, version 1. (2026-10-28 (T11, T18))

### Tests changed

- src/persistence/backends.test.ts › writes, reads, and removes through the storage → the web-storage adapter tests become FileSaveBackend tests with the same failure messages (a file where the folder should be, or a folder where a file should be, makes the storage fail)
- src/persistence/backends.test.ts › maps a thrown read to STORAGE_ERROR → the web-storage adapter tests become FileSaveBackend tests with the same failure messages (a file where the folder should be, or a folder where a file should be, makes the storage fail)
- src/persistence/backends.test.ts › maps a thrown remove to STORAGE_ERROR → the web-storage adapter tests become FileSaveBackend tests with the same failure messages (a file where the folder should be, or a folder where a file should be, makes the storage fail)
- src/persistence/backends.test.ts › maps a generic write failure to STORAGE_ERROR as unavailable → the web-storage adapter tests become FileSaveBackend tests with the same failure messages (a file where the folder should be, or a folder where a file should be, makes the storage fail)
- src/persistence/backends.test.ts › treats a non-object throw as unavailable, not quota → the web-storage adapter tests become FileSaveBackend tests with the same failure messages (a file where the folder should be, or a folder where a file should be, makes the storage fail)
- src/persistence/backends.test.ts › maps a quota error (${label}) to STORAGE_ERROR with the space message → the 4 quota cases become a Theory over the disk-full error numbers the backend recognises (0x80070070, 0x80070027, ENOSPC 28)
- src/persistence/saveStore.test.ts › round-trips a populated reactionProgress, team, and narrativeState → no narrativeState any more
- src/persistence/saveStore.test.ts › save, load, and remove throw INVALID_SLOT for ${label} → a typed int slot: only 0, 11, and -1 remain (the string, fractional, and null slots cannot be expressed)
- src/persistence/saveStore.test.ts › a crafted save with __proto__ / constructor keys does not pollute Object.prototype → prototype-named keys are ignored by the strict reader
- src/persistence/saveStore.test.ts › missing narrativeState loads as null → the older-save normalization tests become upgrade-step tests in the new format
- src/persistence/saveStore.test.ts › missing team loads as [] → the older-save normalization tests become upgrade-step tests in the new format
- src/persistence/saveStore.test.ts › missing reactionProgress loads as {} → the older-save normalization tests become upgrade-step tests in the new format
- src/persistence/saveStore.test.ts › all three missing together load with defaults → the older-save normalization tests become upgrade-step tests in the new format
- src/persistence/saveStore.test.ts › an older save still lists as saved → the older-save normalization tests become upgrade-step tests in the new format
- src/persistence/validation.test.ts › rejects team with 1 entry → a wrong lineup size is cleared on load and reported, not refused
- src/persistence/validation.test.ts › rejects team with 3 entries → a wrong lineup size is cleared on load and reported, not refused
- src/persistence/validation.test.ts › accepts missing team field (backward compat) → a missing team field is now refused (fresh format, no backward compatibility)
- src/persistence/validation.test.ts › accepts npcs as empty Record → loads against the content, reporting a CharacterAdded adjustment for each missing character
- src/persistence/validation.test.ts › returns { valid: false, errors: [...] } with accumulated errors for multiple failures → value reasons accumulate; a wrong JSON type stops at the first error
- src/session/operations/saves.test.ts › returns INVALID_SLOT for out-of-range and non-integer slots → a typed int slot: the non-integer and non-number slots (1.5, "1", null, undefined, NaN, "x", "abc") cannot be expressed; 0, 11, and -1 remain
- src/session/operations/saves.test.ts › returns INVALID_SLOT for bad slots → a typed int slot: the non-integer and non-number slots (1.5, "1", null, undefined, NaN, "x", "abc") cannot be expressed; 0, 11, and -1 remain
- tests/session.saves.test.ts › NPC trust changes persist across save/load → trust is set through CharacterRules (the dialogue that changed it is retired)
- tests/session.saves.test.ts › save → load round-trip preserves state → the dialogue and conversation-log parts of the round trip are gone
- tests/session.saves.test.ts › returns INVALID_SLOT for non-numeric slot → a non-numeric slot is expressed as int.MinValue and int.MaxValue

backends 3 memory-backend tests carried over, saveStore.smoke 4, saveStore 38, persistence/validation 23, operations/saves 26, session.saves 13 carried over as named. New: file-backed tests (a fresh temporary folder per test, deleted afterwards; "survives a restart" is a new FileSaveBackend and a new GameSession on the same folder), identical bytes under en-US, sv-SE, and tr-TR, size in UTF-8 bytes, strict reading, the upgrade pipeline, reconciliation against changed content (14 tests), a mid-fight resume with the same dice, and long-fight saves (35 rounds is about 480 KB). The replace-blocked file test runs on Windows only (SkipUnless).

### Tests dropped

- src/persistence/validation.test.ts › validateGameState accepts narrativeState: null → retired narrative engine (Sprint 7 brings its own conversation state) (narrativeState persistence and its deep validation: 5 persistence checks and 4 deep-validation checks)
- src/persistence/validation.test.ts › validateGameState accepts a valid NarrativeState structure → retired narrative engine (Sprint 7 brings its own conversation state) (narrativeState persistence and its deep validation: 5 persistence checks and 4 deep-validation checks)
- src/persistence/validation.test.ts › validateGameState rejects narrativeState with non-string currentSceneId → retired narrative engine (Sprint 7 brings its own conversation state) (narrativeState persistence and its deep validation: 5 persistence checks and 4 deep-validation checks)
- src/persistence/validation.test.ts › validateGameState rejects narrativeState with non-array visitedSceneIds → retired narrative engine (Sprint 7 brings its own conversation state) (narrativeState persistence and its deep validation: 5 persistence checks and 4 deep-validation checks)
- src/persistence/validation.test.ts › validateGameState rejects narrativeState with non-object choiceFlags → retired narrative engine (Sprint 7 brings its own conversation state) (narrativeState persistence and its deep validation: 5 persistence checks and 4 deep-validation checks)
- src/persistence/validation.test.ts › rejects narrativeState with non-boolean choiceFlags value → retired narrative engine (Sprint 7 brings its own conversation state) (narrativeState persistence and its deep validation: 5 persistence checks and 4 deep-validation checks)
- src/persistence/validation.test.ts › rejects narrativeState with non-string visitedSceneIds entry → retired narrative engine (Sprint 7 brings its own conversation state) (narrativeState persistence and its deep validation: 5 persistence checks and 4 deep-validation checks)
- src/persistence/validation.test.ts › rejects narrativeState sceneHistory entry with non-string sceneId → retired narrative engine (Sprint 7 brings its own conversation state) (narrativeState persistence and its deep validation: 5 persistence checks and 4 deep-validation checks)
- src/persistence/validation.test.ts › rejects narrativeState sceneHistory entry with non-number timestamp → retired narrative engine (Sprint 7 brings its own conversation state) (narrativeState persistence and its deep validation: 5 persistence checks and 4 deep-validation checks)
- src/session/operations/saves.test.ts › resumes a game saved mid-conversation at the same dialogue node → retired dialogue engine (Sprint 7 brings its own conversation state)

### Set aside

- Combat sanity in a saved fight is thin: negative rank, power, speed, or energy, energy above the maximum, duplicate party ids, queue references, and empty parties are not checked, so a hand-edited mid-fight save could fail in combat as INTERNAL_ERROR (security review, not acted on). (for: a later hardening pass)
- A saved fight is not reconciled against content (the spec asks only for characters and lineup). (for: a later hardening pass)
- A non-empty nested RoundHistory is not refused on read (empty is guaranteed on write only); player.id length is not covered by the 64-character check; load report messages echo saved ids and the 200-character truncation can split a surrogate pair. (for: a later hardening pass)
- Cancellation of a save is not mapped (no token is plumbed; the old save survives); overlapping saves to the same slot report STORAGE_ERROR (no data loss); the .tmp file path trusts the host-provided folder (a reparse point there is not defended). (for: the platform and release sprint)
- Autosave and other save kinds ("auto" is reserved by ADR-058); Steam Cloud and console save backends. (for: a later sprint)

## Godot project

No TypeScript tests belong to this system.

### Deliberate differences

- The smoke run saves to user://smoke/saves, never the real save folder. Why: Verification must never touch a player saves. (2026-10-28 (T13))
- The smoke run exercises a fight and a mid-fight save (new game, lineup, encounter, orders, round, mid-fight save, load, byte-identical re-save, delete), not only a new game. Why: Proves the whole chain (session, combat, dice, file saves) under Godot .NET runtime at negligible cost. The stand-in enemy stamina is 400 so the fight survives round 1. (2026-10-28 (T13))
- Stand-in content lives in the Godot project as C# until Sprint 7 adds content packs. Why: ADR-062: stand-in content proves the systems; no content loader exists in this branch scope. (2026-10-28 (T13))
- verify.ps1 imports the Godot project only when godot/.godot does not exist at the start (the build creates godot/.godot/mono), finds Godot through the GODOT environment variable or the documented console exe, and runs the smoke headless with a 180 second timeout (import 300 seconds, process tree killed on timeout). Why: A missing editor, a hang, or a thrown exception must fail verify with a non-zero exit. (2026-10-28 (T13, build))
- The csproj adds ImplicitUsings and RootNamespace; GameHost is a plain class (not a Node) and is not instantiated yet. Why: The renderer and screens are later work. (2026-10-28 (T13, build))

### Tests changed

None.

### Tests dropped

None.

### Set aside

- Desktop export is not tested in this branch (export templates are not installed). Retargeting .NET is a one-property change if export objects to net10.0. (for: the export or release sprint)
- The Godot editor GUI was not opened; Godot.NET.Sdk 4.7.2 came from the local NuGet cache, so a fresh machine needs nuget.org on first build. (for: first setup on a new machine)
- Screens, overworld, and combat presentation (the 3D isometric overworld and FF7-style combat direction). (for: later sprints)

## Combat balance review (combat sprint)

Every combat and AI item the designer set aside on 2026-10-28 so that fights can be felt in Godot before any rule changes. Each is ported exactly as it plays today. The review decides which to change.

Combat math (T5):

- Energy means both this round segments and the total gained, so spending can lower ascension.
- Level 2 and 3 round-start energy is never applied.
- There is no 1 to 5 cap on Special segments, and enemies spend all (fractional) energy.
- Defenseless targets gain reaction energy but no XP.
- A fully mitigated hit counts as an attacker failure.
- Path buffs boost the signature defense rather than the defense used.
- Action paths never dodge or parry normal attacks.
- Rank KO fires whether or not the hit lands.
- The Crushing Blow -0.10 lasts the whole fight.
- The natural-20 defense rule (bugs.md, deferred 2026-05-31).
- resetRoundEnergy and getStartingSegments return here if the energy items are wanted (not ported because nothing calls them).

Enemy decisions (T6):

- The speed-advantage score jumps at zero.
- A critical ally lowers the DEFEND score through targetVulnerability.
- Rank influence is uncapped above 10.
- Exact-equality ties are kept as today.
- Finite-but-huge scores or weights can overflow to NaN and make tie-breaking throw: bound content values.

Round resolution (T7):

- Crushing Blow regardless of block success.
- Rank KO through any defense.
- Group attacks give no energy or XP and always force a Block.
- The counter chain is minimal.
- Enemy group attacks: enemies do not choose GROUP until the review designs them.

Fight setup (T8) and lineup (T4):

- Stamina still resets between fights.
- Which step (flat, additive percent, multiply) each synergy bonus uses.
- Whether a synergy that drives a stat negative should clamp (today the fight setup refuses it) and whether a MaxStamina bonus should also fill Stamina (today it does).

## Open creative questions

Logged, not settled (ADR-062). Creative work happens outside the pipeline (for example `/intuition-meander`); the systems support every option below.

- Does the player character fight alongside the companions? The fight setup takes the player combat stats from content, so omitting them makes a companion-only fight and including them makes the player a fighter.
- How large is the lineup? The size comes from content (today the test content uses 2 for the session tests and 3 for the demo and encounter fixtures).
- What are the synergy bonuses' values? The test content reproduces today bonuses; real values are content.
- Which step does each synergy bonus use (flat, additive percent, multiply)? This is also a balance question for the combat sprint.
- Do synergy bonuses stack, or does only the best apply? "Best only" and "all that qualify" are both supported; the test content uses best only.
- Raised during the build: should synergy show for the player alone when the lineup is empty? Today it is empty when the lineup is empty.
- Raised during the build: can a character without combat stats (townsfolk, for example) ever join a lineup later, for instance as a non-fighting passenger? Today the lineup refuses such a character.
- Raised during the build: should a synergy bonus on max stamina also fill current stamina at the start of a fight, and may a bonus ever lower a stat below zero? Today the first is yes and the second is refused.
- Raised during the build: should fights ever reroll when a mid-fight save is reloaded (for a designer-chosen "luck" mechanic)? Today a reload replays the same dice.

## Notes for Sprint 7

- Content authoring: whether content is authored as JSON files or in Godot's editor as resources converted on load is open. The catalog supports either: the rules take plain content records (GameContent, archetype records, encounters, synergy settings) and do not care where they came from. The stand-in content lives in the Godot project as C# until a loader exists.
- The condition system is built once in Sprint 7 and shared (character requirements, dialogue gates, and anything else that needs conditions); there is no characters-only condition system.
- Sprint 7 brings its own conversation state; the dialogue and narrative state, types, and error codes were dropped from the rules.
- Saves keep only character state and pair it with the current catalog on load, so content fixes reach existing saves; loading against changed content reconciles and reports what changed.
- One archetype record carries both the enemy decision profile and Sprint 7 reaction leanings.
- GameContent.Create takes (settings, characters, archetypes, synergy, encounters); a content loader only has to build those records.
- Characters have a name field; character archetype ids use the archetype record ids.
