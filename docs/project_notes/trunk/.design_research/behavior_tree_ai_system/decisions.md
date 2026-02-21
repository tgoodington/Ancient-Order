# Design Decisions: Behavior Tree AI System

**Date:** 2026-02-21

## Decisions Made

1. **Evaluation model: Utility scoring** — Chosen over classic behavior tree and hybrid approaches. Transparent, testable, data-driven. Every decision traceable to score breakdown.

2. **Multi-output factors** — Each factor returns scores for all 5 action types simultaneously. 7 factors vs ~20 for flat per-action approach. More natural modeling of combat reasoning.

3. **7 scoring factors + rank meta-factor** — OwnStamina, AllyInDanger, TargetVulnerability, EnergyAvailability, SpeedAdvantage, RoundPhase, TeamBalance. Rank coefficient scales factor influence by combatant rank (linear, floor 0.2).

4. **Combined (action, target) scoring** — All action+target pairs evaluated together rather than two-phase (action then target). Enables accurate cross-comparison ("ATTACK weak enemy" vs "DEFEND healthy ally").

5. **Combat perception layer** — Pre-computed readonly CombatPerception snapshot. Factors read perception, never raw CombatState. Enforces immutability and eliminates redundant computation.

6. **Path-based tie-breaking** — Elemental path determines action priority for score ties. 6 paths = 6 tie-break profiles. Generalizes to future combatants automatically.

7. **GROUP excluded via config flag** — GROUP actions filtered from candidates until Group Action Type is designed. Clean separation, no placeholder scoring.

## ECD Coverage

- **Elements**: Evaluator engine, 7 factors, 3 profiles, perception layer, rank coefficient, tie-break table, config ✓
- **Connections**: Round Manager integration, perception mediation, scoring loop, profile data lookup, type conformance ✓
- **Dynamics**: Scoring algorithm, factor logic (7 factors with bracket thresholds), rank scaling, tie-breaking, edge cases, invariants ✓
